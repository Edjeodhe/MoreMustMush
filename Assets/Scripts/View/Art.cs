using UnityEngine;

namespace MoreMush
{
    // Shared visual helpers: prototype pixel coords → world, sprite sizing, shader effect parameters.
    public static class Art
    {
        public const float PPU = 100f;

        // Prototype field/screen pixels (y down) → local world units (y up).
        public static Vector3 P(float x, float y, float z = 0) => new Vector3(x / PPU, -y / PPU, z);

        static Sprite circle;
        public static Sprite Circle => circle != null ? circle : circle = SpriteDB.Get("FX/circle");   // 1 world unit wide

        // Scale a SpriteRenderer so its sprite's larger side is `px` prototype pixels.
        public static void FitPx(SpriteRenderer sr, float px)
        {
            if (sr.sprite == null) return;
            var b = sr.sprite.bounds.size;
            float s = px / PPU / Mathf.Max(b.x, b.y);
            sr.transform.localScale = new Vector3(s, s, 1);
        }

        public static void SizePx(Transform t, Sprite sp, float w, float h)
        {
            if (sp == null) return;
            var b = sp.bounds.size;
            t.localScale = new Vector3(w / PPU / b.x, h / PPU / b.y, 1);
        }

        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        public static void SetFx(Renderer r, float gold = 0, float dark = 0, float flash = 0)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_Gold", gold); mpb.SetFloat("_Dark", dark); mpb.SetFloat("_Flash", flash);
            r.SetPropertyBlock(mpb);
        }
    }
}
