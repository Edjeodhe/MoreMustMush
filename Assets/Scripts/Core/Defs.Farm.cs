using System.Collections.Generic;
using System.Linq;

namespace MoreMush
{
    // Mushroom farm (ranch · field · kitchen), decorations and skins (prototype "버섯 농장", "농장 꾸미기",
    // "버섯 밭", "꼬마 식당", "스킨" sections). Screen coordinates are the prototype's 1920×1080 stage pixels.
    public static partial class Defs
    {
        // ===== 버섯 농장 =====
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
            public const float x0 = 250, y0 = 300, x1 = 1670, y1 = 900;   // 목장 울타리 안
            public const float pan = 0.9f;                         // 장면 넘어가는 시간 (초)
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
            ["kitchen"] = new[] { "보글보글~ 맛있겠다!", "간 맞추는 중이야!", "조심해, 뜨거워!", "오늘의 메뉴는 뭘까?", "셰프 모자 잘 어울려?" },
        };
        public static readonly Dictionary<string, float> FARM_ANIMS = new Dictionary<string, float>
        { ["jump"] = 0.9f, ["spin"] = 0.8f, ["dance"] = 1.6f, ["heart"] = 1.4f, ["sleep"] = 2.6f, ["surprise"] = 0.9f };

        // ===== 농장 꾸미기 (균사석으로 사서 목장에 놓는 장식. 꼬마들이 근처에서 놀기를 좋아한다) =====
        public class Deco { public string id, n, d; public int price; public float x, y; }
        public static readonly Deco[] DECOR =
        {
            new Deco { id = "dc_table", n = "버섯 탁자", d = "커다란 버섯 탁자와 버섯 의자. 꼬마들이 둘러앉아 쉬어요.", price = 20, x = 620, y = 440 },
            new Deco { id = "dc_fire", n = "버섯 화로", d = "버섯 갓 모양 화로에 포근한 불이 타올라요.", price = 25, x = 1290, y = 450 },
            new Deco { id = "dc_lamp", n = "포자 등불", d = "흙길 입구를 밝히는 은은한 버섯 등불 한 쌍.", price = 15, x = 960, y = 400 },
            new Deco { id = "dc_bed", n = "꽃 화단", d = "알록달록 꽃이 핀 나무 화단.", price = 15, x = 400, y = 430 },
            new Deco { id = "dc_swing", n = "꼬마 그네", d = "버섯 나무 두 그루에 매단 그네. 바람에 흔들흔들.", price = 30, x = 470, y = 790 },
            new Deco { id = "dc_well", n = "버섯 분수", d = "버섯 갓에서 물이 퐁퐁 솟는 작은 분수.", price = 40, x = 1450, y = 790 },
            new Deco { id = "dc_house", n = "버섯 오두막", d = "창문이 달린 빨간 버섯 집. 밤에는 불이 켜져요.", price = 50, x = 1530, y = 420 },
            new Deco { id = "dc_statue", n = "균사석 조각상", d = "균사석을 깎아 만든 조각상. 마당 한가운데서 빛나요.", price = 80, x = 960, y = 600 },
        };
        public static readonly Dictionary<string, Deco> DECO = DECOR.ToDictionary(d => d.id);

        // ===== 버섯 밭 =====
        // 8×8 칸 중 가운데 size×size가 열려 있다 (처음 4×4, 골드로 8×8까지). 칸 키 "행,열"은 8×8 기준
        public static class FIELD
        {
            public const int max = 8;
            public const float cx = 690, cy = 586, span = 640;    // 밭 가운데 · 칸 크기 = span / 한 변 칸 수 (최대 112px)
            public static readonly Dictionary<int, double> expand = new Dictionary<int, double> { [4] = 3000, [5] = 30000, [6] = 300000, [7] = 3000000 };
            public const float work = 0.6f;                        // 한 칸 일하는 시간 (초) ÷ 진화 속도
        }
        // 농기구: 등급이 높을수록 수확 때 버섯 포자가 더 많이 나온다
        public class Tool { public string n, grade, bonus; public double cost; public int dropMin, dropMax; public string col; }
        public static readonly Tool[] TOOLS =
        {
            new Tool { n = "낡은 호미", grade = "일반", cost = 0, dropMin = 0, dropMax = 1, bonus = "기본", col = "#9a8a70" },
            new Tool { n = "철제 호미", grade = "고급", cost = 2000, dropMin = 1, dropMax = 2, bonus = "+1", col = "#5fb8ff" },
            new Tool { n = "은빛 괭이", grade = "희귀", cost = 20000, dropMin = 1, dropMax = 3, bonus = "+1~2", col = "#c77dff" },
            new Tool { n = "균사 괭이", grade = "영웅", cost = 200000, dropMin = 2, dropMax = 4, bonus = "+2~3", col = "#2a9a74" },
            new Tool { n = "대지의 쇠스랑", grade = "전설", cost = 2000000, dropMin = 3, dropMax = 6, bonus = "+3~5", col = "#e8a900" },
        };
        // 작물: spore 포자 수 · need 분류 버섯 수(스테이지마다 ×1.1) · time 자라는 시간(초) · gem 수확 균사석
        public class Crop { public string n; public int spore, need, gem; public float time; }
        public static readonly Dictionary<string, Crop> CROPS = new Dictionary<string, Crop>
        {
            ["ed"] = new Crop { n = "식용 버섯", spore = 1, need = 20, time = 300, gem = 2 },
            ["md"] = new Crop { n = "약용 버섯", spore = 1, need = 20, time = 900, gem = 5 },
            ["ps"] = new Crop { n = "독버섯", spore = 2, need = 20, time = 2400, gem = 12 },
        };

        // ===== 꼬마 식당 =====
        public static class KIT
        {
            public static readonly UnityEngine.Vector2 counter = new UnityEngine.Vector2(770, 830), stove = new UnityEngine.Vector2(420, 830), table = new UnityEngine.Vector2(980, 960);
            // [어디서, 몇 초, 손에 든 것]
            public static readonly (string at, float t, string hold)[] steps = { ("counter", 1.0f, "tool_knife"), ("stove", 1.6f, "tool_ladle"), ("table", 0.5f, "tool_plate") };
        }

        // ===== 스킨 (균사석으로 산다) =====
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
        };
    }
}
