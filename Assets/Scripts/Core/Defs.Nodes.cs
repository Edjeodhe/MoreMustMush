using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 스킬·바 버프 =====
        public class Skill
        {
            public string id, n, kind, icon, col;
            public double costGold;
            public bool noPow, noRate;
            public Func<int, string> eff;
        }

        public static readonly Dictionary<string, Skill> SKILLS = new Dictionary<string, Skill>();
        public static readonly List<string> SKILL_IDS = new List<string>();

        static void AddSkill(string id, string n, string kind, double gold, string col, Func<int, string> eff, bool noPow = false, bool noRate = false)
        {
            SKILLS[id] = new Skill { id = id, n = n, kind = kind, costGold = gold, icon = "s_" + id, col = col, eff = eff, noPow = noPow, noRate = noRate };
            SKILL_IDS.Add(id);
        }

        // 성장 설계
        // - 레벨 효과는 레벨당 고정값을 더하는 식이다. 최대 레벨은 2~4로 짧다.
        // - 공격력·타격 점수·수확량은 단계 노드 I(+1) → II(+10) → III(+100) → IV(+1000)로 단위가 커진다.
        //   다음 단계는 앞 단계를 최대 레벨까지 올려야 열린다(가격이 아니라 구조로 막는다).
        // - 버섯 체력·보상도 지역마다 ×10씩 커지므로(ZoneMul) 단계가 오를 때마다 숫자 단위가 한 자리씩 뛴다.
        // - 곱셈은 균사석 노드(후반)에서만 들어온다.
        // - 지역·스테이지로 노드를 잠그지 않는다. 기본 노드는 골드로만 사고, 단계(초반·중반·후반)는 가격으로만 구분한다.
        //   가격은 산 순서대로 늘어놓았을 때 한 번에 크게 뛰지 않게 맞춘다(Defs.Prices.cs, BalanceSim.TunePrices).
        public static double SkillRate(int S) => 0.10 + 0.10 * S;
        public static double SkillPow(int P) => 1 + 0.75 * P;   // 스킬 위력 배율 (피해·시간·속도)
        public static double KidRate(int L) => 0.12 * L;
        public static double MultiRate(int L) => 0.10 * L;

        // ===== 균사 노드 효과 공식 (NF) =====
        public static class NF
        {
            // 단계 노드: 고정값 더하기 (I +1 · II +10 · III +100 · IV +1000)
            public static double ps_atk(int L) => 1 * L;
            public static double ps_atk2(int L) => 10 * L;
            public static double ps_atk3(int L) => 100 * L;
            public static double ps_atk4(int L) => 1000 * L;
            public static double ed_score(int L) => 1 * L;      // 타격 점수 +
            public static double ed_score2(int L) => 10 * L;
            public static double ed_score3(int L) => 100 * L;
            public static double ed_bonus(int L) => 1 * L;      // 버섯 하나당 수확 개수 +
            public static double ed_bonus2(int L) => 10 * L;
            public static double ed_bonus3(int L) => 100 * L;
            // 형제 노드 (I+ · II+ · III+): 같은 단계 효과를 한 번 더. 최대 레벨을 늘리지 않고 줄의 레벨 수를 늘린다
            public static double ed_score1p(int L) => 1 * L;
            public static double ed_score2p(int L) => 10 * L;
            public static double ed_score3p(int L) => 100 * L;
            public static double ed_bonus1p(int L) => 1 * L;
            public static double ed_bonus2p(int L) => 10 * L;
            public static double ed_bonus3p(int L) => 100 * L;
            public static double ps_atk2p(int L) => 10 * L;
            public static double ps_atk3p(int L) => 100 * L;
            public static double ed_price(int L) => 1 + 0.3 * L;
            public static double ed_price2(int L) => 0.5 * L;   // 판매가 +50%씩 (I에 더함)
            public static int ed_cols2(int L) => 4 * L;         // 군락지 수 +4씩
            public static double md_combo2(int L) => 0.1 * L;   // 콤보 계수 +0.1씩
            public static double ps_dur2(int L) => 3 * L;       // 라운드 시간 +3초씩
            public static double ps_crit2(int L) => 3 + 1 * L;  // 치명타 배율 ×3 → ×5
            public static double ed_regen(int L) => 4 - 0.8 * L;
            public static double ed_rare(int L) => 1 + 0.5 * L;
            public static double ed_gold(int L) => 0.01 + 0.02 * L;
            public static double ed_tax(int L) => 1 - 0.1 * L;
            public static double ed_trade(int L) => 1 + 0.3 * L;
            public static double ed_giant(int L) => 0.08 + 0.1 * L;
            public static double ed_special(int L) => 0.1 * L;
            public static double ed_special_life(int L) => SPECIAL_LIFE + 2 * L;
            public static int ed_size(int L) => 5 + 2 * L;   // 군락지당 최대 버섯 수
            public static int ed_dev(int L) => 2 + 2 * L;    // 라운드 시작 숲 장치 수
            public static double md_bar(int L) => 1 + 0.25 * L;
            public static double md_combo(int L) => 0.1 + 0.05 * L;
            public static double md_chef(int L) => Math.Pow(0.92, L);        // 건설 시간 배율 (농장)
            public static double md_buffdur(int L) => 1 + 0.4 * L;
            public static double md_templife(int L) => 1 + 0.4 * L;
            public static double md_stump(int L) => 1 + 0.8 * L;
            public static double ps_size(int L) => 1 + 0.15 * L;
            public static double ps_spd(int L) => 1 + 0.12 * L;
            public static double ps_dur(int L) => 2 * L;   // 라운드 시간 +초
            public static double ps_crit(int L) => 0.05 * L;
            public static double ps_heavy(int L) => 1 + 0.5 * L;
            public static double ps_resist(int L) => 1 - 0.15 * L;
            public static double ps_rush(int L) => 1 + 0.75 * L;
            public static double bl_range(int L) => 20 + 25 * L;
            public static double bl_spd(int L) => 0.6 - 0.12 * L;
            public static double bl_pow(int L) => 0.4 * SkillPow(L);
            // 스킬 범위: 기본값에 위력 레벨당 고정값을 더한다
            public static double burst_r(int P) => 80 + 25 * P;
            public static double tornado_h(int P) => 50 + 22 * P;
            public static double shock_r(int P) => 100 + 30 * P;
            // ↓ 균사석 노드 (후반): 다른 보너스를 다 더한 값에 곱한다
            public static double gm_harvest(int L) => Math.Pow(1.25, L);
            public static double gm_spore(int L) => 0.25 * L;
            public static double gm_toxin(int L) => Math.Pow(0.75, L);
            public static double gm_herb(int L) => 1 + 0.2 * L;              // 자동 수확 보상 배율
            public static double gm_gourmet(int L) => 1 + 0.1 * L;           // 밭 수확 균사석 배율
            public static double gm_farm(int L) => Math.Pow(0.8, L);
            public struct Meteor { public double interval, dmg, r; }
            public static Meteor gm_meteor(int L) => new Meteor { interval = 3.5 * Math.Pow(0.8, Math.Max(0, L - 1)), dmg = 3 * Math.Pow(2, Math.Max(0, L - 1)), r = 110 + 20 * L };
        }

        public class Node
        {
            public string id, br, parent, n, skill, icon, col, d;
            public bool core, sub, gem;
            public bool needMax;   // 부모 노드를 최대 레벨까지 올려야 열린다 (단계 노드 I → II → III)
            public int tier = 1;   // 성장 단계: 1 초반 · 2 중반 · 3 후반
            public int max;
            public double costGold, costGem, g;   // costGold·g: 가격표가 없을 때의 기본가와 사는 순서 기준
            public double[] costs;                // 레벨별 골드 가격 (Defs.Prices.cs)
            public Func<int, string> eff;
        }

        public static readonly List<Node> NODES = new List<Node>();
        public static readonly Dictionary<string, Node> NODE = new Dictionary<string, Node>();
        public static readonly string[] TIER_NAMES = { "", "초반", "중반", "후반" };

        static Node N(string id, string br, string parent, string n, int max, double gold, double g, Func<int, string> eff, double gem = 0, bool core = false, bool sub = false, string skill = null, string icon = null, string col = null, int tier = 1, bool needMax = false)
        {
            var node = new Node { id = id, br = br, parent = parent, n = n, max = max, costGold = gold, costGem = gem, g = g, eff = eff, core = core, sub = sub, skill = skill, icon = icon, col = col, gem = gem > 0, tier = tier, needMax = needMax };
            NODES.Add(node);
            return node;
        }

        static readonly Dictionary<string, string> SKILL_TREE = new Dictionary<string, string>
        {
            ["accel"] = "md_bar", ["burst"] = "accel", ["tspore"] = "accel", ["sharpen"] = "accel",
            ["magnet"] = "burst", ["clone"] = "burst", ["sip"] = "tspore", ["device"] = "tspore", ["split"] = "sharpen", ["clear"] = "sharpen",
            ["bolt"] = "magnet", ["shock"] = "clone", ["tornado"] = "split", ["fest"] = "shock", ["pierce"] = "accel",
        };

        static readonly Dictionary<string, string> NODE_DESC = new Dictionary<string, string>
        {
            ["core_ed"] = "💰 돈·수확 쪽 강화 가지예요. 점수, 판매가, 세금 감면, 마을 의뢰 보상, 군락지 수·재생, 수확량, 황금 변종, 거대·특수 버섯 등장을 올릴 수 있어요.",
            ["core_md"] = "🎯 바·스킬·시간·필드 쪽 강화 가지예요. 필드 넓히기, 바 넓이, 콤보, 꼬마 수확기, 스킬 15종(가속·포자 폭발·충격파…), 버프 지속, 요리 효과를 올릴 수 있어요.",
            ["core_ps"] = "⚔️ 전투 쪽 강화 가지예요. 공격력, 수확기 크기·속도, 제한시간, 치명타, 핀볼 수, 회전 칼날, 단단한 버섯 피해, 포자 저항을 올릴 수 있어요.",
            ["ed_score"] = "버섯을 칠 때마다 얻는 점수에 고정값을 더해요. 점수는 라운드가 끝나면 골드로 바뀌어요. 끝까지 올리면 타격 점수 I+가 열려요.",
            ["ed_score1p"] = "타격 점수를 1 단위로 한 번 더 더해요. 끝까지 올리면 타격 점수 II가 열려요.",
            ["ed_score2"] = "타격 점수를 10 단위로 더해요. 끝까지 올리면 타격 점수 II+가 열려요.",
            ["ed_score2p"] = "타격 점수를 10 단위로 한 번 더 더해요. 끝까지 올리면 타격 점수 III이 열려요.",
            ["ed_score3"] = "타격 점수를 100 단위로 더해요. 끝까지 올리면 타격 점수 III+가 열려요.",
            ["ed_score3p"] = "타격 점수를 100 단위로 한 번 더 더해요.",
            ["ed_bonus2"] = "버섯 하나를 수확할 때 창고에 들어오는 개수를 10 단위로 더해요. 끝까지 올리면 수확량 II+가 열려요.",
            ["ed_bonus2p"] = "수확 개수를 10 단위로 한 번 더 더해요. 끝까지 올리면 수확량 III이 열려요.",
            ["ed_bonus3"] = "버섯 하나를 수확할 때 창고에 들어오는 개수를 100 단위로 더해요. 끝까지 올리면 수확량 III+가 열려요.",
            ["ed_bonus3p"] = "수확 개수를 100 단위로 한 번 더 더해요.",
            ["ed_cols2"] = "군락지 수를 끝까지 올린 뒤 군락지를 더 늘려요.",
            ["ed_price2"] = "판매 가격을 끝까지 올린 뒤 판매가를 더 올려요.",
            ["md_combo2"] = "콤보 계수를 끝까지 올린 뒤 콤보당 배율을 더 키워요.",
            ["ps_atk2"] = "공격력을 10 단위로 더해요. 지역이 바뀌면 버섯 체력이 10배가 되니, 다음 지역에 가기 전에 올려 두세요. 끝까지 올리면 공격력 II+가 열려요.",
            ["ps_atk2p"] = "공격력을 10 단위로 한 번 더 더해요. 끝까지 올리면 공격력 III이 열려요.",
            ["ps_atk3"] = "공격력을 100 단위로 더해요. 끝까지 올리면 공격력 III+가 열려요.",
            ["ps_atk3p"] = "공격력을 100 단위로 한 번 더 더해요. 끝까지 올리면 공격력 IV가 열려요.",
            ["ps_atk4"] = "공격력을 1000 단위로 더해요.",
            ["ps_ball2"] = "영구 핀볼을 하나 더 늘려요.",
            ["ps_ball3"] = "영구 핀볼을 두 개까지 더 늘려요.",
            ["ps_dur2"] = "지속시간을 끝까지 올린 뒤 라운드 시간을 더 늘려요.",
            ["ps_crit2"] = "치명타가 났을 때 피해와 점수 배율을 키워요.",
            ["ed_price"] = "버섯 상점에서 버섯을 팔 때 받는 골드를 늘려요.",
            ["ed_cols"] = "필드에 동시에 있을 수 있는 버섯 군락지의 최대 개수를 늘려요.",
            ["ed_regen"] = "군락지를 다 수확해서 사라진 뒤, 새 군락지가 다시 돋아날 때까지 걸리는 시간이에요. 숫자가 작을수록 빨리 돋아나요.",
            ["ed_rare"] = "새로 돋는 군락지가 에픽·유니크·레전드리 버섯일 확률을 높여요.",
            ["ed_gold"] = "버섯이 황금 변종으로 나올 확률이에요. 황금 버섯은 점수 ×10, 수확량 ×5이고 도감에 황금으로 기록돼요.",
            ["ed_bonus"] = "버섯 하나를 수확할 때 창고에 들어오는 개수를 늘려요. 끝까지 올리면 수확량 I+가 열려요.",
            ["ed_bonus1p"] = "수확 개수를 1 단위로 한 번 더 더해요. 끝까지 올리면 수확량 II가 열려요.",
            ["ed_multi"] = "버섯을 수확할 때 일정 확률로 수확량이 3배가 돼요.",
            ["ed_size"] = "군락지 하나에서 가운데 무더기 둘레에 나는 개별 버섯 수의 최대치를 늘려요.",
            ["ed_tax"] = "세금 고지서 금액을 줄여요.",
            ["ed_trade"] = "버섯 상점의 마을 의뢰를 완료했을 때 받는 골드를 늘려요.",
            ["ed_giant"] = "라운드 중에 거대 버섯이 나타날 확률이에요. 거대 버섯은 점수 ×3, 수확량 ×30이에요.",
            ["ed_special"] = "캐릭터 모양 특수 버섯이 나타날 확률과, 도망가기 전까지 잡을 수 있는 시간을 늘려요.",
            ["md_chef"] = "버섯 농장에서 꼬마가 건물을 짓는 시간을 줄여요.",
            ["md_stump"] = "숲 장치 효과를 키워요: 그루터기 점수, 도토리 점수 ×2 지속시간, 이끼 길 가속.",
            ["md_templife"] = "분열·분신·꼬마 수확기로 생기는 임시 핀볼이 더 오래 남아요.",
            ["md_buffdur"] = "바 버프 중 가속·맑은 포자막의 지속시간과 날 갈기의 강화 타수를 늘려요.",
            ["ps_resist"] = "포자 구름에 닿았을 때 걸리는 느림·약화 디버프가 짧아져요.",
            ["ps_rush"] = "라운드를 시작하고 3초 동안 공격력이 올라가요. 처음 쏜 핀볼로 크게 터뜨리기 좋아요.",
            ["ps_heavy"] = "군락 가운데 버섯 무더기와 거대 버섯처럼 단단한 버섯에 주는 피해를 늘려요.",
            ["ed_dev"] = "라운드 시작 때 필드에 놓이는 숲 장치(그루터기 범퍼·이끼 길·두더지 굴·개울·도토리 스위치) 수를 늘려요.",
            ["md_map"] = "버섯 밭(필드)을 넓혀요. 처음엔 필드가 좁아 화면이 확대되어 수확기·버섯이 크게 보이고, 넓힐수록 군락지와 숲 장치가 더 많이 들어가요(수입 증가). 4레벨이면 원래 크기예요.",
            ["md_bar"] = "화면 아래 슬라이드 바(통나무)의 길이예요. 길수록 핀볼을 받아치기 쉬워요.",
            ["md_combo"] = "핀볼이 바에 맞을 때마다 콤보가 쌓이고, 콤보마다 점수 배율이 올라요. 그 배율이 오르는 폭을 키워요. 바닥 벽에 닿으면 콤보가 끊겨요.",
            ["md_kid"] = "영구 핀볼이 바에 맞을 때 확률로 작은 임시 핀볼이 하나 튀어나와요.",
            ["ps_atk"] = "수확기가 버섯에 부딪힐 때 주는 피해에 고정값을 더해요. 대부분의 스킬 피해도 공격력에 비례해요. 끝까지 올리면 공격력 II가 열려요.",
            ["ps_size"] = "수확기(핀볼)의 크기예요. 클수록 버섯에 잘 맞아요.",
            ["ps_spd"] = "수확기가 날아가는 속도예요.",
            ["ps_dur"] = "한 라운드의 제한시간을 늘려요. 라운드는 15초에서 시작해 스테이지마다 0.5초씩(최대 +10초) 길어지고, 여기에 더해져요.",
            ["ps_crit"] = "버섯을 칠 때 치명타가 날 확률이에요. 치명타는 피해와 점수가 ×3이에요(치명타 피해 노드로 더 커져요).",
            ["ps_ball"] = "라운드를 시작할 때 함께 발사하는 영구 핀볼을 하나 늘려요. 바 버프는 영구 핀볼만 받아요. 공격력 II·III 아래에서 더 늘릴 수 있어요.",
            ["bl"] = "수확기 둘레에 회전 칼날이 생겨서, 범위 안의 버섯에 주기적으로 피해를 줘요.",
            ["bl_pow"] = "회전 칼날이 한 번 벨 때 주는 피해예요.",
            ["bl_range"] = "회전 칼날이 닿는 범위(수확기 둘레 반지름)예요.",
            ["bl_spd"] = "회전 칼날이 베는 간격이에요. 짧을수록 자주 베요.",
            ["accel"] = "영구 핀볼이 바에 맞을 때 확률로 3초 동안 빨라져요.",
            ["sharpen"] = "영구 핀볼이 바에 맞을 때 확률로 다음 3번의 타격 피해가 커져요.",
            ["split"] = "영구 핀볼이 바에 맞을 때 확률로 임시 핀볼이 갈라져 나와요.",
            ["sip"] = "영구 핀볼이 바에 맞을 때 확률로 제한시간이 조금 늘어나요.",
            ["clear"] = "영구 핀볼이 바에 맞을 때 확률로 포자 구름의 느림·약화 디버프를 지우고 잠시 막아 줘요.",
            ["burst"] = "버섯을 칠 때 확률로 그 자리에 포자 폭발이 일어나 주변 버섯에 범위 피해를 줘요.",
            ["tspore"] = "버섯을 칠 때 확률로 제한시간이 조금 늘어나요.",
            ["clone"] = "버섯을 수확할 때 확률로 작은 분신 핀볼이 생겨요.",
            ["magnet"] = "버섯을 칠 때 확률로 1초 동안 가까운 버섯 쪽으로 휘어 날아가요.",
            ["bolt"] = "버섯을 칠 때 확률로 주변 버섯들로 번개가 튀며 연속으로 피해를 줘요.",
            ["tornado"] = "벽에 튕길 때 확률로 회오리가 가로로 필드를 쓸고 지나가요.",
            ["shock"] = "버섯을 수확할 때 확률로 충격파가 퍼지고, 충격파로 수확한 버섯이 또 충격파를 일으켜요(최대 8단계).",
            ["device"] = "버섯을 수확할 때 확률로 그 근처에 숲 장치가 하나 생겨요.",
            ["pierce"] = "단단한 버섯(군락 가운데 무더기·거대 버섯)에 부딪힐 때 확률로 튕기지 않고 뚫고 지나가요.",
            ["fest"] = "라운드 마지막 5초 동안 점수와 수확량이 몇 배가 돼요.",
            ["gm_harvest"] = "균사석으로 여는 특수 노드. 라운드에서 수확하는 모든 버섯 개수를 늘려요.",
            ["gm_spore"] = "균사석으로 여는 특수 노드. 버섯 나무에서 버섯을 딸 때 버섯 포자를 더 얻을 확률이 생겨요.",
            ["gm_gourmet"] = "균사석으로 여는 특수 노드. 버섯 나무에서 버섯을 딸 때 균사석을 더 얻어요.",
            ["gm_herb"] = "균사석으로 여는 특수 노드. 자동 수확 보상이 늘어나요.",
            ["gm_farm"] = "균사석으로 여는 특수 노드. 버섯 나무의 버섯이 더 빨리 자라요.",
            ["gm_meteor"] = "균사석 특수 스킬. 라운드 중 일정 시간마다 하늘에서 균사석이 떨어져, 떨어진 자리 둘레의 버섯에 큰 피해를 줘요. 발동률 없이 늘 일어나요.",
            ["gm_toxin"] = "균사석으로 여는 특수 노드. 독버섯이 뿜는 포자 구름이 줄고, 느림·약화 디버프가 짧아져요.",
        };

        // ===== 수확기 =====
        public class Harvester
        {
            public string id, n, type, look;
            public double unlockGold = -1;   // -1: 공방에서 해금 불가(보상), 0: 처음부터
            public double lvGold;
            public Func<int, Dictionary<string, double>> ab;
            public Func<int, string> text;
        }
        public static readonly List<Harvester> HARVESTERS = new List<Harvester>();
        public static readonly Dictionary<string, Harvester> HV = new Dictionary<string, Harvester>();
        public static readonly double[] HV_LV_MUL = { 0, 0.6, 1.2, 2.4, 4.8 };

        static Dictionary<string, double> D(params (string, double)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

        static void InitSkillsAndNodes()
        {
            string F(double v, int d = 2) => U.FmtN(v, d);
            // 스킬: (해금 가격, 단계). 발동률·위력 노드는 해금 가격의 2배에서 시작해 레벨당 ×3
            AddSkill("accel", "가속", "bar", 150, "#6fd3ff", P => $"3초간 속도 ×{F(1 + 0.15 * SkillPow(P))}");
            AddSkill("sharpen", "날 갈기", "bar", 5000, "#dfe6ee", P => $"다음 3타 피해 ×{F(2 * SkillPow(P), 1)}");
            AddSkill("split", "분열", "bar", 2000000, "#ffe36e", P => $"임시 핀볼 {1 + P}개 (6초)");
            AddSkill("sip", "시간 한 모금", "bar", 20000, "#9ee6ff", P => $"+{F(0.3 * SkillPow(P), 2)}초 (라운드당 +10초까지)");
            AddSkill("clear", "맑은 포자막", "bar", 15000, "#bff6ff", P => $"{F(1.5 * SkillPow(P), 2)}초 디버프 무시·해제");
            AddSkill("burst", "포자 폭발", "hit", 400, "#ffb347", P => $"반경 {U.JsRound(NF.burst_r(P))}, 피해 공격력×{F(2 * SkillPow(P), 1)}");
            AddSkill("tspore", "시간 포자", "hit", 500, "#8fe3ff", P => $"+{F(0.3 * SkillPow(P), 2)}초 (라운드당 +15초까지)");
            AddSkill("clone", "분신 수확기", "harvest", 5000000, "#c9d6ff", P => $"작은 임시 핀볼 {1 + P}개, 4초");
            AddSkill("magnet", "버섯 자석", "hit", 15000, "#ff6b8b", P => $"1초간 가까운 버섯 쪽으로 초당 {U.JsRound(20 * SkillPow(P))}°");
            AddSkill("bolt", "연쇄 번개", "hit", 20000000, "#fff36b", P => $"주변 {2 + P}개, 피해 공격력×{F(SkillPow(P), 1)}");
            AddSkill("tornado", "회오리", "wall", 30000000, "#b8f0e0", P => $"띠 높이 {U.JsRound(NF.tornado_h(P))}, 피해 공격력×{F(2 * SkillPow(P), 1)}");
            AddSkill("shock", "충격파", "harvest", 10000000, "#ffd0a0", P => $"반경 {U.JsRound(NF.shock_r(P))}, 피해 공격력×{F(SkillPow(P), 1)}, 연쇄 최대 8단계");
            AddSkill("device", "숲 장치 등장", "harvest", 25000, "#9be07a", P => "근처에 무작위 숲 장치 (라운드당 최대 +10개)", noPow: true);
            AddSkill("pierce", "관통", "pierce", 300, "#ffd6d6", P => $"단단한 버섯(무더기·거대)을 튕기지 않고 뚫고 지나가며 피해 공격력×{F(SkillPow(P), 1)}");
            AddSkill("fest", "풍년", "fest", 100000000, "#ffd84a", P => $"라운드 마지막 5초 모든 점수·수확량 ×{F(2 * SkillPow(P), 1)}", noRate: true);
            var SKILL_TIER = new Dictionary<string, int>
            {
                ["accel"] = 1, ["burst"] = 1, ["tspore"] = 1, ["pierce"] = 1,
                ["sharpen"] = 2, ["magnet"] = 2, ["sip"] = 2, ["clear"] = 2, ["device"] = 2,
                ["split"] = 3, ["clone"] = 3, ["shock"] = 3, ["bolt"] = 3, ["tornado"] = 3, ["fest"] = 3,
            };

            // 핵심 코어 (1레벨 고정)
            N("core_ed", "ed", null, "식용 균사", 1, 10, 1, L => L > 0 ? "열림 · 버섯 판매가 +10%" : "사면 식용 가지가 열리고 버섯 판매가 +10%", core: true);
            N("core_md", "md", null, "약용 균사", 1, 10, 1, L => L > 0 ? "열림 · 바 길이 +10%" : "사면 약용 가지가 열리고 바 길이 +10%", core: true);
            N("core_ps", "ps", null, "독 균사", 1, 10, 1, L => L > 0 ? "열림 · 공격력 +10%" : "사면 독 가지가 열리고 공격력 +10%", core: true);
            // 퍼센트 효과는 "기본 대비 +%"로 보여 준다 (다른 보너스와도 더해진다)
            string Plus(double mul) => $"+{U.JsRound((mul - 1) * 100)}%";
            string Num(double v) => U.Fmt(v);
            // ── 식용 (돈·수확) ──
            // 초반
            N("ed_score", "ed", "core_ed", "타격 점수 I", 3, 15, 2.2, L => $"버섯을 칠 때 점수 +{Num(NF.ed_score(L))}");
            N("ed_cols", "ed", "ed_score", "군락지 수", 3, 30, 2.2, L => $"최대 {ColCount(L)}개");
            N("ed_regen", "ed", "ed_score", "군락 재생 속도", 3, 50, 2.2, L => $"사라진 군락지가 {F(NF.ed_regen(L))}초 뒤 다시 돋아남");
            N("ed_price", "ed", "ed_score", "판매 가격", 3, 60, 2.2, L => $"버섯 판매가 {Plus(NF.ed_price(L))}");
            N("ed_bonus", "ed", "ed_regen", "수확량 I", 3, 40, 2.2, L => $"버섯 하나당 수확 +{Num(NF.ed_bonus(L))}개");
            N("ed_score1p", "ed", "ed_score", "타격 점수 I+", 3, 150, 2.2, L => $"버섯을 칠 때 점수 +{Num(NF.ed_score1p(L))}", needMax: true);
            N("ed_bonus1p", "ed", "ed_bonus", "수확량 I+", 3, 400, 2.2, L => $"버섯 하나당 수확 +{Num(NF.ed_bonus1p(L))}개", needMax: true);
            // 중반
            N("ed_score2", "ed", "ed_score1p", "타격 점수 II", 3, 2000, 3, L => $"버섯을 칠 때 점수 +{Num(NF.ed_score2(L))}", tier: 2, needMax: true);
            N("ed_bonus2", "ed", "ed_bonus1p", "수확량 II", 3, 3000, 3, L => $"버섯 하나당 수확 +{Num(NF.ed_bonus2(L))}개", tier: 2, needMax: true);
            N("ed_score2p", "ed", "ed_score2", "타격 점수 II+", 3, 40000, 3, L => $"버섯을 칠 때 점수 +{Num(NF.ed_score2p(L))}", tier: 2, needMax: true);
            N("ed_bonus2p", "ed", "ed_bonus2", "수확량 II+", 3, 60000, 3, L => $"버섯 하나당 수확 +{Num(NF.ed_bonus2p(L))}개", tier: 2, needMax: true);
            N("ed_cols2", "ed", "ed_cols", "군락지 수 II", 2, 10000, 3, L => $"군락지 +{NF.ed_cols2(L)}개", tier: 2, needMax: true);
            N("ed_price2", "ed", "ed_price", "판매 가격 II", 2, 15000, 3, L => $"버섯 판매가 +{U.JsRound(NF.ed_price2(L) * 100)}%", tier: 2, needMax: true);
            N("ed_rare", "ed", "ed_cols", "변종 출현율", 3, 5000, 2.2, L => $"에픽 이상 가중치 {Plus(NF.ed_rare(L))}", tier: 2);
            N("ed_size", "ed", "ed_regen", "군락지 버섯 수", 3, 4000, 2.2, L => $"군락지당 3~{NF.ed_size(L)}개", tier: 2);
            N("ed_dev", "ed", "ed_regen", "숲 장치 수", 2, 3000, 2.2, L => $"라운드마다 {NF.ed_dev(L)}개", tier: 2);
            N("ed_tax", "ed", "ed_price", "세금 감면", 2, 8000, 2.2, L => $"세금 -{U.JsRound((1 - NF.ed_tax(L)) * 100)}%", tier: 2);
            // 후반
            N("ed_score3", "ed", "ed_score2p", "타격 점수 III", 3, 1000000, 3, L => $"버섯을 칠 때 점수 +{Num(NF.ed_score3(L))}", tier: 3, needMax: true);
            N("ed_bonus3", "ed", "ed_bonus2p", "수확량 III", 3, 1500000, 3, L => $"버섯 하나당 수확 +{Num(NF.ed_bonus3(L))}개", tier: 3, needMax: true);
            N("ed_score3p", "ed", "ed_score3", "타격 점수 III+", 3, 20000000, 3, L => $"버섯을 칠 때 점수 +{Num(NF.ed_score3p(L))}", tier: 3, needMax: true);
            N("ed_bonus3p", "ed", "ed_bonus3", "수확량 III+", 3, 30000000, 3, L => $"버섯 하나당 수확 +{Num(NF.ed_bonus3p(L))}개", tier: 3, needMax: true);
            N("ed_trade", "ed", "ed_price", "단골 거래", 2, 3000000, 2.2, L => $"마을 의뢰 보상 {Plus(NF.ed_trade(L))}", tier: 3);
            N("ed_gold", "ed", "ed_rare", "황금 변종", 2, 3000000, 2.2, L => $"버섯마다 {U.Pct(NF.ed_gold(L))}", tier: 3);
            N("ed_giant", "ed", "ed_rare", "거대 버섯", 2, 2000000, 2.2, L => $"라운드마다 {U.Pct(NF.ed_giant(L))} 확률로 거대 버섯", tier: 3);
            N("ed_special", "ed", "ed_gold", "꼬마 손님", 2, 10000000, 2.2, L => $"특수 버섯 등장 {U.Pct(SPECIAL_P + NF.ed_special(L))} · 잡을 시간 {NF.ed_special_life(L)}초", tier: 3);
            N("ed_multi", "ed", "ed_bonus", "배수 획득", 2, 5000000, 2.2, L => L > 0 ? $"수확할 때 {U.Pct(MultiRate(L))} 확률로 버섯 ×3" : "없음", tier: 3);
            // ── 약용 (바·스킬·필드) ──
            N("md_map", "md", "core_md", "필드 넓히기", 4, 40, 2.2, L => $"필드 {U.JsRound(W / MAP_ZOOM[L])}×{U.JsRound(H / MAP_ZOOM[L])} · 화면 확대 ×{F(MAP_ZOOM[L])}");
            N("md_bar", "md", "core_md", "바 넓이", 3, 15, 2.2, L => $"{U.JsRound(150 * NF.md_bar(L))}px");
            N("md_combo", "md", "md_bar", "콤보 계수", 3, 60, 2.2, L => $"콤보당 +{F(NF.md_combo(L), 3)}배");
            N("md_kid", "md", "md_bar", "꼬마 수확기", 3, 300, 2.2, L => L > 0 ? $"영구 핀볼이 바에 맞을 때 {U.Pct(KidRate(L))} 확률로 작은 임시 핀볼 1개 (8초)" : "없음", icon: "s_kid", col: "#9fd8ff");
            N("md_combo2", "md", "md_combo", "콤보 계수 II", 2, 8000, 3, L => $"콤보당 +{F(NF.md_combo2(L), 2)}배 더", tier: 2, needMax: true);
            N("md_templife", "md", "md_kid", "임시 핀볼 수명", 2, 20000, 2.2, L => $"임시 핀볼 지속시간 {Plus(NF.md_templife(L))}", tier: 2);
            N("md_chef", "md", "md_combo", "꼬마 건축가", 2, 2000000, 2.2, L => $"건설 시간 ×{F(NF.md_chef(L))}", tier: 3);
            N("md_stump", "md", "md_combo", "숲 장치 강화", 2, 2000000, 2.2, L => $"숲 장치 효과 {Plus(NF.md_stump(L))}", tier: 3);
            foreach (var id in SKILL_IDS)
            {
                var s = SKILLS[id]; int tr = SKILL_TIER[id];
                N(id, "md", SKILL_TREE[id], s.n, 1, s.costGold, 1,
                    L => L > 0 ? $"사용 중 · {(s.noRate ? "" : "발동률 10% · ")}{s.eff(0)}" : $"해금하면 {(s.noRate ? "" : "발동률 10%로 ")}사용 시작", skill: id, icon: s.icon, col: s.col, tier: tr);
                if (!s.noRate) N(id + "_rate", "md", id, s.n + " 발동률", 2, s.costGold * 2, 3, L => $"발동률 {U.Pct(SkillRate(L))}", sub: true, tier: tr);
                if (!s.noPow) N(id + "_pow", "md", id, s.n + " 위력", 2, s.costGold * 2, 3, L => s.eff(L), sub: true, tier: tr);
            }
            N("md_buffdur", "md", "accel", "버프 지속", 2, 30000, 2.2, L => $"가속·맑은 포자막 지속 {Plus(NF.md_buffdur(L))}, 날 갈기 {Math.Ceiling(3 * NF.md_buffdur(L))}타", tier: 2);
            // ── 독 (전투) ──
            // 초반
            N("ps_atk", "ps", "core_ps", "공격력 I", 4, 15, 2.2, L => $"공격력 +{Num(NF.ps_atk(L))}");
            N("ps_size", "ps", "ps_atk", "수확기 크기", 3, 40, 2.2, L => $"반지름 {F(TUNE.ball0 * BALL_SCALE * NF.ps_size(L), 1)}");
            N("ps_spd", "ps", "ps_atk", "공속", 3, 50, 2.2, L => $"이동 속도 {Plus(NF.ps_spd(L))}");
            N("ps_dur", "ps", "ps_atk", "지속시간", 3, 80, 2.2, L => $"라운드 시간 +{NF.ps_dur(L)}초");
            N("ps_ball", "ps", "ps_atk", "핀볼 +1", 1, 500, 1, L => $"영구 핀볼 +{L}개");
            // 중반
            N("ps_atk2", "ps", "ps_atk", "공격력 II", 4, 2000, 2.5, L => $"공격력 +{Num(NF.ps_atk2(L))}", tier: 2, needMax: true);
            N("ps_ball2", "ps", "ps_atk2", "핀볼 +1 (II)", 1, 50000, 1, L => $"영구 핀볼 +{L}개", tier: 2);
            N("ps_atk2p", "ps", "ps_atk2", "공격력 II+", 3, 60000, 2.5, L => $"공격력 +{Num(NF.ps_atk2p(L))}", tier: 2, needMax: true);
            N("ps_dur2", "ps", "ps_dur", "지속시간 II", 2, 15000, 3, L => $"라운드 시간 +{NF.ps_dur2(L)}초 더", tier: 2, needMax: true);
            N("ps_crit", "ps", "ps_size", "치명타", 3, 6000, 2.2, L => L > 0 ? $"{U.Pct(NF.ps_crit(L))} (피해·점수 ×3)" : "0%", tier: 2);
            N("ps_resist", "ps", "ps_spd", "포자 저항", 2, 10000, 2.2, L => $"느림·약화 디버프 지속 -{U.JsRound((1 - NF.ps_resist(L)) * 100)}%", tier: 2);
            N("ps_rush", "ps", "ps_dur", "선제 공격", 2, 25000, 2.2, L => $"라운드 시작 후 3초 동안 공격력 ×{F(NF.ps_rush(L))}", tier: 2);
            N("bl", "ps", "ps_size", "회전 칼날", 1, 60000, 1, L => L > 0 ? "사용 중 · 범위 안 버섯에 주기적 피해" : "해금하면 칼날 사용 시작", icon: "s_blade", col: "#e6e6f0", tier: 2);
            // 후반
            N("ps_atk3", "ps", "ps_atk2p", "공격력 III", 4, 1000000, 2.5, L => $"공격력 +{Num(NF.ps_atk3(L))}", tier: 3, needMax: true);
            N("ps_atk3p", "ps", "ps_atk3", "공격력 III+", 3, 20000000, 2.5, L => $"공격력 +{Num(NF.ps_atk3p(L))}", tier: 3, needMax: true);
            N("ps_atk4", "ps", "ps_atk3p", "공격력 IV", 3, 200000000, 3, L => $"공격력 +{Num(NF.ps_atk4(L))}", tier: 3, needMax: true);
            N("ps_ball3", "ps", "ps_atk3", "핀볼 +2 (III)", 2, 50000000, 5, L => $"영구 핀볼 +{L}개", tier: 3);
            N("ps_crit2", "ps", "ps_crit", "치명타 피해", 2, 20000000, 3, L => $"치명타 피해·점수 ×{NF.ps_crit2(L)}", tier: 3, needMax: true);
            N("ps_heavy", "ps", "ps_crit", "무더기 파쇄", 2, 4000000, 2.2, L => $"군락 무더기·거대 버섯 피해 ×{F(NF.ps_heavy(L))}", tier: 3);
            N("bl_pow", "ps", "bl", "칼날 위력", 2, 3000000, 3, L => $"피해 공격력×{F(NF.bl_pow(L))}", sub: true, tier: 3);
            N("bl_range", "ps", "bl", "칼날 범위", 2, 2000000, 3, L => $"수확기 반지름 + {U.JsRound(NF.bl_range(L))}px", sub: true, tier: 3);
            N("bl_spd", "ps", "bl", "칼날 속도", 2, 2500000, 3, L => $"공격 간격 {F(NF.bl_spd(L))}초", sub: true, tier: 3);
            // 균사석 노드 (후반): 여기서부터 곱셈 효과가 들어온다
            N("gm_harvest", "ed", "ed_bonus", "균사 공명", 3, 3000, 2, L => $"모든 버섯 획득량 ×{F(NF.gm_harvest(L))}", gem: 10, tier: 3);
            N("gm_spore", "ed", "ed_multi", "포자 감응", 3, 2000, 2, L => $"나무 버섯을 딸 때 {U.Pct(NF.gm_spore(L))} 확률로 버섯 포자 +2", gem: 8, tier: 3);
            N("gm_gourmet", "ed", "ed_trade", "풍요의 손길", 3, 2500, 2, L => $"나무 버섯 균사석 ×{F(NF.gm_gourmet(L))}", gem: 10, tier: 3);
            N("gm_herb", "md", "md_chef", "자동 수확 확장", 3, 2500, 2, L => $"자동 수확 보상 ×{F(NF.gm_herb(L))}", gem: 10, tier: 3);
            N("gm_farm", "md", "md_stump", "대지의 축복", 3, 1500, 2, L => $"나무 버섯 성장 시간 ×{F(NF.gm_farm(L))}", gem: 6, tier: 3);
            N("gm_meteor", "md", "md_combo", "균사석 낙하", 3, 5000, 2, L =>
            {
                if (L == 0) return "없음";
                var m = NF.gm_meteor(L);
                return $"{F(m.interval)}초마다 균사석이 떨어져 반경 {m.r} 피해 공격력×{m.dmg}";
            }, gem: 25, col: "#8ff0c8", tier: 3);
            N("gm_toxin", "ps", "ps_resist", "독성 내성", 3, 2000, 2, L => $"포자 디버프 지속 ×{F(NF.gm_toxin(L))} · 독버섯 포자 구름 ×{F(NF.gm_toxin(L))}", gem: 8, tier: 3);

            foreach (var n in NODES)
            {
                if (n.id.EndsWith("_rate")) n.d = $"{U.Iga(SKILLS[n.parent].n)} 발동할 확률을 높여요.";
                else if (n.id.EndsWith("_pow") && SKILLS.ContainsKey(n.parent)) n.d = $"{SKILLS[n.parent].n}의 효과(피해·범위·개수·시간)를 키워요.";
                else n.d = NODE_DESC[n.id];
                if (PRICE_TABLE.TryGetValue(n.id, out var pc) && pc.Length == n.max) n.costs = pc;
                NODE[n.id] = n;
            }

            Harvester AddHv(string id, string n, string type, string look, double unlock, double lvGold, Func<int, Dictionary<string, double>> ab, Func<int, string> text)
            {
                var h = new Harvester { id = id, n = n, type = type, look = look, unlockGold = unlock, lvGold = lvGold, ab = ab, text = text };
                HARVESTERS.Add(h); HV[id] = h; return h;
            }
            AddHv("sam", "낫 수확기", "기본형", "나무 막대에 날이 달린 낫", 0, 1500,
                s => D(("p", 0.15 + 0.07 * (s - 1))),
                s => $"휘두르기: 버섯을 수확할 때 {U.Pct(HV["sam"].ab(s)["p"])} 확률로 가까운 버섯 하나를 함께 벤다 (공격력×2)");
            AddHv("saw", "표창 수확기", "치명타형", "날이 넓은 네 방향 표창", 3000, 0,
                s => D(("p", 0.2 + 0.1 * (s - 1))),
                s => $"급소 찌르기: 에픽 이상 버섯을 맞히면 {U.Pct(HV["saw"].ab(s)["p"])} 확률로 확정 치명타 (피해·점수 ×3)");
            AddHv("mill", "회전칼날 수확기", "수확형", "동그라미 + 회전 칼날", 5000, 0,
                s => D(("r", 34 + 8 * s), ("p", 0.1 + 0.04 * (s - 1))),
                s => $"갈아내기: 늘 작은 회전 칼날(범위 {HV["mill"].ab(s)["r"]}px)을 두르고, 칼날이 벨 때 {U.Pct(HV["mill"].ab(s)["p"])} 확률로 버섯 조각(추가 수확) 획득");
            AddHv("spore", "분열 수확기", "분열형", "포자가 맺힌 버섯 갓", 4000, 0,
                s => D(("p", 0.08 + 0.04 * (s - 1)), ("life", 3 + s)),
                s => $"세포 분열: 버섯을 맞히면 {U.Pct(HV["spore"].ab(s)["p"])} 확률로 작은 임시 핀볼로 분열 ({HV["spore"].ab(s)["life"]}초, 수확기 종류는 무작위)");
            AddHv("bell", "방울 수확기", "울림형", "큰 도토리 방울", 8000, 0,
                s => D(("r", 40 + 12 * s), ("dmg", 0.4 + 0.15 * s)),
                s => $"딸랑 울림: 버섯을 맞힐 때마다 작은 충격파 (반경 {HV["bell"].ab(s)["r"]}, 피해 공격력×{U.FmtN(HV["bell"].ab(s)["dmg"])})");
            AddHv("coin", "엽전 수확기", "재물형", "구멍 뚫린 큰 엽전", 2500, 0,
                s => D(("k", 0.3 + 0.15 * (s - 1))),
                s => $"짤랑짤랑: 버섯에 닿을 때마다 골드 획득 (그 버섯 판매가의 {U.Pct(HV["coin"].ab(s)["k"])})");
            AddHv("gold", "황금 수확기", "보상", "금빛 낫", -1, 15000,
                s => D(("p", 0.03 + 0.02 * (s - 1))),
                s => $"황금 손길: 맞힌 버섯이 {U.Pct(HV["gold"].ab(s)["p"])} 확률로 황금 변종으로 변한다");
        }
    }
}
