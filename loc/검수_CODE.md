# 다국어(i18n) 코드 쪽 점검 보고서

대상: `C:\Project\MoreMustMush` (브랜치 KHD, 0f5ce98) · 점검일 2026-10-19 · 프로젝트 파일은 고치지 않았다(보고만).
검사 스크립트·중간 결과: `scratchpad/loc/` (scan.py, fmt.py, callkeys.py, fonts.py, scene.py, plural.py, junk.py 와 각 *.txt 결과)

## 요약

| 구분 | 결과 |
|---|---|
| Loc 호출 | `Loc.T` 278곳, `Loc.F` 319곳 (리터럴 키 550개, 동적 키 47곳) |
| 코드의 한국어 리터럴 | 1,239개 (Debug/Exception 67개 제외 1,172개). 보간 문자열(`$"…한글…"`)에 한국어를 직접 쓴 곳은 **0곳** |
| DB String 시트 | 1,549줄 (ui 1,323 · upg 180 · ball 24 · wea 12 · boss 10). 번역 빈칸 51줄 중 **실제 화면 문구는 3줄**, 나머지는 쓰레기 줄 |
| DB에 없는 키 | **21개** (실제 화면 문구 19개 + Tooltip 2개). 키가 아닌 한국어 아닌 형식 키 11개는 별도 |
| 형식 안전성 | Loc.F 319곳 **모두 자리 표시 수와 인자 수가 맞음**. 번역 쪽 자리 표시 초과·누락·리치텍스트 태그 불일치·`{n}/{m}` 누락 **0건** → FormatException 날 곳 없음 |
| 버섯 75종 | 이름·설명 150문구 **모두 DB에 있고 4개 언어 번역 완료** |
| 폰트 | 모든 폰트가 Static(구운) 아틀라스. 실제 문구 중 **'뉴'(메인 메뉴)·'썼'(…다 썼어요) 글리프가 어느 폰트에도 없음** |

### 가장 먼저 고칠 것 (심각도 순)

1. **높음** — 옵션 창·언어 창 문구가 DB에 없음: `소리 설정 / 효과음 / 배경음 / 메인 메뉴 / 음소거: 켜짐·꺼짐 / 언어`. 기본 언어가 영어인데 영어 사용자에게 한국어로 보이고, 특히 **언어 고르기 창 제목이 '언어'(한국어)**. 게다가 '메인 메뉴'의 '뉴'는 구운 폰트에 없어 한국어에서도 □로 나온다.
2. **높음** — 씬에 영어로 박힌 글자(LocText 없음, 코드도 안 바꿈): 메인 메뉴의 `Start / Option / Exit Game / Reset`, MainGame의 `MushDex / MushTree / Stage Start`, 스테이지 표시 `"N Stage"`(어순까지 영어), Stage의 `Combo`. 한국어·일본어·러시아어·우크라이나어 모두 영어로 보인다.
3. **중간** — LocTools 키 수집기의 사각지대: `$"{Loc.T("…")}"`처럼 **보간 문자열 구멍 안의 Loc 호출 키는 수집되지 않는다**. 11개 중 2개가 이미 DB에 없다(`보스 {0}에게 졌어요… 골드 −{1} (−{2}%)`, `… 골드 −{1}`).
4. **중간** — 최신 노드 `ed_rare0`(희귀 포자) 이름·설명·효과 문구 3개 미번역/누락 → 모든 언어에서 한국어로 보임.
5. **중간** — 복수형: 숫자+세는 말 Loc.F 81곳(고유 키 69개, 의뢰 틀 4개). 번역가가 대부분 "명사: {0}" 꼴로 피했지만, `1 clones remain!`, RU `1 видов`, UK `2 рівнів` 같은 실제 깨짐이 남아 있다. 같은 키 `{0}종`이 장식 개수에도 쓰여 EN `"{0} species"`가 장식에 붙는 문맥 오류도 있다.
6. **중간** — 처음 실행 언어가 기기 언어와 상관없이 영어(`Loc.DefaultLang = "en"`).

---

## 1. 누락 키 / Loc을 거치지 않는 한국어

### 1-1. DB String 시트에 없는 키 (화면에 한국어로 보임)

| 파일:줄 | 키 | 심각도 |
|---|---|---|
| Assets/Scripts/UI/LangPopup.cs:24 | `언어` (언어 창 제목) | 높음 |
| Assets/Scripts/UI/OptionWindow.cs:61 | `소리 설정` | 높음 |
| Assets/Scripts/UI/OptionWindow.cs:62 | `효과음` | 높음 |
| Assets/Scripts/UI/OptionWindow.cs:63 | `배경음` | 높음 |
| Assets/Scripts/UI/OptionWindow.cs:67 | `메인 메뉴` (글리프 '뉴' 없음 → □) | 높음 |
| Assets/Scripts/UI/OptionWindow.cs:75 | `음소거: 켜짐` / `음소거: 꺼짐` | 높음 |
| Assets/Scripts/Core/Defs.Nodes.cs:353 | `에픽 이상 가중치 ×{0}` | 중간 |
| Assets/Scripts/UI/StageSettle.cs:60 | `얻은 점수 → 골드 (×{0})` | 중간 |
| Assets/Scripts/UI/StageSettle.cs:68 | `신기록! {0} {1}` | 중간 |
| Assets/Scripts/UI/StageSettle.cs:78 | `보스 {0} 처치! 스테이지 {1} 열림` | 중간 |
| Assets/Scripts/UI/StageSettle.cs:79 | `보스 {0}에게 졌어요… 골드 −{1}` (보간 구멍 안 — LocTools가 못 봄) | 중간 |
| Assets/Scripts/UI/SettleScreen.cs:144 | `보스 {0}에게 졌어요… 골드 −{1} (−{2}%)` (보간 구멍 안 — LocTools가 못 봄) | 중간 |
| Assets/Scripts/UI/UnlockGuide.cs:67 | `눌러서 확인해 보세요` | 중간 |
| Assets/Scripts/UI/WorkshopPanel.cs:77 | `{0} 완성! 강화 재료로 보관했어요` | 중간 |
| Assets/Scripts/UI/WorkshopPanel.cs:83 | `오늘 즉시 완료를 다 썼어요` (글리프 '썼' 없음 → □) | 중간 |
| Assets/Scripts/UI/WorkshopPanel.cs:93 | `꼬마가 합류하면 열려요` | 중간 |
| Assets/Scripts/UI/WorkshopPanel.cs:142 | `{0} 합류 시 해금` | 중간 |
| Assets/Scripts/UI/WorkshopPanel.cs:181 | `빈 칸` | 중간 |
| Assets/Scripts/UI/LocFonts.cs:23, :25 | `[Tooltip("…")]` 에디터 문구 (화면 아님) | 낮음(넣지 말 것) |

대부분 최근 커밋(261019 옵션·해금 안내·공방)에서 생긴 문구로, **"MoreMush/다국어/3 번역표 갱신"을 아직 안 돌린 상태**로 보인다.
수정: 번역표 갱신 실행 → 번역 → **4 폰트 굽기**까지 다시 (새 글리프 '뉴'·'썼' 때문에 굽기가 필수).

### 1-2. Loc을 거치지 않고 화면에 나가는 한국어 리터럴 (코드)
- **없음.** Loc 호출 밖의 한국어 리터럴 652개는 전부 데이터 테이블(`Defs*.cs`의 `nKo/dKo/textKo/traitKo` → 프로퍼티에서 `Loc.T`)이거나, 배열을 나중에 `Loc.T(...)`로 감싸 쓰는 것(`OutGameController.TAB_NAMES`→:259, `MushDexPopup.TAB_NAMES`→:113, `FarmView.HELD/LAND/PERCH_LINES`→:166/257/315, `RoundFlow.HV_OVER_MSG`→:25, `HarvesterShop.cs:62`)이라 정상.
- 화면에 안 나가는 것: `Defs.Nodes.cs:164 TIER_NAMES`(아무도 안 씀, 죽은 코드), `Util.cs:104 "이"/"가"`(죽은 함수 Iga), `OutGameController.cs:53 "수확석"`(오브젝트 이름 찾기), `[Tooltip]/[Header]` 6곳.

### 1-3. 씬·프리팹 (Loc 밖 글자)
LocText 키 24종은 모두 DB에 있고 번역됨. 문제는 **LocText가 없고 코드도 글자를 바꾸지 않는 영어 배치 글자**:

| 위치 | 글자 | 심각도 |
|---|---|---|
| MainMenu.unity Canvas/Button_Start · Button_Option · Button_Exit · Button_Reset /Text (TMP) | `Start` `Option` `Exit Game` `Reset` | 높음 (첫 화면) |
| MainGame.unity Canvas/MainBG/Button_MushDex, Button_MushTree /Text (TMP) | `MushDex` `MushTree` | 높음 |
| MainGame.unity Canvas/MainBG/Stage_Image/Button_StageStart/Text (TMP) | `Stage Start` | 높음 |
| MainGame.unity Canvas/MainBG/Stage_Image/Stage_Text + MainGameController.cs:26,50,113 | `"N Stage"` 틀을 씬 글자에서 읽어 `N`만 숫자로 바꿈 → 모든 언어에서 "12 Stage" | 중간 |
| Stage.unity Canvas/Stage_BG/Combo/ComboText | `Combo` | 낮음~중간 |

DB에 이미 옛 UI의 `시작하기 / 옵션 / 초기화 / 종료 / 스테이지 {0}` 줄과 번역이 남아 있다(아래 8절 "옛 문구 436줄"). 수정: 새 버튼 글자에 LocText를 붙이고 key를 기존 줄로 맞추거나(2 씬 글자 연결 — 단, 한글이 없으면 key가 비므로 수동 지정), `MainGameController.RenderStage`를 `Loc.F("스테이지 {0}", n)`로 바꾼다.

---

## 2. 보간·연결로 쪼개진 번역

- `$"…한글…"` 직접 사용: **0곳** (모두 Loc.F로 정리돼 있음 — 좋음).
- **LocTools 사각지대 (중간)**: `Assets/Editor/LocTools.cs:326` `TOKEN` 정규식은 보간 문자열 안의 `"`를 끝으로 보므로 `$"{Loc.T("엔딩")}…"`의 키 `엔딩`이 리터럴로 잡히지 않고, 보간 보고(`:254`)도 구멍 앞 조각 `{Loc.T(`에 한글이 없어 보고하지 않는다. 영향 받는 키 11개:
  `EndingScreen.cs:27`(엔딩/버섯 도감 {0}종 완성!/화면을 누르면 계속해요), `SettleScreen.cs:144`(**DB 없음**), `:298`(총 피해 {0}), `:301`(이름/피해 비율/피해/타격/처치), `:311`, `StageSettle.cs:63`(받은 골드), `:79`(**DB 없음**).
  수정: 키 수집을 토큰이 아니라 `Loc.T(` / `Loc.F(` 호출의 첫 인자 파싱으로 바꾸거나(검사 스크립트 `fmt.py`의 `parse_call` 방식), 보간 구멍 안에서 Loc 호출을 금지하는 규칙을 둔다.
- 조각 연결: `Loc.T(a) + " " + Loc.T(b)`처럼 **한 문장을 조각으로 만든 곳은 찾지 못함**. 있는 것은 "제목 + 값", " · " 나열, `\n` 줄바꿈 연결뿐이라 어순 문제는 작다(낮음). 예: `FarmTabWindow.cs:570` (`꼬마 · 등급 · 걸이형 · 보유`), `OutGameController.cs:167` (`보관 … ` + `가득!`), `StageSettle.cs:62` (`보너스 골드` + `"  +"` + 값).

---

## 3. 한국어 조사 처리

- `Assets/Scripts/Core/Util.cs:100-105 U.Iga()`: 받침 따라 이/가를 붙이는 함수. **아무도 호출하지 않는 죽은 코드**이고, `!Loc.IsKo`면 단어만 돌려주므로 다른 언어에 한국어 조사가 붙을 위험은 없다. String 시트의 `이`(900518)·`가`(900519) 줄은 이 함수의 리터럴을 LocTools가 주운 쓰레기 줄(무해). 수정: 함수 삭제 또는 LocTools 제외.
- 키 문구의 조사 처리(한국어 화면에서만 문제, 번역엔 영향 없음, 낮음):
  - "(으)로·을(를)·이(가)·은(는)" 병기 9개 (`{0}이(가) 세계수에 왔어요!` 등) — 정상적 우회.
  - 자리 표시 바로 뒤에 조사를 고정한 키 18개 — 값에 따라 틀린다. 예: `Lv.{0}이 되면 각성할 수 있어요`(Lv.2이 ✗), `없어진 스킬에 쓴 골드 {0}을 돌려받았어요`(숫자 끝소리 따라 를), `★{0} 진화에 {1}{2}이 필요해요`, `Lv 업에 {0}{1}이 필요해요`.
  수정: 한국어 전용으로 `{0:josa=이가}` 같은 확장 서식을 Loc.F에 두거나(ko일 때만 처리) "(이)가" 병기로 통일.

---

## 4. 하드코딩 영어·단위

| 파일:줄 | 내용 | 심각도 / 메모 |
|---|---|---|
| Tree/TreeNodeView.cs:232 | `"MAX"`, `$"MAX {L}"`, `$"MAX {L}/{n.max}"` | 낮음~중간 (RU 번역은 다른 곳에서 "макс.") |
| Tree/TreeTooltip.cs:30 | `$"LVL {L} / ∞"`, `$"LVL {L} / {n.max}"` | 낮음~중간 |
| Tree/TreeNodeView.cs:220, :231 | `$"{U.Fmt(cost.gold)}G"` — 같은 줄의 골드+균사석 경우는 `Loc.F("{0}G + 균사석 {1}")`(RU "зол.")라 **한 화면에서 G/зол. 섞임** | 낮음 |
| UI/CurrencyBar.cs:23 | `" G"` 접미사 | 낮음 |
| View/RoundView.cs:448 | `$"+{…}G"` (숫자 그림 글꼴) | 낮음(언어 무관 연출) |
| Round/RoundSim.Effects.cs:290 | `"SUPER COMBO!"`, `$"{n} COMBO"` (Lettering 그림 대체 대상) | 낮음(의도된 언어 무관 연출) |
| Flow/StageHUD.cs:130, View/RoundView.cs:457 | `"x" + 배속/콤보` | 문제 없음 |
| Loc.F에 넘기는 **한글 없는 형식 키 11개** — `OutGameController.cs:210 "{0} Lv.{1}"`, `FarmTabWindow.cs:404 "{0} {1}/{2}"`, `:424 "…Lv.{2}/{3}"`, `:431 "★{0} · Lv.{1}"`, `:452`, `:559`, `:570`, `:628`, `:650`, `:681`, `View/FarmCritter.cs:111 "{0} {1} Lv.{2}"` | LocTools는 한글 없는 키를 모으지 않아 DB에 없음 → 에디터에서 매번 "[Loc] 없는 문구" 경고, "Lv."를 언어별로 못 바꿈 | 낮음. 수정: "Lv."가 든 3~4개만 DB 키로 만들고 나머지는 `string.Format`으로 |

- **숫자 축약** `Util.cs:55 Units = K, M, B, T, Qa…`: 모든 언어 공통. 인크리멘탈 장르 관례상 허용 가능(낮음). 일본어에서 万/億을 원하면 Fmt에 언어별 단위표 필요.
- **시간 단위**: `Util.cs:82 FmtTime`, `Game.Farm.cs:16`, `ReturnPopup.cs:84`, `StageHUD.cs:119`(`{0:F1}초`) 모두 `Loc.F`로 번역됨 — 좋음. `Mmss`("5:12")는 언어 무관.

---

## 5. 복수형 (Loc에 복수 지원 없음)

- 숫자 바로 뒤에 세는 말(개/번/종/초/분/시간/일/단계/칸/회/마리…)이 오는 **Loc.F 호출 81곳, 고유 키 69개** + 의뢰 틀(`{n}`) 4개. 목록: `scratchpad/loc/plural.txt`.
- 번역 상태: RU/UK는 대부분 "Собрано: {0}", "Слотов: {0}"처럼 **명사: 숫자** 꼴로 비켜 갔고, 초·분은 "с/мин/ч" 약어라 안전. EN은 "s/m/h" 약어로 안전한 곳이 많다.
- **실제로 깨지는 예 (중간)**:
  - EN `"{0} clones remain!"` — RoundSim.Boss.cs:185, 1마리 남으면 "1 clones remain!"
  - EN `"{0} temporary harvesters (6 sec)"`, `"{0} small temporary harvesters, 4 sec"` — Defs.Nodes.cs:312, :315 (1레벨 값 1)
  - EN `"Converted {0} diamonds into …"` — SaveMigration.cs:58
  - EN `"<b>{0} Tree Mushroom Slots</b>"` — FarmTabWindow.cs:346
  - EN 의뢰 `"Harvest Mushrooms {n} times"` / `"Harvest {m} {n} times"` (목표 1일 때)
  - RU `"{0} видов"` — FarmTabWindow.cs:645 (1 вид / 2 вида / 5 видов)
  - UK `"{0}: покращено одразу на +{1} рівнів!"` — TreeView.cs:195 (k=2~4면 "рівні")
  - **문맥 충돌**: FarmTabWindow.cs:645 `Loc.F("{0}종", 장식 개수)` — 같은 키가 버섯 종 수에도 쓰여 EN "{0} species"가 장식에 붙음. 수정: 장식용 키를 `{0}가지` 등 별도 문구로.
  - (참고) RU/UK 성(性) 일치: `"{0} открыт!"`, `"{0} эволюционировал в {1}!"`, `"Скин {0} куплен!"` — 여성·중성 명사가 들어오면 틀림(낮음).
- 수정 제안: `Loc.P(key, n, args)` 추가 — String 시트에 복수형 줄(예: `키#one`, `키#few`, `키#many`, `키#other`)을 두고 CLDR 규칙(ru/uk: n%10==1&&n%100!=11 → one, 2–4 → few, 나머지 many; en: 1 → one)으로 고른다. 우선 위 7곳만 적용해도 충분.

---

## 6. 형식 안전성 (Loc.F ↔ 번역 자리 표시)

호출부 319곳을 실제로 파싱(괄호·문자열 고려)해 인자 수를 세고, 키·4개 번역의 `{숫자}` 최대값과 대조했다 (`fmt.py` → `fmt_report.txt`).

- 키 자리 표시 > 인자 수 (원문도 예외): **0곳**
- 키보다 인자가 많은 곳 / 자리 표시 없는 Loc.F: **0곳**
- 번역 자리 표시가 인자 수 초과 (FormatException → 한국어로 대체): **0곳**
- 번역이 자리 표시를 빠뜨린 곳: **0곳**
- 의뢰 틀 `{n}`·`{m}`(Game.Quests.cs:52 `Replace`): 4개 언어 모두 보존
- 리치텍스트 태그(`<b> <color> <size> <sprite> <mark> <pos>`) 개수 불일치: **0건**
- 짝 안 맞는 `{`/`}`: **0건**
- `Loc.T`에 `{0}`가 든 키(Loc.F를 써야 할 곳): **0곳**
- 중복 키(같은 Str_KR 두 줄): **0건**

→ 현재 데이터로는 형식 오류가 날 곳이 없다. 다만 이 검사가 도구에 없으므로(LocTools는 개수만 셈) **번역표 갱신 때 위 검사를 자동화**할 것을 권한다(중간). `Loc.F`의 catch(`Loc.cs:69-74`)가 한국어로 대체하므로 실수해도 크래시는 없지만 화면이 조용히 한국어가 된다.

---

## 7. 동적 키 (Loc.T(변수))

동적 Loc.T 47곳 모두 확인:
- `Defs.cs` 버섯 표 `SPECIES_RAW`(Defs.cs:155~): **75종 이름(a[1])·설명(a[8]) 150문구 모두 DB에 있고 EN/JA/RU/UK 모두 채워짐.** `Species.n/d` 프로퍼티(Defs.cs:140-141)가 `Loc.T(nKo/dKo)` — 정상.
- 분류(Cat)·등급(Tier)·프리셋·Weather·Theme·CodexSet·Flame·Zone·Kid(trait/favWhere)·DecoGrade·Deco·Blessing·BeautyStep·Skin·QuestDef·BossDef(이름·기믹)·LocMap(TRAIT_TEXT, RECORD_NAMES)·LocLines(꼬마 대사): 모두 `…Ko` 원문 + `Loc.T` 프로퍼티 구조이고, 해당 문구는 모두 DB에 있고 번역됨.
- DB 표: Upgrade/Skill/Ball(이름·타입·모양)/Weader/Boss 이름 모두 String에 있음. **미번역은 `희귀 포자`(303033 `upg:ed_rare0:name`)와 그 설명(203033)뿐** + 효과 문구 `에픽 이상 가중치 ×{0}`(누락). 중간.
- 꼬마 대사·FarmView 말풍선(HELD/LAND/PERCH)·탭 이름: 모두 DB에 있음.
- (설계 메모, 낮음) 키가 한국어 원문이라 **같은 한국어 = 같은 번역**. 문맥이 다른 동음 문구(위 `{0}종`)를 구분할 방법이 없다. 필요하면 `Loc.T("키", ctx: "deco")` → `Str_Name`으로 구분하는 길을 열어 두면 된다.

---

## 8. String 시트 쓰레기 줄

LocTools.ExportTable(`LocTools.cs:250-256`)은 `Assets/Scripts`의 **한글이 든 모든 문자열 리터럴**(Debug.Log/Exception 줄만 제외)을 키로 넣는다. 그래서 화면에 나가지 않는 문자열도 들어간다.

| 종류 | 줄 수 | 출처 | 영향 |
|---|---|---|---|
| 버섯 표 옵션 태그 `after:양송이 cnt:10`, `spore:slow after:… cnt:20` 등 | 45 | `Defs.cs` SPECIES_RAW의 a[7] 옵션 문자열에 한글 버섯 이름이 들어 있어서 | 무해(번역 빈칸, 화면 안 나감). 번역가 혼란·폰트 굽기 글자만 늘림 |
| `이`, `가` (900518/519) | 2 | `Util.cs:104` Iga() 리터럴 | 무해 |
| 901323 `세트 밖이지만 … (TextLangFont)` | 1 | `LocFonts.cs:23` `[Tooltip]` 의 **예전 문구**. 지금 Tooltip 문구가 바뀌어 다음 갱신 때 새 쓰레기 줄 2개(:23, :25)가 또 추가된다 | 무해. 단 이 줄 때문에 **'폰' 글리프가 구운 폰트에 없음** 경고가 생긴다 |
| `[Tooltip]/[Header]` 문구 (901204, 901218, 901219, 901221, 901302, 901303) | 6 | OutGameController/StageController/TreeView/MainMenu 속성 | 무해 |
| `수확석` (901205) | 1 | `OutGameController.cs:53` 씬 오브젝트 이름 | 무해 |
| `한국어` (900511, EN에 '한국어'만) | 1 | `Loc.cs:17` LANG_NAME (Loc.T를 거치지 않음) | 무해 |
| **옛 UI 문구** (코드·씬 어디에도 없음) | **436** | 예전 씬·코드에서 쓰던 문구 (시작하기/옵션/초기화/일시정지/건설/보유 효과…). LocTools는 "안 쓰는 UI 줄" 수만 알리고 지우지 않음(번역 보존 정책) | 무해. 다만 번역 비용 낭비, 폰트 아틀라스에 글자가 더 구워짐(WebGL 용량) |

수정: LocTools 리터럴 수집에서 `[Tooltip(`·`[Header(`·`SceneUI.Need/Find(` 줄과 `after:`/`cnt:` 패턴을 빼고, 쓰레기 줄은 Str_Name을 `junk`로 바꾸거나 삭제(엑셀 COM으로). 옛 문구 436줄은 새 씬 글자(1-3절)에 다시 쓸 수 있는 것만 남기고 정리.

---

## 9. 폰트

### 9-1. 구성 (`Assets/Resources/Loc/LocFonts.asset`, `LocTools.cs:24-33`)
| 언어 | 제목 | 본문 |
|---|---|---|
| ko | Jua-Regular | GowunDodum-Regular |
| en | Jua-Regular | Nunito-SemiBold |
| ja | MPLUSRounded1c-ExtraBold | ZenMaruGothic-Medium |
| ru / uk | Nunito-Black | Nunito-SemiBold |

5개 언어 모두 제목·본문 있음. koEnFonts = ONEMobilePOP, LINESeedSans_Rg, ONEMobileRegular / koEnTitleFonts = ONEMobilePOP.
모든 `*SDF.asset`이 `m_AtlasPopulationMode: 0`(**Static**). 키릴: Nunito 68자, ONEMobilePOP 60자 / 가나: MPLUS·ZenMaru에만 있음.

### 9-2. 구운 글자 대조 (DB 5개 열 + 코드 리터럴 기호, 대체 폰트 체인 포함)
- 각 언어 제목·본문 체인에서 **없는 글자는 이모지 ⚔️🎯💰(TreeTooltip.Iconize로 아이콘 대체 — 문제 없음)와 '폰'(쓰레기 줄 901323)뿐** — 번역 문구 자체는 다 덮인다.
- **DB에 없는 새 문구의 글리프 '뉴'(`메인 메뉴`)·'썼'(`오늘 즉시 완료를 다 썼어요`)는 어느 폰트에도 없음 → □ (높음)**. 굽기가 DB 문구만 보기 때문(`LocTools.cs:366-373`). 번역표 갱신 → 굽기 순서를 지키면 해결.
- 체인 순서상 주 폰트에 없어 다른 폰트로 넘어가는 글자(모양 섞임, 낮음): Jua에 `` `°·×…※→−▶◀ ``(본문 폰트로), Nunito에 `→▶◀★♡♥`, MPLUS ExtraBold에 `鍮`(ZenMaru로 — 주석에 기록됨), ZenMaru에 `▶◀`.
- ko/ja 본문 체인(GowunDodum/ZenMaru → ONEMobileRegular)에 `ї`(Українська)가 없음. 언어 창은 제목 폰트로 만들어서(TempDialog.MakeText → Role.Title) 지금은 문제없음(낮음).
- **ONEMobilePOP·LINESeedSans_Rg는 `AllTtfs()`(LocTools.cs:54)에 없어 4 폰트 굽기로 다시 구워지지 않는다.** ONEMobilePOP는 한글 12자(겠김냠됨뷰왔월준캔팹폰휴)가 없어 ko/en에서 그 글자만 ONEMobileRegular로 그려진다. 새 문구가 늘수록 더 벌어짐(낮음~중간). 수정: koEnFonts도 굽기 대상에 넣기(ko+en 글자).
- `Logs/font-characters.txt`는 없다(이 작업 사본에서 굽기를 돌리지 않았거나 Logs가 커밋되지 않음).

### 9-3. 세트 밖 폰트 전환 (TextLangFont)
- LocText가 없는 배치 글자는 `TextFit.ApplyScene`(App.cs:79, 씬 로드 때)이 `TextLangFont.Register`로 등록 → ja/ru/uk에서 ONEMobilePOP는 그 언어 **제목** 폰트, LINESeedSans·ONEMobileRegular는 **본문** 폰트로 바뀐다(정상). 4개 씬의 LocText 없는 ONEMobilePOP 글자 90개가 대상.
- GowunBatang-Bold(세트 밖, 키릴·가나 0자) 글자 35개는 **모두 LocText가 있어** LocText.Apply가 역할 폰트로 바꾼다 → 문제 없음.
- 빈틈(낮음): `TextFit.Setup()`(TextFit.cs:31)이 **아무 데서도 호출되지 않는다**. 씬 로드 뒤 코드로 만들거나 복제한 TMP(WorkshopPanel.cs:102 Clone, TempDialog, LangPopup 등)는 TextLangFont에 등록되지 않는다. 만들 때 이미 그 언어 폰트를 복사하거나 `LocFonts.Apply`를 하므로 보통은 맞지만, 열려 있는 동안 언어를 바꾸면 폰트가 안 바뀐다.
- 일관성(낮음): LocText가 붙은 ONEMobilePOP 글자(MainGame 80개, 대부분 트리 노드)는 LocText.Apply가 **ko/en에서도** Jua로 바꾼다. LocText 없는 ONEMobilePOP 글자는 ko/en에서 그대로라 같은 화면에서 두 글꼴이 섞일 수 있다.

---

## 10. 일본어(그리고 한국어) 줄바꿈

`Assets/TextMesh Pro/Resources/TMP Settings.asset`
- `m_leadingCharacters` / `m_followingCharacters`: TMP 기본 금칙 문자 파일(「『（ / 」』、。・！？ー ァィゥ…) 사용 — 일본어 금칙 처리 **정상**. 가나·한자는 TMP가 글자 단위로 줄을 나누므로 띄어쓰기 없는 일본어도 줄바꿈된다.
- `m_UseModernHangulLineBreakingRules: 0` → **한국어가 음절 단위로 끊긴다**(단어 중간 줄바꿈). 원문 언어라 화면 품질에 영향(낮음~중간). 수정: 1로 켜면 한글은 띄어쓰기 기준으로 끊는다.
- 줄바꿈 없는 글자는 LocText.Awake(LocText.cs:22-29)가 50%까지 자동 축소, LocText 없는 UI 글자는 TextFit.Fit가 처리 — 긴 번역(ru) 대비는 되어 있음.
- `m_missingGlyphCharacter: 0` → 없는 글리프는 □. `m_warningsDisabled: 0`이라 에디터에서 경고는 나온다.

---

## 11. 언어 고르기 UX

- **`Loc.cs:20-21 DefaultLang = "en"`, 기기 언어 무시 (중간)**. 한국·일본·러시아·우크라이나 사용자가 처음엔 영어로 보고 지구본 버튼을 찾아야 한다. 수정: 저장된 언어가 없을 때 `Application.systemLanguage`(Korean/Japanese/Russian/Ukrainian → ko/ja/ru/uk, 그 밖 en)로 고르기. WebGL도 브라우저 언어가 들어온다.
- 언어 이름은 `Loc.LANG_NAME`(Loc.cs:15-18)에 각 언어 자기 이름으로 하드코딩 — 좋음(번역 안 거침). 제목 폰트 체인에 다섯 이름 글자가 모두 있음.
- **언어 창 제목 `Loc.T("언어")`가 DB에 없어 모든 언어에서 '언어'로 보임 (높음, 1-1절).**
- 언어 바꾸는 곳: MainMenu(MainMenu.cs:75)와 MainGame(MainGameController.cs:57)뿐. `Loc.NextLang()`(Loc.cs:52)은 안 쓰임.
- Loc.Changed 구독: LocText, TextLangFont, MainGameController.OnLang(스테이지 글자·상점·도감·의뢰), OutGameController.Render, StageHUD.OnLang, FarmTreeView.Sign, MainMenu(옵션 창 Refresh). TreeNodeView는 매 프레임 `shownLang`을 비교해 다시 그림 — 좋음.
- 새로 고침 빈틈:
  - **MainMenu 옵션 창 (중간~낮음)**: OptionWindow.Refresh(OptionWindow.cs:80-85)는 음소거 글자만 바꾼다. `소리 설정/효과음/배경음/메인 메뉴` 라벨과 Extra 버튼은 씬을 다시 열 때까지 옛 언어(게다가 지금은 DB에 없어 한국어).
  - 떠 있는 알림(NoticeToast), UnlockGuide 띠(UnlockGuide.cs:67), TempDialog 확인 창: 다시 그릴 때까지 옛 언어 (낮음, 일시적).
  - Stage_Text "N Stage" 틀은 언어가 바뀌어도 영어 (1-3절).

---

## 12. 숫자·날짜 서식 문화권

- `Loc.F`(Loc.cs:68)와 `U.Fmt/FmtN`(Util.cs:12 `Inv`, :58-76)이 **InvariantCulture** → 모든 언어에서 `1,234` / `3.5`. 러시아어·우크라이나어 관례(`1 234`, `3,5`)와는 다르지만 일관되고 기기 로캘에 휘둘리지 않음(낮음, 의도된 선택으로 보임).
- **예외 1곳 (낮음)**: `Assets/Scripts/UI/SettleScreen.cs:305` `$"…{pct:0.0}%…"` — 보간 서식은 **CurrentCulture**를 써서, 기기 로캘이 ru/de 등이면 `12,5%`, 같은 화면의 다른 숫자는 `.` → 섞임. 수정: `U.FmtN(pct, 1)` 또는 `pct.ToString("0.0", CultureInfo.InvariantCulture)`.
- `float.Parse/double.Parse` 문화권 미지정: 없음. `ToString("N…")`: 없음. `Guid.ToString("N")`은 무관.
- 날짜: 화면에 날짜 표기 없음(의뢰 자정 갱신은 내부 계산).

---

## 권장 작업 순서

1. 번역표 갱신(3) → 누락 21개 번역 → 폰트 굽기(4) — 옵션·언어 창·공방·정산·해금 안내·ed_rare0 한 번에 해결, '뉴'·'썼' 글리프도 해결.
2. 메인 메뉴·MainGame·Stage의 영어 배치 글자에 LocText 키 지정, `RenderStage`를 `Loc.F("스테이지 {0}")`로.
3. LocTools 개선: (a) Loc 호출 인자 직접 파싱으로 보간 구멍 속 키 수집, (b) Tooltip/Header/오브젝트 이름/버섯 옵션 태그 제외, (c) 자리 표시·태그 일치 검사 추가, (d) koEnFonts도 굽기.
4. 처음 실행 언어를 기기 언어로.
5. 복수형 7곳 손보기(키 분리 또는 `Loc.P`), `{0}종`(장식) 키 분리.
6. 잔손질: SettleScreen.cs:305 문화권, "MAX/LVL/Lv./G" 키화, `m_UseModernHangulLineBreakingRules` 켜기, 옵션 창 언어 변경 새로 고침, 죽은 코드(Iga, TIER_NAMES, NextLang, TextFit.Setup 미사용) 정리.
