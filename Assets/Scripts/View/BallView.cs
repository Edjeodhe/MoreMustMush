using UnityEngine;

namespace MoreMush
{
    // One pinball (harvester token). Prefab: Assets/Prefabs/Round/BallView.prefab
    public class BallView : MonoBehaviour
    {
        public SpriteRenderer body;
        public LineRenderer trail;
        public SpriteRenderer blade;     // rotating blade range
        public SpriteRenderer aura;      // buff glow / skin aura
        public SpriteRenderer fire;      // fire critter perk
        public SpriteRenderer[] debuff = new SpriteRenderer[4];

        Vector3[] trailPoints = new Vector3[32];
        string auraKey;
        Color skinAura;

        public void Show(Ball b, RoundSim R, float now)
        {
            var st = R.st;
            bool busy = !b.perm && R.balls.Count > 12;
            transform.localPosition = Art.P(b.x, b.y);

            var skin = Game.HvSkinOn(b.hv);
            body.sprite = SpriteDB.Get("Harvesters/", skin != null ? skin.id : b.hv);
            Art.FitPx(body, b.r * 2.3f);
            body.transform.localRotation = Quaternion.Euler(0, 0, -b.ang * Mathf.Rad2Deg);
            body.color = new Color(1, 1, 1, b.perm ? 1 : Mathf.Min(0.85f, b.life));
            Art.SetFx(body, b.hs >= 5 ? 0.35f : 0);

            // 잔상
            trail.enabled = b.trail.Count >= 2 && !busy;
            if (trail.enabled)
            {
                trail.positionCount = b.trail.Count + 1;
                if (trailPoints.Length < b.trail.Count + 1) System.Array.Resize(ref trailPoints, Mathf.NextPowerOfTwo(b.trail.Count + 1));
                for (int i = 0; i < b.trail.Count; i++) trailPoints[i] = Art.P(b.trail[i].x - b.x, b.trail[i].y - b.y);
                trailPoints[b.trail.Count] = Vector3.zero;
                trail.SetPositions(trailPoints);
                trail.widthMultiplier = b.r * 0.5f / Art.PPU;
                var c = b.moss > 0 ? new Color(120 / 255f, 220 / 255f, 120 / 255f, 0.28f) : b.accel > 0 ? new Color(120 / 255f, 200 / 255f, 1, 0.3f) : new Color(1, 1, 1, 0.2f);
                trail.startColor = new Color(c.r, c.g, c.b, 0); trail.endColor = c;
            }

            blade.enabled = (st.blade || b.hv == "mill") && !busy;
            if (blade.enabled)
            {
                float rr = R.BladeRadius(b);
                blade.transform.localScale = Vector3.one * (rr * 2 / Art.PPU);
                blade.transform.localRotation = Quaternion.Euler(0, 0, -now * 6 * Mathf.Rad2Deg);
                blade.color = b.bladeFx > 0 ? new Color(1, 1, 1, 0.55f) : new Color(230 / 255f, 230 / 255f, 1, 0.2f);
            }

            // 버프 빛이 우선, 없으면 수확기 스킨의 은은한 빛
            bool buff = b.accel > 0 || b.clear > 0 || b.sharpen > 0;
            aura.enabled = buff || skin != null;
            if (buff)
            {
                aura.color = b.clear > 0 ? new Color(190 / 255f, 245 / 255f, 1, 0.18f) : b.sharpen > 0 ? new Color(1, 1, 1, 0.18f) : new Color(110 / 255f, 210 / 255f, 1, 0.18f);
                aura.transform.localScale = Vector3.one * (b.r * 3.2f / Art.PPU);
            }
            else if (skin != null)
            {
                if (auraKey != skin.aura) { auraKey = skin.aura; skinAura = U.Hex(auraKey); }
                var ac = skinAura; ac.a = 0.3f * (b.perm ? 1 : Mathf.Min(0.85f, b.life)); aura.color = ac;
                aura.transform.localScale = Vector3.one * (b.r * 3.4f / Art.PPU);
            }

            fire.enabled = st.fire && !busy;
            if (fire.enabled)
            {
                float fl = 1 + 0.15f * Mathf.Sin(now * 18);
                fire.transform.localScale = Vector3.one * (b.r * 2.8f * fl / Art.PPU);
            }

            bool deb = b.slow > 0 || b.weak > 0;
            for (int i = 0; i < debuff.Length; i++)
            {
                debuff[i].enabled = deb;
                if (!deb) continue;
                float a = now * 4 + i * U.TAU / 4;
                debuff[i].transform.localPosition = Art.P(Mathf.Cos(a) * b.r * 1.4f, Mathf.Sin(a) * b.r * 1.4f);
                debuff[i].color = (i % 2 == 1 && b.weak > 0) || b.slow <= 0 ? new Color(140 / 255f, 140 / 255f, 140 / 255f, 0.9f) : new Color(170 / 255f, 110 / 255f, 220 / 255f, 0.9f);
            }
        }
    }
}
