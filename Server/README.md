# MMM 로그 서버 (Cloudflare Worker + D1)

설계 기준: 바탕화면 `HANDOFF.md`. 게임 데이터는 서버에 올리지 않고, 분석용 로그만 받는다.

| 파일 | 용도 |
|---|---|
| `worker.js` | 대시보드 Worker 코드에 통째로 붙여 넣는다 (바인딩 `env.MMM_DB`, 키 `env.MMM_LOG_KEY`) |
| `migrate_v2.sql` | **지금 배포된 DB**를 새 구조로 바꾸는 SQL (D1 Console에서 한 번) |
| `schema.sql` | DB를 새로 만들 때의 스키마 |
| `queries.sql` | 분석 쿼리 13개 (DAU·신규·복귀율·스테이지 깔때기·보스 실패율·FTUE·에러·크래시·재화 정합성) |
| `wrangler.toml` | 명령줄 배포용 (대시보드로 배포하면 필요 없음) |
| `LogConfig.example.json` | 게임 쪽 설정 파일 형식 |

## 배포 순서 (대시보드)

1. **D1 바꾸기**: Workers & Pages → D1 → `moremustmosh_itch-logs` → Console에 `migrate_v2.sql` 전체를 붙여 실행.
   - 인덱스 `i_event_ts` 삭제, 지금 `logs`는 `logs_v1`로 이름만 바꿔 보존, 새 `logs`·`stage_end` 생성.
   - 마지막 SELECT 결과에 `logs`, `logs_v1`, `stage_end`가 보이면 끝.
2. **Worker 바꾸기**: Workers & Pages → `moremustmush` → Edit code → `worker.js` 내용으로 바꾸고 Deploy.
   - Settings → Bindings에 D1 `MMM_DB`가 있는지, Variables and Secrets에 `MMM_LOG_KEY`(Secret)가 있는지 확인.
3. **동작 확인** (터미널, `<KEY>`는 실제 키):
   ```
   curl -X POST "https://moremustmush.nonggle.workers.dev/?k=<KEY>" -H "Content-Type: application/json" -d "{\"game\":\"MMM\",\"event\":\"ftue\",\"uid\":\"0123456789abcdef0123456789abcdef\",\"eid\":\"curltest-1\",\"t\":0,\"data\":{\"step\":0}}"
   ```
   `ok`가 오면 D1 Console에서 `SELECT * FROM logs WHERE eid = 'curltest-1';` 로 확인하고 지운다(`DELETE FROM logs WHERE eid = 'curltest-1';`).
4. **게임에 키 넣기**: `Assets/Resources/LogConfig.json`의 `"key"`에 같은 키를 넣고 빌드. 이 파일은 `.gitignore`라 커밋되지 않는다(키가 비면 게임은 로그를 쌓기만 하고 보내지 않는다).

## 받는 형식 (게임 → 서버)

`POST /?k=<KEY>`, 본문은 로그 1개 또는 배열(최대 50개·20KB).

```json
{ "game": "MMM", "version": "0.0.2", "platform": "web", "uid": "32자리 16진수", "session": "12자리", "event": "stage_end",
  "t": 812.4, "eid": "세션-일련번호", "ts": 1792035992910, "data": { ... } }
```

- 응답: `ok`(저장, 같은 `eid`는 무시) · `forbidden`(403, 키 불일치) · 413(크기 초과) · 400(JSON·형식 오류, 본문에 이유).
- 거부(400): 모르는 `event`, `uid` 형식 오류, `eid` 누락, 재화(`in`·`out`·`bal`·`score`·`gem`) 음수나 1e40 이상, `stage_end`의 `stage`·`result`·`boss` 누락·이상.
- `ts`(기기 시각)는 `cts` 칸에 참고로 남긴다. 기준 시각 `ts`는 서버가 받은 시각.

## 이벤트별 `data` (게임 코드 `Assets/Scripts/Core/Telemetry.cs`)

| 이벤트 | data |
|---|---|
| `session_start` | `first`, `sessions`, `bal`(세이브가 있으면) |
| `stage_end` | `stage`, `result`(clear·fail·early_exit), `boss`, `score`, `in`, `out`, `bal`, `etc`, `up`[[Upg_ID, 레벨]], `codex`[버섯 번호], `gem`, `out_gem`, `boss_hp`(보스 fail·early_exit), `sec`(early_exit) |
| (스테이지 묶음) | 1~20스테이지는 판마다. 21스테이지부터 보스 구간(21~30 …)의 클리어를 모아 보스 클리어 때 한 줄: `stage`·`result`·`boss`·`bal`·`gem`·`score`는 마지막 판, `stage0`·`score0`은 첫 판, `in`·`out`·`etc`·`out_gem`은 합계, `up`은 나중 레벨, `codex`는 이어 붙임, `c`(판 수). 보스 실패·중도 포기는 그 판만 바로. 엔딩 뒤에는 세션마다 한 줄(`post`:1, `nf` 실패·중도 포기 수). 모으던 것은 앱이 뒤로 갈 때·다음 접속 때 보낸다 |
| `tree_up` | `id`(ring·stage·unlock_<꼬마>·klv_<꼬마>·awaken_<꼬마>·deco_<장식>), `lv`, `out`, `bal`, 균사석을 쓰면 `out_gem`·`gem` |
| `tree_build` | `id`(장식), `out`, `bal`, `out_gem`·`gem` |
| (세계수 묶음) | 세계수 화면에 있는 동안 `tree_up`·`tree_build`·`tree_act`·`quest_clear`는 모았다가 화면을 닫을 때 보낸다. 반복 레벨업(ring·klv_·deco_)과 같은 `tree_act`는 한 줄로 합친다: `c`(합친 횟수), `lv0`(첫 레벨), `out`·`out_gem`·`in`·`n`은 합계, `lv`·`bal`·`gem`은 마지막 값 |
| `tree_act` | `act`(tribute 공물·gift 선물·craft_rush 공방 즉시 완료), `id`(꼬마·장식, 공물은 없음), `in`(경험치·호감도), `n`(개수), 즉시 완료는 `out_gem` |
| `quest_clear` | `id`, `kind`(daily·weekly), `cur`:"exp", `in`(경험치), `bal`(세계수 경험치) |
| `ftue` | `step` (1 새 게임 · 2 첫 발사 · 3 첫 판 끝 · 4 첫 업그레이드 · 5 첫 세계수) |
| `crash` | `stage`, `ver` |
| `error` | `msg`, `at`, `type` (세션당 3건) · 연결 실패 모음은 `msg`:"net_fail", `count` |
| `game_clear` | `play`, `sessions`, `stage`, `rounds`, `bal`, `up`(전체) |

- 버섯 번호: 식용 1·약용 2·독 3 × 100 + 번호 (ed12 → 112, ps0 → 300).
- `etc`: 직전 `stage_end` 이후 업그레이드·이번 판 말고 골드가 바뀐 몫(세계수 성장·수확 보따리·공방 등 합계). `직전 bal + in − out + etc = bal`. 세이브를 고쳐 골드를 늘리면 `etc`가 크게 튄다 → `queries.sql` 11·12번.

## 게임 쪽 동작 요약

- 로그 큐: `persistentDataPath/logq.dat` (SecureStore 암호화, 200건). 전송: 접속 직후·스테이지 종료·창이 뒤로 갈 때, 50건·20KB씩. `ok`만 큐에서 지움. 실패 시 15·30·60초 3회 뒤 다음 전송 때.
- 세이브: `persistentDataPath/save.dat` (+ `.bak`, SecureStore). 예전 PlayerPrefs 세이브는 첫 저장 때 옮기고 지운다.
- 기기 ID: `persistentDataPath/uid.txt` + PlayerPrefs `tlm_uid` (세이브 암호 키의 재료라 두 곳에 둔다).
- 웹: persistentDataPath = 브라우저 IndexedDB. 템플릿의 `autoSyncPersistentDataPath`로 바로 반영 (2026-10-15 WebGL 빌드에서 저장·새로고침 후 이어하기 확인).
- 에디터 플레이·밸런스 시뮬은 보내지 않는다 (`Telemetry.SEND_IN_EDITOR`).
