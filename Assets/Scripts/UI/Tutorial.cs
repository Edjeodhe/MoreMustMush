using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 튜토리얼 (기획서 "숨쉬었음청년_튜토리얼" Ver.01). 화면을 어둡게 덮고 한 곳만 구멍을 내 밝혀 그곳만 누를 수 있게 한다.
    //  G.tut 0: 1스테이지 조준 — 수확기 → 버섯 길만 밝히고, 수확기를 잡고 당기는 손가락을 깜박인다. 발사하면 게임 화면만 보인다
    //           (1스테이지를 중간에 끝내고 트리로 왔으면 "수확하러 가기"만 밝힌다)
    //  G.tut 1: 1스테이지 클리어 뒤 트리 — 시작 노드만 보이고 밝힌다(이어진 노드는 사기 전까지 숨김). 사면 카메라가 물러나며 이어진 노드가 나온다
    //  G.tut 2: 첫 노드 산 뒤 — "수확하러 가기"만 밝힌다. 누르면 끝(G.tut 3, GameFlow.BeginRound)
    // 오버레이는 코드로 만든다 (씬 수정 없음). 메인 캔버스는 늘 1920×1080이라 스테이지 픽셀(왼쪽 위 원점, y 아래)로 놓는다
    public class Tutorial : MonoBehaviour
    {
        // 트리 노드를 튜토리얼이 숨기는가: 강화가 열리기 전(1스테이지 전)엔 전부, 첫 노드를 사기 전엔 시작 노드 말고 전부
        public static bool HidesNode(Node n)
        {
            if (G == null) return false;
            if (!Unlock.On(Unlock.UPGRADE)) return true;
            return G.tut == 1 && n.id != ROOT_NODE;
        }

        const float DIM_A = 0.78f;   // 선형 색 공간이라 값보다 옅게 보인다
        static readonly Color FRAME = new Color(1f, 0.82f, 0.25f, 1f), TEXT = new Color(1f, 0.95f, 0.82f, 1f);

        RectTransform canvas, root;
        readonly Image[] dims = new Image[4];
        readonly Image[] frame = new Image[4];   // 구멍 테두리 (위·아래·왼쪽·오른쪽 막대, 깜박임)
        RectTransform arrow, finger, fingerRing;
        TMP_Text label;
        Image fingerImg, fingerRingImg, arrowImg;
        float zoomFrom = -1, zoomT;   // 첫 노드를 산 뒤 카메라가 물러나는 연출
        int shownTut = -1;

        void Start()
        {
            canvas = GameFlow.I.round.stage;
            root = new GameObject("Tutorial", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas, false);
            root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(0, 1);
            root.sizeDelta = new Vector2(1920, 1080); root.anchoredPosition = Vector2.zero;
            for (int i = 0; i < 4; i++) { dims[i] = MakeImage("Dim" + i, null, new Color(0, 0, 0, DIM_A)); dims[i].raycastTarget = true; }   // 구멍 밖 클릭을 막는다
            for (int i = 0; i < 4; i++) frame[i] = MakeImage("Frame" + i, null, FRAME);
            arrowImg = MakeImage("Arrow", SpriteDB.Get("UI/sign_arrow"), Color.white); arrow = arrowImg.rectTransform; arrow.sizeDelta = new Vector2(150, 98);
            fingerRingImg = MakeImage("FingerRing", SpriteDB.Get("FX/ring"), FRAME); fingerRing = fingerRingImg.rectTransform; fingerRing.sizeDelta = new Vector2(150, 150);
            fingerImg = MakeImage("Finger", SpriteDB.Get("FX/circle"), Color.white); finger = fingerImg.rectTransform; finger.sizeDelta = new Vector2(54, 54);
            var lt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lt.transform.SetParent(root, false);
            label = lt.GetComponent<TextMeshProUGUI>();
            LocFonts.Apply(label, LocFonts.Role.Title, 0);
            label.fontSize = 40; label.color = TEXT; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 1); label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(1100, 140); label.enableAutoSizing = true; label.fontSizeMin = 26; label.fontSizeMax = 40;
            root.gameObject.SetActive(false);
        }

        Image MakeImage(string name, Sprite sp, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.color = col; img.raycastTarget = false; img.preserveAspect = sp != null;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        void LateUpdate()
        {
            var fl = GameFlow.I;
            if (root == null || fl == null) return;
            bool on = G != null && G.tut < 3 && !fl.ModalOpen && Step(fl);
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (on) root.SetAsLastSibling();
        }

        // 지금 단계에 맞게 그린다. 보여 줄 게 없으면 false
        bool Step(GameFlow fl)
        {
            float now = Time.unscaledTime;
            if (fl.Screen == "round")
            {
                var R = RoundSim.R;
                if (R == null || !R.tutorial || R.phase != "aim" || R.TutorialColony == null) return false;
                var m = R.TutorialColony;
                float z = RoundSim.ZOOM;
                Vector2 launch = RoundSim.LAUNCH * z, sh = new Vector2(m.x, m.y) * z;
                float top = sh.y - m.R * z - 30, bottom = launch.y + 110;
                float half = Mathf.Max(120, m.R * z + 20);
                Hole(new Rect(launch.x - half, top, 2 * half, bottom - top));
                // 길 가운데 위쪽 화살표 + 수확기에서 아래로 당기는 손가락 (깜박임)
                Place(arrow, launch.x, (top + launch.y) / 2 - 20, 90, 1);
                float k = Mathf.Repeat(now / 1.4f, 1f);
                Place(fingerRing, launch.x, launch.y, 0, 0.6f + 0.4f * Mathf.Sin(now * 6));
                Place(finger, launch.x, launch.y + Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.4f)) * 130, 0, k < 0.75f ? 1 : 1 - (k - 0.75f) / 0.25f);
                SetLabel(Loc.T("수확기를 잡고 아래로 당겼다 놓으면 버섯을 향해 날아가요!"), launch.x - half - 560, launch.y - 160);
                return true;
            }
            if (fl.Screen != "tree") return false;
            var view = fl.tree.world;
            if (G.tut == 1 && Lv(ROOT_NODE) > 0)
            {
                // 첫 노드를 샀다 → 이어진 노드가 나오고 카메라가 물러난다 (기획서 "카메라 줌 아웃")
                G.tut = 2; SaveGame();
                zoomFrom = view.camZ; zoomT = 0;
                fl.tree.Refresh();
            }
            if (zoomFrom > 0)
            {
                zoomT += Time.unscaledDeltaTime / 0.9f;
                view.camZ = Mathf.Lerp(zoomFrom, 0.85f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(zoomT)));
                var rp = view.Find(ROOT_NODE);
                if (rp != null) { view.camX = Mathf.Lerp(view.camX, rp.TreePos.x, 0.2f); view.camY = Mathf.Lerp(view.camY, rp.TreePos.y + 60, 0.2f); }
                if (zoomT >= 1) zoomFrom = -1;
            }
            if (G.tut == 1)
            {
                var nv = view.Find(ROOT_NODE);
                if (nv == null) return false;
                if (shownTut != 1) { shownTut = 1; view.camX = nv.TreePos.x; view.camY = nv.TreePos.y; view.camZ = 1.25f; }   // 시작 노드를 가운데로 당겨 보여 준다
                Vector2 p = view.W2S(nv.TreePos);
                float r = 95 * view.camZ;
                Hole(new Rect(p.x - r, p.y - r, 2 * r, 2 * r));
                Place(arrow, p.x - r - 110, p.y, 0, 0.75f + 0.25f * Mathf.Sin(now * 6));
                Hide(finger); Hide(fingerRing);
                SetLabel(Loc.T("첫 강화를 사 보세요! 밝게 빛나는 노드를 누르세요"), p.x, p.y - r - 90);
                return true;
            }
            shownTut = G.tut;
            // tut 0(1스테이지를 마치지 못하고 돌아옴) · tut 2(첫 노드를 산 뒤): "수확하러 가기"만 밝힌다
            var go = fl.tree.transform.Find("Buttons/Right/Go") as RectTransform;
            if (go == null || zoomFrom > 0) return zoomFrom > 0 && G.tut == 2 ? DimOnly() : false;
            var b = StageRect(go);
            Hole(new Rect(b.x - 14, b.y - 14, b.width + 28, b.height + 28));
            float bob = Mathf.Sin(now * 6) * 10;
            Place(arrow, b.xMin - 90 + bob, b.yMin - 70 + bob, -35, 0.75f + 0.25f * Mathf.Sin(now * 6));
            Hide(finger); Hide(fingerRing);
            SetLabel(G.tut == 2 ? Loc.T("강해졌어요! 다시 수확하러 가 볼까요?") : Loc.T("수확하러 가서 첫 스테이지를 마쳐 보세요!"), b.center.x - 420, b.yMin - 170);
            return true;
        }

        bool DimOnly()
        {
            Hole(new Rect(-10, -10, 0, 0));
            Hide(arrow); Hide(finger); Hide(fingerRing); label.gameObject.SetActive(false);
            return true;
        }

        // 구멍 밖 네 칸을 어둡게 (구멍은 클릭이 통과한다)
        void Hole(Rect h)
        {
            float x0 = Mathf.Clamp(h.xMin, 0, 1920), x1 = Mathf.Clamp(h.xMax, 0, 1920), y0 = Mathf.Clamp(h.yMin, 0, 1080), y1 = Mathf.Clamp(h.yMax, 0, 1080);
            foreach (var d in dims) d.color = new Color(0, 0, 0, DIM_A);
            Box(dims[0], 0, 0, 1920, y0);
            Box(dims[1], 0, y1, 1920, 1080 - y1);
            Box(dims[2], 0, y0, x0, y1 - y0);
            Box(dims[3], x1, y0, 1920 - x1, y1 - y0);
            bool any = h.width > 0;
            const float T = 6;
            var c = FRAME; c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6);   // 깜박임
            for (int i = 0; i < 4; i++) { frame[i].gameObject.SetActive(any); frame[i].color = c; }
            if (any)
            {
                Box(frame[0], h.xMin - T, h.yMin - T, h.width + 2 * T, T);
                Box(frame[1], h.xMin - T, h.yMax, h.width + 2 * T, T);
                Box(frame[2], h.xMin - T, h.yMin, T, h.height);
                Box(frame[3], h.xMax, h.yMin, T, h.height);
            }
        }

        static void Box(Graphic g, float x, float y, float w, float h) => Box(g.rectTransform, x, y, w, h);
        static void Box(RectTransform rt, float x, float y, float w, float h)
        {
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
            UIUtil.Place(rt, x, y);
        }

        static void Place(RectTransform rt, float x, float y, float rotZ, float alpha)
        {
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            rt.pivot = new Vector2(0.5f, 0.5f);
            UIUtil.Place(rt, x, y);
            rt.localEulerAngles = new Vector3(0, 0, rotZ);
            var g = rt.GetComponent<Graphic>(); var c = g.color; c.a = Mathf.Clamp01(alpha); g.color = c;
        }

        static void Hide(RectTransform rt) { if (rt.gameObject.activeSelf) rt.gameObject.SetActive(false); }

        void SetLabel(string s, float x, float y)
        {
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            UIUtil.SetText(label, s);
            UIUtil.Place(label.rectTransform, Mathf.Clamp(x, 560, 1360), Mathf.Clamp(y, 80, 1000));
        }

        // UI 요소의 화면 위치 → 스테이지 픽셀 사각형
        Rect StageRect(RectTransform t)
        {
            var c = new Vector3[4]; t.GetWorldCorners(c);
            Vector2 a = canvas.InverseTransformPoint(c[0]), b = canvas.InverseTransformPoint(c[2]);
            var r = canvas.rect;
            float x0 = a.x - r.xMin, x1 = b.x - r.xMin, y0 = r.yMax - b.y, y1 = r.yMax - a.y;
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
