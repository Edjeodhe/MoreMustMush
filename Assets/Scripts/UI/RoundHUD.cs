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
        readonly List<Image> skillInner = new List<Image>();   // skillIcons[i]의 안쪽 그림 (GetChild·GetComponent를 매 프레임 하지 않게)

        // 라운드 동안 바뀌지 않는 것(스킬 아이콘 목록, 날씨·지역·풍년 글자)은 라운드(R.st)가 바뀔 때 한 번만 만든다.
        // 숫자 글자는 값이 바뀔 때만 다시 만든다 (예전엔 매 프레임 문자열 30여 개를 만들었다)
        RoundStats shownSt;
        readonly List<(string id, string icon, string col)> icons = new List<(string, string, string)>();
        string festStr;
        Outline bannerOutline;
        double shownScore = double.NaN, shownValue = double.NaN;
        readonly double[] shownGain = { double.NaN, double.NaN, double.NaN };
        int shownTenths = int.MinValue, shownBalls = -1, shownSpd = -1, shownCodex = -1;

        Vector2 statOrigin, codexOrigin;
        float festOffset;
        bool layoutReady;

        void CacheLayout()
        {
            if (layoutReady) return;
            layoutReady = true;
            statOrigin = statBar.anchoredPosition; codexOrigin = codexButton.anchoredPosition;
            festOffset = festText.rectTransform.rect.height;
        }

        // The scene owns HUD positions. The simulation only receives the visual destinations.
        public void BindFlyerTargets(RoundSim round)
        {
            CacheLayout();
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var rect = (RectTransform)canvas.transform;
            for (int i = 0; i < CAT_KEYS.Length; i++)
            {
                var p = rect.InverseTransformPoint(slotIcons[i].position);
                round.harvestTargets[CAT_KEYS[i]] = new Vector2(p.x - rect.rect.xMin, rect.rect.yMax - p.y);
            }
        }

        void RoundChanged(RoundSim R)
        {
            CacheLayout();
            var st = R.st;
            shownSt = st;
            icons.Clear();
            foreach (var id in SKILL_IDS) if (st.sk[id].on) icons.Add((id, SKILLS[id].icon, SKILLS[id].col));
            if (st.blade) icons.Add(("bl", "s_blade", "#e6e6f0"));
            if (st.kidP > 0) icons.Add(("md_kid", "s_kid", "#9fd8ff"));
            for (int i = 0; i < icons.Count; i++)
            {
                while (skillIcons.Count <= i) { var im = Instantiate(skillIconPrefab, skillRow); skillIcons.Add(im); skillInner.Add(im.transform.GetChild(0).GetComponent<Image>()); }
                skillInner[i].sprite = SpriteDB.Icon(icons[i].icon);
            }
            festStr = $"{UIUtil.Ic("s_fest")} 풍년! 점수·수확량 ×{U.FmtN(st.festMul)}";

            var w = st.weather;
            string wcol = w.good ? WEATHER_GOOD : WEATHER_BAD;
            if (bannerOutline == null) bannerOutline = weatherBannerFrame.GetComponent<Outline>();
            if (bannerOutline != null) bannerOutline.effectColor = U.Hex(wcol);
            weatherBannerIcon.sprite = SpriteDB.Icon(w.icon);
            UIUtil.SetText(weatherBannerTitle, $"오늘의 날씨: {w.n}");
            weatherBannerTitle.color = U.Hex(wcol);
            UIUtil.SetText(weatherBannerDesc, $"{(w.good ? "좋은 날씨" : "나쁜 날씨")} · {w.d}");
            weatherBannerDesc.color = U.Hex(w.good ? "#cfe8ff" : "#ffd0c8");
            cornerWeatherIcon.sprite = SpriteDB.Icon(w.icon);
            UIUtil.SetText(cornerWeather, w.n);
            cornerWeather.color = U.Hex(wcol);
            UIUtil.SetText(cornerRegion, $"{UIUtil.Ic(st.theme.icon)} {st.theme.n} · 스테이지 {st.stage}");
            shownBalls = -1;   // 능력치 줄은 새 라운드 값으로 다시
        }


        public void Draw(RoundSim R, float now, int speed, bool touch)
        {
            var st = R.st;
            if (st != shownSt) RoundChanged(R);
            float bump = 1 + R.scoreBump * 0.06f;
            scoreValue.localScale = new Vector3(bump, bump, 1);
            if (R.score != shownScore) { shownScore = R.score; UIUtil.SetText(scoreText, U.Fmt(R.score)); }
            for (int i = 0; i < 3; i++)
            {
                float b = 1 + R.slotBump[CAT_KEYS[i]] * 0.12f;
                slotIcons[i].localScale = new Vector3(b, b, 1);
                double g = R.gains[CAT_KEYS[i]];
                if (g != shownGain[i]) { shownGain[i] = g; UIUtil.SetText(slotTexts[i], "× " + U.Fmt(g)); }
            }
            float gb = 1 + R.saleBump * 0.1f;
            valueIcon.localScale = new Vector3(gb, gb, 1);
            if (R.value != shownValue) { shownValue = R.value; UIUtil.SetText(valueText, U.Fmt(R.value)); }

            float frac = Mathf.Clamp01(R.timeLeft / R.timeMax);
            timeFill.fillAmount = frac;
            timeFill.color = R.timeLeft <= 5 ? (Mathf.Sin(now * 12) > 0 ? U.Hex("#ff5a4a") : U.Hex("#ff9a4a")) : R.timeLeft <= 10 ? U.Hex("#ffc44a") : U.Hex("#7fd65a");
            int tenths = Game.DBG.infinite ? int.MaxValue : Mathf.RoundToInt(R.timeLeft * 10);
            if (tenths != shownTenths) { shownTenths = tenths; UIUtil.SetText(timeText, Game.DBG.infinite ? "∞" : $"{R.timeLeft:F1}초"); }
            UIUtil.Show(festText, R.festOn);
            if (R.festOn) UIUtil.SetText(festText, festStr);

            // 능력치 줄 + 스킬 아이콘
            if (R.balls.Count != shownBalls) { shownBalls = R.balls.Count; UIUtil.SetText(statText, $"{UIUtil.Ic("core_ps")} {U.FmtN(st.atk)}    {UIUtil.Ic("s_accel")} ×{U.FmtN(st.spdMul)}    ● {R.balls.Count}"); }
            for (int i = 0; i < icons.Count; i++)
            {
                var img = skillIcons[i];
                UIUtil.Show(img, true);
                bool fl = R.skillFlash.TryGetValue(icons[i].id, out var f) && f > 0;
                img.color = fl ? U.Hex(icons[i].col) : new Color(1, 1, 1, 0.15f);
                img.rectTransform.sizeDelta = Vector2.one * (fl ? 34 : 28);
            }
            for (int i = icons.Count; i < skillIcons.Count; i++) UIUtil.Show(skillIcons[i], false);
            UIUtil.Show(skillIconPrefab, false);
            statBar.anchoredPosition = statOrigin + new Vector2(0, R.festOn ? -festOffset : 0);

            // 날씨 배너
            bool banner = R.phase == "aim" || R.runT < 3.5f;   // 배너 글자·색은 RoundChanged에서 한 번 채운다
            UIUtil.Show(weatherBanner, banner);
            if (banner) weatherBanner.alpha = R.phase == "aim" ? 1 : Mathf.Clamp01((3.5f - R.runT) / 0.5f);
            int spd = speed * Game.DBG.speed;
            if (spd != shownSpd)
            {
                shownSpd = spd;
                UIUtil.SetText(cornerSpeed, spd > 1 ? $"▶▶ ×{spd}" : "▶ ×1");
                cornerSpeed.color = spd > 1 ? U.Hex("#ffd23a") : U.Hex("#bbbbbb");
            }
            UIUtil.SetText(cornerHint, touch ? "눌러서 배속" : "누르기·F·우클릭 배속");

            float sh = R.codexShake > 0 ? Mathf.Sin(now * 40) * 6 * R.codexShake : 0;
            codexButton.anchoredPosition = codexOrigin + new Vector2(sh, 0);
            int codex = Game.CodexCount();
            if (codex != shownCodex) { shownCodex = codex; UIUtil.SetText(codexText, $"{UIUtil.Ic("book")} 도감 {codex}/{SP_TOTAL}"); }

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
