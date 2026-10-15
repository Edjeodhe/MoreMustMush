using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 세계수 그림: 단계별 나무, 구역(뿌리·줄기·가지·수관)마다 돋는 나무버섯, 장식 슬롯에 놓은 장식, 단계 팻말.
    // 나무버섯이 자라고 따이는 모습은 보여 주기용이다 (실제 수확은 Game.BagRate가 시간으로 계산해 보따리에 쌓는다).
    // 씬의 fruits·glows 칸(13개)이 모자라면 첫 칸을 복제해 늘린다. 오브젝트는 나무 밑동(WT.x, y)에 있다.
    public class FarmTreeView : MonoBehaviour
    {
        public SpriteRenderer treeSprite, shadow;
        public SpriteRenderer[] fruits = new SpriteRenderer[13];
        public SpriteRenderer[] glows = new SpriteRenderer[13];
        public TMP_Text signText;

        public class Slot { public string zone; public TreeMush mush; public Vector2 pos; public float phase; }
        public readonly List<Slot> slots = new List<Slot>();
        public readonly List<Vector2> decoPos = new List<Vector2>();
        readonly List<SpriteRenderer> fruitR = new List<SpriteRenderer>(), glowR = new List<SpriteRenderer>(), decoR = new List<SpriteRenderer>();
        string shownKey;
        float widthPx, heightPx;

        public float Top => WT.y - heightPx;
        public float Width => widthPx;

        // 슬롯 자리: 구역 영역 안에 왼쪽부터 고르게, 위아래로 살짝 엇갈리게
        void Layout()
        {
            string key = $"{Stage()}|{MushSlotCount()}|{DecoSlotCount()}|{Ring()}";
            if (key == shownKey) return;
            shownKey = key;
            int pic = WT.Picture(Stage());
            var sp = SpriteDB.Get("Farm/Tree/tree_" + pic);
            widthPx = WT.width[pic];
            if (sp != null)
            {
                treeSprite.sprite = sp;
                float k = widthPx / Art.PPU / sp.bounds.size.x;
                treeSprite.transform.localScale = new Vector3(k, k, 1);
                treeSprite.transform.localPosition = new Vector3(0, sp.bounds.size.y * k / 2 - 0.06f, 0);
            }
            heightPx = sp != null ? widthPx * sp.bounds.size.y / sp.bounds.size.x : widthPx;
            shadow.transform.localScale = new Vector3(widthPx * 0.9f / Art.PPU, widthPx * 0.16f / Art.PPU, 1);
            slots.Clear();
            foreach (var kv in ZoneSlots())
            {
                var z = ZONE[kv.Key]; int n = kv.Value;
                for (int i = 0; i < n; i++)
                {
                    float t = n == 1 ? 0.5f : i / (float)(n - 1);
                    float x = WT.x + (z.cx + (t * 2 - 1) * z.rx) * widthPx;
                    float y = WT.y - (z.cy + (i % 2 == 0 ? -1 : 1) * z.ry * 0.5f) * heightPx;
                    slots.Add(new Slot { zone = z.id, mush = SlotMush(z.id, i), pos = new Vector2(x, y), phase = (i * 0.37f + z.open * 0.11f) % 1 });
                }
            }
            // 장식: 설치형은 밑동 양옆 땅 위, 걸이형은 가지·수관 높이
            decoPos.Clear();
            int nd = DecoSlotCount();
            for (int i = 0; i < nd; i++)
            {
                int k = i / 2; float side = k % 2 == 0 ? -1 : 1, step = 1 + k / 2;
                decoPos.Add(DecoSlotHang(i)
                    ? new Vector2(WT.x + side * (0.18f + 0.1f * step) * widthPx, WT.y - (0.5f + 0.06f * (k % 3)) * heightPx)
                    : new Vector2(WT.x + side * (0.42f + 0.14f * step) * widthPx, WT.y + 24 + 10 * (k % 2)));
            }
            signText.text = Loc.F("{0}단계", Stage());
        }

        public Vector2 SlotPos(int i) { Layout(); return i >= 0 && i < slots.Count ? slots[i].pos : new Vector2(WT.x, WT.y); }
        public IEnumerable<int> SlotsIn(string zone) { Layout(); for (int i = 0; i < slots.Count; i++) if (slots[i].zone == zone) yield return i; }
        public Vector2 DecoPos(int i) { Layout(); return i >= 0 && i < decoPos.Count ? decoPos[i] : new Vector2(WT.x, WT.y); }

        float FruitPx => 34 + 20f * WT.Picture(Stage()) / (WT.stages - 1);

        // 자라는 정도 0~1 (1 = 다 자라 빛난다). 일하는 꼬마가 있는 구역은 다 자라면 바로 따여 다시 자란다
        public float Growth(int i, double now)
        {
            var s = slots[i];
            double cycle = s.mush.time / System.Math.Max(0.01, GrowMul(s.zone));
            double t = now / 1000 / cycle + s.phase;
            bool worked = KidsIn(s.zone).Count > 0;
            return worked ? (float)(t - System.Math.Floor(t)) : 1;
        }

        public int SlotAt(float x, float y)
        {
            Layout();
            int best = -1; float bd = FruitPx * 0.8f; bd *= bd;
            for (int i = 0; i < slots.Count; i++) { float d = U.D2(x, y, slots[i].pos.x, slots[i].pos.y - FruitPx * 0.3f); if (d < bd) { bd = d; best = i; } }
            return best;
        }
        public int DecoAt(float x, float y)
        {
            Layout();
            for (int i = 0; i < decoPos.Count; i++) if (Mathf.Abs(x - decoPos[i].x) < 50 && y < decoPos[i].y + 10 && y > decoPos[i].y - 100) return i;
            return -1;
        }
        public bool TrunkAt(float x, float y) { Layout(); return Mathf.Abs(x - WT.x) < widthPx * 0.22f && y < WT.y + 10 && y > WT.y - heightPx * 0.6f; }

        static SpriteRenderer Grow(List<SpriteRenderer> list, SpriteRenderer[] scene, int i)
        {
            while (list.Count <= i)
            {
                int n = list.Count;
                list.Add(n < scene.Length && scene[n] != null ? scene[n] : Instantiate(scene[0], scene[0].transform.parent));
            }
            return list[i];
        }

        public void Draw(float now, int hover)
        {
            Layout();
            transform.localPosition = Art.P(WT.x, WT.y);
            treeSprite.sortingOrder = WT.order; shadow.sortingOrder = WT.order - 1;
            double t0 = Now(); float px = FruitPx;
            for (int i = 0; i < slots.Count; i++)
            {
                var f = Grow(fruitR, fruits, i); var g = Grow(glowR, glows, i);
                var s = slots[i];
                float k = Growth(i, t0); bool ripe = k >= 0.97f;
                var spr = SpriteDB.Get("Farm/Tree/", s.mush.icon);
                if (f.sprite != spr) f.sprite = spr;
                f.enabled = true;
                float size = px * (0.3f + 0.7f * Mathf.Min(1, k * 1.15f)) * (hover == i ? 1.15f : 1);
                Art.FitPx(f, size);
                float bob = ripe ? Mathf.Abs(Mathf.Sin(now * 4 + i)) * 4 : 0;
                f.transform.localPosition = new Vector3((s.pos.x - WT.x) / Art.PPU, (WT.y - s.pos.y + size * 0.4f + bob) / Art.PPU, 0);
                var c = U.Hex(s.mush.tint); f.color = ripe ? c : c * new Color(0.85f, 0.85f, 0.85f, 0.95f);
                f.sortingOrder = WT.order + 2;
                g.enabled = ripe || s.mush.id == "glow";
                if (g.enabled)
                {
                    g.transform.localPosition = f.transform.localPosition;
                    float gs = px * 1.6f / Art.PPU * (1 + 0.08f * Mathf.Sin(now * 5 + i));
                    g.transform.localScale = new Vector3(gs, gs, 1);
                    g.sortingOrder = WT.order + 1;
                }
            }
            for (int i = slots.Count; i < fruitR.Count; i++) { fruitR[i].enabled = false; glowR[i].enabled = false; }
            for (int i = fruitR.Count; i < fruits.Length; i++) { if (fruits[i] != null) fruits[i].enabled = false; if (i < glows.Length && glows[i] != null) glows[i].enabled = false; }
            DrawDecos();
        }

        void DrawDecos()
        {
            var placed = G.farm.decoSlots;
            int n = 0;
            for (int i = 0; i < decoPos.Count && i < placed.Count; i++)
            {
                if (placed[i] == null || !DECO.TryGetValue(placed[i], out var d)) continue;
                while (decoR.Count <= n) decoR.Add(Instantiate(fruits[0], fruits[0].transform.parent));   // 장식 칸은 따로 복제
                var r = decoR[n++];
                r.enabled = true;
                var spr = SpriteDB.Get("Farm/Props/", d.art);
                if (r.sprite != spr) r.sprite = spr;
                Art.FitPx(r, d.hang ? 70 : 96);
                var p = decoPos[i];
                r.transform.localPosition = new Vector3((p.x - WT.x) / Art.PPU, (WT.y - p.y + (d.hang ? 35 : 48)) / Art.PPU, 0);
                r.color = Color.white;
                r.sortingOrder = d.hang ? WT.order + 3 : 1000 + Mathf.RoundToInt(p.y);
            }
            for (int i = n; i < decoR.Count; i++) decoR[i].enabled = false;
        }
    }
}
