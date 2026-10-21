# 5회차 DB 적용 (dbA) — Ver.05 → Ver.06 (2026-10-20)

## 다음 사람이 할 일 / 하지 말 일
- **할 일**: Unity를 열어 새로고침(`Assets/DB/MMM_DB_Ver.06.xlsx.meta`는 Unity가 만든다). `DbImport`가 가리키는 DB 파일을 Ver.06으로 바꿨는지 확인(코드 쪽에서 파일명 하드코딩 여부는 안 봄). 컴파일 후 `md_map2`(필드 넓히기 II)·`ps_ball`(2레벨)·`ps_ball3`(4레벨)이 트리에 보이는지 확인. 가능하면 Unity 메뉴 가져오기로 db.json을 한 번 더 만들어 아래 db.json과 같은지 본다.
- **하지 말 일(이미 함)**: Ver.06 복사·값 쓰기·새 노드 행 추가·db.json 재생성·대조. openpyxl로 Ver.06을 저장하지 말 것(도형 삭제). Ver.05는 건드리지 않았다.
- 주의: db.json의 `_source`가 `MMM_DB_Ver.06.xlsx`. 텔레메트리·`BalanceSim` 로그의 "DB …" 표기가 바뀐다.

## 산출물
- 엑셀: `Assets/DB/MMM_DB_Ver.06.xlsx` (Ver.05 복사 후 Excel COM `db_write.py set`·`rows`로만 수정, 쓰기 직전 자동 백업 `BalanceData/db_backup/auto/`)
- db.json: `Assets/Resources/DB/db.json` (`BalanceData/db/dbimport_nounity.py`로 생성. 먼저 Ver.05로 돌려 기존 파일과 줄바꿈·BOM 차이 외 **바이트 동일**함을 확인한 뒤 Ver.06을 만들고, git 인덱스와 같은 LF로 저장)
- JSON(입력): `BalanceData/r5/db/r5_set.json`, `BalanceData/r5/db/r5_rows.json` · 대조 스크립트 `BalanceData/r5/db/r5_verify.py` (다시 돌리면 대조표 출력)
- 노드 등록: `BalanceData/db/code_map.json`에 `"md_map2": 12066` 추가(`md_map` 아래)

## 바꾼 값 (이전 → 지금)
| 시트 | 키 | 열 | 이전 | 지금 |
|---|---|---|---|---|
| Boss | cleaner | Hp_Mul | 2.33 | 2.097 |
| Boss | angel | Hp_Mul | 93.33 | 84.0 |
| Boss | phantom | Hp_Mul | 128.33 | 115.5 |
| Boss | mover | Hp_Mul | 163.33 | 147.0 |
| Upgrade | ps_ball | Efx_MaxLv / Lv1 / Lv2 | 1 / 1 / 0 | 2 / 1 / 2 |
| Upgrade_Cost | 90055 | Cost_Lv1 / Lv2 | 2400 / 0 | 888 / 2666 (×0.9 → 화면 800·2400) |
| Upgrade | ps_ball3 | Efx_MaxLv / Lv3 / Lv4 | 2 / 0 / 0 | 4 / 3 / 4 |
| Upgrade_Cost | 90072 | Cost_Lv3 / Lv4 | 0 / 0 | 2.0e12 / 3.0e12 (Lv1·2는 9.9e11·1.4e12 그대로) |
| Upgrade | md_map | Efx_Lv0~4 | 1.5·1.35·1.2·1.1·1.0 | 1.68·1.5·1.35·1.2·1.1 (MaxLv 4 유지) |
| Upgrade_Cost | 90024 | Cost_Lv1~4 | 340·750·1700·4000 | 150·340·750·1700 |
| Upgrade | inf_gold | Efx_Lv1 | 1.025 | 1.04 |
| Upgrade | inf_atk | Efx_Lv1 | 1.05 | 1.07 |
| Ball | coin | Unlock / LvGold | 2500 / 0 | 1.4e5 / 1.8e5 |
| Ball | saw | 〃 | 3000 / 0 | 2.0e9 / 2.5e9 |
| Ball | hoe | 〃 | 3500 / 0 | 7.4e10 / 9.3e10 |
| Ball | spore | 〃 | 4000 / 0 | 2.2e12 / 2.8e12 |
| Ball | mill | 〃 | 5000 / 0 | 9.9e13 / 1.2e14 |
| Ball | bell | 〃 | 8000 / 0 | 2.1e14 / 2.6e14 |
| String | 301022 | KR·EN·JA·RU·UK·PT | 수확기 +2 (III) … | 수확기 +4 (III) / Harvester +4 (III) / 収穫機 +4 / Сборщик +4 / Збирач +4 / Coletor +4 |
| String | 201022 | 6개 언어 | "두 개까지" | "네 개까지"(EN four, JA 最大4つ, RU четырёх, UK чотирьох, PT quatro) |
| String | 202002 | 6개 언어 | "4레벨이면 원래 크기" | "4레벨 뒤 '필드 넓히기 II'까지 사면 원래 크기" (각 언어 같은 뜻) |
- Boss 확인: 이전 값이 지시와 같았다(2.33·93.33·128.33·163.33). ×0.9 = 2.097·83.997·115.497·146.997 → 지시대로 반올림해 2.097·84.0·115.5·147.0. king 0.35 그대로.
- Ball: sam(0 / 1500)·gold(-1 / 15000) 그대로. 기존엔 sam·gold 외 `Ball_LvGold`가 0이었는데 이제 전부 값이 들어갔다(코드 `ceil(lvGold × HV_LV_MUL[L])`가 0일 때와 다르게 동작하는지 코드 쪽 확인 필요).
- ps_ball3 Efx_Lv1·2(1, 2)는 그대로라 누적 +1·+2·+3·+4. ps_ball은 +1·+2.

## 새 노드 (본뜬 행: md_map / 302002·202002)
| 시트 | 내용 |
|---|---|
| Upgrade | Upg_ID 12066 · Upg_Key md_map2 · Upg_Name "필드 넓히기 II" · Str_Group_ID 22066 · Upg_Pre 12002 · Upg_Req 12002 · Branch md · Type map_zoom · Efx_MaxLv 1 · Cost_ID 90091 · Tier 1 · Sub 0 · Skill/Icon/Color 빈칸 · Efx_Lv0 1.1 · Lv1 1.0 · Lv2~4 0 |
| Upgrade_Cost | Cost_ID 90091 · Cost_Lv1 4000 · 나머지 0 (Gem 전부 0) |
| String | 302066 name "upg:md_map2:name" 6개 언어 (필드 넓히기 II / Widen Field II / フィールド拡張 II / Расширить поле II / Розширення поля II / Ampliar Campo II) · 202066 desc "upg:md_map2:desc" 6개 언어 · 그룹 22066 |
- 12066·90091·22066·302066·202066은 db.json에 없던 값임을 쓰기 전에 확인(Upgrade 최대 ID 13033, Cost 최대 90090).
- Upg_Pre가 지시대로 12002(md_map)라 트리 부모는 md_map(기존 md_map의 Upg_Pre 12003과 무관). 코드 `Defs.Nodes.cs`·`Game.cs:256`은 이미 `md_map2`를 참조한다.

## 대조 결과 (`python BalanceData/r5/db/r5_verify.py`)
- set 55칸 + 새 행 4개(Upgrade 1·Upgrade_Cost 1·String 2) **모두 기대값과 일치, BAD 0**.
- 시트별 행 수: Upgrade 90→91, Upgrade_Cost 90→91, String 1581→1583, 나머지(Skill·Ball·Ball_Efx·Weader·Boss·Const) 변동 없음. **의도 밖 칸 변경 0**, `_source`만 Ver.05→Ver.06.
- `git diff --stat Assets/Resources/DB/db.json`: 110 insertions, 55 deletions(165줄). 전부 위 표의 값 + 새 행 4개(+ `_source`). 줄바꿈이 어긋나 파일 전체가 바뀌는 것은 LF로 저장해 피했다(워킹 파일을 CRLF로 두면 전체가 diff로 잡히므로 Unity가 다시 쓸 때 줄바꿈이 바뀔 수 있음은 별개).

## 알릴 것
- Excel 잠금(`~$`) 없었음. 중단·오류 없음. Unity 조작·`Assets/Scripts` 수정·커밋 없음.
- `md_map` Efx_Lv4가 1.0 → 1.1이 됐다. 코드의 `MAP_ZOOM` 배열(Defs.Nodes.cs)이 DB 값을 읽는지 하드코딩인지는 코드 쪽에서 확인할 것(`md_map2` 미구매 시 `MAP_ZOOM[Lv]`를 쓰는 Game.cs:256).

## 2차 (사용자 결정 16:3x, T5-1 BossTune · 테스트_5.md) — Ver.06에 추가 쓰기
입력 JSON: `BalanceData/r5/db/r5_set2.json` (`db_write.py set` → `dbimport_nounity.py`, LF 저장). 엑셀 잠금 없음, Unity 조작 없음.
| 시트 | 키 | 열 | 1차 값 | 2차 값 |
|---|---|---|---|---|
| Boss | king | Hp_Mul | 0.35 | 1.75 |
| Boss | cleaner | Hp_Mul | 2.097 | 20 |
| Boss | angel | Hp_Mul | 84.0 | 740 |
| Boss | phantom | Hp_Mul | 115.5 | 1650 |
| Boss | mover | Hp_Mul | 147.0 | 1100 |
| Ball | coin | Unlock / LvGold | 1.4e5 / 1.8e5 | 58000 / 72500 |
| Ball | saw | 〃 | 2.0e9 / 2.5e9 | 1.6e9 / 1.95e9 |
| Ball | hoe | 〃 | 7.4e10 / 9.3e10 | 6.1e10 / 7.6e10 |
(spore·mill·bell·sam·gold 그대로)
- 대조: 11칸 전부 일치(BAD 0), 1차 db.json 대비 의도 밖 변경 0, 행 수 변동 없음, `_source` Ver.06. `git diff --stat db.json` 111 insertions / 56 deletions (HEAD 대비 누계).
- 주의: 이 2차 값이 최종이다. 1차 표의 Boss·coin/saw/hoe 값은 덮어써졌다.
