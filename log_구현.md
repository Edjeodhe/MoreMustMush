# 서버 로그 — 클라이언트 구현 (log.md 표 기준)

코드: `Assets/Scripts/Core/Telemetry.cs` (웹·exe 공통). 2026-10-14.

## 연결하는 법

`Telemetry.ENDPOINT`(Telemetry.cs 위쪽)에 Cloudflare 주소를 넣고 다시 빌드한다. 비어 있으면 보내지 않고 기기에 쌓아 두기만 한다(최대 300건, 넘치면 오래된 것부터 버림). 주소를 넣은 빌드가 실행되면 쌓인 것부터 보낸다.

**서버(Worker)가 해 줄 것**
- `POST` + `Content-Type: application/json`, 한 요청 = 로그 한 건.
- 웹 빌드(itch.io)는 다른 도메인이라 CORS가 필요하다: `OPTIONS` 미리 요청에 204, 모든 응답에 `Access-Control-Allow-Origin`(itch 주소 또는 `*`)·`Access-Control-Allow-Headers: Content-Type`·`Access-Control-Allow-Methods: POST, OPTIONS`. CORS 헤더가 없으면 브라우저가 응답을 막아 클라이언트가 실패로 보고 다시 보낸다(중복 저장).
- 2xx = 저장됨(큐에서 지움). 4xx = 형식 오류로 보고 버림(다시 안 보냄). 그 밖(5xx·연결 실패) = 30초 뒤 다시 보냄.

## 보내는 형식 (한 건)

```json
{ "uid": "b6d29a3b…", "name": "stage_end", "ver": "0.1.0", "plat": "web", "sid": "3f1c…", "t": 812.4,
  "ts": "2026-10-14T04:00:15.123Z", "data": "{\"stage\":10,\"result\":\"fail\",…}" }
```

| 필드 | 내용 |
|---|---|
| `uid` | 개인 ID. 첫 실행 때 기기에 만든다(PlayerPrefs `tlm_uid`, 웹은 브라우저 저장소). 새 게임(초기화)을 해도 그대로 |
| `name` | 로그 이름 (아래 표) |
| `ver` | 빌드 버전 (Player Settings 버전, 지금 0.1.0) |
| `plat` | `web` / `exe` (`editor`는 기본으로 안 보냄) |
| `sid` | 실행(세션)마다 새로 만드는 ID — 같은 세션 로그 묶기용 |
| `t` | 세션 시작 후 경과 초 (이벤트 순서·시점) |
| `ts` | 기기 시각(UTC) |
| `data` | 로그별 내용을 JSON 문자열로 (최대 4,000자) |

## 로그별 `data`

| 로그 | 보내는 때 | data |
|---|---|---|
| `session_start` | 게임 실행(타이틀 전) | `first`(첫 실행), `sessions`(이 기기 누적 실행 수) |
| `stage_end` | 정산 직후 | `stage`, `result`, `boss`, `score`, `gain`(정산 골드), `spent`(직전 판 뒤 업그레이드에 쓴 골드), `gold`(정산 후 보유), `ups`(`[노드 ID, 올린 뒤 레벨]` 목록), `codex`(이번 판 새 도감 종), 균사석을 쓰거나 가졌으면 `spent_gem`·`gem`, `early_exit`면 `sec`(나간 시점, 판 시작 후 초), 보스 `fail`·`early_exit`면 `boss_hp`(남은 체력 %, 분신 포함 합) |
| `tree_up` | 세계수 레벨 업 | `id`("tree"), `lv`(올린 뒤), `gold` |
| `tree_build` | 세계수 건물 짓기 시작 | `id`(건물 ID), `gem` |
| `quest_clear` | 의뢰 보상 받기 | `id`(예: d_rounds), `kind`(daily/weekly), `exp`(보상은 세계수 경험치) |
| `ftue` | 기기당 단계마다 한 번 | `step`: 1 새 게임 시작 · 2 첫 발사 · 3 첫 판 끝 · 4 첫 업그레이드 · 5 첫 세계수 화면 |
| `crash` | 다음 실행 때 (지난 실행이 비정상 종료) | `stage`(마지막 판), `ver`(그때 버전) |
| `error` | 에러·예외 로그 | `msg`, `at`(호출 위치), `type` — 세션당 3건. 전송 실패는 `msg: "net_fail"`, `count`로 연결된 뒤 따로 보냄 |
| `game_clear` | 엔딩(도감 완성, `G.ending`) — 기기당 한 번 | `play`(누적 플레이 초), `sessions`, `stage`, `rounds`, `nodes`(1레벨 이상 노드 전부) |

- `result`: 일반 `clear` / `early_exit`, 보스 `clear` / `fail` / `early_exit` ("지금 끝내기"로 나가도 그 전에 보스를 쓰러뜨렸으면 `clear`).
- 크래시 판정: 실행 중 표시를 켜 두고, 앱이 뒤로 가거나(포커스 잃음·일시정지) 꺼질 때 지운다. 다음 실행에 표시가 남아 있으면 `crash`. 웹에서 탭을 보고 있는 채로 닫으면 크래시로 셀 수 있다.
- 종료 로그는 없다(log.md대로): 마지막 로그의 `t`로 플레이 시간을 계산한다.
- 밸런스 시뮬(BalanceSim)과 에디터 플레이는 보내지 않는다. 에디터에서 시험하려면 `Telemetry.SEND_IN_EDITOR = true`.

## 붙인 곳

| 로그 | 코드 |
|---|---|
| session_start · crash | `GameFlow.Awake` → `Telemetry.Init()` |
| stage_end | `RoundSim.Finish()` 끝 (`Telemetry.StageEnd`), 마지막 스테이지 기록은 `GameFlow.BeginRound` (`StageStart`), 중도 이탈 시점은 `GameFlow` "endnow" (`RoundSim.earlyExitSec`) |
| tree_up · tree_build | `Game.TreeUp`, `Game.BuildStart` |
| quest_clear | `Game.QuestClaim` |
| ftue | "newgame", `RoundSim.Launch`(첫 판), `Finish`(첫 판), `BuyNode`·`BulkBuy`, `GameFlow.EnterFarm` |
| game_clear | `GameFlow.AfterSettle` (엔딩 켜질 때) |
| error | `Application.logMessageReceived` |
