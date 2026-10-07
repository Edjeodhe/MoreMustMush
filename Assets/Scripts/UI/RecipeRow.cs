using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One recipe in the kitchen menu (prototype .kr-row): dish, effect, ingredients, cook button.
    public class RecipeRow : MonoBehaviour
    {
        public Image frame, icon;
        public Outline border;
        public TMP_Text title, effect, need;
        public Button button; public Image buttonImage; public TMP_Text buttonText;

        public void Set(Recipe rc, FarmView farm)
        {
            var n = RecipeNeed(rc);
            bool done = DishOn(rc.id), busy = farm.cooking.Contains(rc.id), ok = farm.CanCook(rc);
            int rounds = DishRounds(rc);
            frame.color = done ? U.Hex("#eef7e4") : busy ? U.Hex("#fff6dc") : U.Hex("#fffaf0");
            border.effectColor = done ? U.Hex("#8ac06a") : busy ? U.Hex("#e8a900") : U.Hex("#e2cfa8");
            icon.sprite = SpriteDB.Get("Farm/Icons/" + rc.icon);
            title.text = rc.n;
            effect.text = rc.eff(ChefMul(rc)) + (rounds > 1 ? $" · {rounds}라운드" : "");
            need.text = string.Join("  ", n.Select(kv => (NeedHave(kv.Key) >= kv.Value ? "" : "<color=#d23a2a>") + UIUtil.Ic(kv.Key) + U.Fmt(kv.Value) + (NeedHave(kv.Key) >= kv.Value ? "" : "</color>")));
            button.interactable = ok;
            buttonImage.color = done || busy ? new Color(1, 1, 1, 0.75f) : Color.white;
            G.dishLeft.TryGetValue(rc.id, out var left);
            buttonText.text = done ? $"준비됨{(left > 1 ? $"\n<size=75%>{left}라운드</size>" : "")}" : busy ? "조리 중…" : "요리하기";
        }
    }
}
