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

                string trait = (S > 0 ? $"<color=#e8a900>{U.Stars(S)}</color>" : Loc.T("<color=#8a6a4a>미보유</color>")) + "\n" + h.text(System.Math.Max(1, S));
                if (S > 0 && S < 5)
                {
                    string t = h.text(S + 1); int c = t.IndexOf(": ");
                    trait += "\n" + Loc.F("다음 ★{0}: {1}", S + 1, c >= 0 ? t.Substring(c + 2) : t);
                }
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
