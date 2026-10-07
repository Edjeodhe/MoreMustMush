using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // A critter chip on the ranch bottom bar (prototype .fh-chip): picture, name, hearts, time to the next request,
    // "진화!" badge or evolution stars. Opens the critter card.
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
            bool ready = FarmReady(k.id), evo = CanEvolve(k.id);
            frame.color = evo ? U.Hex("#fff6c8") : ready ? U.Hex("#fff4c4") : U.Hex("#f6ead2");
            title.text = EvoName(k);
            UIUtil.Show(evoBadge, evo);
            UIUtil.Show(evoStars, !evo && EvoOf(k.id) > 0);
            evoStars.text = new string('★', EvoOf(k.id));
            RefreshTime();
        }

        public void RefreshTime()
        {
            if (id == null) return;
            bool ready = FarmReady(id);
            sub.text = $"<color=#d2386a>{HeartStr(id)}</color> {(ready ? "<color=#c0392b><b>" : "")}{FarmLeftText(id)}{(ready ? "</b></color>" : "")}";
            if (ready && !CanEvolve(id)) frame.color = Color.Lerp(U.Hex("#fff4c4"), U.Hex("#ffe08a"), 0.5f + 0.5f * Mathf.Sin(Time.time * 5));
        }
    }
}
