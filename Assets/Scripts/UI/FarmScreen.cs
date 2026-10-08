using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Farm HUD: title and worker count, currencies, critter chips, the mushroom tree panel (opens with "버섯 나무"
    // or by clicking the trunk), and the placement hint bar while a building is being placed.
    // Everything is placed in the hierarchy.
    public class FarmScreen : MonoBehaviour
    {
        [ScenePath("World/Farm")] public FarmView farm;

        [Header("Top")]
        public TMP_Text title, status;
        public TMP_Text goldText, gemText, diaText, sporeText;
        public TMP_Text treeToggleText;
        public GameObject centerMsg; public TMP_Text centerTitle, centerSub;

        [Header("Bottom")]
        public GameObject bottom;                           // back button, chips, care-all (hidden while placing)
        public FarmChip[] chipList = new FarmChip[10];
        public Button careAll; public TMP_Text careAllText;

        [Header("Tree panel")]
        public GameObject treePanel;
        public Image treeIcon;
        public TMP_Text treeHead, treeStats, treeNext;
        public Button treeUp; public TMP_Text treeUpText; public GameObject treeMax;
        public Button pickAll; public TMP_Text pickAllText;
        public Button growFast; public TMP_Text growFastText;

        [Header("Placement")]
        public GameObject placeBar;
        public TMP_Text placeTitle, placeSub;

        public bool treeOpen;
        float tickT; int lastN = -1; int lastBusy = -1;

        void OnEnable() { tickT = 0; }

        void Update()
        {
            if (G?.farm == null) return;
            if (farm.Placing) { DrawPlaceBar(); return; }
            if ((tickT -= Time.deltaTime) > 0) return;
            tickT = 0.5f;
            if (GameFlow.I.ModalOpen) return;
            if (farm.crits.Count != FarmOwned().Count) { FarmEnsure(); farm.Sync(false); farm.dirty = true; }
            if (farm.dirty || FarmReadyCount() != lastN || farm.Picking != lastBusy) Render();
            else { foreach (var c in chipList) c.RefreshTime(); RenderStatus(); if (treeOpen) RenderTree(); }
        }

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
            int rn = TreeRipeCount();
            treeToggleText.text = $"{UIUtil.Ic("fruit_ed")} 버섯 나무 Lv.{TreeLv()}{(rn > 0 ? $" <color=#e8453c>({rn})</color>" : "")}";

            bool none = farm.crits.Count == 0;
            UIUtil.Show(centerMsg, none);
            if (none) { centerTitle.text = "아직 농장에 아무도 없어요"; centerSub.text = "수확 중에 나타나는 특수 버섯(꼬마)을 잡으면 이곳에 들어와 버섯 따기와 건설을 도와요!"; }

            bool placing = farm.Placing;
            UIUtil.Show(bottom, !placing);
            UIUtil.Show(placeBar, placing);
            UIUtil.Show(treePanel, treeOpen && !placing);

            int n = FarmPetCount();
            for (int i = 0; i < chipList.Length; i++) chipList[i].Set(SPECIALS[i]);
            careAll.interactable = n > 0;
            careAllText.text = $"모두 돌보기{(n > 0 ? $" ({n})" : "")}";
            if (treeOpen) RenderTree();
            if (placing) DrawPlaceBar();

            lastN = FarmReadyCount(); lastBusy = farm.Picking; farm.dirty = false;
        }

        void RenderTree()
        {
            int lv = TreeLv(), st = TREE.Stage(lv);
            treeIcon.sprite = SpriteDB.Get("Farm/Tree/tree_" + st);
            treeHead.text = $"버섯 나무 <color=#2a9a74>Lv.{lv}</color> <size=60%><color=#8a6a4a>{st + 1}단계 / 최대 Lv.{TREE.maxLv}</color></size>";
            double grow = System.Math.Pow(TREE.speedPerLv, lv - 1);
            treeStats.text = $"버섯 자리 <b>{TREE.Slots(lv)}</b>개 · 성장 시간 ×{U.FmtN(grow)} · 수확량 ×{U.FmtN(TreeYieldMul())}\n"
                + $"<size=85%><color=#6a5040>{string.Join("  ", FRUITS.Select(kv => $"{UIUtil.Ic("fruit_" + kv.Key)}{kv.Value.n} {TimeText(FruitSeconds(kv.Key))} → {UIUtil.Ic("gem")}{kv.Value.gem}"))}</color></size>";
            double cost = TreeUpCost();
            UIUtil.Show(treeUp, cost > 0); UIUtil.Show(treeMax, cost <= 0);
            treeNext.text = cost > 0 ? $"다음 레벨: 버섯 자리 +1 · 성장 시간 ×{U.FmtN(TREE.speedPerLv)} · 수확량 +{U.Pct(TREE.yieldPerLv)}{(TREE.Stage(lv + 1) > st ? " · <color=#2a9a74>나무가 자라요!</color>" : "")}" : "가장 큰 나무예요";
            if (cost > 0) { treeUp.interactable = G.gold >= cost; treeUpText.text = $"레벨 업  <size=80%>{UIUtil.Ic("gold")}{U.Fmt(cost)}</size>"; }
            int ripe = G.farm.tree.slots.Where((f, i) => FruitRipe(f) && !farm.busy.Contains(i)).Count();
            pickAll.interactable = ripe > 0;
            pickAllText.text = $"모두 따기 ({ripe})" + (farm.Picking > 0 ? $" <size=70%>· 따는 중 {farm.Picking}</size>" : "");
            int ac = TreeAccelCost();
            growFast.interactable = ac > 0;
            growFastText.text = ac > 0 ? $"빨리 자라게  <size=80%>{UIUtil.Ic("gem")}{ac}</size>" : "모두 다 자랐어요";
        }

        void DrawPlaceBar()
        {
            var B = BUILDING[farm.PlaceId];
            if (farm.Moving)
            {
                placeTitle.text = $"{B.n} 옮기기 <size=70%>({B.w}×{B.h}칸)</size>";
                placeSub.text = farm.PlaceOk ? "<color=#3a8a2a>초록 칸</color>에서 놓으면 옮겨져요 · 오른쪽 클릭·Esc 취소"
                    : "<color=#d23a2a>빨간 칸</color>이 있으면 놓을 수 없어요 (울타리 밖 · 나무 밑동 · 다른 건물)";
                return;
            }
            placeTitle.text = $"{B.n} <size=70%>({B.w}×{B.h}칸 · {UIUtil.Ic("dia")}{B.price} · {TimeText(BuildSeconds(B, null))})</size>";
            placeSub.text = farm.PlaceOk ? "<color=#3a8a2a>초록 칸</color>에서 왼쪽 클릭하면 바로 지어요 · 오른쪽 클릭·Esc 취소"
                : "<color=#d23a2a>빨간 칸</color>이 있으면 지을 수 없어요 (울타리 밖 · 나무 밑동 · 다른 건물)";
        }
    }
}
