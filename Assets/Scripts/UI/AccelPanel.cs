using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Speed-up modal: finish a building now (균사석), or ripen every growing mushroom on the tree now (골드).
    // kind = "build" (uid = building) or "tree".
    public class AccelPanel : MonoBehaviour
    {
        public string kind; public int uid;
        public Image icon;
        public TMP_Text title, desc, timeText, costText;
        public Button accel;

        float t;
        void OnEnable() { t = 0; }
        void Update() { if (G != null && (t -= Time.unscaledDeltaTime) <= 0) { t = 0.5f; Render(); } }

        public void Render()
        {
            double cost, have; double left; string cur;
            if (kind == "build")
            {
                var b = G.farm.blds.Find(x => x.uid == uid);
                if (b == null || b.done || Now() >= b.at) { GameFlow.I.CloseModal(); return; }
                var B = BUILDING[b.id];
                icon.sprite = SpriteDB.Get("Farm/Props/" + b.id);
                title.text = Loc.F("{0} 건설 가속", B.n);
                string who = SPC.TryGetValue(b.critter ?? "", out var k) ? EvoName(k) : Loc.T("꼬마");   // 예전·손상 세이브에는 짓는 꼬마가 없을 수 있다
                desc.text = Loc.F("{0} 짓고 있어요. 균사석으로 지금 바로 완성할 수 있어요. <color=#8a6a4a>(남은 {1}분마다 균사석 1개)</color>", U.Iga(who), U.Fmt(BUILD_ACCEL_SEC / 60));
                left = (b.at - Now()) / 1000; cost = BuildAccelCost(b); cur = "gem"; have = G.gem;
            }
            else
            {
                icon.sprite = SpriteDB.Get("Farm/Tree/tree_" + TREE.Stage(TreeLv()));
                title.text = Loc.T("버섯 빨리 자라게 하기");
                desc.text = Loc.F("세계수 나무에서 자라고 있는 버섯이 모두 지금 다 자라요. <color=#8a6a4a>(남은 시간 합 {0}분마다 한 판 수입만큼 골드)</color>", U.Fmt(TREE.accelSec / 60));
                double now = Now(); left = 0; int n = 0;
                foreach (var f in G.farm.tree.slots) if (f.at > now) { left = System.Math.Max(left, (f.at - now) / 1000); n++; }
                if (n == 0) { GameFlow.I.CloseModal(); return; }
                cost = TreeAccelCost(); cur = "gold"; have = G.gold;
            }
            timeText.text = Loc.F("{0} 남은 시간 {1}", UIUtil.Ic("clock"), Mmss((long)System.Math.Ceiling(left)));
            costText.text = Loc.F("{0}{1} 가속  <size=70%>(보유 {2})</size>", UIUtil.Ic(cur), U.Fmt(cost), U.Fmt(have));
            accel.interactable = have >= cost;
        }
    }
}
