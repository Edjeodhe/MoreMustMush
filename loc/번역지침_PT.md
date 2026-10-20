# MoreMustMush 포르투갈어(브라질) 번역 지침

작성: 2026-10-19 · 대상 열: 엑셀 `Assets/DB/MMM_DB_Ver.05.xlsx` `String` 시트의 새 열 `Str_PT` (1,568줄)
함께 보는 파일: [`PT_용어집.tsv`](PT_용어집.tsv) (용어·고유명사 PT 확정안 전체, 엑셀로 열 수 있음) · 검수 보고서 [`검수_요약.md`](검수_요약.md) · 언어별 검수 `검수_EN/JA/RU/UK.md`

이 문서는 **기존 4개 언어(EN·JA·RU·UK)와 같은 게임으로 읽히는** 포르투갈어를 만들기 위한 규칙이다. 기존 번역은 2026-10-19 검수에서 용어·문체를 한 번 정리했으므로, PT는 그 결정을 그대로 따른다. 규칙끼리 부딪히면 **위에 있는 절이 이긴다**(0절 > 2절 기술 규칙 > 3절 문체 > 나머지).

---

## 0. 한눈에 — 꼭 지킬 12가지

| # | 규칙 | 한 줄 이유 |
|---|---|---|
| 1 | **브라질 포르투갈어(pt-BR)**. 언어 코드 `pt`, 언어 창 표시 이름 `Português` | 모바일·웹 게임 포르투갈어 사용자 대부분이 브라질. pt-PT 표현(`ecrã`, `telemóvel`, `miúdo`, `tu tens`·`estás` 같은 2인칭 tu 활용)은 쓰지 않는다 |
| 2 | **원문은 한국어(`Str_KR`)**. 영어는 뜻을 확인하는 참고용 | 지난 검수에서 EN 오역이 RU·UK로 그대로 옮겨 간 사례가 많았다(구역 "정원"→Garden, 라운드 15초 등). EN과 KR이 다르면 KR을 따른다 |
| 3 | `{0}`·`{1}`…, `{n}`·`{m}`, `{2:00}`·`{0:F1}`, 태그 `<b> <color> <size> <mark> <sprite>`, `\n`은 **개수·번호·철자 그대로** | 하나라도 틀리면 그 줄이 한국어로 보이거나 화면이 깨진다 (2절) |
| 4 | 플레이어에게는 **você 화법**(대명사는 되도록 생략), 버튼·안내는 **명령형**(`Toque`, `Colete`), 설명은 **3인칭 현재형**(`Aumenta…`) | EN 명령형·RU/UK 반말(ты) 명령형과 같은 거리감 (3절) |
| 5 | 꼬마(Pequenino) 대사는 **어린아이 말투, 남성형**, 플레이어를 **`Vovô`**라 부른다 | JA `ぼく`, RU·UK 남성 어미와 맞춤 (3-3절) |
| 6 | 용어는 **`PT_용어집.tsv`의 확정안만** 쓴다. 같은 KR 용어를 두 가지로 옮기지 않는다 | 지난 검수 문제의 절반이 용어 갈림이었다 (5절) |
| 7 | `{0}`에 이름이 들어가는 줄은 **관사·전치사 축약·형용사 일치를 쓰지 않는다** → `Rótulo: {0}` 꼴 | `Dourado/Dourada` 같은 성 불일치 방지 (2-5절) |
| 8 | 숫자 뒤 세는 말(개·종·번·마리…)은 **복수형을 피한다** → `Espécies: {0}`, `(×{n})` | 엔진에 복수형 처리가 없다 (2-4절) |
| 9 | 길이 기준: **EN 길이 ×1.15 이내**. 줄바꿈 없는 칸(노드 이름·버튼·탭·도감 카드 이름)은 **같은 범주에서 기존 언어가 쓰는 최장 길이 이내** (부록 A 범주 제목) | 칸이 넘치면 최대 50%까지 줄어든 뒤 잘린다 (4절) |
| 10 | 이름(아이템·노드·스킬·장소·캐릭터)은 **Title Case**, 문장·버튼·안내는 **첫 글자만 대문자** | pt-BR 게임 관례 (3-4절) |
| 11 | 화면에 **빈 칸을 남기지 않는다**. 빈 칸은 한국어로 보인다. 단, 번역 제외 줄(2-8절)은 비운다 | `Loc.T`가 빈 번역을 한국어로 대체한다 |
| 12 | 이모지·폰트에 없는 기호 금지. 쓸 수 있는 기호는 2-6절 표만 | PT 폰트(Nunito)에 없는 글자는 다른 폰트로 그려져 어색해진다 |

---

## 1. 작업 범위와 게임 이해

### 1-1. 무엇을 번역하나

- `String` 시트 1,568줄. 열: `Str_ID`·`Str_Group_ID`·`Str_Name`·`Str_KR`·`Str_EN`·`Str_JA`·`Str_RU`·`Str_UK` → 새 열 `Str_PT`.
- **키는 한국어 원문**이다. 코드가 `Loc.T("한국어")`로 찾으므로 `Str_KR`은 절대 고치지 않는다(앞뒤 공백 포함).
- `Str_Name`으로 줄의 출처를 안다.

| `Str_Name` | 줄 수 | 무엇 | 화면 |
|---|---|---|---|
| `ui` | 1,342 | 코드·씬 문구 (버튼, 안내, 팝업, 꼬마 대사, 버섯 이름·설명, 장식…) | 전부 |
| `upg:<키>:name` / `:desc` | 90 / 90 | 균사 트리 노드 이름 / 설명 | 트리 화면 노드 라벨(**가장 좁음**), 툴팁 |
| `ball:<키>:name/type/look` | 8 / 8 / 8 | 수확기 이름 / 유형 / 외형 설명 | 공방 카드 |
| `boss:<키>:name/gimmick` | 5 / 5 | 보스 이름 / 기믹 | 보스 등장·정산 |
| `wea:<키>:name/effect` | 6 / 6 | 날씨 이름 / 효과 | 라운드 시작·HUD |

### 1-2. 게임 한 줄 요약 (번역가용 배경)

**버섯을 수확하는 인크리멘탈(방치형) 게임.** 할아버지(플레이어, 노련한 버섯 사냥꾼)가 핀볼처럼 튕기는 **수확기**(낫·수리검·괭이 같은 도구)를 쏘아 필드의 **버섯**을 베고, 아래쪽 **바**로 수확기를 받아 쳐 **콤보**를 쌓는다. 라운드가 끝나면 점수가 **골드**로 **정산**되고, 골드로 **균사 트리**의 노드를 강화한다. 10스테이지마다 **보스**가 나온다.
아웃게임에는 **세계수**가 있고, 그곳에 사는 작은 버섯 정령 **꼬마**들(불씨·찌릿·이슬…)이 부탁을 하고, 구역에서 자동 수확을 하고, 장식을 받는다. 버섯 **도감**은 실제 버섯 64종 + 판타지 버섯 11종 + 꼬마 버섯 20종.
분위기: **그림책 같은 몽환 수채화, 아이도 할 수 있는 따뜻하고 귀여운 게임.** 한국어는 해요체, 꼬마는 반말 아이 말투다.

### 1-3. 참고 순서

1. 이 문서와 `PT_용어집.tsv`를 먼저 읽는다.
2. 줄마다 **KR → EN** 순으로 본다. RU·UK는 복수형·성 처리(콜론 라벨)의 참고, JA는 어감(귀여움·해요체 대응)의 참고로 본다.
3. 의미가 애매하면 `Str_Name`과 이웃 ID(같은 기능 문구가 붙어 있다)를 본다. 그래도 모르면 번역하지 말고 질문 목록(10절)에 올린다.

---

## 2. 기술 규칙 (어기면 깨진다)

### 2-1. 자리 표시

| 형식 | 뜻 | 규칙 |
|---|---|---|
| `{0}` ~ `{5}` | 코드가 넣는 값 (숫자·이름·아이콘·색) | **번호와 개수를 그대로.** 어순 때문에 `{1}`을 `{0}`보다 앞에 써도 된다 |
| `{2:00}` | 초를 두 자리로 (`5:07`) | 그대로 (900549, 901044) |
| `{0:F1}` | 소수 한 자리 | 그대로 (900739) |
| `{n}` `{m}` | 의뢰 틀: `{n}` = 목표 수, `{m}` = 버섯 이름 | 그대로 (900199~900203). `string.Format`이 아니라 글자 바꾸기라 다른 `{}`와 섞지 않는다 |

- 리터럴 `{` `}`는 쓰지 않는다(형식 오류 → 그 줄이 한국어로 보이고 오류 로그).
- **아이콘+숫자 자리**: `{0}{1}`처럼 붙어 있는 두 자리는 대개 `{0}`=아이콘(`<sprite>`), `{1}`=금액이다. **사이에 공백을 넣거나 순서를 바꾸지 않는다.**
  예) 901208 KR `{0}을(를) 데려오려면 {1}{2}이 필요해요 (지금 {3})` → PT `Para chamar o Pequenino ({0}), são necessários {1}{2} (agora: {3})`

### 2-2. 리치 텍스트 태그 (TextMeshPro)

| 태그 | 줄 수 | 규칙 |
|---|---|---|
| `<b>…</b>` | 35 | 같은 낱말(구)에 다시 씌운다 |
| `<color=#xxxxxx>…</color>` | 32 | 색 값 그대로. `#8a6a4a` = 회갈색 보조 설명, `#d23a2a` = 부족(빨강). `<color={0}>`(900594)은 코드가 색을 넣는다 |
| `<size=NN%>…</size>` | 27 | 값 그대로. 보통 괄호 속 보조 설명이 작게 들어간다 |
| `<sprite name="…">` | 14 | **이름(`book`, `trophy`, `star`…)을 절대 번역하지 않는다** |
| `<mark=#…>  …  </mark>` | 2 | 안쪽 **공백 두 칸씩**이 배지 여백이다. 유지 |

### 2-3. 줄바꿈·공백

- `\n`(엑셀 셀 안 줄바꿈)은 KR과 같은 개수(현재 900860 한 줄만).
- **앞뒤 공백도 문구의 일부**다. 코드가 그 뒤에 다른 문구를 이어 붙인다.
  - 900155 `발동률 {0} · ` → PT `Chance de ativação {0} · ` (끝의 ` · ` 유지)
  - 900154 `발동률 10%로 ` → 아래 2-7절
- 구분자 ` · `(가운데 점, 앞뒤 한 칸)는 그대로 쓴다.

### 2-4. 복수형 — 엔진에 없다

`Loc.F`는 `string.Format`일 뿐이라 `1 espécie / 2 espécies`를 고를 수 없다. 숫자 뒤에 세는 말이 오는 줄이 109개 있다. RU·UK가 쓴 방식을 그대로 쓴다.

| KR 꼴 | ✗ 피할 것 | ✓ PT | 근거(기존 언어) |
|---|---|---|---|
| `분신이 {0}마리 남았어요!` (900534) | `Restam {0} clones!` | `Clones restantes: {0}!` | EN `Clones left: {0}!`, RU `Осталось двойников: {0}!` |
| `일일 의뢰 {n}개 완료하기` (900203) | `Complete {n} missões diárias` | `Concluir missões diárias: {n}` | RU `Выполнить ежедневных заданий: {n}` |
| `버섯 {n}번 수확하기` (900200) | `Colha {n} vezes` | `Colher cogumelos (×{n})` | EN `Harvest mushrooms ({n} times)`, UK `(×{n})` |
| `도감 {0}/{1}종` (901269) | `{0}/{1} espécies` | `Coleção: {0}/{1}` | UK `Колекція: {0}/{1}` |
| `{0}초` · `{0}분` · `{0}시간` · `{0}일` | `{0} segundos` | `{0} s` · `{0} min` · `{0} h` · `{0} d` | 약어는 복수가 없다 |
| `{0}단계` (세계수·트리, 23줄) | `{0} estágios` | `Estágio {0}` (명사를 앞에) | RU `Стадия {0}` |
| `{0}레벨`, `Lv.{0}` | `{0} níveis` | `Lv. {0}` | 3-5절 |
| `{0}배`, `×{0}` | — | `×{0}` | |

문장 속에서 피할 수 없으면 **숫자가 1이 될 수 없는 줄인지** 확인하고(예: 항상 2 이상인 목표치), 아니면 콜론 라벨로 바꾼다.

### 2-5. 성·수 일치 — `{0}`에 이름이 들어가는 줄

`{0}`에는 **번역된 이름**(버섯·수확기·장식·꼬마·구역·보스·기능)이 들어간다. 포르투갈어는 이름마다 성(o/a)이 달라 `Dourado/Dourada`, `pronto/pronta`, `do/da`, `no/na`, `ao/à`가 틀어진다(RU·UK에서도 `Золотой Лисичка` 같은 오류가 실제로 났다).

**규칙**
1. `{0}` 바로 앞뒤에 **관사(o/a/os/as)와 전치사 축약(do/da/no/na/ao/à/pelo/pela)을 쓰지 않는다.** 무관사 전치사 `de {0}`·`para {0}`·`com {0}`·`em {0}`만 허용.
2. 이름과 일치해야 하는 **형용사·과거분사를 쓰지 않는다.** 대신 ① 동사 문장, ② 성이 고정된 명사 라벨(`Variante dourada`, `Decoração pronta`, `Construção concluída`)을 쓴다.
3. 이름은 되도록 **콜론 뒤**: `Rótulo: {0}`.

| ID | KR | ✗ | ✓ PT |
|---|---|---|---|
| 900527 | `황금 {0}!` | `{0} Dourado!` | `Variante dourada: {0}!` |
| 900752 | `황금 {0}` | `{0} dourada` | `Variante dourada: {0}` |
| 900538 | `거대 {0} 등장!` | `Apareceu o {0} gigante!` | `Gigante à vista: {0}!` |
| 900529 | `보스 {0} 등장!` | `O chefe {0} apareceu!`(관사 OK지만 통일 위해) | `Chefe à vista: {0}!` |
| 900506 | `{0} 전체 ×{1}` ({0}=등급) | `Todos os {0} ×{1}` | `{0}: todos ×{1}` |
| 900510 | `{0} {1}/{2}개 수확 시 해금` | `Desbloqueado ao colher o {0}…` | `Desbloqueia ao colher: {0} {1}/{2}` |
| 900665 | `{0} 해금! 빈 장착 칸에 들어갔어요` | `{0} desbloqueado! Foi colocado…` | `Desbloqueio: {0}! Entrou em um espaço livre` |
| 900786 | `{0} 완성!` (건물) | `{0} concluído!` | `Construção concluída: {0}!` |
| 901181 등 | `{0} 완성! …` (장식) | `{0} pronto!` | `Decoração pronta: {0}! …` |
| 901185 | `{0}을(를) 처음 놓았어요! …` | `Você colocou o {0}…` | `Primeira peça colocada: {0}! …` |
| 901169 | `{0}(으)로 갈게!` (구역) | `Vou para o {0}!` / `Vou à {0}!` | `Vou para a zona “{0}”!` |
| 901266 | `{0}에게 {1} ×{2}` (선물) | `{1} ×{2} para o {0}` | `Presente ({0}): {1} ×{2}` |
| 900201 | `{m} {n}번 수확하기` | `Colha o {m} {n} vezes` | `Colher: {m} (×{n})` |
| 900677 | `{0} 합류!` (꼬마) | `{0} entrou!`(OK) | `{0} chegou!` (동사 완료형은 성 중립) |
| 901240 | `{0} 합류 가능` | `{0} disponível`(형용사 OK지만) | `{0} pode entrar` |
| 900791 | `{0}의 분신` (보스) | `Clone da {0}` | `Clone de {0}` |
| 901227 | `{0} 해금` | `{0} desbloqueado(a)` | `Desbloqueio: {0}` |

### 2-6. 기호와 폰트

PT는 **제목 `Nunito-Black` + 본문 `Nunito-SemiBold`**(RU·UK와 같은 쌍)로 그린다. EN 제목 폰트 Jua에는 악센트 글자가 하나도 없기 때문이다. Nunito에 있는 기호만 쓴다.

| 쓴다 | 쓰지 않는다 |
|---|---|
| 악센트 전부 `á à â ã é ê í ó ô õ ú ü ç` + 대문자, `ª º` | 이모지 (폰트에 없다) |
| 따옴표 **`“ ”`**(겹), 안쪽 인용 `‘ ’` | 곧은 따옴표 `"…"`는 KR에 있어도 PT에서는 `“…”`로 바꾼다 (RU·UK가 `«…»`로 바꾼 것과 같은 처리) |
| 말줄임: **KR을 따른다** — KR `...`(6줄) → `...`, KR `…`(3줄) → `…` | 원문에 없는 말줄임 추가, `...`↔`…` 임의 변경 (EN·RU·UK 모두 원문을 따랐다) |
| 곱셈 **`×`**(U+00D7) | 영문자 `x` (EN 검수에서 지적, 단 코드가 넣는 `x{배속}`은 코드 몫) |
| 마이너스 **`−`**(U+2212, KR에 있으면 유지), 대시 `–` `—` | |
| `·` `→` `★` `☆` `♥` `♡` `▶` `◀` `※` `°` `∞` (KR에 있는 자리만) | 새로 기호를 추가하지 않는다 (`→ ★ ♥ ▶`는 Nunito에 없어 대체 폰트로 그려진다. KR에 있는 곳만 유지) |
| 물결 `~` (꼬마 말끝, KR에 있을 때) | |

### 2-7. 조각을 이어 붙이는 문구 (어순 주의)

코드가 번역된 조각을 다른 문구의 `{0}`에 넣는 곳은 **한 곳뿐**이다 (`Defs.Nodes.cs:299`).

| ID | KR | EN | PT |
|---|---|---|---|
| 900153 | `해금하면 {0}사용 시작` | `Unlocks the skill{0}` | `Libera a habilidade{0}` |
| 900154 | `발동률 10%로 ` | `: 10% trigger chance` | `: chance de ativação de 10%` |

- 900154는 900153의 `{0}` 자리에 **그대로** 들어간다(앞에 공백 없이). 발동률이 없는 스킬은 `{0}`이 빈 글자라 **900153 혼자서도 문장이 끝나야** 한다.
- 결과: `Libera a habilidade: chance de ativação de 10%` / `Libera a habilidade`.

그 밖의 이어 붙이기(` · `나 줄바꿈으로 나열)는 줄마다 완결된 구절로 번역하면 된다.

### 2-8. 번역하지 않는 것

**코드에 박혀 있어 번역 대상이 아님** (보여도 신경 쓰지 말 것): 큰 수 접미사 `K M B T Qa…`, 골드 기호 `G`, 트리의 `MAX`·`Lv`·`LVL`, 배속·콤보 `x2`, `SUPER COMBO!`·`FEVER!`·`COMBO`·`NEW`(그림 글자), `%`·`%p`, 숫자 서식(천 단위 쉼표, 소수점 점).

**비워 둘 줄** (화면에 나오지 않음 — EN·RU·UK도 비어 있다):

| ID / 패턴 | 이유 |
|---|---|
| 900518 `이`, 900519 `가` | 쓰지 않는 한국어 조사 함수가 남긴 줄 |
| 901323 | 코드 주석이 줄로 들어간 것 |
| `Str_KR`이 `after:`·`cnt:`로 시작하는 데이터 태그 (45줄) | 내부 데이터 |
| 개발자 화면(`디버그`, `조정안`, `명세 원본`, `Panels (비워도 됨)` 등) | EN이 채워져 있으면 PT도 채우되 우선순위 최하 |

> 판단 기준: **같은 줄의 EN이 비어 있으면 PT도 비운다.** EN이 채워져 있으면 PT도 채운다.

### 2-9. 숫자 쓰기

- 코드가 만드는 숫자는 언어와 상관없이 `1,234`·`12.5`·`1.20K`처럼 나온다(Invariant 고정). 번역가가 바꿀 수 없다.
- **문구에 직접 쓰는 숫자**는 현지 표기로: 소수점은 **쉼표**(`0,5 segundo`, `×0,75`), 1만 이상은 쓰지 않거나 `10 mil`. (기존 언어: UK는 쉼표 10줄로 통일, RU는 점 8줄·쉼표 2줄로 섞여 있다. PT는 UK처럼 한 가지로 통일한다.)
- 곱셈은 `Ataque ×{1}`처럼 **앞에 공백, 뒤는 붙임** (EN 72줄이 이 꼴).
- 코드가 `%`를 붙여 주는 자리(`+{0}`)에 `%`를 또 쓰지 않는다. KR에 `%`가 직접 쓰여 있을 때만 쓴다 (예: 900126 `+{1}%`는 씀, 900065 `+{0}`는 안 씀).
- 퍼센트는 숫자에 붙인다: `+5%`, `10%` (공백 없음, KR·EN과 같게). 퍼센트포인트 `%p`는 EN처럼 `%`로 쓴다.
- 범위: `1~4스테이지` → 문장 속 `fases 1 a 4`, 표·짧은 칸 `1–4`.

---

## 3. 문체와 어조

### 3-1. 화자별 기준표

| 종류 | KR | EN | RU·UK | **PT 규칙** | PT 예 |
|---|---|---|---|---|---|
| 버튼·탭·메뉴 | 명사형/하기 (`계속하기`, `보상 받기`) | 동사 원형·Title Case (`Resume`, `Claim Reward`) | 동사 원형/명사 | **동사 원형(infinitivo)** 또는 명사, 첫 글자만 대문자 | `Retomar`, `Receber recompensa`, `Evoluir` |
| 조작 안내·튜토리얼 | 해요체 (`~하면 발사!`, `눌러서 확인해 보세요`) | 명령형 | 반말 명령형 (`Возьми…`) | **명령형(você)** — `Toque`, `Arraste`, `Solte` | `Segure o coletor, puxe para trás e solte para lançar!` |
| 노드·스킬·아이템 설명 | 해요체 서술 (`늘려요`) | 3인칭 현재 (`Increases…`) | 3인칭 현재 (`Увеличивает…`) | **주어 생략 3인칭 현재** | `Aumenta o tempo limite da rodada.` |
| 도감 버섯 설명 (75줄) | **한다체 평서** (`~돋는다.`, `~난다.`) — KR에서 유일하게 해요체가 아님 | 3인칭 현재 서술 | 3인칭 현재 (JA는 です/ます로 맞춤) | **백과사전식 3인칭 현재, 주어 생략·무인칭** — 판타지 버섯 설명의 `(판타지)` 머리말은 `(Fantasia)` | `Cresce em troncos de pinheiro caídos. Tem cheiro forte e marcante.` |
| 상태·결과 알림 | `~했어요`, `~열림` | 과거분사/짧은 문장 | `Открыто: {0}` | 짧은 문장·콜론 라벨, 감탄은 `!` 하나 | `Novo recorde! {0}` |
| 오류·부족 | `골드가 부족해요` | `Not enough gold` | `Не хватает золота` | `Ouro insuficiente` (짧은 칸) / `Você não tem ouro suficiente` (긴 칸) | |
| 꼬마 대사 | 반말 아이 말투 | 구어, 축약(`gonna`, `Wanna`) | 반말·애칭 | **아이 구어, 남성형**, 감탄사·지소사 허용 (3-3절) | `Ai, que quente! Ah, não, o quente sou eu!` |
| 칭호 | `칭호 '숲의 장인'` | `Title: "Forest Artisan"` | `Титул «…»` | `Título: “…”` | `Título: “Artesão da Floresta”` |

### 3-2. 플레이어를 부르는 법

- **você**를 쓰되 **대명사는 되도록 빼고** 동사만 쓴다: `Você precisa de…` 보다 `É preciso…` / `Precisa de…`가 자연스러운 칸이 많다.
- `tu`, `o senhor/a senhora`, `vocês`는 쓰지 않는다. 존댓말 거리감은 KR 해요체·EN 친근한 2인칭·RU/UK 반말과 같은 **친근하고 가벼운** 수준.
- 플레이어의 성을 드러내는 형용사를 피한다(`Você está pronto/pronta` ✗ → `Tudo pronto?`). 꼬마가 부르는 호칭 `Vovô`만 남성으로 고정되어 있다.

### 3-3. 꼬마(Pequenino) 대사 — 캐릭터 말투

| 항목 | 규칙 | 예 |
|---|---|---|
| 나이·말투 | 5~7살 아이. 짧은 문장, 감탄, 말끝 늘이기 `~` | `Tô entediado~ Brinca comigo!` |
| 성 | **남성형**으로 일치 (`cansado`, `pronto`, `o quente sou eu`) | JA `ぼく`, RU `горячий`, UK `гарячий`와 같다 |
| 구어 | 꼬마 대사에서만 `tô`, `tá`, `pra`, `né` 허용. 시스템 문구에서는 금지 | `Tá vendo? Eu brilho!` |
| 호칭 | 플레이어 = **`Vovô`** (할아버지). `Vô`는 쓰지 않는다 | `Vovô, colheu bastante hoje?` |
| 2인칭 | 꼬마도 `você` 기준. 다만 브라질 구어에서 흔한 명령형 `Brinca comigo!`, `Olha!`는 꼬마 대사에서만 허용 (pt-PT식 `tu tens` 활용은 금지) | `Olha só, Vovô!` |
| 지소사 | `-inho/-inha`는 귀여움이 필요할 때 한 문장에 하나까지 | `uma gotinha de orvalho` |
| 의성어 | 6절 표를 따른다 | `Tlim-tlim~ Vou ficar rico!` |
| 동요·관용구 | 원문이 노래·관용구면 **포르투갈어권 아이가 아는 것**으로 바꾼다 | 900025 `반짝반짝 작은 별~` → `Brilha, brilha, estrelinha~` (브라질 동요 가사 첫 구절) |

### 3-4. 대소문자

| 대상 | 규칙 | 예 |
|---|---|---|
| 문장, 버튼, 안내, 상태 | 첫 글자만 대문자 | `Receber recompensa`, `Tempo restante` |
| **이름**(노드·스킬·아이템·장식·수확기·스킨·보스·날씨·지역·세트·칭호·캐릭터) | **Title Case** — 관사·전치사·접속사(`de da do das dos e em com a o`)만 소문자 | `Bênção da Árvore do Mundo`, `Foice do Dragão` |
| 실제 버섯 종 이름 | 첫 글자만 대문자, 하이픈 뒤는 소문자 (포르투갈어 종 이름 표기) | `Cauda-de-peru`, `Juba-de-leão`, `Amanita-dos-césares` |
| 판타지 버섯 | Title Case | `Shiitake do Luar`, `Reishi Enferrujado` |
| 문장 속 고유 게임 명사 | 항상 대문자: `Árvore do Mundo`, `Árvore de Micélio`, `Pedra(s) de Micélio`, `Pequenino(s)`, `Coleção`, `Oficina`, `Celeiro`, `Anéis da Árvore`, `Missões`(의뢰 화면·시스템 — EN `Quests`도 대문자) | `Ofereça cogumelos do Celeiro` |
| 문장 속 일반 게임 명사 | 소문자: `coletor`, `cogumelo`, `colônia`, `rodada`, `fase`, `chefe`, `ouro`, `esporo`, `combo`, `barra`, `campo`, `pedido` (EN `granting a favor`처럼) | `cada colônia de cogumelos` |

EN은 버튼도 Title Case지만 PT는 **pt-BR 관례(문장형)**를 따른다. 이름의 Title Case만 EN과 같게 맞춘다.

### 3-5. 약어

짧은 칸에서만, 아래 목록만 쓴다(마침표 포함). 설명문에서는 풀어 쓴다.

| 약어 | 원말 | 약어 | 원말 |
|---|---|---|---|
| `Hab.` | habilidade | `máx.` / `mín.` | máximo / mínimo |
| `Vel.` | velocidade | `Temp.` | temporário |
| `Nº` | número | `pts` | pontos |
| `Lv.` | nível (레벨) — 트리 라벨 `Lv`가 코드 고정이라 같게 맞춘다 | `XP` | 경험치 |
| `s` `min` `h` `d` | 초·분·시간·일 (마침표 없음) | | |

---

## 4. 길이

### 4-1. 기준

- 기존 언어: EN은 KR의 약 2.1배, RU·UK는 EN과 거의 같다(짧은 라벨만 +8%).
- 포르투갈어는 보통 EN보다 10~25% 길다 → **목표 EN ×1.15**.
- **줄바꿈이 없는 칸**(버튼·탭·노드 이름·능력치 이름·도감 카드 이름·헤더)은 글자가 넘치면 원래 크기의 50%까지 줄어든 뒤 잘린다. **줄바꿈이 있는 칸**(툴팁·도감 본문·튜토리얼·팝업)은 여유가 있지만 너무 길면 칸 밖으로 나간다.
- 트리 노드 이름은 월드 글자라 자동 축소도 없다 → **가장 엄격**.
- **실용 기준**: 같은 화면 범주(노드 이름, 도감 이름, 장식 이름…)에서 기존 EN·RU·UK가 이미 쓰고 있는 **가장 긴 길이**는 화면에 들어간다는 뜻이다. 부록 A의 각 범주 제목에 그 길이(`기존 언어 최장 N자`)를 적어 두었다. 이것을 넘는 PT는 `범주 최장 초과`로 표시했으니 화면에서 반드시 확인하고, 넘치면 3-5절 약어나 짧은 동의어로 바꾼다.

### 4-2. 줄이는 방법 (우선순위)

1. 소유사·군더더기 빼기: `Aumenta o tamanho do seu coletor` → `Aumenta o tamanho do coletor`, 라벨은 `Tamanho do Coletor`.
2. 명사구로: `Toque para mudar a velocidade` → `Toque: velocidade` (EN `Tap: Speed`).
3. 콜론 라벨: `Limite de hoje esgotado` (RU `Лимит на сегодня исчерпан`처럼).
4. 3-5절 약어.
5. 그래도 넘치면 10절 질문 목록에 **줄 ID + 글자 수**로 올린다 (개발 쪽에서 칸을 늘리거나 줄바꿈을 켠다).

### 4-3. 특히 좁은 칸 (EN이 이미 빠듯한 곳)

| ID | KR | EN | PT 권장 |
|---|---|---|---|
| 900741 | 누르기·F·우클릭 배속 | Tap·F·RMB: Speed | `Toque·F·BDM: velocidade` |
| 900740 | 눌러서 배속 | Tap: Speed | `Toque: velocidade` |
| 900634 | 모두 돌보기 | Tend All | `Cuidar de todos` |
| 900855 | 라운드 끝내고 정산하기 | End Round & Cash Out | `Encerrar e receber` |
| 901220 | 지금 끝내기 | End Now | `Encerrar já` |
| 901329 / 901330 | 음소거: 켜짐 / 꺼짐 | Mute: On / Off | `Mudo: ligado` / `Mudo: desligado` |
| 900891 | 진화하기 | Evolve | `Evoluir` |
| 900113 | 후반 | Late | `Final` |
| 901284 | 오늘 즉시 완료 소진 | Instant finishes used up today | `Limite de hoje esgotado` |
| 901236 | 라운드 영구 적용 | Permanent in rounds | `Permanente nas rodadas` |
| 900662 · 900723 · 901189 | 세계수 경험치 | World Tree EXP | `XP da Árvore` |
| 901243 | 진화 가능! | Ready to evolve! | `Pode evoluir!` |
| 900871 · 900926 · 900927 · 900928 · 900929 (데미지 통계 열 머리, 열 간격 45·62·78·89%) | 이름 / 피해 비율 / 피해 / 타격 / 처치 | Name / Damage Share / Damage / Hits / Kills | `Nome` / `% do dano` / `Dano` / `Acertos` / `Abates` (열 폭이 좁다) |

전체 목록(위험 40줄): 이 문서 끝 부록 C.

---

## 5. 용어집 (핵심)

**이 표의 PT가 확정안이다.** 이름 전체(버섯 95·노드 90·장식 24 등)는 부록 A와 `PT_용어집.tsv`에 있다. "기존 언어" 열은 2026-10-19 검수 반영 후 실제 db.json 값이다.

**재화·성장**

| KR | EN | JA | RU | UK | **PT (확정)** | 비고 |
|---|---|---|---|---|---|---|
| 골드 | Gold | ゴールド | золото | золото | **ouro** | 문장 속 소문자. 기호 `G`는 코드 고정 |
| 균사석 | Mycelium Stone(s) | 菌糸石 | камни мицелия | міцелієвий камінь | **Pedra de Micélio / Pedras de Micélio** | 고유 명사 → 항상 대문자. 수량 문구는 `Pedras de Micélio: {0}` |
| 균사 | Mycelium | 菌糸 | мицелий | міцелій | **Micélio** | 식용/약용/독 균사 = Micélio Comestível/Medicinal/Venenoso |
| 균사 트리 | Mycelium Tree | 菌糸ツリー | Древо мицелия | Дерево міцелію | **Árvore de Micélio** | 노드 트리 화면 |
| 균사석 노드 | Mycelium Stone Node | 菌糸石ノード | узел камней мицелия | вузол міцелієвого каменя | **nó de Pedra de Micélio** | 특수 노드 설명 머리: `Nó especial desbloqueado com Pedras de Micélio.` |
| 노드 | node | ノード | узел | вузол | **nó (pl. nós)** | 핵심 코어 = Núcleo |
| 세계수 | World Tree | 世界樹 | Мировое древо | Світове дерево | **Árvore do Mundo** | 세계수 나무(JA 世界樹の木)도 같은 말. 짧은 칸 `Árvore` |
| 세계수 단계 | Tier | 段階 | стадия | ярус | **Estágio** | `Estágio {0}` (명사 앞). 스테이지 Fase와 구분 |
| 진화 단계 | Evolution Stage | 進化段階 | стадия эволюции | ступінь | **Estágio de evolução** |  |
| 나이테 | Tree Rings | 年輪 | годовые кольца | річні кільця | **Anéis da Árvore** | 세계수 레벨 재화. 고유 명사 대문자 |
| 경험치 | EXP | 経験値 | опыт | досвід | **XP** | `XP da Árvore` (세계수 경험치) |
| 레벨 | Lv. / Level | Lv. / レベル | ур. / уровень | рів. / рівень | **Lv. / nível** | 라벨 `Lv. {0}`(트리의 코드 고정 `Lv`와 맞춤), 문장 속 `nível` |
| 최대 | Max | 最大 | макс. | макс. | **máx. / máximo** | `Lv. máx.`, `Nível máximo` |
| 강화 | Upgrade | 強化 | улучшение | покращення | **melhoria / melhorar** | 버튼 `Melhorar`. 노드명의 `강화`(숲 장치 강화 등)는 `+` 또는 `Reforço` |
| 한 번에 강화 | Bulk Upgrade | まとめて強化 | Улучшить всё сразу | Масове покращення | **Melhorar tudo** |  |
| 정산 | Payout / Cash Out | 精算 | итоговое золото / итоги | виплата | **Lucro / receber** | 라운드 끝 골드. `Bônus de Lucro`, 버튼 `Encerrar e receber` |
| 정산 골드 | Payout Gold | 精算ゴールド | Итоговое золото | Золото виплати | **Lucro** | 능력치 이름. 문장 속 `o lucro da rodada` |
| 점수 | Score | スコア | очки | бали | **pontos / pontuação** | 약어 `pts`. 아름다움 점수 = `Pontos de Beleza` |
| 타격 점수 | Hit Score | ヒットスコア | Очки за удар | Бали за удар | **Pontos de Acerto** | 노드·능력치 같은 말 |
| 수확량 | Yield | 収穫量 | урожай | врожай | **Rendimento** | 능력치·노드. 문장 속 `rendimento` (EN Yield/Harvest 혼재를 PT는 하나로) |
| 획득량 | yield | 獲得量 | урожай | врожай | **rendimento** |  |
| 수확 / 수확하다 | harvest | 収穫 | сбор / собрать | збір / зібрати | **colheita / colher** | 따다(EN pick) = `colher` 같은 동사 |
| 풍년 | Bountiful Harvest | 豊作 | Богатый урожай | Щедрий урожай | **Colheita Farta** | 스킬 이름·외침 `Colheita farta!` |
| 자동 수확 | Auto Harvest | 自動収穫 | автосбор | автозбір | **Colheita Automática** | ‘오프라인 보상’ 역할. 노드 `자동 수확 확장` = `Colheita Automática+` |
| 수확 보따리 | Harvest Basket | 収穫ふろしき | корзина урожая | кошик урожаю | **Cesta de Colheita** | 단독 `보따리` = `cesta` |
| 확률 | chance | 確率 | шанс | шанс | **chance** | 여성 명사 `a chance` |
| 발동률 | trigger chance | 発動率 | шанс срабатывания | шанс спрацювання | **chance de ativação** | 짧은 칸 `Ativação` |
| 즉시 완료 | Instant finish | 即完了 | мгновенное завершение | миттєве завершення | **Concluir agora** | 버튼. 남은 횟수 문구는 `Conclusões instantâneas: {0}` |
| 일일 / 주간 | Daily / Weekly | デイリー / ウィークリー | Ежедневный / Еженедельный | Щоденний / Тижневий | **Diária(s) / Semanal(is)** | `Missões diárias`, `Missões semanais` |
| 보상 | Reward | ほうび / 報酬 | награда | нагорода | **Recompensa** | 보상 받기 = `Receber recompensa` |
| 재화 | resources | 通貨 | ресурсы | ресурси | **recursos** | `Recursos insuficientes` |
| 엽전 | Coin | 銭 | монета | монета | **moeda (antiga)** | 수확기 `Coletor-Moeda`, 스킨 `Moeda de Jade` |
| 단골 손님 | Regular Customer | 常連さん | Постоянный клиент | Постійний покупець | **Freguês Fiel** | 노드 이름 |

**전투·라운드·수확기**

| KR | EN | JA | RU | UK | **PT (확정)** | 비고 |
|---|---|---|---|---|---|---|
| 라운드 / 판 | Round | ラウンド | раунд | раунд | **rodada** |  |
| 스테이지 | Stage | ステージ | этап | етап | **fase** | `Fase {0}`. 세계수 단계(Estágio)와 구분 |
| 보스 | Boss | ボス | босс | бос | **chefe** | `Chefe à vista: {0}!`, 보스 도전 = `Desafio do chefe` |
| 파밍 | Farming | ファーミング | фарм | фарм | **colheita livre** | 보스 실패 뒤 스테이지 반복 |
| 처치 | defeat / Kills | 倒す / 撃破 | победить | перемогти | **derrotar / abates** | 통계 열 `Abates` |
| 수확기 | Harvester | 収穫機 | сборщик | збирач | **coletor** | 문장 속 소문자. 옛 용어 핀볼(pinball)도 모두 `coletor` |
| 영구 수확기 | permanent harvester | 恒久収穫機 | постоянный сборщик | постійний збирач | **coletor permanente** |  |
| 임시 수확기 | temporary harvester | 一時収穫機 | временный сборщик | тимчасовий збирач | **coletor temporário** | 짧은 칸 `coletor temp.` |
| 분신 | clone | 分身 | двойник | двійник | **clone** | 분신 수확기 = `Coletor Clone`, 분신 바 = `Barra Clone` |
| 분열 | Split | 分裂 | раздвоение | розділення | **Divisão / dividir** | 세포 분열 = `Divisão Celular`. 분열형(유형) = `Divisor` |
| 공격력 | Attack | 攻撃力 | атака | атака | **Ataque** | `Ataque ×{1}` |
| 공속 (노드) | Harvester Speed | スピード | Скорость сборщика | Швидкість атаки | **Vel. do Coletor** | 실제 효과는 날아가는 속도 (UK의 '공격 속도'는 따르지 않는다) |
| 이동 속도 | Move Speed | 移動速度 | Скорость движения | Швидкість руху | **Velocidade** |  |
| 배속 | Speed | 倍速 | скорость (×) | швидкість | **velocidade** | `Velocidade ×2` |
| 피해 | damage | ダメージ | урон | шкода | **dano** | 데미지 통계 = `Estatísticas de dano` |
| 치명타 | Critical Hit / Crit | クリティカル | критический удар / крит | критичний удар / крит | **Golpe Crítico / Crítico** | 외침 `Crítico!`, 확률 `Chance de Crítico`, 피해 `Dano Crítico` |
| 스킬 | Skill | スキル | навык | навичка | **habilidade** | 짧은 칸 `Hab.` |
| 바 (슬라이드 바) | bar | バー | планка | планка | **barra** | 바 스킬 = `habilidade da barra` |
| 버프 / 디버프 | buff / debuff | バフ / デバフ | бафф / дебафф | баф / дебаф | **buff / debuff** | pt-BR 게임 통용어, 소문자, 복수 `buffs` |
| 지속시간 | Duration | 持続時間 | Длительность | Тривалість | **Duração** |  |
| 제한시간 | Time Limit | 制限時間 | лимит времени | ліміт часу | **Tempo Limite** | 문장 속 `tempo limite` |
| 콤보 / 콤보 계수 | Combo / Combo Factor | コンボ / コンボ係数 | комбо / Коэффициент комбо | комбо / Коефіцієнт комбо | **combo / Fator de Combo** |  |
| 연쇄 | Chain | 連鎖 | цепь | ланцюг | **cadeia** | 연쇄 번개 = `Relâmpago em Cadeia` |
| 느림 / 약화 | slow / weaken | 鈍足 / 弱体 | замедление / ослабление | сповільнення / ослаблення | **lentidão / fraqueza** | 디버프 이름 |
| 관통 / 통과 | Pierce / pass-through | 貫通 / 通過 | Пронзание / сквозной | Пробиття / наскрізь | **Perfuração / atravessar** | 관통은 스킬, 통과는 젤리 특성 |
| 유도 | Homing | 誘導 | Наведение | Наведення | **Teleguiado** |  |
| 포자 | spore | 胞子 | спора | спора | **esporo** | 포자 구름 = `nuvem de esporos`, 포자 뿜기 = `Sopro de Esporos` |
| 체력 | HP | 体力 | здоровье | здоров'я | **vida** | 숫자 줄은 `HP`도 허용 |
| 군락 / 군락지 | colony | 群落 | колония | колонія | **colônia** | pt-BR 철자 `colônia`(ô) |
| 무더기 | cluster | 塊 | куча | купка | **monte** | 무더기 파쇄 = `Esmaga-Montes` |
| 거대 버섯 | Giant Mushroom | 巨大キノコ | гигантский гриб | гігантський гриб | **Cogumelo Gigante** | 등장 팝업 `Gigante à vista: {0}!` |
| 대왕 (버섯) | King | 大王 | Король / Гриб-великан | Король / Велетень | **Rei** | `Matsutake-Rei`, `Cogumelo-Rei Brasa` (RU·UK의 великан은 따르지 않고 EN·JA처럼 '왕') |
| 황금 변종 | Golden Variant | 黄金変種 | золотой вариант | золотий різновид | **Variante Dourada** | 외침 `Dourado!`(900523은 변종 감탄) |
| 변종 / 출현율 | variant / spawn rate | 変種 / 出現率 | вариант / шанс | різновид / частота | **variante / taxa** | `Taxa de Variantes` |
| 에픽 이상 | Epic+ | エピック以上 | эпический и выше | епічний і вище | **Épico+** | `Chance de Épico+` |
| 등급 | rarity | レアリティ | редкость | рідкість | **raridade** | Comum / Épico / Único / Lendário |
| 식용 / 약용 / 독 | Edible / Medicinal / Poison | 食用 / 薬用 / 毒 | Съедобный / Лекарственный / Ядовитый | Їстівний / Лікарський / Отруйний | **Comestível / Medicinal / Veneno(so)** | 분류 태그 `Veneno`, 도감 탭 `Venenoso`(독버섯) — 짧아야 탭에 들어간다 |
| 버섯 | mushroom | キノコ | гриб | гриб | **cogumelo** | 문장 속 소문자 |
| 갓 | cap | 傘 | шляпка | капелюшок | **chapéu** |  |
| 나무버섯 | Tree Mushroom | ツリーキノコ | древесный гриб | деревний гриб | **cogumelo da Árvore** | 세계수에 나는 버섯. 4종 이름은 부록 A |
| 숲 장치 | Forest Gadget | 森のしかけ | лесное устройство | лісовий пристрій | **Engenhoca da Floresta** | 노드 `숲 장치 강화` = `Engenhocas+` |
| 필드 / 벽 | field / wall | フィールド / 壁 | поле / стена | поле / стіна | **campo / parede** | 바닥 벽 = `parede de baixo` |

**세계수·꼬마·농장**

| KR | EN | JA | RU | UK | **PT (확정)** | 비고 |
|---|---|---|---|---|---|---|
| 꼬마 | Little One(s) | ちびっこ | малыш | малюк | **Pequenino(s)** | 고유 명사 → 문장 속에서도 대문자. 남성 명사 |
| 꼬마 이름 | Little + 이름 | 이름+ちびっこ | Малыш + 이름 | Малюк + 이름 | **Pequenino + 이름** | `Pequenino Brasa` 등 (부록 A) |
| 할아버지 (플레이어) | Grandpa | おじいちゃん | дедушка | дідусь | **Vovô** | 꼬마가 부르는 호칭 |
| 허수아비 아저씨 | Mr. Scarecrow | かかしさん | дядя Пугало | пане опудало | **Seu Espantalho** | 어른 NPC. 꼬마가 공손하게 인사 (`Olá, Seu Espantalho!`) |
| 부탁 | Favor | お願い | просьба | прохання | **pedido** | 꼬마의 부탁. 의뢰(missão)와 절대 혼용 금지 |
| 부탁을 들어주다 | Grant (a favor) | かなえる | исполнить | виконати | **atender (um pedido)** |  |
| 의뢰 | Quest(s) | 依頼 | задание | завдання | **Missão / Missões** | 일일·주간 과제. 화면 이름은 대문자 |
| 합류 | join | 仲間入り | присоединиться | приєднатися | **chegar / entrar** | `{0} chegou!`, `{0} pode entrar` |
| 호감도 | Affection | 好感度 | привязанность | прихильність | **Afeto** | `Afeto +{0}` |
| 진화 / 각성 | Evolve / Awakening | 進化 / 覚醒 | эволюция / Пробуждение | еволюція / Пробудження | **Evoluir · Evolução / Despertar** |  |
| 가호 | Blessing | 加護 | благословение | благословення | **Bênção** | 세계수의 가호 = `Bênção da Árvore do Mundo` |
| 공물 / 바치다 | Offering / Offer | お供え | подношение | дар | **Oferenda / oferecer** | 모두 바치기 = `Oferecer tudo` |
| 창고 | Granary | 倉庫 | кладовая | комора | **Celeiro** | 고유 명사 대문자 |
| 장식 | Decor / decoration | 飾り | украшение | декор | **enfeite** | 물건. 장식 완성 알림만 `Decoração pronta: {0}!` |
| 꾸미기 | Decorate | 飾りつけ | Украшение | Декор | **Decorar** | 탭 이름 |
| 장식 슬롯 / 칸 | slot | 枠 | слот | слот | **espaço** | 빈 칸 = `Espaço vazio`. 격자 칸(tile) = `quadrado` |
| 아름다움 | Beauty | 美しさ | красота | краса | **Beleza** | `Beleza: {0}` |
| 세트 | Set | セット | набор | набір | **conjunto** | 도감 세트 이름은 부록 A |
| 공방 | Workshop | 工房 | Мастерская | Майстерня | **Oficina** | 꼬마 공방 = `Oficina dos Pequeninos` |
| 제작 | Craft | 制作 | изготовление | виготовлення | **Fabricar / fabricação** | 버튼 `Fabricar` |
| 레시피 / 재료 | Recipe / Materials | レシピ / 材料 | рецепт / материалы | рецепт / матеріали | **receita / materiais** |  |
| 건물 / 건설 | building / construction | 建物 / 建設 | здание / стройка | споруда / будівництво | **construção / construir** | 건설 완료 = `Construção concluída!` |
| 구역 | Zone | エリア | зона | зона | **zona** | 뿌리·줄기·가지·수관. 구역 정원 = `vagas na zona` |
| 지역 | Area / Region | エリア | местность | локація | **região** | 숲·달빛 밤·들판·바다·폐허 도시 (구역과 구분) |
| 날씨 | Weather | 天気 | погода | погода | **Clima** | 좋은/나쁜 날씨 = `Clima bom` / `Clima ruim`, 오늘의 날씨 = `Clima de hoje` |
| 별 능력 | Star Bonus | 星パワー | Звёздный бонус | Зоряний бонус | **Bônus Estelar** | `★ Bônus Estelar: {0} ({1} por estrela)` |
| 능력치 | stats | 能力値 | характеристики | характеристики | **atributos** |  |
| 보유 효과 | Owned Bonus | 所持効果 | Бонус | Бонус | **Bônus de posse** | 보유 중 = `Obtido`, 미보유 = `Não obtido` |
| 도감 | Collection | 図鑑 | Атлас | Колекція | **Coleção** | 고유 명사 대문자. `Coleção: {0}/{1}` |
| 황금 도감 | Golden Collection | 黄金図鑑 | золотой атлас | Золота колекція | **Coleção Dourada** |  |
| 종 (도감 종수) | species | 種 | вид | вид | **espécie(s)** | `Espécies: {0}` |
| 선물 | Gift | プレゼント | подарок | подарунок | **presente** | `Presente ({0}): {1} ×{2}` |
| 칭호 | Title | 称号 | титул | титул | **Título** | `Título: “…”` |
| 상자 | Box | 箱 | ящик | скринька | **Caixa** | 꼬마의 상자 = `Caixa do Pequenino` |
| 떠돌이 | Wanderer | さすらい | бродячий | мандрівник | **andarilho** | 버섯 특성 `sempre andarilho` |
| 엔딩 / 기록 / 신기록 | Ending / Records / New record | エンディング / 記録 / 新記録 | Концовка / Рекорды | Фінал / Рекорди | **Final / Recordes / Novo recorde** | `Novo recorde! {0}` |
| 목장 / 농장 / 식당 | Ranch / Farm / Diner | 牧場 / 農場 / 食堂 | ферма / ферма / закусочная | ранчо / ферма / закусочна | **Rancho / Fazenda / Lanchonete** | 옛 건물 문구 |

**UI 공통어**

| KR | EN | JA | RU | UK | **PT (확정)** | 비고 |
|---|---|---|---|---|---|---|
| 해금 / 해금하다 | Unlock | 解放 | Открыть | Відкрити | **Desbloquear / desbloqueio** | 버튼 `Desbloquear {0}{1}`, 알림 `Desbloqueio: {0}` |
| 열림 (스테이지·지역) | Open / unlocked | 開く | Открыто | Відкрито | **Liberado / liberar** | `Fase {1} liberada` |
| 시작하기 | Start | はじめる | Начать | Почати | **Jogar** | 메인 메뉴. 라운드 시작 버튼도 같은 키 (`Jogar`) |
| 이어하기 / 계속하기 | Continue / Resume | つづきから / つづける | Продолжить | Продовжити | **Continuar / Retomar** | 5-1절 |
| 일시정지 | Pause | 一時停止 | Пауза | Пауза | **Pausar** |  |
| 옵션 / 설정 | Options / Settings | オプション / 設定 | Настройки | Налаштування | **Opções / Configurações** | 5-1절 |
| 초기화 | Reset | 初期化 | Сброс | Скинути | **Redefinir** | 저장 초기화. 타이머 `초기화까지` = `Renova em` |
| 게임 종료 | Quit Game | ゲーム終了 | Выйти из игры | Вийти з гри | **Sair do jogo** |  |
| 메인 메뉴 / 타이틀 | Main Menu | メインメニュー | Главное меню | Головне меню | **Menu principal** |  |
| 예 / 아니오 | Yes / No | はい / いいえ | Да / Нет | Так / Ні | **Sim / Não** |  |
| 닫기 / 취소 / 돌아가기 | Close / Cancel / Back | 閉じる / キャンセル / もどる | Закрыть / Отмена / Назад | Закрити / Скасувати / Назад | **Fechar / Cancelar / Voltar** |  |
| 켜짐 / 꺼짐 | On / Off | オン / オフ | Вкл. / Выкл. | Увімк. / Вимк. | **Ligado / Desligado** | `Mudo: ligado`, 소리 켬/끔 = `Som: ligado/desligado` |
| 소리 설정 / 효과음 / 배경음 | Sound Settings / Sound Effects / Music | サウンド設定 / 効果音 / BGM | Настройки звука / Звуковые эффекты / Музыка | Налаштування звуку / Звукові ефекти / Музика | **Som / Efeitos sonoros / Música** |  |
| 언어 | Language | 言語 | Язык | Мова | **Idioma** |  |
| 받기 / 받음 | Claim / Claimed | 受け取る / 受取済み | Забрать / Получено | Забрати / Отримано | **Receber / Recebido** |  |
| 구입 | Buy | 購入 | Купить | Купити | **Comprar** |  |
| 장착 / 착용 | Equip / Wear | 装備 / 着用 | Надеть | Одягнути | **Equipar / Vestir** | 수확기는 equipar, 꼬마 스킨은 vestir |
| 없음 / 미보유 | None / Not Owned | なし / 未所持 | Нет | Немає | **Nenhum / Não obtido** | 5-1절 |
| 부족 | Not enough X | Xが足りません | Не хватает X | Бракує X | **X insuficiente** | `Ouro insuficiente`, `Pedras de Micélio insuficientes` |
| 필요 / 선행 | need / Requires | 必要 / 前提 | нужно | потрібно | **Requer / necessário** | `Requer: {0}` |
| 전체 / 모두 | All | すべて | Все | Усі | **Tudo / Todos** | `Colher tudo`, `Cuidar de todos` |
| 다음 / 이전 / 지금 | Next / Previous / Now | 次の / これまで / いま | Далее / раньше / сейчас | Далі / раніше / зараз | **Próximo / anterior / agora** | `(agora: {0})`, `(antes: {0})` |
| 남은 시간 / 남음 | Time left / left | 残り時間 / 残り | Осталось | Залишилось | **Tempo restante / restante(s)** | `({0} restantes)` 대신 `(restam: {0})` |
| 완료 / 완성 | Done / Complete | 完了 / できあがり | Готово | Готово / Завершено | **Feito / Pronto** | 5-1절 |
| 비우기 | Clear | 空にする | Очистить | Очистити | **Esvaziar** | 날씨 맑음(Céu Limpo)과 구분 |
| 튜토리얼 / 업그레이드 / 오프라인 보상 | — | — | — | — | **(새로 만들지 않는다)** | KR 원문에 없는 말. 각각 안내 문장, `melhoria`(강화), `Colheita Automática`로 |

### 5-1. 헷갈리기 쉬운 짝 (반드시 구분)

| 짝 | PT | 이유 |
|---|---|---|
| 의뢰 ↔ 부탁 | **Missão** (일일·주간 과제) ↔ **Pedido** (꼬마의 부탁) | EN Quest/Favor, JA 依頼/お願い. 섞으면 안 된다 (EN 901157 오역 사례) |
| 스테이지 ↔ 세계수 단계 ↔ 레벨 | **Fase** ↔ **Estágio** ↔ **Lv. / nível** | EN Stage/Tier/Lv. 세 개가 겹치지 않게 |
| 해금 ↔ 열림 | **Desbloquear** (기능·노드·수확기) ↔ **Liberar / liberado** (스테이지·지역이 열림) | JA에서 `解放` 하나로 겹친 문제 |
| 맑음 ↔ 비우기 | **Céu Limpo** (날씨) ↔ **Esvaziar** (칸 비우기) | EN `Clear` 하나로 겹친 문제 |
| 분열 ↔ 분신 | **Divisão / dividir** (공이 둘로 갈라짐) ↔ **Clone** (복제체) | 다른 개념. 분열형(수확기 유형)은 `Divisor` |
| 없음 ↔ 미보유 ↔ 아니오 | **Nenhum** ↔ **Não obtido** ↔ **Não** | RU `Нет` 하나로 겹친 문제 |
| 이어하기 ↔ 계속하기 | **Continuar** (저장 이어서) ↔ **Retomar** (일시정지 해제) | |
| 옵션 ↔ 설정 | **Opções** (메인 메뉴 버튼) ↔ **Configurações** (설정 창 제목) | |
| 완성! ↔ 완료! | **Pronto!** (만들기 끝) ↔ **Feito!** (일 끝) | RU `Готово!` 하나로 겹친 문제 |
| 수확 끝! ↔ 수확 완료! | **Fim da colheita!** (시간 끝) ↔ **Colheita completa!** (모두 땀) | UK에서 겹친 문제 |
| 수확기 ↔ 수확 ↔ 수확량 | **Coletor** ↔ **Colheita / colher** ↔ **Rendimento** | |
| 황금 변종 ↔ 황금 수확기 ↔ 황금 나무버섯 | **Variante Dourada** ↔ **Coletor Dourado** ↔ **Cogumelo Áureo** | 문장의 `dourado`(형용사)와 이름을 구분 |
| 꼬마 ↔ 꼬마 수확기 ↔ 꼬마 건축가 | **Pequenino** ↔ **Coletor Mirim** ↔ **Construtor Mirim** | EN도 Little One ↔ Little Harvester로 구분 |
| 공물 ↔ 선물 | **Oferenda** (세계수에게) ↔ **Presente** (꼬마에게) | |
| 대왕(버섯) ↔ 거대 버섯 | **Rei** (`Cogumelo-Rei Brasa`, `Matsutake-Rei`) ↔ **Gigante** (`Cogumelo Gigante`) | EN King ↔ Giant |
| 장식(물건) ↔ 꾸미기(탭) | **Enfeite** ↔ **Decorar** | 장식 완성 알림만 성 고정용으로 `Decoração pronta` (2-5절) |

---

## 6. 의성어·감탄사

한국어 의성어는 문장 안에 박혀 있다. 기존 언어는 각자 자기 말의 의성어로 바꿨다(EN `Tap tap`, JA `とんてんかん`, RU `Тук-тук`). PT도 **브라질 아이가 쓰는 소리**로 바꾼다.

| KR | 뜻 | EN | **PT** |
|---|---|---|---|
| 뚝딱뚝딱 | 망치질 | Tap tap | `Toc-toc!` |
| 짤랑짤랑 / 짤랑 | 동전 소리 | Jingle jingle | `Tlim-tlim` |
| 딸랑 울림 | 방울 | Jingle | `Blim-blém` |
| 찌릿찌릿 | 전기 | Zap zap | `Bzzz-bzzz` |
| 휘이잉 | 바람 | Whoooosh | `Fiuuuu` |
| 쑥쑥 | 쑥쑥 자람 | (Growing like crazy) | `crescendo que nem mato` (관용구) |
| 영차영차 | 힘쓰는 구령 | Heave-ho | `Upa, upa!` |
| 쏙쏙 | 쏙 넣음 | nice and snug | `direitinho` |
| 냠냠 | 먹는 소리 | Mmm | `Nham-nham` |
| 콩! | 부딪힘 | Bonk! | `Poc!` |
| 아야! | 아픔 | Ouch! | `Ai!` |
| 으앗! | 놀람 | Eep! | `Eita!` |
| 와아! | 감탄 | Wow! | `Uau!` |
| 휴~ | 안도 | Phew~ | `Ufa~` |
| 헤헤 | 웃음 | Hehe | `Hehe` |
| 쉿 | 조용 | Shh | `Psiu` |
| 퐁퐁 / 퐁 | 물·방귀 | — | `Plop` / `Pum` |
| 앗 뜨거! | 뜨거움 | Ow, hot! | `Ai, que quente!` |

---

## 7. 예시 번역 (기준 문장)

| ID | 종류 | KR | EN (참고) | **PT** | 지킨 규칙 |
|---|---|---|---|---|---|
| 201006 | 노드 설명 | 한 라운드의 제한시간을 늘려요. 라운드는 1~4스테이지 10초, 그 뒤로는 10초에 스테이지마다 0.5초씩(최대 +10초) 길어지고, 여기에 더해져요. | Increases a round's time limit. … | `Aumenta o tempo limite da rodada. Nas fases 1 a 4, a rodada dura 10 segundos; depois, ganha 0,5 segundo a cada fase (até +10 segundos). Este bônus é somado a isso.` | 3인칭 현재, 현지 소수점, 범위 표기 |
| 202004 | 노드 설명 | 수확기가 바에 맞을 때마다 콤보가 쌓이고, … 바닥 벽에 닿으면 콤보가 끊겨요. | Each time a harvester hits the bar, … | `Cada vez que o coletor bate na barra, o combo aumenta, e cada combo eleva o multiplicador de pontos. Aumenta o quanto esse multiplicador sobe. Tocar a parede de baixo quebra o combo.` | 용어 coletor·barra·combo |
| 900828 | 튜토리얼 | 수확기를 잡고 뒤로 끌었다 놓으면 발사! | Grab the Harvester, pull back, and release to launch! | `Segure o coletor, puxe para trás e solte para lançar!` | 명령형 |
| 900855 | 버튼 | 라운드 끝내고 정산하기 | End Round & Cash Out | `Encerrar e receber` | 짧은 칸, 문장형 대소문자 |
| 900833 | 정산 제목 | 이번 판 정산 골드 `<size=60%><color=#8a6a4a>(점수 골드 + 보너스)</color></size>` | This Round's Payout Gold … | `Lucro da rodada <size=60%><color=#8a6a4a>(ouro dos pontos + bônus)</color></size>` | 태그 보존, 정산 = Lucro |
| 900580 | 정보 | 시간당 {0}{1}  {2}{3} `<size=80%>…(스테이지·버섯 오두막·꼬마 효과·'자동 수확 확장'으로 늘어나요)…` | {0}{1} per hour … | `{0}{1} por hora  {2}{3} <size=80%><color=#8a6a4a>(aumenta com a fase, o Chalé de Cogumelo, os bônus dos Pequeninos e “Colheita Automática+”)</color></size>` | 아이콘 자리 붙임, 이름 인용은 노드 이름과 똑같이 |
| 900461 | 보유 효과 | 보유 효과: 부탁을 들어줄 때 균사석 +1 확률 +5%p | Owned Bonus: Chance of +1 Mycelium Stone when granting a favor +5% | `Bônus de posse: chance de +1 Pedra de Micélio ao atender um pedido +5%` | %p → %, 부탁 = pedido |
| 900655 | 알림 | {0} {1}(으)로 진화했어요! 부탁 보상 ×{2} | {0} evolved into {1}! Favor reward ×{2} | `{0} evoluiu para {1}! Recompensa dos pedidos ×{2}` | 동사 완료형(성 중립), 무관사 `para` |
| 900534 | 전투 알림 | 분신이 {0}마리 남았어요! | Clones left: {0}! | `Clones restantes: {0}!` | 복수 회피 |
| 900203 | 의뢰 | 일일 의뢰 {n}개 완료하기 | Complete {n} daily quests | `Concluir missões diárias: {n}` | 복수 회피, `{n}` |
| 900201 | 의뢰 | {m} {n}번 수확하기 | Harvest {m} ({n} times) | `Colher: {m} (×{n})` | 이름 콜론 뒤 |
| 900527 | 전투 팝업 | 황금 {0}! | Golden {0}! | `Variante dourada: {0}!` | 성 고정 명사 |
| 900153+154 | 조립 | 해금하면 {0}사용 시작 + 발동률 10%로 | Unlocks the skill{0} + : 10% trigger chance | `Libera a habilidade{0}` + `: chance de ativação de 10%` | 2-7절 |
| 900860 | 긴 안내 | 가장 싼 강화부터 …\n노드 하나만 끝까지 올리려면 균사 트리에서 그 노드를 `<b>Shift+클릭</b>` 또는 `<b>우클릭</b>`하세요. | … `<b>Shift+Click</b>` or `<b>Right-Click</b>` … | `Compra primeiro as melhorias mais baratas, quantas o seu ouro permitir, de uma vez. Só contam os nós desbloqueados (incluindo o Núcleo, exceto os nós de Pedra de Micélio).\nPara maximizar um único nó, use <b>Shift+clique</b> ou <b>clique direito</b> nele na Árvore de Micélio.` | `\n` 1개, 태그 보존 |
| 900502 | 말풍선 | 부탁 있음! | Favor Available! | `Tem pedido!` | 짧게 |
| 900041 | 꼬마 대사 | 할아버지, 오늘도 수확 잘했어요? | Grandpa, did you harvest well today? | `Vovô, colheu bastante hoje?` | 호칭 Vovô |
| 900011 | 꼬마 대사 | 앗 뜨거! 아니, 내가 뜨거운 거구나! | Ow, hot! Wait, I'm the hot one! | `Ai, que quente! Ah, não… o quente sou eu!` | 남성형 |
| 900020 | 꼬마 대사 | 짤랑짤랑~ 부자가 될 거야! | Jingle jingle~ I'm gonna be rich! | `Tlim-tlim~ Vou ficar rico!` | 의성어, 남성형 |
| 900043 | 꼬마 대사 | 심심해~ 놀아 줘! | I'm bored~ Play with me! | `Tô entediado~ Brinca comigo!` | 구어 허용 |
| 900025 | 꼬마 대사 | 반짝반짝 작은 별~ | Twinkle twinkle little star~ | `Brilha, brilha, estrelinha~` | 현지 동요 |
| 901157 | 안내 | 경험치가 모자라요 (창고 버섯을 바치거나 의뢰를 해요) | Not enough EXP (offer Granary mushrooms or complete Quests) | `XP insuficiente (ofereça cogumelos do Celeiro ou cumpra Missões)` | 의뢰 = Missão |
| 901080 | 보상 | 구역 정원 +1 | Zone Capacity +1 | `Vagas na zona +1` | 정원 = 인원 한도 (Garden 오역 주의) |
| 309004 | 보스 기믹 | 체력 66%·33%에서 분신을 불러요. 분신까지 모두 쓰러뜨려야 해요 | Summons clones at 66% and 33% HP. You must defeat the clones too | `Invoca clones com 66% e 33% de vida. É preciso derrotar os clones também` | 3인칭 현재 |
| 308006 | 날씨 효과 | 바 길이 ×0.75, 제한시간 −3초 | Bar length ×0.75, time limit -3s | `Tamanho da barra ×0,75, tempo limite −3 s` | 현지 소수점, `−` 유지 |
| 900594 | 복합 | `<color={0}>{1}황금 도감</color> <color=#8a7a6a>{2}/{3}</color>  <size=80%>…5종 골드 +10% · 15종 +25%·황금 수확기 · {3}종 +60%…` | … | `<color={0}>{1}Coleção Dourada</color> <color=#8a7a6a>{2}/{3}</color>  <size=80%><color=#8a7a6a>5 espécies: ouro +10% · 15 espécies: +25% · Coletor Dourado · {3} espécies: +60%</color></size>` | 숫자가 고정(5·15)인 곳은 복수형 그대로 OK, `{3}`은 항상 큰 수라 복수 OK |

---

## 8. 작업 방법 (개발·데이터 담당)

### 8-1. 코드·데이터 변경 (번역 전에 한 번) — 순서를 지킬 것

1. **엑셀에 열부터 만든다**: `python BalanceData/db/db_write.py addcols Assets/DB/MMM_DB_Ver.05.xlsx <json>`로 `String` 시트 I열에 `내용_PT` / `Str_PT` / `string`. (C# 필드를 먼저 넣으면 DbImport가 "열 없음"으로 멈춘다 — `DbImport.cs:79`.) **엑셀은 COM으로만 쓴다(openpyxl 저장 금지: 도형이 지워진다).**
2. `Assets/Scripts/Core/Db.cs:54` String 행 클래스에 `Str_PT`, `:151` `LangTable`의 `switch`에 `"pt" => s.Str_PT` (빠뜨리면 pt를 골라도 한국어로 보인다).
3. `Assets/Scripts/Core/Loc.cs:14-18` `LANGS`에 `"pt"`, `LANG_NAME`에 `["pt"] = "Português"`.
4. `Assets/Editor/LocTools.cs`: `:24-33` `FONT_SETS`에 `("pt", "Nunito-Black", "Nunito-SemiBold")`, `:386-395` 폰트 굽기 글자 모음에 `Str_PT`, `:274` 번역 칸 집계에 `Str_PT`.
5. `BalanceData/db/db_write.py:84` 새 줄 추가 때 `Str_PT` 칸.
6. 언어 창(`LangWindow`)은 버튼 6개가 되는지 화면에서 확인(임시 창은 자동, 씬에 배치한 LangPanel이 생기면 `Lang_pt` 버튼 필요).
7. 한·영 전용 UI 폰트 목록(`KoEnLang`)에는 `pt`를 **넣지 않는다**(자동으로 Nunito로 바뀐다).

### 8-2. 번역 넣기

1. 번역은 JSON으로 만든다: `{"String": {"<Str_ID>": {"Str_PT": "…"}}}`. 줄이 많으면 범주별로 나눠 여러 번.
2. 넣기 전 검사(기존 `BalanceData/db/loc_fix_1019.py`의 `check()`와 같은 기준): `{n}` 집합, 태그 개수·종류, `\n` 개수가 KR과 같은지, 한글이 섞이지 않았는지, 앞뒤 공백이 KR과 같은 꼴인지.
3. `python BalanceData/db/db_write.py set Assets/DB/MMM_DB_Ver.05.xlsx <json>` — 쓰기 전에 `BalanceData/db_backup/auto/`에 자동 백업. 엑셀이 열려 있으면 멈춘다(종료 코드 2).
4. `python -I BalanceData/db/dbimport_nounity.py Assets/DB/MMM_DB_Ver.05.xlsx Assets/Resources/DB/db.json MMM_DB_Ver.05.xlsx`로 db.json 다시 만들기.
5. Unity 메뉴 **`MoreMush/다국어/4 폰트 굽기`** — 새 글자(악센트)가 Nunito SDF에 구워져야 보인다. 굽기 뒤 PT 열의 모든 글자가 폰트에 있는지 확인.

### 8-3. 화면 확인

- 언어 창에서 `Português` 선택 → MainMenu·MainGame·Stage·OutGame을 한 바퀴. 옵션 창·언어 창·도감·트리 툴팁·정산·보스 등장·꼬마 대사.
- 넘침: 4-3절 칸, 트리 노드 이름, 도감 카드 이름, 공방 카드, 데미지 통계 열.
- 콘솔: `[Loc] DB String 시트에 없는 문구` 경고, `[Loc] 형식 오류 (pt)` 오류가 없어야 한다.

---

## 9. 검수 체크리스트 (줄마다 / 전체)

**줄마다**
- [ ] KR 원문의 뜻 그대로인가 (EN과 다르면 KR 기준)
- [ ] `{n}`·`{m}`·`{0}`… 번호·개수, 태그, `\n`, 앞뒤 공백
- [ ] 용어가 `PT_용어집.tsv`와 같은가
- [ ] `{0}`이 이름인 줄: 관사·축약·형용사 일치 없음 (2-5절)
- [ ] 숫자 뒤 복수형 없음 (2-4절)
- [ ] 화자에 맞는 문체 (3-1절), 꼬마는 남성형·`Vovô`
- [ ] 길이: EN ×1.15 이내, 줄바꿈 없는 칸은 같은 범주 기존 최장 이내
- [ ] 대소문자 (3-4절), 기호 (2-6절), 숫자 표기 (2-9절)

**전체 (자동 검사로)**
- [ ] 빈 칸 = EN이 빈 줄과 정확히 같은가
- [ ] 한글이 섞인 PT 칸 0 (언어 이름 `한국어` 제외)
- [ ] 같은 KR 용어가 PT에서 두 가지로 갈리지 않는가 (용어집 각 항목을 KR로 찾아 PT 대응을 센다)
- [ ] 서로 다른 KR이 같은 PT가 되는 충돌 (특히 짧은 라벨 — 5-1절)
- [ ] 옛 표현 잔존 0: `pinball`·`proc` 계열을 PT로 옮긴 흔적(`pinball` → `coletor`), pt-PT 낱말(`ecrã`, `miúdo`, `telemóvel`, `tu` 활용)
- [ ] 폰트 굽기 뒤 PT 글자 누락 0

---

## 10. 질문·결정이 필요한 것 (번역 시작 전에 정할 것)

| # | 내용 | 영향 | 제안 |
|---|---|---|---|
| 1 | **천 단위 쉼표**: 코드는 `1,234`로 쓰는데 브라질 사람은 `1,234`를 "1.234(일 점 이삼사)"로 읽는다 | 골드·점수가 1/1000로 보일 수 있다 | `Lang == "pt"`일 때만 `U.Fmt`의 천 단위 구분을 `.`(또는 얇은 공백)로 바꾸는 코드 수정 검토. 최소한 1만 미만 숫자의 쉼표만이라도 |
| 2 | 처음 실행 언어가 기기 언어와 무관하게 영어 (`Loc.DefaultLang = "en"`) | 브라질 사용자가 언어 창을 찾아야 한다 | 기기 언어가 포르투갈어면 `pt`로 시작하는 처리 (다른 브랜치의 "시작언어 연동" 커밋과 함께 검토) |
| 3 | `{0}종`(KR 한 줄)이 버섯 종 수와 장식 개수에 같이 쓰인다 (`FarmTabWindow.cs:645`) | `Espécies: {0}`이 장식에도 붙는다 | 중립 `Total: {0}` 또는 코드에서 키 분리 |
| 4 | 900153+900154 조립 | 콜론 꼴만 가능 | 장기적으로는 `Loc.F("해금하면 발동률 {0}로 사용 시작")`처럼 한 문구로 합치는 코드 수정 |
| 5 | 꼬마 이름 형식 `Pequenino + 명사` (예 `Pequenino Brasa`) | 캐릭터 정체성 | 대안: 지소사 단독 이름(`Brasinha`, `Faisquinha`, `Pedrinho`…). 이 문서는 EN `Little Ember`·RU `Малыш Уголёк`과 같은 구조인 앞의 안을 확정안으로 둔다 |
| 6 | 판타지 버섯·도감 이름 중 학명 확인이 약한 3종 (비단빛깔때기버섯, 뱀껍질광대버섯, 덕다리버섯) | 이름 정확도 | 해당 줄: 900399 · 900213 · 900246 (부록 A 학명 열). 실제 종보다 게임 그림과 설명문을 우선 |
| 7 | 장래 pt-PT를 따로 낼지 | 언어 코드 | 내지 않는다면 `pt` 하나로. 낼 거라면 지금부터 `pt-BR` 코드로 |

---

## 부록 A. 고유명사 전체 (PT 확정안)

`비고`: **확립** = 포르투갈어권에서 통용되는 이름 · **차용** = 원어 그대로(Shiitake 등) · **번역** = 뜻을 옮김 · **판타지** = 게임 고유 · **범주 최장 초과** = 같은 범주 기존 최장 길이보다 길어 화면 확인이 필요한 줄 (4-1절).

### 1a 도감 버섯 (75) — 기존 언어 최장 24자

| Str_ID | KR | EN | JA | RU | UK | 학명 | **PT** | 비고 |
|---|---|---|---|---|---|---|---|---|
| 900228 | 양송이 | Button Mushroom | マッシュルーム | Шампиньон | Печериця | *Agaricus bisporus* | **Champignon** | 확립 |
| 900220 | 느타리 | Oyster Mushroom | ヒラタケ | Вешенка | Глива | *Pleurotus ostreatus* | **Cogumelo-ostra** | 확립 |
| 900219 | 표고 | Shiitake | シイタケ | Шиитаке | Шиітаке | *Lentinula edodes* | **Shiitake** | 차용 |
| 900287 | 팽이버섯 | Enoki | エノキタケ | Эноки | Енокі | *Flammulina velutipes* | **Enoki** | 차용 |
| 900206 | 목이버섯 | Wood Ear | キクラゲ | Древесное ухо | Деревне вухо | *Auricularia auricula-judae* | **Orelha-de-judas** | 확립 |
| 900292 | 새송이버섯 | King Oyster Mushroom | エリンギ | Королевская вешенка | Королівська глива | *Pleurotus eryngii* | **Cogumelo-ostra-rei** | 확립 |
| 900204 | 먹물버섯 | Inky Cap | ササクレヒトヨタケ | Навозник белый | Гнойовик | *Coprinus comatus* | **Cogumelo-tinta** | 확립 |
| 900211 | 말불버섯 | Puffball | ホコリタケ | Дождевик | Дощовик | *Lycoperdon perlatum (Lycoperdon spp.)* | **Bufa-de-lobo** | 확립 |
| 900299 | 꾀꼬리버섯 | Chanterelle | アンズタケ | Лисичка | Лисичка | *Cantharellus cibarius* | **Cantarelo** | 확립 |
| 900246 | 덕다리버섯 | Chicken of the Woods | アイカワタケ | Трутовик серно-жёлтый | Лісова курка | *Laetiporus sulphureus* | **Frango-do-mato** | 확립 |
| 900229 | 큰갓버섯 | Parasol Mushroom | カラカサタケ | Зонтик пёстрый | Гриб-парасолька | *Macrolepiota procera* | **Cogumelo-parasol** | 확립 |
| 900305 | 민자주방망이버섯 | Wood Blewit | ムラサキシメジ | Рядовка фиолетовая | Рядовка фіолетова | *Lepista nuda (= Clitocybe nuda)* | **Pé-azul** | 확립 |
| 900308 | 기와버섯 | Green-Cracking Russula | アイタケ | Сыроежка зеленоватая | Сироїжка зеленувата | *Russula virescens* | **Rússula-verde** | 번역 |
| 900311 | 만가닥버섯 | Beech Mushroom | ブナシメジ | Шимеджи | Шимеджі | *Hypsizygus marmoreus* | **Shimeji-buna** | 차용 |
| 900314 | 곰보버섯 | Morel | アミガサタケ | Сморчок | Зморшок | *Morchella esculenta (Morchella spp.)* | **Morchela** | 확립 |
| 900317 | 달걀버섯 | Caesar's Mushroom | タマゴタケ | Цезарский гриб | Цезарів гриб | *Amanita hemibapha (한국·일본 종)* | **Amanita-dos-césares** | 확립 |
| 900235 | 소혀버섯 | Beefsteak Fungus | カンゾウタケ | Печёночница | Печіночниця | *Fistulina hepatica* | **Língua-de-boi** | 확립 |
| 900321 | 능이버섯 | Neungi Mushroom | コウタケ | Нынги | Саркодон | *Sarcodon aspratus* | **Neungi** | 차용 |
| 900001 | 송이버섯 | Matsutake | マツタケ | Мацутакэ | Мацутаке | *Tricholoma matsutake* | **Matsutake** | 차용 |
| 900209 | 송로버섯 | Truffle | トリュフ | Трюфель | Трюфель | *Tuber spp. (T. melanosporum 등)* | **Trufa** | 확립 |
| 900327 | 싸리버섯 | Yellow Coral Mushroom | ホウキタケ | Рогатик | Рогатик | *Ramaria botrytis (Ramaria spp.)* | **Coral-amarelo** | 번역 |
| 900329 | 달빛표고 | Moonlight Shiitake | 月光シイタケ | Лунный шиитаке | Місячне шиітаке | — (허구) | **Shiitake do Luar** | 판타지 |
| 900331 | 주름버섯 | Meadow Mushroom | ハラタケ | Шампиньон луговой | Печериця лучна | *Agaricus campestris* | **Cogumelo-do-campo** | 확립 |
| 900333 | 바다송이 | Sea Matsutake | ウミマツタケ | Морской мацутакэ | Морське мацутаке | — (허구) | **Matsutake-do-mar** | 판타지 |
| 900335 | 벽돌먹물버섯 | Brick Inky Cap | レンガヒトヨタケ | Кирпичный навозник | Цегляний гнойовик | — (허구) | **Cogumelo-tinta-tijolo** | 판타지 |
| 900221 | 구름버섯 | Turkey Tail | カワラタケ | Траметес разноцветный | Індичий хвіст | *Trametes versicolor* | **Cauda-de-peru** | 확립 |
| 900338 | 치마버섯 | Split Gill | スエヒロタケ | Щелелистник | Щілинник | *Schizophyllum commune* | **Cogumelo-leque** | 번역 |
| 900247 | 간버섯 | Cinnabar Polypore | ヒイロタケ | Трутовик киноварный | Кіноварний трутовик | *Pycnoporus coccineus* | **Orelha-de-pau-vermelha** | 확립 |
| 900207 | 흰목이 | Snow Fungus | シロキクラゲ | Снежный гриб | Сніговий гриб | *Tremella fuciformis* | **Fungo-da-neve** | 확립 |
| 900344 | 잎새버섯 | Maitake | マイタケ | Майтакэ | Майтаке | *Grifola frondosa* | **Maitake** | 차용 |
| 900347 | 한입버섯 | Veiled Polypore | ヒトクチタケ | Гриб на один укус | Грибок-шматочок | *Cryptoporus volvatus* | **Poliporo-velado** | 번역 |
| 900350 | 복령 | Poria | ブクリョウ | Пория | Порія | *Wolfiporia extensa (= Poria cocos)* | **Poria** | 차용 |
| 900353 | 저령 | Zhuling | チョレイマイタケ | Трутовик зонтичный | Парасольковий трутовик | *Polyporus umbellatus* | **Zhuling** | 차용 |
| 900236 | 꽃송이버섯 | Cauliflower Mushroom | ハナビラタケ | Спарассис | Грибна цвітна капуста | *Sparassis crispa* | **Cogumelo-couve-flor** | 확립 |
| 900358 | 노루궁뎅이 | Lion's Mane | ヤマブシタケ | Ежовик гребенчатый | Їжовик гребінчастий | *Hericium erinaceus* | **Juba-de-leão** | 확립 |
| 900240 | 말굽버섯 | Hoof Fungus | ツリガネタケ | Трутовик копытный | Копитний трутовик | *Fomes fomentarius* | **Casco-de-cavalo** | 확립 |
| 900222 | 잔나비걸상버섯 | Artist's Conk | コフキサルノコシカケ | Трутовик плоский | Плаский трутовик | *Ganoderma applanatum* | **Cogumelo-do-artista** | 확립 |
| 900212 | 목도리방귀버섯 | Collared Earthstar | エリマキツチグリ | Звездовик | Земляна зірка | *Geastrum triplex* | **Estrela-da-terra** | 확립 |
| 900248 | 소나무잔나비버섯 | Red-Belted Conk | ツガサルノコシカケ | Трутовик окаймлённый | Сосновий трутовик | *Fomitopsis pinicola* | **Orelha-de-pinheiro** | 번역 |
| 900368 | 영지버섯 | Reishi | 霊芝 | Рейши | Рейші | *Ganoderma lucidum (G. lingzhi)* | **Reishi** | 차용 |
| 900241 | 상황버섯 | Sanghwang Mushroom | メシマコブ | Санхван | Санхван | *Phellinus linteus (Sanghuangporus sanghuang)* | **Sanghwang** | 차용 |
| 900372 | 차가버섯 | Chaga | カバノアナタケ | Чага | Чага | *Inonotus obliquus* | **Chaga** | 차용 |
| 900249 | 말똥진흙버섯 | Willow Bracket | キコブタケ | Трутовик ложный | Несправжній трутовик | *Phellinus igniarius* | **Casco-de-salgueiro** | 번역 |
| 900005 | 동충하초 | Cordyceps | 冬虫夏草 | Кордицепс | Кордицепс | *Cordyceps militaris* | **Cordyceps** | 차용 |
| 900252 | 눈꽃동충하초 | Snowflake Cordyceps | ハナサナギタケ | Снежный кордицепс | Сніжинка-кордицепс | *Isaria japonica (= Paecilomyces tenuipes)* | **Cordyceps-da-neve** | 번역 |
| 900380 | 자작나무버섯 | Birch Polypore | カンバタケ | Берёзовый трутовик | Березова губка | *Fomitopsis betulina (= Piptoporus betulinus)* | **Poliporo-da-bétula** | 확립 |
| 900382 | 달빛동충하초 | Moonlight Cordyceps | 月光冬虫夏草 | Лунный кордицепс | Місячний кордицепс | — (허구) | **Cordyceps do Luar** | 판타지 |
| 900002 | 선녀낙엽버섯 | Fairy Ring Mushroom | シバフタケ | Опёнок луговой | Опеньок луговий | *Marasmius oreades* | **Anel-de-fada** | 확립 |
| 900385 | 산호버섯 | Sea Coral Mushroom | サンゴタケ | Рогатик коралловый | Коралівка | — (허구) | **Coral-do-mar** | 판타지 |
| 900387 | 녹슨영지 | Rusty Reishi | さびた霊芝 | Ржавый рейши | Іржаве рейші | — (허구) | **Reishi Enferrujado** | 판타지 |
| 900389 | 무당버섯 | Russula | ドクベニタケ | Сыроежка | Сироїжка | *Russula emetica* | **Rússula-vermelha** | 번역 |
| 900223 | 노란다발 | Sulphur Tuft | ニガクリタケ | Ложноопёнок | Сірчаний опеньок | *Hypholoma fasciculare* | **Tufo-de-enxofre** | 확립 |
| 900230 | 노란각시버섯 | Flowerpot Parasol | コガネキヌカラカサタケ | Лейкокопринус жёлтый | Горщиковий жовтик | *Leucocoprinus birnbaumii* | **Cogumelo-de-vaso** | 확립 |
| 900394 | 맑은애주름버섯 | Lilac Bonnet | サクラタケ | Мицена чистая | Міцена чиста | *Mycena pura* | **Micena-lilás** | 번역 |
| 900205 | 이끼꽃버섯 | Parrot Waxcap | ワカクサタケ | Мохоцветный гриб | Мохоцвіт | *Gliophorus psittacinus (= Hygrocybe psittacina)* | **Cogumelo-papagaio** | 번역 |
| 900399 | 비단빛깔때기버섯 | Silky Funnel Cap | キヌジョウゴタケ | Говорушка шелковистая | Шовкова говорушка | *Clitocybe sp. (흰 깔때기류, 확인 필요)* | **Funil-sedoso** | 번역 |
| 900231 | 땀버섯 | Fiber Cap | アセタケ | Волоконница | Волоконниця | *Inocybe rimosa (Inocybe spp.)* | **Chapéu-fibroso** | 번역 |
| 900239 | 갈황색미치광이버섯 | Laughing Gym | オオワライタケ | Гимнопил | Сміхотливий гімнопіл | *Gymnopilus junonius (= G. spectabilis)* | **Cogumelo-risonho** | 번역 |
| 900004 | 광대버섯 | Fly Agaric | ベニテングタケ | Мухомор | Червоний мухомор | *Amanita muscaria* | **Mata-moscas** | 확립 |
| 900213 | 뱀껍질광대버섯 | Snakeskin Grisette | ヘビキノコモドキ | Мухомор змеиный | Зміїний мухомор | *Amanita ceciliae (= A. inaurata)* | **Amanita-pele-de-cobra** | 번역 |
| 900234 | 붉은싸리버섯 | Red Coral Mushroom | ハナホウキタケ | Рогатик красный | Червоний корал | *Ramaria formosa* | **Coral-vermelho** | 번역 |
| 900214 | 알광대버섯 | Death Cap | タマゴテングタケ | Бледная поганка | Бліда поганка | *Amanita phalloides* | **Chapéu-da-morte** | 확립 |
| 900413 | 마귀곰보버섯 | False Morel | シャグマアミガサタケ | Строчок обыкновенный | Диявольський строчок | *Gyromitra esculenta* | **Falsa-morchela** | 확립 |
| 900416 | 독깔때기버섯 | Poison Funnel Cap | ドクササコ | Говорушка ядовитая | Отруйна говорушка | *Paralepistopsis acromelalga (= Clitocybe acromelalga)* | **Funil-venenoso** | 번역 |
| 900208 | 화경버섯 | Jack-o'-Lantern Mushroom | ツキヨタケ | Омфалот | Гарбузовий ліхтар | *Omphalotus japonicus* | **Cogumelo-lanterna** | 번역 |
| 900215 | 개나리광대버섯 | Yellow Amanita | タマゴテングタケモドキ | Мухомор жёлтый | Жовтий мухомор | *Amanita subjunquillea* | **Amanita-amarela** | 번역 |
| 900216 | 마귀광대버섯 | Panther Cap | テングタケ | Мухомор пантерный | Пантерний мухомор | *Amanita pantherina* | **Amanita-pantera** | 확립 |
| 900003 | 독우산광대버섯 | Destroying Angel | ドクツルタケ | Белая поганка | Смердючий мухомор | *Amanita virosa* | **Anjo-destruidor** | 확립 |
| 900261 | 붉은사슴뿔버섯 | Poison Fire Coral | カエンタケ | Красный олений рог | Червоний оленячий ріг | *Podostroma cornu-damae* | **Coral-de-fogo** | 번역 |
| 900210 | 갈색고리갓버섯 | Deadly Dapperling | キツネノカラカサ | Лепиота бурокрасная | Гриб-парасолька бурий | *Lepiota brunneoincarnata* | **Lepiota-mortal** | 번역 |
| 900429 | 흰가시광대버섯 | White Spiny Amanita | シロオニタケ | Мухомор белый колючий | Білий колючий мухомор | *Amanita virgineoides* | **Amanita-espinhosa** | 번역 |
| 900431 | 달빛애주름버섯 | Moonlit Bonnet | 月光クヌギタケ | Мицена лунная | Місячна міцена | — (허구) | **Micena do Luar** | 판타지 |
| 900433 | 말똥버섯 | Dung Cap | ヒカゲタケ | Панеолус | Панеолус | *Panaeolus papilionaceus (Panaeolus spp.)* | **Cogumelo-do-esterco** | 번역 |
| 900435 | 해파리버섯 | Jellyfish Mushroom | クラゲタケ | Гриб-медуза | Гриб-медуза | — (허구) | **Cogumelo-água-viva** | 판타지 |
| 900437 | 콘크리트광대버섯 | Concrete Amanita | コンクリートテングタケ | Бетонный мухомор | Бетонний мухомор | — (허구) | **Amanita de Concreto** | 판타지 |

### 1b 특수 버섯 (20) — 기존 언어 최장 23자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900472 | 불씨 버섯 | Ember Mushroom | ほのおキノコ | Гриб Уголёк | Гриб Жаринка | **Cogumelo Brasa** | 판타지 |
| 900473 | 불씨 대왕버섯 | King Ember Mushroom | ほのお大王キノコ | Гриб-великан Уголёк | Велетень Жаринка | **Cogumelo-Rei Brasa** | 판타지 |
| 900474 | 찌릿 버섯 | Zappy Mushroom | ビリビリキノコ | Гриб Искорка | Гриб Іскринка | **Cogumelo Faísca** | 판타지 |
| 900475 | 찌릿 대왕버섯 | King Zappy Mushroom | ビリビリ大王キノコ | Гриб-великан Искорка | Велетень Іскринка | **Cogumelo-Rei Faísca** | 판타지 |
| 900476 | 이슬 버섯 | Dew Mushroom | しずくキノコ | Гриб Росинка | Гриб Росинка | **Cogumelo Orvalho** | 판타지 |
| 900477 | 이슬 대왕버섯 | King Dew Mushroom | しずく大王キノコ | Гриб-великан Росинка | Велетень Росинка | **Cogumelo-Rei Orvalho** | 판타지 |
| 900478 | 동전 버섯 | Coin Mushroom | コインキノコ | Гриб Монетка | Гриб Монетка | **Cogumelo Moeda** | 판타지 |
| 900479 | 동전 대왕버섯 | King Coin Mushroom | コイン大王キノコ | Гриб-великан Монетка | Велетень Монетка | **Cogumelo-Rei Moeda** | 판타지 |
| 900480 | 별똥 버섯 | Stardrop Mushroom | ほしくずキノコ | Гриб Звёздочка | Гриб Зорепадик | **Cogumelo Estrela** | 판타지 |
| 900481 | 별똥 대왕버섯 | King Stardrop Mushroom | ほしくず大王キノコ | Гриб-великан Звёздочка | Велетень Зорепадик | **Cogumelo-Rei Estrela** | 판타지 |
| 900482 | 새싹 버섯 | Sprout Mushroom | めばえキノコ | Гриб Росток | Гриб Паросток | **Cogumelo Broto** | 판타지 |
| 900483 | 새싹 대왕버섯 | King Sprout Mushroom | めばえ大王キノコ | Гриб-великан Росток | Велетень Паросток | **Cogumelo-Rei Broto** | 판타지 |
| 900484 | 달빛 버섯 | Moonlight Mushroom | つきあかりキノコ | Гриб Лунник | Гриб Місячко | **Cogumelo Luar** | 판타지 |
| 900485 | 달빛 대왕버섯 | King Moonlight Mushroom | つきあかり大王キノコ | Гриб-великан Лунник | Велетень Місячко | **Cogumelo-Rei Luar** | 판타지 |
| 900486 | 바람 버섯 | Breeze Mushroom | かぜキノコ | Гриб Ветерок | Гриб Вітерець | **Cogumelo Brisa** | 판타지 |
| 900487 | 바람 대왕버섯 | King Breeze Mushroom | かぜ大王キノコ | Гриб-великан Ветерок | Велетень Вітерець | **Cogumelo-Rei Brisa** | 판타지 |
| 900488 | 조약돌 버섯 | Pebble Mushroom | こいしキノコ | Гриб Камешек | Гриб Камінчик | **Cogumelo Pedrinha** | 판타지 |
| 900489 | 조약돌 대왕버섯 | King Pebble Mushroom | こいし大王キノコ | Гриб-великан Камешек | Велетень Камінчик | **Cogumelo-Rei Pedrinha** | 판타지 |
| 900490 | 꿀밤 버섯 | Chestnut Mushroom | どんぐりキノコ | Гриб Каштанчик | Гриб Каштанчик | **Cogumelo Castanha** | 판타지 |
| 900491 | 꿀밤 대왕버섯 | King Chestnut Mushroom | どんぐり大王キノコ | Гриб-великан Каштанчик | Велетень Каштанчик | **Cogumelo-Rei Castanha** | 판타지 |

### 2a 꼬마 (10) — 기존 언어 최장 16자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900452 | 불씨 꼬마 | Little Ember | ほのおちびっこ | Малыш Уголёк | Малюк Жаринка | **Pequenino Brasa** | 번역 |
| 900454 | 찌릿 꼬마 | Little Zappy | ビリビリちびっこ | Малыш Искорка | Малюк Іскринка | **Pequenino Faísca** | 번역 |
| 900456 | 이슬 꼬마 | Little Dew | しずくちびっこ | Малыш Росинка | Малюк Росинка | **Pequenino Orvalho** | 번역 · 범주 최장 초과(17>16) — 화면 확인 |
| 900458 | 동전 꼬마 | Little Coin | コインちびっこ | Малыш Монетка | Малюк Монетка | **Pequenino Moeda** | 번역 |
| 900460 | 별똥 꼬마 | Little Stardrop | ほしくずちびっこ | Малыш Звёздочка | Малюк Зорепадик | **Pequenino Estrela** | 번역 · 범주 최장 초과(17>16) — 화면 확인 |
| 900462 | 새싹 꼬마 | Little Sprout | めばえちびっこ | Малыш Росток | Малюк Паросток | **Pequenino Broto** | 번역 |
| 900464 | 달빛 꼬마 | Little Moonlight | つきあかりちびっこ | Малыш Лунник | Малюк Місячко | **Pequenino Luar** | 번역 |
| 900466 | 바람 꼬마 | Little Breeze | かぜちびっこ | Малыш Ветерок | Малюк Вітерець | **Pequenino Brisa** | 번역 |
| 900468 | 조약돌 꼬마 | Little Pebble | こいしちびっこ | Малыш Камешек | Малюк Камінчик | **Pequenino Pedrinha** | 번역 · 범주 최장 초과(18>16) — 화면 확인 |
| 900470 | 꿀밤 꼬마 | Little Chestnut | どんぐりちびっこ | Малыш Каштанчик | Малюк Каштанчик | **Pequenino Castanha** | 번역 · 범주 최장 초과(18>16) — 화면 확인 |

### 2b 꼬마 스킨 (10) — 기존 언어 최장 26자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900094 | 용암 꼬마 | Little Lava | 溶岩ちびっこ | Малыш-лава | Лавовий малюк | **Pequenino Lava** | 번역 |
| 900095 | 네온 꼬마 | Little Neon | ネオンちびっこ | Малыш-неон | Неоновий малюк | **Pequenino Neon** | 번역 |
| 900096 | 눈송이 꼬마 | Little Snowflake | 雪ちびっこ | Малыш-снежинка | Сніжинка-малюк | **Pequenino Neve** | 번역 |
| 900097 | 부자 꼬마 | Little Moneybags | お金持ちちびっこ | Малыш-богач | Багатенький малюк | **Pequenino Ricaço** | 번역 |
| 900098 | 은하 꼬마 | Little Galaxy | 銀河ちびっこ | Малыш-галактика | Галактичний малюк | **Pequenino Galáxia** | 번역 |
| 900099 | 벚꽃 꼬마 | Little Cherry Blossom | 桜ちびっこ | Малыш-сакура | Сакура-малюк | **Pequenino Cerejeira** | 번역 |
| 900100 | 햇님 꼬마 | Little Sunny | おひさまちびっこ | Малыш-солнышко | Сонячний малюк | **Pequenino Solzinho** | 번역 |
| 900101 | 구름 꼬마 | Little Cloud | 雲ちびっこ | Малыш-облачко | Хмаринка-малюк | **Pequenino Nuvem** | 번역 |
| 900102 | 보석 꼬마 | Little Gem | 宝石ちびっこ | Малыш-самоцвет | Самоцвітний малюк | **Pequenino Joia** | 번역 |
| 900103 | 딸기초코 꼬마 | Little Strawberry Choco | いちごチョコちびっこ | Малыш-шоколадка | Полунично-шоколадний малюк | **Pequenino Choco-Morango** | 번역 |

### 2d 세계수 단계 (8) — 기존 언어 최장 21자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900933 | 새싹 | Sprout | 新芽 | Росток | Паросток | **Broto** | 번역 |
| 900934 | 묘목 | Sapling | 若木 | Саженец | Саджанець | **Muda** | 번역 |
| 900935 | 어린 나무 | Young Tree | 若い木 | Молодое дерево | Молоде дерево | **Árvore Jovem** | 번역 |
| 900936 | 버섯 나무 | Mushroom Tree | キノコの木 | Грибное дерево | Грибне дерево | **Árvore-Cogumelo** | 번역 |
| 900937 | 이끼 고목 | Mossy Elder | 苔むす古木 | Замшелое древо | Мшистий старець | **Ancião Musgoso** | 번역 |
| 900938 | 룬 신목 | Sacred Rune Tree | ルーンの神木 | Рунное древо | Рунне дерево | **Árvore Rúnica** | 번역 |
| 900939 | 고대수 | Ancient Tree | 古代樹 | Древнее древо | Прадавнє дерево | **Árvore Ancestral** | 번역 |
| 900940 | 아르카나 세계수 | Arcana World Tree | アルカナ世界樹 | Мировое древо Аркана | Світове дерево Аркани | **Árvore do Mundo Arcana** | 번역 · 범주 최장 초과(22>21) — 화면 확인 |

### 2e 나무버섯·구역 (8) — 기존 언어 최장 25자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900949 | 선반 나무버섯 | Shelf Tree Mushroom | 棚ツリーキノコ | Полочный древесный гриб | Полицевий деревний гриб | **Cogumelo Prateleira** | 번역 |
| 900950 | 다발 나무버섯 | Cluster Tree Mushroom | 房ツリーキノコ | Пучковый древесный гриб | Гронистий деревний гриб | **Cogumelo em Cacho** | 번역 |
| 900951 | 발광 나무버섯 | Glowing Tree Mushroom | 光るツリーキノコ | Светящийся древесный гриб | Сяйний деревний гриб | **Cogumelo Luminoso** | 번역 |
| 900952 | 황금 나무버섯 | Golden Tree Mushroom | 黄金ツリーキノコ | Золотой древесный гриб | Золотий деревний гриб | **Cogumelo Áureo** | 번역 |
| 900953 | 뿌리 | Roots | 根っこ | Корни | Коріння | **Raízes** | 번역 |
| 900955 | 줄기 | Trunk | 幹 | Ствол | Стовбур | **Tronco** | 번역 |
| 900957 | 가지 | Branches | 枝 | Ветви | Гілки | **Galhos** | 번역 |
| 900959 | 수관 | Canopy | 樹冠 | Крона | Крона | **Copa** | 번역 |

### 3a 수확기 (16) — 기존 언어 최장 29자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 100001 | 오래된 낫 | Old Sickle | 古びた鎌 | Старый серп | Старий серп | **Foice Velha** | 번역 |
| 200001 | 기본형 | Basic | 基本型 | Базовый | Базовий | **Básico** | 번역 |
| 100002 | 절삭 수리검 | Cutting Shuriken | 切断手裏剣 | Режущий сюрикэн | Різальний сюрікен | **Shuriken Cortante** | 번역 |
| 200002 | 속도형 | Speed | スピード型 | Скоростной | Швидкість | **Veloz** | 번역 |
| 100003 | 약초 괭이 | Herb Hoe | 薬草のくわ | Мотыга травника | Мотика трав'яра | **Enxada de Ervas** | 번역 |
| 200003 | 치명타형 | Critical | クリティカル型 | Критический | Критичний | **Crítico** | 번역 |
| 100004 | 회전칼날 수확기 | Spinning Blade Harvester | 回転ブレード収穫機 | Сборщик с вращающимся лезвием | Збирач з обертовим лезом | **Coletor-Lâmina** | 번역 |
| 200004 | 범위형 | Area | 範囲型 | Площадной | Область | **Área** | 번역 |
| 100005 | 분열 수확기 | Splitting Harvester | 分裂収穫機 | Делящийся сборщик | Збирач-розділювач | **Coletor Divisor** | 번역 |
| 200005 | 분열형 | Splitting | 分裂型 | Делящийся | Розділення | **Divisor** | 번역 |
| 100006 | 방울 수확기 | Bell Harvester | 鈴の収穫機 | Сборщик-колокольчик | Дзвінковий збирач | **Coletor-Sino** | 번역 |
| 200006 | 울림형 | Resonance | 響き型 | Звенящий | Резонанс | **Ressonância** | 번역 |
| 100007 | 엽전 수확기 | Coin Harvester | 銭の収穫機 | Сборщик-монета | Монетний збирач | **Coletor-Moeda** | 번역 |
| 200007 | 재물형 | Wealth | 財宝型 | Денежный | Багатство | **Riqueza** | 번역 |
| 100008 | 황금 수확기 | Golden Harvester | 黄金の収穫機 | Золотой сборщик | Золотий збирач | **Coletor Dourado** | 번역 |
| 200008 | 보상 | Reward | 報酬型 | Наградной | Нагорода | **Recompensa** | 번역 |

### 3b 수확기 스킨 (7) — 기존 언어 최장 21자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900104 | 벚꽃 낫 | Cherry Blossom Sickle | 桜の鎌 | Серп сакуры | Серп сакури | **Foice de Cerejeira** | 번역 |
| 900105 | 흑요석 수리검 | Obsidian Shuriken | 黒曜石の手裏剣 | Обсидиановый сюрикэн | Обсидіановий сюрікен | **Shuriken de Obsidiana** | 번역 |
| 900106 | 얼음 칼날 | Ice Blade | 氷の刃 | Ледяное лезвие | Крижаний клинок | **Lâmina de Gelo** | 번역 |
| 900107 | 은하 포자 | Galaxy Spore | 銀河の胞子 | Галактическая спора | Галактична спора | **Esporo Galáctico** | 번역 |
| 900108 | 황금 방울 | Golden Bell | 黄金の鈴 | Золотой колокольчик | Золотий дзвоник | **Sino Dourado** | 번역 |
| 900109 | 옥 엽전 | Jade Coin | 翡翠の銭 | Нефритовая монета | Нефритова монета | **Moeda de Jade** | 번역 |
| 900110 | 용의 낫 | Dragon Sickle | 竜の鎌 | Серп дракона | Серп дракона | **Foice do Dragão** | 번역 |

### 4a 보스 (5) — 기존 언어 최장 20자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 209001 | 대왕송이 | King Matsutake | 大王松茸 | Король мацутакэ | Король мацутаке | **Matsutake-Rei** | 번역 |
| 209002 | 숲 속의 청소부 | Forest Cleaner | 森の掃除屋 | Лесной уборщик | Лісовий прибиральник | **Faxineiro da Floresta** | 번역 · 범주 최장 초과(21>20) — 화면 확인 |
| 209003 | 죽음의 천사 | Angel of Death | 死の天使 | Ангел смерти | Ангел смерті | **Anjo da Morte** | 번역 |
| 209004 | 환상 버섯 | Phantom Mushroom | まぼろしキノコ | Гриб-призрак | Примарний гриб | **Cogumelo Fantasma** | 번역 |
| 209005 | 움직이는 버섯 | Wandering Mushroom | 動くキノコ | Странствующий гриб | Мандрівний гриб | **Cogumelo Andarilho** | 번역 |

### 4b 날씨 (6) — 기존 언어 최장 18자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 208001 | 맑음 | Clear | 晴れ | Ясно | Ясно | **Céu Limpo** | 번역 |
| 208002 | 보슬비 | Drizzle | 小雨 | Морось | Мжичка | **Garoa** | 번역 |
| 208003 | 보름달 밤 | Full Moon Night | 満月の夜 | Ночь полнолуния | Ніч повного місяця | **Noite de Lua Cheia** | 번역 |
| 208004 | 폭우 | Downpour | 大雨 | Ливень | Злива | **Temporal** | 번역 |
| 208005 | 포자 바람 | Spore Wind | 胞子の風 | Споровый ветер | Споровий вітер | **Vento de Esporos** | 번역 |
| 208006 | 한파 | Cold Snap | 寒波 | Похолодание | Холоди | **Onda de Frio** | 번역 |

### 4c 지역 (5) — 기존 언어 최장 23자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900217 | 숲 | Forest | 森 | Лес | Ліс | **Floresta** | 번역 |
| 900224 | 달빛 밤 | Moonlit Night | 月夜 | Лунная ночь | Місячна ніч | **Noite de Luar** | 번역 |
| 900226 | 들판 | Meadow | 野原 | Луг | Луг | **Campina** | 번역 |
| 900232 | 바다 | Sea | 海 | Море | Море | **Mar** | 번역 |
| 900237 | 버섯 폐허 도시 | Ruined Mushroom City | キノコ廃墟の街 | Руины грибного города | Зруйноване грибне місто | **Cidade-Cogumelo em Ruínas** | 번역 · 범주 최장 초과(25>23) — 화면 확인 |

### 4d 숲 장치 (4) — 기존 언어 최장 23자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900242 | 그루터기 범퍼 | Stump Bumper | 切り株バンパー | Пень-отбойник | Пеньковий відбивач | **Toco Amortecedor** | 번역 |
| 302057 | 그루터기 | Stump | 切り株 | Пень | Пеньок | **Toco** | 번역 |
| 302058 | 이끼 길 | Moss Path | 苔の道 | Моховая тропа | Моховий шлях | **Trilha de Musgo** | 번역 |
| 302059 | 도토리 스위치 | Acorn Switch | どんぐりスイッチ | Желудёвый переключатель | Жолудевий перемикач | **Botão de Bolota** | 번역 |

### 4e 도감 세트 (10) — 기존 언어 최장 18자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900243 | 광대버섯 가문 | Amanita Family | テングタケ一族 | Род мухоморов | Родина мухоморів | **Família Amanita** | 번역 |
| 900245 | 선반 버섯 | Shelf Mushroom | サルノコシカケのなかま | Полочный гриб | Полицевий гриб | **Orelhas-de-Pau** | 번역 |
| 900251 | 동충하초 형제 | Cordyceps Brothers | 冬虫夏草きょうだい | Братья кордицепсы | Брати-кордицепси | **Irmãos Cordyceps** | 번역 |
| 900254 | 젤리 친구들 | Jelly Friends | ゼリーのなかまたち | Желейные друзья | Желейні друзі | **Amigos Gelatinosos** | 번역 |
| 900256 | 방구와 연기 | Puffs and Smoke | けむりとおなら | Пыхи и дымок | Пуфи й дим | **Puns e Fumaça** | 번역 |
| 900258 | 빛나는 숲 | Glowing Forest | 光る森 | Светящийся лес | Сяйний ліс | **Floresta Brilhante** | 번역 |
| 900260 | 산호와 뿔 | Coral and Antlers | サンゴとツノ | Коралл и рога | Корал і роги | **Corais e Chifres** | 번역 |
| 900263 | 버섯 요리사 | Mushroom Chef | キノコ料理人 | Грибной повар | Грибний кухар | **Chef de Cogumelos** | 번역 |
| 900265 | 약초꾼 | Herbalist | 薬草つみ | Травник | Травник | **Erveiro** | 번역 |
| 900267 | 독 연구가 | Poison Researcher | 毒の研究家 | Исследователь ядов | Дослідник отрут | **Toxicologista** | 번역 |

### 4f 불꽃·등급·분류 (25) — 기존 언어 최장 21자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900269 | 노란 불꽃 | Yellow Flame | 黄色い炎 | Жёлтое пламя | Жовте полум'я | **Chama Amarela** | 번역 |
| 900270 | 푸른 불꽃 | Blue Flame | 青い炎 | Синее пламя | Синє полум'я | **Chama Azul** | 번역 |
| 900271 | 보라 불꽃 | Purple Flame | 紫の炎 | Фиолетовое пламя | Фіолетове полум'я | **Chama Roxa** | 번역 |
| 900272 | 초록 불꽃 | Green Flame | 緑の炎 | Зелёное пламя | Зелене полум'я | **Chama Verde** | 번역 |
| 900273 | 하얀 불꽃 | White Flame | 白い炎 | Белое пламя | Біле полум'я | **Chama Branca** | 번역 |
| 900277 | 일반 | Common | ノーマル | Обычный | Звичайний | **Comum** | 번역 |
| 900278 | 에픽 | Epic | エピック | Эпический | Епічний | **Épico** | 번역 |
| 900279 | 유니크 | Unique | ユニーク | Уникальный | Унікальний | **Único** | 번역 |
| 900280 | 레전드리 | Legendary | レジェンダリー | Легендарный | Легендарний | **Lendário** | 번역 |
| 900983 | 고급 | Uncommon | 上質 | Необычное | Добротний | **Incomum** | 번역 |
| 900984 | 희귀 | Rare | レア | Редкое | Рідкісний | **Raro** | 번역 |
| 900274 | 식용 | Edible | 食用 | Съедобный | Їстівний | **Comestível** | 번역 |
| 900275 | 약용 | Medicinal | 薬用 | Лекарственный | Лікарський | **Medicinal** | 번역 |
| 900276 | 독 | Poison | 毒 | Ядовитый | Отруйний | **Veneno** | 번역 |
| 900091 | 식용 버섯 | Edible Mushroom | 食用キノコ | Съедобный гриб | Їстівний гриб | **Cogumelo Comestível** | 번역 |
| 900092 | 약용 버섯 | Medicinal Mushroom | 薬用キノコ | Лекарственный гриб | Лікарський гриб | **Cogumelo Medicinal** | 번역 |
| 900093 | 독버섯 | Poisonous Mushroom | 毒キノコ | Ядовитый гриб | Отруйний гриб | **Venenoso** | 번역 |
| 900868 | 독 버섯 | Poisonous Mushrooms | 毒キノコ | Ядовитые грибы | Отруйні гриби | **Venenosos** | 번역 |
| 900869 | 특수 버섯 | Special Mushrooms | 特別キノコ | Особые грибы | Особливі гриби | **Especiais** | 번역 |
| 900862 | 식용 균사 | Edible Mycelium | 食用菌糸 | Съедобный мицелий | Їстівний міцелій | **Micélio Comestível** | 번역 |
| 900863 | 약용 균사 | Medicinal Mycelium | 薬用菌糸 | Лекарственный мицелий | Лікарський міцелій | **Micélio Medicinal** | 번역 |
| 900864 | 독 균사 | Poison Mycelium | 毒菌糸 | Ядовитый мицелий | Отруйний міцелій | **Micélio Venenoso** | 번역 |
| 900111 | 초반 | Early | 序盤 | Начало | Початок | **Início** | 번역 |
| 900112 | 중반 | Mid | 中盤 | Середина | Середина | **Meio** | 번역 |
| 900113 | 후반 | Late | 終盤 | Поздняя игра | Кінець | **Fim** | 번역 |

### 5a 노드 (87) — 기존 언어 최장 29자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 303002 | 타격 점수 I | Hit Score I | ヒットスコア I | Очки за удар I | Бали за удар I | **Pontos de Acerto I** |  |
| 303006 | 군락지 수 | Colony Count | 群落数 | Число колоний | Кількість колоній | **Nº de Colônias** |  |
| 303007 | 군락 재생 속도 | Colony Regrowth Speed | 群落再生速度 | Возрождение колоний | Швидкість відновлення колоній | **Regeneração de Colônias** |  |
| 303003 | 정산 보너스 | Payout Bonus | 精算ボーナス | Бонус итогов | Бонус виплати | **Bônus de Lucro** |  |
| 303012 | 수확량 I | Yield I | 収穫量 I | Урожай I | Врожай I | **Rendimento I** |  |
| 303016 | 타격 점수 I+ | Hit Score I+ | ヒットスコア I+ | Очки за удар I+ | Бали за удар I+ | **Pontos de Acerto I+** |  |
| 303017 | 수확량 I+ | Yield I+ | 収穫量 I+ | Урожай I+ | Врожай I+ | **Rendimento I+** |  |
| 303018 | 타격 점수 II | Hit Score II | ヒットスコア II | Очки за удар II | Бали за удар II | **Pontos de Acerto II** |  |
| 303019 | 수확량 II | Yield II | 収穫量 II | Урожай II | Врожай II | **Rendimento II** |  |
| 303020 | 타격 점수 II+ | Hit Score II+ | ヒットスコア II+ | Очки за удар II+ | Бали за удар II+ | **Pontos de Acerto II+** |  |
| 303021 | 수확량 II+ | Yield II+ | 収穫量 II+ | Урожай II+ | Врожай II+ | **Rendimento II+** |  |
| 303022 | 군락지 수 II | Colony Count II | 群落数 II | Число колоний II | Кількість колоній II | **Nº de Colônias II** |  |
| 303023 | 정산 보너스 II | Payout Bonus II | 精算ボーナス II | Бонус итогов II | Бонус виплати II | **Bônus de Lucro II** |  |
| 303008 | 변종 출현율 | Rare Spawn Rate | 変種出現率 | Шанс редких грибов | Частота різновидів | **Taxa de Variantes** |  |
| 303014 | 군락지 버섯 수 | Mushrooms per Colony | 群落のキノコ数 | Грибов в колонии | Грибів у колонії | **Cogumelos por Colônia** |  |
| 303024 | 타격 점수 III | Hit Score III | ヒットスコア III | Очки за удар III | Бали за удар III | **Pontos de Acerto III** |  |
| 303025 | 수확량 III | Yield III | 収穫量 III | Урожай III | Врожай III | **Rendimento III** |  |
| 303026 | 타격 점수 III+ | Hit Score III+ | ヒットスコア III+ | Очки за удар III+ | Бали за удар III+ | **Pontos de Acerto III+** |  |
| 303027 | 수확량 III+ | Yield III+ | 収穫量 III+ | Урожай III+ | Врожай III+ | **Rendimento III+** |  |
| 303005 | 단골 손님 | Regular Customer | 常連さん | Постоянный клиент | Постійний покупець | **Freguês Fiel** |  |
| 303009 | 황금 변종 | Golden Variant | 黄金変種 | Золотой вариант | Золотий різновид | **Variante Dourada** |  |
| 303010 | 거대 버섯 | Giant Mushroom | 巨大キノコ | Гигантский гриб | Гігантський гриб | **Cogumelo Gigante** |  |
| 303013 | 배수 획득 | Triple Harvest | 3倍収穫 | Множитель урожая | Множник збору | **Colheita Tripla** |  |
| 302002 | 필드 넓히기 | Widen Field | フィールド拡張 | Расширить поле | Розширення поля | **Ampliar Campo** |  |
| 302003 | 바 넓이 | Bar Width | バー幅 | Ширина планки | Ширина планки | **Largura da Barra** |  |
| 302004 | 콤보 계수 | Combo Factor | コンボ係数 | Коэффициент комбо | Коефіцієнт комбо | **Fator de Combo** |  |
| 302007 | 꼬마 수확기 | Little Harvester | ちびっこ収穫機 | Маленький сборщик | Малий збирач | **Coletor Mirim** |  |
| 302053 | 콤보 계수 II | Combo Factor II | コンボ係数 II | Коэффициент комбо II | Коефіцієнт комбо II | **Fator de Combo II** |  |
| 302008 | 임시 수확기 수명 | Temp Harvester Lifespan | 一時収穫機の寿命 | Жизнь врем. сборщиков | Життя тимчасових збирачів | **Vida do Coletor Temp.** |  |
| 302005 | 꼬마 건축가 | Little Builder | ちびっこ大工 | Малыш-строитель | Малий будівельник | **Construtor Mirim** |  |
| 302006 | 숲 장치 강화 | Forest Gadget Boost | 森のしかけ強化 | Усиление лесных устройств | Посилення лісових пристроїв | **Engenhocas+** |  |
| 302009 | 가속 | Speed Up | 加速 | Ускорение | Прискорення | **Acelerar** | 스킬 |
| 302012 | 날 갈기 | Sharpen | 刃研ぎ | Заточка | Гостріння | **Afiar** | 스킬 |
| 302015 | 분열 | Split | 分裂 | Раздвоение | Розділення | **Divisão** | 스킬 |
| 302021 | 맑은 포자막 | Clear Spore Veil | 澄んだ胞子膜 | Очищающая завеса | Чиста спорова завіса | **Véu de Esporos Puro** | 스킬 |
| 302024 | 포자 폭발 | Spore Burst | 胞子爆発 | Споровый взрыв | Споровий вибух | **Explosão de Esporos** | 스킬 |
| 302030 | 분신 수확기 | Clone Harvester | 分身収穫機 | Сборщик-двойник | Збирач-двійник | **Coletor Clone** | 스킬 |
| 302033 | 버섯 자석 | Mushroom Magnet | キノコ磁石 | Грибной магнит | Грибний магніт | **Ímã de Cogumelos** | 스킬 |
| 302036 | 연쇄 번개 | Chain Lightning | 連鎖雷 | Цепная молния | Ланцюгова блискавка | **Relâmpago em Cadeia** | 스킬 |
| 302039 | 회오리 | Tornado | 竜巻 | Торнадо | Смерч | **Tornado** | 스킬 |
| 302042 | 충격파 | Shockwave | 衝撃波 | Ударная волна | Ударна хвиля | **Onda de Choque** | 스킬 |
| 302047 | 관통 | Pierce | 貫通 | Пронзание | Пробиття | **Perfuração** | 스킬 |
| 302060 | 유도 | Homing | 誘導 | Наведение | Наведення | **Teleguiado** | 스킬 |
| 302061 | 기운 북돋기 | Power Up | 気力アップ | Прилив сил | Підбадьорення | **Dar Ânimo** | 스킬 |
| 302062 | 분신 바 | Clone Bar | 分身バー | Двойник-планка | Планка-двійник | **Barra Clone** | 스킬 |
| 302050 | 풍년 | Bountiful Harvest | 豊作 | Урожайный год | Щедрий урожай | **Colheita Farta** | 스킬 |
| 302052 | 버프 지속 | Buff Duration | バフ持続 | Длительность баффов | Тривалість бафів | **Duração dos Buffs** |  |
| 301002 | 공격력 I | Attack I | 攻撃力 I | Атака I | Атака I | **Ataque I** |  |
| 301003 | 수확기 크기 | Harvester Size | 収穫機のサイズ | Размер сборщика | Розмір збирача | **Tamanho do Coletor** |  |
| 301004 | 공속 | Harvester Speed | スピード | Скорость сборщика | Швидкість атаки | **Vel. do Coletor** |  |
| 301006 | 지속시간 | Duration | 持続時間 | Длительность | Тривалість | **Duração** |  |
| 301010 | 수확기 +1 | Harvester +1 | 収穫機 +1 | Сборщик +1 | Збирач +1 | **Coletor +1** |  |
| 301015 | 공격력 II | Attack II | 攻撃力 II | Атака II | Атака II | **Ataque II** |  |
| 301016 | 수확기 +1 (II) | Harvester +1 (II) | 収穫機 +1 (II) | Сборщик +1 (II) | Збирач +1 (II) | **Coletor +1 (II)** |  |
| 301017 | 공격력 II+ | Attack II+ | 攻撃力 II+ | Атака II+ | Атака II+ | **Ataque II+** |  |
| 301018 | 지속시간 II | Duration II | 持続時間 II | Длительность II | Тривалість II | **Duração II** |  |
| 301008 | 치명타 | Critical Hit | クリティカル | Критический удар | Критичний удар | **Golpe Crítico** |  |
| 301005 | 포자 저항 | Spore Resistance | 胞子耐性 | Стойкость к спорам | Стійкість до спор | **Resistência a Esporos** |  |
| 301007 | 선제 공격 | First Strike | 先制攻撃 | Первый удар | Перший удар | **Primeiro Golpe** |  |
| 301011 | 회전 칼날 | Spinning Blade | 回転ブレード | Вращающееся лезвие | Обертове лезо | **Lâmina Giratória** |  |
| 301025 | 바 넓이 II | Bar Width II | バー幅 II | Ширина планки II | Ширина планки II | **Largura da Barra II** |  |
| 301026 | 바 반사 속도 | Bar Rebound Speed | バー反射速度 | Скорость отскока от планки | Швидкість відбиття від планки | **Rebote da Barra** |  |
| 301027 | 바 스킬 지속 | Bar Skill Duration | バースキル持続 | Длительность навыков планки | Тривалість навичок планки | **Duração Hab. da Barra** |  |
| 301028 | 바 스킬 발동률 | Bar Skill Trigger Chance | バースキル発動率 | Шанс навыков планки | Шанс навичок планки | **Chance Hab. da Barra** |  |
| 301029 | 북돋기 강화 | Power Up Boost | 気力アップ強化 | Усиление «Прилива сил» | Посилення підбадьорення | **Reforço do Ânimo** |  |
| 301019 | 공격력 III | Attack III | 攻撃力 III | Атака III | Атака III | **Ataque III** |  |
| 301020 | 공격력 III+ | Attack III+ | 攻撃力 III+ | Атака III+ | Атака III+ | **Ataque III+** |  |
| 301021 | 공격력 IV | Attack IV | 攻撃力 IV | Атака IV | Атака IV | **Ataque IV** |  |
| 301022 | 수확기 +2 (III) | Harvester +2 (III) | 収穫機 +2 (III) | Сборщик +2 (III) | Збирач +2 (III) | **Coletor +2 (III)** |  |
| 301023 | 치명타 피해 | Critical Damage | クリティカルダメージ | Критический урон | Критична шкода | **Dano Crítico** |  |
| 301009 | 무더기 파쇄 | Cluster Crusher | 塊くだき | Крушитель куч | Розтрощувач купок | **Esmaga-Montes** |  |
| 303028 | 균사 공명 | Mycelium Resonance | 菌糸の共鳴 | Резонанс мицелия | Резонанс міцелію | **Ressonância do Micélio** |  |
| 303029 | 균사 감응 | Mycelium Attunement | 菌糸の感応 | Отклик мицелия | Налаштування на міцелій | **Sintonia do Micélio** |  |
| 303030 | 풍요의 손길 | Touch of Plenty | 実りの手 | Рука изобилия | Дотик достатку | **Toque da Fartura** |  |
| 302054 | 자동 수확 확장 | Auto Harvest Expansion | 自動収穫の拡張 | Расширение автосбора | Розширення автозбору | **Colheita Automática+** |  |
| 302055 | 대지의 축복 | Blessing of the Earth | 大地の祝福 | Благословение земли | Благословення землі | **Bênção da Terra** |  |
| 302056 | 균사석 낙하 | Mycelium Stone Fall | 菌糸石落下 | Падение камней мицелия | Падіння міцелієвих каменів | **Chuva de Pedras** |  |
| 301024 | 독성 내성 | Toxin Resistance | 毒への耐性 | Стойкость к токсинам | Стійкість до токсинів | **Resistência a Toxinas** |  |
| 301030 | 균사 투지 | Mycelium Grit | 菌糸の闘志 | Боевой дух мицелия | Міцелієвий бойовий дух | **Garra do Micélio** |  |
| 301031 | 보스 파쇄 | Boss Crusher | ボスくだき | Крушитель боссов | Розтрощувач босів | **Esmaga-Chefes** |  |
| 303031 | 균사 정산 | Mycelium Payout | 菌糸の精算 | Итоги мицелия | Міцелієва виплата | **Lucro de Micélio** |  |
| 302063 | 보스의 전리품 | Boss Spoils | ボスの戦利品 | Трофеи босса | Трофеї боса | **Espólio do Chefe** |  |
| 302064 | 균사 술법 | Mycelium Arts | 菌糸の術 | Искусство мицелия | Мистецтво міцелію | **Artes do Micélio** |  |
| 302065 | 균사 바 | Mycelium Bar | 菌糸バー | Планка мицелия | Міцелієва планка | **Barra de Micélio** |  |
| 301032 | 끝없는 투지 | Endless Grit | 果てなき闘志 | Неукротимый дух | Незламний дух | **Garra Infinita** | 무한 |
| 303032 | 끝없는 풍년 | Endless Bounty | 果てなき豊作 | Неиссякаемый урожай | Нескінченний урожай | **Fartura Infinita** | 무한 |
| 303033 | 희귀 포자 | Rare Spores | 希少な胞子 | Редкие споры | Рідкісні спори | **Esporos Raros** |  |

### 5b 능력치 (12) — 기존 언어 최장 23자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900439 | 공격력 | Attack | 攻撃力 | Атака | Атака | **Ataque** |  |
| 900440 | 이동 속도 | Move Speed | 移動速度 | Скорость | Швидкість руху | **Velocidade** |  |
| 900441 | 치명타 확률 | Crit Chance | クリティカル率 | Шанс крита | Шанс криту | **Chance de Crítico** |  |
| 900442 | 스킬 피해 | Skill Damage | スキルダメージ | Урон навыков | Шкода навичок | **Dano de Habilidade** |  |
| 900443 | 스킬 발동률 | Skill Trigger Chance | スキル発動率 | Шанс навыков | Шанс навичок | **Ativação de Habilidade** |  |
| 900444 | 제한시간 | Time Limit | 制限時間 | Время | Ліміт часу | **Tempo Limite** |  |
| 900446 | 바 길이 | Bar Length | バーの長さ | Длина планки | Довжина планки | **Tamanho da Barra** |  |
| 900447 | 타격 점수 | Hit Score | ヒットスコア | Очки за удар | Бали за удар | **Pontos de Acerto** |  |
| 900448 | 수확량 | Yield | 収穫量 | Урожай | Врожай | **Rendimento** |  |
| 900449 | 정산 골드 | Payout Gold | 精算ゴールド | Итоговое золото | Золото виплати | **Lucro** |  |
| 900450 | 에픽 이상 출현 | Epic+ Spawn | エピック以上の出現 | Эпик и выше | Епічні+ частіше | **Chance de Épico+** |  |
| 900451 | 황금 변종 확률 | Golden Variant Chance | 黄金変種の確率 | Шанс золотого | Шанс золотого різновиду | **Chance de Dourado** |  |

### 6a 장식 (24) — 기존 언어 최장 27자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900985 | 잎사귀 등불 | Leaf Lantern | 葉っぱのランタン | Фонарик из листьев | Листяний ліхтарик | **Lanterna de Folhas** |  |
| 900986 | 꽃 화관 | Flower Wreath | 花のかんむり | Цветочный венок | Квітковий вінок | **Coroa de Flores** |  |
| 900987 | 나무 새집 | Wooden Birdhouse | 木の巣箱 | Деревянный скворечник | Дерев'яна шпаківня | **Casinha de Passarinho** |  |
| 900988 | 물방울 풍경 | Dewdrop Wind Chime | しずくの風鈴 | Росяные колокольчики | Росяний передзвін | **Sininho de Orvalho** |  |
| 900989 | 이끼 샘물 | Mossy Spring | 苔の泉 | Моховой родник | Мшисте джерельце | **Fonte Musgosa** |  |
| 900990 | 수련 연못 | Lily Pond | スイレンの池 | Пруд с кувшинками | Ставок з лататтям | **Lago de Lírios** |  |
| 900991 | 깃털 드림캐처 | Feather Dreamcatcher | 羽根のドリームキャッチャー | Ловец снов с перьями | Пір'їновий ловець снів | **Filtro dos Sonhos** |  |
| 900992 | 바람개비 | Pinwheel | かざぐるま | Вертушка | Вітрячок | **Cata-Vento** |  |
| 900993 | 덩굴 그네 | Vine Swing | つるのブランコ | Качели из лозы | Ліанова гойдалка | **Balanço de Cipó** |  |
| 900994 | 돌 화로 | Stone Hearth | 石のたき火台 | Каменный очаг | Кам'яне вогнище | **Fogareiro de Pedra** |  |
| 900995 | 불꽃 등롱 | Flame Lantern | 炎のランタン | Огненный фонарь | Вогняний ліхтар | **Lanterna de Chamas** |  |
| 900996 | 버섯 촛불 | Mushroom Candle | キノコのろうそく | Грибная свеча | Грибна свічка | **Vela de Cogumelo** |  |
| 900997 | 버섯 돌탁자 | Mushroom Stone Table | キノコの石テーブル | Грибной каменный стол | Грибний кам'яний столик | **Mesa de Pedra Cogumelo** |  |
| 900998 | 이끼 바위 | Mossy Rock | 苔むす岩 | Замшелый валун | Мшистий валун | **Rocha Musgosa** |  |
| 900999 | 자수정 정동 | Amethyst Geode | アメジストのジオード | Аметистовая жеода | Аметистова жеода | **Geodo de Ametista** |  |
| 901000 | 피뢰침 오브 | Lightning Rod Orb | 避雷針オーブ | Шар-громоотвод | Куля-блискавковідвід | **Orbe Para-Raios** |  |
| 901001 | 황금 수정등 | Golden Crystal Lamp | 黄金のクリスタルランプ | Золотая хрустальная лампа | Золота кришталева лампа | **Luminária Dourada** |  |
| 901002 | 놋쇠 종 | Brass Bell | 真鍮のベル | Латунный колокол | Латунний дзвіночок | **Sino de Latão** |  |
| 901003 | 초승달 장식 | Crescent Moon Charm | 三日月の飾り | Подвеска-месяц | Оберіг-півмісяць | **Lua Crescente** |  |
| 901004 | 별빛 가랜드 | Starlight Garland | 星明かりのガーランド | Гирлянда из звёздного света | Зоряна гірлянда | **Guirlanda de Estrelas** |  |
| 901005 | 부엉이 목각 | Carved Wooden Owl | フクロウの木彫り | Резная деревянная сова | Різьблена сова | **Coruja Entalhada** |  |
| 901006 | 룬 비석 | Rune Monument | ルーンの石碑 | Рунная стела | Рунний монумент | **Monumento Rúnico** |  |
| 901007 | 꼬마 왕관 | Little One's Crown | ちびっこの王冠 | Корона малыша | Корона малюка | **Coroa de Pequenino** |  |
| 901008 | 빛의 구슬 | Orb of Light | 光の玉 | Шар света | Куля світла | **Orbe de Luz** |  |

### 6b 건물 (10) — 기존 언어 최장 27자

| Str_ID | KR | EN | JA | RU | UK | **PT** | 비고 |
|---|---|---|---|---|---|---|---|
| 900546 | 공방 | Workshop | 工房 | Мастерская | Майстерня | **Oficina** |  |
| 901110 | 꼬마 공방 | Little One Workshop | ちびっこ工房 | Мастерская малышей | Майстерня малюків | **Oficina dos Pequeninos** |  |
| 900085 | 버섯 오두막 | Mushroom Cottage | キノコの小屋 | Грибной домик | Грибна хатка | **Chalé de Cogumelo** | 옛 |
| 900078 | 버섯 화로 | Mushroom Brazier | キノコのたき火台 | Грибная жаровня | Грибна жаровня | **Braseiro de Cogumelo** | 옛 |
| 900071 | 버섯 탁자 | Mushroom Table | キノコのテーブル | Грибной стол | Грибний столик | **Mesa de Cogumelo** | 옛 |
| 900073 | 포자 등불 | Spore Lantern | 胞子ランタン | Фонарь из спор | Спорова лампа | **Lanterna de Esporos** | 옛 |
| 900075 | 꽃 화단 | Flower Bed | 花壇 | Клумба | Клумба | **Canteiro de Flores** | 옛 |
| 900081 | 꼬마 그네 | Kids' Swing | ちびっこのブランコ | Качели малышей | Гойдалка для малюків | **Balanço dos Pequeninos** | 옛 |
| 900083 | 버섯 분수 | Mushroom Fountain | キノコの噴水 | Грибной фонтан | Грибний фонтан | **Fonte de Cogumelo** | 옛 |
| 900088 | 균사석 조각상 | Mycelium Stone Statue | 菌糸石の彫像 | Статуя из камня мицелия | Статуя з міцелієвого каменя | **Estátua de Pedra de Micélio** | 옛 |

## 부록 B. 범주별 줄 수와 진행 표

| 범주 (`Str_Name`) | 줄 수 | EN이 빈 줄 (PT도 비움) | 부록 A 확정안 | 남은 번역 | 진행 |
|---|---|---|---|---|---|
| `ui` | 1342 | 48 | 225 | 1069 | ☐ |
| `upg:desc` | 90 | 0 | 0 | 90 | ☐ |
| `upg:name` | 90 | 0 | 90 | 0 | ☐ |
| `ball:name` | 8 | 0 | 8 | 0 | ☐ |
| `ball:type` | 8 | 0 | 8 | 0 | ☐ |
| `ball:look` | 8 | 0 | 0 | 8 | ☐ |
| `wea:name` | 6 | 0 | 6 | 0 | ☐ |
| `wea:desc` | 6 | 0 | 0 | 6 | ☐ |
| `boss:name` | 5 | 0 | 5 | 0 | ☐ |
| `boss:gimmick` | 5 | 0 | 0 | 5 | ☐ |
| **합계** | **1568** | **48** | **342** | **1178** | |

권장 순서: ① 부록 A 이름(확정안 그대로 넣기) → ② UI 공통어·버튼 → ③ 노드 설명 `upg:*:desc` → ④ 알림·팝업 → ⑤ 도감 설명 → ⑥ 꼬마 대사 → ⑦ 튜토리얼·긴 안내. 이름을 먼저 넣어야 설명문에서 인용하는 이름이 맞는다.

## 부록 C. 넘침 위험 줄 (기존 언어에서 길었던 칸)

| ID | KR | EN (글자) | RU (글자) | UK (글자) | PT 목표 (EN×1.15) |
|---|---|---|---|---|---|
| 900741 | 누르기·F·우클릭 배속 | Tap·F·RMB: Speed (16) | Нажми, F или правый клик: скорость (34) | Торкнись, F або правий клік для швидкості (41) | ≤ 18자 |
| 900740 | 눌러서 배속 | Tap: Speed (10) | Нажми для смены скорости (24) | Торкнись, щоб змінити швидкість (31) | ≤ 12자 |
| 900353 | 저령 | Zhuling (7) | Трутовик зонтичный (18) | Парасольковий трутовик (22) | ≤ 8자 |
| 901303 | Panels (비워도 됨) | Panels (optional) (17) | Panels (можно оставить пустым) (30) | Panels (можна залишити порожнім) (32) | ≤ 20자 |
| 900026 | 쑥쑥 자라는 중이야! | Growing like crazy! (19) | Расту как на дрожжах! (21) | Ростемо не по днях, а по годинах! (33) | ≤ 22자 |
| 300006 | 큰 도토리 방울 | A big acorn bell (16) | Большой желудёвый колокольчик (29) | Великий жолудевий дзвінок (25) | ≤ 18자 |
| 900634 | 모두 돌보기 | Tend All (8) | Позаботиться обо всех (21) | Доглянути всіх (14) | ≤ 9자 |
| 301026 | 바 반사 속도 | Bar Rebound Speed (17) | Скорость отскока от планки (26) | Швидкість відбиття від планки (29) | ≤ 20자 |
| 900855 | 라운드 끝내고 정산하기 | End Round & Cash Out (20) | Закончить раунд и забрать золото (32) | Завершити раунд і забрати золото (32) | ≤ 23자 |
| 901240 | {0} 합류 가능 | {0} can join (12) | {0} может присоединиться (24) | {0} може приєднатися (20) | ≤ 14자 |
| 901248 | <b>{0}</b>  합류 가능 | <b>{0}</b>  can join (13) | <b>{0}</b>  может присоединиться (25) | <b>{0}</b>  може приєднатися (21) | ≤ 15자 |
| 302059 | 도토리 스위치 | Acorn Switch (12) | Желудёвый переключатель (23) | Жолудевий перемикач (19) | ≤ 14자 |
| 900527 | 황금 {0}! | Golden {0}! (11) | Золотой вариант: {0}! (21) | Золотий різновид: {0}! (22) | ≤ 13자 |
| 900136 | 에픽 이상 가중치 {0} | Epic+ odds {0} (14) | Вес эпических и выше {0} (24) | Вага епічних і вище {0} (23) | ≤ 16자 |
| 900138 | 버섯마다 {0} | {0} per mushroom (16) | Шанс {0} для каждого гриба (26) | {0} з кожного гриба (19) | ≤ 18자 |
| 900221 | 구름버섯 | Turkey Tail (11) | Траметес разноцветный (21) | Індичий хвіст (13) | ≤ 13자 |
| 900891 | 진화하기 | Evolve (6) | Эволюционировать (16) | Еволюціонувати (14) | ≤ 7자 |
| 901004 | 별빛 가랜드 | Starlight Garland (17) | Гирлянда из звёздного света (27) | Зоряна гірлянда (15) | ≤ 20자 |
| 901236 | 라운드 영구 적용 | Permanent in rounds (19) | Действует в раундах постоянно (29) | Постійне в раундах (18) | ≤ 22자 |
| 901331 | 에픽 이상 가중치 ×{0} | Epic+ odds ×{0} (15) | Вес эпических и выше ×{0} (25) | Вага епічних і вище ×{0} (24) | ≤ 17자 |
| 301027 | 바 스킬 지속 | Bar Skill Duration (18) | Длительность навыков планки (27) | Тривалість навичок планки (25) | ≤ 21자 |
| 301029 | 북돋기 강화 | Power Up Boost (14) | Усиление «Прилива сил» (22) | Посилення підбадьорення (23) | ≤ 16자 |
| 301030 | 균사 투지 | Mycelium Grit (13) | Боевой дух мицелия (18) | Міцелієвий бойовий дух (22) | ≤ 15자 |
| 900081 | 꼬마 그네 | Kids' Swing (11) | Качели малышей (14) | Гойдалка для малюків (20) | ≤ 13자 |
| 900281 | 조정안 | Adjusted Plan (13) | Скорректированный план (22) | Скоригований план (17) | ≤ 15자 |
| 900413 | 마귀곰보버섯 | False Morel (11) | Строчок обыкновенный (20) | Диявольський строчок (20) | ≤ 13자 |
| 900534 | 분신이 {0}마리 남았어요! | Clones left: {0}! (17) | Осталось двойников: {0}! (24) | Залишилось двійників: {0}! (26) | ≤ 20자 |
| 900662 | 세계수 경험치 +{0} | World Tree EXP +{0} (19) | Опыт Мирового древа +{0} (24) | Досвід Світового дерева +{0} (28) | ≤ 22자 |
| 900723 | 세계수 나무 경험치 | World Tree EXP (14) | Опыт Мирового древа (19) | Досвід Світового дерева (23) | ≤ 16자 |
| 901189 | 세계수 경험치 | World Tree EXP (14) | Опыт Мирового древа (19) | Досвід Світового дерева (23) | ≤ 16자 |
| 901220 | 지금 끝내기 | End Now (7) | Завершить сейчас (16) | Завершити зараз (15) | ≤ 8자 |
| 901329 | 음소거: 켜짐 | Mute: On (8) | Без звука: вкл. (15) | Без звуку: увімк. (17) | ≤ 9자 |
| 302006 | 숲 장치 강화 | Forest Gadget Boost (19) | Усиление лесных устройств (25) | Посилення лісових пристроїв (27) | ≤ 22자 |
| 900108 | 황금 방울 | Golden Bell (11) | Золотой колокольчик (19) | Золотий дзвоник (15) | ≤ 13자 |
| 900109 | 옥 엽전 | Jade Coin (9) | Нефритовая монета (17) | Нефритова монета (16) | ≤ 10자 |
| 900113 | 후반 | Late (4) | Поздняя игра (12) | Кінець (6) | ≤ 5자 |
| 900239 | 갈황색미치광이버섯 | Laughing Gym (12) | Гимнопил (8) | Сміхотливий гімнопіл (20) | ≤ 14자 |
| 900282 | 명세 원본 | Original Spec (13) | Исходная спецификация (21) | Початковий задум (16) | ≤ 15자 |
| 900358 | 노루궁뎅이 | Lion's Mane (11) | Ежовик гребенчатый (18) | Їжовик гребінчастий (19) | ≤ 13자 |
| 900640 | 레벨 업  <size=80%>{0}{1}</size> | Level Up  <size=80%>{0}{1}</size> (16) | Повысить уровень  <size=80%>{0}{1}</size> (24) | Підвищити рівень  <size=80%>{0}{1}</size> (24) | ≤ 18자 |
