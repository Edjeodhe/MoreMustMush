using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush
{
    // Combo counter, fever banner (prototype drawComboHUD) and the screen-edge fever flames (drawFever),
    // plus the white flash overlay. Lives on the round canvas between the field texts and the HUD.
    public class ComboHUD : MonoBehaviour
    {
        public RectTransform combo; public TMP_Text comboNum, comboLabel, feverMul;
        public RectTransform feverMsg; public TMP_Text feverTitle, feverSub;
        public RectTransform flameLayer; public Image flash;

        public Image[] edges = new Image[4];          // screen-edge glow strips: bottom, top, left, right
        public Image tongueTemplate, emberTemplate;   // hidden template slots, cloned as the fever grows

        readonly List<Image> tongues = new List<Image>();
        readonly List<Image> embers = new List<Image>();

        // 바뀔 때만 만드는 글자 (콤보 숫자·피버 배율·피버 문구)
        readonly Dictionary<int, string> comboStr = new Dictionary<int, string>();
        Flame shownFeverF; double shownFeverMul = double.NaN; string feverStr;
        object shownMsg; string msgStr;

        Vector2 feverOrigin;
        void Awake()
        {
            feverOrigin = feverMul.rectTransform.anchoredPosition;
            UIUtil.SubCanvas(flameLayer);
        }   // 불꽃 수십 개가 매 프레임 움직여도 나머지 UI는 다시 배칭하지 않게

        Image Clone(Image template, List<Image> into)
        {
            var img = Instantiate(template, flameLayer);
            img.name = template.name + " " + into.Count;
            img.gameObject.SetActive(true);
            into.Add(img);
            return img;
        }

        public void Draw(RoundSim R, float now)
        {
            DrawFlames(R, now);
            int c = R.chain;
            UIUtil.Show(combo, c >= 3);
            if (c >= 3)
            {
                float size = Mathf.Min(110, 40 + 11 * Mathf.Log(c + 1, 2));
                float s = 1 + R.chainBump * 0.25f;
                var F = FLAME[FlameIdx(c)];
                string col = c >= FLAME_STEP ? F.text : c >= FEVER[1].at ? "#fff6d0" : c >= FEVER[0].at ? "#ffe36e" : c >= 10 ? "#ffc04a" : "#ffffff";
                string outl = c >= FLAME_STEP ? F.@out : c >= FEVER[1].at ? "#c2200a" : c >= FEVER[0].at ? "#d2560e" : "#5a2a00";
                combo.localScale = new Vector3(s, s, 1);
                combo.localRotation = Quaternion.Euler(0, 0, 0.06f * Mathf.Rad2Deg);
                if (!comboStr.TryGetValue(c, out var cs)) comboStr[c] = cs = c.ToString();
                Style(comboNum, cs, size, col, outl);
                Style(comboLabel, "COMBO", Mathf.Max(26, size * 0.32f), col, outl);
                comboLabel.rectTransform.anchoredPosition = new Vector2(0, -size * 0.62f);
                UIUtil.Show(feverMul, R.fever > 0);
                if (R.fever > 0)
                {
                    double fm = R.FeverMulPublic;
                    if (F != shownFeverF || fm != shownFeverMul) { shownFeverF = F; shownFeverMul = fm; feverStr = $"{F.icon} 점수 ×{fm}"; }
                    Style(feverMul, feverStr, 24, col, outl);
                    feverMul.rectTransform.anchoredPosition = feverOrigin + new Vector2(0, -size * 0.62f);
                }
            }
            else UIUtil.Show(feverMul, false);

            var m = R.feverMsg;
            UIUtil.Show(feverMsg, m != null);
            if (m != null)
            {
                float k = m.t;
                float sc = k < 0.18f ? 0.4f + k / 0.18f * 0.8f : 1.2f - Mathf.Min(0.2f, (k - 0.18f) * 0.4f);
                float a = k > 1.2f ? Mathf.Max(0, (1.6f - k) / 0.4f) : 1;
                feverMsg.localScale = new Vector3(sc, sc, 1);
                feverMsg.localRotation = Quaternion.Euler(0, 0, 0.05f * Mathf.Rad2Deg);
                var MF = m.pal >= 0 ? FLAME[m.pal] : null;
                if (m != shownMsg) { shownMsg = m; msgStr = m.noIcon ? m.text : $"{(MF != null ? MF.icon : UIUtil.Ic("s_burst"))} {m.text}"; }
                string txt = msgStr;
                Style(feverTitle, txt, m.lv == 2 ? 92 : 80, MF?.text ?? "#ffe9a0", MF?.@out ?? "#a01a08", a);
                UIUtil.Show(feverSub, m.sub != null);
                if (m.sub != null) Style(feverSub, m.sub, 36, MF?.text ?? "#ffe9a0", MF?.@out ?? "#a01a08", a);
            }
            flash.enabled = R.flash > 0;
            if (flash.enabled) flash.color = new Color(1, 1, 240 / 255f, R.flash * 0.3f);
        }

        static void Style(TMP_Text t, string s, float size, string col, string outline, float alpha = 1)
        {
            UIUtil.SetText(t, s);
            t.fontSize = size;
            var c = U.Hex(col); c.a = alpha; t.color = c;
            var o = U.Hex(outline);
            if ((Color)t.outlineColor != o) t.outlineColor = o;
        }

        void DrawFlames(RoundSim R, float now)
        {
            const float W = Defs.W, H = Defs.H;
            float k = R.feverK;
            bool on = k >= 0.02f || R.embers.Count > 0;
            flameLayer.gameObject.SetActive(on);
            if (!on) return;
            float a1 = Mathf.Min(1, k), a2 = Mathf.Max(0, k - 1);
            var F = FLAME[R.flamePal];
            float I = R.flameInt;
            float pulse = 0.85f + 0.15f * Mathf.Sin(now * 8);
            float ea = (0.13f + 0.06f * a2) * a1 * pulse * I, ed = 150 + 60 * a2 + 60 * I;
            // edges: bottom, top, left, right (gradient sprite fades from pivot side inward)
            Edge(0, W / 2, H, W, ed, 0, F.edge, ea);
            Edge(1, W / 2, 0, W, ed * 0.8f, 180, F.edge, ea * 0.85f);
            Edge(2, 0, H / 2, H, ed * 0.7f, -90, F.edge, ea * 0.85f);
            Edge(3, W, H / 2, H, ed * 0.7f, 90, F.edge, ea * 0.85f);
            int n = 0;
            n = Side(n, 0, H, 1, 0, 0, -1, W, 22, 1, 0, a1, a2, I, now, F);
            n = Side(n, 0, 0, 1, 0, 0, 1, W, 22, 0.75f, 3, a1, a2, I, now, F);
            n = Side(n, 0, 0, 0, 1, 1, 0, H, 12, 0.75f, 7, a1, a2, I, now, F);
            n = Side(n, W, 0, 0, 1, -1, 0, H, 12, 0.75f, 11, a1, a2, I, now, F);
            for (int i = n; i < tongues.Count; i++) tongues[i].enabled = false;
            int e = 0;
            foreach (var em in R.embers)
            {
                while (embers.Count <= e) Clone(emberTemplate, embers);
                var img = embers[e++];
                img.enabled = true;
                float al = (1 - em.t / em.life) * Mathf.Min(0.85f, 0.6f * I);
                var col = em.t / em.life < 0.4f ? F.core : F.mid; col.a = al; img.color = col;
                img.rectTransform.anchoredPosition = new Vector2(em.x, -em.y);
                img.rectTransform.sizeDelta = Vector2.one * (em.s * (0.5f + al * 0.5f) * 2);
            }
            for (int i = e; i < embers.Count; i++) embers[i].enabled = false;
        }

        void Edge(int i, float x, float y, float len, float depth, float rot, Color col, float alpha)
        {
            var img = edges[i];
            img.rectTransform.anchoredPosition = new Vector2(x, -y);
            img.rectTransform.sizeDelta = new Vector2(len, depth);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
            col.a = alpha; img.color = col;
        }

        int Side(int n, float ox, float oy, float ux, float uy, float nx, float ny, float len, int cnt, float hs, float seed, float a1, float a2, float I, float now, Flame F)
        {
            for (int i = 0; i < cnt; i++)
            {
                float t = (i + 0.5f) / cnt;
                float hgt = (36 + 26 * Mathf.Sin(now * 7 + i * 1.7f + seed) + 14 * Mathf.Sin(now * 13 + i * 2.9f + seed)) * a1 * (1 + 0.5f * a2) * (0.7f + 0.5f * I) * hs;
                if (hgt < 4) continue;
                while (tongues.Count <= n) Clone(tongueTemplate, tongues);
                var img = tongues[n++];
                img.enabled = true;
                float px = ox + ux * len * t, py = oy + uy * len * t, w = len / cnt * 0.8f * 2;
                float sway = Mathf.Sin(now * 11 + i + seed) * len / cnt * 0.35f;
                img.rectTransform.anchoredPosition = new Vector2(px, -py);
                img.rectTransform.sizeDelta = new Vector2(w, hgt);
                // inward normal (nx, ny) in stage coords → rotation of the up-pointing tongue sprite
                float ang = Mathf.Atan2(-ny, nx) * Mathf.Rad2Deg - 90 + sway * 0.2f;
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, ang);
                var c = Color.Lerp(F.core, F.mid, 0.4f); c.a = Mathf.Min(0.7f, 0.42f * I); img.color = c;
            }
            return n;
        }
    }
}
