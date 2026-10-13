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
            // 지운 날씨의 줄은 숨긴다 (씬에 남아 있어도 깨지지 않게)
            if (!WEATHER.TryGetValue(id ?? "", out var w)) { gameObject.SetActive(false); return; }
            icon.sprite = SpriteDB.Icon(w.icon);
            title.text = $"<color={(w.good ? "#2a7ad8" : "#d23a2a")}>{w.n}</color>";
            desc.text = $"{(w.good ? Loc.T("좋은 날씨") : Loc.T("나쁜 날씨"))} · {w.d}";
            fav.text = Loc.F("잘 나옴: {0}", FavText(w));
            chance.text = $"{System.Math.Round(WeatherChance(w) * 100)}% · {(w.good ? Loc.T("일반 채집") : Loc.T("보스 채집"))}";
            group.alpha = 1;
        }
    }
}
