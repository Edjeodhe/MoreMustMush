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
    public partial class FarmTreeView : MonoBehaviour
    {
        public SpriteRenderer treeSprite, shadow;
        public SpriteRenderer[] fruits = new SpriteRenderer[13];
        public SpriteRenderer[] glows = new SpriteRenderer[13];
        public TMP_Text signText;

        public class Slot { public string zone; public TreeMush mush; public Vector2 pos; public float phase; }
        public readonly List<Slot> slots = new List<Slot>();
        public readonly List<Vector2> decoPos = new List<Vector2>();
        readonly List<SpriteRenderer> fruitR = new List<SpriteRenderer>(), glowR = new List<SpriteRenderer>(), decoR = new List<SpriteRenderer>();
        readonly Dictionary<int, Rect> decoRect = new Dictionary<int, Rect>();   // 슬롯 → 놓인 장식이 그려진 자리 (무대 px, y 아래 +)
        string shownKey;
        float widthPx, heightPx, footV = 1;   // footV = 그림에서 땅에 닿는 선 (v, 아래 1)

        public float Top => WT.y - footV * heightPx;
        public float Width => widthPx;
        public float Height { get { Layout(); return heightPx; } }

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
            // 땅에 닿는 선(도면 foot): 그림 아래 흐려 놓은 띠는 땅 속으로 내려서, 진한 둔덕 바닥이 밑동(WT.y)에 닿게 한다 (2026-10-20 떠 보임)
            footV = PicData(pic)?.foot ?? 1;
            if (sp != null) treeSprite.transform.localPosition = new Vector3(0, sp.bounds.size.y * treeSprite.transform.localScale.y / 2 - 0.06f - (1 - footV) * heightPx / Art.PPU, 0);
            shadow.transform.localScale = new Vector3(widthPx * 0.9f / Art.PPU, widthPx * 0.16f / Art.PPU, 1);
            slots.Clear();
            var plan = PicData(pic);   // 도면(tree_layout.json)에 적힌 자리가 있으면 그 자리, 없으면 구역 영역 공식
            foreach (var kv in ZoneSlots())
            {
                var z = ZONE[kv.Key]; int n = kv.Value;
                List<Vector2> pts = null; plan?.mush.TryGetValue(z.id, out pts);
                for (int i = 0; i < n; i++)
                {
                    Vector2 pos;
                    if (pts != null && i < pts.Count) pos = UV(pts[i].x, pts[i].y);
                    else
                    {
                        float t = n == 1 ? 0.5f : i / (float)(n - 1);
                        pos = new Vector2(WT.x + (z.cx + (t * 2 - 1) * z.rx) * widthPx, WT.y - (z.cy + (i % 2 == 0 ? -1 : 1) * z.ry * 0.5f) * heightPx);
                    }
                    slots.Add(new Slot { zone = z.id, mush = SlotMush(z.id, i), pos = pos, phase = (i * 0.37f + z.open * 0.11f) % 1 });
                }
            }
            // 장식 슬롯 공식 자리 (도면에 장식 자리가 없을 때·빈 슬롯 짚기): 설치형은 밑동 양옆 땅 위, 걸이형은 가지·수관 높이
            // (2026-10-20 나무에서 장식을 떼어 낸 맨 나무: 걸이형은 수관 아래 가지 안쪽에 — 바깥 칸이 수관 끝 허공에 뜨지 않게 좁혔다)
            decoPos.Clear();
            int nd = DecoSlotCount();
            for (int i = 0; i < nd; i++)
            {
                int k = i / 2; float side = k % 2 == 0 ? -1 : 1, step = 1 + k / 2;
                decoPos.Add(DecoSlotHang(i)
                    ? new Vector2(WT.x + side * (0.14f + 0.09f * step) * widthPx, WT.y - (0.5f + 0.06f * (k % 3)) * heightPx)
                    : new Vector2(WT.x + side * (0.42f + 0.14f * step) * widthPx, WT.y + 24 + 10 * (k % 2)));
            }
            Sign();
        }

        // 나무 옆 단계 팻말: 단계가 오를 때와 언어가 바뀔 때
        void Sign() { if (signText != null) signText.text = Loc.F("{0}단계", Stage()); }
        void OnEnable() => Loc.Changed += Sign;
        void OnDisable() => Loc.Changed -= Sign;

        public Vector2 SlotPos(int i) { Layout(); return i >= 0 && i < slots.Count ? slots[i].pos : new Vector2(WT.x, WT.y); }
        public IEnumerable<int> SlotsIn(string zone) { Layout(); for (int i = 0; i < slots.Count; i++) if (slots[i].zone == zone) yield return i; }
        public Vector2 DecoPos(int i) { Layout(); return i >= 0 && i < decoPos.Count ? decoPos[i] : new Vector2(WT.x, WT.y); }

        // fruits and decor grow with the camera zoom so they keep their size on screen next to the much bigger tree
        float ZoomK => WT.zoom[WT.Picture(Stage())];
        float FruitPx => (34 + 20f * WT.Picture(Stage()) / (WT.stages - 1)) * ZoomK;

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
        // 놓인 장식은 그려진 자리(도면 자리일 수 있다)로, 빈 슬롯은 공식 자리로 짚는다
        public int DecoAt(float x, float y)
        {
            Layout();
            foreach (var kv in decoRect) if (kv.Value.Contains(new Vector2(x, y))) return kv.Key;
            for (int i = 0; i < decoPos.Count; i++)
            {
                if (decoRect.ContainsKey(i)) continue;
                if (Mathf.Abs(x - decoPos[i].x) < 60 * ZoomK && y < decoPos[i].y + 10 && y > decoPos[i].y - 120 * ZoomK) return i;
            }
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
                var spr = SpriteDB.Get(s.mush.icon) ?? SpriteDB.Get("Farm/Tree/", s.mush.icon);
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
                    // a small faint halo: 24 big ones at 0.55 fogged the whole grown tree
                    float gs = px * 1.15f / Art.PPU * (1 + 0.08f * Mathf.Sin(now * 5 + i));
                    g.transform.localScale = new Vector3(gs, gs, 1);
                    var gc = g.color; gc.a = 0.28f; g.color = gc;
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
            var plan = PicData(WT.Picture(Stage()));
            decoRect.Clear();
            int n = 0;
            for (int i = 0; i < decoPos.Count && i < placed.Count; i++)
            {
                if (placed[i] == null || !DECO.TryGetValue(placed[i], out var d)) continue;
                while (decoR.Count <= n) decoR.Add(Instantiate(fruits[0], fruits[0].transform.parent));   // 장식 칸은 따로 복제
                var r = decoR[n++];
                r.enabled = true;
                var spr = SpriteDB.Get("Farm/Props/", d.art);
                if (r.sprite != spr) r.sprite = spr;
                // 2026-10-20 수채화 장식으로 교체하며 조금 키움 (예전 70·96은 큰 나무 옆에서 잘 안 보였다)
                // 도면에 그 장식 자리가 있으면 걸이형은 고리(위 끝)를, 설치형은 바닥(아래 끝)을 그 자리에 댄다. 없으면 슬롯 공식 자리에 바닥을 댄다
                DecoSpot at = null; plan?.deco.TryGetValue(d.id, out at);
                float px = DecoPx(d) * (at?.s ?? 1);
                Art.FitPx(r, px);
                var b = spr != null ? spr.bounds.size : Vector3.one;
                float w = px * b.x / Mathf.Max(b.x, b.y), h = px * b.y / Mathf.Max(b.x, b.y);
                var p = at != null ? UV(at.u, at.v) : decoPos[i];
                float cy = at != null && d.hang ? p.y + h / 2 : p.y - h / 2;   // 무대 y (아래 +)
                r.transform.localPosition = new Vector3((p.x - WT.x) / Art.PPU, (WT.y - cy) / Art.PPU, 0);
                r.color = Color.white;
                r.sortingOrder = d.hang ? WT.order + 3 : 1000 + Mathf.RoundToInt(p.y);
                decoRect[i] = new Rect(p.x - w / 2, cy - h / 2, w, h);
            }
            for (int i = n; i < decoR.Count; i++) decoR[i].enabled = false;
        }

        // 장식 크기 (무대 px, 긴 변). 웹 편집기(Tools/tree-layout)도 같은 값으로 그린다
        float DecoPx(Deco d) => (d.hang ? 95 : 115) * ZoomK;
    }
}
