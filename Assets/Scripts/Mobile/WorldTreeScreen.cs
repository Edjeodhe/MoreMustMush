using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // WorldTree 씬 화면. 재화·바구니(세계수 보상 누적치) 표시, 옵션 팝업, 스킬 트리로 돌아가기.
    // 값은 게임 쪽에서 SetGold/SetMycelite/SetBasket으로 넣는다.
    public class WorldTreeScreen : MonoBehaviour
    {
        [SerializeField] TMP_Text goldText;
        [SerializeField] TMP_Text myceliteText;
        [SerializeField] TMP_Text basketText;

        [SerializeField] Button optionButton;
        [SerializeField] UiPopup optionPopup;
        [SerializeField] string backScene = "SkillTree";

        void Awake()
        {
            optionButton.onClick.AddListener(optionPopup.Toggle);
        }

        // Back 버튼 OnClick(인스펙터)에 연결돼 있다
        public void GoBack()
        {
            if (Application.CanStreamedLevelBeLoaded(backScene)) SceneManager.LoadScene(backScene);
            else Debug.LogWarning($"[WorldTreeScreen] 빌드 설정에 없는 씬: {backScene}");
        }

        public void SetGold(long value) => goldText.text = value.ToString("N0");
        public void SetMycelite(long value) => myceliteText.text = value.ToString("N0");
        public void SetBasket(double value) => basketText.text = Short(value);

        // 1234 → 1.2k, 1234567 → 1.2M (1000 미만은 그대로)
        static string Short(double v)
        {
            if (v < 1e3) return Mathf.FloorToInt((float)v).ToString();
            if (v < 1e6) return (v / 1e3).ToString("0.#") + "k";
            if (v < 1e9) return (v / 1e6).ToString("0.#") + "M";
            return (v / 1e9).ToString("0.#") + "B";
        }
    }
}
