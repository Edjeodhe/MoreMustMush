using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MoreMush
{
    // 알림 띠 (MainGame·OutGame·Stage 캔버스에 배치한 `Toast`). App.Notify 문구를 위 가운데에 잠깐 띄운다.
    // 배치: Toast(CanvasGroup + 배경 Image) > 글자(TMP). 누름은 받지 않는다.
    // 문구가 겹치면 차례로 보여 준다 (구독할 때 App이 쌓아 둔 문구도 이리로 온다).
    [RequireComponent(typeof(CanvasGroup))]
    public class NoticeToast : MonoBehaviour
    {
        const float SHOW_SEC = 1.6f, FADE_SEC = 0.25f, RISE_PX = 20;
        const int QUEUE_MAX = 3;   // 밀린 문구는 최근 것만

        CanvasGroup group;
        TMP_Text text;
        RectTransform rect;
        Vector2 origin;
        float hideAt = -1;
        readonly Queue<string> waiting = new Queue<string>();

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            text = SceneUI.TextIn(transform);
            if (text == null) Debug.LogWarning("[Toast] 알림 띠 안에 글자(TMP)가 없어요");
            rect = (RectTransform)transform;
            origin = rect.anchoredPosition;
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
        }

        void OnEnable() => App.Notified += Push;
        void OnDisable() => App.Notified -= Push;

        void Push(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (!Showing) { Show(msg); return; }
            if (waiting.Count >= QUEUE_MAX) waiting.Dequeue();
            waiting.Enqueue(msg);
        }

        bool Showing => Time.unscaledTime < hideAt;

        void Show(string msg)
        {
            if (text != null) UIUtil.SetText(text, msg);
            hideAt = Time.unscaledTime + SHOW_SEC;
            transform.SetAsLastSibling();   // 그 사이 열린 창보다 위로
        }

        void Update()
        {
            if (!Showing && waiting.Count > 0 && group.alpha == 0) Show(waiting.Dequeue());
            bool on = Showing;
            if (!on && group.alpha == 0) return;
            group.alpha = Mathf.MoveTowards(group.alpha, on ? 1 : 0, Time.unscaledDeltaTime / FADE_SEC);
            rect.anchoredPosition = origin + new Vector2(0, (1 - group.alpha) * RISE_PX);
        }
    }
}
