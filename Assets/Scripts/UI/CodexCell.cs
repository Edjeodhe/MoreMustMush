using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One codex grid cell (prototype .cc). `id` is a species id (ed0…) or "sp:fire" for special critters.
    public class CodexCell : MonoBehaviour
    {
        public string id;
        public Image frame, icon;
        public TMP_Text label, stars, themeMark, goldStar;
        public GameObject giantMark;    // brown "巨" tag
        Outline outline, selOutline;

        void Awake()
        {
            var outs = GetComponents<Outline>();
            outline = outs.Length > 0 ? outs[0] : null;
            selOutline = outs.Length > 1 ? outs[1] : null;
            var ua = GetComponent<UIAction>();
            if (ua != null) { ua.act = "codexsel"; ua.arg = id; }
        }

        public void Select(string selected)
        {
            if (selOutline != null) selOutline.enabled = selected == id;
        }

        public void Render(string selected)
        {
            Select(selected);
            if (id.StartsWith("sp:"))
            {
                var k = SPC[id.Substring(3)];
                bool own = HasSpecial(k.id);
                UIUtil.SetImage(icon, SpriteDB.Get($"Characters/Critters/{k.id}_ref"), own ? 0 : 1);
                label.text = own ? k.n : "???";
                frame.color = own ? Color.white : U.Hex("#d8cdb8");
                if (outline != null) outline.effectColor = U.Hex("#d9c6a2");
                foreach (var t in new[] { stars, themeMark, goldStar }) UIUtil.Show(t, false);
                UIUtil.Show(giantMark, false);
                return;
            }
            var sp = SP[id];
            bool h = Harvested(sp.id), un = IsUnlockedSp(sp);
            G.codex.TryGetValue(sp.id, out var e);
            UIUtil.SetImage(icon, SpriteDB.Single(sp.id), h ? 0 : un ? 2 : 1);
            label.text = h || un ? sp.n : sp.boss > 0 ? UIUtil.Ic(BOSS_ICON) : sp.theme != null ? UIUtil.Ic(THEME[sp.theme].icon) : "???";
            frame.color = h ? Color.white : un ? U.Hex("#fbf6ea") : U.Hex("#d8cdb8");
            if (outline != null) outline.effectColor = h && sp.t > 0 ? U.Hex(new[] { "", "#7fc4ff", "#c77dff", "#ffb627" }[sp.t]) : U.Hex("#d9c6a2");
            UIUtil.Show(stars, h);
            if (h) { int s = StarOf(sp.id); stars.text = s > 0 ? $"<color=#e8a900>{new string('★', s)}</color><color=#d8ccb4>{new string('★', 5 - s)}</color>" : ""; }
            UIUtil.Show(themeMark, sp.theme != null || sp.boss > 0);
            if (sp.theme != null) themeMark.text = UIUtil.Ic(THEME[sp.theme].icon);
            else if (sp.boss > 0) themeMark.text = UIUtil.Ic(BOSS_ICON);
            UIUtil.Show(goldStar, e != null && e.gold);
            UIUtil.Show(giantMark, e != null && e.giant);
        }
    }
}
