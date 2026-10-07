using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush
{
    // Round HUD (prototype drawHUD): score, harvest slots, value, time bar, stat line, weather banner,
    // weather/region/speed corner, codex & pause buttons, aim hints, "수확 끝!" and pause dim.
    // References left empty are found by their hierarchy names on Awake.
    public class RoundHUD : MonoBehaviour
    {
        public RectTransform scoreValue; public TMP_Text scoreText;
        public RectTransform[] slotIcons = new RectTransform[3]; public TMP_Text[] slotTexts = new TMP_Text[3];
        public RectTransform valueIcon; public TMP_Text valueText;
        public Image timeFill; public TMP_Text timeText; public TMP_Text festText;
        public RectTransform statBar; public TMP_Text statText; public RectTransform skillRow; public Image skillIconPrefab;
        public CanvasGroup weatherBanner; public Image weatherBannerFrame, weatherBannerIcon; public TMP_Text weatherBannerTitle, weatherBannerDesc;
        public Image cornerWeatherIcon; public TMP_Text cornerWeather, cornerRegion, cornerSpeed, cornerHint;
        public RectTransform codexButton; public TMP_Text codexText;
        public GameObject aimHint; public TMP_Text aimHint2;
        public Image endDim; public RectTransform endTitle;
        public GameObject pausedDim;

        readonly List<Image> skillIcons = new List<Image>();


        public void Draw(RoundSim R, float now, int speed, bool touch)
        {
            var st = R.st;
            float bump = 1 + R.scoreBump * 0.06f;
            scoreValue.localScale = new Vector3(bump, bump, 1);
            UIUtil.SetText(scoreText, U.Fmt(R.score));
            for (int i = 0; i < 3; i++)
            {
                float b = 1 + R.slotBump[CAT_KEYS[i]] * 0.12f;
                slotIcons[i].localScale = new Vector3(b, b, 1);
                UIUtil.SetText(slotTexts[i], "× " + U.Fmt(R.gains[CAT_KEYS[i]]));
            }
            float gb = 1 + R.saleBump * 0.1f;
            valueIcon.localScale = new Vector3(gb, gb, 1);
            UIUtil.SetText(valueText, U.Fmt(R.value));

            float frac = Mathf.Clamp01(R.timeLeft / R.timeMax);
            timeFill.fillAmount = frac;
            timeFill.color = R.timeLeft <= 5 ? (Mathf.Sin(now * 12) > 0 ? U.Hex("#ff5a4a") : U.Hex("#ff9a4a")) : R.timeLeft <= 10 ? U.Hex("#ffc44a") : U.Hex("#7fd65a");
            UIUtil.SetText(timeText, Game.DBG.infinite ? "∞" : $"{R.timeLeft:F1}초");
            UIUtil.Show(festText, R.festOn);
            if (R.festOn) UIUtil.SetText(festText, $"{UIUtil.Ic("s_fest")} 풍년! 점수·수확량 ×{U.FmtN(st.festMul)}");

            // 능력치 줄 + 스킬 아이콘
            UIUtil.SetText(statText, $"{UIUtil.Ic("core_ps")} {U.FmtN(st.atk)}    {UIUtil.Ic("s_accel")} ×{U.FmtN(st.spdMul)}    ● {R.balls.Count}");
            var icons = new List<(string id, string icon, string col)>();
            foreach (var id in SKILL_IDS) if (st.sk[id].on) icons.Add((id, SKILLS[id].icon, SKILLS[id].col));
            if (st.blade) icons.Add(("bl", "s_blade", "#e6e6f0"));
            if (st.kidP > 0) icons.Add(("md_kid", "s_kid", "#9fd8ff"));
            for (int i = 0; i < icons.Count; i++)
            {
                while (skillIcons.Count <= i) { var im = Instantiate(skillIconPrefab, skillRow); skillIcons.Add(im); }
                var img = skillIcons[i];
                img.gameObject.SetActive(true);
                bool fl = R.skillFlash.TryGetValue(icons[i].id, out var f) && f > 0;
                img.color = fl ? U.Hex(icons[i].col) : new Color(1, 1, 1, 0.15f);
                var ic = img.transform.GetChild(0).GetComponent<Image>();
                ic.sprite = SpriteDB.Icon(icons[i].icon);
                img.rectTransform.sizeDelta = Vector2.one * (fl ? 34 : 28);
            }
            for (int i = icons.Count; i < skillIcons.Count; i++) skillIcons[i].gameObject.SetActive(false);
            skillIconPrefab.gameObject.SetActive(false);
            statBar.anchoredPosition = new Vector2(statBar.anchoredPosition.x, R.festOn ? -100 : -66);

            // 날씨 배너
            var w = st.weather;
            string wcol = w.good ? WEATHER_GOOD : WEATHER_BAD;
            bool banner = R.phase == "aim" || R.runT < 3.5f;
            UIUtil.Show(weatherBanner, banner);
            if (banner)
            {
                weatherBanner.alpha = R.phase == "aim" ? 1 : Mathf.Clamp01((3.5f - R.runT) / 0.5f);
                weatherBannerFrame.GetComponent<Outline>().effectColor = U.Hex(wcol);
                weatherBannerIcon.sprite = SpriteDB.Icon(w.icon);
                UIUtil.SetText(weatherBannerTitle, $"오늘의 날씨: {w.n}");
                weatherBannerTitle.color = U.Hex(wcol);
                UIUtil.SetText(weatherBannerDesc, $"{(w.good ? "좋은 날씨" : "나쁜 날씨")} · {w.d}");
                weatherBannerDesc.color = U.Hex(w.good ? "#cfe8ff" : "#ffd0c8");
            }
            cornerWeatherIcon.sprite = SpriteDB.Icon(w.icon);
            UIUtil.SetText(cornerWeather, w.n);
            cornerWeather.color = U.Hex(wcol);
            UIUtil.SetText(cornerRegion, $"{UIUtil.Ic(st.theme.icon)} {st.theme.n} · 스테이지 {st.stage}");
            int spd = speed * Game.DBG.speed;
            UIUtil.SetText(cornerSpeed, spd > 1 ? $"▶▶ ×{spd}" : "▶ ×1");
            cornerSpeed.color = spd > 1 ? U.Hex("#ffd23a") : U.Hex("#bbbbbb");
            UIUtil.SetText(cornerHint, touch ? "눌러서 배속" : "누르기·F·우클릭 배속");

            float sh = R.codexShake > 0 ? Mathf.Sin(now * 40) * 6 * R.codexShake : 0;
            codexButton.anchoredPosition = new Vector2(1716 + sh, codexButton.anchoredPosition.y);
            UIUtil.SetText(codexText, $"{UIUtil.Ic("book")} 도감 {Game.CodexCount()}/{SP_TOTAL}");

            UIUtil.Show(aimHint, R.phase == "aim");
            if (R.phase == "aim") UIUtil.SetText(aimHint2, touch ? "발사 후에는 화면을 손가락으로 좌우로 밀어 바로 받아치세요 (안 움직여도 괜찮아요)" : "발사 후에는 마우스를 좌우로 움직여 바로 받아치세요 (안 움직여도 괜찮아요)");

            UIUtil.Show(endDim, R.phase == "end");
            if (R.phase == "end")
            {
                float k = 1.5f - R.endT;
                endDim.color = new Color(0, 0, 0, Mathf.Min(0.35f, k * 0.5f));
                float s = k < 0.2f ? 0.5f + k * 2.5f : 1;
                endTitle.localScale = new Vector3(s, s, 1);
            }
            UIUtil.Show(pausedDim, R.paused);
        }
    }
}
