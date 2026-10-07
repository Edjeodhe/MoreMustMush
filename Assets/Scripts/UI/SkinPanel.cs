using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Skin shop modal (prototype openSkinShop): critter skins (10) and harvester skins (7), bought with gems.
    // Ten cards are placed in the hierarchy; the harvester tab uses the first seven.
    public class SkinPanel : MonoBehaviour
    {
        public string tab = "ch";
        public TMP_Text gemText;
        public Image[] tabs = new Image[2];
        public TMP_Text[] tabTexts = new TMP_Text[2];
        public SkinCard[] cards = new SkinCard[10];

        public void Render()
        {
            gemText.text = $"{UIUtil.Ic("gem")}{U.Fmt(G.gem)}";
            for (int i = 0; i < 2; i++)
            {
                bool on = (i == 0 ? "ch" : "hv") == tab;
                tabs[i].color = on ? U.Hex("#2a9a74") : Color.white;
                tabTexts[i].color = on ? Color.white : U.Hex("#3b2414");
            }
            var list = tab == "ch" ? CHAR_SKINS : HV_SKINS;
            for (int i = 0; i < cards.Length; i++)
            {
                bool on = i < list.Length;
                UIUtil.Show(cards[i], on);
                if (on) cards[i].Set(list[i]);
            }
        }
    }
}
