using UnityEngine;

namespace MoreMush
{
    // Behind title / settlement (prototype drawTitleBg): the overrun town (the grandpa rig stands in it)
    // and a scythe harvester bouncing around the screen.
    public class TitleBackdrop : MonoBehaviour
    {
        public SpriteRenderer background;
        public SpriteRenderer harvester;

        void Update()
        {
            float now = Time.time;
            if (background.sprite != null)
            {
                background.transform.localPosition = Art.P(960, 540, 1);
                Art.SizePx(background.transform, background.sprite, Defs.W, Defs.H);
            }
            float t = now * 0.6f;
            harvester.transform.localPosition = Art.P(960 + Mathf.Cos(t) * 760, 540 + Mathf.Sin(t * 1.3f) * 400);
            harvester.transform.localRotation = Quaternion.Euler(0, 0, -now * 8 * Mathf.Rad2Deg);
        }
    }
}
