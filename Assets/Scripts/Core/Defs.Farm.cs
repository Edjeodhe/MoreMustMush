using System.Collections.Generic;
using System.Linq;

namespace MoreMush
{
    // Mushroom farm (one screen: ranch with the mushroom tree inside), buildings, critter grades, idle harvest reward and skins.
    // Screen coordinates are the 1920×1080 stage pixels. Every number here is a design value meant to be tuned.
    public static partial class Defs
    {
        // ===== 버섯 농장: 꼬마 부탁·호감도·진화 =====
        public static class FARM
        {
            public static readonly float[] wait = { 240, 600 };   // 다음 부탁까지 (초, 무작위) × 진화 배율
            public const float first = 60;                        // 처음 들어온 꼬마의 첫 부탁 (초)
            public const int hearts = 5;                          // 호감도 칸 수 (다 채우면 진화 가능)
            public static readonly int[] per = { 3, 5, 8 };       // 호감도 한 칸에 필요한 부탁 수 (진화 단계별)
            public const int evoMax = 2;
            public const float bonusP = 0.1f;                     // 채운 호감도 칸마다 부탁 때 균사석 보너스 확률
            public static readonly float[] evoGem = { 1, 1.5f, 2 };
            public static readonly float[] evoWait = { 1, 0.85f, 0.7f };
            public static readonly float[] evoSpd = { 1, 1.2f, 1.4f };
            public const float x0 = 260, y0 = 300, x1 = 1660, y1 = 860;   // 꼬마가 돌아다니는 울타리 안
            public const float diaP = 0.25f;                       // 부탁 들어줄 때 다이아몬드 1개 확률
            public const int diaHeart = 2;                         // 호감도 한 칸을 새로 채우면 다이아몬드
        }

        public class FarmKind { public string id, icon, n; public float w; public int gem; }
        public static readonly FarmKind[] FARM_KINDS =
        {
            new FarmKind { id = "water", icon = "req_water", n = "목이 말라요", w = 34, gem = 1 },
            new FarmKind { id = "pet", icon = "req_pet", n = "쓰다듬어 줘요", w = 30, gem = 1 },
            new FarmKind { id = "song", icon = "req_song", n = "노래 불러 줘요", w = 20, gem = 2 },
            new FarmKind { id = "snack", icon = "req_snack", n = "간식 주세요", w = 11, gem = 3 },
            new FarmKind { id = "gift", icon = "req_gift", n = "선물이 있어요!", w = 5, gem = 6 },
        };
        public static readonly Dictionary<string, FarmKind> FARM_KIND = FARM_KINDS.ToDictionary(k => k.id);

        public static readonly Dictionary<string, string[]> FARM_LINES = new Dictionary<string, string[]>
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
            ["all"] = new[] { "할아버지, 오늘도 수확 잘했어요?", "여기 농장 진짜 좋아!", "심심해~ 놀아 줘!", "포자 날린다~", "배고파... 버섯 수프 없나?", "헤헤, 간지러워!", "같이 산책할래?" },
            ["thanks"] = new[] { "고마워! 이거 받아!", "헤헤, 최고야!", "역시 할아버지야!", "기분 좋아~ 선물이야!" },
            ["field"] = new[] { "영차영차! 밭일은 맡겨 줘!", "흙냄새 좋다~", "포자 쏙쏙 심어 줄게!", "다 자라면 균사석이 나와!", "허수아비 아저씨 안녕!" },
            ["build"] = new[] { "뚝딱뚝딱! 금방 지을게!", "망치질은 자신 있어!", "조금만 기다려 줘~", "튼튼하게 짓는 중이야!" },
        };
        public static readonly Dictionary<string, float> FARM_ANIMS = new Dictionary<string, float>
        { ["jump"] = 0.9f, ["spin"] = 0.8f, ["dance"] = 1.6f, ["heart"] = 1.4f, ["sleep"] = 2.6f, ["surprise"] = 0.9f };

        // ===== 꼬마 별 등급 (골드 + 균사석으로 강화, 아웃게임 능력치만) =====
        public static class CSTAR
        {
            public const int max = 5;
            public static readonly double[] gold = { 10000, 100000, 1000000, 10000000, 100000000 };   // n성 → n+1성
            public static readonly int[] gem = { 5, 10, 20, 40, 80 };
            public const float build = 0.08f, work = 0.08f;       // 별마다 이 꼬마의 건설 속도 · 버섯 따기 속도
        }

        // 꼬마 보유 효과: 잡으면 생기고, 별 등급마다 커진다 (값 = v0 + vs × 별)
        // key: fieldGemSelf 이 꼬마가 딴 나무 버섯 균사석 · build 모든 건설 속도 · grow 나무 버섯 성장 시간 · autoGold 자동 수확 골드
        //      dia 부탁 다이아 확률 · spore 버섯 딸 때 포자 +1 확률 · autoGem 자동 수확 균사석 · work 모든 꼬마 버섯 따기 속도
        //      buildSelf 이 꼬마의 건설 시간 · fieldGem 나무 버섯 균사석
        public class CritterFx { public string key, fmt; public float v0, vs; }
        public static readonly Dictionary<string, CritterFx> CRITTER_FX = new Dictionary<string, CritterFx>
        {
            ["fire"] = new CritterFx { key = "fieldGemSelf", v0 = 0.20f, vs = 0.10f, fmt = "불씨 꼬마가 버섯을 따면 균사석 +{0}" },
            ["spark"] = new CritterFx { key = "build", v0 = 0.10f, vs = 0.05f, fmt = "모든 건물 건설 속도 +{0}" },
            ["dew"] = new CritterFx { key = "grow", v0 = 0.05f, vs = 0.02f, fmt = "나무 버섯 성장 시간 −{0}" },
            ["coin"] = new CritterFx { key = "autoGold", v0 = 0.10f, vs = 0.05f, fmt = "자동 수확 골드 +{0}" },
            ["star"] = new CritterFx { key = "dia", v0 = 0.05f, vs = 0.02f, fmt = "부탁을 들어줄 때 다이아몬드 확률 +{0}p" },
            ["leaf"] = new CritterFx { key = "spore", v0 = 0.20f, vs = 0.10f, fmt = "버섯 딸 때 포자 +1 확률 +{0}p" },
            ["moon"] = new CritterFx { key = "autoGem", v0 = 0.10f, vs = 0.05f, fmt = "자동 수확 균사석 +{0}" },
            ["wind"] = new CritterFx { key = "work", v0 = 0.10f, vs = 0.05f, fmt = "모든 꼬마 버섯 따기 속도 +{0}" },
            ["rock"] = new CritterFx { key = "buildSelf", v0 = 0.20f, vs = 0.10f, fmt = "조약돌 꼬마가 지으면 건설 시간 −{0}" },
            ["chest"] = new CritterFx { key = "fieldGem", v0 = 0.05f, vs = 0.03f, fmt = "나무 버섯 균사석 +{0}" },
        };

        // ===== 건축 (다이아몬드로 짓는 건물. 규격(칸)과 최대 개수는 정해져 있다) =====
        // 배치 격자: 칸 40px, (x0, y0)부터 cols × rows. 칸 가운데가 울타리 타원 안이고, 나무 둘레·다른 건물과 겹치지 않아야 놓을 수 있다
        public static class GRID
        {
            public const float cell = 40, x0 = 120, y0 = 260;
            public const int cols = 42, rows = 16;
            public const float ecx = 960, ecy = 565, erx = 840, ery = 285;   // 울타리 안 타원 (Tools/art/make_build_grid.py도 같이)
        }
        // stat: grow 나무 버섯 성장 시간 − · spore 버섯 딸 때 포자 +1 확률 + · wait 꼬마 부탁 간격 − · build 건설 시간 −
        //       dia 부탁 다이아 확률 + · fieldGem 나무 버섯 균사석 + · auto 자동 수확 보상 + · harvest 라운드 버섯 수확량 +
        public class Building { public string id, n, d, stat, statFmt; public float val, px; public int price, max, w, h; public float time; }
        public static readonly Building[] BUILDINGS =
        {
            new Building { id = "dc_table", n = "버섯 탁자", d = "커다란 버섯 탁자와 버섯 의자.", price = 20, time = 600, max = 2, w = 6, h = 3, px = 240, stat = "grow", val = 0.02f, statFmt = "나무 버섯 성장 시간 −{0}" },
            new Building { id = "dc_lamp", n = "포자 등불", d = "은은한 버섯 등불 한 쌍.", price = 15, time = 300, max = 3, w = 6, h = 2, px = 230, stat = "spore", val = 0.03f, statFmt = "버섯 딸 때 포자 +1 확률 +{0}p" },
            new Building { id = "dc_bed", n = "꽃 화단", d = "알록달록 꽃이 핀 나무 화단.", price = 15, time = 300, max = 3, w = 5, h = 2, px = 190, stat = "wait", val = 0.02f, statFmt = "꼬마 부탁 간격 −{0}" },
            new Building { id = "dc_fire", n = "버섯 화로", d = "버섯 갓 모양 화로에 포근한 불이 타올라요.", price = 25, time = 900, max = 1, w = 4, h = 3, px = 140, stat = "build", val = 0.03f, statFmt = "건물 건설 시간 −{0}" },
            new Building { id = "dc_swing", n = "꼬마 그네", d = "버섯 나무 두 그루에 매단 그네.", price = 30, time = 1200, max = 1, w = 6, h = 3, px = 240, stat = "dia", val = 0.02f, statFmt = "부탁을 들어줄 때 다이아몬드 확률 +{0}p" },
            new Building { id = "dc_well", n = "버섯 분수", d = "버섯 갓에서 물이 퐁퐁 솟는 작은 분수.", price = 40, time = 1800, max = 2, w = 5, h = 3, px = 180, stat = "fieldGem", val = 0.02f, statFmt = "나무 버섯 균사석 +{0}" },
            new Building { id = "dc_house", n = "버섯 오두막", d = "창문이 달린 빨간 버섯 집.", price = 50, time = 3600, max = 2, w = 6, h = 4, px = 230, stat = "auto", val = 0.03f, statFmt = "자동 수확 보상 +{0}" },
            new Building { id = "dc_statue", n = "균사석 조각상", d = "균사석을 깎아 만든 조각상.", price = 80, time = 7200, max = 1, w = 3, h = 3, px = 120, stat = "harvest", val = 0.02f, statFmt = "라운드 버섯 수확량 +{0}" },
        };
        public static readonly Dictionary<string, Building> BUILDING = BUILDINGS.ToDictionary(b => b.id);
        public const int BUILD_SLOTS = 15;   // 지을 수 있는 건물 총수 (최대 개수 합)

        // ===== 버섯 나무 (밭 대신 한 그루. 가지에 버섯이 저절로 열리고, 꼬마가 따 온다) =====
        public static class TREE
        {
            public const int maxLv = 10;
            public const float x = 960, y = 480;                  // 나무 밑동 (화면 좌표): 목장 가운데 뒤쪽, 랜드마크 자리
            public static int Slots(int lv) => 3 + lv;            // 버섯이 열리는 자리 수
            public static double Cost(int lv) => 3000 * System.Math.Pow(3, lv - 1);   // lv → lv+1 골드
            public const float speedPerLv = 0.9f;                 // 성장 시간 × 0.9^(lv-1)
            public const float yieldPerLv = 0.15f;                // 수확량 × (1 + 0.15 × (lv-1))
            public static int Stage(int lv) => System.Math.Min(4, (lv - 1) / 2);   // 나무 그림 단계 (tree_0 ~ tree_4)
            public static readonly float[] width = { 180, 240, 300, 360, 420 };   // 단계별 나무 너비 (px). 5단계 우듬지가 화면 위에 걸리지 않게
            // 단계별 버섯이 열리는 타원 (나무 그림 너비 대비: 가운데 x, 밑동에서 위로 y, 반지름 rx·ry) — Farm/Tree/tree_N 그림에서 잰 값
            public static readonly float[][] canopy =
            {
                new[] { 0f, 0.80f, 0.30f, 0.20f }, new[] { 0f, 1.00f, 0.34f, 0.26f }, new[] { 0f, 0.80f, 0.38f, 0.26f },
                new[] { 0f, 0.66f, 0.40f, 0.24f }, new[] { 0f, 0.72f, 0.40f, 0.26f },
            };
            public const float pick = 0.6f;                       // 버섯 하나 따는 시간 (초) ÷ 속도
            public const float accelSec = 600;                    // 균사석 1개로 줄이는 성장 시간 (초)
            public const float zoneX0 = 740, zoneX1 = 1180, zoneY0 = 260, zoneY1 = 540;   // 건물을 놓을 수 없는 나무 밑동 둘레 (Tools/art/make_build_grid.py도 같이)
            public const int order = 880;                         // 나무는 꼬마·건물과 그 그림자(899~)보다 항상 뒤에 그린다 (그림자 −1 · 버섯 +1·+2 · 팻말 +3·+4)
        }
        // 나무에 열리는 버섯: w 열릴 확률 가중치 · time 자라는 시간(초) · gem 딸 때 균사석
        public class Fruit { public string n; public int gem; public float w, time; }
        public static readonly Dictionary<string, Fruit> FRUITS = new Dictionary<string, Fruit>
        {
            ["ed"] = new Fruit { n = "식용 버섯", w = 60, time = 180, gem = 2 },
            ["md"] = new Fruit { n = "약용 버섯", w = 30, time = 420, gem = 5 },
            ["ps"] = new Fruit { n = "독버섯", w = 10, time = 900, gem = 12 },
        };
        public const float BUILD_ACCEL_SEC = 300;                 // 균사석 1개로 줄이는 건설 시간 (초)
        public const float BUILD_HOLD = 0.35f, BUILD_DRAG = 18;   // 건물을 이만큼(초) 꾹 누르거나 이만큼(px) 끌면 옮기기 시작

        // 건설하는 꼬마 그림 (Characters/Critters/Build/build_<id>_0 들어 올림 · _1 내려침)
        public static class BUILDER
        {
            public const float w = 2.6f;                          // 그림 너비 = 꼬마 반지름 × w
            public const float beat = 0.6f, strike = 0.2f;        // 망치질 한 번 (초) · 그중 내려친 프레임
        }

        // ===== 자동 수확 보상 (마지막으로 받은 뒤 쌓인다, 최대 8시간) =====
        public static class AUTO
        {
            public const float maxHours = 8;
            public const double goldTaxK = 0.25, goldMin = 200;   // 골드/시간 = max(goldMin, 이번 사이클 최소 세금 × goldTaxK)
            public const double gemH = 1, diaH = 0.4;             // 균사석·다이아몬드/시간
            public const float minClaim = 60;                      // 받을 수 있는 최소 시간 (초)
            public const float returnMin = 600;                    // 이만큼(초) 자리를 비웠다 돌아오면 복귀 팝업
        }

        // ===== 스킨 (다이아몬드로 산다) =====
        // 꼬마 스킨: 꼬마마다 1개. 갓을 스킨 갓(Critters/skin_<of>)으로 바꾸고 액세서리(Critters/Acc/<acc>)를 단다.
        public class Skin { public string id, of, n, acc, aura; public int price; public bool top, ch; }
        public static readonly Skin[] CHAR_SKINS =
        {
            new Skin { id = "sk_fire", of = "fire", n = "용암 꼬마", acc = "bandana", price = 30, ch = true },
            new Skin { id = "sk_spark", of = "spark", n = "네온 꼬마", acc = "headphones", price = 30, ch = true },
            new Skin { id = "sk_dew", of = "dew", n = "눈송이 꼬마", acc = "scarf", price = 30, ch = true },
            new Skin { id = "sk_coin", of = "coin", n = "부자 꼬마", acc = "tophat", top = true, price = 40, ch = true },
            new Skin { id = "sk_star", of = "star", n = "은하 꼬마", acc = "halo", price = 40, ch = true },
            new Skin { id = "sk_leaf", of = "leaf", n = "벚꽃 꼬마", acc = "flower", price = 30, ch = true },
            new Skin { id = "sk_moon", of = "moon", n = "햇님 꼬마", acc = "sunglasses", price = 30, ch = true },
            new Skin { id = "sk_wind", of = "wind", n = "구름 꼬마", acc = "cloud", top = true, price = 30, ch = true },
            new Skin { id = "sk_rock", of = "rock", n = "보석 꼬마", acc = "crown", top = true, price = 50, ch = true },
            new Skin { id = "sk_chest", of = "chest", n = "딸기초코 꼬마", acc = "bow", price = 30, ch = true },
        };
        // 수확기 스킨: 수확기마다 1개. 스프라이트 Harvesters/hs_<of>
        public static readonly Skin[] HV_SKINS =
        {
            new Skin { id = "hs_sam", of = "sam", n = "벚꽃 낫", aura = "#ffb0c8", price = 40 },
            new Skin { id = "hs_saw", of = "saw", n = "흑요석 표창", aura = "#b88aff", price = 50 },
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
            ["bandana"] = new AccPlace(0, 0.1f, 0.66f), ["headphones"] = new AccPlace(0, 0.14f, 0.76f), ["scarf"] = new AccPlace(0, -0.3f, 0.56f),
            ["tophat"] = new AccPlace(0.02f, 0.52f, 0.36f), ["halo"] = new AccPlace(0, 0.62f, 0.46f, true), ["flower"] = new AccPlace(0.22f, 0.3f, 0.24f),
            ["sunglasses"] = new AccPlace(0, -0.115f, 0.42f), ["cloud"] = new AccPlace(0, 0.54f, 0.46f), ["crown"] = new AccPlace(0, 0.52f, 0.32f),
            ["bow"] = new AccPlace(-0.22f, 0.32f, 0.27f), ["chef"] = new AccPlace(0, 0.54f, 0.4f), ["straw"] = new AccPlace(0, 0.4f, 0.72f),
            ["hardhat"] = new AccPlace(0, 0.46f, 0.5f),
        };
    }
}
