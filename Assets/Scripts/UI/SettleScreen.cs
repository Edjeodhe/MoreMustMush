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
    // Stage 씬 Canvas/SettleScreen에 둔다. StageController.Finished를 받아 열리고, 버튼(UIAction act)은 여기서 붙인다:
    //   totree → MainGame(균사 트리) · worldtree → 세계수(OutGame) · dmgstats → 데미지 통계 창
    public class SettleScreen : MonoBehaviour
    {
        public RectTransform basket;
        public Image basketShroomPrefab;
        public TMP_Text[] gainTexts = new TMP_Text[3];
        public TMP_Text title;   // 비어 있으면 Card/Title을 찾아 쓴다
        public TMP_Text saleValue, scoreLabel, scoreValue, bonusLabel, bonusValue, goldValue, meta, themeText, recsText, toTreeText;
        public GameObject bonusRow, themeBox, unlockBox, newsBox, recsBox;
        public RectTransform unlockCards, newsCards;
        public NewCard cardPrefab;   // Assets/Prefabs/UI/NewCard.prefab
        public RectTransform card;
        public NewCard unlockMore, newsMore;     // "+n개" 칸 (각 Cards 안에 숨겨 둔 슬롯)

        const int UNLOCK_ROWS = 1, NEWS_ROWS = 3;  // 카드 칸 줄 수 상한
        const float SCREEN_MARGIN = 24;            // 정산 카드 위아래 여백

        RoundSim.Summary sum;
        public RoundSim.Summary Sum => sum;
        readonly List<NewCard> unlockList = new List<NewCard>(), newsList = new List<NewCard>();
        float t0;
        bool rolledSound, animating;
        float animationEnd;
        readonly List<(RectTransform rt, float delay, float y)> drops = new List<(RectTransform, float, float)>();


        StageController stage;

        void Awake()
        {
            foreach (var a in GetComponentsInChildren<UIAction>(true)) Wire(a);
        }

        // 씬에서 꺼져 있어도 Stage가 부르게: StageController가 정산을 넘기면 켜고 보여 준다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, _) => Attach(); Attach(); }
        static void Attach()
        {
            var st = StageController.I; if (st == null) return;
            var ss = Object.FindFirstObjectByType<SettleScreen>(FindObjectsInactive.Include);
            if (ss == null || ss.stage == st) return;
            ss.stage = st;
            st.Finished += s => { ss.gameObject.SetActive(true); ss.Show(s); };
            ss.gameObject.SetActive(false);   // 라운드 중에는 숨긴다
        }

        void Wire(UIAction a)
        {
            var b = a.GetComponent<Button>();
            b.onClick.RemoveAllListeners();
            switch (a.act)
            {
                case "totree": b.onClick.AddListener(() => { Snd.Ui(); Leave(Scenes.MainGame); }); break;
                case "worldtree": b.onClick.AddListener(() => { Snd.Ui(); Leave(Scenes.OutGame); }); break;
                case "dmgstats": b.onClick.AddListener(() => { Snd.Ui(); ShowDamageStats(sum); }); break;
            }
        }

        static void Leave(string scene) { RoundFlow.AfterSettle(); App.Go(scene); }

        // "데미지 통계" 버튼: 균사 트리로 버튼을 복제해 그 옆에 둔다 (액션 dmgstats → ShowDamageStats)
        GameObject statsButton;
        void EnsureStatsButton()
        {
            if (statsButton != null) return;
            var toTree = toTreeText.GetComponentInParent<UIAction>();
            if (toTree == null) return;
            statsButton = Instantiate(toTree.gameObject, toTree.transform.parent);
            statsButton.name = "DmgStats";
            statsButton.transform.SetSiblingIndex(toTree.transform.GetSiblingIndex());   // 균사 트리로 버튼 왼쪽
            statsButton.GetComponent<UIAction>().act = "dmgstats";
            Wire(statsButton.GetComponent<UIAction>());
            // secondary action: the wooden button with ink text, so "to the tree" stays the one green primary
            var wood = SpriteDB.Get("UI/btn_wood");
            var bi = statsButton.GetComponent<Image>(); if (wood != null && bi != null) bi.sprite = wood;
            var t = statsButton.GetComponentInChildren<TMP_Text>();
            t.color = new Color(0.29f, 0.23f, 0.16f);
            var lt = t.GetComponent<LocText>(); if (lt != null) lt.key = "데미지 통계";
            t.text = Loc.T("데미지 통계");
        }

        public void Show(RoundSim.Summary s)
        {
            sum = s; t0 = Time.unscaledTime; rolledSound = false; animating = true;
            // 제목: 끝까지 했으면 "수확 완료!", "라운드 끝내고 정산하기"로 중간에 끝냈으면 "수확 중단" (씬 Card/Title)
            if (title == null) title = transform.Find("Card/Title")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = s.aborted ? Loc.T("수확 중단") : Loc.T("수확 완료!");
                // the cream lettering is toned down to warm brown so it reads on the cream card
                var li = Lettering.Apply(title, s.aborted ? "settle_stop" : "settle_done", 1.5f);
                if (li != null) li.color = LetterOnCard;
            }
            for (int i = 0; i < 3; i++) gainTexts[i].text = Loc.F("+{0}개", U.Fmt(s.gains[CAT_KEYS[i]]));
            scoreLabel.text = Loc.F("얻은 점수 → 골드 <size=70%><color=#8a6a4a>(×{0})</color></size>", U.FmtN(s.goldMul));
            bonusRow.SetActive(s.bonusGold > 0);
            bonusLabel.text = Loc.T("보너스 골드 <size=70%><color=#8a6a4a>(엽전 수확기)</color></size>");
            bonusValue.text = "+" + U.Fmt(s.bonusGold);
            meta.text = Loc.F("수확 {0}번", U.Fmt(s.harvests));
            themeBox.SetActive(s.newTheme != null);
            if (s.newTheme != null) { var th = THEME[s.newTheme]; themeText.text = Loc.F("{0} 새 지역 열림: <color=#2a6ab8>{1}</color>", UIUtil.Ic(th.icon), th.n) + "\n" + Loc.F("<size=55%><color=#4a6a8a>{0} · 다음 라운드부터 이 지역으로 가요</color></size>", th.d); }

            Clear(unlockList); Clear(newsList);
            foreach (var id in s.unlocks) Card(unlockCards, unlockList, SpriteDB.Single(id), 0, SP[id].n, Loc.T("해금"), "unlock");
            unlockBox.SetActive(unlockList.Count > 0);
            foreach (var id in s.newSpecies) Card(newsCards, newsList, SpriteDB.Single(id), 0, SP[id].n, Loc.T("NEW"), "");
            foreach (var id in s.newGolden) Card(newsCards, newsList, SpriteDB.Single(id), 3, Loc.F("황금 {0}", SP[id].n), Loc.T("NEW"), "gold");
            foreach (var (id, st) in s.newStars) Card(newsCards, newsList, SpriteDB.Single(id), 0, SP[id].n, Loc.T("NEW"), "star");
            newsBox.SetActive(newsList.Count > 0);
            var recs = s.recs.Select(r => Loc.F("신기록! {0} <color=#3b2414>{1}</color>", RECORD_NAMES[r.k], U.Fmt(r.v)) + (r.old > 0 ? " <size=70%><color=#8a6a4a>" + Loc.F("(이전 {0})", U.Fmt(r.old)) + "</color></size>" : "")).ToList();
            var bossLine = BossLine(s);
            if (bossLine != null) recs.Insert(0, bossLine);
            if (s.goldHv) recs.Insert(0, Loc.F("{0} 황금 수확기 해금!", UIUtil.Ic("medal")) + (HvOn("gold") ? "" : " " + Loc.T("(장착 칸이 차서 꺼 두었어요)")));
            recsBox.SetActive(recs.Count > 0);
            recsText.text = string.Join("\n", recs);
            toTreeText.text = Loc.T("균사 트리로 ▶");
            EnsureStatsButton();
            SpawnBasket(s);
            animationEnd = Mathf.Max(1.9f, drops.Count == 0 ? 0 : drops[drops.Count - 1].delay + 0.6f);
            FitCards();
            FitToScreen();
        }

        static string BossLine(RoundSim.Summary s)
        {
            if (s.bossId != null)
            {
                var b = BOSS[s.bossId];
                return s.bossCleared
                    ? Loc.F("{0} 보스 {1} 처치! 스테이지 {2} 열림", UIUtil.Ic(BOSS_ICON), b.n, s.nextStage) + "\n<size=80%>" +
                      Loc.F("처치 보상: {0}+{1} · {2}+{3} · 버섯 +{4}개", UIUtil.Ic("gold"), U.Fmt(s.bossGold), UIUtil.Ic("gem"), s.bossGem, U.Fmt(s.bossShrooms)) + "</size>"
                    : $"<color=#d04a3a>{Loc.F("보스 {0}에게 졌어요… 골드 −{1} (−{2}%)", b.n, U.Fmt(s.bossPenalty), Mathf.RoundToInt((float)(1 - BOSS_FAIL_GOLD) * 100))}</color>\n<size=80%>" +
                      Loc.T("보스 도전이 꺼졌어요. 이 스테이지에서 파밍하다가 균사 트리의 보스 도전 버튼으로 다시 도전하세요") + "</size>";
            }
            if (s.aborted) return Loc.T("수확이 도중에 중단되어 스테이지가 완료되지 않습니다.");
            if (IsBossStage(s.stage)) return Loc.F("스테이지 {0} 보스가 기다려요 (보스 도전 꺼짐 · 파밍 중)", s.stage);
            return s.nextStage > s.stage ? Loc.F("스테이지 {0} 완료 → 스테이지 {1}", s.stage, s.nextStage) : null;
        }

        // 줄 수를 다 줄여도 화면을 넘으면(기록·지역 알림이 많을 때 등) 비율을 유지한 채 축소한다.
        void FitToScreen()
        {
            var host = (RectTransform)transform;
            card.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            float availH = host.rect.height - SCREEN_MARGIN * 2, availW = host.rect.width - SCREEN_MARGIN * 2;
            if (availH <= 0 || availW <= 0) return;
            float k = Mathf.Min(1f, availH / card.rect.height, availW / card.rect.width);
            card.localScale = new Vector3(k, k, 1f);
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
            more.label.text = Loc.F("+{0}개", list.Count - shown);
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

        static readonly Color LetterOnCard = new Color(0.72f, 0.5f, 0.32f);


        void SpawnBasket(RoundSim.Summary s)
        {
            foreach (var d in drops) if (d.rt != null) Destroy(d.rt.gameObject);
            drops.Clear();
            var pool = new List<string>();
            foreach (var c in CAT_KEYS) { int n = Mathf.Min(14, Mathf.CeilToInt(Mathf.Sqrt((float)s.gains[c]))); for (int i = 0; i < n; i++) pool.Add(c); }
            pool = pool.OrderBy(_ => Random.value).ToList();
            var harvested = new Dictionary<string, List<Species>>();
            var fallback = new Dictionary<string, Species>();
            foreach (var c in CAT_KEYS) harvested[c] = new List<Species>();
            foreach (var sp in SPECIES)
            {
                if (!fallback.ContainsKey(sp.c)) fallback[sp.c] = sp;
                if (Harvested(sp.id)) harvested[sp.c].Add(sp);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                string c = pool[i];
                var got = harvested[c];
                var sp = got.Count > 0 ? U.Pick(got) : fallback[c];
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
            if (sum == null || !animating) return;
            float el = Time.unscaledTime - t0;
            // 바구니에 버섯이 후두둑 (cubic-bezier 오버슈트 근사)
            foreach (var d in drops)
            {
                float k = Mathf.Clamp01((el - d.delay) / 0.6f);
                float e = 1 - Mathf.Pow(1 - k, 3) + Mathf.Sin(k * Mathf.PI) * 0.15f;
                d.rt.anchoredPosition = new Vector2(d.rt.anchoredPosition.x, Mathf.Lerp(40, -d.y, e));
                UIUtil.Show(d.rt, el >= d.delay);
            }
            // 롤링 카운터
            float Ease(float delay) { float k = Mathf.Clamp01((el * 1000 - delay) / 1100); return 1 - Mathf.Pow(1 - k, 3); }
            scoreValue.text = "+" + U.Fmt(sum.scoreGold * Ease(0));
            saleValue.text = "~" + U.Fmt(sum.value * Ease(400));
            float e3 = Ease(800);
            goldValue.text = $"{UIUtil.Ic("gold")}{U.Fmt(sum.net * e3)}";
            if (e3 >= 1 && !rolledSound) { rolledSound = true; Snd.Buy(); }
            if (el >= animationEnd) animating = false;
        }

        // ===== 데미지 통계 (예전 RuntimeOverlays) — 출처(직접 타격·스킬·능력)별 · 수확기별. 아무 데나 누르면 닫힌다 =====
        static GameObject dmgStats;
        static void ShowDamageStats(RoundSim.Summary s)
        {
            if (dmgStats != null || s?.dmgBySrc == null) return;
            var go = new GameObject("DmgStatsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 1000;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            dmgStats = go;
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(go.transform, false);
            var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            dim.GetComponent<Button>().onClick.AddListener(() => { Snd.Ui(); Destroy(dmgStats); dmgStats = null; });
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(dim.transform, false);
            card.GetComponent<Image>().color = U.Hex("#fff6e4");
            ((RectTransform)card.transform).sizeDelta = new Vector2(1200, 900);
            var tg = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tg.transform.SetParent(card.transform, false);
            var t = tg.GetComponent<TextMeshProUGUI>();
            LocFonts.Apply(t, LocFonts.Role.Title, -1);
            t.text = DamageStatsText(s); t.fontSize = 30; t.color = U.Hex("#4a3420"); t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft; t.enableWordWrapping = false;
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(48, 40); rt.offsetMax = new Vector2(-48, -40);
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 30;
        }

        static string DamageStatsText(RoundSim.Summary s)
        {
            double total = 0; foreach (var v in s.dmgBySrc.Values) total += v.dmg;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=140%><b>{Loc.T("데미지 통계")}</b></size>   <color=#8a6a4a>{Loc.F("총 피해 {0}", U.Fmt(total))}</color>\n\n");
            void Table(string title, Dictionary<string, DmgStat> d, System.Func<string, string> name)
            {
                sb.Append($"<b>{title}</b>\n<color=#8a6a4a><size=85%>{Loc.T("이름")}<pos=45%>{Loc.T("피해 비율")}<pos=62%>{Loc.T("피해")}<pos=78%>{Loc.T("타격")}<pos=89%>{Loc.T("처치")}</size></color>\n");
                foreach (var kv in d.OrderByDescending(x => x.Value.dmg))
                {
                    double pct = total > 0 ? kv.Value.dmg / total * 100 : 0;
                    sb.Append($"{name(kv.Key)}<pos=45%>{pct.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%<pos=62%>{U.Fmt(kv.Value.dmg)}<pos=78%>{kv.Value.hits}<pos=89%>{kv.Value.kills}\n");
                }
                sb.Append('\n');
            }
            Table(Loc.T("스킬·능력별"), s.dmgBySrc, RoundSim.DmgSrcName);
            Table(Loc.T("수확기별"), s.dmgByHv, RoundSim.DmgHvName);
            sb.Append($"<size=80%><color=#8a6a4a>{Loc.T("화면을 누르면 닫혀요")}</color></size>");
            return sb.ToString();
        }
    }
}
