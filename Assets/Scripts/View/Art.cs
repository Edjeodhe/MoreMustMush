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
            // 셰이더 기본값이 0이라 보이는 것은 같다. 수가 많은 버섯 몸통·날아가는 버섯은 이 대신 SpriteFx 재질 + FxColor를 쓴다
            if (gold == 0 && dark == 0 && flash == 0)
            {
                if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
                return;
            }
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(GoldId, gold); mpb.SetFloat(DarkId, dark); mpb.SetFloat(FlashId, flash);
            r.SetPropertyBlock(mpb);
        }

        // Materials/SpriteFx.mat(셰이더 _VCFX)용 정점 색: 효과값을 속성 블록 대신 색에 담아 같은 재질끼리 묶어 그리게 한다.
        // r = 1 - 번쩍, g = 1 - 황금, b = 1 - 실루엣, a = 투명도 (흰색 = 효과 없음). 이 재질에서는 색으로 물들일 수 없다
        public static Color FxColor(float gold = 0, float flash = 0, float alpha = 1, float dark = 0) =>
            new Color(1 - Mathf.Clamp01(flash), 1 - Mathf.Clamp01(gold), 1 - Mathf.Clamp01(dark), alpha);

        // 2×2 선형 변환 [[a, b], [c, d]] = 회전(phi) · 크기(scale) · 회전(theta) (각도는 도). 행렬식이 양수일 때 scale은 둘 다 양수.
        // Transform 두 겹(부모 회전+크기, 자식 회전)으로 기울임(shear)을 만들 때 쓴다
        public static void Decompose2x2(float a, float b, float c, float d, out float phi, out Vector2 scale, out float theta)
        {
            float e = (a + d) / 2, f = (a - d) / 2, g = (c + b) / 2, h = (c - b) / 2;
            float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + g * g);
            float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(h, e);
            scale = new Vector2(q + r, q - r);
            theta = (a2 - a1) / 2 * Mathf.Rad2Deg;
            phi = (a2 + a1) / 2 * Mathf.Rad2Deg;
        }
    }
}
