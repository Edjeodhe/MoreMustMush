using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 수확기 상점(공방) 팝업. 배치한 카드 Harvesters (1)~(8)이 HARVESTERS 순서대로 수확기 하나씩을 맡는다.
    // 카드 구성 (이름으로 찾음): Image(아이콘) · Stars(Star1~5, 그림과 설명 사이) · Panel(이름) · Panel (1)(설명) · Button_Buy · Button_Upg
    //   Stars: 강화 단계(★)만큼 별 그림을 앞에서부터 켠다 (가운데 정렬). 없는 카드는 설명 첫 줄에 ★☆ 글자로
    //   Button_Buy: 미보유면 구매, 보유하면 장착 켜기/끄기. 켜져 있으면 노란 버튼(UI/btn_active)
    //   Button_Upg: 다음 ★ 강화
    // 다시 그리기는 팝업을 열 때 · 버튼을 누른 뒤 · 언어를 바꿀 때 · 열려 있는 동안 재화가 바뀔 때만 (MainGameController가 부른다).
    public class HarvesterShop
    {
        public const string CARD_PREFIX = "Harvesters (";

        public readonly GameObject root;
        readonly Card[] cards;
        readonly System.Action changed;   // 재화가 바뀌었다 (상단 재화 표시 갱신)

        public HarvesterShop(GameObject root, System.Action onChanged)
        {
            this.root = root;
            changed = onChanged;
            int n = HARVESTERS.Count;
            cards = new Card[n];
            for (int i = 0; i < n; i++)
            {
                var t = SceneUI.Need(root.transform, CARD_PREFIX + (i + 1) + ")");
                cards[i] = t != null ? new Card(this, t, HARVESTERS[i].id) : null;
            }
            // 카드가 수확기보다 많으면 남는 카드는 숨긴다
            for (int i = n + 1; ; i++)
            {
                var extra = SceneUI.Find(root.transform, CARD_PREFIX + i + ")");
                if (extra == null) break;
                extra.gameObject.SetActive(false);
            }
        }

        public bool IsOpen => root != null && root.activeSelf;

        public void Render()
        {
            if (G == null || !IsOpen) return;
            foreach (var c in cards) c?.Render();
        }

        void Buy(string id)
        {
            if (HvStar(id) == 0)
            {
                if (!HvUnlock(id)) { Snd.Err(); return; }
                Snd.Buy();
                App.Notify(HvOn(id) ? Loc.F("{0} 해금! 빈 장착 칸에 들어갔어요", HV[id].n) : Loc.F("{0} 해금! 장착 칸이 모자라 꺼 두었어요", HV[id].n));
            }
            else
            {
                if (!HvToggle(id)) { Snd.Err(); App.Notify(Loc.F("장착 칸이 다 찼어요 ({0}/{1}). 다른 수확기를 먼저 꺼 주세요", OnHvs().Count, HvSlots())); return; }
                Snd.Ui();
                if (HvOver()) App.Notify(Loc.T(RoundFlow.HV_OVER_MSG));
            }
            AfterChange();
        }

        void Upgrade(string id)
        {
            if (!HvLevel(id)) { Snd.Err(); return; }
            Snd.Buy();
            App.Notify(HvStar(id) >= 5 ? Loc.F("{0} {1} 최대! 날이 늘었어요", HV[id].n, U.Stars(HvStar(id))) : $"{HV[id].n} {U.Stars(HvStar(id))}");
            AfterChange();
        }

        void AfterChange() { Render(); changed?.Invoke(); }

        // ===== 카드 하나 =====
        class Card
        {
            readonly string id;
            readonly Image icon;
            readonly TMP_Text title, desc, buyText, upgText;
            readonly Button buy, upg;
            readonly GameObject[] stars;   // Stars/Star1~5 (배치 전이면 빈 배열)
            readonly Image buyImg;          // 켜진 수확기면 노란 btn_active, 아니면 배치한 그림
            readonly Sprite buySprite;
            readonly Color buyTextColor;
            static readonly Color ACTIVE_TEXT = new Color(0.40f, 0.26f, 0.04f);   // 노란 버튼 위 진한 갈색 글자

            public Card(HarvesterShop shop, Transform t, string id)
            {
                this.id = id;
                FixSquashedLayout(t as RectTransform);   // 설명 박스·버튼 그림은 씬에 배치한 그대로 쓴다 (예전엔 사각형으로 바꿨다)
                icon = SceneUI.Need(t, "Image")?.GetComponent<Image>();
                title = SceneUI.TextIn(SceneUI.Need(t, "Panel"));
                desc = SceneUI.TextIn(SceneUI.Need(t, "Panel (1)"));
                var b = SceneUI.Need(t, "Button_Buy");
                var u = SceneUI.Need(t, "Button_Upg");
                buy = SceneUI.Bind(b, () => shop.Buy(id));
                upg = SceneUI.Bind(u, () => shop.Upgrade(id));
                buyText = SceneUI.TextIn(b);
                upgText = SceneUI.TextIn(u);
                buyImg = b != null ? b.GetComponent<Image>() : null;
                buySprite = buyImg != null ? buyImg.sprite : null;
                buyTextColor = buyText != null ? buyText.color : Color.black;
                var st = t.Find("Stars");
                var list = new System.Collections.Generic.List<GameObject>();
                if (st != null) foreach (Transform s in st) list.Add(s.gameObject);
                stars = list.ToArray();
            }

            // 씬의 카드는 가로 배율(0.34)로 줄여 배치돼 있어 글자가 납작하고, 이름 나무바(Panel)는 배율 2.37로 옆 카드까지 넘친다.
            // 카드 배율을 1로 펴고 같은 화면 크기로 자식을 다시 놓는다 (씬을 고쳐 배율이 1이면 아무것도 안 한다)
            static void FixSquashedLayout(RectTransform card)
            {
                if (card == null || card.localScale.x > 0.99f) return;
                float k = card.localScale.x;
                card.localScale = Vector3.one;
                card.sizeDelta = new Vector2(card.sizeDelta.x * k, card.sizeDelta.y);
                float w = card.sizeDelta.x;   // 화면 폭 (약 313)

                var icon = card.Find("Image") as RectTransform;
                if (icon != null) { icon.localScale = Vector3.one; icon.anchoredPosition = new Vector2(icon.anchoredPosition.x * k, icon.anchoredPosition.y); icon.sizeDelta = new Vector2(110, 110); }

                var bar = card.Find("Panel") as RectTransform;
                if (bar != null)
                {
                    bar.localScale = Vector3.one;
                    bar.anchoredPosition = new Vector2(0, bar.anchoredPosition.y);
                    bar.sizeDelta = new Vector2(-16, bar.sizeDelta.y);   // 카드 폭 − 16
                    FitText(SceneUI.TextIn(bar), new Vector2(w - 50, 50), 18, 34);
                }

                var desc = card.Find("Panel (1)") as RectTransform;
                if (desc != null)
                {
                    desc.anchoredPosition = new Vector2(0, desc.anchoredPosition.y);
                    desc.sizeDelta = new Vector2(-20, desc.sizeDelta.y);
                    FitText(SceneUI.TextIn(desc), new Vector2(w - 44, desc.rect.height - 8), 14, 22);
                }

                foreach (var (name, x) in new[] { ("Button_Buy", -72f), ("Button_Upg", 72f) })
                {
                    var b = card.Find(name) as RectTransform;
                    if (b == null) continue;
                    b.anchoredPosition = new Vector2(x, b.anchoredPosition.y);
                    b.sizeDelta = new Vector2(132, 58);
                    var bt = SceneUI.TextIn(b);
                    if (bt != null) { bt.enableAutoSizing = true; bt.fontSizeMin = 12; bt.fontSizeMax = 22; }
                }
            }

            // "이름: 설명" → "설명" (콜론이 없으면 그대로)
            static string DescOnly(string s) { int c = s.IndexOf(": "); return c >= 0 ? s.Substring(c + 2) : s; }

            static void FitText(TMP_Text t, Vector2 size, float min, float max)
            {
                if (t == null) return;
                t.rectTransform.sizeDelta = size;
                t.enableAutoSizing = true; t.fontSizeMin = min; t.fontSizeMax = max;
            }

            public void Render()
            {
                var h = HV[id];
                int S = HvStar(id);
                bool on = HvOn(id);

                if (icon != null)
                {
                    // 장착한 스킨이 있으면 그 그림 (스킨을 바꾸면 다음에 그릴 때 따라온다)
                    var skin = HvSkinOn(id);
                    icon.sprite = (skin != null ? SpriteDB.Get("Harvesters/" + skin.id) : null) ?? SpriteDB.Get("Harvesters/" + id) ?? SpriteDB.Get("Harvesters/" + BallView.HvLook(id));
                    icon.preserveAspect = true;   // 스킨 그림은 정사각이 아닌 것도 있다 (0.86~1.12)
                    icon.color = S > 0 ? Color.white : new Color(1, 1, 1, 0.55f);
                }
                // 이름 칸은 짧아서 이름만. 별·미보유는 설명 첫 줄에
                UIUtil.SetText(title, h.n);

                // 별 그림: 강화 단계만큼 켠다
                for (int i = 0; i < stars.Length; i++) stars[i].SetActive(i < S);

                // 설명만 (능력 이름 "황금 손길: " 같은 앞머리는 뺀다). 별 그림이 있으면 별 글자 줄은 뺀다
                string head = S == 0 ? Loc.T("<color=#8a6a4a>미보유</color>") : stars.Length == 0 ? $"<color=#e8a900>{U.Stars(S)}</color>" : null;
                string trait = (head != null ? head + "\n" : "") + DescOnly(h.text(System.Math.Max(1, S)));
                if (S > 0 && S < 5) trait += "\n" + Loc.F("다음 ★{0}: {1}", S + 1, DescOnly(h.text(S + 1)));
                UIUtil.SetText(desc, trait);

                // 구매 / 장착
                if (S == 0)
                {
                    bool reward = h.unlockGold < 0;   // 공방에서 살 수 없는 보상 수확기
                    if (buy != null) buy.interactable = !reward && G.gold >= h.unlockGold;
                    UIUtil.SetText(buyText, reward ? Loc.F("황금 도감 15종 달성 보상 ({0}/15)", GoldenCount()) : Loc.F("해금 {0}{1}", UIUtil.Ic("gold"), U.Fmt(h.unlockGold)));
                }
                else
                {
                    bool canOn = on || HvCanTurnOn();   // 칸이 다 찼으면 꺼진 수확기는 켤 수 없다
                    if (buy != null) buy.interactable = canOn;
                    UIUtil.SetText(buyText, on ? Loc.T("켜짐") : canOn ? Loc.T("꺼짐") : Loc.T("칸 없음"));
                }
                // 켜진(장착한) 수확기 = 노란 버튼
                bool active = S > 0 && on;
                var activeSprite = active ? SpriteDB.Get("UI/btn_active") : null;
                if (buyImg != null) buyImg.sprite = activeSprite != null ? activeSprite : buySprite;
                if (buyText != null) buyText.color = activeSprite != null ? ACTIVE_TEXT : buyTextColor;

                // 강화
                var lc = S > 0 ? HvLevelCost(h) : null;
                if (upg != null) upg.interactable = lc != null && CanAfford(lc.Value);
                // 미보유면 다음 별만 보여 준다 (번역이 필요 없는 표시, 버튼은 꺼짐)
                UIUtil.SetText(upgText, S == 0 ? "★2" : lc != null ? $"★{S + 1} {UIUtil.Ic("gold")}{U.Fmt(lc.Value.gold)}" : Loc.T("★ 최대"));
            }
        }
    }
}
