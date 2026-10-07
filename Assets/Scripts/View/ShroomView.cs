using UnityEngine;

namespace MoreMush
{
    // One mushroom (single, colony heap or giant) on the field. Prefab: Assets/Prefabs/Round/ShroomView.prefab
    public class ShroomView : MonoBehaviour
    {
        public Transform pivot;          // squish/grow anchor near the base
        public SpriteRenderer body;
        public SpriteRenderer shadow;
        public SpriteRenderer hpBack, hpFill;
        public SpriteRenderer cracks;
        public SpriteRenderer glow;      // night/full-moon glow

        public void Show(Shroom m, float now, bool night, int order)
        {
            var sp = m.sp;
            bool heap = m.cluster;
            body.sprite = heap || m.giant ? SpriteDB.Colony(sp.id) : SpriteDB.Single(sp.id);
            float size = m.giant ? m.r * 2.4f : heap ? m.r * 2.25f : m.r * 2.35f;
            Art.FitPx(body, size);
            body.transform.localPosition = Art.P(0, -m.r * 0.3f);
            body.sortingOrder = order;
            Art.SetFx(body, m.golden ? 1 : 0);

            float gs = m.grow < 1 ? 1 - Mathf.Pow(1 - m.grow, 3) : 1;
            float sq = m.squish;
            transform.localPosition = Art.P(m.x, m.y + m.r * 0.3f);
            pivot.localScale = new Vector3(gs * (1 + sq * 0.18f), gs * (1 - sq * 0.2f), 1);

            shadow.enabled = !m.wall;
            if (shadow.enabled)
            {
                shadow.transform.localPosition = Art.P(0, m.r * 0.55f);
                shadow.transform.localScale = new Vector3(m.r * 1.8f * m.grow / Art.PPU, m.r * 0.6f * m.grow / Art.PPU, 1);
            }

            bool showHp = m.hp < m.maxHp && !(m.sp.t == 0 && !m.giant && !m.cluster);
            hpBack.enabled = hpFill.enabled = showHp;
            if (showHp)
            {
                float w = m.giant ? 160 : m.cluster ? m.r * 1.5f : m.r * 1.8f, y = -m.r - (m.giant ? 30 : 10) - m.r * 0.3f;
                float hh = m.giant ? 8 : 4;
                hpBack.transform.localPosition = Art.P(0, y + hh / 2);
                hpBack.transform.localScale = new Vector3((w + 2) / Art.PPU, (hh + 2) / Art.PPU, 1);
                float k = Mathf.Max(0, (float)(m.hp / m.maxHp));
                hpFill.transform.localPosition = Art.P(-w / 2 + w * k / 2, y + hh / 2);
                hpFill.transform.localScale = new Vector3(w * k / Art.PPU, hh / Art.PPU, 1);
                hpFill.color = m.giant ? U.Hex("#ff6b6b") : U.Hex(Defs.TIERS[m.sp.t].color);
                hpBack.sortingOrder = hpFill.sortingOrder - 1;
            }

            cracks.enabled = m.giant && m.hp < m.maxHp;
            if (cracks.enabled)
            {
                float k = 1 - (float)(m.hp / m.maxHp);
                cracks.color = new Color(1, 1, 1, Mathf.Clamp01(k * 1.2f));
                cracks.transform.localPosition = Art.P(0, -40 - m.r * 0.3f);
                Art.FitPx(cracks, 150);
                cracks.sortingOrder = order + 1;
            }

            glow.enabled = night && sp.glow;
            if (glow.enabled)
            {
                glow.color = sp.c == "ps" && sp.sh == "fan" ? new Color(140 / 255f, 1, 160 / 255f, 0.5f) : new Color(1, 250 / 255f, 200 / 255f, 0.45f);
                glow.transform.localPosition = Art.P(0, -m.r * 0.3f);
                glow.transform.localScale = Vector3.one * (m.r * 6 / Art.PPU);
            }
        }
    }
}
