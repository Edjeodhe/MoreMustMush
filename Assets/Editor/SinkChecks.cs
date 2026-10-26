using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 싱크 노드(Efx_MaxLv = 0: 끝없는 투지·풍년·위력·기세·질주·동료)와 9회차 반복 보스·끈기 회귀 검사. 실제 세이브(Game.G·PlayerPrefs)는 건드리지 않는다.
    // 메뉴 MoreMush/Validate Sink Nodes · 배치 -executeMethod MoreMush.EditorTools.SinkChecks.Batch (결과 BalanceData/sink_checks.txt)
    public static class SinkChecks
    {
        [MenuItem("MoreMush/Validate Sink Nodes")]
        public static void Menu() => Debug.Log(Run());

        public static void Batch()
        {
            int code = 0; string text;
            try { text = Run(); }
            catch (Exception e) { text = "실패: " + e; code = 1; }
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData/sink_checks.txt")), text);
            EditorApplication.Exit(code);
        }

        public static string Run()
        {
            var lines = new List<string>();
            void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAIL: " + name); lines.Add("PASS: " + name); }

            // 균사 트리 A안(2026-10-19)에는 싱크 노드가 없다 (무한 확장은 미정). 다시 넣으면 아래 검사가 돈다 (ComputeStats에 효과도 다시 이어야 한다)
            if (!NODE.TryGetValue("inf_atk", out var atk) || !NODE.TryGetValue("inf_gold", out var gold) || !NODE.ContainsKey("inf_sdmg")) return "SKIP: 싱크 노드(inf_atk·inf_gold)가 DB에 없어요 (균사 트리 A안: 무한 확장 미정)";
            Check(atk.inf && gold.inf && atk.max == 0 && gold.max == 0, "싱크 2개가 inf·max 0으로 만들어짐");
            Check(atk.req == "ps_atk4" && gold.req == "ed_score3p", "열림 조건 = ps_atk4·ed_score3p 최대");
            Check(Math.Abs(atk.infCostK - 1.06) < 1e-9 && atk.infMul > 1 && gold.infMul > 1, $"가격 ×1.06 · 효과 레벨당 +{atk.infMul - 1:0.###} / +{gold.infMul - 1:0.###}");
            Check(NodeCost(atk, 0).gold == Math.Ceiling(atk.infCost0 * COST_MUL) && NodeCost(atk, 0).gem == 0, "1레벨 가격 = Cost_Lv1");
            var capCost = NodeCost(atk, atk.infCap).gold;
            Check(atk.infCap > 1000 && !double.IsInfinity(capCost) && !double.IsNaN(capCost), $"상한 {atk.infCap}레벨 가격이 유한함 ({capCost:E2})");
            Check(!double.IsInfinity(Math.Pow(atk.infMul, atk.infCap)), "효과: 상한에서도 유한");
            Check(!NODES.Any(n => n.req != null && NODE[n.req].inf), "싱크를 '최대 레벨로 열기' 선행으로 쓰는 노드 없음");

            // 9회차 싱크 4개: 열림 조건과 효과 상한 (INF_CAP)
            var sdmg = NODE["inf_sdmg"]; var srate = NODE["inf_srate"]; var spd = NODE["inf_spd"]; var ball = NODE["inf_ball"];
            Check(new[] { sdmg, srate, spd, ball }.All(n => n.inf && n.max == 0), "새 싱크 4개가 inf·max 0으로 만들어짐");
            Check(sdmg.req == "fest" && srate.req == "fest" && spd.req == "ps_ball3" && ball.req == "ps_ball3", "열림 조건 = 축제(fest)·영구 수확기 III 최대");
            Check(srate.infCap == 50 && spd.infCap == 50 && ball.infCap == 6 && sdmg.infCap > 1000, $"효과 상한: 기세 {srate.infCap} · 질주 {spd.infCap} · 동료 {ball.infCap} · 위력 없음({sdmg.infCap})");

            // 반복 보스 체력: 60부터 300까지 보스마다 오르기만 한다 (바퀴가 바뀌어도)
            double prevHp = 0; int drop = -1;
            for (int s = BOSS_EVERY * (BOSSES.Count + 1); s <= 300; s += BOSS_EVERY)
            {
                double hp = BossHp(BossAt(s), LatestThemeAt(s).id, s);
                if (hp < prevHp && drop < 0) drop = s;
                prevHp = hp;
            }
            Check(drop < 0, drop < 0 ? "반복 보스 체력 60 → 300 단조 증가" : $"반복 보스 체력이 {drop}에서 떨어짐");
            Check(GritMul(0) == 1 && Math.Abs(GritMul(2) - GRIT_STEP) < 1e-12 && GritMul(9999) == GRIT_CAP, "끈기: 0점 ×1 · 2점 ×1.05 · 상한 ×3");
            Check(!GritOn(BOSS_EVERY * BOSSES.Count) && GritOn(BOSS_EVERY * (BOSSES.Count + 1)) && !GritOn(BOSS_EVERY * (BOSSES.Count + 1) + 1), "끈기는 반복 보스 스테이지(60~)에서만");

            var prevG = G; bool prevS = SaveIO.Suspended; SaveIO.Suspended = true;
            try
            {
                G = new SaveData();
                Check(NodeState(atk) != "avail", "ps_atk4 최대 전에는 끝없는 투지가 열리지 않음");
                foreach (var n in NODES) if (!n.inf && !n.gem) G.nodes[n.id] = n.max;   // 골드 트리 전부 최대
                Check(NodeState(atk) == "avail" && NodeState(gold) == "avail", "트리 최대 뒤 싱크 2개 열림");
                double atk0 = ComputeStats("clear", THEMES[0].id).atk, gm0 = ComputeStats("clear", THEMES[0].id).goldMul;
                G.gold = 1e20;
                int k = BuyMax(atk);
                int L = Lv("inf_atk");
                Check(k == L && k > 50 && k < 10000, $"BuyMax(1e20 골드) → {k}레벨");
                double ratio = ComputeStats("clear", THEMES[0].id).atk / atk0;
                Check(Math.Abs(ratio / (1 + (atk.infMul - 1) * L) - 1) < 1e-9, $"공격력 ×(1 + {atk.infMul - 1:0.###}×{L}) 반영 (덧셈)");
                G.nodes["inf_gold"] = 40;
                double gr = ComputeStats("clear", THEMES[0].id).goldMul / gm0;
                Check(Math.Abs(gr / (1 + (gold.infMul - 1) * 40) - 1) < 1e-9, "점수→골드 ×(1 + 레벨당 증가×40) 반영 (덧셈)");
                Check(NodeState(atk) != "max", "상한 전에는 MAX가 아님");
                int slots0 = HvSlots(); G.nodes["inf_ball"] = 4;
                Check(HvSlots() == slots0 + 4, "동료 4레벨 → 장착 칸 +4");
                G.nodes["inf_ball"] = ball.infCap;
                Check(NodeState(ball) == "max", "동료 6레벨 = MAX");

                // 세이브 정리: 싱크 레벨은 상한까지 남고, 넘으면 상한으로 잘린다
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(new SaveData { nodes = new Dictionary<string, int> { ["inf_atk"] = 500, ["inf_gold"] = 99999 } });
                var g2 = SaveIO.FromJson(json);   // 실제 세이브 파일은 건드리지 않고 읽기·정리 단계만 시험한다
                Check(g2 != null && g2.nodes["inf_atk"] == 500 && g2.nodes["inf_gold"] == gold.infCap, "세이브 정리: 500 유지 · 99999 → 상한");
            }
            finally { G = prevG; SaveIO.Suspended = prevS; }
            return string.Join("\n", lines);
        }
    }
}
