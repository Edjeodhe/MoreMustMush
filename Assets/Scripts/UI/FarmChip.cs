using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // A critter chip on the farm bottom bar (prototype .fh-chip): picture, name and star grade, hearts with the time
    // to the next request (or the build timer while it is building), "진화!" badge or evolution stars. Opens the critter card.
    public class FarmChip : MonoBehaviour
    {
        public Image frame;
        public CritterIcon icon;
        public TMP_Text title, sub;
        public GameObject evoBadge; public TMP_Text evoStars;
        public Button button; public UIAction action;
        public CanvasGroup group;
        string id;

        public void Set(Special k)
        {
            id = k.id;
            bool own = HasSpecial(k.id);
            action.arg = k.id;
            button.interactable = own;
            group.alpha = own ? 1 : 0.55f;
            icon.Set(k.id, own ? CharSkinOn(k.id) : null, !own);
            if (!own)
            {
                frame.color = U.Hex("#f6ead2");
                title.text = "???"; sub.text = "";
                UIUtil.Show(evoBadge, false); UIUtil.Show(evoStars, false);
                id = null;
                return;
            }
            bool evo = CanEvolve(k.id);
            int st = CStarOf(k.id);
            title.text = EvoName(k) + (st > 0 ? $" <color=#e8a900><size=80%>★{st}</size></color>" : "");
            UIUtil.Show(evoBadge, evo);
            UIUtil.Show(evoStars, !evo && EvoOf(k.id) > 0);
            evoStars.text = $"진화 {EvoOf(k.id)}";
            RefreshTime();
        }

        public void RefreshTime()
        {
            if (id == null) return;
            bool ready = FarmReady(id), evo = CanEvolve(id);
            var b = G.farm.blds.FirstOrDefault(x => !x.done && x.critter == id);
            string tail = b != null ? $"<color=#a0602a>{UIUtil.Ic("hammer")}{BuildLeftText(b)}</color>"
                : ready ? $"<color=#c0392b><b>{FarmLeftText(id)}</b></color>" : FarmLeftText(id);
            sub.text = $"<color=#d2386a>{HeartStr(id)}</color> {tail}";
            frame.color = evo ? U.Hex("#fff6c8") : ready ? Color.Lerp(U.Hex("#fff4c4"), U.Hex("#ffe08a"), 0.5f + 0.5f * Mathf.Sin(Time.time * 5))
                : b != null ? U.Hex("#efe4d4") : U.Hex("#f6ead2");
        }
    }
}
