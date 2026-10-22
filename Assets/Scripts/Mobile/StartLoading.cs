using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // Start 씬 로딩 화면. 퍼센트 글자(00% → 100%)와 진행 바(progress_back 위로 progress_fill이 차오름)를 같이 움직인다.
    // nextScene을 넣으면 그 씬을 뒤에서 읽고, 다 차면 넘어간다. 비워 두면 연출만 하고 멈춘다.
    public class StartLoading : MonoBehaviour
    {
        [SerializeField] TMP_Text percentText;
        [SerializeField] RectTransform fill;      // 진행 바 안쪽 채움. 앵커 x 최대값으로 길이를 늘린다
        [SerializeField] string nextScene = "";
        [SerializeField] float minDuration = 2f;  // 로딩이 빨라도 이만큼은 보여 준다

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            AsyncOperation op = null;
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            {
                op = SceneManager.LoadSceneAsync(nextScene);
                op.allowSceneActivation = false;
            }
            else if (!string.IsNullOrEmpty(nextScene))
                Debug.LogWarning($"[StartLoading] 빌드 설정에 없는 씬: {nextScene}");

            float t = 0f;
            float shown = 0f;
            while (shown < 1f)
            {
                t += Time.unscaledDeltaTime;
                // 실제 로딩(0.9에서 멈춤)과 최소 시간 중 느린 쪽을 따른다
                float real = op == null ? 1f : Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.Min(real, t / Mathf.Max(0.01f, minDuration));
                Show(shown);
                yield return null;
            }

            if (op != null) op.allowSceneActivation = true;
        }

        void Show(float p)
        {
            if (percentText != null) percentText.text = $"{Mathf.FloorToInt(p * 100f):00}%";
            if (fill != null) fill.anchorMax = new Vector2(p, fill.anchorMax.y);
        }
    }
}
