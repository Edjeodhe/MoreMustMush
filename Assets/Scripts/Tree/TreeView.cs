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
    // 임시: 새 UI(MainGame)에 트리 칸이 배치되기 전까지 옛 월드 트리를 MainGame에 그대로 쓴다.
    public class TreeView : MonoBehaviour
    {
        public Transform content;                 // pan/zoom root; node objects live under content/Nodes
        public SpriteRenderer soil;               // radial soil background (screen-fixed)
        public LineRenderer[] trunks = new LineRenderer[3];
        public TextMeshPro[] hubLabels = new TextMeshPro[3];
        public SpriteRenderer centerGlow;
        public GrandpaRig grandpa;
        public TreeTooltip tooltip;               // MainGame 캔버스의 노드 설명 창
        [Tooltip("1920×1080 기준 캔버스 영역 (화면 → 스테이지 좌표). 비우면 화면 전체를 1920×1080으로 본다")]
        public RectTransform stageRect;
        public Camera cam;

        public float camX, camY, camZ = 0.95f;
        readonly List<TreeNodeView> nodes = new List<TreeNodeView>();
        readonly Dictionary<string, TreeNodeView> byId = new Dictionary<string, TreeNodeView>();
        TreeNodeView hover, sel;
        Vector2? dragStart; Vector2 dragCam; bool dragMoved;
        float pressT = -1; bool pressDone;
        float pinchD, pinchZ; bool pinching;

        void Awake()
        {
            content.GetComponentsInChildren(true, nodes);
            foreach (var n in nodes) byId[n.nodeId] = n;
        }

        public Vector2 W2S(Vector2 p) => new Vector2((p.x - camX) * camZ + 960, (p.y - camY) * camZ + 560);
        public Vector2 S2W(Vector2 s) => new Vector2((s.x - 960) / camZ + camX, (s.y - 560) / camZ + camY);

        Vector2 ParentPos(Node n) => n.parent != null ? byId[n.parent].TreePos : Vector2.zero;   // 시작 노드는 부모 줄을 그리지 않는다

        void Update()
        {
            if (G == null) return;
            float dt = Time.unscaledDeltaTime, now = Time.time;
            content.localScale = new Vector3(camZ, camZ, 1);
            content.localPosition = Art.P(960 - camX * camZ, 560 - camY * camZ);
            soil.transform.localPosition = Art.P(960, 540, 1);
            Art.SizePx(soil.transform, soil.sprite, W, H);

            // 가지 코어(식용·약용·독 균사)가 없어졌다: 가운데에서 허브로 가는 줄기·가지 이름표를 끈다
            if (!trunksOff)
            {
                trunksOff = true;
                for (int i = 0; i < trunks.Length; i++) { trunks[i].positionCount = 0; trunks[i].enabled = false; }
                foreach (var l in hubLabels) if (l != null) l.gameObject.SetActive(false);
            }
            grandpa.transform.localPosition = Art.P(-14, 82 + Mathf.Sin(now * 2) * 2);

            HandleInput();
            ClampCam();
            foreach (var nv in nodes) nv.Draw(ParentPos(nv.Node), now, hover == nv, camZ, dt);
        }

        bool trunksOff;

        // 카메라가 트리 밖으로 나가지 않게: 화면 가운데가 보이는 노드(숨김 제외)와 가운데 할아버지를 감싼 영역 + 여유 안에 머문다
        const float CAM_PAD = 200;
        void ClampCam()
        {
            float x0 = 0, x1 = 0, y0 = 0, y1 = 0;
            foreach (var nv in nodes)
            {
                if (Hidden(nv.Node)) continue;
                var p = nv.TreePos;
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
            }
            camX = Mathf.Clamp(camX, x0 - CAM_PAD, x1 + CAM_PAD);
            camY = Mathf.Clamp(camY, y0 - CAM_PAD, y1 + CAM_PAD);
        }

        TreeNodeView Hit(Vector2 s)
        {
            var p = S2W(s);
            TreeNodeView best = null; float bd = 1e9f;
            foreach (var nv in nodes)
            {
                if (Hidden(nv.Node)) continue;
                float dd = (p - nv.TreePos).sqrMagnitude, r = TreeNodeView.Radius(nv.Node) + 8;
                if (dd < r * r && dd < bd) { bd = dd; best = nv; }
            }
            return best;
        }

        void HandleInput()
        {
            // 팝업이 열리면 진행 중이던 드래그·핀치·길게 누르기를 버린다 (놓는 입력을 못 받으므로)
            if (App.ModalOpen) { tooltip.Hide(); hover = sel = null; dragStart = null; pinching = false; pressT = -1; pressDone = false; return; }
            var ts = Touchscreen.current;
            // 두 손가락 확대·축소
            int pressed = 0;
            if (ts != null) for (int i = 0; i < ts.touches.Count; i++) if (ts.touches[i].press.isPressed) pressed++;
            if (pressed >= 2)
            {
                var tt = ts.touches.Where(t => t.press.isPressed).Take(2).Select(t => StageSpace.FromScreen(stageRect, cam, t.position.ReadValue())).ToArray();
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
            var s = StageSpace.FromScreen(stageRect, cam, p.position.ReadValue());
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();   // -1 = 지금 포인터 (터치 id는 0이 아니라 0을 넘기면 UI 위 판정이 틀렸다)

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
            if (overUI && !dragStart.HasValue) { hover = null; tooltip.Hide(); }   // 버튼·창 위에서는 가려진 노드를 고르지 않는다
            else if (isMouse || dragStart.HasValue)
            {
                hover = Hit(s);
                if (hover != null) tooltip.Show(hover.Node, s); else if (isMouse || !dragStart.HasValue) tooltip.Hide();
            }
            if (dragStart.HasValue && p.press.wasReleasedThisFrame)
            {
                bool moved = dragMoved; dragStart = null; pressT = -1;
                if (pressDone) { pressDone = false; return; }
                if (moved) return;
                var n = Hit(s);
                bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                if (n == null) { if (!isMouse) { sel = null; hover = null; tooltip.Hide(); } return; }
                // 손가락: 처음 누른 노드는 정보만, 같은 노드를 한 번 더 누르면 강화
                if (!isMouse && sel != n) { sel = n; hover = n; tooltip.Show(n.Node, s); Snd.Ui(); return; }
                Buy(n, s, shift);
            }
        }

        void Buy(TreeNodeView nv, Vector2 s, bool max)
        {
            var n = nv.Node;
            string st = NodeState(n);
            if (st == "locked") { Snd.Err(); App.Notify(n.needMax ? Loc.T("앞 단계를 최대 레벨까지 올려야 열려요") : Loc.T("선행 노드를 먼저 사야 열려요")); return; }
            if (st == "max") return;
            int before = Lv(n.id);
            int k = max ? BuyMax(n) : BuyNode(n) ? 1 : 0;
            if (k > 0)
            {
                Snd.Buy(); if (before == 0) nv.growAnim = 0;
                tooltip.Show(n, s);
                if (k > 1) App.Notify(Loc.F("{0} +{1}단계 한 번에 강화!", n.n, k));
            }
            else
            {
                Snd.Err();
                App.Notify(n.gem && G.gold >= NodeCost(n, Lv(n.id)).gold ? Loc.T("균사석이 부족해요 (세계수에서 얻어요)") : Loc.T("골드가 부족해요"));
            }
        }

        // 트리 화면에서 숨기는 노드: 게임 규칙상 숨김 + 잠긴 노드("?"). 살 수 있게 되면(부모를 사거나 최대 레벨로) 나타난다
        public static bool Hidden(Node n)
        {
            string s = NodeState(n);
            return s == "hidden" || s == "locked";
        }

        public void MarkGrown(IEnumerable<string> ids) { foreach (var id in ids) if (byId.TryGetValue(id, out var nv)) nv.growAnim = 0; }
    }
}
