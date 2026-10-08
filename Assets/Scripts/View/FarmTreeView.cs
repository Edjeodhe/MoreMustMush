using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // The mushroom tree on the farm: its growth-stage picture (bigger and lusher with level), the mushrooms ripening on
    // the canopy (one fixed slot per possible mushroom, placed in the hierarchy), and a little level sign.
    // The object sits at the trunk base (Defs.TREE.x, y). It is a landmark at the back of the ranch and is always drawn
    // behind critters and buildings (Defs.TREE.order), so it never hides them.
    public class FarmTreeView : MonoBehaviour
    {
        public SpriteRenderer treeSprite, shadow;
        public SpriteRenderer[] fruits = new SpriteRenderer[13];   // TREE.Slots(maxLv)
        public SpriteRenderer[] glows = new SpriteRenderer[13];
        public TMP_Text signText;

        int shownLv = -1;
        readonly List<Vector2> slots = new List<Vector2>();
        float widthPx, heightPx;

        public float Top => TREE.y - heightPx;

        // 나무 그림·버섯 자리를 레벨에 맞춘다 (자리는 우듬지 타원 안에 해바라기 씨 모양으로 고르게)
        void Layout()
        {
            int lv = TreeLv(); if (lv == shownLv) return;
            shownLv = lv;
            int st = TREE.Stage(lv);
            var sp = SpriteDB.Get("Farm/Tree/tree_" + st);
            widthPx = TREE.width[st];
            BuildingView.Fit(treeSprite, sp, widthPx);
            heightPx = sp != null ? widthPx * sp.bounds.size.y / sp.bounds.size.x : widthPx;
            shadow.transform.localScale = new Vector3(widthPx * 0.9f / Art.PPU, widthPx * 0.16f / Art.PPU, 1);
            var c = TREE.canopy[st];
            slots.Clear();
            int n = TREE.Slots(lv);
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Sqrt((i + 0.5f) / n), a = i * 2.39996f;
                slots.Add(new Vector2(TREE.x + (c[0] + Mathf.Cos(a) * r * c[2]) * widthPx, TREE.y - (c[1] + Mathf.Sin(a) * r * c[3]) * widthPx));
            }
            signText.text = $"Lv.{lv}";
        }

        public Vector2 SlotPos(int i) { Layout(); return i >= 0 && i < slots.Count ? slots[i] : new Vector2(TREE.x, TREE.y); }

        float FruitPx => 40 + 6 * TREE.Stage(TreeLv());

        public int FruitAt(float x, float y)
        {
            Layout();
            int best = -1; float bd = FruitPx * 0.7f; bd *= bd;
            for (int i = 0; i < slots.Count && i < G.farm.tree.slots.Count; i++)
            {
                float d = U.D2(x, y, slots[i].x, slots[i].y - FruitPx * 0.3f);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public bool TrunkAt(float x, float y) { Layout(); return Mathf.Abs(x - TREE.x) < widthPx * 0.22f && y < TREE.y + 10 && y > TREE.y - heightPx * 0.45f; }

        public void Draw(float now, HashSet<int> busy, int hover)
        {
            Layout();
            transform.localPosition = Art.P(TREE.x, TREE.y);
            treeSprite.sortingOrder = TREE.order; shadow.sortingOrder = TREE.order - 1;
            var sl = G.farm.tree.slots; double t0 = Now();
            float px = FruitPx;
            for (int i = 0; i < Mathf.Min(fruits.Length, glows.Length); i++)
            {
                bool on = i < sl.Count && i < slots.Count;
                fruits[i].enabled = on;
                if (!on) { glows[i].enabled = false; continue; }
                var f = sl[i];
                bool ripe = t0 >= f.at;
                float k = ripe ? 1 : Mathf.Clamp01((float)((t0 - f.t0) / System.Math.Max(1, f.at - f.t0)));
                var spr = SpriteDB.Get("Farm/Tree/fruit_", f.kind);
                if (fruits[i].sprite != spr) fruits[i].sprite = spr;
                float size = px * (0.3f + 0.7f * k) * (hover == i ? 1.15f : 1);
                Art.FitPx(fruits[i], size);
                float bob = ripe ? Mathf.Abs(Mathf.Sin(now * 4 + i)) * 4 : 0;
                fruits[i].transform.localPosition = new Vector3((slots[i].x - TREE.x) / Art.PPU, (TREE.y - slots[i].y + size * 0.4f + bob) / Art.PPU, 0);
                fruits[i].color = busy.Contains(i) ? new Color(1, 1, 1, 0.55f) : ripe ? Color.white : new Color(0.85f, 0.85f, 0.85f, 0.95f);
                fruits[i].sortingOrder = treeSprite.sortingOrder + 2;
                glows[i].enabled = ripe && !busy.Contains(i);
                if (glows[i].enabled)
                {
                    glows[i].transform.localPosition = fruits[i].transform.localPosition;
                    float g = px * 1.6f / Art.PPU * (1 + 0.08f * Mathf.Sin(now * 5 + i));
                    glows[i].transform.localScale = new Vector3(g, g, 1);
                    glows[i].sortingOrder = treeSprite.sortingOrder + 1;
                }
            }
        }
    }
}
