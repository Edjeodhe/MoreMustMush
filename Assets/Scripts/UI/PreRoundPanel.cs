using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // "수확 준비" modal (prototype renderPreRound): region picks, active harvesters, prepared dishes.
    public class PreRoundPanel : MonoBehaviour
    {
        public TMP_Text stage, harvesters, dishes, dishesHead;
        public Image[] themeCards = new Image[5];
        public Image[] themeIcons = new Image[5];
        public TMP_Text[] themeNames = new TMP_Text[5], themeDescs = new TMP_Text[5], themeCounts = new TMP_Text[5];


        public void Render()
        {
            stage.text = $"스테이지 {StageNow()}";
            for (int i = 0; i < THEMES.Count; i++)
            {
                var th = THEMES[i]; var card = themeCards[i];
                bool ok = ThemeOpen(th), on = G.theme == th.id;
                var ex = SPECIES.Where(sp => sp.theme == th.id).ToList();
                card.color = on ? U.Hex("#eef7ff") : U.Hex("#fffaf0");
                card.GetComponent<Outline>().effectColor = on ? U.Hex("#5fb8ff") : U.Hex("#cdb68e");
                card.GetComponent<CanvasGroup>().alpha = ok ? 1 : 0.45f;
                card.GetComponent<Button>().interactable = ok;
                themeIcons[i].sprite = SpriteDB.Icon(th.icon);
                themeNames[i].text = th.n;
                themeDescs[i].text = ok ? th.d : $"스테이지 {th.from}부터 열려요";
                themeCounts[i].text = ok ? $"전용 버섯 {ex.Count(sp => Harvested(sp.id))}/{ex.Count}" : UIUtil.Ic("lock");
            }
            harvesters.text = string.Join("   ", ActiveHvs().Select(id => $"{HV[id].n} <color=#e8a900>{U.Stars(G.hv.TryGetValue(id, out var s) ? s : 1)}</color>"));
            var ds = G.dishes.Where(id => RECIPE.ContainsKey(id)).ToList();
            UIUtil.Show(dishes, ds.Count > 0); UIUtil.Show(dishesHead, ds.Count > 0);
            dishes.text = string.Join("   ", ds.Select(id => RECIPE[id].n + (G.dishLeft.TryGetValue(id, out var l) && l > 1 ? $" <size=70%>({l}라운드)</size>" : "")));
        }
    }
}
