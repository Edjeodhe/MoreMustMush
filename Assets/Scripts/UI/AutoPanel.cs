using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Idle harvest reward popup (자동 수확 보상, Cookie Run Kingdom style): time stored (max 8 h), reward so far,
    // hourly rates, claim button. Refreshes every half second while open.
    public class AutoPanel : MonoBehaviour
    {
        public TMP_Text stage, timeText, rates;
        public Image timeFill;
        public TMP_Text goldText, gemText, diaText;
        public Button claim; public TMP_Text claimText;

        float t;

        void OnEnable() { t = 0; }

        void Update()
        {
            if (G == null || (t -= Time.unscaledDeltaTime) > 0) return;
            t = 0.5f;
            Render();
        }

        public void Render()
        {
            AutoEnsure();
            double h = AutoHours();
            var th = THEME.TryGetValue(G.theme, out var tt) ? tt : LatestTheme();
            stage.text = $"현재 스테이지 <b>{StageNow()}</b> · {th.n}";
            timeText.text = $"{(int)h}시간 {(int)(h * 60 % 60)}분 쌓임 <size=75%><color=#8a6a4a>/ 최대 {AUTO.maxHours}시간</color></size>";
            timeFill.fillAmount = (float)(h / AUTO.maxHours);
            var r = AutoReward(); var rt = AutoRates();
            goldText.text = U.Fmt(r.gold); gemText.text = U.Fmt(r.gem); diaText.text = U.Fmt(r.dia);
            rates.text = $"시간당 {UIUtil.Ic("gold")}{U.Fmt(rt.gold)}  {UIUtil.Ic("gem")}{U.FmtN(rt.gem, 1)}  {UIUtil.Ic("dia")}{U.FmtN(rt.dia, 1)} <size=80%><color=#8a6a4a>(스테이지·버섯 오두막·꼬마 효과·'자동 수확 확장'으로 늘어나요)</color></size>";
            claim.interactable = AutoReady();
            claimText.text = AutoReady() ? "보상 받기" : "조금만 기다려 주세요";
        }
    }
}
