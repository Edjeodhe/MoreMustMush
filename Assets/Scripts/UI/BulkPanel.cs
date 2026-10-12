using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // "일괄 강화" modal (prototype openBulk / doBulk): buy the cheapest upgrades in the chosen branches.
    public class BulkPanel : MonoBehaviour
    {
        public string br = "all";
        public TMP_Text plan;
        public Transform tabs;          // branch tab buttons (UIAction arg = branch)
        public Button goButton;

        string[] Branches => br == "all" ? CAT_KEYS : new[] { br };

        public void SetBranch(string b) { br = b; Render(); }

        public void Render()
        {
            foreach (Transform tab in tabs)
            {
                var ua = tab.GetComponent<UIAction>();
                bool on = ua != null && ua.arg == br;
                string col = ua != null && ua.arg != "all" ? CATS[ua.arg].color : "#7a4f2e";
                tab.GetComponent<Image>().color = on ? U.Hex(col) : Color.white;
                tab.GetComponentInChildren<TMP_Text>().color = on ? Color.white : U.Hex("#3b2414");
            }
            var p = BulkBuy(Branches, 0, true);
            plan.text = Loc.F("보유 {0}골드 → 강화 <color=#2a6ab8>{1}</color>단계 · 비용 <color=#2a6ab8>{2}</color>골드 · 남는 골드 {3}", U.Fmt(G.gold), p.levels, U.Fmt(p.spent), U.Fmt(G.gold - p.spent));
            goButton.interactable = p.levels > 0;
        }

        public void Go()
        {
            var before = G.nodes.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToHashSet();
            var res = BulkBuy(Branches, 0, false);
            if (res.levels == 0) { Snd.Err(); return; }
            GameFlow.I.tree.world.MarkGrown(G.nodes.Where(kv => kv.Value > 0 && !before.Contains(kv.Key)).Select(kv => kv.Key));
            Snd.Buy(); GameFlow.I.CloseModal(); GameFlow.I.tree.Refresh();
            GameFlow.I.ShowToast(Loc.F("{0}단계 강화 완료! (−{1}골드)", res.levels, U.Fmt(res.spent)));
        }
    }
}
