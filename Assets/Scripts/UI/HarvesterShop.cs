using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 수확기 상점(공방) 팝업. 배치한 카드 Harvesters (1)~(8)이 HARVESTERS 순서대로 수확기 하나씩을 맡는다.
    // 카드 구성 (이름으로 찾음): Image(아이콘) · Panel(이름) · Panel (1)(설명) · Button_Buy · Button_Upg
    //   Button_Buy: 미보유면 구매, 보유하면 장착 켜기/끄기
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

            public Card(HarvesterShop shop, Transform t, string id)
            {
                this.id = id;
                FixSquashedLayout(t as RectTransform);
                SquareBoxes(t);
                icon = SceneUI.Need(t, "Image")?.GetComponent<Image>();
                title = SceneUI.TextIn(SceneUI.Need(t, "Panel"));
                desc = SceneUI.TextIn(SceneUI.Need(t, "Panel (1)"));
                var b = SceneUI.Need(t, "Button_Buy");
                var u = SceneUI.Need(t, "Button_Upg");
                buy = SceneUI.Bind(b, () => shop.Buy(id));
                upg = SceneUI.Bind(u, () => shop.Upgrade(id));
                buyText = SceneUI.TextIn(b);
                upgText = SceneUI.TextIn(u);
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

            // 설명 바탕(초록)과 해금·★ 버튼을 둥근 그림 대신 테두리 있는 네모로
            static readonly Color DescBg = new Color(0.80f, 0.89f, 0.72f), DescLine = new Color(0.42f, 0.58f, 0.36f),
                                  BuyBg = new Color(0.47f, 0.69f, 0.36f), UpgBg = new Color(0.87f, 0.73f, 0.58f), BtnLine = new Color(0.45f, 0.30f, 0.18f);
            static void SquareBoxes(Transform card)
            {
                Square(card.Find("Panel (1)"), DescBg, DescLine);
                Square(card.Find("Button_Buy"), BuyBg, BtnLine);
                Square(card.Find("Button_Upg"), UpgBg, BtnLine);
            }

            static void Square(Transform t, Color bg, Color line)
            {
                if (t == null || !t.TryGetComponent<Image>(out var im)) return;
                im.sprite = null; im.type = Image.Type.Simple; im.color = bg;
                if (!t.TryGetComponent<Outline>(out var o)) o = t.gameObject.AddComponent<Outline>();
                o.effectColor = line; o.effectDistance = new Vector2(3, -3);
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
                    icon.color = S > 0 ? Color.white : new Color(1, 1, 1, 0.55f);
                }
                // 이름 칸은 짧아서 이름만. 별·미보유는 설명 첫 줄에
                UIUtil.SetText(title, h.n);

                // 설명만 (능력 이름 "황금 손길: " 같은 앞머리는 뺀다)
                string trait = (S > 0 ? $"<color=#e8a900>{U.Stars(S)}</color>" : Loc.T("<color=#8a6a4a>미보유</color>")) + "\n" + DescOnly(h.text(System.Math.Max(1, S)));
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

                // 강화
                var lc = S > 0 ? HvLevelCost(h) : null;
                if (upg != null) upg.interactable = lc != null && CanAfford(lc.Value);
                // 미보유면 다음 별만 보여 준다 (번역이 필요 없는 표시, 버튼은 꺼짐)
                UIUtil.SetText(upgText, S == 0 ? "★2" : lc != null ? $"★{S + 1} {UIUtil.Ic("gold")}{U.Fmt(lc.Value.gold)}" : Loc.T("★ 최대"));
            }
        }
    }
}
