using UnityEngine;

namespace MoreMush
{
    // Spore cloud: six soft puffs circling (prototype drawRound "포자 구름"). Prefab: Assets/Prefabs/Round/CloudView.prefab
    public class CloudView : MonoBehaviour
    {
        public SpriteRenderer[] puffs = new SpriteRenderer[6];

        public void Show(Cloud c, float now)
        {
            transform.localPosition = Art.P(c.x, c.y);
            float a = Mathf.Min(1, c.t / 0.5f) * 0.35f;
            Color col = c.fog ? U.Rgba(90, 170, 90, a * 1.4f) : c.slow && c.weak ? U.Rgba(150, 110, 170, a) : c.slow ? U.Rgba(160, 120, 200, a) : U.Rgba(150, 150, 150, a);
            float k = c.r / 120;   // 독 안개는 포자 구름보다 크다
            for (int i = 0; i < puffs.Length; i++)
            {
                float ang = i / 6f * U.TAU + now * 0.5f;
                puffs[i].transform.localPosition = Art.P(Mathf.Cos(ang) * 50 * k, Mathf.Sin(ang) * 40 * k);
                puffs[i].transform.localScale = Vector3.one * (120 * k / Art.PPU);
                puffs[i].color = col;
            }
        }
    }
}
