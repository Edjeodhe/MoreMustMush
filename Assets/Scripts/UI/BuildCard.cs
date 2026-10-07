using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One building card in the build shop: picture, effect, footprint · count · build time, build button.
    public class BuildCard : MonoBehaviour
    {
        public Image frame, pic;
        public TMP_Text title, effect, info;
        public Button button; public UIAction action; public TMP_Text buttonText;

        public void Set(Building b)
        {
            int n = BuiltCount(b.id);
            var fail = CanBuild(b.id);
            frame.color = n > 0 ? U.Hex("#effaf4") : U.Hex("#fffaf0");
            pic.sprite = SpriteDB.Get("Farm/Props/" + b.id);
            title.text = b.n;
            effect.text = string.Format(b.statFmt, U.Pct(b.val)) + (n > 0 ? $" <color=#3a8a2a>(지금 {U.Pct(b.val * G.farm.blds.FindAll(x => x.id == b.id && x.done).Count)})</color>" : "");
            info.text = $"{b.w}×{b.h}칸 · {n}/{b.max}개 · {UIUtil.Ic("hammer")}{TimeText(BuildSeconds(b, null))}";
            action.act = "build"; action.arg = b.id;
            button.interactable = fail == BuildFail.None;
            buttonText.text = fail == BuildFail.Max ? "최대 개수" : fail == BuildFail.NoCritter ? "쉬는 꼬마 없음" : $"{UIUtil.Ic("dia")}{b.price} 건설";
        }
    }
}
