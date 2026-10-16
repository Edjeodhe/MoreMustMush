using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 1스테이지 튜토리얼 판의 조준 안내 (기획서 "숨쉬었음청년_튜토리얼" Ver.01, 옛 UI/Tutorial.cs의 라운드 단계를 Stage 씬으로 옮김).
    // 화면을 어둡게 덮고 수확기 → 첫 군락 길만 밝힌다. 수확기를 잡고 당기는 손가락을 깜박이고, 발사하면 사라진다.
    // 씬에 배치한 UI가 없어 코드로 만든다. StageController가 튜토리얼 판(RoundSim.tutorial)에서만 붙인다.
    // 좌표는 스테이지 픽셀 (1920×1080, 왼쪽 위 원점, y 아래)
    public class StageTutorial : MonoBehaviour
    {
        const float DIM_A = 0.78f, FRAME_T = 6;
        static readonly Color FRAME = new Color(1f, 0.82f, 0.25f, 1f), TEXT = new Color(1f, 0.95f, 0.82f, 1f);

        RectTransform root, arrow, finger, fingerRing;
        readonly Image[] dims = new Image[4], frame = new Image[4];
        TMP_Text label;

        public static void Attach(StageController stage)
        {
            if (stage.GetComponent<StageTutorial>() == null) stage.gameObject.AddComponent<StageTutorial>();
        }

        void Start()
        {
            var host = GetComponent<StageController>().stageRect;
            if (host == null) { var c = TempDialog.RootCanvas(gameObject.scene); host = c != null ? (RectTransform)c.transform : null; }
            if (host == null) { Debug.LogWarning("[StageTutorial] 캔버스가 없어 안내를 띄우지 못해요"); enabled = false; return; }
            root = new GameObject("StageTutorial", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(host, false);
            root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(0, 1);
            root.sizeDelta = StageSpace.SIZE; root.anchoredPosition = Vector2.zero;
            for (int i = 0; i < 4; i++) { dims[i] = MakeImage("Dim" + i, null, new Color(0, 0, 0, DIM_A)); dims[i].raycastTarget = true; }   // 구멍 밖 클릭을 막는다
            for (int i = 0; i < 4; i++) frame[i] = MakeImage("Frame" + i, null, FRAME);
            arrow = MakeImage("Arrow", SpriteDB.Get("UI/sign_arrow"), Color.white).rectTransform; arrow.sizeDelta = new Vector2(150, 98);
            fingerRing = MakeImage("FingerRing", SpriteDB.Get("FX/ring"), FRAME).rectTransform; fingerRing.sizeDelta = new Vector2(150, 150);
            finger = MakeImage("Finger", SpriteDB.Get("FX/circle"), Color.white).rectTransform; finger.sizeDelta = new Vector2(54, 54);
            var lt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lt.transform.SetParent(root, false);
            label = lt.GetComponent<TextMeshProUGUI>();
            LocFonts.Apply(label, LocFonts.Role.Title, 0);
            label.color = TEXT; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 1); label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(1100, 140); label.enableAutoSizing = true; label.fontSizeMin = 26; label.fontSizeMax = 40;
            root.gameObject.SetActive(false);
        }

        void OnDestroy() { if (root != null) Destroy(root.gameObject); }

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
            if (root == null) return;
            var R = RoundSim.R;
            bool on = R != null && R.tutorial && R.phase == "aim" && R.TutorialColony != null && !R.paused && !App.ModalOpen;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on)
            {
                if (R == null || R.phase != "aim") { Destroy(this); }   // 한 번 발사하면 끝
                return;
            }
            root.SetAsLastSibling();
            Draw(R, Time.unscaledTime);
        }

        void Draw(RoundSim R, float now)
        {
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
            UIUtil.SetText(label, Loc.T("수확기를 잡고 아래로 당겼다 놓으면 버섯을 향해 날아가요!"));
            UIUtil.Place(label.rectTransform, Mathf.Clamp(launch.x - half - 560, 560, 1360), Mathf.Clamp(launch.y - 160, 80, 1000));
        }

        // 구멍 밖 네 칸을 어둡게 (구멍은 클릭이 통과한다)
        void Hole(Rect h)
        {
            float W = StageSpace.SIZE.x, H = StageSpace.SIZE.y;
            float x0 = Mathf.Clamp(h.xMin, 0, W), x1 = Mathf.Clamp(h.xMax, 0, W), y0 = Mathf.Clamp(h.yMin, 0, H), y1 = Mathf.Clamp(h.yMax, 0, H);
            Box(dims[0], 0, 0, W, y0);
            Box(dims[1], 0, y1, W, H - y1);
            Box(dims[2], 0, y0, x0, y1 - y0);
            Box(dims[3], x1, y0, W - x1, y1 - y0);
            var c = FRAME; c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6);   // 깜박임
            foreach (var f in frame) f.color = c;
            Box(frame[0], h.xMin - FRAME_T, h.yMin - FRAME_T, h.width + 2 * FRAME_T, FRAME_T);
            Box(frame[1], h.xMin - FRAME_T, h.yMax, h.width + 2 * FRAME_T, FRAME_T);
            Box(frame[2], h.xMin - FRAME_T, h.yMin, FRAME_T, h.height);
            Box(frame[3], h.xMax, h.yMin, FRAME_T, h.height);
        }

        static void Box(Graphic g, float x, float y, float w, float h)
        {
            var rt = g.rectTransform;
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
            UIUtil.Place(rt, x, y);
        }

        static void Place(RectTransform rt, float x, float y, float rotZ, float alpha)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            UIUtil.Place(rt, x, y);
            rt.localEulerAngles = new Vector3(0, 0, rotZ);
            var g = rt.GetComponent<Graphic>(); var c = g.color; c.a = Mathf.Clamp01(alpha); g.color = c;
        }
    }
}
