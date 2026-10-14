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
        public TMP_Text goldText, gemText;
        public TMP_Text codexButtonText, workshopButtonText;
        public GameObject workshopButton;
        public GameObject farmBadge; public TMP_Text farmBadgeText;   // 꼬마 부탁 + 다 자란 작물
        public TMP_Text autoText; public GameObject autoBadge;        // 자동 수확 보상: 쌓인 시간, 가득 차면 알림
        public Image bossButtonFrame; public TMP_Text bossButtonText;  // 보스 도전 켜고 끄기 (UIAction bosstoggle)
        public RectTransform sparkLayer;
        public Spark sparkTemplate;     // hidden template slot under sparkLayer, cloned per spark


        public void SetActiveWorld(bool on) { if (world != null) world.gameObject.SetActive(on); if (!on && tooltip != null) tooltip.Hide(); }

        public void Refresh()
        {
            if (G == null) return;
            var th = THEME.TryGetValue(G.theme, out var tt) ? tt : LatestTheme();
            Theme nextT = null; foreach (var x in THEMES) if (x.from > StageNow()) { nextT = x; break; }
            stageIcon.sprite = SpriteDB.Icon(th.icon);
            stageTitle.text = Loc.F("스테이지 {0}", StageNow());
            stageSub.text = nextT != null ? Loc.F("{0} · {1} {2}스테이지 뒤", th.n, UIUtil.Ic(nextT.icon), nextT.from - StageNow()) : th.n;
            RefreshBoss();
            goldText.text = U.Fmt(G.gold);
            gemText.text = U.Fmt(G.gem);
            codexButtonText.text = Loc.F("{0} 도감 ({1}/{2})", UIUtil.Ic("book"), CodexCount(), SP_TOTAL);
            int hvOn = OnHvs().Count, hvSlots = HvSlots();
            string hvCount = hvOn > hvSlots ? $"<color=#d04a3a>{hvOn}/{hvSlots}</color>" : $"{hvOn}/{hvSlots}";
            workshopButtonText.text = Loc.T("공방") + $" <size=75%>({Loc.F("장착 {0}", hvCount)})</size>";
            RefreshFarmBadge();
        }

        // 보스 도전 버튼: 켜짐이면 보스 스테이지에서 보스와 싸우고, 꺼짐이면 그 스테이지에서 파밍만 한다
        void RefreshBoss()
        {
            if (bossButtonText == null) return;
            int bs = NextBossStage(StageNow());
            var b = BossAt(bs);
            string state = G.bossOn ? $"<color=#b8321e>{Loc.T("보스 도전: 켜짐")}</color>" : $"<color=#2a5a9a>{Loc.T("보스 도전: 꺼짐 (파밍)")}</color>";
            string where = bs == StageNow() ? Loc.F("지금 스테이지 {0} · {1}", bs, b.n) : Loc.F("스테이지 {0} · {1}", bs, b.n);
            bossButtonText.text = $"{UIUtil.Ic(BOSS_ICON)} {state}\n<size=70%>{where}</size>";
            if (bossButtonFrame != null) bossButtonFrame.color = G.bossOn ? U.Hex("#fff0e6") : U.Hex("#eef7ff");
        }

        int farmN = -1; float farmT;
        void RefreshFarmBadge()
        {
            farmN = FarmReadyCount();
            UIUtil.Show(farmBadge, farmN > 0);
            farmBadgeText.text = farmN.ToString();
            double h = AutoHours();
            autoText.text = Loc.F("{0} 자동 수확 <size=75%>{1}:{2:00}</size>", UIUtil.Ic("auto_harvest"), (int)h, (int)(h * 60 % 60));
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
