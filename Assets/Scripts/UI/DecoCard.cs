using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One decoration card (prototype .sk-card in the deco grid).
    public class DecoCard : MonoBehaviour
    {
        public Image frame, pic;
        public TMP_Text title, desc;
        public Button button; public UIAction action; public Image buttonImage; public TMP_Text buttonText;

        public void Set(Deco d)
        {
            bool own = DecoOwn(d.id), on = DecoOn(d.id);
            frame.color = own ? U.Hex("#effaf4") : U.Hex("#fffaf0");
            title.text = d.n; desc.text = d.d;
            action.arg = d.id;
            if (!own)
            {
                action.act = "buydeco"; button.interactable = G.gem >= d.price;
                buttonText.text = $"{UIUtil.Ic("gem")}{d.price} 구입";
                buttonImage.color = Color.white;
            }
            else
            {
                action.act = "placedeco"; button.interactable = true;
                buttonText.text = on ? "놓여 있음 (치우기)" : "목장에 놓기";
                buttonImage.color = on ? U.Hex("#b8f0d8") : new Color(1, 1, 1, 0.75f);
            }
        }
    }
}
