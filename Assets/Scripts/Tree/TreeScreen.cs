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
        public TMP_Text goldText, gemText, sporeText, diaText;
        public TMP_Text codexButtonText, workshopButtonText;
        public GameObject workshopButton;
        public GameObject farmBadge; public TMP_Text farmBadgeText;   // 꼬마 부탁 + 다 자란 작물
        public TMP_Text autoText; public GameObject autoBadge;        // 자동 수확 보상: 쌓인 시간, 가득 차면 알림
        public RectTransform sparkLayer;
        public Spark sparkTemplate;     // hidden template slot under sparkLayer, cloned per spark


        public void SetActiveWorld(bool on) { if (world != null) world.gameObject.SetActive(on); if (!on && tooltip != null) tooltip.Hide(); }

        public void Refresh()
        {
            if (G == null) return;
            var th = THEME.TryGetValue(G.theme, out var tt) ? tt : LatestTheme();
            Theme nextT = null; foreach (var x in THEMES) if (x.from > StageNow()) { nextT = x; break; }
            stageIcon.sprite = SpriteDB.Icon(th.icon);
            stageTitle.text = $"스테이지 {StageNow()}";
            stageSub.text = th.n + (nextT != null ? $" · {UIUtil.Ic(nextT.icon)} {nextT.from - StageNow()}스테이지 뒤" : "");
            goldText.text = U.Fmt(G.gold);
            gemText.text = U.Fmt(G.gem);
            diaText.text = U.Fmt(G.dia);
            sporeText.text = U.Fmt(G.spore);
            codexButtonText.text = $"{UIUtil.Ic("book")} 도감 ({CodexCount()}/{SP_TOTAL})";
            workshopButtonText.text = "공방";
            RefreshFarmBadge();
        }

        int farmN = -1; float farmT;
        void RefreshFarmBadge()
        {
            farmN = FarmReadyCount();
            UIUtil.Show(farmBadge, farmN > 0);
            farmBadgeText.text = farmN.ToString();
            double h = AutoHours();
            autoText.text = $"{UIUtil.Ic("auto_harvest")} 자동 수확 <size=75%>{(int)h}:{(int)(h * 60 % 60):00}</size>";
            UIUtil.Show(autoBadge, h >= Defs.AUTO.maxHours || h >= 1);
        }

        void Update()
        {
            if (G != null && (farmT -= Time.deltaTime) <= 0) { farmT = 0.5f; RefreshFarmBadge(); }
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
