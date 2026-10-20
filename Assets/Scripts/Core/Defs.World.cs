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
            public string id, icon;
            public string nKo, dKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string d { get => Loc.T(dKo); set => dKo = value; }
            public float favMul = 1;
            public bool good;
            public int favTier = -1;
            public double rate = 1;   // 같은 유형 안에서 뽑힐 가중치 (DB Wea_Rate)
            public string[] fav;
            public HashSet<string> favIds = new HashSet<string>();
        }
        // 전투 기획서 Ver05(2026-10-12 회의): 날씨를 6종으로 줄였다. 일반 채집 = 좋은 날씨 3종, 보스 채집 = 나쁜 날씨 3종, 같은 확률.
        // (가을 낙엽·천둥번개·무지개·짙은 안개·가뭄 삭제, 해금 조건 없음)
        // DB Weader 시트의 행 순서. Weader_Type 1 = 일반 채집(좋은 날씨), 0 = 보스 채집(나쁜 날씨). 설명은 String 시트 wea:<키>:desc
        public static readonly List<Weather> WEATHERS = new List<Weather>();
        public static readonly Dictionary<string, Weather> WEATHER = new Dictionary<string, Weather>();
        public const string WEATHER_GOOD = "#5fb8ff", WEATHER_BAD = "#ff5a4a";

        // ===== 지역 (기본 트리 2시간 기준: 숲 1 · 밤 20 · 들판 45 · 바다 75 · 폐허 105스테이지부터) =====
        public class Theme
        {
            public string id, icon;
            public string nKo, dKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string d { get => Loc.T(dKo); set => dKo = value; }
            public int from;
            public string[] fav;
            public HashSet<string> favIds = new HashSet<string>();
        }
        public static readonly List<Theme> THEMES = new List<Theme>
        {
            new Theme { id = "forest", n = "숲", icon = "t_forest", from = 1, d = "버섯 사냥의 시작. 축축한 숲 바닥", fav = new[] { "표고", "느타리", "구름버섯", "잔나비걸상버섯", "광대버섯", "노란다발" } },
            new Theme { id = "night", n = "달빛 밤", icon = "t_night", from = 20, d = "달빛 아래 발광 버섯이 빛나는 밤의 숲. 필드가 조금 어둡다", fav = new[] { "화경버섯", "독우산광대버섯", "송이버섯", "동충하초", "먹물버섯", "갈색고리갓버섯" } },
            new Theme { id = "field", n = "들판", icon = "t_field", from = 45, d = "바람 부는 너른 들판. 꽃과 풀 사이에 버섯 고리", fav = new[] { "양송이", "말불버섯", "큰갓버섯", "목도리방귀버섯", "노란각시버섯", "땀버섯" } },
            new Theme { id = "sea", n = "바다", icon = "t_sea", from = 75, d = "버섯이 해변 모래사장까지 번졌다", fav = new[] { "목이버섯", "흰목이", "이끼꽃버섯", "붉은싸리버섯", "소혀버섯", "꽃송이버섯" } },
            new Theme { id = "ruins", n = "버섯 폐허 도시", icon = "t_ruins", from = 105, d = "거대 버섯이 집어삼킨 마을. 이야기의 끝", fav = new[] { "먹물버섯", "갈황색미치광이버섯", "말굽버섯", "상황버섯", "마귀광대버섯", "송로버섯" } },
        };
        public static readonly Dictionary<string, Theme> THEME = new Dictionary<string, Theme>();
        public const float THEME_FAV_MUL = 3;

        // 버섯 체력·보상(점수·수확 개수) 배율: 지역마다 ×10, 스테이지마다 ×1.01.
        // 공격력·타격 점수 단계 노드(+1/+10/+100/+1000)와 단위를 맞춰, 새 지역에 갈 때마다 숫자가 한 자리씩 뛴다.
        public static readonly double ZONE_STEP = Db.CPos("ZONE_STEP"), STAGE_GROWTH = Db.CPos("STAGE_GROWTH");
        public static int ThemeIndex(string id) => Math.Max(0, THEMES.FindIndex(t => t.id == id));
        // 실험(구조안 S1, 사용자 결정 전): 0이 아니면 새 지역의 ×10을 해금 뒤 ZONE_RAMP판에 나눠 올린다(판마다 ×10^(1/ZONE_RAMP)).
        // 기본 0 = 해금 즉시 ×10(지금 게임). 밸런스 시뮬에서만 명령줄 -ramp 10으로 켠다
        public static int ZONE_RAMP = 0;
        // 시뮬 전용 실험(콤보 상한 원인 측정): true면 버섯 체력 배율(지역·스테이지·초반)을 1로 고정한다. 기본 false = 지금 게임. BalanceComboProbe에서만 켠다
        public static bool SIM_HP1 = false;
        // stage = 콘텐츠 스테이지 (Defs.ContentStage). 튜토리얼 4판은 1스테이지 배율, 지역 from도 표시 스테이지 기준 (5회차 P-2)
        public static double ZoneMul(string themeId, int stage)
        {
            int i = ThemeIndex(themeId);
            double e = i;
            if (ZONE_RAMP > 0 && i >= 1) e = i - 1 + Math.Max(0, Math.Min(1, (stage - THEMES[i].from + 1) / (double)ZONE_RAMP));
            return Math.Pow(ZONE_STEP, e) * Math.Pow(STAGE_GROWTH, stage - 1);
        }

        // ===== 숲 장치 =====
        // 라운드 시작 때만 놓인다. 약용 '바 넓이' 다음 노드로 종류별로 해금하고, 해금한 장치는 라운드마다 1개씩 반드시 놓인다
        public static readonly string[] DEVICE_TYPES = { "stump", "moss", "acorn" };
        public static readonly LocMap DEVICE_NAME = new LocMap(new Dictionary<string, string>
        {
            ["stump"] = "그루터기 범퍼", ["moss"] = "이끼 길", ["acorn"] = "도토리 스위치",
        });
        // 그루터기에 튕긴 수확기: 공격력 ×(1 + (STUMP_ATK_MUL-1)×숲 장치 강화), STUMP_ATK_DUR초 (다시 맞으면 시간만 갱신)
        public static readonly float STUMP_ATK_MUL = (float)Db.C("STUMP_ATK_MUL"), STUMP_ATK_DUR = (float)Db.C("STUMP_ATK_DUR");   // 전투 기획서 Ver05: 예시 5초
        // 그루터기에 부딪힐 때 얻는 점수 = 기본 1타 점수 × 이 값 (밸런스 3회차: 5 → 3, 구매 하나로 판 수입 ×2 넘지 않게)
        public const double STUMP_SCORE = 3;

        // ===== 도감 세트 =====
        public class CodexSet
        {
            public string id, cat; public string[] m; public List<string> ids;
            public string nKo, dKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string d { get => Loc.T(dKo); set => dKo = value; }
        }
        public static readonly List<CodexSet> SETS = new List<CodexSet>
        {
            new CodexSet { id = "amanita", n = "광대버섯 가문", m = new[] { "광대버섯", "뱀껍질광대버섯", "알광대버섯", "개나리광대버섯", "독우산광대버섯", "마귀광대버섯" }, d = "독버섯 수확량 +30%" },
            new CodexSet { id = "shelf", n = "선반 버섯", m = new[] { "덕다리버섯", "소혀버섯", "간버섯", "말굽버섯", "잔나비걸상버섯", "상황버섯", "소나무잔나비버섯", "말똥진흙버섯" }, d = "슬라이드 바 길이 +40" },
            new CodexSet { id = "cordy", n = "동충하초 형제", m = new[] { "동충하초", "눈꽃동충하초" }, d = "떠돌이 점수 +50%" },
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
        public static readonly float COMBO_WINDOW = (float)Db.CPos("COMBO_WINDOW");
        public static readonly (int at, string n, float mul)[] FEVER = { (25, "FEVER!", 1.5f), (70, "SUPER FEVER!!", 2f) };
        public const int FLAME_STEP = 10000;
        public class Flame
        {
            public string icon, text, @out; public UnityEngine.Color core, mid, edge;
            public string nKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
        }
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
            foreach (var r in Db.Data.Weader)
            {
                if (r.Weader_Type != 0 && r.Weader_Type != 1) throw new InvalidOperationException($"[DB] Weader {r.Wea_Key}: Weader_Type은 1(일반) 또는 0(보스) ({r.Weader_Type})");
                if (r.Wea_Rate < 0) throw new InvalidOperationException($"[DB] Weader {r.Wea_Key}: Wea_Rate는 0 이상 ({r.Wea_Rate})");
                WEATHERS.Add(new Weather
                {
                    id = r.Wea_Key, nKo = r.Wea_Name, dKo = Db.Text($"wea:{r.Wea_Key}:desc"), icon = r.Icon, good = r.Weader_Type == 1, rate = r.Wea_Rate,
                    favMul = (float)r.Fav_Mul, favTier = r.Fav_Tier, fav = string.IsNullOrEmpty(r.Fav) ? null : r.Fav.Split(',').Select(x => x.Trim()).ToArray(),
                });
            }
            foreach (var w in WEATHERS)
            {
                WEATHER[w.id] = w;
                if (w.fav == null) continue;
                foreach (var n in w.fav)
                    w.favIds.Add(SP_BY_NAME.TryGetValue(n, out var sp) ? sp.id : throw new InvalidOperationException($"[DB] Weader {w.id}: 도감에 없는 선호 버섯 '{n}'"));
            }
            foreach (var t in THEMES) { THEME[t.id] = t; foreach (var n in t.fav) t.favIds.Add(SP_BY_NAME[n].id); }
            foreach (var s in SETS) s.ids = s.cat != null ? SPECIES.Where(x => x.c == s.cat).Select(x => x.id).ToList() : s.m.Select(n => SP_BY_NAME[n].id).ToList();
            GOLDEN_STEPS = new[] { new[] { 5 }, new[] { 15 }, new[] { SP_TOTAL } };
        }
    }
}
