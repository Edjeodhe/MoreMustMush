using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Critter card modal (prototype openPetCard): picture, perk, hearts, request perks, evolve and skin buttons.
    public class PetCardPanel : MonoBehaviour
    {
        public string id;
        public CritterIcon pic;
        public Image ring;                       // evolution glow behind the picture
        public TMP_Text evoStars, title, perk, note, list;
        public TMP_Text[] hearts = new TMP_Text[5];
        public Image[] heartFills = new Image[5];  // progress toward the next heart, under that heart
        public Button evolve; public TMP_Text evolveText; public GameObject evoMax;
        public Button skin; public UIAction skinAction; public Image skinImage; public TMP_Text skinText;

        public void Render()
        {
            var k = SPC[id];
            int e = EvoOf(id), h = HeartsOf(id), per = HeartPer(id);
            bool full = h >= FARM.hearts;
            var sk = System.Array.Find(CHAR_SKINS, s => s.of == id);
            bool worn = CharSkinOn(id) == sk;

            pic.Set(id, CharSkinOn(id));
            ring.color = e == 2 ? new Color(1, 0.82f, 0.23f, 0.85f) : e == 1 ? new Color(0.56f, 0.94f, 0.78f, 0.85f) : new Color(1, 1, 1, 0);
            evoStars.text = new string('★', e) + new string('☆', FARM.evoMax - e);
            title.text = EvoName(k);
            perk.text = k.perk;
            for (int i = 0; i < FARM.hearts; i++)
            {
                hearts[i].text = i < h ? "♥" : "♡";
                hearts[i].color = i < h ? U.Hex("#ff5a8a") : U.Hex("#e8b8c8");
                bool part = i == h && !full;
                UIUtil.Show(heartFills[i], part);
                if (part) heartFills[i].fillAmount = HeartFrac(id);
            }
            note.text = full ? (e < FARM.evoMax ? "가득 찼어요! 진화할 수 있어요" : "가득") : $"다음 칸까지 부탁 {HeartLeft(id)}번 (한 칸 = 부탁 {per}번)";
            list.text =
                $"· 부탁 보상: 균사석 ×{U.FmtN(FARM.evoGem[e])} {(e < FARM.evoMax ? $"(진화하면 ×{U.FmtN(FARM.evoGem[e + 1])})" : "")}\n" +
                $"· 호감도 보너스: 부탁 때 <b>{U.JsRound(FARM.bonusP * h * 100)}%</b> 확률로 균사석 +1~{2 + e} (채운 칸마다 +{U.JsRound(FARM.bonusP * 100)}%)\n" +
                $"· 부탁 간격 ×{U.FmtN(FARM.evoWait[e])} · 밭일·요리 속도 ×{U.FmtN(FARM.evoSpd[e])}";

            bool max = e >= FARM.evoMax;
            UIUtil.Show(evolve, !max); UIUtil.Show(evoMax, max);
            if (!max) { evolve.interactable = full; evolveText.text = $"진화하기 → {EvoName(k, e + 1)}"; }

            if (SkinOwn(sk.id))
            {
                skinAction.act = "wearskin"; skinAction.arg = sk.id + "|card";
                skinText.text = worn ? $"{sk.n} 착용 중 (벗기)" : $"{sk.n} 입기";
                skinImage.color = worn ? U.Hex("#b8f0d8") : new Color(1, 1, 1, 0.75f);
            }
            else
            {
                skinAction.act = "skinshop"; skinAction.arg = null;
                skinText.text = $"스킨 상점: {sk.n} ({UIUtil.Ic("gem")}{sk.price})";
                skinImage.color = new Color(1, 1, 1, 0.75f);
            }
        }
    }
}
