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

        // Sliced SpriteRenderer sized in prototype pixels. The object may be scaled down so the 9-slice border
        // (drawn big in the generated UI kit) looks thinner; the size is set in that scaled space.
        public static void SlicedPx(SpriteRenderer sr, float w, float h)
        {
            float k = sr.transform.localScale.x;
            sr.size = new Vector2(w, h) / PPU / (k == 0 ? 1 : k);
        }

        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        // 셰이더 속성 id는 한 번만 구한다 (라운드에서 프레임마다 수백 번 불림)
        static readonly int GoldId = Shader.PropertyToID("_Gold"), DarkId = Shader.PropertyToID("_Dark"), FlashId = Shader.PropertyToID("_Flash");
        public static void SetFx(Renderer r, float gold = 0, float dark = 0, float flash = 0)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(GoldId, gold); mpb.SetFloat(DarkId, dark); mpb.SetFloat(FlashId, flash);
            r.SetPropertyBlock(mpb);
        }
    }
}
