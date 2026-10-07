using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One sell row of the mushroom shop (prototype .shoprow). Cloned from the hidden template in ShopPanel.
    public class ShopRow : MonoBehaviour
    {
        public string id;
        public Image icon;
        public TMP_Text title, tier, have, qtyText, sellText;
        public Slider slider;
        public UIAction[] steps = new UIAction[4];      // −10, −1, +1, +10   (act shopq, arg "id|d")
        public UIAction[] sets = new UIAction[3];       // 0, 절반, 전부      (act shopset, arg "id|f")
        public UIAction sell;                           // act shopsell, arg id
        static readonly int[] STEP = { -10, -1, 1, 10 };
        static readonly string[] SET = { "0", "0.5", "1" };

        ShopPanel owner;
        bool silent;

        void Awake() => slider.onValueChanged.AddListener(v => { if (!silent && owner != null) owner.SetQty(id, v); });

        public void Set(ShopPanel p, Species sp, double pm)
        {
            owner = p; id = sp.id;
            double h = InvCount(id), q = p.Qty(id), up = UnitPrice(sp, pm);
            icon.sprite = SpriteDB.Single(id);
            title.text = sp.n;
            int s = StarOf(id);
            tier.text = $"<color={TIERS[sp.t].color}>{TIERS[sp.t].name}</color> <color=#e8a900>{new string('★', s)}</color><color=#d8ccb4>{new string('★', 5 - s)}</color>";
            have.text = $"보유 <size=125%>{U.Fmt(h)}</size>개\n<size=75%><color=#8a6a4a>1개 {U.FmtN(up, up < 10 ? 2 : 0)}골드</color></size>";
            qtyText.text = U.Fmt(q);
            silent = true; slider.maxValue = (float)h; slider.value = (float)q; silent = false;
            sellText.text = $"판매\n<size=75%>+{U.Fmt(q * up)}</size>";
            sell.GetComponent<Button>().interactable = q > 0;
            sell.arg = id;
            for (int i = 0; i < steps.Length; i++) steps[i].arg = $"{id}|{STEP[i]}";
            for (int i = 0; i < sets.Length; i++) sets[i].arg = $"{id}|{SET[i]}";
        }
    }
}
