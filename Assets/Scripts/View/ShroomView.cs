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

        static readonly Color LowHp = U.Hex("#e0705a");
        static readonly Color BossHp = U.Hex("#d9614e"), GiantHp = U.Hex("#e08a64"), HpTrough = new Color(0.24f, 0.17f, 0.2f, 0.55f);
        bool pillsChecked, pills;

        // Dream set: HP bars are 9-sliced watercolor pills (UI/hp_back, UI/hp_fill); older sets keep the plain square
        SpriteRenderer hpLag;   // damage trail between the trough and the fill (made from the fill on first use)
        bool UsePills()
        {
            if (pillsChecked) return pills;
            pillsChecked = true;
            var back = SpriteDB.Get("UI/hp_back"); var fill = SpriteDB.Get("UI/hp_fill");
            pills = back != null && fill != null;
            if (pills)
            {
                hpBack.sprite = back; hpFill.sprite = fill; hpBack.drawMode = hpFill.drawMode = SpriteDrawMode.Sliced;
                hpLag = Instantiate(hpFill, hpFill.transform.parent);
                hpLag.name = "HpLag";
            }
            return pills;
        }

        static float BackOut(float t) { const float c = 1.9f; t -= 1; return 1 + (c + 1) * t * t * t + c * t * t; }

        // a pill w x h field pixels centred at (x, y): scaled so its height is h, so the round ends stay round
        static void Pill(SpriteRenderer r, float x, float y, float w, float h)
        {
            float sh = r.sprite.rect.height / r.sprite.pixelsPerUnit, s = h / Art.PPU / sh;
            r.transform.localPosition = Art.P(x, y);
            r.transform.localScale = new Vector3(s, s, 1);
            r.size = new Vector2(Mathf.Max(w / Art.PPU / s, sh), sh);
        }

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
            // every hit flashes the mushroom white for a moment (bosses a little longer)
            float hitFlash = m.hitT < 0.08f ? 0.75f * (1 - m.hitT / 0.08f) : 0;
            Art.SetFx(body, m.golden ? 1 : 0, 0, Mathf.Max(hitFlash, m.boss ? m.squish * 0.5f : 0));
            body.color = m.bossClone ? new Color(1, 1, 1, 0.7f + 0.15f * Mathf.Sin(now * 6)) : Color.white;
            // 말랑한 몸: 밑동은 고정, 갓이 천천히 흔들리고 맞으면 출렁인다 (셰이더 휨, 버섯마다 위상이 다름)
            float phase = m.x * 0.013f + m.y * 0.021f;
            float lean = Mathf.Sin(now * (big ? 1.1f : 1.7f) + phase) * (heap ? 0.02f : 0.035f)
                       + m.squish * Mathf.Sin((1 - m.squish) * 19f) * (big ? 0.08f : 0.16f);
            Art.SetBend(body, lean);

            // spawn: back-out ease, so the mushroom pops a little past full size and settles
            float gs = m.grow < 1 ? BackOut(m.grow) : 1;
            float stretchY = m.grow < 1 ? 1 + 0.25f * Mathf.Sin(m.grow * Mathf.PI) : 1;
            float sq = m.squish;
            // knockback: pushed away from the harvester on a hit, then springs back (view only, the sim keeps its spot)
            float kb = m.hitT < 0.3f ? Mathf.Exp(-m.hitT * 14) * Mathf.Cos(m.hitT * 30) * (m.boss || m.giant ? 5 : 10) : 0;
            transform.localPosition = Art.P(m.x + m.kx * kb, m.y + m.r * 0.3f + m.ky * kb);
            pivot.localScale = new Vector3(gs * (1 + sq * 0.18f) / Mathf.Sqrt(stretchY), gs * (1 - sq * 0.2f) * stretchY, 1);

            shadow.enabled = !m.wall;
            if (shadow.enabled)
            {
                shadow.transform.localPosition = Art.P(0, m.r * 0.55f);
                shadow.transform.localScale = new Vector3(m.r * 1.8f * m.grow / Art.PPU, m.r * 0.6f * m.grow / Art.PPU, 1);
            }

            bool showHp = m.boss || (m.hp < m.maxHp && !(m.sp.t == 0 && !m.giant && !m.cluster));
            // small mushrooms' bars fade out a while after the last hit; bosses and giants keep theirs
            float hpA = m.boss || m.giant ? 1 : Mathf.Clamp01((2.2f - m.hitT) / 0.4f);
            showHp &= hpA > 0.01f;
            hpBack.enabled = hpFill.enabled = showHp;
            if (hpLag != null) hpLag.enabled = showHp && UsePills();
            if (showHp)
            {
                float w = m.boss ? m.r * 2.2f : m.giant ? 160 : m.cluster ? m.r * 1.5f : m.r * 1.8f, y = -m.r - (big ? 30 : 10) - m.r * 0.3f;
                float hh = (m.boss ? 14 : m.giant ? 10 : 7) * (m.hitT < 0.15f ? 1 + 0.35f * (1 - m.hitT / 0.15f) : 1);   // swells on a hit
                if (m.boss) y = m.r * 0.9f;   // 보스는 크고 필드 위쪽에 있어 체력 막대를 발밑에 둔다
                float k = Mathf.Max(0, (float)(m.hp / m.maxHp));
                hpFill.color = m.boss ? BossHp : m.giant ? GiantHp : U.Hex(Defs.TIERS[m.sp.t].color);
                if (!m.boss && k < 0.3f) hpFill.color = Color.Lerp(LowHp, hpFill.color, k / 0.3f);   // runs coral near the end
                hpBack.sortingOrder = hpFill.sortingOrder - 1;
                if (UsePills())
                {
                    // watercolor pills: the trough a little bigger, the fill grows from the left, its round end kept
                    Pill(hpBack, 0, y + hh / 2, w + 4, hh + 4);
                    var tc = HpTrough; tc.a *= hpA; hpBack.color = tc;
                    // trail: a cream strip showing the damage just dealt, draining toward the real value
                    float lk = Mathf.Clamp01((float)((m.hpLag > m.hp ? m.hpLag : m.hp) / m.maxHp));
                    float lw = w * lk;
                    hpLag.enabled = lw > w * k + 0.5f;
                    if (hpLag.enabled) { Pill(hpLag, -w / 2 + Mathf.Max(lw, hh) / 2, y + hh / 2, Mathf.Max(lw, hh), hh); hpLag.color = new Color(1, 0.94f, 0.78f, hpA); hpLag.sortingOrder = hpFill.sortingOrder - 1; }
                    float fw = w * k;
                    hpFill.enabled = fw > 0.5f;
                    Pill(hpFill, -w / 2 + Mathf.Max(fw, hh) / 2, y + hh / 2, Mathf.Max(fw, hh), hh);
                    var fc = hpFill.color; fc.a = hpA; hpFill.color = fc;
                    hpBack.sortingOrder = hpFill.sortingOrder - 2;
                }
                else
                {
                    hpBack.transform.localPosition = Art.P(0, y + hh / 2);
                    hpBack.transform.localScale = new Vector3((w + 2) / Art.PPU, (hh + 2) / Art.PPU, 1);
                    hpFill.transform.localPosition = Art.P(-w / 2 + w * k / 2, y + hh / 2);
                    hpFill.transform.localScale = new Vector3(w * k / Art.PPU, hh / Art.PPU, 1);
                }
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
