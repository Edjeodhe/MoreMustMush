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
            // 효과가 없으면 속성 블록을 지운다: 블록이 붙은 렌더러는 묶어 그리지(배칭) 못해 그림마다 그리기 호출이 따로 생겼다.
            // 셰이더 기본값이 0이라 보이는 것은 같다 (버섯 휨은 바로 뒤 SetBend가 다시 넣는다)
            if (gold == 0 && dark == 0 && flash == 0)
            {
                if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
                return;
            }
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(GoldId, gold); mpb.SetFloat(DarkId, dark); mpb.SetFloat(FlashId, flash);
            r.SetPropertyBlock(mpb);
        }

        static readonly int BendId = Shader.PropertyToID("_Bend"), BendBaseId = Shader.PropertyToID("_BendBase"), BendHId = Shader.PropertyToID("_BendH");
        // Soft-body lean for a sprite on MoreMush/Sprite: lean = top offset as a fraction of the sprite width.
        public static void SetBend(SpriteRenderer r, float lean)
        {
            var b = r.sprite != null ? r.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(BendId, lean * b.size.x); mpb.SetFloat(BendBaseId, b.min.y); mpb.SetFloat(BendHId, b.size.y);
            r.SetPropertyBlock(mpb);
        }
    }
}
