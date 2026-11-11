using UnityEngine;

namespace MoreMush
{
    // One forest device (stump, moss, acorn switch). Prefab: Assets/Prefabs/Round/DeviceView.prefab
    public class DeviceView : MonoBehaviour
    {
        public SpriteRenderer a, b, c;          // main sprites (stump / moss / acorns)
        public SpriteRenderer[] glows = new SpriteRenderer[3];
        SpriteRenderer[] abc;                                    // acorn 세 개 (프레임마다 배열을 만들지 않게)
        // 꽃밭(예전 이끼 길): 9-slice로 늘이던 그림 대신 꽃밭 타일 3종(Props/flower_0..2)을 긴 쪽으로 무작위 순서로 잇는다 (사용자 2026-11-11)
        readonly System.Collections.Generic.List<SpriteRenderer> tiles = new System.Collections.Generic.List<SpriteRenderer>();
        Device tileFor; int[] tileOrder = new int[0];
        const float TILE_STEP = 0.8f, TILE_SIZE = 1.35f;   // 타일 간격 · 그림 크기 (짧은 변 대비): 가장자리가 들쭉날쭉해 겹쳐 잇는다

        public void Show(Device d, float now)
        {
            a.enabled = b.enabled = c.enabled = false;
            if (a.drawMode != SpriteDrawMode.Simple) a.drawMode = SpriteDrawMode.Simple;   // 예전 이끼 9-slice가 남지 않게
            int usedTiles = 0;
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
                    float w = d.x1 - d.x0, h = d.y1 - d.y0;
                    bool hz = w >= h;
                    float side = hz ? h : w, len = hz ? w : h, step = side * TILE_STEP;
                    int n = Mathf.Max(1, Mathf.CeilToInt((len - side) / step) + 1);
                    if (!ReferenceEquals(tileFor, d) || tileOrder.Length != n)
                    {
                        tileFor = d;
                        var rng = new System.Random(Mathf.RoundToInt(d.x0 * 7 + d.y0 * 13));
                        tileOrder = new int[n];
                        for (int i = 0; i < n; i++) tileOrder[i] = rng.Next(3);
                    }
                    float start = -(n - 1) * step / 2;
                    for (int i = 0; i < n; i++)
                    {
                        var r = Tile(i);
                        var sp = SpriteDB.Get(FlowerKeys[tileOrder[i]]) ?? SpriteDB.Get("Props/moss");
                        if (!ReferenceEquals(r.sprite, sp)) r.sprite = sp;
                        float o = start + i * step;
                        r.transform.localPosition = Art.P((d.x0 + d.x1) / 2 + (hz ? o : 0), (d.y0 + d.y1) / 2 + (hz ? 0 : o));
                        Art.FitPx(r, side * TILE_SIZE);
                        r.enabled = true;
                    }
                    usedTiles = n;
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
            for (int i = Mathf.Max(usedTiles, 1); i < tiles.Count; i++) tiles[i].enabled = false;   // 0번은 a (다른 장치도 쓴다)
        }

        static readonly string[] FlowerKeys = { "Props/flower_0", "Props/flower_1", "Props/flower_2" };

        // 타일 칸: 0번은 a, 그 뒤는 a를 복제해 같은 자리·정렬로 쓴다
        SpriteRenderer Tile(int i)
        {
            if (tiles.Count == 0) tiles.Add(a);
            while (tiles.Count <= i)
            {
                var r = Instantiate(a, a.transform.parent);
                r.name = "FlowerTile" + tiles.Count;
                tiles.Add(r);
            }
            return tiles[i];
        }
    }
}
