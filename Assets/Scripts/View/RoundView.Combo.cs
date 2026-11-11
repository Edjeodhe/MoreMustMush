using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush
{
    // 가운데 SUPER COMBO! 배너 (RoundSim.feverMsg). 콤보 수는 오른쪽 위 HUD(StageHUD)에만 뜨고, 배너와 맞힐 때마다 뜨는 콤보 수는 없다.
    // 사람이 만든 프리팹(ComboBanner)을 글자 층에 복제해 쓰고, 여기서는 크기·자리·투명도만 움직인다.
    public partial class RoundView
    {
        [AssetPath("Assets/Prefabs/Round/ComboBanner.prefab")] public ComboPopSlot comboBannerPrefab;

        public const float ComboTilt = 15;   // 콤보·수퍼콤보 글씨 기울기(도, 오른쪽이 올라간다). HUD 콤보(StageHUD)도 같이 쓴다

        ComboPopSlot banner;
        float comboClock;   // 흔들림용 시계 (일시정지면 멈춘다)

        // 반짝이: 프리팹의 반짝이를 복제해 SPARKS개로 늘리고, 배너가 뜰 때마다 글자 둘레 넓게 크게 흩뿌린다 (사용자 2026-11-11)
        const int SPARKS = 16;
        struct Spark { public Image img; public Vector2 to; public float size, delay, spin; }
        readonly List<Spark> sparks = new List<Spark>();
        static readonly System.Random sparkRng = new System.Random(4242);   // 게임 난수(UnityEngine.Random)를 건드리지 않게 따로
        static float SR(float a, float b) => a + (float)sparkRng.NextDouble() * (b - a);

        // 크게 내리꽂혀 1 아래로 한 번 눌렸다가 자리 잡는 펀치 (t=0에서 1+a)
        static float Punch(float t, float a, float decay = 11, float freq = 26) => 1 + a * Mathf.Exp(-t * decay) * Mathf.Cos(t * freq);

        void DrawCombos(RoundSim R, float z, bool live)
        {
            if (live) comboClock += Time.deltaTime;
            DrawBanner(R, z);
        }

        // 새 배너마다 반짝이 자리·크기·박자를 다시 뽑는다: 글자 둘레 넓은 타원에 고루 (가운데 글자 위는 피한다)
        void ScatterSparks()
        {
            if (sparks.Count == 0)
            {
                Image src = null;
                if (banner.sparks != null) foreach (var sp in banner.sparks) if (sp != null) { src = sp; break; }
                if (src == null) return;
                foreach (var sp in banner.sparks) if (sp != null) sparks.Add(new Spark { img = sp });
                while (sparks.Count < SPARKS) sparks.Add(new Spark { img = Instantiate(src, src.transform.parent) });
                foreach (var s in sparks) { s.img.raycastTarget = false; s.img.rectTransform.anchoredPosition = Vector2.zero; }
            }
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                float ang = (i + SR(-0.35f, 0.35f)) / sparks.Count * Mathf.PI * 2, rad = SR(0.75f, 1.25f);
                s.to = new Vector2(Mathf.Cos(ang) * 560 * rad, Mathf.Sin(ang) * 270 * rad);
                s.size = SR(90, 190);
                s.delay = SR(0, 0.45f);
                s.spin = SR(-260, 260);
                s.img.rectTransform.sizeDelta = new Vector2(s.size, s.size);
                sparks[i] = s;
            }
        }

        // 반짝이: 글자에서 바깥으로 튀어 나가며 크게 피었다 돌며 진다 (하나씩 엇갈려)
        void DrawSparks(float t, float fade)
        {
            foreach (var s in sparks)
            {
                float k = (t - s.delay) / 0.7f;
                bool on = k > 0 && k < 1;
                if (s.img.enabled != on) s.img.enabled = on;
                if (!on) continue;
                var rt = s.img.rectTransform;
                rt.anchoredPosition = s.to * (0.45f + 0.55f * EaseOut(k));
                float ss = Mathf.Sin(k * Mathf.PI) * 1.15f;
                rt.localScale = new Vector3(ss, ss, 1);
                rt.localRotation = Quaternion.Euler(0, 0, s.spin * k);
                s.img.canvasRenderer.SetAlpha(fade);
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
                // 글자 그림은 프리팹에 걸린 Art/effect/text/super_combo 하나. 아래 "25 Combo!" 수와 뒤 색 빛(glow)은 뺐다 (사용자 2026-11-11)
                if (banner.num != null) banner.num.gameObject.SetActive(false);
                if (banner.glow != null) banner.glow.gameObject.SetActive(false);
                ScatterSparks();
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
            banner.body.localRotation = Quaternion.Euler(0, 0, ComboTilt + (t < IN ? 0 : 4 * Mathf.Exp(-(t - IN) * 6) * Mathf.Sin((t - IN) * 22)));
            if (banner.tag != null) banner.tag.canvasRenderer.SetAlpha(a);
            // 꽂히는 순간의 잔상·고리, 사라질 때 한 번 더 잔상
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
            DrawSparks(t - IN, a);
        }

        void ReleaseCombos()
        {
            if (banner != null) { banner.key = null; banner.gameObject.SetActive(false); }
        }
    }
}
