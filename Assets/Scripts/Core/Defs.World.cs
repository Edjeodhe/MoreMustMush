using System;
using System.Collections.Generic;
using System.Linq;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 날씨 =====
        public class Weather
        {
            public string id, n, icon, d;
            public float p, favMul = 1;
            public bool good;
            public int need = 0, favTier = -1;
            public string[] fav;
            public HashSet<string> favIds = new HashSet<string>();
        }
        public static readonly List<Weather> WEATHERS = new List<Weather>
        {
            new Weather { id = "clear", n = "맑음", icon = "w_clear", p = 34, good = true, d = "변화 없음" },
            new Weather { id = "rain", n = "보슬비", icon = "w_rain", p = 10, good = true, d = "군락 재생 속도 ×2, 군락지 +20%", fav = new[] { "먹물버섯", "이끼꽃버섯", "목이버섯", "흰목이" }, favMul = 3 },
            new Weather { id = "moon", n = "보름달 밤", icon = "w_moon", p = 7, good = true, d = "필드가 어두워지고 발광 버섯이 빛남, 황금 변종 ×3", fav = new[] { "화경버섯", "독우산광대버섯", "송이버섯", "송로버섯", "갈색고리갓버섯" }, favMul = 4 },
            new Weather { id = "autumn", n = "가을 낙엽", icon = "w_autumn", p = 8, good = true, d = "떠돌이 군락 30%, 떠돌이 점수 ×2", fav = new[] { "동충하초", "눈꽃동충하초", "꽃송이버섯", "잎새버섯", "능이버섯" }, favMul = 3 },
            new Weather { id = "thunder", n = "천둥번개", icon = "w_thunder", p = 7, good = true, d = "연쇄 번개 확률 ×2, 가끔 필드에 번개", fav = new[] { "차가버섯", "영지버섯", "붉은사슴뿔버섯" }, favMul = 3 },
            new Weather { id = "rainbow", n = "무지개", icon = "w_rainbow", p = 2, good = true, d = "거대 버섯 확정 등장, 점수 골드 +50%", favTier = 3, favMul = 3, need = 40 },
            new Weather { id = "fog", n = "짙은 안개", icon = "w_fog", p = 8, good = false, d = "시야가 흐려져 핀볼 주변만 보인다" },
            new Weather { id = "wind", n = "포자 바람", icon = "w_wind", p = 8, good = false, d = "버섯이 포자를 2배 자주 뿜고, 느림·약화 디버프가 2배 오래 간다", fav = new[] { "말불버섯", "목도리방귀버섯", "광대버섯", "뱀껍질광대버섯", "알광대버섯", "개나리광대버섯", "독우산광대버섯", "마귀광대버섯" }, favMul = 3 },
            new Weather { id = "storm", n = "폭우", icon = "w_storm", p = 6, good = false, d = "핀볼 속도 ×0.8, 군락 재생이 2배 느려짐" },
            new Weather { id = "cold", n = "한파", icon = "w_cold", p = 6, good = false, d = "바 길이 ×0.75, 제한시간 −3초" },
            new Weather { id = "drought", n = "가뭄", icon = "w_drought", p = 4, good = false, d = "군락지 수 ×0.7" },
        };
        public static readonly Dictionary<string, Weather> WEATHER = new Dictionary<string, Weather>();
        public const string WEATHER_GOOD = "#5fb8ff", WEATHER_BAD = "#ff5a4a";

        // ===== 지역 (10스테이지마다) =====
        public class Theme
        {
            public string id, n, icon, d;
            public int from;
            public string[] fav;
            public HashSet<string> favIds = new HashSet<string>();
        }
        public static readonly List<Theme> THEMES = new List<Theme>
        {
            new Theme { id = "forest", n = "숲", icon = "t_forest", from = 1, d = "버섯 사냥의 시작. 축축한 숲 바닥", fav = new[] { "표고", "느타리", "구름버섯", "잔나비걸상버섯", "광대버섯", "노란다발" } },
            new Theme { id = "night", n = "달빛 밤", icon = "t_night", from = 11, d = "달빛 아래 발광 버섯이 빛나는 밤의 숲. 필드가 조금 어둡다", fav = new[] { "화경버섯", "독우산광대버섯", "송이버섯", "동충하초", "먹물버섯", "갈색고리갓버섯" } },
            new Theme { id = "field", n = "들판", icon = "t_field", from = 21, d = "바람 부는 너른 들판. 꽃과 풀 사이에 버섯 고리", fav = new[] { "양송이", "말불버섯", "큰갓버섯", "목도리방귀버섯", "노란각시버섯", "땀버섯" } },
            new Theme { id = "sea", n = "바다", icon = "t_sea", from = 31, d = "버섯이 해변 모래사장까지 번졌다", fav = new[] { "목이버섯", "흰목이", "이끼꽃버섯", "붉은싸리버섯", "소혀버섯", "꽃송이버섯" } },
            new Theme { id = "ruins", n = "버섯 폐허 도시", icon = "t_ruins", from = 41, d = "거대 버섯이 집어삼킨 마을. 이야기의 끝", fav = new[] { "먹물버섯", "갈황색미치광이버섯", "말굽버섯", "상황버섯", "마귀광대버섯", "송로버섯" } },
        };
        public static readonly Dictionary<string, Theme> THEME = new Dictionary<string, Theme>();
        public const float THEME_FAV_MUL = 3;

        // ===== 버섯 요리 (2단계에서 만들 수 있게 됨; 능력치 계산은 지금부터 반영) =====
        public class Recipe
        {
            public string id, cat, n, icon;
            public Dictionary<string, int> need;
            public Func<double, string> eff;
            public Action<RoundStats, double> apply;
        }
        public static readonly List<Recipe> RECIPES = new List<Recipe>
        {
            new Recipe { id = "soup", cat = "ed", n = "버섯 수프", icon = "dish_soup", need = new Dictionary<string, int> { ["ed"] = 30, ["spore"] = 2 }, eff = k => $"수확량 ×{U.FmtN(1 + 0.5 * k)}", apply = (st, k) => st.harvestMul *= 1 + 0.5 * k },
            new Recipe { id = "songi", cat = "ed", n = "송이 덮밥", icon = "dish_songi", need = new Dictionary<string, int> { ["ed"] = 50, ["spore"] = 5 }, eff = k => $"황금 변종 확률 ×{U.FmtN(1 + 2 * k)}", apply = (st, k) => st.golden *= 1 + 2 * k },
            new Recipe { id = "tea", cat = "md", n = "약초 차", icon = "dish_tea", need = new Dictionary<string, int> { ["md"] = 30, ["spore"] = 2 }, eff = k => $"제한시간 +{U.FmtN(5 * k, 1)}초", apply = (st, k) => st.duration += 5 * k },
            new Recipe { id = "reishi", cat = "md", n = "영지 보약", icon = "dish_reishi", need = new Dictionary<string, int> { ["md"] = 50, ["spore"] = 5 }, eff = k => $"모든 스킬 발동률 ×{U.FmtN(1 + 0.5 * k)}", apply = (st, k) => { foreach (var s in st.sk.Values) s.p *= 1 + 0.5 * k; } },
            new Recipe { id = "cure", cat = "md", n = "해독 약초탕", icon = "dish_cure", need = new Dictionary<string, int> { ["md"] = 40, ["spore"] = 3 }, eff = k => "포자 디버프(느림·약화)에 걸리지 않음", apply = (st, k) => st.debuffDur = 0 },
            new Recipe { id = "venom", cat = "ps", n = "독버섯 꼬치", icon = "dish_venom", need = new Dictionary<string, int> { ["ps"] = 40, ["spore"] = 3 }, eff = k => $"공격력 ×{U.FmtN(1 + 0.4 * k)} · 치명타 +{U.JsRound(5 * k)}%p", apply = (st, k) => { st.atk *= 1 + 0.4 * k; st.crit += 0.05 * k; } },
            new Recipe { id = "fog", cat = "ps", n = "독안개 스튜", icon = "dish_fog", need = new Dictionary<string, int> { ["ps"] = 50, ["spore"] = 4 }, eff = k => $"필드 버섯 체력 −{U.JsRound(100 * (1 - 1 / (1 + 0.4 * k)))}%", apply = (st, k) => st.shroomHp /= 1 + 0.4 * k },
            new Recipe { id = "hotpot", cat = "mix", n = "삼색 버섯전골", icon = "dish_hotpot", need = new Dictionary<string, int> { ["ed"] = 20, ["md"] = 20, ["ps"] = 20, ["spore"] = 4, ["gem"] = 2 }, eff = k => $"점수 ×{U.FmtN(1 + k)}", apply = (st, k) => st.scoreMul *= 1 + k },
            new Recipe { id = "jap", cat = "mix", n = "목이 잡채", icon = "dish_jap", need = new Dictionary<string, int> { ["ed"] = 25, ["md"] = 15, ["spore"] = 3, ["gem"] = 3 }, eff = k => "영구 핀볼 +1", apply = (st, k) => st.permBalls += 1 },
            new Recipe { id = "truffle", cat = "mix", n = "송로 리조또", icon = "dish_truffle", need = new Dictionary<string, int> { ["ed"] = 30, ["ps"] = 30, ["spore"] = 6, ["gem"] = 5 }, eff = k => $"에픽 이상 출현 ×{U.FmtN(1 + k)} · 거대 버섯 확정", apply = (st, k) => { st.rareMul *= 1 + k; st.giantP = 1; } },
        };
        public static readonly Dictionary<string, Recipe> RECIPE = new Dictionary<string, Recipe>();

        // ===== 숲 장치 =====
        public static readonly string[] DEVICE_TYPES = { "stump", "moss", "mole", "stream", "acorn" };
        public static readonly Dictionary<string, string> DEVICE_NAME = new Dictionary<string, string>
        {
            ["stump"] = "그루터기 범퍼", ["moss"] = "이끼 길", ["mole"] = "두더지 굴", ["stream"] = "개울", ["acorn"] = "도토리 스위치",
        };

        // ===== 도감 세트 =====
        public class CodexSet { public string id, n, d, cat; public string[] m; public List<string> ids; }
        public static readonly List<CodexSet> SETS = new List<CodexSet>
        {
            new CodexSet { id = "amanita", n = "광대버섯 가문", m = new[] { "광대버섯", "뱀껍질광대버섯", "알광대버섯", "개나리광대버섯", "독우산광대버섯", "마귀광대버섯" }, d = "독버섯 수확량 +30%" },
            new CodexSet { id = "shelf", n = "선반 버섯", m = new[] { "덕다리버섯", "소혀버섯", "간버섯", "말굽버섯", "잔나비걸상버섯", "상황버섯", "소나무잔나비버섯", "말똥진흙버섯" }, d = "슬라이드 바 길이 +40" },
            new CodexSet { id = "cordy", n = "동충하초 형제", m = new[] { "동충하초", "눈꽃동충하초" }, d = "떠돌이 점수 +50%" },
            new CodexSet { id = "tax", n = "성실 납세자", m = new[] { "꾀꼬리버섯", "노루궁뎅이", "광대버섯", "달걀버섯", "영지버섯", "화경버섯" }, d = "세금 −20%" },
            new CodexSet { id = "jelly", n = "젤리 친구들", m = new[] { "목이버섯", "흰목이", "이끼꽃버섯" }, d = "젤리 통과 감속 완화 (×0.55 → ×0.75)" },
            new CodexSet { id = "fart", n = "방구와 연기", m = new[] { "말불버섯", "목도리방귀버섯" }, d = "포자 폭발 반경 +30" },
            new CodexSet { id = "glow", n = "빛나는 숲", m = new[] { "화경버섯", "독우산광대버섯", "송이버섯", "동충하초" }, d = "황금 변종 확률 +1%p" },
            new CodexSet { id = "coral", n = "산호와 뿔", m = new[] { "붉은싸리버섯", "붉은사슴뿔버섯" }, d = "치명타 확률 +5%p" },
            new CodexSet { id = "cook", n = "버섯 요리사", cat = "ed", d = "타격 점수 +50%" },
            new CodexSet { id = "herb", n = "약초꾼", cat = "md", d = "스킬 피해 +25%" },
            new CodexSet { id = "poison", n = "독 연구가", cat = "ps", d = "공격력 +3" },
        };
        public static int[][] GOLDEN_STEPS;
        public static double[] GOLDEN_STEP_BONUS = { 0.10, 0.25, 0.60 };

        // ===== 콤보·피버 =====
        public const float COMBO_WINDOW = 1.5f;
        public static readonly (int at, string n, float mul)[] FEVER = { (25, "FEVER!", 1.5f), (70, "SUPER FEVER!!", 2f) };
        public const int FLAME_STEP = 10000;
        public class Flame { public string n, icon, text, @out; public UnityEngine.Color core, mid, edge; }
        // generated burst icon tinted with the palette's text colour (the prototype used 🔥🔵🟣🟢⚪)
        const string FLAME_ICON = "<sprite name=\"s_burst\" tint=1>";
        public static readonly Flame[] FLAME =
        {
            new Flame { n = "노란 불꽃", icon = FLAME_ICON, core = U.Rgba(255, 190, 80, 1), mid = U.Rgba(235, 100, 25, 1), edge = U.Rgba(210, 80, 20, 1), text = "#ffe36e", @out = "#c2400a" },
            new Flame { n = "푸른 불꽃", icon = FLAME_ICON, core = U.Rgba(170, 225, 255, 1), mid = U.Rgba(60, 140, 255, 1), edge = U.Rgba(30, 90, 220, 1), text = "#bfe6ff", @out = "#1a4fb8" },
            new Flame { n = "보라 불꽃", icon = FLAME_ICON, core = U.Rgba(235, 180, 255, 1), mid = U.Rgba(160, 80, 240, 1), edge = U.Rgba(110, 40, 200, 1), text = "#ead0ff", @out = "#5a1a9a" },
            new Flame { n = "초록 불꽃", icon = FLAME_ICON, core = U.Rgba(200, 255, 180, 1), mid = U.Rgba(70, 210, 90, 1), edge = U.Rgba(30, 150, 60, 1), text = "#d0ffc0", @out = "#1a6a2a" },
            new Flame { n = "하얀 불꽃", icon = FLAME_ICON, core = U.Rgba(255, 255, 255, 1), mid = U.Rgba(215, 215, 240, 1), edge = U.Rgba(160, 160, 210, 1), text = "#ffffff", @out = "#4a4a6a" },
        };
        public static int FlameIdx(int c) => Math.Min(FLAME.Length - 1, c / FLAME_STEP);
        public static float FlameIntensity(int c)
        {
            int inTier = FlameIdx(c) < FLAME.Length - 1 ? c % FLAME_STEP : c - (FLAME.Length - 1) * FLAME_STEP;
            return 0.5f + 0.75f * U.Clamp((float)Math.Log10(1 + inTier) / 4f, 0, 1);
        }
        public static int FeverLv(int c) => c >= FEVER[1].at ? 2 : c >= FEVER[0].at ? 1 : 0;

        static void InitWorld()
        {
            foreach (var w in WEATHERS) { WEATHER[w.id] = w; if (w.fav != null) foreach (var n in w.fav) w.favIds.Add(SP_BY_NAME[n].id); }
            foreach (var t in THEMES) { THEME[t.id] = t; foreach (var n in t.fav) t.favIds.Add(SP_BY_NAME[n].id); }
            foreach (var r in RECIPES) RECIPE[r.id] = r;
            foreach (var s in SETS) s.ids = s.cat != null ? SPECIES.Where(x => x.c == s.cat).Select(x => x.id).ToList() : s.m.Select(n => SP_BY_NAME[n].id).ToList();
            GOLDEN_STEPS = new[] { new[] { 5 }, new[] { 15 }, new[] { SP_TOTAL } };
        }
    }
}
