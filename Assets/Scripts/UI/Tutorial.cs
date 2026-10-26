using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 튜토리얼 (기획서 "숨쉬었음청년_튜토리얼" Ver.01). 화면을 어둡게 덮고 한 곳만 구멍을 내 밝혀 그곳만 누를 수 있게 한다.
    //  G.tut 0: Stage — 1스테이지 조준. 수확기 → 버섯 길만 밝히고, 수확기를 잡고 당기는 손가락을 깜박인다. 발사하면 게임 화면만 보인다
    //           MainGame — 1스테이지를 중간에 끝내고 돌아왔으면 출발 버튼(Button_StageStart)만 밝힌다
    //  G.tut 1: MainGame — 1스테이지 클리어 뒤 트리. 시작 노드만 보이고 밝힌다(이어진 노드는 사기 전까지 숨김). 사면 카메라가 물러나며 이어진 노드가 나온다
    //  G.tut 2: MainGame — 첫 노드 산 뒤. 출발 버튼만 밝힌다. 누르면 끝(G.tut 3, RoundFlow.Request)
    // 옛 한 씬 UI(GameFlow)에서 씬 분리 구조로 옮김. 오버레이는 코드로 만든 자기 캔버스(씬 수정 없음)이고 화면 픽셀(왼쪽 아래 원점)로 놓는다
    public class Tutorial : MonoBehaviour
    {
        // 트리 노드를 튜토리얼이 숨기는가: 첫 노드를 사기 전엔 시작 노드 말고 전부
        public static bool HidesNode(Node n) => G != null && G.tut == 1 && n.id != ROOT_NODE;

        // Stage 씬: 튜토리얼 판(1스테이지 첫 판)일 때만
        public static void ForStage(StageController sc)
        {
            if (G == null || G.tut != 0 || sc == null) return;
            Make(sc.gameObject.scene).stage = sc;
        }

        // MainGame 씬: 튜토리얼이 끝나지 않았으면
        public static void ForMain(Scene scene, TreeView tree, Transform startButton)
        {
            if (G == null || G.tut >= 3) return;
            var t = Make(scene); t.tree = tree; t.startButton = startButton as RectTransform;
        }

        static Tutorial Make(Scene scene)
        {
            var go = new GameObject("Tutorial");
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<Tutorial>();
        }

        const float DIM_A = 0.78f;   // 선형 색 공간이라 값보다 옅게 보인다
        const int SORT = 800;        // 팝업·이동 버튼 위, 정산 창(1000) 아래
        static readonly Color FRAME = new Color(1f, 0.82f, 0.25f, 1f), TEXT = new Color(1f, 0.95f, 0.82f, 1f);

        StageController stage;
        TreeView tree;
        RectTransform startButton;

        RectTransform root;
        readonly Image[] dims = new Image[4];
        readonly Image[] frame = new Image[4];   // 구멍 테두리 (위·아래·왼쪽·오른쪽 막대, 깜박임)
        RectTransform arrow, finger, fingerRing;
        TMP_Text label;
        float k = 1;                  // 1920×1080 기준 1px → 화면 px
        float zoomFrom = -1, zoomT;   // 첫 노드를 산 뒤 카메라가 물러나는 연출
        int shownTut = -1;

        void Awake()
        {
            var c = gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = SORT;
            gameObject.AddComponent<GraphicRaycaster>();   // 어두운 칸이 구멍 밖 클릭을 막는다
            root = new GameObject("Root", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
            for (int i = 0; i < 4; i++) { dims[i] = MakeImage("Dim" + i, null, new Color(0, 0, 0, DIM_A)); dims[i].raycastTarget = true; }
            for (int i = 0; i < 4; i++) frame[i] = MakeImage("Frame" + i, null, FRAME);
            arrow = MakeImage("Arrow", SpriteDB.Get("UI/sign_arrow"), Color.white).rectTransform;
            fingerRing = MakeImage("FingerRing", SpriteDB.Get("FX/ring"), FRAME).rectTransform;
            finger = MakeImage("Finger", SpriteDB.Get("FX/circle"), Color.white).rectTransform;
            var lt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lt.transform.SetParent(root, false);
            label = lt.GetComponent<TextMeshProUGUI>();
            LocFonts.Apply(label, LocFonts.Role.Title, 0);
            label.color = TEXT; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.enableAutoSizing = true;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero; label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            root.gameObject.SetActive(false);
        }

        Image MakeImage(string name, Sprite sp, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.color = col; img.raycastTarget = false; img.preserveAspect = sp != null;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        void LateUpdate()
        {
            if (G == null || G.tut >= 3) { Destroy(gameObject); return; }
            root.sizeDelta = new Vector2(Screen.width, Screen.height);
            k = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            bool on = !App.ModalOpen && (stage != null ? StepStage() : StepMain());
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
        }

        // ===== Stage: 첫 조준 =====
        bool StepStage()
        {
            var R = RoundSim.R;
            if (R == null || !R.tutorial || R.phase != "aim" || R.TutorialColony == null || R.paused) return false;
            var m = R.TutorialColony;
            float z = RoundSim.ZOOM;
            Vector2 launch = RoundSim.LAUNCH * z, sh = new Vector2(m.x, m.y) * z;
            float top = sh.y - m.R * z - 30, bottom = launch.y + 110;
            float half = Mathf.Max(120, m.R * z + 20);
            Hole(FromStage(new Rect(launch.x - half, top, 2 * half, bottom - top)));
            // 길 가운데 위쪽 화살표 + 수확기에서 아래로 당기는 손가락 (깜박임)
            float now = Time.unscaledTime, t = Mathf.Repeat(now / 1.4f, 1f);
            Place(arrow, S(launch.x, (top + launch.y) / 2 - 20), new Vector2(150, 98), 90, 1);
            Place(fingerRing, S(launch.x, launch.y), new Vector2(150, 150), 0, 0.6f + 0.4f * Mathf.Sin(now * 6));
            Place(finger, S(launch.x, launch.y + Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 1.4f)) * 130), new Vector2(54, 54), 0, t < 0.75f ? 1 : 1 - (t - 0.75f) / 0.25f);
            SetLabel(Loc.T("수확기를 잡고 아래로 당겼다 놓으면 버섯을 향해 날아가요!"), S(launch.x - half - 560, launch.y - 160));
            return true;
        }

        Vector2 S(float x, float y) => StageSpace.ToScreen(stage.stageRect, stage.cam, new Vector2(x, y));

        Rect FromStage(Rect r)
        {
            Vector2 a = S(r.xMin, r.yMax), b = S(r.xMax, r.yMin);   // y가 뒤집힌다
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // ===== MainGame: 첫 노드 → 출발 =====
        bool StepMain()
        {
            float now = Time.unscaledTime;
            if (tree != null && tree.isActiveAndEnabled)
            {
                if (G.tut == 1 && Lv(ROOT_NODE) > 0)
                {
                    // 첫 노드를 샀다 → 이어진 노드가 나오고 카메라가 물러난다 (기획서 "카메라 줌 아웃")
                    G.tut = 2; SaveGame();
                    zoomFrom = tree.camZ; zoomT = 0;
                }
                if (zoomFrom > 0)
                {
                    zoomT += Time.unscaledDeltaTime / 0.9f;
                    tree.camZ = Mathf.Lerp(zoomFrom, 0.85f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(zoomT)));
                    var rp = tree.Find(ROOT_NODE);
                    if (rp != null) { tree.camX = Mathf.Lerp(tree.camX, rp.TreePos.x, 0.2f); tree.camY = Mathf.Lerp(tree.camY, rp.TreePos.y + 60, 0.2f); }
                    if (zoomT >= 1) zoomFrom = -1;
                }
                if (G.tut == 1)
                {
                    var nv = tree.Find(ROOT_NODE);
                    if (nv == null) return false;
                    if (shownTut != 1) { shownTut = 1; tree.camX = nv.TreePos.x; tree.camY = nv.TreePos.y; tree.camZ = 1.25f; }   // 시작 노드를 가운데로 당겨 보여 준다
                    Vector2 p = tree.W2S(nv.TreePos);
                    float r = 95 * tree.camZ;
                    Hole(FromTree(new Rect(p.x - r, p.y - r, 2 * r, 2 * r)));
                    Place(arrow, TS(p.x - r - 110, p.y), new Vector2(150, 98), 0, 0.75f + 0.25f * Mathf.Sin(now * 6));
                    Hide(finger); Hide(fingerRing);
                    SetLabel(Loc.T("첫 강화를 사 보세요! 밝게 빛나는 노드를 누르세요"), TS(p.x, p.y - r - 90));
                    return true;
                }
            }
            shownTut = G.tut;
            if (G.tut == 1) return false;   // 트리가 아직 안 보인다
            // tut 0(1스테이지를 마치지 못하고 돌아옴) · tut 2(첫 노드를 산 뒤): 출발 버튼만 밝힌다
            if (zoomFrom > 0) return DimOnly();
            if (startButton == null || !startButton.gameObject.activeInHierarchy) return false;
            var b = ScreenRect(startButton);
            float pad = 14 * k;
            Hole(new Rect(b.x - pad, b.y - pad, b.width + 2 * pad, b.height + 2 * pad));
            float bob = Mathf.Sin(now * 6) * 10 * k;
            Place(arrow, new Vector2(b.xMin - 90 * k + bob, b.yMax + 70 * k - bob), new Vector2(150, 98), -35, 0.75f + 0.25f * Mathf.Sin(now * 6));
            Hide(finger); Hide(fingerRing);
            SetLabel(G.tut == 2 ? Loc.T("강해졌어요! 다시 수확하러 가 볼까요?") : Loc.T("수확하러 가서 첫 스테이지를 마쳐 보세요!"),
                     new Vector2(b.center.x - 420 * k, b.yMax + 170 * k));
            return true;
        }

        Vector2 TS(float x, float y) => StageSpace.ToScreen(tree.stageRect, tree.cam, new Vector2(x, y));

        Rect FromTree(Rect r)
        {
            Vector2 a = TS(r.xMin, r.yMax), b = TS(r.xMax, r.yMin);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        bool DimOnly()
        {
            Hole(Rect.zero);
            Hide(arrow); Hide(finger); Hide(fingerRing); label.gameObject.SetActive(false);
            return true;
        }

        // ===== 그리기 (화면 px, 왼쪽 아래 원점) =====
        // 구멍 밖 네 칸을 어둡게 (구멍은 클릭이 통과한다)
        void Hole(Rect h)
        {
            float W = Screen.width, H = Screen.height;
            float x0 = Mathf.Clamp(h.xMin, 0, W), x1 = Mathf.Clamp(h.xMax, 0, W), y0 = Mathf.Clamp(h.yMin, 0, H), y1 = Mathf.Clamp(h.yMax, 0, H);
            Box(dims[0], 0, y1, W, H - y1);        // 위
            Box(dims[1], 0, 0, W, y0);             // 아래
            Box(dims[2], 0, y0, x0, y1 - y0);      // 왼쪽
            Box(dims[3], x1, y0, W - x1, y1 - y0); // 오른쪽
            bool any = h.width > 0;
            float T = 6 * k;
            var c = FRAME; c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6);   // 깜박임
            for (int i = 0; i < 4; i++) { frame[i].gameObject.SetActive(any); frame[i].color = c; }
            if (!any) return;
            Box(frame[0], h.xMin - T, h.yMax, h.width + 2 * T, T);
            Box(frame[1], h.xMin - T, h.yMin - T, h.width + 2 * T, T);
            Box(frame[2], h.xMin - T, h.yMin, T, h.height);
            Box(frame[3], h.xMax, h.yMin, T, h.height);
        }

        static void Box(Graphic g, float x, float y, float w, float h)
        {
            var rt = g.rectTransform;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
            rt.anchoredPosition = new Vector2(x, y);
        }

        // size는 1920×1080 기준 px
        void Place(RectTransform rt, Vector2 screen, Vector2 size, float rotZ, float alpha)
        {
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size * k;
            rt.anchoredPosition = screen;
            rt.localEulerAngles = new Vector3(0, 0, rotZ);
            var g = rt.GetComponent<Graphic>(); var c = g.color; c.a = Mathf.Clamp01(alpha); g.color = c;
        }

        static void Hide(RectTransform rt) { if (rt.gameObject.activeSelf) rt.gameObject.SetActive(false); }

        void SetLabel(string s, Vector2 screen)
        {
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            UIUtil.SetText(label, s);
            label.fontSizeMax = 40 * k; label.fontSizeMin = 26 * k;
            label.rectTransform.sizeDelta = new Vector2(1100, 140) * k;
            float hw = 550 * k, hh = 80 * k;
            label.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(screen.x, hw, Screen.width - hw), Mathf.Clamp(screen.y, hh, Screen.height - hh));
        }

        // UI 요소의 화면 사각형
        static Rect ScreenRect(RectTransform t)
        {
            var c = new Vector3[4]; t.GetWorldCorners(c);
            var canvas = t.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
