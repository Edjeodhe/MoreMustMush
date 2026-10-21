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

        // 성장 설계 (균사 트리 A안 15차, 2026-10-19)
        // - 트리는 줄(같은 스킬) 단위다. 한 줄의 레벨은 여러 노드(행)에 나뉘어 놓이고, 앞 행을 최대 레벨까지 올려야 다음 행이 열린다(DB Upg_Req).
        //   줄의 값 = 그 줄에서 마지막으로 산 행의 Efx (각 행의 Efx_Lv0~N은 그 행까지 올렸을 때의 줄 전체 값). Game.LineV
        // - 효과는 % 합연산(+10%, +20%…)과 고정값(공격력 +2…)으로 통일. 수확기 상시 능력치와 슬라이드 바 버프는 따로 곱한다.
        // - 노드 자리(Pos_X·Pos_Y)와 레어도(자리로 정한 가격 등급)는 기획서/균사트리_배치안. 가격은 임시(레어도 기반).
        public static double SkillRate(int S) => 0.10 + 0.10 * S;
        // 슬라이드 바 스킬 지속시간: 3 + n초 (n = 위력 레벨). 기운 북돋기: 공격력 +POWER_ATK·스킬 발동률 +POWER_SKILL × (1 + 위력 레벨)
        public static double BarSkillDur(int P) => 3 + P;
        public static readonly double POWER_ATK = Db.C("POWER_ATK"), POWER_SKILL = Db.C("POWER_SKILL");
        public static readonly float BAR_CLONE_DUR = (float)Db.C("BAR_CLONE_DUR");
        public static readonly int BAR_CLONE_MAX = Db.CInt("BAR_CLONE_MAX", 0);
        public static double SkillPow(int P) => 1 + 0.75 * P;   // 스킬 위력 배율 (피해·시간·속도)

        // 노드 효과 값 (DB Efx_Lv0~10). 레벨은 0~최대 레벨로 자른다
        static double V(string id, int L)
        {
            var n = NODE[id];
            return n.efx[L < 0 ? 0 : L > n.max ? n.max : L];
        }
        public const int MAX_LV = 10;   // 한 행의 최대 레벨 (사냥 준비 Lv.10)

        // ===== 균사 노드 효과 (NF) =====
        // 트리 노드는 줄 값(Game.LineV)으로 읽는다. 여기는 세계수 화면이 쓰는 노드(가지 wt, 트리에 안 보임)와 레벨과 상관없는 공식만 남긴다
        public static class NF
        {
            public static double md_chef(int L) => V("md_chef", L);        // 꼬마 공방 제작 시간 배율 (세계수)
            public static double gm_spore(int L) => V("gm_spore", L);
            public static double gm_herb(int L) => V("gm_herb", L);              // 수확 보따리 배율
            public static double gm_gourmet(int L) => V("gm_gourmet", L);           // 나무버섯 균사석 배율
            public static double gm_farm(int L) => V("gm_farm", L);
            // 스킬 범위 공식 (예전 스킬 노드). 지금 트리에서는 쓰지 않는다
            public static double burst_r(int P) => 80 + 25 * P;
            public static double tornado_h(int P) => 50 + 22 * P;
            public static double shock_r(int P) => 100 + 30 * P;
            public struct Meteor { public double interval, dmg, r; }
        }

        // 트리 밖 노드 (세계수 화면용, 가지 wt): 트리에 그리지 않고 일괄 강화에서도 빠진다
        public const string WORLD_BRANCH = "wt";
        public static bool InTree(Node n) => n.br != WORLD_BRANCH;
        // 트리 네모 칸 한 칸의 픽셀 (TreeLayout)
        public const float TREE_CELL = 130;

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
            public double[] efx;     // 레벨별 효과 값 0~10. DB Upgrade.Efx_Lv0~10 (그 행까지 올렸을 때의 줄 전체 값)
            public Func<int, string> eff;
            public string line;      // 줄(같은 스킬) 키. DB Line
            public int gx, gy;       // 트리 네모 칸 좌표. DB Pos_X·Pos_Y
            public int lv0;          // 이 행 앞까지 줄의 레벨 (줄 레벨 = lv0 + 이 행 레벨)
        }

        public static readonly List<Node> NODES = new List<Node>();
        public static readonly Dictionary<string, Node> NODE = new Dictionary<string, Node>();
        public static readonly Dictionary<string, List<Node>> LINES = new Dictionary<string, List<Node>>();   // 줄 키 → 행 (레벨 순)
        public static string LineName(string line) => LINES.TryGetValue(line, out var l) ? Loc.T(l[0].nKo.Split(new[] { " Lv." }, StringSplitOptions.None)[0]) : null;
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
                if (u.Efx_MaxLv < 0 || u.Efx_MaxLv > MAX_LV) throw new InvalidOperationException($"[DB] Upgrade {key}: Efx_MaxLv는 0(싱크)·1~{MAX_LV} ({u.Efx_MaxLv})");
                string line = string.IsNullOrEmpty(u.Line) ? key : u.Line;
                if (!costOf.TryGetValue(u.Cost_ID, out var c)) throw new InvalidOperationException($"[DB] Upgrade {key}: Upgrade_Cost에 Cost_ID {u.Cost_ID}가 없어요");
                Func<int, string> eff;
                if (skill != null)
                {
                    if (!SKILLS.TryGetValue(skill, out var s)) throw new InvalidOperationException($"[DB] Upgrade {key}: Skill 시트에 없는 스킬 {skill}");
                    if (skill != key) throw new InvalidOperationException($"[DB] Upgrade {key}: 스킬 노드는 Upg_Key와 Skill이 같아야 해요 ({skill})");
                    eff = SkillNodeEffect(s);
                }
                else if (nodeEff.TryGetValue(line, out var fmt)) { string k = key; eff = L => fmt(V(k, L)); }
                else throw new InvalidOperationException($"[DB] Upgrade {key}: 코드에 효과가 없는 줄 {line}이에요 (새 효과는 코드가 필요해요, Defs.Nodes.cs NodeEffects)");
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
                    line = line, gx = u.Pos_X, gy = u.Pos_Y,
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
                if (!LINES.TryGetValue(line, out var rows)) LINES[line] = rows = new List<Node>();
                rows.Add(n);
            }
            // 줄의 행 순서: 첫 행(Upg_Req 없음)부터 Upg_Req를 따라간다. 갈래가 지거나 끊기면 바로 알린다
            foreach (var kv in LINES)
            {
                var all = kv.Value;
                var order = new List<Node>();
                var cur = all.Where(n => n.req == null || NODE[n.req].line != kv.Key).ToList();
                if (cur.Count != 1) throw new InvalidOperationException($"[DB] Upgrade 줄 {kv.Key}: 첫 행(같은 줄의 Upg_Req가 없는 행)이 {cur.Count}개예요");
                for (var n = cur[0]; n != null; n = all.FirstOrDefault(x => x.req == n.id))
                {
                    if (order.Contains(n)) throw new InvalidOperationException($"[DB] Upgrade 줄 {kv.Key}: Upg_Req가 빙글빙글 돌아요");
                    n.lv0 = order.Sum(x => x.max); order.Add(n);
                }
                if (order.Count != all.Count) throw new InvalidOperationException($"[DB] Upgrade 줄 {kv.Key}: Upg_Req로 이어지지 않는 행이 있어요 ({all.Count - order.Count}개)");
                all.Clear(); all.AddRange(order);
            }
            CheckTree(nodeEff.Keys);
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
            var lost = codeKeys.Where(k => !LINES.ContainsKey(k)).ToList();
            if (lost.Count > 0) throw new InvalidOperationException("[DB] Upgrade에서 빠진 줄 (코드가 씀): " + string.Join(", ", lost));
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

        // 줄 효과 문구 (줄 값 → 글). 값은 행의 DB Efx(그 행까지 올렸을 때의 줄 값)에서 읽고, 글 모양만 코드에 둔다.
        // 여기 있는 줄은 DB에 꼭 있어야 한다 (CheckTree). 스킬 노드는 SkillNodeEffect
        static Dictionary<string, Func<double, string>> NodeEffects()
        {
            string F(double v, int d = 2) => U.FmtN(v, d);
            string P(double v) => $"+{U.JsRound(v * 100)}%";
            string Pc(double v) => $"{U.JsRound(v * 1000) / 10}%";
            string Sec(double v) => v > 0 ? Loc.F("{0}초", F(v, 1)) : Loc.T("없음");
            var E = new Dictionary<string, Func<double, string>>();
            // ── 시작: 사냥 준비 (슬라이드 바 버프)
            E["power"] = v => v > 0 ? Loc.F("바에 맞으면 {0}초 동안 공격력 +{1}", POWER_DUR, U.Fmt(v)) : Loc.T("없음");
            // ── 식용
            E["ed_hunt"] = v => Loc.F("획득 점수 {0}", P(v));
            E["ed_haggle"] = v => Loc.F("점수 → 골드 전환율 {0}", P(v));
            E["ed_regular"] = v => Loc.F("점수 → 골드 전환율 {0}", P(v));
            E["ed_view"] = v => Loc.F("군락지 수 +{0}", v);
            E["ed_sky"] = v => Loc.F("군락지 수 +{0}", v);
            E["ed_god"] = v => Loc.F("군락지 수 +{0}", v);
            E["ed_epic"] = v => Loc.F("에픽 버섯 스폰율 {0}", P(v));
            E["ed_unique"] = v => Loc.F("유니크 버섯 스폰율 {0}", P(v));
            E["ed_regen"] = v => Loc.F("군락지 리스폰 {0}초", F(v, 1));
            E["ed_more"] = v => Loc.F("군락지 둘레 단일 버섯 {0}개", v);
            E["ed_gold"] = v => Loc.F("황금 변종 스폰율 +{0}", Pc(v));
            E["ed_hand1"] = v => Loc.F("일반 버섯 수확량 {0}", P(v));
            E["ed_hand2"] = v => Loc.F("에픽 버섯 수확량 {0}", P(v));
            E["ed_hand3"] = v => Loc.F("유니크 버섯 수확량 {0}", P(v));
            E["ed_hand4"] = v => Loc.F("보스 레전더리 버섯 수확량 {0}", P(v));
            E["ed_flex"] = v => Loc.F("점수 3배 확률 {0}", Pc(v));
            E["ed_legend"] = v => Loc.F("라운드 마지막 5초 점수·수확량 {0}", P(v));
            // ── 약용
            E["md_spring"] = v => v > 0 ? Loc.F("바에 맞으면 [가속] {0} (속도 +50%)", Sec(v)) : Loc.T("없음");
            E["md_pierce"] = v => v > 0 ? Loc.F("바에 맞으면 [관통] {0}", Sec(v)) : Loc.T("없음");
            E["md_homing"] = v => v > 0 ? Loc.F("바에 맞으면 [유도] {0}", Sec(v)) : Loc.T("없음");
            E["md_heal"] = v => v > 0 ? Loc.F("바에 맞으면 디버프 제거 + [약손] {0}", Sec(v)) : Loc.T("없음");
            E["md_poison"] = v => Loc.F("단일 버섯을 딸 때 {0} 확률로 다음 타격 피해 +200%", Pc(v));
            E["md_burst"] = v => Loc.F("군락에 부딪힐 때 {0} 확률로 필드 전체 피해 (공격력 {1}%)", Pc(v), U.JsRound(BURST_ATK * 100));
            E["md_split"] = v => v > 0 ? Loc.F("바에 맞을 때 {0}% 확률로 [분열] {1} (꼬마 수확기)", U.JsRound(SPLIT_P * 100), Sec(v)) : Loc.T("없음");
            E["md_bolt"] = v => Loc.F("군락을 다 딸 때 {0} 확률로 다른 군락에 번개 (공격력 {1}%)", Pc(v), U.JsRound(BOLT_ATK * 100));
            E["md_tornado"] = v => Loc.F("군락에 부딪힐 때 {0} 확률로 그 군락에 지속 피해 (공격력 {1}%)", Pc(v), U.JsRound(DOT_ATK * 100));
            E["md_env"] = v => v <= 0 ? Loc.T("없음") : v < 2 ? Loc.T("그루터기") : v < 3 ? Loc.T("그루터기 · 꽃밭") : Loc.T("그루터기 · 꽃밭 · 도토리 점등");
            E["md_wisdom"] = v => v > 0 ? Loc.F("바에 맞으면 [지혜] {0}초 · 수확기 스킬 발동률 +{1}", WISDOM_DUR, Pc(v)) : Loc.T("없음");
            E["md_fever"] = v => v > 0 ? Loc.F("바에 맞으면 [피버] {0} (콤보 계수 +50%)", Sec(v)) : Loc.T("없음");
            E["md_molt"] = v => Loc.F("디버프를 얻을 때 {0} 확률로 해제", Pc(v));
            E["md_grand"] = v => Loc.F("라운드 시작 때 가진 바 버프 즉시 발동 확률 {0}", Pc(v));
            // ── 독성
            E["ps_hug"] = v => Loc.F("바 길이 {0} (분신 바도 같은 길이)", P(v));
            E["ps_clone"] = v => v > 0 ? Loc.F("바에 맞을 때 {0}% 확률로 {1} 분신 바", U.JsRound(CLONE_P * 100), Sec(v)) : Loc.T("없음");
            E["ps_mirror"] = v => Loc.F("분신 바 최대 {0}개", v);
            E["ps_blade"] = v => Loc.F("공격력 {0}", U.Fmt(v));
            E["ps_stamina"] = v => Loc.F("라운드 시간 {0}초", U.Fmt(ROUND_SEC + v));
            E["ps_crit"] = v => Loc.F("치명타 확률 {0}", Pc(v));
            E["ps_critdmg"] = v => Loc.F("치명타 피해 {0}%", U.JsRound((1 + v) * 100));
            E["ps_size"] = v => Loc.F("수확기 크기 {0}", P(v));
            E["ps_wind"] = v => Loc.F("수확기 속도 {0}", P(v));
            E["ps_kid"] = v => Loc.F("도는 꼬마 수확기 {0}개", v);
            E["ps_kidatk"] = v => Loc.F("도는 꼬마 수확기 공격력 +{0}%", U.JsRound(v * 100));
            E["ps_combo"] = v => Loc.F("콤보 계수 {0}", P(v));
            E["ps_tools"] = v => Loc.F("수확기 장착 칸 {0}개", 1 + v);
            // ── 세계수 (가지 wt, 트리 밖)
            E["md_chef"] = v => Loc.F("꼬마 공방 제작 시간 ×{0}", F(v));
            E["gm_spore"] = v => Loc.F("나무버섯을 딸 때 {0} 확률로 균사석 +2", U.Pct(v));
            E["gm_gourmet"] = v => Loc.F("나무버섯 균사석 ×{0}", F(v));
            E["gm_herb"] = v => Loc.F("수확 보따리 ×{0}", F(v));
            E["gm_farm"] = v => Loc.F("나무버섯 성장 시간 ×{0}", F(v));
            return E;
        }

        // ===== 트리 효과 상수 (균사 트리 A안). 노드 값이 아닌 고정 규칙 =====
        public const float ROUND_SEC = 10;          // 라운드 기본 시간 (노장의 체력으로 늘어난다)
        public const float POWER_DUR = 3;           // 사냥 준비 버프 시간
        public const float WISDOM_DUR = 3;          // 노인의 지혜 [지혜] 시간 (고정)
        public const float SPRING_MUL = 1.5f;       // [가속] 속도 배율
        public const double FEVER_COMBO = 0.5;      // [피버] 콤보 계수 +50% (상시 콤보 계수 노드와 따로 곱한다)
        public const double SPLIT_P = 0.10, CLONE_P = 0.10;   // 분열·분신술 발동 확률 (바에 맞을 때)
        public const float KID_STAT = 0.10f;        // 꼬마 수확기 = 수확기 공격력의 10%
        public const double BURST_ATK = 0.10, BOLT_ATK = 0.30, DOT_ATK = 0.05;   // 포자 폭발·연속 번개·회오리(틱당) 피해 = 공격력 ×
        public const float DOT_TICK = 0.5f;         // 회오리 지속 피해 간격 (초)
        public const double POISON_MUL = 3;         // 이독제독: 다음 타격 피해 +200%
        public const double CRIT_BASE = 0.5;        // 치명타 피해 기본 150% (치명 박살이 올린다)
        public const int COL_BASE = 3;              // 군락지 기본 수 (넓은 시야·하늘의 시야·신의 눈이 더한다)
        public const int COL_FULL = 15;             // 이 군락지 수에서 필드가 가장 넓다 (화면 확대 ×1)
        public const float ZOOM_MIN_FIELD = 1.5f;   // 군락지 기본 수일 때 화면 확대 (필드가 좁다)
        public const int BOSS_LEGEND_N = 3;         // 보스를 쓰러뜨리면 얻는 레전더리 버섯 수 (보스 전용)
        public static float ZoomForCols(int cols) => Mathf.Lerp(ZOOM_MIN_FIELD, 1, Mathf.Clamp01((cols - COL_BASE) / (float)(COL_FULL - COL_BASE)));

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
