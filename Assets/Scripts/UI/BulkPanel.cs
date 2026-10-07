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
        public bool keep = true;
        public TMP_Text keepLabel, plan;
        public Toggle keepToggle;
        public Transform tabs;          // branch tab buttons (UIAction arg = branch)
        public Button goButton;

        void Awake()
        {
            keepToggle.onValueChanged.AddListener(v => { keep = v; Render(); });
        }

        string[] Branches => br == "all" ? CAT_KEYS : new[] { br };

        public void SetBranch(string b) { br = b; Render(); }
        public void ToggleKeep() { keep = !keep; Render(); }

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
            keepToggle.SetIsOnWithoutNotify(keep);
            keepLabel.text = $"다음 세금 예상액({U.Fmt(TaxBill())}골드)만큼 남기기";
            var p = BulkBuy(Branches, keep ? TaxBill() : 0, true);
            plan.text = $"보유 {U.Fmt(G.gold)}골드 → 강화 <color=#2a6ab8>{p.levels}</color>단계 · 비용 <color=#2a6ab8>{U.Fmt(p.spent)}</color>골드 · 남는 골드 {U.Fmt(G.gold - p.spent)}";
            goButton.interactable = p.levels > 0;
        }

        public void Go()
        {
            var before = G.nodes.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToHashSet();
            var res = BulkBuy(Branches, keep ? TaxBill() : 0, false);
            if (res.levels == 0) { Snd.Err(); return; }
            GameFlow.I.tree.world.MarkGrown(G.nodes.Where(kv => kv.Value > 0 && !before.Contains(kv.Key)).Select(kv => kv.Key));
            Snd.Buy(); GameFlow.I.CloseModal(); GameFlow.I.tree.Refresh();
            GameFlow.I.ShowToast($"{res.levels}단계 강화 완료! (−{U.Fmt(res.spent)}골드)");
        }
    }
}
