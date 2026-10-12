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
            effect.text = string.Format(b.statFmt, U.Pct(b.val)) + (n > 0 ? " <color=#3a8a2a>" + Loc.F("(지금 {0})", U.Pct(b.val * G.farm.blds.FindAll(x => x.id == b.id && x.done).Count)) + "</color>" : "");
            info.text = Loc.F("{0}×{1}칸 · {2}/{3}개 · {4}{5}", b.w, b.h, n, b.max, UIUtil.Ic("hammer"), TimeText(BuildSeconds(b, null)));
            action.act = "build"; action.arg = b.id;
            button.interactable = fail == BuildFail.None;
            buttonText.text = fail == BuildFail.Max ? Loc.T("최대 개수") : fail == BuildFail.NoCritter ? Loc.T("쉬는 꼬마 없음") : Loc.F("{0}{1} 건설", UIUtil.Ic("gem"), b.price);
        }
    }
}
