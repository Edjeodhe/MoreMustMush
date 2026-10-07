using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One harvester card in the workshop (prototype .hvcard). `id` is set in the inspector.
    public class HvCard : MonoBehaviour
    {
        public string id;
        public Image frame, icon;
        public Outline border;
        public TMP_Text title, stars, type, trait, next, req;
        public Button unlock, toggle, level;          // UIAction hvunlock / hvtoggle / hvlevel, arg = id
        public TMP_Text unlockText, toggleText, levelText;
        public Image toggleImage;

        public void Render()
        {
            var h = HV[id];
            int S = HvStar(id); bool on = HvOn(id);
            frame.color = S > 0 ? U.Hex("#fffaf0") : U.Hex("#efe6d4");
            border.effectColor = on ? U.Hex("#5fb8ff") : U.Hex("#cdb68e");
            icon.sprite = SpriteDB.Get("Harvesters/" + (Game.HvSkinOn(id) is Skin sk ? sk.id : id)) ?? SpriteDB.Get("Harvesters/" + id);
            icon.color = S > 0 ? Color.white : new Color(1, 1, 1, 0.55f);
            title.text = h.n;
            stars.text = S > 0 ? $"<color=#e8a900>{U.Stars(S)}</color>" : "<color=#8a6a4a>미보유</color>";
            type.text = $"{h.type} · {h.look}";
            trait.text = h.text(System.Math.Max(1, S));
            UIUtil.Show(next, S > 0 && S < 5);
            if (S > 0 && S < 5) { string t = h.text(S + 1); int c = t.IndexOf(": "); next.text = $"다음 ★{S + 1}: {(c >= 0 ? t.Substring(c + 2) : t)}"; }

            UIUtil.Show(unlock, S == 0 && h.unlockGold >= 0);
            UIUtil.Show(req, S == 0 && h.unlockGold < 0);
            req.text = $"황금 도감 15종 달성 보상 ({GoldenCount()}/15)";
            if (S == 0 && h.unlockGold >= 0) { unlock.interactable = G.gold >= h.unlockGold; unlockText.text = $"해금 {UIUtil.Ic("gold")}{U.Fmt(h.unlockGold)}"; }

            UIUtil.Show(toggle, S > 0);
            UIUtil.Show(level, S > 0);
            if (S > 0)
            {
                toggleText.text = on ? "켜짐" : "꺼짐";
                toggleImage.color = on ? U.Hex("#9fd2ff") : new Color(1, 1, 1, 0.75f);
                var lc = HvLevelCost(h);
                level.interactable = lc != null && CanAfford(lc.Value);
                levelText.text = lc != null ? $"★{S + 1} {UIUtil.Ic("gold")}{U.Fmt(lc.Value.gold)}" : "★ 최대";
            }
        }
    }
}
