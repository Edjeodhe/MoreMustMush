using System;
using System.Collections.Generic;

namespace MoreMush
{
    // 세이브 버전 올리기. SaveIO.Load가 Sanitize 끝에서 부르며, 한 버전씩 차례로 올린다.
    public static class SaveMigration
    {
        public const int CURRENT = 3;

        // 이번 로드에서 플레이어에게 알릴 문장 (GameFlow가 토스트로 띄우고 비운다)
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
