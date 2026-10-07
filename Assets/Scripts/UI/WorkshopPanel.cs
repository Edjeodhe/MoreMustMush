using System.Linq;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Workshop modal (prototype openWorkshop): harvester unlock · level · on/off, plus the weather table.
    // One HvCard per harvester and one WeatherRow per weather are placed in the hierarchy.
    public class WorkshopPanel : MonoBehaviour
    {
        public TMP_Text sub, goldText;
        public HvCard[] cards = new HvCard[7];
        public WeatherRow[] weathers = new WeatherRow[11];

        public void Render()
        {
            sub.text = "수확기마다 <color=#3a8a2a>고유 능력</color>이 달라요(우열 없음). <color=#3a8a2a>활성화</color>한 수확기들 중에서 핀볼마다 무작위로 나오고, 분열·분신으로 생긴 핀볼도 무작위예요.\n" +
                $"지금 활성: {string.Join(" · ", ActiveHvs().Select(id => HV[id].n))}. 강화하면 별(최대 5성)이 오르며 능력이 세지고, 5성이면 날이 늘어요.";
            goldText.text = $"{UIUtil.Ic("gold")}{U.Fmt(G.gold)}";
            foreach (var c in cards) c.Render();
            foreach (var w in weathers) w.Render();
        }
    }
}
