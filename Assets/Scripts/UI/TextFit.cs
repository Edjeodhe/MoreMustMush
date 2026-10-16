using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush
{
    // 배치한 글자 공통 처리. 씬이 열릴 때 App이 그 씬의 모든 TMP에 한 번 적용한다 (한 번 훑으며 둘 다):
    //   칸 맞춤 — 넘치면 글자를 줄이고(배치한 크기가 최대, 최소는 절반·단 8 아래로는 안 줄임), 그래도 안 들어가면 끝을 "…"로 자른다
    //   언어 폰트 — TextLangFont에 등록 (일본어처럼 배치한 폰트에 없는 글자는 그 언어 폰트로)
    // 칸 안에 들어가는 글자는 배치한 크기 그대로라 겉보기는 바뀌지 않는다. 줄바꿈 설정은 배치한 그대로 둔다.
    // 씬이 열린 뒤 코드로 만든 글자는 Setup(글자)을 불러 같은 처리를 한다.
    // 단, 코드가 fontSize를 직접 정하는 글자(라운드 필드 숫자 등)에는 쓰지 않는다 — 자동 크기가 켜지면 fontSize 대입이 무시된다.
    public static class TextFit
    {
        const float MIN_RATIO = 0.5f;   // 배치한 크기의 몇 배까지 줄일지
        const float MIN_SIZE = 8f;      // 이보다 작게는 줄이지 않는다 (배치한 크기가 이보다 작으면 줄이지 않고 "…"만)

        public static void ApplyScene(Scene scene)
        {
            TextLangFont.Prune();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    Fit(t);
                    TextLangFont.Register(t, false);
                }
            TextLangFont.ApplyAll();
        }

        // 씬이 열린 뒤 코드로 만든 글자 하나
        public static void Setup(TMP_Text t)
        {
            Fit(t);
            TextLangFont.Register(t);
        }

        static void Fit(TMP_Text t)
        {
            if (t == null) return;
            // 이미 자동 크기인 글자(LocText 등이 맞춰 둔 것)는 크기 범위를 그대로 둔다
            if (!t.enableAutoSizing)
            {
                float size = t.fontSize;
                t.fontSizeMax = size;
                t.fontSizeMin = Mathf.Min(size, Mathf.Max(MIN_SIZE, size * MIN_RATIO));
                t.enableAutoSizing = true;
            }
            // 칸 밖으로 그대로 흘러넘치는 설정만 바꾼다 (가리기·스크롤 등 일부러 고른 설정은 둔다)
            if (t.overflowMode == TextOverflowModes.Overflow) t.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
