using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Critter card modal: picture, owned effect, star grade (gold + 균사석 upgrade, out-game stats), hearts,
    // request perks, evolve and skin buttons.
    public class PetCardPanel : MonoBehaviour
    {
        public string id;
        public CritterIcon pic;
        public Image ring;                         // evolution glow behind the picture
        public TMP_Text evoStars, title, perk, note, list;
        public TMP_Text[] hearts = new TMP_Text[5];
        public Image[] heartFills = new Image[5];  // progress toward the next heart, under that heart
        [Header("Star grade")]
        public TMP_Text gradeStars, gradeInfo;
        public Button starUp; public TMP_Text starUpText; public GameObject starMax;
        [Header("Buttons")]
        public Button evolve; public TMP_Text evolveText; public GameObject evoMax;
        public Button skin; public UIAction skinAction; public Image skinImage; public TMP_Text skinText;

        public void Render()
        {
            var k = SPC[id];
            int e = EvoOf(id), h = HeartsOf(id), per = HeartPer(id), st = CStarOf(id);
            bool full = h >= FARM.hearts;
            var sk = System.Array.Find(CHAR_SKINS, s => s.of == id);
            bool worn = CharSkinOn(id) == sk;

            pic.Set(id, CharSkinOn(id));
            ring.color = e == 2 ? new Color(1, 0.82f, 0.23f, 0.85f) : e == 1 ? new Color(0.56f, 0.94f, 0.78f, 0.85f) : new Color(1, 1, 1, 0);
            evoStars.text = e == 0 ? Loc.T("진화 전") : Loc.F("진화 {0}단계", e);
            title.text = EvoName(k);
            perk.text = Loc.F("보유 효과: {0}", FxText(id)) + (st < CSTAR.max ? " <color=#8a6a4a>" + Loc.F("(다음 별: {0})", U.Pct(FxOf(id, st + 1))) + "</color>" : "");

            // 별 등급
            gradeStars.text = $"<color=#e8a900>{new string('★', st)}</color><color=#d9c6a2>{new string('★', CSTAR.max - st)}</color>";
            gradeInfo.text = Loc.F("건설 속도 +{0} · 버섯 따기 속도 +{1} <color=#8a6a4a>(별마다 +{2})</color>", U.Pct(CSTAR.build * st), U.Pct(CSTAR.work * st), U.Pct(CSTAR.build));
            var cost = StarCost(id);
            UIUtil.Show(starUp, cost != null); UIUtil.Show(starMax, cost == null);
            if (cost != null)
            {
                starUp.interactable = G.gold >= cost.Value.gold && G.gem >= cost.Value.gem;
                starUpText.text = Loc.F("★{0} 강화  <size=80%>{1}{2} {3}{4}</size>", st + 1, UIUtil.Ic("gold"), U.Fmt(cost.Value.gold), UIUtil.Ic("gem"), cost.Value.gem);
            }

            int heartCount = Mathf.Min(FARM.hearts, hearts.Length, heartFills.Length);
            for (int i = 0; i < hearts.Length; i++) UIUtil.Show(hearts[i], i < heartCount);
            for (int i = heartCount; i < heartFills.Length; i++) UIUtil.Show(heartFills[i], false);
            for (int i = 0; i < heartCount; i++)
            {
                hearts[i].text = i < h ? "♥" : "♡";
                hearts[i].color = i < h ? U.Hex("#ff5a8a") : U.Hex("#e8b8c8");
                bool part = i == h && !full;
                UIUtil.Show(heartFills[i], part);
                if (part) heartFills[i].fillAmount = HeartFrac(id);
            }
            note.text = full ? (e < FARM.evoMax ? Loc.T("가득 찼어요! 진화할 수 있어요") : Loc.T("가득")) : Loc.F("다음 칸까지 부탁 {0}번 (한 칸 = 부탁 {1}번)", HeartLeft(id), per);
            list.text =
                Loc.F("· 부탁 보상: 균사석 ×{0} {1} · 가끔 +1, 호감도 칸을 채우면 +{2}", U.FmtN(FARM.evoGem[e]), e < FARM.evoMax ? Loc.F("(진화하면 ×{0})", U.FmtN(FARM.evoGem[e + 1])) : "", FARM.gemHeart) + "\n" +
                Loc.F("· 호감도 보너스: 부탁 때 <b>{0}%</b> 확률로 균사석 +1~{1}", U.JsRound(FARM.bonusP * h * 100), 2 + e) + "\n" +
                Loc.F("· 부탁 간격 ×{0} · 진화 일 속도 ×{1}", U.FmtN(FARM.evoWait[e]), U.FmtN(FARM.evoSpd[e]));

            bool max = e >= FARM.evoMax;
            UIUtil.Show(evolve, !max); UIUtil.Show(evoMax, max);
            if (!max) { evolve.interactable = full; evolveText.text = Loc.F("진화하기 → {0}", EvoName(k, e + 1)); }

            if (SkinOwn(sk.id))
            {
                skinAction.act = "wearskin"; skinAction.arg = sk.id + "|card";
                skinText.text = worn ? Loc.F("{0} 착용 중 (벗기)", sk.n) : Loc.F("{0} 입기", sk.n);
                skinImage.color = worn ? U.Hex("#b8f0d8") : new Color(1, 1, 1, 0.75f);
            }
            else
            {
                skinAction.act = "skinshop"; skinAction.arg = null;
                skinText.text = Loc.F("스킨 상점: {0} ({1}{2})", sk.n, UIUtil.Ic("gem"), sk.price);
                skinImage.color = new Color(1, 1, 1, 0.75f);
            }
        }
    }
}
