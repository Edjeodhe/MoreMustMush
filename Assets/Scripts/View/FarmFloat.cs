using TMPro;
using UnityEngine;

namespace MoreMush
{
    // Rising text on the farm (prototype FARM_RT.floats): outlined text, optionally led by a gem icon.
    public class FarmFloat : MonoBehaviour
    {
        public TMP_Text text;
        public SpriteRenderer gem;
        string shown;

        public void Draw(float x, float y, string s, bool withGem, Color col, float alpha)
        {
            if (shown != s) { shown = s; text.text = s; text.ForceMeshUpdate(); }
            float w = text.preferredWidth * Art.PPU;
            float tx = withGem ? x + 18 : x;
            transform.localPosition = Art.P(tx, y);
            col.a = alpha; text.color = col;
            gem.enabled = withGem;
            if (withGem) { gem.transform.localPosition = Art.P(-w / 2 - 22, 0); gem.color = new Color(1, 1, 1, alpha); }
        }
    }
}
