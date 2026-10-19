using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MoreMush
{
    // 새 기능 해금 안내 (MainGame 캔버스에 배치한 `UnlockGuide`). 튜토리얼처럼 화면을 어둡게 덮고
    // 새로 열린 기능의 버튼만 밝혀서 화살표와 문구로 알린다. 그 버튼이나 어두운 곳을 누르면 닫히고, 여러 개면 차례로 보여 준다.
    // 배치: UnlockGuide(늘 켜 둠, 빈 판) > Window(꺼 둠) > Dim(검은 판 + Button) · Callout(Canvas 덮어쓰기) > Arrow · Label(Text)
    // 밝히는 법: 대상 버튼에 정렬 순서를 올린 캔버스를 잠깐 붙여 어두운 판 위로 올린다 (닫으면 원래대로).
    public class UnlockGuide : MonoBehaviour
    {
        const float ARROW_GAP = 70, LABEL_GAP = 150, BOB_PX = 14, BOB_HZ = 2.2f, PULSE = 0.06f, EDGE = 360;
        const int ABOVE_DIM = 1, CALLOUT_ABOVE = 2;   // 대상 버튼 = 루트 캔버스 +1, 안내 말풍선 = +2

        struct Entry { public string feat; public Transform target; }

        GameObject window;
        RectTransform rootRect, callout, arrow, labelBox;
        TMP_Text label;
        readonly Queue<Entry> queue = new Queue<Entry>();
        bool open;
        Entry cur;
        Vector3 curScale;
        Vector2 arrowBase, arrowDir;
        // 대상 버튼에 우리가 붙인 것 (닫을 때 되돌린다)
        Canvas curCanvas; GraphicRaycaster curRaycaster;
        bool canvasAdded, prevOverride; int prevOrder;
        Button curButton; UnityAction onTarget;

        void Awake()
        {
            rootRect = (RectTransform)transform;
            window = SceneUI.Need(transform, "Window")?.gameObject;
            callout = SceneUI.Need(transform, "Callout") as RectTransform;
            arrow = SceneUI.Need(transform, "Arrow") as RectTransform;
            var lb = SceneUI.Need(transform, "Label");
            labelBox = lb as RectTransform;
            label = SceneUI.TextIn(lb);
            SceneUI.Bind(SceneUI.Need(transform, "Dim"), Dismiss);
            if (window != null) window.SetActive(false);
        }

        void OnDisable() { if (open) { queue.Clear(); Finish(); } }

        public bool IsOpen => open;

        // 새로 열린 기능과 그 버튼 (버튼이 안 보이면 건너뛴다)
        public void Show(string feat, Transform target)
        {
            if (window == null || target == null || !target.gameObject.activeInHierarchy) return;
            queue.Enqueue(new Entry { feat = feat, target = target });
            if (!open) Begin();
        }

        void Begin()
        {
            if (queue.Count == 0) return;
            cur = queue.Dequeue();
            open = true;
            Lift(cur.target);
            curScale = cur.target.localScale;
            curButton = cur.target.GetComponent<Button>();
            if (curButton != null) { onTarget = Dismiss; curButton.onClick.AddListener(onTarget); }
            UIUtil.SetText(label, Loc.F("새 기능이 열렸어요: {0}", Unlock.Name(cur.feat)) + "\n<size=75%>" + Loc.T("눌러서 확인해 보세요") + "</size>");
            window.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Place();
            App.ModalPush();   // 안내가 떠 있는 동안 트리·세계수 입력은 멈춘다
            Snd.Record();
        }

        // 대상 버튼 위(화면 아래쪽이면) 또는 아래(위쪽이면)에 화살표와 문구를 놓는다
        void Place()
        {
            var corners = new Vector3[4];
            ((RectTransform)cur.target).GetWorldCorners(corners);
            Vector2 lo = rootRect.InverseTransformPoint(corners[0]), hi = rootRect.InverseTransformPoint(corners[2]);
            Vector2 center = (lo + hi) / 2, half = (hi - lo) / 2;
            float dir = center.y < rootRect.rect.center.y ? 1 : -1;   // 1 = 위로 안내, -1 = 아래로 안내
            arrowDir = new Vector2(0, -dir);                           // 화살표는 버튼을 가리킨다
            arrowBase = center + new Vector2(0, dir * (half.y + ARROW_GAP));
            arrow.anchoredPosition = arrowBase;
            arrow.localRotation = Quaternion.Euler(0, 0, dir > 0 ? -90 : 90);   // 기본 그림은 오른쪽을 가리킨다
            float x = Mathf.Clamp(center.x, rootRect.rect.xMin + EDGE, rootRect.rect.xMax - EDGE);
            labelBox.anchoredPosition = new Vector2(x, center.y + dir * (half.y + LABEL_GAP + labelBox.rect.height / 2));
        }

        void Update()
        {
            if (!open) return;
            float t = Time.unscaledTime;
            arrow.anchoredPosition = arrowBase + arrowDir * (Mathf.Abs(Mathf.Sin(t * BOB_HZ)) * BOB_PX);
            if (cur.target != null) cur.target.localScale = curScale * (1 + PULSE * Mathf.Sin(t * BOB_HZ * 1.6f));
        }

        void Dismiss()
        {
            if (!open) return;
            Finish();
            Begin();   // 다음 안내가 있으면 이어서
        }

        void Finish()
        {
            open = false;
            if (cur.target != null) { cur.target.localScale = curScale; Unlift(); }
            if (curButton != null && onTarget != null) curButton.onClick.RemoveListener(onTarget);
            curButton = null; onTarget = null;
            if (window != null) window.SetActive(false);
            App.ModalPop();
        }

        void Lift(Transform t)
        {
            var parent = t.parent != null ? t.parent.GetComponentInParent<Canvas>() : null;
            curCanvas = t.GetComponent<Canvas>();
            canvasAdded = curCanvas == null;
            if (canvasAdded) curCanvas = t.gameObject.AddComponent<Canvas>();
            prevOverride = curCanvas.overrideSorting; prevOrder = curCanvas.sortingOrder;
            curCanvas.overrideSorting = true;
            curCanvas.sortingOrder = (parent != null ? parent.rootCanvas.sortingOrder : 0) + ABOVE_DIM;
            if (canvasAdded && parent != null) curCanvas.additionalShaderChannels = parent.rootCanvas.additionalShaderChannels;
            curRaycaster = t.GetComponent<GraphicRaycaster>();
            if (curRaycaster == null) curRaycaster = t.gameObject.AddComponent<GraphicRaycaster>();
            // 말풍선은 대상 버튼보다 위
            var cc = callout != null ? callout.GetComponent<Canvas>() : null;
            if (cc != null) { cc.overrideSorting = true; cc.sortingOrder = (parent != null ? parent.rootCanvas.sortingOrder : 0) + CALLOUT_ABOVE; }
        }

        void Unlift()
        {
            // 붙인 캔버스·레이캐스터는 떼지 않고 정렬만 되돌린다 (GraphicRaycaster가 Canvas에 기대므로 떼는 순서 문제를 피한다)
            if (curCanvas != null) { curCanvas.overrideSorting = prevOverride; curCanvas.sortingOrder = prevOrder; }
            curCanvas = null; curRaycaster = null;
        }
    }
}
