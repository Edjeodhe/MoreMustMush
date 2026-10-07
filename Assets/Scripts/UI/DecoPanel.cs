using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Farm decoration shop modal (prototype openDecoShop): eight decorations bought with gems, placed or put away.
    public class DecoPanel : MonoBehaviour
    {
        public TMP_Text gemText;
        public DecoCard[] cards = new DecoCard[8];   // DECOR order

        public void Render()
        {
            gemText.text = $"{UIUtil.Ic("gem")}{U.Fmt(G.gem)}";
            for (int i = 0; i < cards.Length; i++) cards[i].Set(DECOR[i]);
        }
    }
}
