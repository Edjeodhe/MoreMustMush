using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 스킬·바 버프 =====
        // 스킬의 이름·발동 종류·색·아이콘은 엑셀 DB Skill 시트, 가격·레벨·트리 위치는 Upgrade 시트. 효과 문구(eff)만 코드
        public class Skill
        {
            public string id, kind, icon, col;
            public string nKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public bool noRate;
            public Func<int, string> eff;
        }

        public static readonly Dictionary<string, Skill> SKILLS = new Dictionary<string, Skill>();
        public static readonly List<string> SKILL_IDS = new List<string>();

        // 성장 설계
        // - 레벨 효과는 레벨당 고정값을 더하는 식이다. 최대 레벨은 2~4로 짧다.
        // - 공격력·타격 점수·수확량은 단계 노드 I(+1) → II(+10) → III(+100) → IV(+1000)로 단위가 커진다.
        //   다음 단계는 앞 단계를 최대 레벨까지 올려야 열린다(가격이 아니라 구조로 막는다).
        // - 버섯 체력·보상도 지역마다 ×10씩 커지므로(ZoneMul) 단계가 오를 때마다 숫자 단위가 한 자리씩 뛴다.
        // - 곱셈은 균사석 노드(후반)에서만 들어온다.
        // - 지역·스테이지로 노드를 잠그지 않는다. 기본 노드는 골드로만 사고, 단계(초반·중반·후반)는 가격으로만 구분한다.
        //   가격은 산 순서대로 늘어놓았을 때 한 번에 크게 뛰지 않게 맞춘다(엑셀 DB Upgrade_Cost, BalanceSim.TunePrices).
        public static double SkillRate(int S) => 0.10 + 0.10 * S;
        // 슬라이드 바 스킬 지속시간: 3 + n초 (n = 위력 레벨). 기운 북돋기: 공격력 +POWER_ATK·스킬 발동률 +POWER_SKILL × (1 + 위력 레벨)
        public static double BarSkillDur(int P) => 3 + P;
        public static readonly double POWER_ATK = Db.C("POWER_ATK"), POWER_SKILL = Db.C("POWER_SKILL");
        public static readonly float BAR_CLONE_DUR = (float)Db.C("BAR_CLONE_DUR");
        public static readonly int BAR_CLONE_MAX = Db.CInt("BAR_CLONE_MAX", 0);
        public static double SkillPow(int P) => 1 + 0.75 * P;   // 스킬 위력 배율 (피해·시간·속도)
        public static double KidRate(int L) => V("md_kid", L);
        public static double MultiRate(int L) => V("ed_multi", L);

        // 노드 효과 값 (DB Efx_Lv0~4). 레벨은 0~최대 레벨로 자른다
        static double V(string id, int L)
        {
            var n = NODE[id];
            return n.efx[L < 0 ? 0 : L > n.max ? n.max : L];
        }


        // ===== 균사 노드 효과 공식 (NF) =====
        public static class NF
        {
            // 노드 효과 값은 엑셀 DB Upgrade 시트의 Efx_Lv0~4 (레벨별 값)에서 읽는다. 함수 이름 = 노드 키
            // 단계 노드: 고정값 더하기 (I +1 · II +10 · III +100 · IV +1000)
            public static double ps_atk(int L) => V("ps_atk", L);
            // 공격력 II 이상은 증가량 25% 너프 (사용자 지시 2026-10-13: II부터 너무 강함 → DB 값이 7.5·75·750 단위)
            public static double ps_atk2(int L) => V("ps_atk2", L);
            public static double ps_atk3(int L) => V("ps_atk3", L);
            public static double ps_atk4(int L) => V("ps_atk4", L);
            public static double ed_score(int L) => V("ed_score", L);      // 타격 점수 +
            public static double ed_score2(int L) => V("ed_score2", L);
            public static double ed_score3(int L) => V("ed_score3", L);
            public static double ed_bonus(int L) => V("ed_bonus", L);      // 버섯 하나당 수확 개수 +
            public static double ed_bonus2(int L) => V("ed_bonus2", L);
            public static double ed_bonus3(int L) => V("ed_bonus3", L);
            // 형제 노드 (I+ · II+ · III+): 같은 단계 효과를 한 번 더. 최대 레벨을 늘리지 않고 줄의 레벨 수를 늘린다
            public static double ed_score1p(int L) => V("ed_score1p", L);
            public static double ed_score2p(int L) => V("ed_score2p", L);
            public static double ed_score3p(int L) => V("ed_score3p", L);
            public static double ed_bonus1p(int L) => V("ed_bonus1p", L);
            public static double ed_bonus2p(int L) => V("ed_bonus2p", L);
            public static double ed_bonus3p(int L) => V("ed_bonus3p", L);
            public static double ps_atk2p(int L) => V("ps_atk2p", L);
            public static double ps_atk3p(int L) => V("ps_atk3p", L);
            public static double ed_price(int L) => V("ed_price", L);
            public static double ed_price2(int L) => V("ed_price2", L);   // 정산 골드 +50%씩 (I에 더함)
            public static int ed_cols2(int L) => (int)Math.Round(V("ed_cols2", L));         // 군락지 수 +4씩
            public static double md_combo2(int L) => V("md_combo2", L);   // 콤보 계수 +0.1씩
            public static double ps_dur2(int L) => V("ps_dur2", L);       // 라운드 시간 +3초씩
            public static double ps_crit2(int L) => V("ps_crit2", L);  // 치명타 배율 ×3 → ×5
            public static double ed_regen(int L) => V("ed_regen", L);
            public static double ed_rare(int L) => V("ed_rare", L);
            public static double ed_gold(int L) => V("ed_gold", L);
            public static double ed_trade(int L) => V("ed_trade", L);
            public static double ed_giant(int L) => V("ed_giant", L);
            public static int ed_size(int L) => (int)Math.Round(V("ed_size", L));   // 군락지당 최대 버섯 수
            public static double md_bar(int L) => V("md_bar", L);
            // 슬라이드 바 수치 강화 (독 가지): 바 길이 +20%p씩(기본 150 기준) · 바 반사 속도 · 바 스킬 지속 +초 · 바 스킬 발동률 +%p · 기운 북돋기 공격력 +%p (발동률은 1/4)
            public static double ps_bar(int L) => V("ps_bar", L);
            public static double ps_barspd(int L) => V("ps_barspd", L);
            public static double ps_bardur(int L) => V("ps_bardur", L);
            public static double ps_barrate(int L) => V("ps_barrate", L);
            public static double ps_power(int L) => V("ps_power", L);
            public static double md_combo(int L) => V("md_combo", L);
            public static double md_chef(int L) => V("md_chef", L);        // 꼬마 공방 제작 시간 배율 (세계수)
            public static double md_buffdur(int L) => V("md_buffdur", L);
            public static double md_templife(int L) => V("md_templife", L);
            public static double md_stump(int L) => V("md_stump", L);
            public static double ps_size(int L) => V("ps_size", L);
            public static double ps_spd(int L) => V("ps_spd", L);
            public static double ps_dur(int L) => V("ps_dur", L);   // 라운드 시간 +초
            public static double ps_crit(int L) => V("ps_crit", L);
            public static double ps_heavy(int L) => V("ps_heavy", L);
            public static double ps_resist(int L) => V("ps_resist", L);
            public static double ps_rush(int L) => V("ps_rush", L);
            // 회전 칼날·스킬 범위는 노드 레벨이 아니라 하위 단계(BladeLv·위력)로 정해서 코드에 둔다
            public static double bl_range(int L) => 20 + 25 * L;
            public static double bl_spd(int L) => 0.6 - 0.12 * L;
            public static double bl_pow(int L) => 0.4 * SkillPow(L);
            // 스킬 범위: 기본값에 위력 레벨당 고정값을 더한다
            public static double burst_r(int P) => 80 + 25 * P;
            public static double tornado_h(int P) => 50 + 22 * P;
            public static double shock_r(int P) => 100 + 30 * P;
            // ↓ 균사석 노드 (후반): 다른 보너스를 다 더한 값에 곱한다
            public static double gm_harvest(int L) => V("gm_harvest", L);
            public static double gm_spore(int L) => V("gm_spore", L);
            public static double gm_toxin(int L) => V("gm_toxin", L);
            public static double gm_herb(int L) => V("gm_herb", L);              // 수확 보따리 배율
            public static double gm_gourmet(int L) => V("gm_gourmet", L);           // 나무버섯 균사석 배율
            public static double gm_atk(int L) => V("gm_atk", L);               // 공격력 배율
            // 싱크 노드 (최대 레벨 없음, DB Efx_MaxLv = 0): 레벨마다 Efx_Lv1배씩 곱한다. V()는 쓰지 않는다 (efx 표가 레벨 4까지뿐)
            public static double inf_atk(int L) => Inf("inf_atk", L);             // 공격력 ×1.05^L (끝없는 투지)
            public static double inf_gold(int L) => Inf("inf_gold", L);           // 정산 골드 ×1.025^L (끝없는 풍년, 점수→골드에만)
            static double Inf(string id, int L) => L <= 0 || !NODE.TryGetValue(id, out var n) ? 1 : Math.Pow(n.infMul, Math.Min(L, n.infCap));
            public static double gm_score(int L) => V("gm_score", L);             // 정산 골드 배율
            public static double gm_boss(int L) => V("gm_boss", L);             // 보스에게 주는 피해 배율
            public static int gm_bossgem(int L) => (int)Math.Round(V("gm_bossgem", L));                    // 보스 처치 균사석 +
            public static double gm_skill(int L) => V("gm_skill", L);             // 모든 스킬 발동률 배율
            public static double gm_bar(int L) => V("gm_bar", L);               // 바 길이 배율 (필드 폭 1/3까지)
            public static double gm_farm(int L) => V("gm_farm", L);
            public struct Meteor { public double interval, dmg, r; }
            public static Meteor gm_meteor(int L) => new Meteor { interval = 3.5 * Math.Pow(0.8, Math.Max(0, L - 1)), dmg = 3 * Math.Pow(2, Math.Max(0, L - 1)), r = 110 + 20 * L };
        }

        public class Node
        {
            public string id, br, parent, skill, icon, col;
            public string nKo, dKo;   // 한국어 원문 (찾기·내보내기는 nKo로)
            public Func<string> dFn;        // 설명을 그때그때 만드는 노드 (있으면 dKo보다 먼저)
            public string n => Loc.T(nKo);
            // 트리의 작은 노드 글자: 이름의 마지막 낱말
            public string subLabel => n.Substring(n.LastIndexOf(' ') + 1);
            public string d { get => dFn != null ? dFn() : Loc.T(dKo); set => dKo = value; }
            public bool sub, gem;
            public bool core;       // 예전 코어 노드 (2026-10-13 트리 개편으로 없어져 늘 false). 트리 그리기 코드가 아직 본다
            public string req;     // 이 노드를 최대 레벨까지 올려야 열린다 (단계 노드 I → II → III). 부모와 다를 수 있다(공격력 I → 공속 → 공격력 II)
            public bool needMax => req != null;
            public int tier = 1;   // 성장 단계: 1 초반 · 2 중반 · 3 후반
            public int max;          // 싱크 노드는 0 (상한 비교는 Game.AtMax로)
            // 싱크 노드 (DB Efx_MaxLv = 0, 2026-10-13 밸런스 2회차): 레벨 상한 없이 계속 산다.
            // 가격 = infCost0 × infCostK^L (Cost_Lv1, Cost_Lv2/Cost_Lv1), 효과 = infMul^L (Efx_Lv1). infCap = 가격이 double을 넘기 직전 레벨
            public bool inf;
            public double infCost0, infCostK, infMul;
            public int infCap;
            public double[] costs;   // 레벨별 골드 가격 (길이 = max). DB Upgrade_Cost.Cost_Lv1~4
            public double[] gems;    // 레벨별 균사석 가격. DB Upgrade_Cost.Gem_Lv1~4
            public double[] efx;     // 레벨별 효과 값 0~4. DB Upgrade.Efx_Lv0~4
            public Func<int, string> eff;
        }

        public static readonly List<Node> NODES = new List<Node>();
        public static readonly Dictionary<string, Node> NODE = new Dictionary<string, Node>();
        public static readonly string[] TIER_NAMES = { "", "초반", "중반", "후반" };

        public const string ROOT_NODE = "power";
        public const int SKILL_MAX = 4;

        // 스킬 노드 레벨 → 발동률 단계(S)·위력 단계(P). 1레벨 = 해금(기본), 2 발동률, 3 위력, 4 둘 다 (예전 발동률·위력 노드 2레벨씩과 같은 최대치)
        // 발동률이 없는 스킬은 2~4레벨이 모두 위력 (4레벨 위력 3단계: 예전 위력 노드 최대 2단계보다 한 단계 더)
        public static (int S, int P) SkillSP(Skill s, int L)
        {
            if (L <= 1) return (0, 0);
            if (s.noRate) return (0, L - 1);
            return (L >= 4 ? 2 : 1, L >= 4 ? 2 : L >= 3 ? 1 : 0);
        }
        // 회전 칼날 레벨 → 범위·속도·위력 단계. 2 범위+속도, 3 위력, 4 모두 2단계
        public static (int range, int spd, int pow) BladeLv(int L) => L <= 1 ? (0, 0, 0) : L == 2 ? (1, 1, 0) : L == 3 ? (1, 1, 1) : (2, 2, 2);

        // ===== 수확기 =====
        public class Harvester
        {
            public string id;
            public string nKo, typeKo, lookKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string type { get => Loc.T(typeKo); set => typeKo = value; }
            public string look { get => Loc.T(lookKo); set => lookKo = value; }
            public double unlockGold = -1;   // -1: 공방에서 해금 불가(보상), 0: 처음부터
            public double lvGold;
            // 유형 기본 능력치 (전투 기획서 Ver05 · 02. 수확기): 공격력·이동 속도·크기 배율, 치명타 확률 +
            public double atkMul = 1, spdMul = 1, sizeMul = 1, crit;
            public Func<int, Dictionary<string, double>> ab;
            public Func<int, string> text;
        }
        public static readonly List<Harvester> HARVESTERS = new List<Harvester>();
        public static readonly Dictionary<string, Harvester> HV = new Dictionary<string, Harvester>();
        public static readonly double[] HV_LV_MUL = { 0, 0.6, 1.2, 2.4, 4.8 };

        static Dictionary<string, double> D(params (string, double)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

        static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        // 엑셀 DB(Db.Data)로 스킬·노드·수확기를 만든다. DB와 코드가 안 맞으면(효과 없는 키, 없는 부모 등) 바로 알린다
        static void InitSkillsAndNodes()
        {
            var db = Db.Data;
            var skillEff = SkillEffects();
            foreach (var r in db.Skill)
            {
                if (!skillEff.TryGetValue(r.Skill_Key, out var eff)) throw new InvalidOperationException($"[DB] Skill 시트의 {r.Skill_Key}: 코드에 효과가 없는 스킬이에요 (Defs.Nodes.cs SkillEffects)");
                SKILLS[r.Skill_Key] = new Skill { id = r.Skill_Key, nKo = r.Skill_Name, kind = r.Kind, col = r.Color, icon = SkillIcon(r.Skill_Key, r.Icon), noRate = r.No_Rate != 0, eff = eff };
                SKILL_IDS.Add(r.Skill_Key);
            }

            var nodeEff = NodeEffects();
            var keyOf = new Dictionary<int, string>();
            foreach (var u in db.Upgrade)
            {
                if (keyOf.ContainsKey(u.Upg_ID)) throw new InvalidOperationException($"[DB] Upgrade: Upg_ID {u.Upg_ID}가 겹쳐요 ({keyOf[u.Upg_ID]}, {u.Upg_Key})");
                if (keyOf.ContainsValue(u.Upg_Key)) throw new InvalidOperationException($"[DB] Upgrade: Upg_Key {u.Upg_Key}가 겹쳐요");
                keyOf[u.Upg_ID] = u.Upg_Key;
            }
            var costOf = new Dictionary<int, Db.UpgradeCost>();
            foreach (var c in db.Upgrade_Cost)
                if (!costOf.TryAdd(c.Cost_ID, c)) throw new InvalidOperationException($"[DB] Upgrade_Cost: Cost_ID {c.Cost_ID}가 겹쳐요");
            string Key(int id, string who) => id == 0 ? null : keyOf.TryGetValue(id, out var k) ? k : throw new InvalidOperationException($"[DB] Upgrade {who}: 없는 Upg_ID {id}");
            foreach (var u in db.Upgrade)
            {
                string key = u.Upg_Key, skill = NullIfEmpty(u.Skill);
                bool inf = u.Efx_MaxLv == 0;   // 싱크 노드: 최대 레벨 없음
                if (u.Efx_MaxLv < 0 || u.Efx_MaxLv > 4) throw new InvalidOperationException($"[DB] Upgrade {key}: Efx_MaxLv는 0(싱크)·1~4 ({u.Efx_MaxLv})");
                if (!costOf.TryGetValue(u.Cost_ID, out var c)) throw new InvalidOperationException($"[DB] Upgrade {key}: Upgrade_Cost에 Cost_ID {u.Cost_ID}가 없어요");
                Func<int, string> eff;
                if (skill != null)
                {
                    if (!SKILLS.TryGetValue(skill, out var s)) throw new InvalidOperationException($"[DB] Upgrade {key}: Skill 시트에 없는 스킬 {skill}");
                    if (skill != key) throw new InvalidOperationException($"[DB] Upgrade {key}: 스킬 노드는 Upg_Key와 Skill이 같아야 해요 ({skill})");
                    eff = SkillNodeEffect(s);
                }
                else if (!nodeEff.TryGetValue(key, out eff)) throw new InvalidOperationException($"[DB] Upgrade {key}: 코드에 효과가 없는 노드예요 (새 효과는 코드가 필요해요)");
                if (inf)
                {
                    if (skill != null) throw new InvalidOperationException($"[DB] Upgrade {key}: 싱크(Efx_MaxLv 0)는 스킬 노드일 수 없어요");
                    if (c.Gem.Any(g => g > 0)) throw new InvalidOperationException($"[DB] Upgrade {key}: 싱크는 골드로만 사요 (Gem_Lv1~4 = 0)");
                    if (!(c.Gold[0] > 0 && c.Gold[1] > c.Gold[0])) throw new InvalidOperationException($"[DB] Upgrade {key}: 싱크 가격은 Cost_Lv1 > 0, Cost_Lv2 > Cost_Lv1 (증가율 = Lv2/Lv1)");
                    if (!(u.Efx[1] > 1)) throw new InvalidOperationException($"[DB] Upgrade {key}: 싱크 효과 배율 Efx_Lv1은 1보다 커야 해요");
                    if (u.Upg_Req == 0) throw new InvalidOperationException($"[DB] Upgrade {key}: 싱크는 Upg_Req(최대 레벨로 열 노드)가 있어야 해요");
                }
                var gems = c.Gem.Take(u.Efx_MaxLv).ToArray();
                var n = new Node
                {
                    id = key, br = u.Branch, parent = Key(u.Upg_Pre, key), req = Key(u.Upg_Req, key), nKo = u.Upg_Name, dKo = Db.Text($"upg:{key}:desc"),
                    max = u.Efx_MaxLv, tier = u.Tier, sub = u.Sub != 0, skill = skill, icon = NullIfEmpty(u.Icon), col = NullIfEmpty(u.Color),
                    costs = c.Gold.Take(u.Efx_MaxLv).ToArray(), gems = gems, gem = gems.Any(g => g > 0), efx = u.Efx, eff = eff,
                };
                if (inf)
                {
                    n.inf = true; n.infCost0 = c.Gold[0]; n.infCostK = c.Gold[1] / c.Gold[0]; n.infMul = u.Efx[1];
                    // 가격이 double.MaxValue/4를 넘기 전 레벨에서 멈춘다 (c0 3.6e12 · ×1.076이면 약 9,290레벨). 그때 효과 1.05^L도 유한하다
                    n.infCap = (int)Math.Floor(Math.Log(double.MaxValue / 4 / (n.infCost0 * COST_MUL)) / Math.Log(n.infCostK));
                }
                if (skill != null || key == "bl")
                {
                    string dKo = n.dKo; int kind = key == "bl" ? 0 : SKILLS[skill].noRate ? 1 : 2;
                    n.dFn = () => Loc.T(dKo) + "\n<size=85%><color=#8a6a4a>"
                        + (kind == 0 ? Loc.T("2레벨 범위·속도 · 3레벨 위력 · 4레벨 모두 한 단계 더") : kind == 1 ? Loc.T("2~4레벨 위력") : Loc.T("2레벨 발동률 · 3레벨 위력 · 4레벨 발동률·위력")) + "</color></size>";
                }
                NODES.Add(n); NODE[key] = n;
            }
            CheckTree(nodeEff.Keys);
            for (int L = 0; L < MAP_ZOOM.Length; L++) MAP_ZOOM[L] = (float)V("md_map", L);
            InitHarvesters();
        }

        // 트리 검사: 시작 노드는 하나(부모 없음 = ROOT_NODE뿐), 부모를 따라가면 반드시 시작 노드에 닿고(순환 없음),
        // 코드가 효과를 가진 노드·스킬은 DB에서 빠지지 않았어야 한다 (빠지면 기능이 조용히 사라진다)
        static void CheckTree(IEnumerable<string> codeKeys)
        {
            if (!NODE.ContainsKey(ROOT_NODE)) throw new InvalidOperationException($"[DB] Upgrade에 시작 노드 {ROOT_NODE}가 없어요");
            foreach (var n in NODES)
            {
                if (n.parent == null && n.id != ROOT_NODE) throw new InvalidOperationException($"[DB] Upgrade {n.id}: Upg_Pre가 0이에요 (시작 노드 {ROOT_NODE}만 부모가 없어야 해요)");
                var p = n; int steps = 0;
                while (p.parent != null)
                {
                    p = NODE[p.parent];
                    if (++steps > NODES.Count) throw new InvalidOperationException($"[DB] Upgrade {n.id}: 부모를 따라가면 빙글빙글 돌아요 (Upg_Pre 순환)");
                }
            }
            foreach (var n in NODES)
                if (n.req != null && NODE[n.req].inf) throw new InvalidOperationException($"[DB] Upgrade {n.id}: Upg_Req가 싱크 노드 {n.req}예요 (싱크는 최대 레벨이 없어 '최대 레벨로 열기'가 안 돼요)");
            var lost = codeKeys.Concat(SKILL_IDS).Where(k => !NODE.ContainsKey(k)).ToList();
            if (lost.Count > 0) throw new InvalidOperationException("[DB] Upgrade에서 빠진 노드 (코드가 씀): " + string.Join(", ", lost));
        }

        // 스킬 노드 (4레벨). 1레벨 해금, 2~4레벨에 발동률·위력이 번갈아 오른다 (SkillSP)
        static Func<int, string> SkillNodeEffect(Skill s) => L =>
        {
            if (L == 0) return Loc.F("해금하면 {0}사용 시작", s.noRate ? "" : Loc.T("발동률 10%로 "));
            var (S, P) = SkillSP(s, L);
            return (s.noRate ? "" : Loc.F("발동률 {0} · ", U.Pct(SkillRate(S)))) + s.eff(P);
        };

        // 스킬 효과 문구 (위력 단계 P → 글). 수치 공식이 들어 있어 코드에 둔다
        static Dictionary<string, Func<int, string>> SkillEffects()
        {
            string F(double v, int d = 2) => U.FmtN(v, d);
            return new Dictionary<string, Func<int, string>>
            {
                ["accel"] = P => Loc.F("3초간 속도 ×{0}", F(1 + 0.15 * SkillPow(P))),
                ["sharpen"] = P => Loc.F("다음 3타 피해 ×{0}", F(2 * SkillPow(P), 1)),
                ["split"] = P => Loc.F("임시 수확기 {0}개 (6초)", 1 + P),
                ["clear"] = P => Loc.F("바에 맞을 때마다 디버프 해제 · {0}초 보호", F(BarSkillDur(P), 1)),
                ["burst"] = P => Loc.F("반경 {0}, 피해 공격력×{1}", U.JsRound(NF.burst_r(P)), F(2 * SkillPow(P), 1)),
                ["clone"] = P => Loc.F("작은 임시 수확기 {0}개, 4초", 1 + P),
                ["magnet"] = P => Loc.F("1초간 가까운 버섯 쪽으로 초당 {0}°", U.JsRound(20 * SkillPow(P))),
                ["bolt"] = P => Loc.F("주변 {0}개, 피해 공격력×{1}", 2 + P, F(SkillPow(P), 1)),
                ["tornado"] = P => Loc.F("띠 높이 {0}, 피해 공격력×{1}", U.JsRound(NF.tornado_h(P)), F(2 * SkillPow(P), 1)),
                ["shock"] = P => Loc.F("반경 {0}, 피해 공격력×{1}, 연쇄 최대 8단계", U.JsRound(NF.shock_r(P)), F(SkillPow(P), 1)),
                ["pierce"] = P => Loc.F("{0}초 동안 버섯 군락을 튕기지 않고 뚫고 지나가며 피해", F(BarSkillDur(P), 1)),
                ["homing"] = P => Loc.F("{0}초 동안 바에 튕길 때마다 가장 가까운 군락(보스 스테이지는 보스)으로 날아감", F(BarSkillDur(P), 1)),
                ["power"] = P => Loc.F("바에 맞을 때마다 {0}초 동안 공격력 +{1}% · 스킬 발동률 +{2}%p", F(BarSkillDur(P), 1), U.JsRound(POWER_ATK * (1 + P) * 100), U.JsRound(POWER_SKILL * (1 + P) * 100)),
                ["barclone"] = P => Loc.F("{0}초 동안 필드 가운데나 양옆에 분신 슬라이드 바 (최대 {1}개)", F(BAR_CLONE_DUR + P, 1), BAR_CLONE_MAX),
                ["fest"] = P => Loc.F("라운드 마지막 5초 모든 점수·수확량 ×{0}", F(2 * SkillPow(P), 1)),
            };
        }

        // 노드 효과 문구 (레벨 L → 글). 값은 NF(DB Efx)에서 읽고, 글 모양만 코드에 둔다. 스킬 노드는 SkillNodeEffect
        static Dictionary<string, Func<int, string>> NodeEffects()
        {
            string F(double v, int d = 2) => U.FmtN(v, d);
            // 퍼센트 효과는 "기본 대비 +%"로 보여 준다 (다른 보너스와도 더해진다)
            string Plus(double mul) => $"+{U.JsRound((mul - 1) * 100)}%";
            string Num(double v) => U.Fmt(v);
            var E = new Dictionary<string, Func<int, string>>();
            // ── 식용 (돈·수확) ──
            // 초반
            E["ed_score"] = L => Loc.F("버섯을 칠 때 점수 +{0}", Num(NF.ed_score(L)));
            E["ed_cols"] = L => Loc.F("최대 {0}개", ColCount(L));
            E["ed_regen"] = L => Loc.F("사라진 군락지가 {0}초 뒤 다시 돋아남", F(NF.ed_regen(L)));
            E["ed_price"] = L => Loc.F("정산 골드 {0}", Plus(NF.ed_price(L)));
            E["ed_bonus"] = L => Loc.F("버섯 하나당 수확 +{0}개", Num(NF.ed_bonus(L)));
            E["ed_score1p"] = L => Loc.F("버섯을 칠 때 점수 +{0}", Num(NF.ed_score1p(L)));
            E["ed_bonus1p"] = L => Loc.F("버섯 하나당 수확 +{0}개", Num(NF.ed_bonus1p(L)));
            // 중반
            E["ed_score2"] = L => Loc.F("버섯을 칠 때 점수 +{0}", Num(NF.ed_score2(L)));
            E["ed_bonus2"] = L => Loc.F("버섯 하나당 수확 +{0}개", Num(NF.ed_bonus2(L)));
            E["ed_score2p"] = L => Loc.F("버섯을 칠 때 점수 +{0}", Num(NF.ed_score2p(L)));
            E["ed_bonus2p"] = L => Loc.F("버섯 하나당 수확 +{0}개", Num(NF.ed_bonus2p(L)));
            E["ed_cols2"] = L => Loc.F("군락지 +{0}개", NF.ed_cols2(L));
            E["ed_price2"] = L => Loc.F("정산 골드 +{0}%", U.JsRound(NF.ed_price2(L) * 100));
            E["ed_rare"] = L => Loc.F("에픽 이상 가중치 {0}", Plus(NF.ed_rare(L)));
            E["ed_size"] = L => Loc.F("군락지당 3~{0}개", NF.ed_size(L));
            // 후반
            E["ed_score3"] = L => Loc.F("버섯을 칠 때 점수 +{0}", Num(NF.ed_score3(L)));
            E["ed_bonus3"] = L => Loc.F("버섯 하나당 수확 +{0}개", Num(NF.ed_bonus3(L)));
            E["ed_score3p"] = L => Loc.F("버섯을 칠 때 점수 +{0}", Num(NF.ed_score3p(L)));
            E["ed_bonus3p"] = L => Loc.F("버섯 하나당 수확 +{0}개", Num(NF.ed_bonus3p(L)));
            E["ed_trade"] = L => Loc.F("정산 골드 {0}", Plus(NF.ed_trade(L)));
            E["ed_gold"] = L => Loc.F("버섯마다 {0}", U.Pct(NF.ed_gold(L)));
            E["ed_giant"] = L => Loc.F("라운드마다 {0} 확률로 거대 버섯", U.Pct(NF.ed_giant(L)));
            E["ed_multi"] = L => L > 0 ? Loc.F("수확할 때 {0} 확률로 버섯 ×3", U.Pct(MultiRate(L))) : Loc.T("없음");
            // ── 약용 (바·스킬·필드) ──
            E["md_map"] = L => Loc.F("필드 {0}×{1} · 화면 확대 ×{2}", U.JsRound(W / MAP_ZOOM[L]), U.JsRound(H / MAP_ZOOM[L]), F(MAP_ZOOM[L]));
            E["md_bar"] = L => $"{U.JsRound(150 * NF.md_bar(L))}px";
            E["md_combo"] = L => Loc.F("콤보당 +{0}배", F(NF.md_combo(L), 3));
            // 숲 장치: 바 넓이 바로 다음에 골드로 해금. 해금하면 라운드마다 반드시 1개씩 놓인다
            E["dev_stump"] = L => L > 0 ? Loc.F("라운드마다 1개 · 부딪히면 {0}초 공격력 ×{1}", STUMP_ATK_DUR, STUMP_ATK_MUL) : Loc.T("해금하면 라운드마다 놓여요");
            E["dev_moss"] = L => L > 0 ? Loc.T("라운드마다 1개 · 지나가면 2초 가속") : Loc.T("해금하면 라운드마다 놓여요");
            E["dev_acorn"] = L => L > 0 ? Loc.T("라운드마다 1개 · 셋 다 켜면 5초 점수 ×2 (한 번)") : Loc.T("해금하면 라운드마다 놓여요");
            E["md_kid"] = L => L > 0 ? Loc.F("영구 수확기가 바에 맞을 때 {0} 확률로 작은 임시 수확기 1개 (8초)", U.Pct(KidRate(L))) : Loc.T("없음");
            E["md_combo2"] = L => Loc.F("콤보당 +{0}배 더", F(NF.md_combo2(L), 2));
            E["md_templife"] = L => Loc.F("임시 수확기 지속시간 {0}", Plus(NF.md_templife(L)));
            E["md_chef"] = L => Loc.F("꼬마 공방 제작 시간 ×{0}", F(NF.md_chef(L)));
            E["md_stump"] = L => Loc.F("숲 장치 효과 {0}", Plus(NF.md_stump(L)));
            E["md_buffdur"] = L => Loc.F("가속·맑은 포자막 지속 {0}, 날 갈기 {1}타", Plus(NF.md_buffdur(L)), Math.Ceiling(3 * NF.md_buffdur(L)));
            // ── 독 (전투) ──
            // 초반
            E["ps_atk"] = L => Loc.F("공격력 +{0}", Num(NF.ps_atk(L)));
            E["ps_size"] = L => Loc.F("반지름 {0}", F(TUNE.ball0 * BALL_SCALE * NF.ps_size(L), 1));
            E["ps_spd"] = L => Loc.F("이동 속도 {0}", Plus(NF.ps_spd(L)));
            E["ps_dur"] = L => Loc.F("라운드 시간 +{0}초", NF.ps_dur(L));
            E["ps_ball"] = L => Loc.F("영구 수확기 +{0}개", L);
            // 중반
            E["ps_atk2"] = L => Loc.F("공격력 +{0}", Num(NF.ps_atk2(L)));
            E["ps_ball2"] = L => Loc.F("영구 수확기 +{0}개", L);
            E["ps_atk2p"] = L => Loc.F("공격력 +{0}", Num(NF.ps_atk2p(L)));
            E["ps_dur2"] = L => Loc.F("라운드 시간 +{0}초 더", NF.ps_dur2(L));
            E["ps_crit"] = L => L > 0 ? Loc.F("{0} (피해·점수 ×3)", U.Pct(NF.ps_crit(L))) : "0%";
            E["ps_resist"] = L => Loc.F("느림·약화 디버프 지속 -{0}%", U.JsRound((1 - NF.ps_resist(L)) * 100));
            E["ps_rush"] = L => Loc.F("라운드 시작 후 3초 동안 공격력 ×{0}", F(NF.ps_rush(L)));
            // 회전 칼날도 스킬처럼 노드 하나 (4레벨): 2 범위·속도, 3 위력, 4 모두 한 단계 더 (BladeLv)
            E["bl"] = L =>
            {
                if (L == 0) return Loc.T("해금하면 칼날 사용 시작");
                var (rg, sp, pw) = BladeLv(L);
                return Loc.F("반지름 +{0}px · {1}초마다 · 피해 공격력×{2}", U.JsRound(NF.bl_range(rg)), F(NF.bl_spd(sp)), F(NF.bl_pow(pw)));
            };
            // 슬라이드 바 수치 강화 (보라 가지). 가격은 가안
            E["ps_bar"] = L => Loc.F("바 길이 +{0}px (필드 폭 1/3까지)", U.JsRound(150 * NF.ps_bar(L)));
            E["ps_barspd"] = L => Loc.F("바에 튕길 때 속도 {0}", Plus(NF.ps_barspd(L)));
            E["ps_bardur"] = L => Loc.F("바 스킬 지속시간 +{0}초", F(NF.ps_bardur(L), 1));
            E["ps_barrate"] = L => Loc.F("바 스킬 발동률 +{0}%p", U.JsRound(NF.ps_barrate(L) * 100));
            E["ps_power"] = L => Loc.F("기운 북돋기 공격력 +{0}%p · 발동률 +{1}%p", U.JsRound(NF.ps_power(L) * 100), U.JsRound(NF.ps_power(L) * 100 * POWER_SKILL / POWER_ATK));
            // 후반
            E["ps_atk3"] = L => Loc.F("공격력 +{0}", Num(NF.ps_atk3(L)));
            E["ps_atk3p"] = L => Loc.F("공격력 +{0}", Num(NF.ps_atk3p(L)));
            E["ps_atk4"] = L => Loc.F("공격력 +{0}", Num(NF.ps_atk4(L)));
            E["ps_ball3"] = L => Loc.F("영구 수확기 +{0}개", L);
            E["ps_crit2"] = L => Loc.F("치명타 피해·점수 ×{0}", NF.ps_crit2(L));
            E["ps_heavy"] = L => Loc.F("군락 무더기·거대 버섯 피해 ×{0}", F(NF.ps_heavy(L)));
            // 균사석 노드 (후반): 여기서부터 곱셈 효과가 들어온다
            E["gm_harvest"] = L => Loc.F("모든 버섯 획득량 ×{0}", F(NF.gm_harvest(L)));
            E["gm_spore"] = L => Loc.F("나무버섯을 딸 때 {0} 확률로 균사석 +2", U.Pct(NF.gm_spore(L)));
            E["gm_gourmet"] = L => Loc.F("나무버섯 균사석 ×{0}", F(NF.gm_gourmet(L)));
            E["gm_herb"] = L => Loc.F("수확 보따리 ×{0}", F(NF.gm_herb(L)));
            E["gm_farm"] = L => Loc.F("나무버섯 성장 시간 ×{0}", F(NF.gm_farm(L)));
            E["gm_meteor"] = L =>
            {
                if (L == 0) return Loc.T("없음");
                var m = NF.gm_meteor(L);
                return Loc.F("{0}초마다 균사석이 떨어져 반경 {1} 피해 공격력×{2}", F(m.interval), m.r, m.dmg);
            };
            E["gm_toxin"] = L => Loc.F("포자 디버프 지속 ×{0} · 독버섯 포자 구름 ×{1}", F(NF.gm_toxin(L)), F(NF.gm_toxin(L)));
            // 균사석 노드 추가 (가안): 공격력·정산 골드·보스·스킬·바·보스 균사석
            E["gm_atk"] = L => Loc.F("공격력 ×{0}", F(NF.gm_atk(L)));
            E["inf_atk"] = L => Loc.F("공격력 ×{0}", F(NF.inf_atk(L)));      // 끝없는 투지 (싱크)
            E["inf_gold"] = L => Loc.F("정산 골드 ×{0}", F(NF.inf_gold(L)));  // 끝없는 풍년 (싱크, 점수→골드에만)
            E["gm_boss"] = L => Loc.F("보스에게 주는 피해 ×{0}", F(NF.gm_boss(L)));
            E["gm_score"] = L => Loc.F("정산 골드 ×{0}", F(NF.gm_score(L)));
            E["gm_bossgem"] = L => Loc.F("보스를 쓰러뜨릴 때 균사석 +{0}", NF.gm_bossgem(L));
            E["gm_skill"] = L => Loc.F("모든 스킬 발동률 ×{0}", F(NF.gm_skill(L)));
            E["gm_bar"] = L => Loc.F("바 길이 ×{0} (필드 폭 1/3까지)", F(NF.gm_bar(L)));
            return E;
        }

        // 수확기: 이름·유형·해금·레벨 가격·기본 능력치는 DB Ball·Ball_Efx 시트, 고유 능력(ab·문구)은 코드
        static void InitHarvesters()
        {
            var db = Db.Data;
            var abil = HarvesterAbilities();
            var efx = db.Ball_Efx.ToDictionary(e => e.Ball_Efx_ID);
            foreach (var r in db.Ball)
            {
                if (!abil.TryGetValue(r.Ball_Key, out var a)) throw new InvalidOperationException($"[DB] Ball 시트의 {r.Ball_Key}: 코드에 고유 능력이 없는 수확기예요 (Defs.Nodes.cs HarvesterAbilities)");
                if (!efx.TryGetValue(r.Ball_Efx_ID, out var e)) throw new InvalidOperationException($"[DB] Ball {r.Ball_Key}: Ball_Efx에 {r.Ball_Efx_ID}가 없어요");
                // ab는 별 등급만 보는 순수 함수라 등급별 결과를 기억한다 (타격·프레임마다 Dictionary를 새로 만들지 않게). 결과는 읽기 전용으로 쓸 것
                var memo = new Dictionary<string, double>[8];
                var ab = a.ab;
                Func<int, Dictionary<string, double>> cached = s => (uint)s < (uint)memo.Length ? memo[s] ??= ab(s) : ab(s);
                var h = new Harvester
                {
                    id = r.Ball_Key, nKo = r.Ball_Name, typeKo = r.Ball_Type, lookKo = r.Ball_Look, unlockGold = r.Ball_Unlock, lvGold = r.Ball_LvGold,
                    atkMul = e.Atk, spdMul = e.Spd, sizeMul = e.Size, crit = e.Cri, ab = cached, text = a.text,
                };
                HARVESTERS.Add(h); HV[h.id] = h;
            }
        }

        static Dictionary<string, (Func<int, Dictionary<string, double>> ab, Func<int, string> text)> HarvesterAbilities() => new Dictionary<string, (Func<int, Dictionary<string, double>>, Func<int, string>)>
        {
            ["sam"] = (s => D(("p", 0.15 + 0.07 * (s - 1))),
                s => Loc.F("휘두르기: 버섯을 수확할 때 {0} 확률로 가까운 버섯 하나를 함께 벤다 (공격력×2)", U.Pct(HV["sam"].ab(s)["p"]))),
            ["saw"] = (s => D(("k", 0.25 + 0.1 * (s - 1)), ("max", 3)),
                s => Loc.F("튕겨 베기: 벽·바에 튕길 때마다 다음 타격 피해 +{0} (최대 {1}번 쌓임, 맞히면 사라짐)", U.Pct(HV["saw"].ab(s)["k"]), HV["saw"].ab(s)["max"])),
            ["hoe"] = (s => D(("p", 0.2 + 0.1 * (s - 1))),
                s => Loc.F("급소 찌르기: 에픽 이상 버섯을 맞히면 {0} 확률로 확정 치명타 (피해·점수 ×3)", U.Pct(HV["hoe"].ab(s)["p"]))),
            ["mill"] = (s => D(("r", 34 + 8 * s), ("p", 0.1 + 0.04 * (s - 1))),
                s => Loc.F("갈아내기: 늘 작은 회전 칼날(범위 {0}px)을 두르고, 칼날이 벨 때 {1} 확률로 버섯 조각(추가 수확) 획득", HV["mill"].ab(s)["r"], U.Pct(HV["mill"].ab(s)["p"]))),
            ["spore"] = (s => D(("p", 0.08 + 0.04 * (s - 1)), ("hits", 2)),
                s => Loc.F("세포 분열: 버섯을 맞히면 {0} 확률로 능력치 50%인 작은 수확기를 소환 ({1}번 부딪히면 사라짐)", U.Pct(HV["spore"].ab(s)["p"]), HV["spore"].ab(s)["hits"])),
            ["bell"] = (s => D(("r", 40 + 12 * s), ("dmg", 0.4 + 0.15 * s)),
                s => Loc.F("딸랑 울림: 버섯을 맞힐 때마다 작은 충격파 (반경 {0}, 피해 공격력×{1})", HV["bell"].ab(s)["r"], U.FmtN(HV["bell"].ab(s)["dmg"]))),
            ["coin"] = (s => D(("k", 0.3 + 0.15 * (s - 1))),
                s => Loc.F("짤랑짤랑: 버섯에 닿을 때마다 골드 획득 (그 버섯 값어치의 {0})", U.Pct(HV["coin"].ab(s)["k"]))),
            ["gold"] = (s => D(("p", 0.03 + 0.02 * (s - 1))),
                s => Loc.F("황금 손길: 맞힌 버섯이 {0} 확률로 황금 변종으로 변한다", U.Pct(HV["gold"].ab(s)["p"]))),
        };
    }
}
