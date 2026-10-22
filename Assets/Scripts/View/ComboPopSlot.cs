using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // The SUPER COMBO! banner on the field text layer (prefab Assets/Prefabs/Round/ComboBanner.prefab, made in the
    // editor). RoundView clones / shows it and animates the parts; this only holds the references.
    //   body: scaled / shaken / tilted together · num: "25 COMBO" in the ComboDigits skin
    //   glow: soft light bloom behind · ring: burst ring · tag: the SUPER COMBO! word · tagGhost: afterimage of tag
    //   sparks: glints around
    public class ComboPopSlot : MonoBehaviour
    {
        public RectTransform body;
        public TextMeshProUGUI num;
        public Image glow, ring, tag, tagGhost;
        public Image[] sparks;

        [System.NonSerialized] public object key;
        [System.NonSerialized] public int skin = -1;   // 0 gold, 1 crit: colours set for it
    }
}
