using System;
using System.Collections.Generic;

namespace MoreMush
{
    // 세이브 버전 올리기. SaveIO.Load가 Sanitize 끝에서 부르며, 한 버전씩 차례로 올린다.
    public static class SaveMigration
    {
        public const int CURRENT = 7;

        // 이번 로드에서 플레이어에게 알릴 문장 (App.FlushMigrationNotice가 알리고 비운다)
        public static string Notice;

        // v3에서 지운 노드(시간 증가·숲 장치 생성 스킬)의 레벨별 가격. 지운 가격표를 여기에 남겨 환불에 쓴다
        static readonly Dictionary<string, double[]> RETIRED_V3 = new Dictionary<string, double[]>
        {
            ["tspore"] = new double[] { 9800 },
            ["tspore_rate"] = new double[] { 35000, 130000 },
            ["tspore_pow"] = new double[] { 35000, 130000 },
            ["sip"] = new double[] { 3100000 },
            ["sip_rate"] = new double[] { 29000000, 99000000 },
            ["sip_pow"] = new double[] { 35000000, 120000000 },
            ["device"] = new double[] { 4400000 },
            ["device_rate"] = new double[] { 44000000, 140000000 },
        };

        public static void Run(SaveData g)
        {
            if (g.v < 3) ToV3(g);
            if (g.v < 4) ToV4(g);
            if (g.v < 5) ToV5(g);
            if (g.v < 6) ToV6(g);
            if (g.v < 7) ToV7(g);
            g.v = Math.Max(g.v, CURRENT);
        }

        static void ToV3(SaveData g)
        {
            double refund = 0;
            foreach (var kv in RETIRED_V3)
            {
                if (!g.nodes.TryGetValue(kv.Key, out int L)) continue;
                for (int i = 0; i < Math.Min(L, kv.Value.Length); i++) refund += Math.Ceiling(kv.Value[i] * Defs.COST_MUL);
                g.nodes.Remove(kv.Key);
            }
            if (refund > 0)
            {
                g.gold += refund;
                AddNotice(Loc.F("없어진 스킬에 쓴 골드 {0}을 돌려받았어요", U.Fmt(refund)));
            }
            FitHarvestersToSlots(g);
            // 다이아몬드는 없어졌다. 남은 다이아는 균사석으로 1:1 (포자·창고 버섯·마을 의뢰는 세이브에서 빠져 그냥 사라진다)
            if (g.legacyDia >= 1)
            {
                double d = Math.Floor(g.legacyDia);
                g.gem += d;
                AddNotice(Loc.F("다이아몬드 {0}개를 균사석으로 바꿨어요", U.Fmt(d)));
            }
            g.legacyDia = 0;
        }

        // v4: 스테이지가 판 수와 따로 간다(보스를 못 깨면 멈춘다). 지나온 보스는 쓰러뜨린 것으로 친다
        // v4에서 지운 노드: 숲 장치 수(장치는 종류별 해금 노드로), 맑은 포자막 발동률(바에 맞으면 늘 발동)
        static readonly Dictionary<string, double[]> RETIRED_V4 = new Dictionary<string, double[]>
        {
            ["ed_dev"] = new double[] { 150000, 410000 },
            ["clear_rate"] = new double[] { 13000000, 47000000 },
        };

        static void ToV4(SaveData g)
        {
            g.stage = g.rounds + 1;
            g.bossOn = true;
            double refund = Refund(g, RETIRED_V4);
            if (refund > 0) AddNotice(Loc.F("바뀐 강화 노드에 쓴 골드 {0}을 돌려받았어요", U.Fmt(refund)));
            // 예전엔 숲 장치가 늘 나왔다. 이미 진행한 세이브는 장치 셋을 해금해 준다
            if (g.rounds > 0) foreach (var id in new[] { "dev_stump", "dev_moss", "dev_acorn" }) g.nodes[id] = 1;
        }

        // v5: 트리 개편. 가지 코어(식용·약용·독 균사)가 없어지고 기운 북돋기가 시작 노드가 됐다.
        // 스킬의 발동률·위력 하위 노드(2레벨씩)는 스킬 노드 4레벨로 합친다: 새 레벨 = 1 + 하위 노드 레벨 합(최대 4). 회전 칼날도 같은 식
        // 진행한 세이브는 시작 노드만 1레벨로 준다. 부모가 바뀐 노드는 새 부모를 사야 더 올릴 수 있다 (조상을 공짜로 채우지 않는다)
        static void ToV5(SaveData g)
        {
            int Get(string id) => g.nodes.TryGetValue(id, out var L) ? L : 0;
            foreach (var s in Defs.SKILL_IDS)
            {
                int sub = Get(s + "_rate") + Get(s + "_pow");
                g.nodes.Remove(s + "_rate"); g.nodes.Remove(s + "_pow");
                if (Get(s) > 0) g.nodes[s] = Math.Min(Defs.SKILL_MAX, 1 + sub);
            }
            int bl = Get("bl_range") + Get("bl_spd") + Get("bl_pow");
            foreach (var k in new[] { "bl_range", "bl_spd", "bl_pow", "core_ed", "core_md", "core_ps" }) g.nodes.Remove(k);
            if (Get("bl") > 0) g.nodes["bl"] = Math.Min(Defs.SKILL_MAX, 1 + (bl + 1) / 2);
            bool any = false; foreach (var kv in g.nodes) if (kv.Value > 0) { any = true; break; }
            if (any && Get(Defs.ROOT_NODE) <= 0) g.nodes[Defs.ROOT_NODE] = 1;
            if (g.nodes.Count > 0) AddNotice(Loc.T("균사 트리가 바뀌었어요. 스킬 강화는 스킬 노드의 레벨로 합쳤어요"));
        }

        // v6: 꼬마버섯 세계수 (농장 개편). 나무 Lv → 성장 단계(최대 8, 나이테 Lv.1), 경험치는 그대로.
        // 건물·꼬마 별 등급·부탁·호감도 하트·진화는 없어졌다 → 건물에 쓴 균사석과 꼬마 별에 쓴 골드·균사석을 돌려준다
        static readonly Dictionary<string, int> RETIRED_BUILDING_GEM = new Dictionary<string, int>
        {
            ["dc_table"] = 20, ["dc_lamp"] = 15, ["dc_bed"] = 15, ["dc_fire"] = 25, ["dc_swing"] = 30, ["dc_well"] = 40, ["dc_house"] = 50, ["dc_statue"] = 80,
        };
        static readonly double[] RETIRED_CSTAR_GOLD = { 10000, 100000, 1000000, 10000000, 100000000 };
        static readonly int[] RETIRED_CSTAR_GEM = { 5, 10, 20, 40, 80 };

        // v7: 튜토리얼·단계별 기능 해금. 이미 하던 세이브는 튜토리얼을 건너뛰고 기능을 모두 연다
        static void ToV7(SaveData g)
        {
            if (g.rounds <= 0) return;
            g.tut = 3;
            Unlock.OpenAll(g);
        }

        static void ToV6(SaveData g)
        {
            var f = g.farm;
            if (f.legacyTree != null)
            {
                f.stage = Math.Max(1, Math.Min(Defs.WT.stages, f.legacyTree.lv));
                f.ring = 1;
                if (f.legacyTree.exp > 0 && !double.IsInfinity(f.legacyTree.exp)) f.exp = f.legacyTree.exp;
            }
            double gem = 0, gold = 0;
            if (f.legacyBlds != null)
                foreach (var b in f.legacyBlds) if (b?.id != null && RETIRED_BUILDING_GEM.TryGetValue(b.id, out var p)) gem += p;
            if (f.legacyStar != null)
                foreach (var kv in f.legacyStar)
                    for (int i = 0; i < Math.Min(kv.Value, RETIRED_CSTAR_GOLD.Length); i++) { gold += RETIRED_CSTAR_GOLD[i]; gem += RETIRED_CSTAR_GEM[i]; }
            g.gem += gem; g.gold += gold;
            // 이미 있던 꼬마는 선호 구역(열렸고 자리가 있으면)으로 보낸다
            foreach (var k in Defs.KIDS)
            {
                if (!g.specials.TryGetValue(k.id, out var has) || !has || f.zone.ContainsKey(k.id) || f.stage < Defs.ZONE[k.zone].open) continue;
                int inZone = 0; foreach (var z in f.zone.Values) if (z == k.zone) inZone++;
                if (inZone < Defs.HARV.ZoneCap(f.stage)) f.zone[k.id] = k.zone;
            }
            if (g.legacyAutoT > 0 && g.legacyAutoT < Game.Now()) f.bag.t = g.legacyAutoT;  // 예전 자동 수확 시계부터 보따리가 쌓인 것으로 (꼬마는 FarmEnsure가 합류·배치)
            f.legacyTree = null; f.legacyBlds = null; f.legacyStar = null; g.legacyAutoT = 0;
            if (g.rounds > 0 || gem > 0 || gold > 0)
                AddNotice(Loc.T("농장이 꼬마버섯 세계수로 바뀌었어요! 꼬마를 구역에 보내 나무버섯을 모아 보세요")
                    + (gem > 0 || gold > 0 ? "\n" + Loc.F("건물·꼬마 별에 쓴 균사석 {0}·골드 {1}을 돌려받았어요", U.Fmt(gem), U.Fmt(gold)) : ""));
        }

        static double Refund(SaveData g, Dictionary<string, double[]> retired)
        {
            double refund = 0;
            foreach (var kv in retired)
            {
                if (!g.nodes.TryGetValue(kv.Key, out int L)) continue;
                for (int i = 0; i < Math.Min(L, kv.Value.Length); i++) refund += Math.Ceiling(kv.Value[i] * Defs.COST_MUL);
                g.nodes.Remove(kv.Key);
            }
            g.gold += refund;
            return refund;
        }

        // 예전엔 수확기를 모두 켤 수 있었다. 장착 칸(영구 핀볼 수)을 넘으면 별이 높은 것부터 칸만큼만 남기고 끈다
        static void FitHarvestersToSlots(SaveData g)
        {
            int Lv(string id) => g.nodes.TryGetValue(id, out var L) ? L : 0;
            int slots = 1 + Lv("ps_ball") + Lv("ps_ball2") + Lv("ps_ball3");
            var on = new List<string>();
            foreach (var h in Defs.HARVESTERS)
                if (g.hv.ContainsKey(h.id) && (!g.hvOn.TryGetValue(h.id, out var o) || o)) on.Add(h.id);
            if (on.Count <= slots) return;
            on.Sort((a, b) => g.hv[b].CompareTo(g.hv[a]));   // List.Sort는 불안정하지만 같은 별끼리 순서는 상관없다
            for (int i = slots; i < on.Count; i++) g.hvOn[on[i]] = false;
            AddNotice(Loc.F("수확기 장착 칸이 생겼어요. 별이 높은 수확기 {0}개만 켜 두었어요 (공방에서 바꿀 수 있어요)", slots));
        }

        static void AddNotice(string s) => Notice = string.IsNullOrEmpty(Notice) ? s : Notice + "\n" + s;
    }
}
