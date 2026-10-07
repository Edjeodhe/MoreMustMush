using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Title box (prototype renderTitle): mushroom icons, logo, subtitle, start/continue/reset, controls help.
    public class TitleScreen : MonoBehaviour
    {
        public RectTransform[] icons = new RectTransform[6];
        public GameObject continueButton, newGameButton, resetButton;
        public TMP_Text howto;

        static readonly string[] IconIds = { "ed0", "md0", "ps0", "ed18", "md18", "ps18" };


        public void Show(bool hasSave)
        {
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null) icons[i].GetComponent<Image>().sprite = SpriteDB.Single(IconIds[i]);
            UIUtil.Show(continueButton, hasSave);
            UIUtil.Show(newGameButton, !hasSave);
            UIUtil.Show(resetButton, hasSave);
            howto.text = RoundController.IsTouch
                ? "<b>조작</b> 핀볼을 손가락으로 잡고 뒤로 끌었다 놓아 발사 · 화면을 좌우로 밀어 슬라이드 바 이동 · 오른쪽 위 배속 칸 누르기(×1·×2·×4) · 일시정지 · 균사 트리는 두 손가락으로 확대·축소"
                : "<b>조작</b> 핀볼을 잡고 뒤로 끌었다 놓아 발사 · 마우스 좌우로 슬라이드 바 이동 · F/우클릭 배속(×1·×2·×4) · Esc 일시정지 · ` 디버그";
        }

        void Update()
        {
            // 아이콘이 통통 (CSS bob 1.4s, 짝수 칸은 0.3s 늦게)
            float t = Time.time;
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                float ph = (t - (i % 2 == 1 ? 0.3f : 0)) / 1.4f * Mathf.PI * 2;
                icons[i].localPosition = new Vector3(icons[i].localPosition.x, -4 * (0.5f - 0.5f * Mathf.Cos(ph)), 0);
            }
        }
    }
}
