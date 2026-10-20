using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // A combo counter or the SUPER COMBO! banner on the field text layer (prefabs Assets/Prefabs/Round/ComboPop.prefab
    // and ComboBanner.prefab, made in the editor). RoundView clones / shows them and animates the parts; this only
    // holds the references. Parts a prefab doesn't use stay empty.
    //   body: scaled / shaken / tilted together · num: "12 COMBO" in the ComboDigits skin · ghost: afterimage of num
    //   glow: soft light bloom behind · ring: burst ring · tag: "CRITICAL!" (pop) or the SUPER COMBO! word (banner)
    //   tagGhost: afterimage of tag · sparks: glints around
    public class ComboPopSlot : MonoBehaviour
    {
        public RectTransform body;
        public TextMeshProUGUI num, ghost;
        public Image glow, ring, tag, tagGhost;
        public Image[] sparks;

        [System.NonSerialized] public object key;
        [System.NonSerialized] public int seen;
        [System.NonSerialized] public string shown;
        [System.NonSerialized] public int skin = -1;   // 0 gold, 1 crit: colours set for it
    }
}
