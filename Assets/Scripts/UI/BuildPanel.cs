using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Build shop modal (건축): eight buildings bought with 균사석 (gem). Each has a fixed footprint and a max count;
    // pressing "건설" closes the modal and starts placement (FarmView ghost).
    public class BuildPanel : MonoBehaviour
    {
        [FormerlySerializedAs("diaText")] public TMP_Text gemText;
        public TMP_Text workers;
        public BuildCard[] cards = new BuildCard[8];   // BUILDINGS order

        public void Render()
        {
            gemText.text = $"{UIUtil.Ic("gem")}{U.Fmt(G.gem)}";
            int own = FarmOwned().Count, free = FreeCritters().Count;
            workers.text = Loc.F("일할 수 있는 꼬마 {0}/{1}", free, own);
            for (int i = 0; i < cards.Length; i++)
            {
                bool on = i < BUILDINGS.Length; UIUtil.Show(cards[i], on);
                if (on) cards[i].Set(BUILDINGS[i]);
            }
        }
    }
}
