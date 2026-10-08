using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Mycelium tree in world space (prototype drawTree + tree mouse handlers): soil background, trunks to the
    // three hubs, nodes (children with TreeNodeView), grandpa in the center. Drag to pan, wheel/pinch to zoom,
    // click to buy, Shift+click / right click / long press to buy as many levels as gold allows.
    public class TreeView : MonoBehaviour
    {
        public Transform content;                 // pan/zoom root; node objects live under content/Nodes
        public SpriteRenderer soil;               // radial soil background (screen-fixed)
        public LineRenderer[] trunks = new LineRenderer[3];
        public TextMeshPro[] hubLabels = new TextMeshPro[3];
        public SpriteRenderer centerGlow;
        public GrandpaRig grandpa;
        [ScenePath("Canvas/TreeScreen")] public TreeScreen screen;
        [ScenePath("World/Round")] public RoundController stageMapper;       // reuses its screen → stage mapping

        public float camX, camY, camZ = 0.95f;
        readonly List<TreeNodeView> nodes = new List<TreeNodeView>();
        readonly Dictionary<string, TreeNodeView> byId = new Dictionary<string, TreeNodeView>();
        Dictionary<string, Vector2> hubs;
        TreeNodeView hover, sel;
        Vector2? dragStart; Vector2 dragCam; bool dragMoved;
        float pressT = -1; bool pressDone;
        float pinchD, pinchZ; bool pinching;

        void Awake()
        {
            content.GetComponentsInChildren(true, nodes);
            foreach (var n in nodes) byId[n.nodeId] = n;
            hubs = TreeLayout.Hubs();
        }

        public Vector2 W2S(Vector2 p) => new Vector2((p.x - camX) * camZ + 960, (p.y - camY) * camZ + 560);
        public Vector2 S2W(Vector2 s) => new Vector2((s.x - 960) / camZ + camX, (s.y - 560) / camZ + camY);

        Vector2 ParentPos(Node n) => n.parent != null ? byId[n.parent].TreePos : hubs[n.br];

        void Update()
        {
            if (G == null) return;
            float dt = Time.unscaledDeltaTime, now = Time.time;
            content.localScale = new Vector3(camZ, camZ, 1);
            content.localPosition = Art.P(960 - camX * camZ, 560 - camY * camZ);
            soil.transform.localPosition = Art.P(960, 540, 1);
            Art.SizePx(soil.transform, soil.sprite, W, H);

            for (int i = 0; i < 3; i++)
            {
                string br = CAT_KEYS[i]; var h = hubs[br];
                bool lit = NODES.Any(n => n.br == br && Lv(n.id) > 0);
                trunks[i].positionCount = 2;
                trunks[i].SetPosition(0, Vector3.zero); trunks[i].SetPosition(1, Art.P(h.x, h.y));
                trunks[i].startColor = trunks[i].endColor = lit ? U.Hex("#f2e6c8") : new Color(240 / 255f, 225 / 255f, 195 / 255f, 0.45f);
                hubLabels[i].text = CATS[br].name + " 균사";
                hubLabels[i].transform.localPosition = Art.P(h.x + (br == "ed" ? 70 : br == "md" ? 58 : -58), h.y + (br == "ed" ? 0 : 34));
            }
            grandpa.sparkle = NODES.Any(n => { var s = NodeState(n); return (s == "avail" || s == "owned") && CanAfford(NodeCost(n, Lv(n.id))); });
            grandpa.transform.localPosition = Art.P(-14, 82 + Mathf.Sin(now * 2) * 2);

            HandleInput();
            foreach (var nv in nodes) nv.Draw(ParentPos(nv.Node), now, hover == nv, camZ, dt);
        }

        TreeNodeView Hit(Vector2 s)
        {
            var p = S2W(s);
            TreeNodeView best = null; float bd = 1e9f;
            foreach (var nv in nodes)
            {
                if (NodeState(nv.Node) == "hidden") continue;
                float dd = (p - nv.TreePos).sqrMagnitude, r = TreeNodeView.Radius(nv.Node) + 8;
                if (dd < r * r && dd < bd) { bd = dd; best = nv; }
            }
            return best;
        }

        void HandleInput()
        {
            if (GameFlow.I.ModalOpen) { screen.tooltip.Hide(); return; }
            var ts = Touchscreen.current;
            // 두 손가락 확대·축소
            if (ts != null && ts.touches.Count(t => t.press.isPressed) >= 2)
            {
                var tt = ts.touches.Where(t => t.press.isPressed).Take(2).Select(t => stageMapper.ToStage(t.position.ReadValue())).ToArray();
                float d = Vector2.Distance(tt[0], tt[1]); Vector2 mid = (tt[0] + tt[1]) / 2;
                if (!pinching) { pinching = true; pinchD = Mathf.Max(1, d); pinchZ = camZ; dragStart = null; pressT = -1; }
                var before = S2W(mid);
                camZ = Mathf.Clamp(pinchZ * d / pinchD, 0.3f, 1.8f);
                var after = S2W(mid);
                camX += before.x - after.x; camY += before.y - after.y;
                return;
            }
            if (pinching) { pinching = false; return; }

            var p = Pointer.current;
            if (p == null) return;
            bool isMouse = p is Mouse;
            var s = stageMapper.ToStage(p.position.ReadValue());
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(isMouse ? -1 : 0);

            var mouse = Mouse.current;
            if (mouse != null && isMouse && !overUI)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0)
                {
                    var before = S2W(s);
                    camZ = Mathf.Clamp(camZ * (wheel < 0 ? 0.9f : 1.1f), 0.3f, 1.8f);
                    var after = S2W(s);
                    camX += before.x - after.x; camY += before.y - after.y;
                }
                if (mouse.rightButton.wasPressedThisFrame) { var n = Hit(s); if (n != null) Buy(n, s, true); }
            }

            if (p.press.wasPressedThisFrame && !overUI)
            {
                dragStart = s; dragCam = new Vector2(camX, camY); dragMoved = false;
                if (!isMouse) { pressT = Time.unscaledTime; pressDone = false; }
            }
            if (dragStart.HasValue && p.press.isPressed)
            {
                var d = s - dragStart.Value;
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) > 5) dragMoved = true;
                if (dragMoved) { camX = dragCam.x - d.x / camZ; camY = dragCam.y - d.y / camZ; }
                // 길게 누르기 = 끝까지 강화
                if (!isMouse && pressT >= 0 && !dragMoved && Time.unscaledTime - pressT > 0.5f && !pressDone)
                {
                    var n = Hit(s);
                    if (n != null) { pressDone = true; Buy(n, s, true); }
                    pressT = -1;
                }
            }
            if (isMouse || dragStart.HasValue)
            {
                hover = Hit(s);
                if (hover != null) screen.tooltip.Show(hover.Node, s); else if (isMouse || !dragStart.HasValue) screen.tooltip.Hide();
            }
            if (dragStart.HasValue && p.press.wasReleasedThisFrame)
            {
                bool moved = dragMoved; dragStart = null; pressT = -1;
                if (pressDone) { pressDone = false; return; }
                if (moved) return;
                var n = Hit(s);
                bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                if (n == null) { if (!isMouse) { sel = null; hover = null; screen.tooltip.Hide(); } return; }
                // 손가락: 처음 누른 노드는 정보만, 같은 노드를 한 번 더 누르면 강화
                if (!isMouse && sel != n) { sel = n; hover = n; screen.tooltip.Show(n.Node, s); Snd.Ui(); return; }
                Buy(n, s, shift);
            }
        }

        void Buy(TreeNodeView nv, Vector2 s, bool max)
        {
            var n = nv.Node;
            string st = NodeState(n);
            if (st == "locked") { Snd.Err(); GameFlow.I.ShowToast(n.needMax ? "앞 단계를 최대 레벨까지 올려야 열려요" : "선행 노드를 먼저 사야 열려요"); return; }
            if (st == "max") return;
            int before = Lv(n.id);
            int k = max ? BuyMax(n) : BuyNode(n) ? 1 : 0;
            if (k > 0)
            {
                Snd.Buy(); if (before == 0) nv.growAnim = 0;
                screen.Refresh(); screen.tooltip.Show(n, s);
                screen.Burst(s, U.Hex(CATS[n.br].color));
                if (k > 1) GameFlow.I.ShowToast($"{n.n} +{k}단계 한 번에 강화!");
            }
            else
            {
                Snd.Err();
                GameFlow.I.ShowToast(n.gem && G.gold >= NodeCost(n, Lv(n.id)).gold ? "균사석이 부족해요 (버섯 농장에서 얻어요)" : "골드가 부족해요");
            }
        }

        public void MarkGrown(IEnumerable<string> ids) { foreach (var id in ids) if (byId.TryGetValue(id, out var nv)) nv.growAnim = 0; }
    }
}
