using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Farm HUD (prototype #farmhud: farmTopHTML + petsHUDHTML / fieldHUDHTML / kitchenHUDHTML).
    // Everything is placed in the hierarchy; this script fills texts and toggles what each scene shows.
    public class FarmScreen : MonoBehaviour
    {
        [ScenePath("World/Farm")] public FarmView farm;

        [Header("Top")]
        public TMP_Text title;
        public Image[] tabs = new Image[3];                 // field, pets, kitchen
        public TMP_Text[] tabTexts = new TMP_Text[3];
        public GameObject[] badges = new GameObject[3];
        public TMP_Text[] badgeTexts = new TMP_Text[3];
        public TMP_Text goldText, gemText, sporeText;

        [Header("Navigation")]
        public UIAction navL, navR;
        public TMP_Text navLText, navRText;
        public CanvasGroup[] fades;                         // hidden while the camera pans
        public GameObject centerMsg; public TMP_Text centerTitle, centerSub;

        [Header("Ranch")]
        public GameObject chips;
        public FarmChip[] chipList = new FarmChip[10];
        public Button careAll; public TMP_Text careAllText;

        [Header("Field")]
        public GameObject fieldPanel;
        public TMP_Text fieldHead;
        public CropCard[] crops = new CropCard[3];          // ed, md, ps
        public TMP_Text stock;
        public Button[] fieldButtons = new Button[3];       // till, plant, harvest
        public TMP_Text[] fieldButtonTexts = new TMP_Text[3];
        public Image toolIcon; public TMP_Text toolTitle, toolSub;
        public Button toolUp; public Image toolUpIcon; public TMP_Text toolUpText; public GameObject toolMax;
        public TMP_Text sizeText, expandSub;
        public Button expand; public TMP_Text expandText; public GameObject expandMax;

        [Header("Kitchen")]
        public GameObject kitchenPanel;
        public TMP_Text kitchenHead;
        public Button cookAll;
        public RecipeRow[] recipes = new RecipeRow[10];     // RECIPES order
        public TMP_Text readyText;

        float tickT; int lastN = -1; string lastSig;

        static readonly string[] VIEWS = { "field", "pets", "kitchen" };

        void OnEnable() { tickT = 0; }

        void Update()
        {
            if (G?.farm == null) return;
            float a = farm.Moving ? 0 : 1;
            foreach (var g in fades) if (g != null) { g.alpha = Mathf.MoveTowards(g.alpha, a, Time.deltaTime / 0.3f); g.blocksRaycasts = !farm.Moving; }
            if ((tickT -= Time.deltaTime) > 0) return;
            tickT = 0.5f;
            if (GameFlow.I.ModalOpen || farm.Moving) return;
            if (farm.View == "pets" && farm.petList.Count != FarmOwned().Count) { FarmEnsure(); farm.Sync(false); farm.dirty = true; }
            if (farm.dirty || FarmReadyCount() != lastN || (farm.View == "field" && FieldSig() != lastSig)) Render();
            else foreach (var c in chipList) c.RefreshTime();
        }

        string FieldSig() => string.Concat(FieldKeys().Select(t => { var p = PlotAt(t.key); return farm.busy.ContainsKey(t.key) ? '9' : p == null ? '0' : p.s == "till" ? '1' : TileRipe(p) ? '3' : '2'; }));

        public void Render()
        {
            if (G?.farm == null) return;
            string v = farm.View;
            int pn = FarmPetCount(), fn = FieldRipeCount(), kn = farm.cooking.Count;
            title.text = v == "field" ? $"{UIUtil.Ic("crop_ed")} 버섯 밭 <size=60%><color=#f0d8a8>{G.farm.size}×{G.farm.size}</color></size>"
                : v == "kitchen" ? $"{UIUtil.Ic("dish_soup")} 꼬마 식당 <size=60%><color=#f0d8a8>요리사 {farm.cooks.Count}마리</color></size>"
                : $"{UIUtil.Ic("ed")} 버섯 농장 <size=60%><color=#f0d8a8>{FarmOwned().Count}/{SPECIALS.Length} 입주</color></size>";
            int[] ns = { fn, pn, kn };
            for (int i = 0; i < 3; i++)
            {
                bool on = VIEWS[i] == v;
                tabs[i].color = on ? U.Hex("#8fd062") : Color.white;
                tabTexts[i].color = on ? Color.white : U.Hex("#3b2414");
                UIUtil.Show(badges[i], ns[i] > 0);
                badgeTexts[i].text = ns[i].ToString();
            }
            goldText.text = $"{UIUtil.Ic("gold")}{U.Fmt(G.gold)}";
            gemText.text = $"{UIUtil.Ic("gem")}{U.Fmt(G.gem)}";
            sporeText.text = $"{UIUtil.Ic("spore")}{U.Fmt(G.spore)}";

            // 장면 이동 버튼
            UIUtil.Show(navL, v != "field");
            UIUtil.Show(navR, v != "kitchen");
            navL.arg = v == "kitchen" ? "pets" : "field";
            navLText.text = v == "kitchen" ? $"◀ {UIUtil.Ic("ed")} 꼬마 농장으로 가기" : $"◀ {UIUtil.Ic("crop_ed")} 농사하러 가기";
            navR.arg = v == "field" ? "pets" : "kitchen";
            navRText.text = v == "field" ? $"{UIUtil.Ic("ed")} 꼬마 농장으로 가기 ▶" : $"{UIUtil.Ic("dish_soup")} 요리하러 가기 ▶";

            bool none = v == "field" ? farm.workers.Count == 0 : v == "kitchen" ? farm.cooks.Count == 0 : farm.petList.Count == 0;
            UIUtil.Show(centerMsg, none);
            if (none)
            {
                ((RectTransform)centerMsg.transform).anchoredPosition = new Vector2(v == "pets" ? 960 : 700, -546);
                centerTitle.text = v == "field" ? "밭일을 도와줄 꼬마가 없어요" : v == "kitchen" ? "요리할 꼬마가 없어요" : "아직 농장에 아무도 없어요";
                centerSub.text = v == "field" ? "수확 중에 나타나는 특수 버섯(꼬마)을 잡으면 함께 농사를 지어요!"
                    : v == "kitchen" ? "수확 중에 나타나는 특수 버섯(꼬마)을 잡으면 요리사가 돼요!"
                    : "수확 중에 나타나는 특수 버섯(꼬마)을 잡으면 이곳에 들어와요!";
            }

            UIUtil.Show(chips, v == "pets");
            UIUtil.Show(careAll, v == "pets");
            UIUtil.Show(fieldPanel, v == "field");
            UIUtil.Show(kitchenPanel, v == "kitchen");
            if (v == "pets") RenderPets(pn);
            else if (v == "field") RenderField();
            else RenderKitchen();

            lastN = FarmReadyCount(); lastSig = FieldSig(); farm.dirty = false;
        }

        void RenderPets(int n)
        {
            for (int i = 0; i < chipList.Length; i++) chipList[i].Set(SPECIALS[i]);
            careAll.interactable = n > 0;
            careAllText.text = $"모두 돌보기{(n > 0 ? $" ({n})" : "")}";
        }

        void RenderField()
        {
            var f = G.farm;
            var keys = FieldKeys().Where(t => !farm.busy.ContainsKey(t.key)).ToList();
            int raw = keys.Count(t => PlotAt(t.key) == null), till = keys.Count(t => PlotAt(t.key)?.s == "till"), ripe = keys.Count(t => TileRipe(PlotAt(t.key)));
            int working = farm.Working;
            fieldHead.text = $"심을 작물 <size=55%><color=#8a6a4a>칸을 누르면 꼬마가 가서 갈기 → 심기 → 수확{(working > 0 ? $" · <b>작업 {working}칸 진행 중</b>" : "")}</color></size>";
            string[] cs = { "ed", "md", "ps" };
            for (int i = 0; i < 3; i++) crops[i].Set(cs[i]);
            stock.text = $"창고  {UIUtil.Ic("ed")}{U.Fmt(InvTotal("ed"))}   {UIUtil.Ic("md")}{U.Fmt(InvTotal("md"))}   {UIUtil.Ic("ps")}{U.Fmt(InvTotal("ps"))}";
            int[] cnt = { raw, till, ripe };
            string[] names = { "모두 갈기", "모두 심기", "모두 수확" };
            for (int i = 0; i < 3; i++) { fieldButtons[i].interactable = cnt[i] > 0; fieldButtonTexts[i].text = $"{names[i]} ({cnt[i]})"; }

            var T = TOOLS[f.tool];
            toolIcon.sprite = SpriteDB.Get("Farm/Field/tool_" + f.tool);
            toolTitle.text = $"<color={T.col}>[{T.grade}]</color> {T.n}";
            toolSub.text = $"수확 때 포자 {T.dropMin}~{T.dropMax}개 (보너스 {T.bonus})";
            bool hasNext = f.tool + 1 < TOOLS.Length;
            UIUtil.Show(toolUp, hasNext); UIUtil.Show(toolMax, !hasNext);
            if (hasNext)
            {
                var nx = TOOLS[f.tool + 1];
                toolUp.interactable = G.gold >= nx.cost;
                toolUpIcon.sprite = SpriteDB.Get("Farm/Field/tool_" + (f.tool + 1));
                toolUpText.text = $"{nx.grade} {nx.n}\n<size=72%>{U.Fmt(nx.cost)}G · 포자 {nx.bonus}</size>";
            }
            sizeText.text = $"{f.size}×{f.size}";
            expandSub.text = $"최대 {FIELD.max}×{FIELD.max} · 지금 {f.size * f.size}칸";
            double ec = ExpandCost();
            UIUtil.Show(expand, ec > 0); UIUtil.Show(expandMax, ec <= 0);
            if (ec > 0) { expand.interactable = G.gold >= ec; expandText.text = $"{f.size + 1}×{f.size + 1}로\n<size=72%>{U.Fmt(ec)}G</size>"; }
        }

        void RenderKitchen()
        {
            double k = NF.md_chef(Lv("md_chef"));
            kitchenHead.text = $"오늘의 메뉴 <size=55%><color=#8a6a4a>꼬마 요리사가 만들어요 · 다음 라운드에 먹어요 (효과 ×{U.FmtN(k)} '버섯 요리사')</color></size>";
            cookAll.interactable = RECIPES.Any(farm.CanCook);
            for (int i = 0; i < recipes.Length; i++) recipes[i].Set(RECIPES[i], farm);
            var ready = G.dishes.Where(id => RECIPE.ContainsKey(id)).Select(id => UIUtil.Ic(RECIPE[id].icon)).ToList();
            readyText.text = $"상에 차린 요리: {(ready.Count > 0 ? string.Join(" ", ready) : "<size=78%><color=#9a8a70>아직 없어요</color></size>")}";
        }
    }
}
