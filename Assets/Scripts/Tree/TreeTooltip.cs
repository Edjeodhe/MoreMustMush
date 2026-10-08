using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Node tooltip (prototype showNodeTip): name, level, description, effect now → next, cost, notes.
    public class TreeTooltip : MonoBehaviour
    {
        public RectTransform rect;
        public TMP_Text title, lv, desc, eff, cost, note;

        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }

        public void Show(Node n, Vector2 at)
        {
            if (n == null || NodeState(n) == "hidden") { Hide(); return; }
            int L = Lv(n.id); string s = NodeState(n);
            title.text = (n.gem ? UIUtil.Ic("gem") + " " : "") + n.n;
            title.color = U.Hex(n.gem ? "#2a9a74" : CATS[n.br].color);
            lv.text = $"LVL {L} / {n.max}";
            desc.text = Iconize(n.d);
            string costTxt = "", noteTxt = "";
            if (s == "locked")
                eff.text = n.needMax ? $"선행: {NODE[n.parent].n} 최대 레벨 ({Lv(n.parent)}/{NODE[n.parent].max})" : $"선행: {NODE[n.parent].n}";
            else
            {
                eff.text = n.eff(L) + (L < n.max ? $"\n<b>→</b> {n.eff(L + 1)}" : "");
                if (L < n.max)
                {
                    var c = NodeCost(n, L);
                    costTxt = $"{UIUtil.Ic("gold")} {(G.gold + 1e-9 < c.gold ? "<color=#d23a2a>" : "")}{U.Fmt(c.gold)}{(G.gold + 1e-9 < c.gold ? "</color>" : "")}"
                        + (c.gem > 0 ? $"   {UIUtil.Ic("gem")} {(G.gem + 1e-9 < c.gem ? "<color=#d23a2a>" : "")}{U.Fmt(c.gem)}{(G.gem + 1e-9 < c.gem ? "</color>" : "")}" : "");
                }
                else costTxt = "<color=#b8860b>최대 레벨</color>";
            }
            if (n.skill != null)
            {
                string k = SKILLS[n.skill].kind;
                noteTxt = k == "bar" ? "바 버프 · 영구 핀볼이 바에 맞을 때 발동" : k == "hit" ? "스킬 · 버섯을 칠 때 발동률로 자동 발동" : k == "harvest" ? "스킬 · 버섯을 딸 때 발동률로 자동 발동" : k == "wall" ? "스킬 · 벽에 튕길 때 발동률로 자동 발동" : k == "pierce" ? "스킬 · 단단한 버섯에 부딪힐 때 발동률로 관통" : "스킬 · 라운드 마지막 5초 자동";
            }
            if (n.id == "md_kid") noteTxt = "임시 핀볼은 바 버프를 받지 않아요";
            if (s != "locked" && L < n.max)
                noteTxt += (noteTxt != "" ? "\n" : "") + (RoundController.IsTouch ? $"한 번 더 누르면 강화{(n.max > 1 ? " · 길게 누르면 재화가 되는 만큼 한 번에" : "")}" : n.max > 1 ? "Shift+클릭 · 우클릭: 골드가 되는 만큼 한 번에 강화" : "");
            cost.text = costTxt; UIUtil.Show(cost, costTxt != "");
            note.text = noteTxt; UIUtil.Show(note, noteTxt != "");
            gameObject.SetActive(true);
            float tx = U.Clamp(at.x + 24 + 330 > W ? at.x - 350 : at.x + 24, 10, W - 340);
            rect.anchoredPosition = new Vector2(tx, -U.Clamp(at.y - 20, 90, H - 260));
        }

        // emoji in node descriptions → inline icons
        public static string Iconize(string s) => s.Replace("💰", UIUtil.Ic("core_ed")).Replace("🎯", UIUtil.Ic("core_md")).Replace("⚔️", UIUtil.Ic("core_ps"));
    }
}
