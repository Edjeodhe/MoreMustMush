using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // One field plot slot (prototype drawField per-tile part). Children are authored for a 112 px plot and the
    // slot is scaled to the current plot size: grass / tilled soil / sprout base, the growing crop, growth bar,
    // ripe glow + gem, the "a critter is coming" overlay with its job icon, and the hover highlight.
    public class FieldTile : MonoBehaviour
    {
        public SpriteRenderer ground, glow, crop, barBack, barFill, gem, busy, busyIcon, hover;
        const float FULL = 112, BAR = 92;

        public void Draw(float cx, float cy, float T, SaveData.Plot p, double t0, float now, string bz, bool hov, int tool)
        {
            transform.localPosition = Art.P(cx, cy);
            float s = T / FULL;
            transform.localScale = new Vector3(s, s, 1);

            bool grow = p != null && p.s == "grow", ripe = grow && t0 >= p.at;
            float k = !grow ? 0 : ripe ? 1 : Mathf.Clamp01((float)((t0 - p.t0) / System.Math.Max(1, p.at - p.t0)));
            string g = p == null ? "tile_grass" : grow && !ripe && k < 0.25f ? "tile_sprout" : "tile_soil";
            var gs = SpriteDB.Get("Farm/Field/" + g);
            if (ground.sprite != gs) { ground.sprite = gs; Art.FitPx(ground, FULL - 4); }

            bool showCrop = grow && (ripe || k >= 0.25f);
            crop.enabled = showCrop;
            if (showCrop)
            {
                var cs = SpriteDB.Get("Farm/Field/crop_" + p.crop);
                if (crop.sprite != cs) crop.sprite = cs;
                float sc = 0.45f + 0.55f * (ripe ? 1 : (k - 0.25f) / 0.75f);
                Art.FitPx(crop, 86 * sc);
                float bob = ripe ? Mathf.Abs(Mathf.Sin(now * 4 + cx)) * 4 : 0;
                crop.transform.localPosition = Art.P(0, 4 - bob - 30 * (1 - sc));
            }
            glow.enabled = ripe;
            gem.enabled = ripe;
            if (ripe) gem.transform.localPosition = Art.P(FULL / 2 - 15, -FULL / 2 + 15 + Mathf.Sin(now * 5 + cx + cy) * 3);

            bool bar = grow && !ripe;
            barBack.enabled = barFill.enabled = bar;
            if (bar)
            {
                barFill.color = U.Hex(CATS[p.crop].color);
                barFill.transform.localScale = new Vector3(BAR * k / Art.PPU, barFill.transform.localScale.y, 1);
                barFill.transform.localPosition = Art.P(-BAR / 2 + BAR * k / 2, FULL / 2 - 10);
            }

            busy.enabled = busyIcon.enabled = bz != null;
            if (bz != null)
            {
                var bs = SpriteDB.Get(bz == "till" ? "Farm/Field/tool_" + tool : bz == "plant" ? "Icons/spore" : "UI/basket");
                if (busyIcon.sprite != bs) { busyIcon.sprite = bs; Art.FitPx(busyIcon, 30); }
            }
            hover.enabled = hov;
        }
    }
}
