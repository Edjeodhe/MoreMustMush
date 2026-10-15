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
            // doodle set: the whole critter is one composed icon (Tools/art/build_critter_rig.py), centred on the rig
            // origin and ICON_UNITS (1.2) rig units square, drawn in the cap image; the shared-body parts hide
            var whole = SpriteDB.Get("Characters/Critters/icon_" + kind + (skin != null ? "_skin" : ""));
            foreach (var im in parts) if (im != null) im.enabled = whole == null;
            if (whole != null)
            {
                cap.sprite = whole;
                var crt = cap.rectTransform;
                crt.anchoredPosition = Vector2.zero;
                crt.sizeDelta = Vector2.one * 1.2f * unitPx;
            }
            else cap.sprite = SpriteDB.Get("Characters/Critters/" + (skin != null ? "skin_" : "cap_") + kind);
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
