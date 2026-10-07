using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One skin card (prototype .sk-card): before ▶ after pictures, name, owner, buy / wear button.
    public class SkinCard : MonoBehaviour
    {
        public Image frame;
        public CritterIcon chBase, chSkin;     // critter skins
        public Image hvBase, hvSkin, hvAura;   // harvester skins
        public TMP_Text title, who, buttonText;
        public Button button; public UIAction action;   // buyskin / wearskin, arg = skin id
        public Image buttonImage;

        public void Set(Skin sk)
        {
            bool own = SkinOwn(sk.id), ch = sk.ch;
            bool has = ch ? HasSpecial(sk.of) : HvStar(sk.of) > 0;
            bool worn = (ch ? CharSkinOn(sk.of) : HvSkinOn(sk.of)) == sk;
            frame.color = own ? U.Hex("#effaf4") : U.Hex("#fffaf0");
            UIUtil.Show(chBase, ch); UIUtil.Show(chSkin, ch);
            UIUtil.Show(hvBase, !ch); UIUtil.Show(hvSkin, !ch); UIUtil.Show(hvAura, !ch);
            if (ch) { chBase.Set(sk.of, null, !has); chSkin.Set(sk.of, sk); }
            else
            {
                hvBase.sprite = SpriteDB.Get("Harvesters/" + sk.of);
                hvSkin.sprite = SpriteDB.Get("Harvesters/" + sk.id);
                var a = U.Hex(sk.aura); a.a = 0.45f; hvAura.color = a;
            }
            title.text = sk.n;
            who.text = ch ? (has ? SPC[sk.of].n : "아직 못 잡은 꼬마") : (has ? HV[sk.of].n : "아직 없는 수확기");

            if (!own)
            {
                action.act = "buyskin"; button.interactable = G.gem >= sk.price;
                buttonText.text = $"{UIUtil.Ic("gem")}{sk.price} 구입";
                buttonImage.color = Color.white;
            }
            else if (!has)
            {
                action.act = "none"; button.interactable = false;
                buttonText.text = ch ? "꼬마를 잡으면 입혀요" : "수확기를 얻으면 써요";
                buttonImage.color = Color.white;
            }
            else
            {
                action.act = "wearskin"; button.interactable = true;
                buttonText.text = worn ? "착용 중 (벗기)" : "착용하기";
                buttonImage.color = worn ? U.Hex("#9fd2ff") : new Color(1, 1, 1, 0.75f);
            }
            action.arg = sk.id;
        }
    }
}
