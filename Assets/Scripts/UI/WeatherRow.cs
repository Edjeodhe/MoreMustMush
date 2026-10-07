using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One weather row in the workshop (prototype .wrow). `id` is set in the inspector.
    public class WeatherRow : MonoBehaviour
    {
        public string id;
        public Image icon;
        public TMP_Text title, desc, fav, chance;
        public CanvasGroup group;

        public void Render()
        {
            var w = WEATHER[id];
            double ch = WeatherChance(w);
            icon.sprite = SpriteDB.Icon(w.icon);
            title.text = $"<color={(w.good ? "#2a7ad8" : "#d23a2a")}>{w.n}</color>";
            desc.text = $"{(w.good ? "좋은 날씨" : "나쁜 날씨")} · {w.d}";
            fav.text = $"잘 나옴: {FavText(w)}";
            chance.text = ch > 0 ? $"{System.Math.Round(ch * 100)}%" : $"도감 {w.need}종";
            group.alpha = ch > 0 ? 1 : 0.5f;
        }
    }
}
