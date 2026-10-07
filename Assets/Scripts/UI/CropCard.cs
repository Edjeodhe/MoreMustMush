using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One crop choice in the field panel (prototype .fd-crop): needed spores and mushrooms, growth time, gem reward.
    public class CropCard : MonoBehaviour
    {
        public Image frame;
        public Outline border;
        public TMP_Text title, need, time;
        public GameObject lack;

        public void Set(string c)
        {
            var C = CROPS[c]; var n = CropNeed(c);
            bool on = G.farm.crop == c;
            frame.color = on ? Color.white : U.Hex("#fffaf0");
            border.effectColor = on ? U.Hex(CATS[c].color) : U.Hex("#d9c6a2");
            title.text = $"<color={CATS[c].color}>{C.n}</color>";
            string Lack(bool ok) => ok ? "" : "<color=#d23a2a>";
            string End(bool ok) => ok ? "" : "</color>";
            bool sOk = G.spore >= n["spore"], cOk = InvTotal(c) >= n[c];
            need.text = $"{Lack(sOk)}{UIUtil.Ic("spore")}{n["spore"]}{End(sOk)}  {Lack(cOk)}{UIUtil.Ic(c)}{U.Fmt(n[c])}{End(cOk)}";
            time.text = $"{UIUtil.Ic("clock")} {TimeText(CropTime(c))} → {UIUtil.Ic("gem")}{C.gem}";
            UIUtil.Show(lack, !CanPlant(c));
        }
    }
}
