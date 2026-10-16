using System.Collections.Generic;
using System.Linq;

namespace MoreMush
{
    // 꼬마버섯 세계수 (신규콘텐츠 기획서 Ver03 "농장 개편"): 성장 8단계 × 나이테, 나무버섯 4종 · 구역 4곳, 꼬마 8종의 자동 수확·성장·각성·호감도·부탁,
    // 꼬마 공방(장식 제작) · 장식 24종 · 세트 · 강화 · 아름다움 점수, 세계수의 가호. 수치는 기획서 임시안, 기획서에 없는 값은 기획서/세계수_구현설계.md.
    // 화면 좌표는 1920×1080 스테이지 픽셀.
    public static partial class Defs
    {
        // ===== 세계수 성장 (단계 1~8 · 나이테 Lv.1~10) =====
        public static class WT
        {
            public const int stages = 8, ringMax = 10;
            static readonly string[] STAGE_KO = { "새싹", "묘목", "어린 나무", "버섯 나무", "이끼 고목", "룬 신목", "고대수", "아르카나 세계수" };
            public static string StageName(int s) => Loc.T(STAGE_KO[System.Math.Max(1, System.Math.Min(stages, s)) - 1]);
            // 그 단계로 진화하는 데 드는 경험치·골드·균사석 (index = 단계 − 1, 1단계는 시작 단계)
            public static readonly double[] exp = { 0, 300, 1000, 3000, 8000, 20000, 50000, 120000 };
            public static readonly double[] evoGold = { 0, 2000, 6000, 15000, 40000, 100000, 250000, 600000 };
            public static readonly int[] evoGem = { 0, 0, 10, 30, 80, 200, 500, 1200 };
            public static readonly int[] mushSlots = { 4, 6, 8, 10, 13, 16, 20, 24 };   // 나무버섯 슬롯
            public static readonly int[] decoSlots = { 1, 2, 3, 4, 6, 8, 10, 12 };      // 장식 슬롯
            public const double ringK = 1.35, ringDiv = 20, lastRingMul = 2.4;
            // 나이테 Lv.L → L+1 골드: 다음 진화 골드 ÷ 20 × 1.35^(L−1) (마지막 단계는 7→8 진화 골드 × 2.4 기준)
            public static double RingCost(int stage, int lv) => (stage < stages ? evoGold[stage] : evoGold[stages - 1] * lastRingMul) / ringDiv * System.Math.Pow(ringK, lv - 1);
            public const double ringGrow = 0.03;                  // 나이테 레벨마다 나무버섯 성장 속도 +3%
            public static int Picture(int stage) => System.Math.Max(1, System.Math.Min(stages, stage)) - 1;   // tree_0~7 (단계마다 그림 하나)
            // 그림(단계)별 나무 너비 (px, 1920×1080 무대 기준). 단계마다 확 커지고 마지막 단계는 훨씬 크게 (2026-10-16)
            // 나무가 화각보다 빨리 커져서 꼭대기가 화면에서 단계마다 확실히 올라간다 (사용자: 위로 커지는 느낌):
            // 화면 아래에서 꼭대기까지 50→54→58→62→66→70→74%, 마지막 단계만 95%로 확 (키 ×1.44, 화각은 조금만 넓혀 상쇄하지 않는다)
            public static readonly float[] width = { 220, 246, 388, 569, 670, 1012, 1556, 2883 };
            // 단계별 카메라 화각 배율: 땅(화면 아래)은 고정하고 위·양옆으로 넓어진다. 겹친 큰 배경(FarmView.BG_LAYERS)이 3.8배까지 덮는다
            public static readonly float[] zoom = { 1f, 1.1f, 1.25f, 1.42f, 1.62f, 1.85f, 2.1f, 2.2f };
            public const float x = 820, y = 720;                  // 나무 밑동 (화면 좌표): 배경 빈터의 밝은 자리 (장식도 이 자리를 중심으로 놓였다)
            public const float camX = 960;                        // 화각이 넓어질 때 카메라 중심 x (무대 가운데, 큰 배경이 양옆을 덮는다)
            public const int order = 880;
            public const int craftBase = 2;                       // 동시 제작 슬롯 (4·6·8단계에 +1)
            public static int CraftSlots(int stage) => craftBase + (stage >= 4 ? 1 : 0) + (stage >= 6 ? 1 : 0) + (stage >= 8 ? 1 : 0);
            public const float rushSec = 600;                     // 즉시 완료: 남은 시간 10분마다 균사석 1
            public const int rushPerDay = 5;                      // 즉시 완료 하루 횟수
            public const int codexForCraft = 10;                  // 도감 10종을 모으면 꼬마 공방이 열린다 (도감 기획서 Ver03 진행 보상)
        }

        // 세계수의 가호: 진화한 단계까지 모두 쌓이는 라운드 버프. 8단계는 앞의 가호를 모두 ×1.5
        // key: harvest 버섯 획득량 · gold 라운드 골드 · colsN 군락지 수(개) · dur 제한시간(초) · skill 스킬 발동률 · goldenMul 황금 변종 확률 배율
        public class Blessing { public string key; public double v; public string textKo; public string text => Loc.T(textKo); }
        public static readonly Blessing[] BLESS =
        {
            new Blessing { key = "harvest", v = 0.03, textKo = "버섯 획득량 +3%" },
            new Blessing { key = "gold", v = 0.05, textKo = "라운드 골드 +5%" },
            new Blessing { key = "colsN", v = 1, textKo = "군락지 수 +1" },
            new Blessing { key = "harvest", v = 0.06, textKo = "버섯 획득량 +6%" },
            new Blessing { key = "dur", v = 2, textKo = "제한시간 +2초" },
            new Blessing { key = "skill", v = 0.05, textKo = "스킬 발동률 +5%" },
            new Blessing { key = "goldenMul", v = 1.3, textKo = "황금 변종 확률 ×1.3" },
            new Blessing { key = "all", v = 1.5, textKo = "모든 가호 효과 ×1.5" },
        };

        // 단계마다 나무가 원하는 버섯 3종 (공물 가치 ×2). 지역·해금 순서를 따라 고른다
        public static readonly string[][] WT_PREFER =
        {
            new[] { "양송이", "느타리", "무당버섯" }, new[] { "구름버섯", "치마버섯", "노란다발" }, new[] { "표고", "간버섯", "노란각시버섯" },
            new[] { "표고", "영지버섯", "화경버섯" }, new[] { "큰갓버섯", "말굽버섯", "광대버섯" }, new[] { "상황버섯", "노루궁뎅이", "달걀버섯" },
            new[] { "송이버섯", "동충하초", "독우산광대버섯" }, new[] { "송로버섯", "눈꽃동충하초", "갈색고리갓버섯" },
        };
        public const double TRIBUTE_PREFER = 2, GIFT_FAV = 3;
        public static readonly double[] TIER_VALUE = { 1, 3, 10, 40 };   // 공물·선물 가치 = 등급 판매 기본가

        // ===== 나무버섯 · 구역 =====
        // time 자라는 시간(초) · gold·gem 수확 1개 · from 등장 단계 · icon 그림(Farm/Tree/fruit_*) · tint 색
        public class TreeMush
        {
            public string id, icon, tint; public int tier, from, gem; public double gold; public float time;
            public string nKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
        }
        public static readonly TreeMush[] TREE_MUSH =
        {
            new TreeMush { id = "shelf", n = "선반 나무버섯", tier = 0, time = 60, gold = 50, from = 1, icon = "fruit_ed", tint = "#ffffff" },
            new TreeMush { id = "bundle", n = "다발 나무버섯", tier = 1, time = 300, gold = 200, from = 2, icon = "fruit_md", tint = "#ffffff" },
            new TreeMush { id = "glow", n = "발광 나무버섯", tier = 2, time = 1800, gold = 300, gem = 1, from = 4, icon = "fruit_ps", tint = "#b8f0ff" },
            new TreeMush { id = "golden", n = "황금 나무버섯", tier = 3, time = 10800, gold = 1000, gem = 5, from = 6, icon = "fruit_ed", tint = "#ffd23a" },
        };
        public static readonly Dictionary<string, TreeMush> TMUSH = TREE_MUSH.ToDictionary(m => m.id);
        public const double GOLDEN_IN_CROWN = 0.05;              // 수관 발광 자리에 황금 나무버섯이 돋을 확률

        // 구역: open 열리는 단계 · weight 나무버섯 슬롯을 나누는 비율 · mush 돋는 버섯(번갈아) · high 높은 구역(바람 꼬마)
        // 화면: cx 나무 가운데 기준 x 폭 비율, cy 밑동에서 위로 (나무 높이 비율) — 구역 안에 슬롯을 늘어놓는다
        public class Zone
        {
            public string id; public int open, weight; public string[] mush; public bool high; public float cx, cy, rx, ry;
            public string nKo, featKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string feat { get => Loc.T(featKo); set => featKo = value; }
        }
        public static readonly Zone[] ZONES =
        {
            new Zone { id = "root", n = "뿌리", open = 1, weight = 4, mush = new[] { "shelf" }, cx = 0, cy = 0.02f, rx = 0.62f, ry = 0.05f, feat = "선반 슬롯이 많고 이동 거리가 짧아요" },
            new Zone { id = "trunk", n = "줄기", open = 2, weight = 3, mush = new[] { "shelf", "bundle" }, cx = 0, cy = 0.28f, rx = 0.12f, ry = 0.16f, feat = "선반·다발이 고루 나는 균형형 구역" },
            new Zone { id = "branch", n = "가지", open = 4, weight = 3, mush = new[] { "bundle", "glow" }, high = true, cx = 0, cy = 0.6f, rx = 0.42f, ry = 0.12f, feat = "이동 시간이 길지만 가치가 높아요" },
            new Zone { id = "crown", n = "수관", open = 6, weight = 2, mush = new[] { "glow" }, high = true, cx = 0, cy = 0.86f, rx = 0.3f, ry = 0.08f, feat = "가장 귀한 버섯이 나는 최고 가치 구역" },
        };
        public static readonly Dictionary<string, Zone> ZONE = ZONES.ToDictionary(z => z.id);

        // ===== 꼬마 자동 수확 =====
        public static class HARV
        {
            public const float sec = 4;                           // 수확 1회 = 이동 + 수확 (초)
            public const double prefer = 0.5;                     // 선호 구역에 배치하면 수확량 +50%
            public const double perLv = 0.08;                     // 꼬마 레벨마다 수확량 +8%
            public static int ZoneCap(int stage) => stage >= 6 ? 3 : 2;   // 구역당 꼬마 수
            public const float bagHours = 8;                      // 수확 보따리 최대 보관 (시간)
            public const float bagMinClaim = 1;                   // 받을 수 있는 최소 금액(골드) — 빈 보따리는 받지 않는다
            public const float x0 = 300, x1 = 1300, y0 = 880, y1 = 1010;   // 쉬는 꼬마가 돌아다니는 땅 (화각 1배 기준, 넓어지면 나무 밑동 중심으로 같이 넓힌다)
            public const float halfW = (x1 - x0) / 2;
            public const float returnMin = 600;                   // 이만큼(초) 자리를 비웠다 돌아오면 보따리 창
        }

        // ===== 꼬마 8종 (동전·꿀밤은 2차) =====
        // zone 선호 구역 · trait 수확 특성 · bonus 라운드 특별 보너스(각성 ★·세트마다 한 단계) · fav 좋아하는 버섯
        // trait: speed 수확 속도+ · regen 이 꼬마가 있는 구역 재생+ · wind 이동 ×2·높은 구역 +50% · gem 균사석+ · carry 운반·보관+
        //        chain 연쇄 수확 · glow 발광 수확 ×2·균사석+ · aura 모든 꼬마 수확량+
        // bonus: cols 군락지 수% · dur 제한시간(초) · spd 핀볼 속도% · atk 공격력% · bar 슬라이드 바% · spark 연쇄 번개 %p · golden 황금 변종 확률 배율+ · skill 스킬 발동률%
        public class Kid
        {
            public string id, zone, trait, bonus; public double tv, tv2, bv; public string[] fav;
            public string traitKo, favWhereKo;
            public string traitText => Loc.T(traitKo);
            public string favWhere => Loc.T(favWhereKo);
        }
        public static readonly Kid[] KIDS =
        {
            new Kid { id = "leaf", zone = "root", trait = "speed", tv = 0.10, traitKo = "나무버섯 수확 속도 +10%", bonus = "cols", bv = 0.05, fav = new[] { "느타리", "표고" }, favWhereKo = "숲" },
            new Kid { id = "dew", zone = "trunk", trait = "regen", tv = 0.20, traitKo = "있는 구역의 버섯 재생 속도 +20%", bonus = "dur", bv = 1, fav = new[] { "목이버섯", "흰목이" }, favWhereKo = "바다 · 보슬비" },
            new Kid { id = "wind", zone = "crown", trait = "wind", tv = 2, tv2 = 0.5, traitKo = "이동 속도 ×2 · 높은 구역 수확 +50%", bonus = "spd", bv = 0.04, fav = new[] { "큰갓버섯", "말불버섯" }, favWhereKo = "들판" },
            new Kid { id = "fire", zone = "trunk", trait = "gem", tv = 0.20, traitKo = "수확할 때 균사석 +20%", bonus = "atk", bv = 0.05, fav = new[] { "영지버섯", "상황버섯" }, favWhereKo = "천둥번개 · 버섯 폐허 도시" },
            new Kid { id = "rock", zone = "root", trait = "carry", tv = 2, tv2 = 4, traitKo = "2개씩 운반 · 보따리 보관 +4시간", bonus = "bar", bv = 0.04, fav = new[] { "말굽버섯", "잔나비걸상버섯" }, favWhereKo = "숲 · 버섯 폐허 도시" },
            new Kid { id = "spark", zone = "branch", trait = "chain", tv = 2, traitKo = "수확할 때 주변 버섯 2개 연쇄 수확", bonus = "spark", bv = 0.02, fav = new[] { "차가버섯", "붉은사슴뿔버섯" }, favWhereKo = "천둥번개" },
            new Kid { id = "moon", zone = "branch", trait = "glow", tv = 2, tv2 = 0.30, traitKo = "발광 버섯 수확 ×2 · 균사석 +30%", bonus = "golden", bv = 0.2, fav = new[] { "화경버섯", "송이버섯" }, favWhereKo = "달빛 밤 · 보름달 밤" },
            new Kid { id = "star", zone = "crown", trait = "aura", tv = 0.15, traitKo = "오라: 모든 꼬마 수확량 +15%", bonus = "skill", bv = 0.05, fav = new[] { "송로버섯", "동충하초" }, favWhereKo = "달빛 밤 · 버섯 폐허 도시" },
        };
        public static readonly Dictionary<string, Kid> KID = KIDS.ToDictionary(k => k.id);
        public static string KidBonusText(Kid k, int step)
        {
            double v = k.bv * step;
            switch (k.bonus)
            {
                case "cols": return Loc.F("라운드 군락지 수 +{0}", U.Pct(v));
                case "dur": return Loc.F("라운드 제한시간 +{0}초", U.FmtN(v));
                case "spd": return Loc.F("라운드 핀볼 속도 +{0}", U.Pct(v));
                case "atk": return Loc.F("라운드 공격력 +{0}", U.Pct(v));
                case "bar": return Loc.F("라운드 슬라이드 바 길이 +{0}", U.Pct(v));
                case "spark": return Loc.F("라운드 연쇄 번개 확률 +{0}p", U.Pct(v));
                case "golden": return Loc.F("라운드 황금 변종 확률 ×{0}", U.FmtN(1 + v));
                default: return Loc.F("라운드 모든 스킬 발동률 +{0}", U.Pct(v));
            }
        }

        // 꼬마 레벨·각성
        public static class KLV
        {
            public const int max = 30;
            public static double Cost(int lv) => System.Math.Ceiling(100 * System.Math.Pow(1.25, lv - 1));   // Lv → Lv+1 골드
            public static int Cap(int awaken) => System.Math.Min(max, 10 * (awaken + 1));                      // 각성 전 Lv.10에서 멈춘다
            public static readonly double[] awakenMul = { 1, 1.3, 1.7, 2.2 };
            public static readonly double[] awakenGold = { 10000, 50000, 200000 };   // ★n → ★n+1
            public static readonly int[] awakenGem = { 20, 80, 250 };
            public const int awakenMax = 3;
            public static readonly int[] sumAt = { 50, 100, 150, 200, 240 };   // 꼬마 Lv 합계 보상: 장식 슬롯 +1 (100·200은 구역 정원 +1, 240은 칭호)
        }

        // 호감도 (창고 버섯 선물)
        public static class AFF
        {
            public const int max = 10;
            public static double Need(int lv) => 30 * lv;          // Lv → Lv+1
            public const double greatPerLv = 0.01;                // 수확 대성공(2배) 확률 +1%/Lv
            public const double placeGift = 30;                   // 그 꼬마의 장식을 처음 놓으면 호감도
        }

        // 일일 부탁 · 주간 상자
        public static class REQ
        {
            public const int perDay = 3, weekGoal = 7, weekGem = 30;
            public static readonly int[] count = { 20, 10, 5, 2 };   // 등급별 요구 개수
            public const double goldMul = 3, aff = 20;            // 보상: 골드 = 지금 단계 나이테 Lv.1 비용 × 3 · 호감도 +20
        }

        // ===== 꼬마 공방 · 장식 =====
        public class DecoGrade { public double gold; public int gem; public float hours; public double beauty; public string nKo; public string n => Loc.T(nKo); }
        public static readonly DecoGrade[] DECO_GRADES =
        {
            new DecoGrade { nKo = "일반", gold = 1000, gem = 2, hours = 1, beauty = 15 },
            new DecoGrade { nKo = "고급", gold = 5000, gem = 10, hours = 2, beauty = 30 },
            new DecoGrade { nKo = "희귀", gold = 20000, gem = 30, hours = 4, beauty = 60 },
        };
        public static readonly double[] DECO_STAR_MUL = { 1, 1.25, 1.5, 1.8, 2.2 };   // 강화 ★1~5 효과·아름다움 배율
        public const int DECO_STAR_MAX = 5;

        // fx: grow 나무버섯 성장 속도 · shelf 선반 수확량 · bagH 보따리 보관(시간) · self 주인 꼬마 수확량 · treeGold 세계수 골드 · dur 라운드 제한시간(초)
        //     move 꼬마 이동 속도 · spd 라운드 핀볼 속도 · pick 꼬마 수확 시간 − · atk 라운드 공격력 · crit 라운드 치명타 %p · carryRoot 뿌리 운반량 +
        //     bar 라운드 슬라이드 바 · gem 균사석 획득 · spark 라운드 연쇄 번개 %p · glowUp 발광 나무버섯 + · lucky 수확 2배 확률 · bagEff 보따리 수확 효율
        //     golden 라운드 황금 변종 %p · allKid 모든 꼬마 수확량 · beautyMul 아름다움 점수 배율 · skill 라운드 스킬 발동률
        // hang 걸이형(가지·수관 슬롯) / 설치형(뿌리 주변 슬롯) · art 그림 키(Farm/Props/<art>)
        public class Deco
        {
            public string id, of, fx, art; public int grade; public bool hang; public double v;
            public string nKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
        }
        public static readonly Deco[] DECOS =
        {
            new Deco { id = "leaf_lamp", of = "leaf", n = "잎사귀 등불", grade = 0, hang = true, fx = "grow", v = 0.05, art = "leaf_lamp" },
            new Deco { id = "leaf_crown", of = "leaf", n = "꽃 화관", grade = 1, hang = true, fx = "shelf", v = 0.15, art = "leaf_crown" },
            new Deco { id = "leaf_house", of = "leaf", n = "나무 새집", grade = 2, hang = true, fx = "bagH", v = 1, art = "leaf_house" },
            new Deco { id = "dew_chime", of = "dew", n = "물방울 풍경", grade = 0, hang = true, fx = "self", v = 0.10, art = "dew_chime" },
            new Deco { id = "dew_spring", of = "dew", n = "이끼 샘물", grade = 1, hang = false, fx = "treeGold", v = 0.08, art = "dew_spring" },
            new Deco { id = "dew_pond", of = "dew", n = "수련 연못", grade = 2, hang = false, fx = "dur", v = 1, art = "dew_pond" },
            new Deco { id = "wind_catcher", of = "wind", n = "깃털 드림캐처", grade = 0, hang = true, fx = "move", v = 0.10, art = "wind_catcher" },
            new Deco { id = "wind_mill", of = "wind", n = "바람개비", grade = 1, hang = false, fx = "spd", v = 0.03, art = "wind_mill" },
            new Deco { id = "wind_swing", of = "wind", n = "덩굴 그네", grade = 2, hang = true, fx = "pick", v = 0.10, art = "wind_swing" },
            new Deco { id = "fire_brazier", of = "fire", n = "돌 화로", grade = 0, hang = false, fx = "self", v = 0.10, art = "fire_brazier" },
            new Deco { id = "fire_lantern", of = "fire", n = "불꽃 등롱", grade = 1, hang = true, fx = "atk", v = 0.05, art = "fire_lantern" },
            new Deco { id = "fire_candle", of = "fire", n = "버섯 촛불", grade = 2, hang = false, fx = "crit", v = 0.02, art = "fire_candle" },
            new Deco { id = "rock_table", of = "rock", n = "버섯 돌탁자", grade = 0, hang = false, fx = "carryRoot", v = 1, art = "rock_table" },
            new Deco { id = "rock_moss", of = "rock", n = "이끼 바위", grade = 1, hang = false, fx = "bar", v = 0.03, art = "rock_moss" },
            new Deco { id = "rock_geode", of = "rock", n = "자수정 정동", grade = 2, hang = false, fx = "gem", v = 0.10, art = "rock_geode" },
            new Deco { id = "spark_orb", of = "spark", n = "피뢰침 오브", grade = 0, hang = false, fx = "spark", v = 0.02, art = "spark_orb" },
            new Deco { id = "spark_lamp", of = "spark", n = "황금 수정등", grade = 1, hang = false, fx = "glowUp", v = 0.10, art = "spark_lamp" },
            new Deco { id = "spark_bell", of = "spark", n = "놋쇠 종", grade = 2, hang = true, fx = "lucky", v = 0.05, art = "spark_bell" },
            new Deco { id = "moon_crescent", of = "moon", n = "초승달 장식", grade = 0, hang = true, fx = "self", v = 0.10, art = "moon_crescent" },
            new Deco { id = "moon_garland", of = "moon", n = "별빛 가랜드", grade = 1, hang = true, fx = "bagEff", v = 0.10, art = "moon_garland" },
            new Deco { id = "moon_owl", of = "moon", n = "부엉이 목각", grade = 2, hang = false, fx = "golden", v = 0.005, art = "moon_owl" },
            new Deco { id = "star_rune", of = "star", n = "룬 비석", grade = 0, hang = false, fx = "allKid", v = 0.05, art = "star_rune" },
            new Deco { id = "star_crown", of = "star", n = "꼬마 왕관", grade = 1, hang = true, fx = "beautyMul", v = 0.10, art = "star_crown" },
            new Deco { id = "star_orb", of = "star", n = "빛의 구슬", grade = 2, hang = true, fx = "skill", v = 0.03, art = "star_orb" },
        };
        public static readonly Dictionary<string, Deco> DECO = DECOS.ToDictionary(d => d.id);
        public const double DECO_SET_YIELD = 0.30;               // 세트(같은 꼬마 장식 3종 배치): 그 꼬마 수확량 +30% · 특별 보너스 +1단계
        public const int DECO_FIRST_GEM = 5;                      // 처음 배치하면 균사석

        public static string DecoFxText(Deco d, double mul = 1)
        {
            double v = d.v * mul;
            string kid = KID.TryGetValue(d.of, out _) && SPC.TryGetValue(d.of, out var sp) ? sp.n : "";
            switch (d.fx)
            {
                case "grow": return Loc.F("나무버섯 성장 속도 +{0}", U.Pct(v));
                case "shelf": return Loc.F("선반 나무버섯 수확량 +{0}", U.Pct(v));
                case "bagH": return Loc.F("보따리 보관 시간 +{0}시간", U.FmtN(v));
                case "self": return Loc.F("{0} 수확량 +{1}", kid, U.Pct(v));
                case "treeGold": return Loc.F("세계수 골드 획득 +{0}", U.Pct(v));
                case "dur": return Loc.F("라운드 제한시간 +{0}초", U.FmtN(v));
                case "move": return Loc.F("꼬마 이동 속도 +{0}", U.Pct(v));
                case "spd": return Loc.F("라운드 핀볼 속도 +{0}", U.Pct(v));
                case "pick": return Loc.F("꼬마 수확 시간 −{0}", U.Pct(v));
                case "atk": return Loc.F("라운드 공격력 +{0}", U.Pct(v));
                case "crit": return Loc.F("라운드 치명타 확률 +{0}p", U.Pct(v));
                case "carryRoot": return Loc.F("뿌리 구역 꼬마 운반량 +{0}", U.FmtN(v));
                case "bar": return Loc.F("라운드 슬라이드 바 길이 +{0}", U.Pct(v));
                case "gem": return Loc.F("균사석 획득 +{0}", U.Pct(v));
                case "spark": return Loc.F("라운드 연쇄 번개 확률 +{0}p", U.Pct(v));
                case "glowUp": return Loc.F("발광 나무버섯 수확 +{0}", U.Pct(v));
                case "lucky": return Loc.F("수확할 때 {0} 확률로 2배", U.Pct(v));
                case "bagEff": return Loc.F("보따리 수확 효율 +{0}", U.Pct(v));
                case "golden": return Loc.F("라운드 황금 변종 확률 +{0}p", U.Pct(v));
                case "allKid": return Loc.F("모든 꼬마 수확량 +{0}", U.Pct(v));
                case "beautyMul": return Loc.F("아름다움 점수 ×{0}", U.FmtN(1 + v));
                default: return Loc.F("라운드 모든 스킬 발동률 +{0}", U.Pct(v));
            }
        }

        // 아름다움 점수 (가진 장식 전체 합) 보상
        public class BeautyStep { public double at; public string key; public double v; public string textKo; public string text => Loc.T(textKo); }
        public static readonly BeautyStep[] BEAUTY =
        {
            new BeautyStep { at = 100, key = "gold", v = 0.05, textKo = "라운드 골드 +5%" },
            new BeautyStep { at = 300, key = "slots", v = 2, textKo = "나무버섯 슬롯 +2" },
            new BeautyStep { at = 700, key = "allKid", v = 0.10, textKo = "모든 꼬마 수확량 +10%" },
            new BeautyStep { at = 1200, key = "title", v = 0, textKo = "칭호 '숲의 장인'" },
            new BeautyStep { at = 2000, key = "decoMul", v = 1.1, textKo = "모든 장식 효과 ×1.1" },
        };

        // ===== 꼬마 대사 =====
        static readonly Dictionary<string, string[]> FARM_LINES_KO = new Dictionary<string, string[]>
        {
            ["fire"] = new[] { "앗 뜨거! 아니, 내가 뜨거운 거구나!", "불멍 할래? 내가 불이야!", "모닥불 옆이 제일 좋아~" },
            ["spark"] = new[] { "찌릿찌릿! 만지면 감전이야!", "번개 치는 날 태어났어!", "갓이 곤두서는 기분이야!" },
            ["dew"] = new[] { "아침 이슬 한 방울 마실래?", "촉촉한 게 최고야~", "비 오는 날이 제일 좋아!" },
            ["coin"] = new[] { "짤랑짤랑~ 부자가 될 거야!", "저금통에 넣어 줄래?", "반짝이는 건 다 좋아!" },
            ["star"] = new[] { "소원 빌었어? 나 별똥별이야!", "밤하늘이 그리워~", "반짝반짝 작은 별~" },
            ["leaf"] = new[] { "쑥쑥 자라는 중이야!", "햇볕 냄새 좋다~", "물 주면 더 클 수 있어!" },
            ["moon"] = new[] { "쉿, 낮에는 졸려...", "보름달이 뜨면 힘이 나!", "밤 산책 같이 갈래?" },
            ["wind"] = new[] { "휘이잉~ 날아갈 것 같아!", "바람 따라 어디든 갈래!", "포자를 멀리멀리 날려 줄게!" },
            ["rock"] = new[] { "...단단해.", "굴러가는 건 자신 있어!", "이끼가 간지러워." },
            ["chest"] = new[] { "꿀밤 맞을래? 농담이야!", "다람쥐가 자꾸 날 쫓아와!", "달콤한 냄새 나지?" },
            ["all"] = new[] { "할아버지, 오늘도 수확 잘했어요?", "여기 세계수 진짜 좋아!", "심심해~ 놀아 줘!", "포자 날린다~", "배고파... 버섯 수프 없나?", "헤헤, 간지러워!", "같이 산책할래?" },
            ["thanks"] = new[] { "고마워! 이거 받아!", "헤헤, 최고야!", "역시 할아버지야!", "기분 좋아~ 선물이야!" },
            ["field"] = new[] { "영차영차! 수확은 맡겨 줘!", "나무버섯 냄새 좋다~", "보따리에 담아 둘게!", "다 자라면 바로 따 줄게!" },
            ["idle"] = new[] { "일하고 싶어! 구역에 보내 줘!", "어디로 가면 돼?", "나도 버섯 딸 수 있어!" },
        };
        public static readonly LocLines FARM_LINES = new LocLines(FARM_LINES_KO);
        public static readonly Dictionary<string, float> FARM_ANIMS = new Dictionary<string, float>
        { ["jump"] = 0.9f, ["spin"] = 0.8f, ["dance"] = 1.6f, ["heart"] = 1.4f, ["sleep"] = 2.6f, ["surprise"] = 0.9f };

        // ===== 스킨 (균사석으로 산다) =====
        // 꼬마 스킨: 꼬마마다 1개. 갓을 스킨 갓(Critters/skin_<of>)으로 바꾸고 액세서리(Critters/Acc/<acc>)를 단다.
        public class Skin
        {
            public string id, of, acc, aura; public int price; public bool top, ch;
            public string nKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
        }
        public static readonly Skin[] CHAR_SKINS =
        {
            // Dream: each skin's accessory is painted onto its cap (skin_<id>) or body (body_<id>_skin) by
            // Tools/art/build_skin_acc.py, so no separate acc sprite is stacked on top any more
            new Skin { id = "sk_fire", of = "fire", n = "용암 꼬마", price = 30, ch = true },
            new Skin { id = "sk_spark", of = "spark", n = "네온 꼬마", price = 30, ch = true },
            new Skin { id = "sk_dew", of = "dew", n = "눈송이 꼬마", price = 30, ch = true },
            new Skin { id = "sk_coin", of = "coin", n = "부자 꼬마", top = true, price = 40, ch = true },
            new Skin { id = "sk_star", of = "star", n = "은하 꼬마", price = 40, ch = true },
            new Skin { id = "sk_leaf", of = "leaf", n = "벚꽃 꼬마", price = 30, ch = true },
            new Skin { id = "sk_moon", of = "moon", n = "햇님 꼬마", price = 30, ch = true },
            new Skin { id = "sk_wind", of = "wind", n = "구름 꼬마", top = true, price = 30, ch = true },
            new Skin { id = "sk_rock", of = "rock", n = "보석 꼬마", top = true, price = 50, ch = true },
            new Skin { id = "sk_chest", of = "chest", n = "딸기초코 꼬마", price = 30, ch = true },
        };
        // 수확기 스킨: 수확기마다 1개. 스프라이트 Harvesters/hs_<of>
        public static readonly Skin[] HV_SKINS =
        {
            new Skin { id = "hs_sam", of = "sam", n = "벚꽃 낫", aura = "#ffb0c8", price = 40 },
            new Skin { id = "hs_saw", of = "saw", n = "흑요석 수리검", aura = "#b88aff", price = 50 },
            new Skin { id = "hs_mill", of = "mill", n = "얼음 칼날", aura = "#9fe8ff", price = 50 },
            new Skin { id = "hs_spore", of = "spore", n = "은하 포자", aura = "#8a8aff", price = 50 },
            new Skin { id = "hs_bell", of = "bell", n = "황금 방울", aura = "#ffd84a", price = 60 },
            new Skin { id = "hs_coin", of = "coin", n = "옥 엽전", aura = "#8ff0c8", price = 50 },
            new Skin { id = "hs_gold", of = "gold", n = "용의 낫", aura = "#ff7a3a", price = 80 },
        };
        public static readonly Dictionary<string, Skin> SKIN = CHAR_SKINS.Concat(HV_SKINS).ToDictionary(s => s.id);

        // 꼬마 액세서리·모자 자리 (CritterRig 단위: 꼬마 가운데 기준 x, y · 너비 w · 갓 뒤에 그리면 back)
        public struct AccPlace { public float x, y, w; public bool back; public AccPlace(float x, float y, float w, bool back = false) { this.x = x; this.y = y; this.w = w; this.back = back; } }
        public static readonly Dictionary<string, AccPlace> ACC_PLACE = new Dictionary<string, AccPlace>
        {
            ["bandana"] = new AccPlace(0, 0.12f, 0.9f), ["headphones"] = new AccPlace(0, 0.12f, 1f), ["scarf"] = new AccPlace(0, -0.29f, 0.46f),
            ["tophat"] = new AccPlace(0.03f, 0.34f, 0.42f), ["halo"] = new AccPlace(0, 0.48f, 0.6f, true), ["flower"] = new AccPlace(0.3f, 0.26f, 0.26f),
            ["sunglasses"] = new AccPlace(0, -0.2f, 0.36f), ["cloud"] = new AccPlace(0.03f, 0.37f, 0.5f), ["crown"] = new AccPlace(0.03f, 0.34f, 0.36f),
            ["bow"] = new AccPlace(-0.3f, 0.24f, 0.32f), ["chef"] = new AccPlace(0.03f, 0.37f, 0.46f), ["straw"] = new AccPlace(0, 0.25f, 1.05f),
            ["hardhat"] = new AccPlace(0.03f, 0.35f, 0.5f),
        };
    }
}
