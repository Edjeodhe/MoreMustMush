using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush
{
    // A critter drawn with UI images (skin shop cards, pet card): the same parts and layout as CritterRig,
    // laid out in the hierarchy. Code swaps the cap / accessory sprites and places the accessory.
    public class CritterIcon : MonoBehaviour
    {
        public Image cap, acc;
        public Image[] parts;            // body, eyes, mouth, blush, feet: dimmed together for a silhouette
        public float unitPx = 120;       // UI pixels per CritterRig unit
        public float capY = 0.228f;      // cap sprite center in rig units

        public void Set(string kind, Skin skin, bool silhouette = false)
        {
            cap.sprite = SpriteDB.Get("Characters/Critters/" + (skin != null ? "skin_" : "cap_") + kind);
            string a = skin?.acc;
            UIUtil.Show(acc, a != null);
            if (a != null)
            {
                var sp = SpriteDB.Get("Characters/Critters/Acc/" + a);
                acc.sprite = sp;
                var p = ACC_PLACE[a];
                var rt = acc.rectTransform;
                rt.anchoredPosition = new Vector2(p.x, p.y) * unitPx;
                if (sp != null) rt.sizeDelta = new Vector2(p.w, p.w * sp.bounds.size.y / sp.bounds.size.x) * unitPx;
            }
            var fx = silhouette ? UIUtil.Fx(1) : null;
            cap.material = fx; acc.material = fx;
            foreach (var im in parts) im.material = fx;
        }
    }
}
