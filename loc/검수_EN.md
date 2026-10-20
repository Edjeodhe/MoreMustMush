# MoreMustMush 영어(EN) 로컬라이제이션 QA 리뷰

대상: `scratchpad/loc/EN.tsv` (1,549행), 원문 KR 대비 전수 검토. 일부 문자열은 `Assets/Scripts`에서 실제 사용처(placeholder 내용, 이어붙이기)를 확인함.

## 1. 요약

- **종합 등급: B−**
  - 문장 자체는 대체로 자연스럽고, 꼬마 대사의 귀여운 톤도 잘 살렸음. 해요체 → 친근한 영어로 무난하게 옮김.
  - 그러나 **같은 게임 용어가 여러 번역으로 갈라진 곳이 많음** (꼬마 이름 Kid/Little One, 정산 Settlement/Payout, 발동률 proc/trigger, 의뢰/부탁 섞임, 나이테 Tree Rings/Growth Ring, 버섯 오두막 Cottage/Cabin 등). 900148~900200, 900452~ 구간처럼 **나중에 번역한 묶음은 표기 스타일이 다름** (`x` 대 `×`, 일반명사 대문자, `proc rate`).
  - **의미 오류가 몇 개 있음**: 라운드 시간 설명의 수치가 원문과 다름(201006), 문장 이어붙이기가 깨짐(900153+900154), 구역 "정원"을 Garden으로 옮김(901080), 의뢰를 Favors로 옮김(901157).
  - **버섯 영어명**: 약 10종이 실제 종과 다른 이름이거나 다른 종 이름임 (선녀낙엽버섯, 간버섯, 잔나비걸상버섯, 민자주방망이버섯, 노란각시버섯 등). 싸리버섯과 산호버섯은 둘 다 "Coral Mushroom"으로 **이름이 겹침**.
- **심각도별 건수** (용어 표의 항목은 각 1건으로 셈)

| 치명 | 높음 | 중간 | 낮음 | 합계 |
|---|---|---|---|---|
| 3 | 17 | 41 | 33 | 94 |

---

## 2. 용어 일관성 표

| KR 용어 | 쓰인 번역들 | 해당 ID | 권장 |
|---|---|---|---|
| 꼬마 (일반명사) | Little One(s) / little ones (소문자) / Kid | Little One: 900068, 900569, 900587, 901076 등 · little ones: 900670, 900671, 900677~900685, 900850, 900878 · Kid: 900092~900103 | **Little One(s)**, 문장 중간에서도 대문자로 통일 |
| 꼬마 이름 (불씨 꼬마 등) | Ember **Kid** / Ember **Little One**; Pebble **Kid** / Pebble **Little One** | Kid: 900061, 900069, 900094~900103 · Little One: 900452~900470 | 한 형식으로 통일. 권장 **Little Ember, Little Zappy, Little Dew, Little Pebble …** (짧고 자연스러우며 Little One과 이어짐). 스킨 900094~900103도 Little Lava 등으로 |
| 정산 / 정산 골드 | Settlement / Payout / Cash Out | Settlement: 303003, 303023, 303031, 900132, 900135, 900184, 203023 · Payout: 900449, 900833, 203032 · Cash Out: 900855 | **Payout** (Payout Bonus, Payout Gold, Mycelium Payout). 버튼 900855는 "End Round & Collect"도 가능 |
| 발동률 | proc rate / trigger rate / trigger chance | proc: 900154, 900155, 900171, 900172, 900186, 900190, 900443, 900554~900557, 301028 · trigger: 201029, 202056, 900126, 900946, 900982 | **trigger chance** (캐주얼 유저용. "proc"는 하드코어 은어) |
| 의뢰 (일일/주간 퀘스트) | Quests / requests / Favors | Quests: 900675, 900717, 900718, 900720 · requests: 900203, 900641 · Favors(오역): 901157 | **Quest(s)**. 부탁(Favor)과 섞이면 안 됨 |
| 부탁 (꼬마의 부탁) | Favor / request | Favor: 900461, 900502, 900655, 900708, 901098 등 · request: 900065, 900077 | **Favor** |
| 도감 | Collection / Mushroom Guide | Collection: 900545, 900727, 900742, 900807, 900827, 901269 등 · Mushroom Guide: 203009, 900657, 900866, 900913 | **Collection** (제목은 "Mushroom Collection") |
| 나이테 | Tree Rings / Growth Ring | Tree Rings: 901052, 901057, 901062, 901223, 901224, 901238 · Growth Ring: 901142, 901155, 901156 | **Tree Rings** |
| 세계수 단계 | Tier / stage / Stage | Tier: 900635, 901072, 901074 등 · stage: 901046 · Stage: 901051, 901061 | **Tier** (게임의 Stage와 겹치지 않게) |
| 버섯 오두막 | Mushroom Cottage / Mushroom Cabin | 900085 / 900580 | **Mushroom Cottage** |
| 자동 수확 | Auto Harvest / Auto-Harvest / Auto-harvest | 900064, 900067, 900087, 900576, 900804 / 302054, 900178 / 900459, 900465, 900690 | **Auto Harvest** (302054는 "Auto Harvest Expansion"으로. 900580에서 이 이름을 인용함) |
| 콤보 계수 | Combo Factor / Combo Multiplier | 302004, 202053 / 302053 | **Combo Factor** (302053 → Combo Factor II) |
| 가속 (바 버프) | Speed Up / Haste | 302009, 201028, 202052 / 900156 | **Speed Up** |
| 날 갈기 | Sharpen / Blade Sharpening | 302012, 201028, 202052 / 900156 | **Sharpen** |
| 기운 북돋기 | Power Up / Vigor Boost / Boost | 302061, 201027, 201029 / 900172 / 301029 "Boost Enhancement" | **Power Up**. 301029는 "Power Up Boost" |
| 수확량 | Harvest Amount / Harvest Yield / Yield / Harvest | 303012~303027 / 900448 / 900244, 900611, 900941 / 900128, 901087 | 능력치는 **Yield**. 노드 이름은 "Yield I, I+ …" (아니면 노드 이름만 Harvest Amount 유지, 능력치 라벨 900448은 "Yield") |
| 독버섯 | Poisonous Mushroom / Poison Mushroom | 900093, 201024 / 900181, 900244, 900868 | 버섯은 **Poisonous Mushroom**, 분류 태그(900276, 900864)는 Poison |
| 대왕(버섯) | King / Giant | 209001 King Matsutake / 900473~900491 Giant ○○ Mushroom | **King** (거대 버섯 = Giant Mushroom과 겹치므로) |
| 공속 | Attack Speed | 301004 (설명 202004·900159는 이동 속도) | **Harvester Speed** (실제 효과가 날아가는 속도) |
| 치명타 | Critical Hit / Critical / Crit | 301008 / 301023, 900262, 901016 / 900441, 201008, 900790 | 긴 라벨은 Critical, 짧은 칸만 Crit. 201008은 "Critical Damage node"로 (301023 이름과 맞춤) |
| 보유 효과 | Owned bonus / Owned Effect | 900453~900471, 900600 / 900702 | **Owned Bonus** |
| 맑음 (날씨) | Clear / Sunny | 208001 / 900823 | **Clear** |
| 장식 | Decor / Decoration | 901028, 901058, 901244 등 / 901079, 901088, 901112 | 짧은 라벨 Decor, 문장 Decoration. 같은 단어 "Decor Slot"은 901079도 맞춤 |
| 종 (도감 수) | species / types | 900693, 901269 / 900594, 900624 | **species** |
| 재화 | Currency / resources | 900813 / 901188 | 둘 다 **Not enough resources** |
| 뚝딱뚝딱 | Bang bang / Hammer hammer | 900057 / 900785 | **Tap tap!** 같은 의성어 하나로 |
| 핀볼 (옛 용어) | pinball | 900977, 901193, 901194, 901195 | **Harvester** (게임 안 다른 곳은 모두 Harvester) |
| 곱하기 기호 | × / x | ×: 900114~900147, 900506 등 · x: 900148~900200, 308002~308006, 900255 등 | **×**로 통일 |
| 퍼센트포인트 | %p / +{0}p | 900065, 900066, 900126, 900171, 900172, 900259, 900262, 900980, 901016, 901022 (900461·900463은 % 로 씀) | 영어권에 %p 표기가 없음. **%**로 통일 (또는 "pp") |

일관성이 잘 지켜진 용어: 균사석 Mycelium Stone, 균사 트리 Mycelium Tree, 세계수 World Tree, 수확기 Harvester, 영구/임시 수확기 permanent/temporary harvester, 군락(지) colony, 타격 점수 Hit Score, 황금 변종 Golden Variant, 해금 unlock, 호감도 Affection, 진화 Evolve, 각성 Awaken, 창고 Granary, 공물 Offering, 가호 Blessing, 보따리 Basket, 숲 장치 Forest Gadget, 배속 Speed.

---

## 3. 문제 목록

### 치명 (3)

| ID | KR | 현재 EN | 문제 | 수정안 |
|---|---|---|---|---|
| 201006 | 한 라운드의 제한시간을 늘려요. 라운드는 1~4스테이지 10초, 그 뒤로는 10초에 스테이지마다 0.5초씩(최대 +10초) 길어지고, 여기에 더해져요. | Increases the time limit of a round. Rounds start at 15 seconds and get 0.5 seconds longer each stage (up to +10 seconds), and this is added on top. | **수치 오류**: 원문은 10초인데 15초로 적혀 있고, "1~4스테이지는 고정"이라는 조건도 빠짐. 옛 원문에서 번역한 듯함 | Increases a round's time limit. Rounds last 10 seconds in Stages 1–4, then get 0.5 seconds longer each stage after that (up to +10 seconds). This bonus is added on top. |
| 900153 + 900154 | 해금하면 {0}사용 시작 / 발동률 10%로 | Unlock to start using {0} / At 10% proc rate, | **이어붙이기가 깨짐.** 코드(Defs.Nodes.cs:299)가 900154를 900153의 {0}에 넣음 → 실제 화면: "Unlock to start using At 10% proc rate, ". 스킬이 noRate면 "Unlock to start using " 으로 목적어 없이 끝남 | 900153: `Unlocks this skill{0}` / 900154: ` (10% trigger chance)` → 결과 "Unlocks this skill (10% trigger chance)" / "Unlocks this skill" |
| 303033 / 203033 | 희귀 포자 / 에픽 이상 버섯이 더 자주 돋아요. 다 올리면 숲의 본래 빈도가 돼요. | (비어 있음) | 미번역. 균사 트리 노드라 화면에 그대로 나옴 | name: `Rare Spores` / desc: `Epic-or-higher mushrooms sprout more often. Max it out to reach the forest's natural rate.` |

### 높음 (17)

| ID | KR | 현재 EN | 문제 | 수정안 |
|---|---|---|---|---|
| 901080 | 구역 정원 +1 | Zone Garden +1 | **오역**: 정원(定員, 정해진 인원)을 정원(garden)으로 옮김. 코드 주석상 구역에 보낼 수 있는 꼬마 수 +1 | Zone capacity +1 |
| 901157 | 경험치가 모자라요 (창고 버섯을 바치거나 의뢰를 해요) | Not enough EXP (offer Granary mushrooms or grant Favors) | **오역**: 의뢰(Quest)를 Favor(부탁)로 옮김. 다른 시스템 | Not enough EXP (offer Granary mushrooms or complete Quests) |
| 900203, 900641 | 일일 의뢰 {n}개 완료하기 / 경험치 {0}/{1} 의뢰로 모아요 | Complete {n} daily requests / Earn it through requests | 의뢰가 "requests"라서 부탁(900065 "request")과 헷갈림. 의뢰 창 제목은 Quests임 | Complete {n} daily quests / Earn it from Quests |
| 900327 / 900385 | 싸리버섯 / 산호버섯 | Coral Mushroom / Coral Mushroom | **다른 두 버섯이 같은 영어 이름** (도감에서 구분 불가) | 싸리버섯 → Yellow Coral Mushroom (설명 "연노랑 가지"), 산호버섯(판타지·바다) → Sea Coral Mushroom |
| 900002 | 선녀낙엽버섯 | Fairy Leaf Mushroom | 종 오역. 선녀낙엽버섯 = *Marasmius oreades*, 영어명 Fairy Ring Mushroom (설명 900384도 "fairy ring") | Fairy Ring Mushroom |
| 900247 | 간버섯 | Liver Mushroom | 종 오역. 간버섯 = *Pycnoporus cinnabarinus* (설명 "선명한 주홍색 선반 버섯"). Liver는 소혀버섯(Beefsteak)의 별명이라 더 헷갈림 | Cinnabar Polypore |
| 900222 | 잔나비걸상버섯 | Tinder Bracket | 종 오역. 잔나비걸상버섯 = *Ganoderma applanatum*(원숭이 걸상, 설명 900363). Tinder는 말굽버섯(900240 Hoof Fungus)의 다른 이름 | Artist's Conk |
| 900061, 900069 ↔ 900452~900470 | 불씨 꼬마 / 조약돌 꼬마 | Ember Kid / Pebble Kid ↔ Ember Little One / Pebble Little One | 같은 캐릭터가 두 이름으로 나옴 (용어 표 참조) | 모두 Little Ember / Little Pebble 형식으로 (스킨 900094~900103도 같은 형식) |
| 900580 | …버섯 오두막·꼬마 효과·'자동 수확 확장'으로 늘어나요 | …Mushroom Cabin, Little One bonuses, and 'Auto Harvest Expansion' | 건물 이름이 900085 "Mushroom Cottage"와 다름. 인용한 노드 이름도 302054 "Auto-Harvest Expansion"과 다름 | (Increased by Stage, Mushroom Cottage, Little One bonuses, and 'Auto Harvest Expansion') — 302054도 Auto Harvest Expansion으로 |
| 302053 | 콤보 계수 II | Combo Multiplier II | 선행 노드 302004 "Combo Factor"와 이름이 다름 (202053 설명도 Combo Factor로 인용) | Combo Factor II |
| 900156 | 가속·맑은 포자막 지속 {0}, 날 갈기 {1}타 | Haste and Clear Spore Veil last {0}, Blade Sharpening {1} hits | 스킬 이름이 302009 Speed Up, 302012 Sharpen과 다름 | Speed Up & Clear Spore Veil last {0} · Sharpen {1} hits |
| 900172, 301029 | 기운 북돋기 공격력 +{0}%p… / 북돋기 강화 | Vigor Boost Attack +{0}%p · proc rate +{1}%p / Boost Enhancement | 302061 "Power Up"과 다름 | 900172: Power Up: Attack +{0}% · trigger chance +{1}% / 301029: Power Up Boost |
| 900449, 900833, 203032 ↔ 303003 등 | 정산 골드 | Payout Gold ↔ Settlement gold | 정산 용어가 둘로 갈림 (용어 표 참조) | 모두 Payout으로 (Payout Bonus, Payout Bonus II, Mycelium Payout, Payout gold) |
| 901142, 901155, 901156 | 나이테 | Growth Ring | 다른 행(901052 등)은 Tree Rings | Tree Rings Lv.{3}/{4} / Tree Rings Lv.{0}! Tree Mushrooms grow faster / Grow your Tree Rings to Lv.{0} first |
| 900977, 901193, 901194, 901195 | 핀볼 속도 / 핀볼 수확기 / 핀볼을 잡고… / 라운드의 핀볼 | Pinball speed / pinball Harvester / Grab the pinball… / the pinball in a round | 옛 용어 "pinball"이 남음. 게임 안 다른 곳은 모두 Harvester (900828, 900878과 같은 내용이 둘로 나뉨) | Harvester speed / …a seasoned mushroom hunter's Harvester / Grab the Harvester, pull back, and release to launch! / …Harvester skins show on your Harvesters in a round |
| 301004 | 공속 | Attack Speed | 효과는 수확기가 날아가는 속도(202004 설명, 900159 "Move speed")인데 이름은 공격 속도라 오해를 부름 | Harvester Speed |
| 900554~900557 (+900443, 900154, 900155, 900171, 900186, 900190, 301028) | 발동률 | proc rate / Proc Rate | 201029·900126 등의 "trigger rate"와 섞임. "proc"는 캐주얼 유저에게 낯선 말 | 모두 trigger chance로. 예) 900554: Skill · Chance to trigger when hitting mushrooms |

### 중간 (41)

| ID | KR | 현재 EN | 문제 | 수정안 |
|---|---|---|---|---|
| 900305 | 민자주방망이버섯 | Lilac Club Mushroom | 실제 종 *Lepista nuda*의 통용명이 있음 | Wood Blewit |
| 900230 | 노란각시버섯 | Yellow Fieldcap | Yellow Fieldcap은 *Bolbitius*. 노란각시버섯은 *Leucocoprinus birnbaumii* (설명 "화분 흙에서도 돋는다"와 맞음) | Flowerpot Parasol |
| 900205 | 이끼꽃버섯 | Moss Flower Mushroom | 직역. 실제 종(*Gliophorus psittacinus*, 설명 "초록 점액")의 영어명이 있음 | Parrot Waxcap |
| 900210 | 갈색고리갓버섯 | Brown Ringed Parasol | 설명(독우산광대버섯급 맹독, 작음)에 맞는 *Lepiota brunneoincarnata*의 영어명은 Deadly Dapperling | Deadly Dapperling |
| 900249 | 말똥진흙버섯 | Dung Tinder Fungus | 직역("말똥")이라 영어로 어색함. *Phellinus igniarius* (설명 "버드나무, 말굽형") | Willow Bracket |
| 900261 | 붉은사슴뿔버섯 | Red Stag Antler Mushroom | *Trichoderma cornu-damae*의 영어 통용명이 있음. 설명("만지기만 해도 위험")과도 맞음 | Poison Fire Coral |
| 900248 | 소나무잔나비버섯 | Pine Bracket | *Fomitopsis pinicola*의 통용명 (설명 "노랗고 빨간 띠") | Red-Belted Conk |
| 900308 | 기와버섯 | Tile Mushroom | *Russula virescens*의 통용명 (설명 "청록빛, 기와처럼 갈라짐") | Green-Cracking Russula |
| 900347 | 한입버섯 | Bite-Size Mushroom | 직역. *Cryptoporus volvatus*의 영어명 | Veiled Polypore |
| 900394 | 맑은애주름버섯 | Clear Bonnet | *Mycena pura*의 영어명 (설명 "보랏빛") | Lilac Bonnet |
| 900328 | 빗자루처럼 갈라진 연노랑 가지. 데쳐 먹는 가을 숲 버섯. | …A blanched autumn forest mushroom. | "데쳐 먹는"을 "데쳐진"으로 옮김 | Pale yellow branches split like a broom. An autumn forest mushroom eaten after blanching. |
| 900300 | 살구 향이 나는 나팔 모양 버섯. 서양에서는 샹트렐이라 부른다. | …Called a chanterelle in the West. | 이름이 이미 Chanterelle이라 문장이 중복됨 | A trumpet-shaped mushroom with an apricot scent. In Korea it's named after the golden oriole. |
| 203022, 203023, 202053, 201018 | ○○를 끝까지 올린 뒤 … 더 늘려요 | Max out the Colony Count first, then add even more colonies. (나머지도 같은 구조) | 효과 설명인데 명령문(지시)처럼 읽힘 | Adds even more colonies after Colony Count is maxed. / Raises round-end gold further after Payout Bonus is maxed. / Boosts the per-combo multiplier further after Combo Factor is maxed. / Extends round time further after Duration is maxed. |
| 201005, 201024 | 느림·약화 디버프가 짧아져요 | …debuffs … last shorter | 비문 ("last shorter") | The slow and weaken debuffs from spore clouds wear off faster. / …and slow/weaken debuffs wear off faster. |
| 900572 | …(남은 시간 합 {0}분마다 한 판 수입만큼 골드) | (Gold equal to one round's income per {0} min of total time left) | 비용 안내인데 보상처럼 읽힘 | (Costs one round's income in gold per {0} min of total time left) |
| 900609 | ★ 별 능력: <b>{0}</b> 별마다 {1} · 지금 {2} | ★ Star Bonus: <b>{0}</b> per star {1} · Now {2} | 어순 오류 → "Attack per star +2%"처럼 읽힘 | ★ Star Bonus: <b>{0}</b> {1} per star · Now {2} |
| 900614 | ★ 별 능력: {0} (별마다 {1}) | ★ Star Bonus: {0} (per star {1}) | 어순 | ★ Star Bonus: {0} ({1} per star) |
| 900510 | {0} {1}/{2}개 수확 시 해금 | Unlocks when {0} {1}/{2} harvested | 비문. {0}=앞 버섯 이름 | Unlocks after harvesting {0} ({1}/{2}) |
| 901043 | 건물·꼬마 별에 쓴 균사석 {0}·골드 {1}을 돌려받았어요 | You got back the Mycelium Stone {0} · gold {1} spent on building and Little One stars | 어색함, 가운뎃점이 문장 안에 남음 | Refunded {0} Mycelium Stones and {1} gold spent on buildings and Little One stars |
| 901100 | <b>{0}</b>: "{1} {2}개 갖다 줘!" | <b>{0}</b>: "Bring me {2} {1}!" | {1}=버섯 이름(단수형)이라 "Bring me 5 Button Mushroom!"처럼 복수형이 틀림 | <b>{0}</b>: "Bring me {1} ×{2}!" |
| 901174 | {0}에게 {1} {2}개를 선물했어요 | Gave {2} {1} to {0} as a gift | 위와 같은 복수형 문제 | Gave {1} ×{2} to {0}! |
| 901111 | 도감 {0}/{1}종을 모으면 꼬마 공방이 열려요 | Collect {0}/{1} Collection species to open the Little One Workshop | 어색함 | Discover {1} species in the Collection to open the Little One Workshop ({0}/{1}) |
| 901179 | 도감을 더 모으면 공방이 열려요 | Collect more of the Collection to open the Workshop | 어색함 | Discover more mushrooms to open the Workshop |
| 901208, 901211, 901215 | {0}을(를) 데려오려면 {1}{2}이 필요해요 (지금 {3}) 등 | Bringing {0} needs {1}{2} (now {3}) / Level Up needs {0}{1} / ★{0} Evolution needs… | 무생물 주어 + needs 구조가 어색함 | You need {1}{2} to bring {0} (you have {3}) / You need {0}{1} to level up / You need {1}{2} for ★{0} Evolution (you have {3}) |
| 900473~900491 | ○○ 대왕버섯 | Giant ○○ Mushroom | 대왕 = King(209001)인데 Giant로 옮겨 거대 버섯(Giant Mushroom, 900528 등)과 겹침 | King Ember Mushroom, King Zappy Mushroom … |
| 303008 | 변종 출현율 | Variant Rate | 실제 효과는 희귀 등급(에픽 이상) 확률. Golden Variant와 헷갈림 | Rare Spawn Rate |
| 303013, 900524 | 배수 획득 / ×3 배수 획득! | Multiplier Gain / ×3 multiplier gained! | 무슨 효과인지 모르는 이름. 실제로는 수확량 3배 | Triple Harvest / Triple harvest! |
| 900983 | 고급 | Fine | 등급 이름으로 낯섦 (Common < ? < Rare) | Uncommon |
| 900860 | (2줄 문자열) | (2줄 문자열) | TSV에 `\n`이 아니라 **실제 줄바꿈 문자**가 들어 있음 (줄 1081~1083). 다른 행과 형식이 달라 임포트 때 행이 깨질 수 있음. EN 안의 "Gold" 대문자도 다른 행과 다름 | 줄바꿈을 `\n`으로 바꿔서 저장. "as many as your gold allows" |
| 900823 | 오늘의 날씨: 맑음 | Today's Weather: Sunny | 208001 날씨 이름 Clear와 다름 (씬 초기 텍스트) | Today's Weather: Clear |
| 901046, 901051, 901061 | 세계수가 {0}단계가 되면… / 성장 · {0}단계 / 진화 → {0}단계 | …stage {0} / Growth · Stage {0} / Evolve → Stage {0} | 세계수 단계는 다른 행에서 Tier. Stage는 게임 스테이지와 겹침 | Joins when the World Tree reaches Tier {0}. / Growth · Tier {0} {1} / Evolve → Tier {0} {1} |
| 900600 / 900702 | 보유 효과 | Owned bonus / Owned Effect | 같은 라벨이 두 가지 | Owned Bonus: {0} (둘 다) |
| 900545, 900657, 900866, 900913, 203009 | 도감 | Collection / Mushroom Guide | 용어 갈림 (용어 표 참조) | Mushroom Collection / Collection |
| 900181, 900244, 900868 | 독버섯 | Poison Mushroom(s) | 900093·201024는 Poisonous Mushroom | Poisonous Mushroom(s) |
| 303012~303027, 900448 | 수확량 | Harvest Amount I… / Harvest Yield | 같은 능력치가 둘로 갈림 | 노드 Yield I, Yield I+ … / 라벨 Yield |
| 900148~900200 구간, 308002~308006, 900611 등 | ×{0} | x{0} | 같은 기호가 x와 ×로 섞임 (용어 표 참조) | × 로 통일 |
| 900148, 900149, 900175, 900176, 900186, 900191~900198, 900218, 900225, 900238, 900422 | 버섯/수확기/콤보/스킬 (일반명사) | "Mushroom", "Harvester", "Combo", "Skill"을 문장 중간에 대문자로 씀 | 같은 내용의 앞쪽 행들(upg desc)은 소문자. 번역 묶음마다 스타일이 다름 | 문장 중간 일반명사는 소문자로 (고유명·노드명만 대문자) |
| 900065, 900066, 900126, 900171, 900172, 900259, 900262, 900980, 901016, 901022 | +{0}p / +5%p | +{0}p / %p | %p는 한국식 표기라 영어권에서 오타처럼 보임 | "+{0}"(U.Pct가 이미 %를 붙임) / "+5%". 정확한 표기가 꼭 필요하면 "pp" |
| 900018 | 촉촉한 게 최고야~ | Nothing beats moist~ | 비문이고, moist는 영어권에서 거부감 있는 단어로 유명함 | Nothing beats a little damp~ |
| 202061 | …조작하는 만큼 강해지는 초반 핵심 버프예요. | …makes you stronger the more you play. | "조작" = 바를 직접 움직여 받아치는 것. "play"는 뜻이 흐려짐 | …A key early buff that rewards active bar play. |
| 900975~900982 | 라운드 ○○ +{0} | Round colonies +{0} / Round all skill trigger rate +{0} … | "Round"를 형용사처럼 앞에 붙여 어색함 (특히 900982) | In rounds: Colonies +{0} / In rounds: All skill trigger chances +{0} … (또는 "Colonies +{0} (rounds)") |

### 낮음 (33)

| ID | KR | 현재 EN | 문제 | 수정안 |
|---|---|---|---|---|
| 900035 | ...단단해. | ...Hard. | 조약돌 꼬마의 자기소개인데 뜻이 애매함 | ...I'm solid. |
| 900057, 900785 | 뚝딱뚝딱! | Bang bang! / Hammer hammer! | Bang bang은 총소리처럼 들림. 두 행이 서로 다름 | Tap tap! (둘 다) |
| 900788 | 다 지었다! 어때? | All done! How is it? | 부자연스러움 | All done! What do you think? |
| 900015 | 번개 치는 날 태어났어! | I was born on a lightning day! | 어색함 | I was born during a thunderstorm! |
| 900042 | 여기 세계수 진짜 좋아! | This World Tree is really great! | 딱딱함 | I love it here on the World Tree! |
| 901320 | 냠냠 버섯 냄새 | Yum yum, mushroom smell | 어색함 | Mmm, smells like mushrooms! |
| 900359 | 하얀 털 같은 침이 고드름처럼 늘어진다. | White, fur-like fangs… | 버섯의 침은 spines (fangs = 송곳니) | White, fur-like spines hang down like icicles. |
| 900363 | 원숭이가 걸터앉을 만큼 크고 넓은 반원 선반. | A big, wide half-moon shelf big enough for… | big이 두 번 | A wide half-moon shelf big enough for a monkey to sit on. |
| 900413 | 마귀곰보버섯 | Devil's False Morel | 통용명은 False Morel | False Morel |
| 900389 | 무당버섯 | Russula | 속(屬) 이름. 빨간 종의 통용명이 있음 (선택) | The Sickener (또는 Russula 유지) |
| 900241 / 900321 | 상황버섯 / 능이버섯 | Sanghwang Mushroom / Neungi Mushroom | 한국 고유 이름이라 허용 가능. 영어권 통용명도 있음 (선택) | Meshima / Scaly Hedgehog (아니면 현행 유지) |
| 900208 | 화경버섯 | Jack-o'-Lantern Mushroom | 허용 범위. 아시아 종(*Omphalotus japonicus*) 통용명은 Moonlight Mushroom (선택) | 현행 유지 또는 Moonlight Mushroom |
| 900237 (+900966, 900968, 900974) | 버섯 폐허 도시 | Mushroom Ruins City | 어순이 어색함 | Ruined Mushroom City |
| 900116, 900534, 901292 | 임시 수확기 {0}개 / 분신이 {0}마리 남았어요! / {0}칸 | {0} temporary harvesters / {0} clones remain! / {0} slots | {0}=1일 수 있음 (split은 1+P, 분신 남은 수 1) → "1 clones" | Temp harvesters ×{0} (6 sec) / Clones left: {0}! / Slots: {0} |
| 202042, 900123 | 최대 8단계 | up to 8 levels | 연쇄 단계인데 levels라 노드 레벨처럼 읽힘 | up to 8 chains |
| 900188 | …4레벨 모두 한 단계 더 | …Lv4 everything one step more | 어색함 | Lv2: range & speed · Lv3: power · Lv4: all of the above +1 |
| 900611 | …수확 ×7~10 | …Harvest ×7~10 | 영어에서 범위는 – | …Yield ×7–10 |
| 900547 | 지금 스테이지 {0} · {1} | Now Stage {0} · {1} | 900578 "Current Stage"와 다름 | Current Stage {0} · {1} |
| 900639 | 가장 큰 나무예요 | It's the biggest tree | 최대 레벨 안내로 어색함 | It's fully grown! |
| 900664 | 보스 도전 끔 · 보스 스테이지에서는 파밍만 해요 | …Only farming on boss stages | 비문 | Boss Challenge Off · Boss stages are farm-only |
| 900917 | 젤리 버섯 안 | Inside Jelly Mushroom | 관사/복수 | Inside Jelly Mushrooms |
| 900938 | 룬 신목 | Rune Tree | 신목(神木)의 뜻이 빠짐 | Sacred Rune Tree |
| 900956, 901068, 901109 | 선반·다발 / 꼬마 선물·부탁 / 지역·날씨 | shelf · cluster / gifts · Favors / region · weather | 문장 안에 가운뎃점이 그대로 남음 | shelf and cluster / gifts and Favors / region and weather |
| 901027 / 901081 | 칭호 '숲의 장인' / 칭호 '꼬마 대장' | Title 'Forest Artisan' / Title: 'Little One Captain' | 콜론 유무가 다름 | Title: "Forest Artisan" / Title: "Little One Leader" |
| 901062~901065 | ○○ 부족 | Not enough Tree Rings / Not enough EXP / Not Enough Gold / Not Enough Mycelium Stones | 대소문자가 섞임 | 모두 "Not enough …" |
| 901139, 901301 | 같은 장식 1 | 1 same decoration | 비문 | 1 duplicate decoration |
| 900808 | 공방 (도감 10종) | Workshop (10 Species) | 해금 조건이라는 뜻이 안 드러남 | Workshop (needs 10 species) |
| 900520 | 도토리 점등! 점수 ×2 | Acorn lit! | 도토리가 셋 | Acorns lit! Score ×2 |
| 900740, 900741 | 눌러서 배속 / 누르기·F·우클릭 배속 | Tap to Change Speed / Tap, F, or Right-Click for Speed | 짧은 배속 버튼 라벨인데 KR보다 2~3배 김 | Tap: Speed / Tap·F·RMB: Speed |
| 201021, 301032, 301030 | 끝없는 투지 / 균사 투지 | Endless Fighting Spirit / Mycelium Fighting Spirit | 노드 이름 칸 대비 김 | Endless Grit / Mycelium Grit |
| 900763 | 아직 못 잡은 꼬마 | Little One Not Yet Caught | 꼬마는 "합류"하는 설정이라 caught가 어색함. 901191 "Yet to Join"과 다름 | Little One Yet to Join |
| 900136 | 에픽 이상 가중치 {0} | Epic and above weight {0} | "weight"는 개발 용어 | Epic+ odds {0} |
| 900281, 900282, 900617~900619, 900626, 900894~900903, 901204, 901218, 901219, 901221, 901302, 901303 | (디버그·인스펙터 문자열) | — | 플레이어에게 안 보이는 개발용 문자열. 번역 우선순위를 낮추거나 번역 대상에서 빼도 됨 (901204 등은 Inspector 툴팁) | 현행 유지, 번역 테이블에서 제외 검토 |

---

## 4. 참고

- placeholder·태그·줄바꿈 개수는 이미 자동 검사를 통과했으므로 여기서 다시 보지 않음. 위 "치명" 900153은 개수는 맞지만 **문장 조립 결과**가 깨지는 경우임.
- 이미 알려진 빈 행(`after:… cnt:…` 데이터 태그, `이`/`가`, `한국어`, 901323)은 제외함. 303033/203033은 화면에 나오므로 치명으로 포함함.
- 버섯 영어명 수정안은 설명문(색·서식지·독성)과 맞는 실제 종의 통용 영어명을 기준으로 했음. 한국 고유 이름(Sanghwang, Neungi)은 의도한 것이면 유지해도 됨.
