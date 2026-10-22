using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // SkillTree 씬 하단 메뉴. 세계수·스테이지 진입은 씬 이동, 의뢰·공방·도감은 팝업.
    // 팝업은 한 번에 하나만 연다 (다른 메뉴를 누르면 열려 있던 것은 닫힌다).
    public class SkillTreeScreen : MonoBehaviour
    {
        [SerializeField] Button worldTreeButton;
        [SerializeField] Button questButton;
        [SerializeField] Button indexButton;
        [SerializeField] Button workshopButton;
        [SerializeField] Button stageButton;

        [SerializeField] UiPopup questPopup;
        [SerializeField] UiPopup indexPopup;
        [SerializeField] UiPopup workshopPopup;

        [SerializeField] string worldTreeScene = "WorldTree";
        [SerializeField] string stageScene = "InGame";

        void Awake()
        {
            worldTreeButton.onClick.AddListener(() => Go(worldTreeScene));
            stageButton.onClick.AddListener(() => Go(stageScene));
            questButton.onClick.AddListener(() => Show(questPopup));
            indexButton.onClick.AddListener(() => Show(indexPopup));
            workshopButton.onClick.AddListener(() => Show(workshopPopup));
        }

        void Update()
        {
            // 안드로이드 뒤로 가기 = 열린 팝업 닫기
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseAll();
        }

        void Show(UiPopup popup)
        {
            if (popup.IsOpen) { popup.Close(); return; }
            CloseAll();
            popup.Open();
        }

        void CloseAll()
        {
            questPopup.Close();
            indexPopup.Close();
            workshopPopup.Close();
        }

        static void Go(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning($"[SkillTreeScreen] 빌드 설정에 없는 씬: {scene}");
        }
    }
}
