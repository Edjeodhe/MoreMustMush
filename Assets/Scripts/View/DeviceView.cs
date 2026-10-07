using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // One forest device (stump, moss, mole holes, stream, acorn switch). Prefab: Assets/Prefabs/Round/DeviceView.prefab
    public class DeviceView : MonoBehaviour
    {
        public SpriteRenderer a, b, c;          // main sprites (stump / moss / stream / mole holes / acorns)
        public SpriteRenderer[] glows = new SpriteRenderer[3];
        public LineRenderer link;               // mole tunnel dashed line
        readonly List<LineRenderer> chevrons = new List<LineRenderer>();

        public void Show(Device d, float now)
        {
            a.enabled = b.enabled = c.enabled = false;
            foreach (var g in glows) g.enabled = false;
            link.enabled = false;
            int used = 0;
            transform.localPosition = Vector3.zero;
            switch (d.type)
            {
                case "stump":
                {
                    a.enabled = true; a.sprite = SpriteDB.Get("Props/stump");
                    a.transform.localPosition = Art.P(d.x, d.y);
                    Art.FitPx(a, 92 * Defs.DEV_SCALE * (d.hitT > 0 ? 1.12f : 1));
                    Art.SetFx(a, 0, 0, d.hitT > 0 ? 0.35f : 0);
                    break;
                }
                case "moss":
                case "stream":
                {
                    a.enabled = true; a.sprite = SpriteDB.Get(d.type == "moss" ? "Props/moss" : "Props/stream");
                    a.drawMode = SpriteDrawMode.Sliced;
                    a.transform.localPosition = Art.P((d.x0 + d.x1) / 2, (d.y0 + d.y1) / 2);
                    a.transform.localScale = Vector3.one;
                    a.size = new Vector2((d.x1 - d.x0) / Art.PPU, (d.y1 - d.y0) / Art.PPU);
                    if (d.type == "stream") used = StreamChevrons(d, now);
                    break;
                }
                case "mole":
                {
                    a.enabled = b.enabled = true;
                    a.sprite = b.sprite = SpriteDB.Get("Props/mole");
                    a.transform.localPosition = Art.P(d.a.x, d.a.y); b.transform.localPosition = Art.P(d.b.x, d.b.y);
                    Art.FitPx(a, 84 * Defs.DEV_SCALE); Art.FitPx(b, 84 * Defs.DEV_SCALE);
                    link.enabled = true; link.positionCount = 2;
                    link.SetPosition(0, Art.P(d.a.x, d.a.y)); link.SetPosition(1, Art.P(d.b.x, d.b.y));
                    break;
                }
                case "acorn":
                {
                    var rs = new[] { a, b, c };
                    for (int i = 0; i < 3; i++)
                    {
                        rs[i].enabled = true;
                        rs[i].sprite = SpriteDB.Get(d.lit[i] > 0 ? "Props/acorn_on" : "Props/acorn_off");
                        rs[i].transform.localPosition = Art.P(d.pts[i].x, d.pts[i].y);
                        Art.FitPx(rs[i], 40 * Defs.DEV_SCALE);
                        glows[i].enabled = d.lit[i] > 0;
                        glows[i].transform.localPosition = rs[i].transform.localPosition;
                        glows[i].transform.localScale = Vector3.one * (68 * Defs.DEV_SCALE / Art.PPU);
                    }
                    break;
                }
            }
            for (int i = used; i < chevrons.Count; i++) chevrons[i].enabled = false;
        }

        // flow arrows scrolling along the stream (prototype draws white chevrons moving at 250 px/s)
        int StreamChevrons(Device d, float now)
        {
            float off = (now * 250) % 80;
            int n = 0;
            if (d.fx != 0)
            {
                for (float x = d.x0 - 80; x < d.x1; x += 80)
                    foreach (var yy in new[] { 0.3f, 0.7f })
                    {
                        float xx = x + (d.fx > 0 ? off : 80 - off), y = U.Lerp(d.y0, d.y1, yy);
                        if (xx < d.x0 || xx + 30 > d.x1) continue;
                        float hx = d.fx > 0 ? xx + 30 : xx;
                        Chevron(n++, new[] { Art.P(xx, y), Art.P(xx + 30, y), Art.P(hx, y), Art.P(hx - d.fx * 8, y - 6), Art.P(hx, y), Art.P(hx - d.fx * 8, y + 6) });
                    }
            }
            else
            {
                for (float y = d.y0 - 80; y < d.y1; y += 80)
                    foreach (var xf in new[] { 0.3f, 0.7f })
                    {
                        float yy = y + (d.fy > 0 ? off : 80 - off), x = U.Lerp(d.x0, d.x1, xf);
                        if (yy < d.y0 || yy + 30 > d.y1) continue;
                        float hy = d.fy > 0 ? yy + 30 : yy;
                        Chevron(n++, new[] { Art.P(x, yy), Art.P(x, yy + 30), Art.P(x, hy), Art.P(x - 6, hy - d.fy * 8), Art.P(x, hy), Art.P(x + 6, hy - d.fy * 8) });
                    }
            }
            return n;
        }

        void Chevron(int i, Vector3[] pts)
        {
            while (chevrons.Count <= i)
            {
                var lr = Instantiate(link, transform);
                lr.name = "Chevron " + chevrons.Count;
                lr.startColor = lr.endColor = new Color(1, 1, 1, 0.7f);
                lr.textureMode = LineTextureMode.Stretch;
                chevrons.Add(lr);
            }
            var l = chevrons[i];
            l.enabled = true; l.positionCount = pts.Length; l.SetPositions(pts);
        }
    }
}
