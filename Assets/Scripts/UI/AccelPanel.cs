using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Speed-up modal (균사석 가속): finish a building now, or ripen every growing mushroom on the tree now.
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
            int cost; double left;
            if (kind == "build")
            {
                var b = G.farm.blds.Find(x => x.uid == uid);
                if (b == null || b.done || Now() >= b.at) { GameFlow.I.CloseModal(); return; }
                var B = BUILDING[b.id];
                icon.sprite = SpriteDB.Get("Farm/Props/" + b.id);
                title.text = $"{B.n} 건설 가속";
                string who = SPC.TryGetValue(b.critter ?? "", out var k) ? EvoName(k) : "꼬마";   // 예전·손상 세이브에는 짓는 꼬마가 없을 수 있다
                desc.text = $"{U.Iga(who)} 짓고 있어요. 균사석으로 지금 바로 완성할 수 있어요. <color=#8a6a4a>(남은 {U.Fmt(BUILD_ACCEL_SEC / 60)}분마다 균사석 1개)</color>";
                left = (b.at - Now()) / 1000; cost = BuildAccelCost(b);
            }
            else
            {
                icon.sprite = SpriteDB.Get("Farm/Tree/tree_" + TREE.Stage(TreeLv()));
                title.text = "버섯 빨리 자라게 하기";
                desc.text = $"버섯 나무에서 자라고 있는 버섯이 모두 지금 다 자라요. <color=#8a6a4a>(남은 시간 합 {U.Fmt(TREE.accelSec / 60)}분마다 균사석 1개)</color>";
                double now = Now(); left = 0; int n = 0;
                foreach (var f in G.farm.tree.slots) if (f.at > now) { left = System.Math.Max(left, (f.at - now) / 1000); n++; }
                if (n == 0) { GameFlow.I.CloseModal(); return; }
                cost = TreeAccelCost();
            }
            timeText.text = $"{UIUtil.Ic("clock")} 남은 시간 {Mmss((long)System.Math.Ceiling(left))}";
            costText.text = $"{UIUtil.Ic("gem")}{cost} 가속  <size=70%>(보유 {U.Fmt(G.gem)})</size>";
            accel.interactable = G.gem >= cost;
        }
    }
}
