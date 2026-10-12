using UnityEngine;

namespace MoreMush
{
    // One forest device (stump, moss, acorn switch). Prefab: Assets/Prefabs/Round/DeviceView.prefab
    public class DeviceView : MonoBehaviour
    {
        public SpriteRenderer a, b, c;          // main sprites (stump / moss / acorns)
        public SpriteRenderer[] glows = new SpriteRenderer[3];
        SpriteRenderer[] abc;                                    // acorn 세 개 (프레임마다 배열을 만들지 않게)

        public void Show(Device d, float now)
        {
            a.enabled = b.enabled = c.enabled = false;
            // 풀 슬롯은 라운드마다 다른 장치를 맡는다. 이끼가 바꾼 9-slice 모드를 다른 장치에 남기지 않는다
            var mode = d.type == "moss" ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
            if (a.drawMode != mode) a.drawMode = mode;
            foreach (var g in glows) g.enabled = false;
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
                {
                    a.enabled = true; a.sprite = SpriteDB.Get("Props/moss");
                    a.drawMode = SpriteDrawMode.Sliced;
                    a.transform.localPosition = Art.P((d.x0 + d.x1) / 2, (d.y0 + d.y1) / 2);
                    a.transform.localScale = Vector3.one;
                    a.size = new Vector2((d.x1 - d.x0) / Art.PPU, (d.y1 - d.y0) / Art.PPU);
                    break;
                }
                case "acorn":
                {
                    var rs = abc ??= new[] { a, b, c };
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
        }
    }
}
