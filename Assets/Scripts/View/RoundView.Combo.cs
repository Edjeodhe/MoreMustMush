using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush
{
    // 가운데 SUPER COMBO! 배너 (RoundSim.feverMsg). 콤보 수는 오른쪽 위 HUD(StageHUD)에만 뜨고, 맞힐 때마다 뜨는 콤보는 없다.
    // 사람이 만든 프리팹(ComboBanner)을 글자 층에 복제해 쓰고, 여기서는 크기·자리·투명도만 움직인다.
    public partial class RoundView
    {
        [AssetPath("Assets/Prefabs/Round/ComboBanner.prefab")] public ComboPopSlot comboBannerPrefab;

        ComboPopSlot banner;
        float comboClock;   // 흔들림용 시계 (일시정지면 멈춘다)

        static readonly Color GlowGold = new Color(1f, 0.86f, 0.5f), GlowCrit = new Color(1f, 0.55f, 0.62f);

        static float EaseOutBack(float k, float c = 2.2f) { k = Mathf.Clamp01(k) - 1; return 1 + (c + 1) * k * k * k + c * k * k; }
        // 크게 내리꽂혀 1 아래로 한 번 눌렸다가 자리 잡는 펀치 (t=0에서 1+a)
        static float Punch(float t, float a, float decay = 11, float freq = 26) => 1 + a * Mathf.Exp(-t * decay) * Mathf.Cos(t * freq);

        void DrawCombos(RoundSim R, float z, bool live)
        {
            if (live) comboClock += Time.deltaTime;
            DrawBanner(R, z);
        }

        // 반짝이: 하나씩 엇갈려 피었다 진다 (돌면서). 자리는 프리팹에 놓인 대로
        static void Sparks(ComboPopSlot sl, bool on, float t, float fade)
        {
            if (sl.sparks == null) return;
            for (int i = 0; i < sl.sparks.Length; i++)
            {
                var sp = sl.sparks[i];
                if (sp == null) continue;
                float k = (t - 0.04f - i * 0.09f) / 0.45f;
                sp.enabled = on && k > 0 && k < 1;
                if (!sp.enabled) continue;
                float ss = Mathf.Sin(k * Mathf.PI) * 1.1f;
                sp.rectTransform.localScale = new Vector3(ss, ss, 1);
                sp.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 220 + i * 40);
                sp.canvasRenderer.SetAlpha(fade);
            }
        }

        // 가운데 SUPER COMBO!: 크게 내리꽂혀 출렁 → 잠깐 머물며 숨쉬기 → 더 커지면서 사라진다 (RoundSim이 1.6초 뒤 지운다)
        void DrawBanner(RoundSim R, float z)
        {
            var m = R.feverMsg;
            if (m == null || comboBannerPrefab == null)
            {
                if (banner != null && banner.gameObject.activeSelf) banner.gameObject.SetActive(false);
                return;
            }
            if (banner == null) { banner = Instantiate(comboBannerPrefab, textLayer); banner.name = comboBannerPrefab.name; }
            if (!banner.gameObject.activeSelf) banner.gameObject.SetActive(true);
            bool crit = m.lv >= 2;
            if (!ReferenceEquals(banner.key, m))
            {
                banner.key = m;
                banner.transform.SetAsLastSibling();
                if (banner.num != null) banner.num.text = ComboDigits.Spell(m.n, crit, true) ?? m.n + " COMBO";
            }
            int skin = crit ? 1 : 0;
            if (banner.skin != skin)
            {
                banner.skin = skin;
                var word = SpriteDB.Get(crit ? "UI/Combo/super_combo_c" : "UI/Combo/super_combo_n");
                if (word != null)
                {
                    if (banner.tag != null) banner.tag.sprite = word;
                    if (banner.tagGhost != null) banner.tagGhost.sprite = word;
                }
                if (banner.glow != null) banner.glow.color = crit ? GlowCrit : GlowGold;
            }

            const float IN = 0.2f, OUT = 1.15f, END = 1.6f;
            float t = m.t;
            float s, a;
            if (t < IN) { float k = t / IN; s = Mathf.Lerp(2.8f, 1, k * k); a = Mathf.Clamp01(k * 2.5f); }   // 내리꽂힘
            else if (t < OUT) { s = Punch(t - IN, 0.18f, 7, 24) * (1 + 0.025f * Mathf.Sin((t - IN) * 9)); a = 1; }
            else { float k = Mathf.Clamp01((t - OUT) / (END - OUT)); s = 1 + 0.7f * k * k; a = 1 - k; }   // 커지면서 사라짐
            float amp = t > IN ? 14 * Mathf.Exp(-(t - IN) * 9) : 0;
            var rt = (RectTransform)banner.transform;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = new Vector2(RoundSim.WW * 0.5f * z, -RoundSim.WH * 0.42f * z);
            banner.body.anchoredPosition = new Vector2(Mathf.Sin(comboClock * 91), Mathf.Cos(comboClock * 77)) * amp * z;
            banner.body.localScale = new Vector3(s * z, s * z, 1);
            banner.body.localRotation = Quaternion.Euler(0, 0, -3 + (t < IN ? 0 : 4 * Mathf.Exp(-(t - IN) * 6) * Mathf.Sin((t - IN) * 22)));
            if (banner.tag != null) banner.tag.canvasRenderer.SetAlpha(a);
            if (banner.num != null)
            {
                // 콤보 수는 꽂힌 뒤 한 박자 늦게 아래에서 튀어 나온다
                float ns = EaseOutBack((t - IN - 0.05f) / 0.2f, 2.6f);
                banner.num.rectTransform.localScale = new Vector3(ns, ns, 1);
                banner.num.canvasRenderer.SetAlpha(a);
            }
            // 꽂히는 순간의 잔상·고리·빛, 사라질 때 한 번 더 잔상
            if (banner.tagGhost != null)
            {
                float k = t < OUT ? Mathf.Clamp01((t - IN) / 0.4f) : Mathf.Clamp01((t - OUT) / 0.35f);
                bool on = t >= IN;
                float gs = 1 + 0.6f * EaseOut(k);
                banner.tagGhost.rectTransform.localScale = new Vector3(gs, gs, 1);
                banner.tagGhost.canvasRenderer.SetAlpha(on ? 0.6f * (1 - k) * (t < OUT ? 1 : a) : 0);
            }
            if (banner.ring != null)
            {
                float k = Mathf.Clamp01((t - IN) / 0.4f);
                float rs = 0.5f + 1.3f * EaseOut(k);
                banner.ring.rectTransform.localScale = new Vector3(rs, rs, 1);
                banner.ring.canvasRenderer.SetAlpha(t >= IN ? (1 - k) * (1 - k) : 0);
            }
            if (banner.glow != null)
            {
                float k = Mathf.Clamp01((t - IN) / 0.6f);
                float gs = 1 + 0.5f * EaseOut(k);
                banner.glow.rectTransform.localScale = new Vector3(gs, gs, 1);
                banner.glow.canvasRenderer.SetAlpha(t < IN ? 0 : (0.35f + 0.55f * (1 - k)) * a);
            }
            Sparks(banner, true, t - IN, a);
        }

        void ReleaseCombos()
        {
            if (banner != null) { banner.key = null; banner.gameObject.SetActive(false); }
        }
    }
}
