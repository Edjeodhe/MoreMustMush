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
        public SpriteRenderer hpLag;     // damage trail between the trough and the fill (prefab child; absent in old sets = plain square bars)
        public SpriteRenderer cracks;
        // 밤·보스 빛(Glow) 스프라이트는 뺐다 (2026-10-20 사용자 지시, 모바일 겹쳐 그리기 부담)

        static readonly Color LowHp = U.Hex("#e0705a");
        static readonly Color BossHp = U.Hex("#d9614e"), GiantHp = U.Hex("#e08a64"), HpTrough = new Color(0.24f, 0.17f, 0.2f, 0.55f);
        // Dream set: HP bars are 9-sliced watercolor pills (UI/hp_back, UI/hp_fill), set up in the prefab together with HpLag.
        // 예전에는 처음 체력바를 보일 때 HpLag를 Instantiate했다 (라운드 중 생성 끊김) → 프리팹에 넣어 둔다
        bool UsePills() => hpLag != null;

        static float BackOut(float t) { const float c = 1.9f; t -= 1; return 1 + (c + 1) * t * t * t + c * t * t; }

        // a pill w x h field pixels centred at (x, y): scaled so its height is h, so the round ends stay round
        static void Pill(SpriteRenderer r, float x, float y, float w, float h)
        {
            float sh = r.sprite.rect.height / r.sprite.pixelsPerUnit, s = h / Art.PPU / sh;
            r.transform.localPosition = Art.P(x, y);
            r.transform.localScale = new Vector3(s, s, 1);
            r.size = new Vector2(Mathf.Max(w / Art.PPU / s, sh), sh);
        }

        // 몸통을 피벗(밑동) 아래에 놓는다: 그림 크기 px, 피벗 위 rise px, 흔들림 lean(그림 폭 대비 꼭대기 쏠림), 피벗 크기(sqx, sqy).
        // 예전에는 셰이더가 렌더러마다 속성 블록(_Bend)으로 정점을 휘었는데, 속성 블록이 붙으면 묶어 그리지 못해 버섯마다 그리기 호출이
        // 따로 생겼다 → 그림 밑변을 고정한 기울임 x' = x + k(y - 밑), y' = 밑 + c(y - 밑)을 피벗(회전·크기)과 몸통(회전) 두 Transform으로 만든다.
        // 셰이더의 2차 휨 대신 1차 기울임이지만 꼭대기 쏠림과 줄어듦(c)은 같다
        void PlaceBody(Sprite sp, float px, float rise, float lean, float sqx, float sqy)
        {
            var bt = body.transform;
            Vector2 p = new Vector2(0, rise / Art.PPU);   // = Art.P(0, -rise)
            float s = sp != null ? px / Art.PPU / Mathf.Max(sp.bounds.size.x, sp.bounds.size.y, 1e-6f) : 0;   // Art.FitPx와 같은 크기
            if (s < 1e-6f || sqx < 1e-4f || sqy < 1e-4f)
            {
                // 막 돋아나는 순간(크기 0)이나 그림 없음: 기울임 없이 크기만 (0으로 나누지 않게)
                pivot.localRotation = Quaternion.identity; pivot.localScale = new Vector3(sqx, sqy, 1);
                bt.localPosition = p; bt.localRotation = Quaternion.identity; bt.localScale = Vector3.one;
                return;
            }
            var b = sp.bounds;
            float k = lean * b.size.x / Mathf.Max(b.size.y, 1e-4f), c = 1 - 0.25f * Mathf.Abs(k), baseY = b.min.y;
            // 피벗 공간에서 몸통 = Sq·(p + s·(B·v + t)), B = [[1, k], [0, c]], t = 밑변 고정 이동
            Art.Decompose2x2(s * sqx, s * sqx * k, 0, s * sqy * c, out float phi, out Vector2 sc, out float theta);
            pivot.localRotation = Quaternion.Euler(0, 0, phi);
            pivot.localScale = new Vector3(sc.x, sc.y, 1);
            // 몸통 위치 = Σ⁻¹·R(−phi)·Sq·(p + s·t)
            float wx = sqx * (p.x - s * k * baseY), wy = sqy * (p.y + s * baseY * (1 - c));
            float pr = -phi * Mathf.Deg2Rad, cr = Mathf.Cos(pr), sr = Mathf.Sin(pr);
            bt.localPosition = new Vector3((cr * wx - sr * wy) / sc.x, (sr * wx + cr * wy) / Mathf.Max(sc.y, 1e-6f), 0);
            bt.localRotation = Quaternion.Euler(0, 0, theta);
            bt.localScale = Vector3.one;
        }

        public void Show(Shroom m, float now, int order)
        {
            var sp = m.sp;
            bool heap = m.cluster, big = m.giant || m.boss;
            // 보스는 군락이 아닌 단일 버섯 (전용 그림이 있으면 그것을 쓴다)
            var bodySp = m.boss ? SpriteDB.Get("Bosses/", m.bossDef.id) ?? SpriteDB.Single(sp.id) : heap || big ? SpriteDB.Colony(sp.id) : SpriteDB.Single(sp.id);
            if (!ReferenceEquals(body.sprite, bodySp)) body.sprite = bodySp;
            float size = big ? m.r * 2.4f : heap ? m.r * 2.25f : m.r * 2.35f;
            body.sortingOrder = order;
            // 보스는 맞을 때 하얗게 번쩍, 환상 버섯 분신은 반투명
            // every hit flashes the mushroom white for a moment (bosses a little longer)
            // 효과는 정점 색으로 (몸통 재질 = SpriteFx): 속성 블록을 붙이지 않아 버섯끼리 묶어 그린다
            float hitFlash = m.hitT < 0.08f ? 0.75f * (1 - m.hitT / 0.08f) : 0;
            body.color = Art.FxColor(m.golden ? 1 : 0, Mathf.Max(hitFlash, m.boss ? m.squish * 0.5f : 0), m.bossClone ? 0.7f + 0.15f * Mathf.Sin(now * 6) : 1);
            // 말랑한 몸: 밑동은 고정, 갓이 천천히 흔들리고 맞으면 출렁인다 (버섯마다 위상이 다름)
            float phase = m.x * 0.013f + m.y * 0.021f;
            float lean = Mathf.Sin(now * (big ? 1.1f : 1.7f) + phase) * (heap ? 0.02f : 0.035f)
                       + m.squish * Mathf.Sin((1 - m.squish) * 19f) * (big ? 0.08f : 0.16f);

            // spawn: back-out ease, so the mushroom pops a little past full size and settles
            float gs = m.grow < 1 ? BackOut(m.grow) : 1;
            float stretchY = m.grow < 1 ? 1 + 0.25f * Mathf.Sin(m.grow * Mathf.PI) : 1;
            float sq = m.squish;
            // knockback: pushed away from the harvester on a hit, then springs back (view only, the sim keeps its spot)
            float kb = m.hitT < 0.3f ? Mathf.Exp(-m.hitT * 14) * Mathf.Cos(m.hitT * 30) * (m.boss || m.giant ? 5 : 10) : 0;
            transform.localPosition = Art.P(m.x + m.kx * kb, m.y + m.r * 0.3f + m.ky * kb);
            PlaceBody(bodySp, size, m.r * 0.3f, lean, gs * (1 + sq * 0.18f) / Mathf.Sqrt(stretchY), gs * (1 - sq * 0.2f) * stretchY);

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
        }
    }
}
