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
            bool heap = m.cluster, big = m.giant || m.boss;
            // 보스는 군락이 아닌 단일 버섯 (전용 그림이 있으면 그것을 쓴다)
            body.sprite = m.boss ? SpriteDB.Get("Bosses/", m.bossDef.id) ?? SpriteDB.Single(sp.id) : heap || big ? SpriteDB.Colony(sp.id) : SpriteDB.Single(sp.id);
            float size = big ? m.r * 2.4f : heap ? m.r * 2.25f : m.r * 2.35f;
            Art.FitPx(body, size);
            body.transform.localPosition = Art.P(0, -m.r * 0.3f);
            body.sortingOrder = order;
            // 보스는 맞을 때 하얗게 번쩍, 환상 버섯 분신은 반투명
            Art.SetFx(body, m.golden ? 1 : 0, 0, m.boss ? m.squish * 0.5f : 0);
            body.color = m.bossClone ? new Color(1, 1, 1, 0.7f + 0.15f * Mathf.Sin(now * 6)) : Color.white;
            // 말랑한 몸: 밑동은 고정, 갓이 천천히 흔들리고 맞으면 출렁인다 (셰이더 휨, 버섯마다 위상이 다름)
            float phase = m.x * 0.013f + m.y * 0.021f;
            float lean = Mathf.Sin(now * (big ? 1.1f : 1.7f) + phase) * (heap ? 0.02f : 0.035f)
                       + m.squish * Mathf.Sin((1 - m.squish) * 19f) * (big ? 0.08f : 0.16f);
            Art.SetBend(body, lean);

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

            bool showHp = m.boss || (m.hp < m.maxHp && !(m.sp.t == 0 && !m.giant && !m.cluster));
            hpBack.enabled = hpFill.enabled = showHp;
            if (showHp)
            {
                float w = m.boss ? m.r * 2.2f : m.giant ? 160 : m.cluster ? m.r * 1.5f : m.r * 1.8f, y = -m.r - (big ? 30 : 10) - m.r * 0.3f;
                float hh = m.boss ? 12 : m.giant ? 8 : 4;
                if (m.boss) y = m.r * 0.9f;   // 보스는 크고 필드 위쪽에 있어 체력 막대를 발밑에 둔다
                hpBack.transform.localPosition = Art.P(0, y + hh / 2);
                hpBack.transform.localScale = new Vector3((w + 2) / Art.PPU, (hh + 2) / Art.PPU, 1);
                float k = Mathf.Max(0, (float)(m.hp / m.maxHp));
                hpFill.transform.localPosition = Art.P(-w / 2 + w * k / 2, y + hh / 2);
                hpFill.transform.localScale = new Vector3(w * k / Art.PPU, hh / Art.PPU, 1);
                hpFill.color = m.boss ? U.Hex("#ff4a4a") : m.giant ? U.Hex("#ff6b6b") : U.Hex(Defs.TIERS[m.sp.t].color);
                hpBack.sortingOrder = hpFill.sortingOrder - 1;
            }

            cracks.enabled = big && m.hp < m.maxHp;
            if (cracks.enabled)
            {
                float k = 1 - (float)(m.hp / m.maxHp);
                cracks.color = new Color(1, 1, 1, Mathf.Clamp01(k * 1.2f));
                cracks.transform.localPosition = Art.P(0, -40 - m.r * 0.3f);
                Art.FitPx(cracks, 150);
                cracks.sortingOrder = order + 1;
            }

            // 보스: 비현실적인 네온 발광 (밤이 아니어도 늘 켠다)
            if (m.boss)
            {
                glow.enabled = true;
                var gc = U.Hex(m.bossDef.col); gc.a = 0.55f + 0.2f * Mathf.Sin(now * 3);
                glow.color = gc;
                glow.transform.localPosition = Art.P(0, -m.r * 0.3f);
                glow.transform.localScale = Vector3.one * (m.r * (5.2f + 0.3f * Mathf.Sin(now * 3)) / Art.PPU);
                return;
            }
            glow.enabled = night && sp.glow;
            if (glow.enabled)
            {
                // 선화 버섯이 가려지지 않게 작고 옅은 빛
                glow.color = sp.c == "ps" && sp.sh == "fan" ? new Color(140 / 255f, 1, 160 / 255f, 0.3f) : new Color(1, 250 / 255f, 200 / 255f, 0.26f);
                glow.transform.localPosition = Art.P(0, -m.r * 0.3f);
                glow.transform.localScale = Vector3.one * (m.r * 3.6f / Art.PPU);
            }
        }
    }
}
