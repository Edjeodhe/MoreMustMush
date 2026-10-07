using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Tree screen UI (prototype #topbar + #treebtns + legend + tooltip). The tree itself is TreeView (world).
    public class TreeScreen : MonoBehaviour
    {
        [ScenePath("World/TreeWorld")] public TreeView world;
        public TreeTooltip tooltip;
        public Image stageIcon; public TMP_Text stageTitle, stageSub;
        public TMP_Text goldText, gemText, sporeText;
        public Image taxBox; public TMP_Text taxTitle, taxSub;
        public TMP_Text codexButtonText, workshopButtonText;
        public GameObject workshopButton;
        public RectTransform sparkLayer;
        public Spark sparkTemplate;     // hidden template slot under sparkLayer, cloned per spark


        public void SetActiveWorld(bool on) { if (world != null) world.gameObject.SetActive(on); if (!on && tooltip != null) tooltip.Hide(); }

        public void Refresh()
        {
            if (G == null) return;
            var T = G.tax; int left = TaxRoundsLeft(); double bill = TaxBill();
            bool due = left <= 1;
            var th = THEME.TryGetValue(G.theme, out var tt) ? tt : LatestTheme();
            Theme nextT = null; foreach (var x in THEMES) if (x.from > StageNow()) { nextT = x; break; }
            stageIcon.sprite = SpriteDB.Icon(th.icon);
            stageTitle.text = $"스테이지 {StageNow()}";
            stageSub.text = th.n + (nextT != null ? $" · {UIUtil.Ic(nextT.icon)} {nextT.from - StageNow()}스테이지 뒤" : "");
            goldText.text = U.Fmt(G.gold);
            gemText.text = U.Fmt(G.gem);
            sporeText.text = U.Fmt(G.spore);
            taxTitle.text = $"사이클 {T.cycle} 세금 {U.Fmt(bill)}";
            taxSub.text = $"{(left == 1 ? "이번 라운드 끝나면 청구!" : $"{left}라운드 뒤 청구")} · 수익 {U.Fmt(T.income)}의 {Mathf.RoundToInt(TAX.share * 100)}% (최소 {U.Fmt(TaxAmount(T.cycle))}){(T.debt > 0 ? $" · 체납 {U.Fmt(T.debt)}" : "")}";
            taxBox.color = due ? (G.gold >= bill ? U.Hex("#e4f6d4") : U.Hex("#ffe0c8")) : U.Hex("#f6ead2");
            taxPulse = due && G.gold < bill;
            codexButtonText.text = $"{UIUtil.Ic("book")} 도감 ({CodexCount()}/{SP_TOTAL})";
            workshopButtonText.text = "공방";
        }

        bool taxPulse;
        void Update()
        {
            if (taxPulse && taxBox != null)
            {
                var o = taxBox.GetComponent<Outline>();
                if (o != null) o.effectColor = new Color(232 / 255f, 69 / 255f, 44 / 255f, 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2 / 1.2f)));
            }
        }

        // 강화 성공 불꽃 (prototype burstDom)
        public void Burst(Vector2 stagePos, Color col)
        {
            for (int i = 0; i < 10; i++)
            {
                var sp = Instantiate(sparkTemplate, sparkLayer);
                sp.gameObject.SetActive(true);
                sp.GetComponent<Image>().color = col;
                ((RectTransform)sp.transform).anchoredPosition = new Vector2(stagePos.x, -stagePos.y);
                float a = Random.value * U.TAU, s = 40 + Random.value * 60;
                sp.vel = new Vector2(Mathf.Cos(a) * s, -Mathf.Sin(a) * s);
            }
        }
    }
}
