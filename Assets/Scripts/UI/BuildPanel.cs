using System.Linq;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Build shop modal (건축): eight buildings bought with diamonds. Each has a fixed footprint and a max count;
    // pressing "건설" closes the modal and starts placement (FarmView ghost).
    public class BuildPanel : MonoBehaviour
    {
        public TMP_Text diaText, workers;
        public BuildCard[] cards = new BuildCard[8];   // BUILDINGS order

        public void Render()
        {
            diaText.text = $"{UIUtil.Ic("dia")}{U.Fmt(G.dia)}";
            int own = FarmOwned().Count, free = FreeCritters().Count;
            workers.text = $"일할 수 있는 꼬마 {free}/{own}";
            for (int i = 0; i < cards.Length; i++) cards[i].Set(BUILDINGS[i]);
        }
    }
}
