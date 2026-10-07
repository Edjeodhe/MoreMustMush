using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Mushroom shop modal (prototype openShop / openShopOther): sell · village quests · spore shop.
    // Sell rows and quest cards are clones of hidden template slots placed in the hierarchy.
    public class ShopPanel : MonoBehaviour
    {
        public string mode = "sell", tab = "all";

        [Header("Header")]
        public Image[] modeTabs = new Image[3];          // sell, quest, spore
        public TMP_Text[] modeTabTexts = new TMP_Text[3];
        public GameObject questDot; public TMP_Text questDotText;
        public TMP_Text sub, goldText, sporeText, debtText;
        public GameObject sporeChip, debtChip;
        public Button questAllButton; public TMP_Text questAllText;

        [Header("Sell")]
        public GameObject sellView;
        public Image[] catTabs = new Image[4];           // all, ed, md, ps
        public TMP_Text[] catTabTexts = new TMP_Text[4];
        public RectTransform sellContent; public ShopRow rowTemplate;
        public GameObject sellEmpty;
        public TMP_Text footValue; public Button sellAllButton;

        [Header("Quests")]
        public GameObject questView;
        public RectTransform questContent; public QuestRow questTemplate;
        public GameObject questEmpty;

        [Header("Spore")]
        public GameObject sporeView;
        public TMP_Text sporePrice, sporeOwn, sporeUses;
        public Button[] sporeButtons = new Button[4];    // 1, 10, 100, max
        public TMP_Text[] sporeButtonTexts = new TMP_Text[4];

        static readonly string[] MODES = { "sell", "quest", "spore" };
        static readonly string[] TABS = { "all", "ed", "md", "ps" };
        static readonly string[] MODE_NAMES = { "판매", "마을 의뢰", "포자 상점" };

        public readonly Dictionary<string, double> qty = new Dictionary<string, double>();   // 판매 수량 (없으면 전부)
        readonly List<ShopRow> rows = new List<ShopRow>();
        readonly List<QuestRow> quests = new List<QuestRow>();

        public double Qty(string id) => U.Clamp((float)System.Math.Floor(qty.TryGetValue(id, out var q) ? q : InvCount(id)), 0, (float)InvCount(id));

        public void ResetQty() => qty.Clear();

        public void SetQty(string id, double v)
        {
            qty[id] = U.Clamp((float)System.Math.Floor(v), 0, (float)InvCount(id));
            RefreshRow(id);
        }

        public void StepQty(string id, double d) => SetQty(id, Qty(id) + d);
        public void SetQtyFrac(string id, double f) => SetQty(id, InvCount(id) * f);

        public void Render()
        {
            if (mode == "cook") mode = "sell";
            for (int i = 0; i < 3; i++)
            {
                bool on = mode == MODES[i];
                modeTabs[i].color = on ? U.Hex("#7a4f2e") : Color.white;
                modeTabTexts[i].color = on ? Color.white : U.Hex("#3b2414");
                modeTabTexts[i].text = MODE_NAMES[i];
            }
            int ready = QuestReadyCount();
            UIUtil.Show(questDot, ready > 0); questDotText.text = ready.ToString();
            goldText.text = $"{UIUtil.Ic("gold")}{U.Fmt(G.gold)}";
            sporeText.text = $"{UIUtil.Ic("spore")}{U.Fmt(G.spore)}";
            UIUtil.Show(sporeChip, mode != "sell");
            UIUtil.Show(debtChip, mode == "sell" && G.tax.debt > 0);
            debtText.text = $"체납 {U.Fmt(G.tax.debt)}";
            UIUtil.Show(questAllButton, mode == "quest");
            questAllButton.interactable = ready > 0;
            questAllText.text = $"일괄 완료{(ready > 0 ? $" ({ready})" : "")}";

            UIUtil.Show(sellView, mode == "sell");
            UIUtil.Show(questView, mode == "quest");
            UIUtil.Show(sporeView, mode == "spore");
            if (mode == "sell") RenderSell();
            else if (mode == "quest") RenderQuests();
            else RenderSpore();
        }

        void RenderSell()
        {
            double pm = PriceMul();
            sub.text = $"창고에 쌓인 버섯을 원하는 개수만큼 팔아 골드로 바꿔요. 팔아도 <color=#3a8a2a>도감 기록과 별은 그대로</color> 남아요.\n판매가 = 등급 기본가(일반 1 · 에픽 3 · 유니크 10 · 레전드리 40) × 판매가 보너스 ×{U.FmtN(pm)}";
            for (int i = 0; i < 4; i++)
            {
                bool on = tab == TABS[i];
                string col = i == 0 ? "#7a4f2e" : CATS[TABS[i]].color;
                catTabs[i].color = on ? U.Hex(col) : Color.white;
                catTabTexts[i].color = on ? Color.white : U.Hex("#3b2414");
                catTabTexts[i].text = $"{(i == 0 ? "전체" : CATS[TABS[i]].name)} {U.Fmt(InvTotal(i == 0 ? null : TABS[i]))}";
            }
            var list = ShopList(tab);
            while (rows.Count < list.Count) { var r = Instantiate(rowTemplate, sellContent); r.gameObject.SetActive(true); rows.Add(r); }
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < list.Count;
                UIUtil.Show(rows[i], on);
                if (on) rows[i].Set(this, list[i], pm);
            }
            UIUtil.Show(sellEmpty, list.Count == 0);
            double val = InvValue(tab == "all" ? null : tab);
            footValue.text = $"이 탭 전체 가치 ≈ <color=#c99a00>{U.Fmt(val)}</color>골드";
            sellAllButton.interactable = val >= 1;
        }

        // 수량만 바뀌면 그 줄만 갱신 (슬라이더를 끄는 중에 전체를 다시 그리지 않도록)
        public void RefreshRow(string id)
        {
            double pm = PriceMul();
            foreach (var r in rows) if (r.gameObject.activeSelf && r.id == id) r.Set(this, SP[id], pm);
        }

        void RenderQuests()
        {
            sub.text = $"마을 사람들이 원하는 버섯을 모아 전달하면 <color=#3a8a2a>판매가보다 훨씬 많은</color> 골드를 줘요. 의뢰는 3개씩이고, 세금 사이클이 바뀌면 새 의뢰로 바뀌어요. ('단골 거래' 노드로 보상 증가)";
            RefreshQuests();
            while (quests.Count < G.quests.Count) { var q = Instantiate(questTemplate, questContent); q.gameObject.SetActive(true); quests.Add(q); }
            for (int i = 0; i < quests.Count; i++)
            {
                bool on = i < G.quests.Count;
                UIUtil.Show(quests[i], on);
                if (on) quests[i].Set(i, G.quests[i]);
            }
            UIUtil.Show(questEmpty, G.quests.Count == 0);
        }

        void RenderSpore()
        {
            sub.text = "버섯 포자를 골드로 사요. 포자는 <color=#3a8a2a>버섯 농장 밭 재배</color>와 <color=#3a8a2a>꼬마 식당 요리</color>에 써요.";
            double pr = SporePrice(), mx = SporeMax();
            sporePrice.text = $"1개 {UIUtil.Ic("gold")}{U.Fmt(pr)}골드";
            sporeOwn.text = $"보유 {U.Fmt(G.spore)}개";
            int[] ns = { 1, 10, 100, -1 };
            string[] labels = { "1개", "10개", "100개", "살 수 있는 만큼" };
            for (int i = 0; i < 4; i++)
            {
                double n = ns[i] < 0 ? System.Math.Max(1, mx) : ns[i];
                sporeButtons[i].interactable = G.gold >= n * pr;
                string small = ns[i] < 0 ? (mx > 0 ? $"{U.Fmt(mx)}개 · {U.Fmt(mx * pr)}G" : "-") : $"{U.Fmt(ns[i] * pr)}G";
                sporeButtonTexts[i].text = $"{labels[i]}\n<size=70%>{small}</size>";
            }
            string uses = string.Join(" ", RECIPES.Where(r => r.need.ContainsKey("spore")).Select(r => $"{UIUtil.Ic("dish_" + r.id)}{r.need["spore"]}"));
            sporeUses.text =
                $"<b>버섯 밭 재배</b>  농장 밭에 포자 + 식용·약용·독 버섯을 심으면, 다 자랐을 때 <b>균사석</b>을 줘요. (식용 {CROPS["ed"].spore} · 약용 {CROPS["md"].spore} · 독 {CROPS["ps"].spore}개)\n" +
                $"<b>꼬마 식당 요리</b>  요리마다 포자가 들어가요. {uses}\n" +
                "<b>농기구</b>  밭에서 작물을 수확할 때 포자가 함께 나와요. 좋은 농기구일수록 많이 나와요.";
        }
    }
}
