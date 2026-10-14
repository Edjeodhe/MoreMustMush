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
            int on = OnHvs().Count, slots = HvSlots();
            string count = on > slots ? $"<color=#d04a3a>{on}/{slots}</color>" : $"{on}/{slots}";
            // 장착 개수를 맨 위에 크게 (켠 수 / 장착 칸)
            sub.text = $"<b><size=120%>{Loc.F("장착 {0}: {1}", count, string.Join(" · ", ActiveHvs().Select(id => HV[id].n)))}</size></b>" +
                (on > slots ? $"  <color=#d04a3a>{Loc.T(GameFlow.HV_OVER_MSG)}</color>" : "") + "\n" +
                Loc.T("수확기마다 <color=#3a8a2a>고유 능력</color>이 달라요. 영구 수확기마다 <color=#3a8a2a>장착 칸</color>이 하나씩 있고, 켠 수확기가 한 칸씩 차지해요(같은 종류는 하나만). 남은 칸은 기본 수확기가 채우고, 분열·분신으로 생긴 수확기는 기본 수확기 분신(공격력 20%)이에요.");
            goldText.text = $"{UIUtil.Ic("gold")}{U.Fmt(G.gold)}";
            foreach (var c in cards) c.Render();
            foreach (var w in weathers) w.Render();
        }
    }
}
