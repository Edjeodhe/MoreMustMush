using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 씬 상단 HUD. 제한 시간을 스스로 세고(일시정지 중엔 멈춤), 번 골드·콤보는 게임 쪽에서 SetGold/SetCombo로 넣는다.
    // 일시정지는 팝업 없이 Time.timeScale만 0으로 내린다. 옵션 팝업이 열려 있는 동안에도 멈춘다
    // (둘은 따로 센다 — 일시정지한 채로 옵션을 열었다 닫아도 계속 멈춰 있다).
    public class StageHud : MonoBehaviour
    {
        [SerializeField] float duration = 30f;        // 스테이지 제한 시간(초)

        [SerializeField] TMP_Text earnedGoldText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] RectTransform timerFill;     // 진행 바 안쪽 채움. 앵커 x 최대값 = 남은 비율
        [SerializeField] GameObject comboRoot;        // 콤보가 2 미만이면 숨긴다
        [SerializeField] TMP_Text comboText;

        [SerializeField] Button optionButton;
        [SerializeField] UiPopup optionPopup;
        [SerializeField] Button pauseButton;

        public float Remaining { get; private set; }
        public bool Paused { get; private set; }
        public event System.Action TimeUp;

        long gold;
        int combo;
        int shownSecond = -1;

        void Awake()
        {
            optionButton.onClick.AddListener(optionPopup.Toggle);
            optionPopup.Opened += ApplyTimeScale;
            optionPopup.Closed += ApplyTimeScale;
            pauseButton.onClick.AddListener(TogglePause);
            Remaining = duration;
            SetGold(0);
            SetCombo(0);
            ShowTime();
        }

        void Update()
        {
            if (Remaining <= 0f) return;
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            ShowTime();
            if (Remaining <= 0f) TimeUp?.Invoke();
        }

        void OnDestroy()
        {
            // 씬을 떠날 때 멈춘 시간을 남기지 않는다
            Time.timeScale = 1f;
        }

        public void SetGold(long value)
        {
            gold = value;
            earnedGoldText.text = gold.ToString("N0");
        }

        public void AddGold(long delta) => SetGold(gold + delta);

        public void SetCombo(int value)
        {
            combo = value;
            comboRoot.SetActive(combo >= 2);
            comboText.text = combo.ToString();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            ApplyTimeScale();
        }

        void ApplyTimeScale() => Time.timeScale = Paused || optionPopup.IsOpen ? 0f : 1f;

        void ShowTime()
        {
            timerFill.anchorMax = new Vector2(duration > 0f ? Remaining / duration : 0f, timerFill.anchorMax.y);
            int sec = Mathf.CeilToInt(Remaining);
            if (sec == shownSecond) return;
            shownSecond = sec;
            timerText.text = sec + "초";
        }
    }
}
