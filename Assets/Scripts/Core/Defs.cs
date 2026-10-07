using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoreMush
{
    // Static game data ported 1:1 from Prototype/MoreMush.v14.html (constants, tables, formulas).
    public static partial class Defs
    {
        // ===== 기본 상수 (1920×1080 논리 좌표) =====
        public const int W = 1920, H = 1080;
        public const float BAR_T = 14;
        // 화면에서 잘 보이게 키우는 배율. 그림과 충돌 범위를 같이 키운다 (수확기 = 핀볼, 장치 = 그루터기·이끼·두더지굴·개울·도토리)
        public const float BALL_SCALE = 1.5f, DEV_SCALE = 1.4f;
        public static readonly float[] MAP_ZOOM = { 1.5f, 1.35f, 1.2f, 1.1f, 1.0f };   // 필드 넓히기 0~4레벨
        public const int MAX_BALLS = 60;

        // ===== 분류 =====
        public class Cat { public string key, name, color, dark, light; }
        public static readonly Dictionary<string, Cat> CATS = new Dictionary<string, Cat>
        {
            ["ed"] = new Cat { key = "ed", name = "식용", color = "#f28c28", dark = "#b85e0e", light = "#ffe2c2" },
            ["md"] = new Cat { key = "md", name = "약용", color = "#3fae4a", dark = "#237a2c", light = "#d2f2cf" },
            ["ps"] = new Cat { key = "ps", name = "독", color = "#9b4fd1", dark = "#6a2c98", light = "#ead6f7" },
        };
        public static readonly string[] CAT_KEYS = { "ed", "md", "ps" };
        public const string GOLD_C = "#f5c518";
        public const float SELL_PRICE = 1;

        // ===== 등급 =====
        public class Tier { public string name, color; public float hp, score, drop, r, w; }
        public static readonly Tier[] TIERS =
        {
            new Tier { name = "일반", hp = 3, score = 1, drop = 1, r = 26, w = 50, color = "#cfc8b8" },
            new Tier { name = "에픽", hp = 10, score = 5, drop = 3, r = 22, w = 25, color = "#5fb8ff" },
            new Tier { name = "유니크", hp = 30, score = 20, drop = 10, r = 18, w = 15, color = "#c77dff" },
            new Tier { name = "레전드리", hp = 100, score = 100, drop = 40, r = 16, w = 10, color = "#ffb627" },
        };
        static readonly float[] BASE_R = TIERS.Select(t => t.r).ToArray();

        // ===== 밸런스 프리셋 =====
        // colStep: 군락지 수 강화 레벨당 늘어나는 군락지 수 (덧셈식)
        public class Preset { public string id, n; public float col0, colStep, rMul, ball0, launch, min; public float[] hp; }
        public static readonly Dictionary<string, Preset> PRESETS = new Dictionary<string, Preset>
        {
            ["tuned"] = new Preset { id = "tuned", n = "조정안", col0 = 12, colStep = 5, rMul = 1.25f, ball0 = 18, hp = new float[] { 1, 4, 12, 40 }, launch = 1200, min = 700 },
            ["spec"] = new Preset { id = "spec", n = "명세 원본", col0 = 3, colStep = 3, rMul = 1, ball0 = 14, hp = new float[] { 3, 10, 30, 100 }, launch = 900, min = 320 },
        };
        public static Preset TUNE = PRESETS["tuned"];
        const string PresetKey = "mushroomPinball_preset";

        public static void ApplyPreset(string id)
        {
            TUNE = PRESETS.TryGetValue(id ?? "", out var p) ? p : PRESETS["tuned"];
            for (int i = 0; i < TIERS.Length; i++) { TIERS[i].r = Mathf.Round(BASE_R[i] * TUNE.rMul); TIERS[i].hp = TUNE.hp[i]; }
            PlayerPrefs.SetString(PresetKey, TUNE.id);
        }

        public static void LoadPreset() => ApplyPreset(PlayerPrefs.GetString(PresetKey, "tuned"));

        // 라운드 시간: 처음 15초, 스테이지마다 +0.5초(최대 +10초). 지속시간 강화는 여기에 초 단위로 더한다
        public const float BASE_TIME = 15;
        public static double StageTime(int stage) => Math.Min(10, 0.5 * (stage - 1));
        public const float INCOME_MUL = 3;
        public const float SCORE_GOLD = 1;
        public const float COST_MUL = 1;
        public static int ColCount(int L) => Mathf.CeilToInt(TUNE.col0 + TUNE.colStep * L);

        // ===== 도감 75종 =====
        public class Species
        {
            public string c, n, sh, c1, c2, pat, d;
            public int t, idx;
            public string id, trait, ab, afterId, after, theme, spore;
            public int cnt, tax;
            public bool init, jelly, wander, glow, thick, tall, small, big, leaf, ring, horn;
        }

        public static readonly List<Species> SPECIES = new List<Species>();
        public static readonly Dictionary<string, Species> SP = new Dictionary<string, Species>();
        public static readonly Dictionary<string, Species> SP_BY_NAME = new Dictionary<string, Species>();
        public static int SP_TOTAL => SPECIES.Count;
        public static int SP_PER_CAT => SP_TOTAL / 3;

        // [분류, 이름, 등급, 모양, 주색, 보조색, 무늬, 옵션, 특징 한 줄]. 옵션은 "key:value" 목록.
        static readonly string[][] SPECIES_RAW =
        {
            new[] { "ed", "양송이", "0", "button", "#f7f2e8", "#e2d6c0", "", "init", "세계에서 가장 많이 재배되는 버섯. 갓이 펴지기 전에 먹는다." },
            new[] { "ed", "느타리", "0", "fan", "#b9b5ae", "#efeae1", "gills", "init", "죽은 활엽수에 부채 모양으로 겹겹이 자란다." },
            new[] { "ed", "표고", "0", "cap", "#8b5a3a", "#f1e6d4", "crack", "after:양송이 cnt:10", "갓이 거북등처럼 갈라진 것을 화고라 부르며 귀하게 친다." },
            new[] { "ed", "팽이버섯", "0", "cylinder", "#f6f1dc", "#e9dfba", "", "after:느타리 cnt:10", "가늘고 긴 대에 작은 갓이 다발로 달린다. 원래는 노란 갈색이다." },
            new[] { "ed", "목이버섯", "0", "jelly", "#4b3127", "#8a6452", "", "jelly after:표고 cnt:15", "사람 귀를 닮았고, 말려도 물에 불리면 다시 말랑해진다." },
            new[] { "ed", "새송이버섯", "0", "cap", "#c9a77e", "#f4ecdc", "", "thick after:팽이버섯 cnt:15", "굵고 하얀 대가 통통한 버섯. 큰느타리라고도 한다." },
            new[] { "ed", "먹물버섯", "0", "cylinder", "#f4f2ec", "#2b2a33", "scales", "after:목이버섯 cnt:20", "다 자라면 갓이 스스로 녹아 먹물 같은 액체가 된다." },
            new[] { "ed", "말불버섯", "0", "ball", "#f3efe3", "#d6cdb6", "warts", "spore:slow after:새송이버섯 cnt:20", "다 익으면 살짝만 눌러도 연기처럼 포자를 뿜는다." },
            new[] { "ed", "꾀꼬리버섯", "1", "trumpet", "#f6a44e", "#f9c98a", "", "tax:1", "살구 향이 나는 나팔 모양 버섯. 서양에서는 샹트렐이라 부른다." },
            new[] { "ed", "덕다리버섯", "1", "shelf", "#f28a22", "#f7d44a", "", "after:표고 cnt:30", "나무에 선반처럼 층층이 붙고, 닭고기 같은 식감이라고 한다." },
            new[] { "ed", "큰갓버섯", "1", "cap", "#a3785a", "#efe3cf", "scales", "tall after:먹물버섯 cnt:25", "키가 30cm 넘게 자라는 커다란 우산 버섯." },
            new[] { "ed", "민자주방망이버섯", "1", "cap", "#a88bd2", "#cdbbea", "", "after:꾀꼬리버섯 cnt:15", "갓과 주름이 모두 연보랏빛인 늦가을 버섯." },
            new[] { "ed", "기와버섯", "1", "cap", "#4fa596", "#dfe9e2", "crack", "after:덕다리버섯 cnt:15", "청록빛 갓 표면이 기와처럼 잘게 갈라진다." },
            new[] { "ed", "만가닥버섯", "1", "cluster", "#cdb49a", "#efe5d6", "", "after:말불버섯 cnt:25", "한 뿌리에서 만 가닥으로 갈라진 듯 빽빽하게 모여 자란다." },
            new[] { "ed", "곰보버섯", "2", "cone", "#b88a55", "#efe2c8", "honey", "after:큰갓버섯 cnt:20", "갓이 벌집처럼 움푹움푹 파인 봄 버섯. 반드시 익혀 먹는다." },
            new[] { "ed", "달걀버섯", "2", "egg", "#e8452c", "#f8f3ea", "", "tax:4", "하얀 알 같은 주머니를 깨고 주홍빛 갓이 올라온다." },
            new[] { "ed", "소혀버섯", "2", "shelf", "#b3222d", "#e0555a", "", "after:기와버섯 cnt:20", "소 혀처럼 붉고 촉촉하며, 자르면 붉은 즙이 난다." },
            new[] { "ed", "능이버섯", "2", "trumpet", "#6b4a35", "#a07a5a", "scales", "after:곰보버섯 cnt:12", "1능이 2표고 3송이라 할 만큼 향이 진한 가을 버섯." },
            new[] { "ed", "송이버섯", "3", "cap", "#8c6b4b", "#f3e8d6", "", "thick glow after:능이버섯 cnt:10", "살아 있는 소나무 뿌리와 함께 자라 인공 재배가 어렵다." },
            new[] { "ed", "송로버섯", "3", "lump", "#3a2a22", "#6a5040", "", "glow tax:8", "땅속에서 자라 돼지나 개가 냄새로 찾아낸다. 트러플이라 부른다." },
            new[] { "ed", "싸리버섯", "1", "coral", "#f3d9a4", "#fff1cf", "", "theme:forest", "빗자루처럼 갈라진 연노랑 가지. 데쳐 먹는 가을 숲 버섯." },
            new[] { "ed", "달빛표고", "2", "cap", "#5a6fa8", "#dfe6ff", "crack", "theme:night glow", "(판타지) 달빛을 받으면 갓의 갈라진 틈이 은빛으로 빛난다." },
            new[] { "ed", "주름버섯", "1", "button", "#f4efe6", "#d8a7a0", "", "theme:field", "풀밭에 흔한 양송이의 친척. 주름이 분홍빛이다." },
            new[] { "ed", "바다송이", "2", "cap", "#3f8fb0", "#dff4ff", "", "theme:sea thick", "(판타지) 갯바위 틈에서 자라는 짭조름한 송이." },
            new[] { "ed", "벽돌먹물버섯", "2", "cylinder", "#b5523a", "#3a2a2a", "scales", "theme:ruins", "(판타지) 무너진 벽돌 틈에서 자라 갓이 벽돌빛이다." },

            new[] { "md", "구름버섯", "0", "fan", "#6a6f8e", "#d8c49a", "stripe", "init", "여러 색 띠무늬 부채가 구름처럼 겹쳐 자란다." },
            new[] { "md", "치마버섯", "0", "fan", "#f1eee7", "#cfc5b5", "gills", "small init", "주름이 세로로 갈라진 작은 흰 부채 버섯." },
            new[] { "md", "간버섯", "0", "shelf", "#ea5a1e", "#f6934e", "", "after:구름버섯 cnt:10", "죽은 나무에 붙는 선명한 주홍색 선반 버섯." },
            new[] { "md", "흰목이", "0", "jelly", "#f3f0e8", "#ffffff", "", "jelly after:치마버섯 cnt:10", "반투명한 흰 꽃잎이 모인 듯한 젤리 버섯." },
            new[] { "md", "잎새버섯", "0", "cluster", "#8a7a69", "#c4b8a6", "", "leaf after:간버섯 cnt:15", "회갈색 꽃잎 같은 갓이 다발로 겹친다. 일본 이름은 마이타케." },
            new[] { "md", "한입버섯", "0", "ball", "#d99a48", "#f1cc8e", "pore", "after:흰목이 cnt:15", "밤톨처럼 동그랗고 소나무 줄기에 붙어 자란다." },
            new[] { "md", "복령", "0", "lump", "#7a5236", "#efe6d2", "", "after:잎새버섯 cnt:20", "소나무 뿌리에 붙어 땅속에서 자라는 덩어리. 속살은 하얗다." },
            new[] { "md", "저령", "0", "cluster", "#9c8064", "#e8dcc6", "", "after:한입버섯 cnt:20", "작은 갓이 수십 개씩 가지 끝에 달려 꽃다발처럼 보인다." },
            new[] { "md", "꽃송이버섯", "1", "flower", "#f3e3bd", "#dcc38a", "", "after:잎새버섯 cnt:30", "꽃양배추처럼 주름진 크림색 덩어리로 자란다." },
            new[] { "md", "노루궁뎅이", "1", "furry", "#f8f4ea", "#d9ccb4", "", "tax:2", "하얀 털 같은 침이 고드름처럼 늘어진다." },
            new[] { "md", "말굽버섯", "1", "shelf", "#9c9c96", "#66665f", "rings", "after:복령 cnt:25", "말발굽 모양으로 해마다 나이테 같은 층이 는다." },
            new[] { "md", "잔나비걸상버섯", "1", "shelf", "#8b6b4a", "#ece0c6", "", "big after:말굽버섯 cnt:15", "원숭이가 걸터앉을 만큼 크고 넓은 반원 선반." },
            new[] { "md", "목도리방귀버섯", "1", "star", "#b89c78", "#8a6d4f", "", "spore:slow after:저령 cnt:25", "별 모양 목도리 위 공에서 방귀처럼 포자가 퐁 나온다." },
            new[] { "md", "소나무잔나비버섯", "1", "shelf", "#5a3324", "#f0b23a", "rings", "after:꽃송이버섯 cnt:15", "소나무에 붙는 선반 버섯. 가장자리가 노랗고 빨갛게 띠를 두른다." },
            new[] { "md", "영지버섯", "2", "kidney", "#a41f2b", "#eaa53a", "", "tax:5", "옻칠한 듯 붉은 광택이 나는 콩팥 모양 버섯." },
            new[] { "md", "상황버섯", "2", "shelf", "#6f4a2a", "#f3c42e", "", "after:잔나비걸상버섯 cnt:20", "갈색 선반에 샛노란 테두리가 둘러져 있다." },
            new[] { "md", "차가버섯", "2", "lump", "#221c19", "#8b5a3a", "", "after:소나무잔나비버섯 cnt:20", "자작나무에 붙은 검은 숯덩이처럼 보인다." },
            new[] { "md", "말똥진흙버섯", "2", "shelf", "#3e3a36", "#9a7a52", "crack", "after:상황버섯 cnt:12", "버드나무 등에 붙는 단단한 말굽형 버섯. 표면이 잘게 갈라진다." },
            new[] { "md", "동충하초", "3", "club", "#f27a1a", "#ffb05a", "", "wander glow after:차가버섯 cnt:10", "곤충 몸에서 주황 곤봉처럼 솟아난다." },
            new[] { "md", "눈꽃동충하초", "3", "club", "#f5f0d8", "#fff9c8", "", "wander glow tax:10", "누에 번데기에서 눈꽃처럼 하얀 가루 가지가 피어난다." },
            new[] { "md", "자작나무버섯", "1", "shelf", "#efe7d6", "#b7a78f", "", "theme:forest", "자작나무에만 붙는 하얀 선반 버섯." },
            new[] { "md", "달빛동충하초", "2", "club", "#9fd0ff", "#e6f4ff", "", "theme:night wander glow", "(판타지) 밤에만 땅 위로 올라와 떠도는 푸른 곤봉." },
            new[] { "md", "선녀낙엽버섯", "1", "cap", "#e8c99a", "#f6e6c8", "", "theme:field small", "잔디밭에 둥근 고리 모양으로 줄지어 난다. 요정의 고리라고도 한다." },
            new[] { "md", "산호버섯", "2", "coral", "#ff8a9a", "#ffd0d8", "", "theme:sea", "(판타지) 바닷물에 젖어도 시들지 않는 분홍 산호 모양 버섯." },
            new[] { "md", "녹슨영지", "3", "kidney", "#8a4a2a", "#c98a4a", "", "theme:ruins glow", "(판타지) 녹슨 철골에 붙어 자란 영지. 쇠 냄새가 난다." },

            new[] { "ps", "무당버섯", "0", "cap", "#e0252f", "#f7efe4", "", "init", "비 온 뒤 흔히 보이는 새빨간 버섯. 맵고 쓰다." },
            new[] { "ps", "노란다발", "0", "cluster", "#e9c21d", "#f4de6c", "", "init", "유황색 작은 버섯이 다발로 모여 자란다." },
            new[] { "ps", "노란각시버섯", "0", "bell", "#f6da0c", "#fff27e", "", "after:무당버섯 cnt:10", "샛노란 종 모양. 화분 흙에서도 종종 돋는다." },
            new[] { "ps", "맑은애주름버섯", "0", "cap", "#9b7cc4", "#dccbef", "", "small after:노란다발 cnt:10", "보랏빛이 도는 작고 가냘픈 갓." },
            new[] { "ps", "이끼꽃버섯", "0", "jelly", "#2e7a3a", "#72d06c", "", "jelly after:노란각시버섯 cnt:15", "짙은 초록 점액에 덮여 반짝인다." },
            new[] { "ps", "비단빛깔때기버섯", "0", "trumpet", "#fbfbfb", "#e5e5ef", "", "after:맑은애주름버섯 cnt:15", "순백색 비단 같은 깔때기 모양." },
            new[] { "ps", "땀버섯", "0", "bell", "#b08a5a", "#e2cfa8", "scales", "after:이끼꽃버섯 cnt:20", "먹으면 땀이 비 오듯 난다는 갈색 고깔 버섯." },
            new[] { "ps", "갈황색미치광이버섯", "0", "cluster", "#e08a2a", "#f6c46a", "", "after:비단빛깔때기버섯 cnt:20", "주황빛 다발 버섯. 먹으면 환각에 빠진다고 한다." },
            new[] { "ps", "광대버섯", "1", "cap", "#d9271c", "#ffffff", "dots", "spore:weak tax:3", "빨간 갓에 하얀 사마귀 점. 동화 속 그 버섯." },
            new[] { "ps", "뱀껍질광대버섯", "1", "cap", "#8a7a65", "#dccfb9", "scales", "spore:weak after:땀버섯 cnt:25", "갓에 뱀 비늘 같은 무늬가 있다." },
            new[] { "ps", "붉은싸리버섯", "1", "coral", "#f47d6b", "#f8b58c", "", "after:갈황색미치광이버섯 cnt:25", "분홍·주황 산호처럼 가지를 친다." },
            new[] { "ps", "알광대버섯", "1", "cap", "#8e9c4c", "#f3f3ea", "", "spore:both ring after:뱀껍질광대버섯 cnt:15", "올리브색 갓과 흰 턱받이. 치명적인 맹독 버섯." },
            new[] { "ps", "마귀곰보버섯", "1", "brain", "#8a3b2a", "#dcbc9c", "", "after:붉은싸리버섯 cnt:15", "뇌처럼 주름진 적갈색 갓. 곰보버섯과 헷갈리기 쉽다." },
            new[] { "ps", "독깔때기버섯", "1", "trumpet", "#d9a06a", "#f0d2a8", "", "after:광대버섯 cnt:15", "깔때기 모양 갈색 버섯. 먹으면 손발이 불에 덴 듯 아프다." },
            new[] { "ps", "화경버섯", "2", "fan", "#e8923a", "#8dff9e", "gills", "glow tax:6", "낮엔 주황빛, 밤엔 주름이 초록빛으로 은은하게 빛난다." },
            new[] { "ps", "개나리광대버섯", "2", "cap", "#f2d22a", "#fbf3c9", "", "ring after:알광대버섯 cnt:20", "개나리처럼 노란 갓을 가진 광대버섯." },
            new[] { "ps", "마귀광대버섯", "2", "cap", "#7a5a3a", "#ffffff", "dots", "spore:weak after:마귀곰보버섯 cnt:20", "갈색 갓에 흰 사마귀가 흩뿌려진 광대버섯." },
            new[] { "ps", "독우산광대버섯", "2", "cap", "#fbfbf6", "#e9ecef", "", "spore:both ring glow after:개나리광대버섯 cnt:12", "온통 새하얀 우산. 죽음의 천사라 불린다." },
            new[] { "ps", "붉은사슴뿔버섯", "3", "coral", "#e2241a", "#ff7b3a", "", "horn after:마귀광대버섯 cnt:10", "불꽃 같은 붉은 손가락 모양. 만지기만 해도 위험하다." },
            new[] { "ps", "갈색고리갓버섯", "3", "cap", "#c9b49a", "#7a4a2a", "scales", "ring glow tax:12", "작고 얌전해 보이지만 독우산광대버섯과 같은 맹독을 품었다." },
            new[] { "ps", "흰가시광대버섯", "2", "cap", "#f6f4ee", "#e8e2d6", "warts", "theme:forest ring", "하얀 갓에 가시 같은 사마귀가 돋은 독버섯." },
            new[] { "ps", "달빛애주름버섯", "1", "cap", "#7ad6a0", "#d8ffe6", "", "theme:night small glow", "(판타지) 밤이면 초록빛을 내는 작은 애주름버섯." },
            new[] { "ps", "말똥버섯", "1", "bell", "#9a8a74", "#3a3430", "", "theme:field", "소똥·말똥 위에 자라는 회갈색 종 모양 버섯." },
            new[] { "ps", "해파리버섯", "2", "jelly", "#b48cff", "#efe2ff", "", "theme:sea jelly spore:weak", "(판타지) 해파리처럼 반투명하고 흐물거린다. 만지면 따끔하다." },
            new[] { "ps", "콘크리트광대버섯", "3", "cap", "#9a9a96", "#d8d8d2", "crack", "theme:ruins ring spore:both", "(판타지) 콘크리트를 뚫고 자란 회색 광대버섯. 이야기의 마지막 독." },
        };

        // ===== 별 능력치 =====
        public class Ability { public string n, unit; public float per; public bool pct, pp; }
        public static readonly Dictionary<string, Ability> AB = new Dictionary<string, Ability>
        {
            ["atk"] = new Ability { n = "공격력", per = 0.02f, pct = true },
            ["spd"] = new Ability { n = "이동 속도", per = 0.01f, pct = true },
            ["size"] = new Ability { n = "수확기 크기", per = 0.01f, pct = true },
            ["crit"] = new Ability { n = "치명타 확률", per = 0.003f, pct = true, pp = true },
            ["skillDmg"] = new Ability { n = "스킬 피해", per = 0.03f, pct = true },
            ["skill"] = new Ability { n = "스킬 발동률", per = 0.015f, pct = true },
            ["dur"] = new Ability { n = "제한시간", per = 0.1f, unit = "초" },
            ["bar"] = new Ability { n = "바 길이", per = 0.015f, pct = true },
            ["combo"] = new Ability { n = "콤보 계수", per = 0.02f, pct = true },
            ["score"] = new Ability { n = "타격 점수", per = 0.03f, pct = true },
            ["harvest"] = new Ability { n = "수확량", per = 0.02f, pct = true },
            ["price"] = new Ability { n = "판매가", per = 0.03f, pct = true },
            ["rare"] = new Ability { n = "에픽 이상 출현", per = 0.02f, pct = true },
            ["golden"] = new Ability { n = "황금 변종 확률", per = 0.0005f, pct = true, pp = true },
            ["regen"] = new Ability { n = "군락 재생 속도", per = 0.015f, pct = true },
            ["cols"] = new Ability { n = "군락지 수", per = 0.01f, pct = true },
        };
        public static readonly float[] AB_TIER = { 1, 1.5f, 2.5f, 4 };

        static readonly Dictionary<string, string> SP_AB = new Dictionary<string, string>
        {
            ["양송이"] = "harvest", ["느타리"] = "price", ["표고"] = "score", ["팽이버섯"] = "regen", ["목이버섯"] = "cols", ["새송이버섯"] = "harvest", ["먹물버섯"] = "golden", ["말불버섯"] = "price",
            ["꾀꼬리버섯"] = "rare", ["덕다리버섯"] = "score", ["큰갓버섯"] = "cols", ["민자주방망이버섯"] = "golden", ["기와버섯"] = "regen", ["만가닥버섯"] = "harvest",
            ["곰보버섯"] = "price", ["달걀버섯"] = "rare", ["소혀버섯"] = "score", ["능이버섯"] = "harvest", ["송이버섯"] = "price", ["송로버섯"] = "golden",
            ["싸리버섯"] = "harvest", ["달빛표고"] = "golden", ["주름버섯"] = "price", ["바다송이"] = "rare", ["벽돌먹물버섯"] = "cols",
            ["구름버섯"] = "bar", ["치마버섯"] = "combo", ["간버섯"] = "skill", ["흰목이"] = "dur", ["잎새버섯"] = "spd", ["한입버섯"] = "bar", ["복령"] = "combo", ["저령"] = "skill",
            ["꽃송이버섯"] = "dur", ["노루궁뎅이"] = "skill", ["말굽버섯"] = "bar", ["잔나비걸상버섯"] = "combo", ["목도리방귀버섯"] = "spd", ["소나무잔나비버섯"] = "dur",
            ["영지버섯"] = "skill", ["상황버섯"] = "dur", ["차가버섯"] = "combo", ["말똥진흙버섯"] = "bar", ["동충하초"] = "skill", ["눈꽃동충하초"] = "dur",
            ["자작나무버섯"] = "bar", ["달빛동충하초"] = "skill", ["선녀낙엽버섯"] = "combo", ["산호버섯"] = "dur", ["녹슨영지"] = "skill",
            ["무당버섯"] = "atk", ["노란다발"] = "crit", ["노란각시버섯"] = "size", ["맑은애주름버섯"] = "skillDmg", ["이끼꽃버섯"] = "atk", ["비단빛깔때기버섯"] = "spd", ["땀버섯"] = "crit", ["갈황색미치광이버섯"] = "skillDmg",
            ["광대버섯"] = "atk", ["뱀껍질광대버섯"] = "crit", ["붉은싸리버섯"] = "size", ["알광대버섯"] = "skillDmg", ["마귀곰보버섯"] = "atk", ["독깔때기버섯"] = "crit",
            ["화경버섯"] = "skillDmg", ["개나리광대버섯"] = "atk", ["마귀광대버섯"] = "size", ["독우산광대버섯"] = "crit", ["붉은사슴뿔버섯"] = "atk", ["갈색고리갓버섯"] = "skillDmg",
            ["흰가시광대버섯"] = "atk", ["달빛애주름버섯"] = "crit", ["말똥버섯"] = "size", ["해파리버섯"] = "skillDmg", ["콘크리트광대버섯"] = "atk",
        };

        public static float AbPerStar(Species sp) => AB[sp.ab].per * AB_TIER[sp.t];

        public static string AbFmt(string key, double v)
        {
            var a = AB[key];
            if (a.unit != null) return $"+{U.FmtN(v, 2)}{a.unit}";
            return $"+{U.FmtN(v * 100, a.pp ? 2 : 1)}{(a.pp ? "%p" : "%")}";
        }

        public static readonly int[] STAR_N = { 10, 50, 200, 800, 3000 };

        // ===== 특수 버섯 =====
        public class Special { public string id, n, c1, c2, perk; }
        public static readonly Special[] SPECIALS =
        {
            new Special { id = "fire", n = "불씨 꼬마", c1 = "#ff7a2a", c2 = "#ffd34a", perk = "화염 이펙트: 수확기가 불꽃을 두르고 버섯 피해 +30%" },
            new Special { id = "spark", n = "찌릿 꼬마", c1 = "#ffe03a", c2 = "#fff7b0", perk = "찌릿 번개: 버섯을 칠 때 6% 확률로 연쇄 번개" },
            new Special { id = "dew", n = "이슬 꼬마", c1 = "#5fc8ff", c2 = "#d8f4ff", perk = "이슬 시계: 라운드 제한시간 +2초" },
            new Special { id = "coin", n = "동전 꼬마", c1 = "#f5c518", c2 = "#fff2a8", perk = "동전 주머니: 버섯 판매가 +15%" },
            new Special { id = "star", n = "별똥 꼬마", c1 = "#b07bff", c2 = "#f2e2ff", perk = "별똥 행운: 에픽 이상 등장 가중치 +25%" },
            new Special { id = "leaf", n = "새싹 꼬마", c1 = "#5fc85a", c2 = "#d8f8c0", perk = "새싹 기운: 군락지 수 +15%" },
            new Special { id = "moon", n = "달빛 꼬마", c1 = "#3a4a8a", c2 = "#f6f0b0", perk = "달빛 축복: 황금 버섯 확률 ×1.5" },
            new Special { id = "wind", n = "바람 꼬마", c1 = "#5fd8c0", c2 = "#e8fff8", perk = "순풍: 핀볼 속도 +10%" },
            new Special { id = "rock", n = "조약돌 꼬마", c1 = "#8a8a86", c2 = "#c8c6bc", perk = "든든한 바: 슬라이드 바 길이 +12%" },
            new Special { id = "chest", n = "꿀밤 꼬마", c1 = "#a0602a", c2 = "#f0c890", perk = "꿀밤 주머니: 버섯 수확량 +15%" },
        };
        public static readonly Dictionary<string, Special> SPC = SPECIALS.ToDictionary(s => s.id);
        public const float SPECIAL_P = 0.3f, SPECIAL_LIFE = 10;

        // ===== 세금 =====
        public static class TAX
        {
            public const int every = 3;
            public const float @base = 100, growth = 1.5f, share = 0.35f, rate0 = 0.2f, rateStep = 0.1f, rateMax = 0.5f;
        }
        public static double TaxAmount(int cycle) => Math.Ceiling(TAX.@base * Math.Pow(TAX.growth, cycle - 1) / 10) * 10;
        public static double TaxRate(int unpaid) => U.JsRound(Math.Min(TAX.rateMax, TAX.rate0 + TAX.rateStep * unpaid) * 100) / 100;

        public static readonly Dictionary<string, string> TRAIT_TEXT = new Dictionary<string, string>
        {
            ["jelly"] = "젤리(통과): 튕기지 않고 통과하며 여러 개를 한 번에 긁는다",
            ["slow"] = "포자 뿜기: 맞으면 확률로 포자 구름(느림)",
            ["weak"] = "포자 뿜기: 맞으면 확률로 포자 구름(약화)",
            ["both"] = "포자 뿜기: 맞으면 확률로 포자 구름(느림+약화)",
            ["wander"] = "떠돌이: 군락지가 필드를 돌아다닌다 (점수 ×1.5)",
        };

        public static readonly Dictionary<string, string> RECORD_NAMES = new Dictionary<string, string>
        {
            ["score"] = "한 라운드 최고 점수", ["combo"] = "최장 콤보 (연속 수확)", ["harvest"] = "한 라운드 최다 수확", ["balls"] = "최대 동시 핀볼 수", ["chain"] = "충격파 최장 연쇄",
        };
        public static readonly string[] RECORD_KEYS = { "score", "combo", "harvest", "balls", "chain" };

        static Defs()
        {
            var idx = new Dictionary<string, int> { ["ed"] = 0, ["md"] = 0, ["ps"] = 0 };
            foreach (var a in SPECIES_RAW)
            {
                var s = new Species { c = a[0], n = a[1], t = int.Parse(a[2]), sh = a[3], c1 = a[4], c2 = a[5], pat = a[6], d = a[8] };
                foreach (var opt in a[7].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = opt.Split(':');
                    string k = kv[0], v = kv.Length > 1 ? kv[1] : null;
                    switch (k)
                    {
                        case "init": s.init = true; break;
                        case "after": s.after = v; break;
                        case "cnt": s.cnt = int.Parse(v); break;
                        case "tax": s.tax = int.Parse(v); break;
                        case "theme": s.theme = v; break;
                        case "spore": s.spore = v; break;
                        case "jelly": s.jelly = true; break;
                        case "wander": s.wander = true; break;
                        case "glow": s.glow = true; break;
                        case "thick": s.thick = true; break;
                        case "tall": s.tall = true; break;
                        case "small": s.small = true; break;
                        case "big": s.big = true; break;
                        case "leaf": s.leaf = true; break;
                        case "ring": s.ring = true; break;
                        case "horn": s.horn = true; break;
                    }
                }
                s.idx = idx[s.c]++;
                s.id = s.c + s.idx;
                s.trait = s.jelly ? "jelly" : s.spore != null ? "spore" : s.wander ? "wander" : null;
                SPECIES.Add(s); SP[s.id] = s; SP_BY_NAME[s.n] = s;
            }
            foreach (var s in SPECIES)
            {
                if (s.after != null) s.afterId = SP_BY_NAME[s.after].id;
                s.ab = SP_AB[s.n];
            }
            InitSkillsAndNodes();
            InitWorld();
            LoadPreset();
        }
    }
}
