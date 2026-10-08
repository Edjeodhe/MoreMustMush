using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One building on the farm (clone of a hidden template slot). The object sits at the bottom-center of the
    // building's footprint. Under construction it is drawn faded with a progress bar, time left and a hammer.
    public class BuildingView : MonoBehaviour
    {
        public SpriteRenderer sprite, shadow;
        public GameObject site;                      // construction overlay (bar, timer, hammer)
        public SpriteRenderer barBack, barFill, timerBack, hammer;
        public TMP_Text timer;

        const float BAR = 120;

        // Sprite fitted to a width in stage pixels and lifted so its bottom sits on the parent's origin
        public static void Fit(SpriteRenderer sr, Sprite s, float px)
        {
            if (s == null) return;
            if (sr.sprite != s) sr.sprite = s;
            float k = px / Art.PPU / s.bounds.size.x;
            sr.transform.localScale = new Vector3(k, k, 1);
            sr.transform.localPosition = new Vector3(0, s.bounds.size.y * k / 2 - 0.06f, 0);
        }

        // lifted: being moved (the ghost shows where it goes; the old spot stays faintly visible)
        long shownSec = -1;

        public void Draw(SaveData.Bld b, bool lifted = false)
        {
            var B = BUILDING[b.id];
            var r = BuildRect(B, b.gx, b.gy);
            transform.localPosition = Art.P(r.center.x, r.yMax);
            Fit(sprite, SpriteDB.Get("Farm/Props/", b.id), B.px);
            int order = 1000 + Mathf.RoundToInt(r.yMax) - 6;
            sprite.sortingOrder = order;
            shadow.transform.localScale = new Vector3(r.width * 0.95f / Art.PPU, 0.3f, 1);

            UIUtil.Show(site, !b.done && !lifted);
            if (b.done) { sprite.color = new Color(1, 1, 1, lifted ? 0.3f : 1); return; }
            float k = Mathf.Clamp01((float)((Now() - b.t0) / System.Math.Max(1, b.at - b.t0)));
            sprite.color = new Color(0.8f, 0.72f, 0.62f, lifted ? 0.25f : 0.45f + 0.35f * k);
            if (lifted) return;
            site.transform.localPosition = new Vector3(0, (r.height + 70) / Art.PPU, 0);
            barFill.transform.localScale = new Vector3(BAR * k / Art.PPU, barFill.transform.localScale.y, 1);
            barFill.transform.localPosition = new Vector3((-BAR / 2 + BAR * k / 2) / Art.PPU, barFill.transform.localPosition.y, 0);
            long sec = System.Math.Max(0, (long)System.Math.Ceiling((b.at - Now()) / 1000));   // 남은 초가 바뀔 때만 글자를 다시 만든다
            if (sec != shownSec) { shownSec = sec; UIUtil.SetText(timer, Mmss(sec)); }
            hammer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 10 + b.uid) * 25);
        }
    }
}
