using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Settlement card (prototype showSettlement): basket drop, gains, rolling gold counters, unlocks and news.
    public class SettleScreen : MonoBehaviour
    {
        public RectTransform basket;
        public Image basketShroomPrefab;
        public TMP_Text[] gainTexts = new TMP_Text[3];
        public TMP_Text saleValue, scoreLabel, scoreValue, bonusLabel, bonusValue, debtValue, goldValue, meta, themeText, recsText, toTreeText;
        public GameObject bonusRow, debtRow, themeBox, unlockBox, newsBox, recsBox;
        public RectTransform unlockCards, newsCards;
        [AssetPath("Assets/Prefabs/UI/NewCard.prefab")] public NewCard cardPrefab;
        public RectTransform card;
        public NewCard unlockMore, newsMore;     // "+n개" 칸 (각 Cards 안에 숨겨 둔 슬롯)

        const int UNLOCK_ROWS = 1, NEWS_ROWS = 3;  // 카드 칸 줄 수 상한
        const float SCREEN_MARGIN = 24;            // 정산 카드 위아래 여백

        RoundSim.Summary sum;
        readonly List<NewCard> unlockList = new List<NewCard>(), newsList = new List<NewCard>();
        float t0;
        bool rolledSound;
        readonly List<(RectTransform rt, float delay, float y)> drops = new List<(RectTransform, float, float)>();


        public void Show(RoundSim.Summary s)
        {
            sum = s; t0 = Time.unscaledTime; rolledSound = false;
            for (int i = 0; i < 3; i++) gainTexts[i].text = $"+{U.Fmt(s.gains[CAT_KEYS[i]])}개";
            scoreLabel.text = $"얻은 점수 → 골드 <size=70%><color=#8a6a4a>(×{U.FmtN(s.goldMul)})</color></size>";
            bonusRow.SetActive(s.bonusGold > 0);
            bonusLabel.text = $"보너스 골드 <size=70%><color=#8a6a4a>(엽전 수확기 {U.Fmt(s.coinGold)} · 특수 버섯 {U.Fmt(s.bonusGold - s.coinGold)})</color></size>";
            bonusValue.text = "+" + U.Fmt(s.bonusGold);
            debtRow.SetActive(s.debtTaken > 0);
            debtValue.text = "−" + U.Fmt(s.debtTaken);
            bool due = G.tax.roundsIn >= TAX.every;
            meta.text = $"창고 {U.Fmt(InvTotal())}개 (≈{U.Fmt(InvValue())}골드) · {(due ? $"{UIUtil.Ic("tax")} 세금 고지서가 도착했어요!" : $"세금 청구까지 {TaxRoundsLeft()}라운드")}";
            themeBox.SetActive(s.newTheme != null);
            if (s.newTheme != null) { var th = THEME[s.newTheme]; themeText.text = $"{UIUtil.Ic(th.icon)} 새 지역 열림: <color=#2a6ab8>{th.n}</color>\n<size=55%><color=#4a6a8a>{th.d} · 다음 라운드부터 이 지역으로 가요</color></size>"; }

            Clear(unlockList); Clear(newsList);
            foreach (var id in s.unlocks) Card(unlockCards, unlockList, SpriteDB.Single(id), 0, SP[id].n, "해금", "unlock");
            unlockBox.SetActive(unlockList.Count > 0);
            if (s.specialGot != null) Card(newsCards, newsList, SpriteDB.Get("Characters/Critters/" + s.specialGot + "_ref"), 0, SPC[s.specialGot].n, "NEW", "special");
            foreach (var id in s.newSpecies) Card(newsCards, newsList, SpriteDB.Single(id), 0, SP[id].n, "NEW", "");
            foreach (var id in s.newGolden) Card(newsCards, newsList, SpriteDB.Single(id), 3, "황금 " + SP[id].n, "NEW", "gold");
            foreach (var (id, st) in s.newStars) Card(newsCards, newsList, SpriteDB.Single(id), 0, SP[id].n, "NEW", "star");
            newsBox.SetActive(newsList.Count > 0);
            var recs = s.recs.Select(r => $"신기록! {RECORD_NAMES[r.k]} <color=#3b2414>{U.Fmt(r.v)}</color>{(r.old > 0 ? $" <size=70%><color=#8a6a4a>(이전 {U.Fmt(r.old)})</color></size>" : "")}").ToList();
            if (s.goldHv) recs.Insert(0, $"{UIUtil.Ic("medal")} 황금 수확기 해금!");
            recsBox.SetActive(recs.Count > 0);
            recsText.text = string.Join("\n", recs);
            toTreeText.text = due ? "세금 고지서 확인 ▶" : "균사 트리로 ▶";
            SpawnBasket(s);
            FitCards();
        }

        // 카드가 많으면 줄 수를 상한까지 자르고, 그래도 정산 카드가 화면을 넘치면 새 버섯 → 해금 순으로 한 줄씩 더 줄인다.
        void FitCards()
        {
            int unlockRows = UNLOCK_ROWS, newsRows = NEWS_ROWS;
            float limit = ((RectTransform)transform).rect.height - SCREEN_MARGIN * 2;
            while (true)
            {
                Limit(unlockList, unlockMore, unlockCards, unlockRows);
                Limit(newsList, newsMore, newsCards, newsRows);
                LayoutRebuilder.ForceRebuildLayoutImmediate(card);   // 화면 루트에는 레이아웃이 없어 자식까지 내려가지 않음
                if (card.rect.height <= limit) break;
                if (newsRows > 1 && newsList.Count > Cols(newsCards) * (newsRows - 1)) newsRows--;
                else if (unlockRows > 1 && unlockList.Count > Cols(unlockCards) * (unlockRows - 1)) unlockRows--;
                else break;
            }
        }

        static int Cols(RectTransform cards) => cards.GetComponent<GridLayoutGroup>().constraintCount;

        static void Limit(List<NewCard> list, NewCard more, RectTransform cards, int rows)
        {
            int slots = Cols(cards) * rows;
            bool over = list.Count > slots;
            int shown = over ? slots - 1 : list.Count;
            for (int i = 0; i < list.Count; i++) list[i].gameObject.SetActive(i < shown);
            more.gameObject.SetActive(over);
            if (!over) return;
            more.label.text = $"+{list.Count - shown}개";
            more.transform.SetAsLastSibling();
        }

        void Card(RectTransform parent, List<NewCard> list, Sprite s, int fx, string name, string tag, string style)
        {
            var c = Instantiate(cardPrefab, parent);
            c.gameObject.SetActive(true);
            c.Set(s, fx, name, tag, style);
            list.Add(c);
        }

        // 만든 카드만 지운다 ("+n개" 슬롯은 남김). Destroy는 프레임 끝에 일어나므로 먼저 꺼서 이번 레이아웃 계산에서 빠지게 한다.
        static void Clear(List<NewCard> list)
        {
            foreach (var c in list) if (c != null) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            list.Clear();
        }

        void SpawnBasket(RoundSim.Summary s)
        {
            foreach (var d in drops) if (d.rt != null) Destroy(d.rt.gameObject);
            drops.Clear();
            var pool = new List<string>();
            foreach (var c in CAT_KEYS) { int n = Mathf.Min(14, Mathf.CeilToInt(Mathf.Sqrt((float)s.gains[c]))); for (int i = 0; i < n; i++) pool.Add(c); }
            pool = pool.OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < pool.Count; i++)
            {
                string c = pool[i];
                var got = SPECIES.Where(x => x.c == c && Harvested(x.id)).ToList();
                var sp = got.Count > 0 ? U.Pick(got) : SPECIES.First(x => x.c == c);
                var img = Instantiate(basketShroomPrefab, basket);
                img.gameObject.SetActive(true);
                img.sprite = SpriteDB.Single(sp.id);
                img.rectTransform.anchoredPosition = new Vector2(20 + Random.value * 300, 40);
                drops.Add((img.rectTransform, i * 0.04f, 60 - Random.value * 40));
            }
            basketShroomPrefab.gameObject.SetActive(false);
        }

        void Update()
        {
            if (sum == null) return;
            float el = Time.unscaledTime - t0;
            // 바구니에 버섯이 후두둑 (cubic-bezier 오버슈트 근사)
            foreach (var d in drops)
            {
                float k = Mathf.Clamp01((el - d.delay) / 0.6f);
                float e = 1 - Mathf.Pow(1 - k, 3) + Mathf.Sin(k * Mathf.PI) * 0.15f;
                d.rt.anchoredPosition = new Vector2(d.rt.anchoredPosition.x, Mathf.Lerp(40, -d.y, e));
                d.rt.gameObject.SetActive(el >= d.delay);
            }
            // 롤링 카운터
            float Ease(float delay) { float k = Mathf.Clamp01((el * 1000 - delay) / 1100); return 1 - Mathf.Pow(1 - k, 3); }
            scoreValue.text = "+" + U.Fmt(sum.scoreGold * Ease(0));
            saleValue.text = "≈" + U.Fmt(sum.value * Ease(400));
            float e3 = Ease(800);
            goldValue.text = $"{UIUtil.Ic("gold")}{U.Fmt(sum.net * e3)}";
            if (e3 >= 1 && !rolledSound) { rolledSound = true; Snd.Buy(); }
        }
    }
}
