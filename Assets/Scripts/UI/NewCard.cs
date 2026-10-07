using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Small card "icon + name + tag" used in settlement/tax news (prototype .newcard). Prefab: Prefabs/UI/NewCard
    public class NewCard : MonoBehaviour
    {
        public Image frame, icon;
        public TMP_Text label, tagLabel;
        float pop;


        // style: "" (new), "gold", "star", "unlock", "special"
        public void Set(Sprite s, int fx, string name, string tagText, string style)
        {
            UIUtil.SetImage(icon, s, fx);
            label.text = name; tagLabel.text = tagText;
            var outline = frame.GetComponent<Outline>();
            string border = style == "gold" ? "#e8a900" : style == "star" ? "#e8a900" : style == "unlock" ? "#5fb8ff" : style == "special" ? "#b07bff" : "#8ac06a";
            string bg = style == "gold" ? "#fff8d8" : style == "unlock" ? "#eef7ff" : style == "special" ? "#f6efff" : "#ffffff";
            frame.color = U.Hex(bg);
            if (outline != null) outline.effectColor = U.Hex(border);
            pop = 0;
        }

        void Update()
        {
            if (pop >= 1) return;
            pop = Mathf.Min(1, pop + Time.unscaledDeltaTime / 0.5f);
            float s = pop < 0.4f ? 1 + 0.15f * pop / 0.4f : 1.15f - 0.15f * (pop - 0.4f) / 0.6f;
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
