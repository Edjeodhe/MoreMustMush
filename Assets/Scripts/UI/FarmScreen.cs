using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Farm HUD: title and worker count, currencies, critter chips, field panel (opens with "밭 관리"),
    // and the placement bar (confirm / cancel) that follows the building ghost. Everything is placed in the hierarchy.
    public class FarmScreen : MonoBehaviour
    {
        [ScenePath("World/Farm")] public FarmView farm;

        [Header("Top")]
        public TMP_Text title, status;
        public TMP_Text goldText, gemText, diaText, sporeText;
        public TMP_Text fieldToggleText;
        public GameObject centerMsg; public TMP_Text centerTitle, centerSub;

        [Header("Bottom")]
        public GameObject bottom;                           // back button, chips, care-all (hidden while placing)
        public FarmChip[] chipList = new FarmChip[10];
        public Button careAll; public TMP_Text careAllText;

        [Header("Field panel")]
        public GameObject fieldPanel;
        public TMP_Text fieldHead;
        public CropCard[] crops = new CropCard[3];          // ed, md, ps
        public TMP_Text stock;
        public Button[] fieldButtons = new Button[3];       // till, plant, harvest
        public TMP_Text[] fieldButtonTexts = new TMP_Text[3];
        public TMP_Text sizeText, expandSub;
        public Button expand; public TMP_Text expandText; public GameObject expandMax;

        [Header("Placement")]
        public RectTransform placeBar;
        public TMP_Text placeTitle, placeSub;
        public Button placeOk;

        public bool fieldOpen;
        float tickT; int lastN = -1; string lastSig;

        void OnEnable() { tickT = 0; }

        void Update()
        {
            if (G?.farm == null) return;
            if (farm.Placing) { DrawPlaceBar(); return; }
            if ((tickT -= Time.deltaTime) > 0) return;
            tickT = 0.5f;
            if (GameFlow.I.ModalOpen) return;
            if (farm.crits.Count != FarmOwned().Count) { FarmEnsure(); farm.Sync(false); farm.dirty = true; }
            if (farm.dirty || FarmReadyCount() != lastN || FieldSig() != lastSig) Render();
            else { foreach (var c in chipList) c.RefreshTime(); RenderStatus(); }
        }

        string FieldSig() => string.Concat(FieldKeys().Select(t => { var p = PlotAt(t.key); return farm.busy.ContainsKey(t.key) ? '9' : p == null ? '0' : p.s == "till" ? '1' : TileRipe(p) ? '3' : '2'; }));

        void RenderStatus()
        {
            int own = FarmOwned().Count, free = FreeCritters().Count, bld = G.farm.blds.Count(b => !b.done);
            status.text = own == 0 ? "" : $"일할 수 있는 꼬마 <b>{free}/{own}</b>{(bld > 0 ? $" <color=#f0d8a8>· 건설 중 {bld}</color>" : "")}";
        }

        public void Render()
        {
            if (G?.farm == null) return;
            title.text = $"{UIUtil.Ic("ed")} 버섯 농장 <size=60%><color=#f0d8a8>{FarmOwned().Count}/{SPECIALS.Length} 입주</color></size>";
            RenderStatus();
            goldText.text = $"{UIUtil.Ic("gold")}{U.Fmt(G.gold)}";
            gemText.text = $"{UIUtil.Ic("gem")}{U.Fmt(G.gem)}";
            diaText.text = $"{UIUtil.Ic("dia")}{U.Fmt(G.dia)}";
            sporeText.text = $"{UIUtil.Ic("spore")}{U.Fmt(G.spore)}";
            int fn = FieldRipeCount();
            fieldToggleText.text = $"{UIUtil.Ic("crop_ed")} 밭 관리{(fn > 0 ? $" <color=#e8453c>({fn})</color>" : "")}";

            bool none = farm.crits.Count == 0;
            UIUtil.Show(centerMsg, none);
            if (none) { centerTitle.text = "아직 농장에 아무도 없어요"; centerSub.text = "수확 중에 나타나는 특수 버섯(꼬마)을 잡으면 이곳에 들어와 밭일과 건설을 도와요!"; }

            bool placing = farm.Placing;
            UIUtil.Show(bottom, !placing);
            UIUtil.Show(placeBar, placing);
            UIUtil.Show(fieldPanel, fieldOpen && !placing);

            int n = FarmPetCount();
            for (int i = 0; i < chipList.Length; i++) chipList[i].Set(SPECIALS[i]);
            careAll.interactable = n > 0;
            careAllText.text = $"모두 돌보기{(n > 0 ? $" ({n})" : "")}";
            if (fieldOpen) RenderField();
            if (placing) DrawPlaceBar();

            lastN = FarmReadyCount(); lastSig = FieldSig(); farm.dirty = false;
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
            sizeText.text = $"{f.size}×{f.size}";
            expandSub.text = $"최대 {FIELD.max}×{FIELD.max} · 지금 {f.size * f.size}칸";
            double ec = ExpandCost();
            UIUtil.Show(expand, ec > 0); UIUtil.Show(expandMax, ec <= 0);
            if (ec > 0) { expand.interactable = G.gold >= ec; expandText.text = $"{f.size + 1}×{f.size + 1}로\n<size=72%>{U.Fmt(ec)}G</size>"; }
        }

        // 확인·취소 막대: 건물 잔상 바로 위에 붙는다 (화면 밖으로 나가지 않게)
        void DrawPlaceBar()
        {
            var B = BUILDING[farm.PlaceId]; var r = farm.PlaceRect;
            bool ok = farm.PlaceOk;
            placeTitle.text = $"{B.n} <size=70%>({B.w}×{B.h}칸 · {UIUtil.Ic("dia")}{B.price} · {TimeText(BuildSeconds(B, null))})</size>";
            placeSub.text = ok ? "누르거나 끌어서 옮기고, 확인을 누르면 꼬마가 지으러 가요" : "<color=#d23a2a>여기에는 지을 수 없어요 (울타리 밖 · 밭 · 다른 건물)</color>";
            placeOk.interactable = ok;
            var size = placeBar.sizeDelta;
            float x = Mathf.Clamp(r.center.x, size.x / 2 + 16, W - size.x / 2 - 16);
            float y = r.yMin - 130 - size.y / 2;
            if (y < size.y / 2 + 100) y = r.yMax + 40 + size.y / 2;
            placeBar.anchoredPosition = new Vector2(x, -y);
        }
    }
}
