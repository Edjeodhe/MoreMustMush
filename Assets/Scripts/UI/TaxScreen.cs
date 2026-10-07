using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Tax bill (prototype showTaxBill / taxPay / taxSkip result).
    public class TaxScreen : MonoBehaviour
    {
        public TMP_Text sub, billLabel, billValue, haveValue, invLabel, payNote, skipNote, resultText, resultMeta;
        public GameObject billGroup, invRow, resultGroup, unlockBox;
        public Button payButton;
        public RectTransform unlockCards;
        [AssetPath("Assets/Prefabs/UI/NewCard.prefab")] public NewCard cardPrefab;


        void Head(int cycle, double income) => sub.text = $"사이클 {cycle} · {TAX.every}라운드 동안 번 골드 <color=#c99a00>{U.Fmt(income)}</color>";

        public void ShowBill()
        {
            var T = G.tax; double bill = TaxBill(); bool canPay = G.gold >= bill;
            double rate = TaxRate(T.unpaid), penalty = System.Math.Ceiling(T.income * rate);
            var nu = NextTaxUnlock();
            Head(T.cycle, T.income);
            billGroup.SetActive(true); resultGroup.SetActive(false);
            billLabel.text = $"납부할 세금 <size=55%><color=#8a6a4a>(수익의 {Mathf.RoundToInt(TAX.share * 100)}% · 최소 {U.Fmt(TaxAmount(T.cycle))}{(SetDone("tax") ? " · 성실 납세자 −20%" : "")})</color></size>";
            billValue.text = $"{UIUtil.Ic("gold")}{U.Fmt(bill)}";
            haveValue.text = U.Fmt(G.gold);
            haveValue.color = canPay ? U.Hex("#3b2414") : U.Hex("#d23a2a");
            invRow.SetActive(InvTotal() > 0);
            invLabel.text = $"창고 버섯 {U.Fmt(InvTotal())}개 <size=55%><color=#8a6a4a>(≈{U.Fmt(InvValue())}골드 · 판매는 2단계 버섯 상점에서)</color></size>";
            payButton.interactable = canPay;
            payNote.text = $"−{U.Fmt(bill)}골드 · 납부 {T.paid + 1}회째" + (nu != null ? $"\n{UIUtil.Ic("tax")} 세금 {nu.tax}회 납부 시 <color=#c0392b>{(nu.tax == T.paid + 1 ? nu.n : "새 버섯")}</color> 해금" : "");
            skipNote.text = $"이번 사이클 수익의 <color=#c0392b>{Mathf.RoundToInt((float)rate * 100)}%</color> (−{U.Fmt(penalty)}골드) 차감\n미납 {T.unpaid + 1}회째 · 다음 미납은 {Mathf.RoundToInt((float)TaxRate(T.unpaid + 1) * 100)}%" + (penalty > G.gold ? "\n골드가 모자란 만큼은 체납금으로 다음 수익에서 떼어 가요" : "");
        }

        public void ShowPaid((int cycle, double income) head, List<string> unlocks)
        {
            Head(head.cycle, head.income);
            billGroup.SetActive(false); resultGroup.SetActive(true);
            resultText.text = $"<color=#3a8a2a>납부 완료!</color> <size=55%><color=#8a6a4a>(누적 {G.tax.paid}회)</color></size>";
            for (int i = unlockCards.childCount - 1; i >= 0; i--) Destroy(unlockCards.GetChild(i).gameObject);
            foreach (var id in unlocks) { var c = Instantiate(cardPrefab, unlockCards); c.gameObject.SetActive(true); c.Set(SpriteDB.Single(id), 0, SP[id].n, "해금", "unlock"); }
            unlockBox.SetActive(unlocks.Count > 0);
            resultMeta.text = $"다음 세금: 사이클 {G.tax.cycle} · {TAX.every}라운드 뒤 · 그 사이클 수익의 {Mathf.RoundToInt(TAX.share * 100)}% (최소 {U.Fmt(TaxAmount(G.tax.cycle))})";
        }

        public void ShowSkipped((int cycle, double income) head, double rate, double penalty, double take)
        {
            Head(head.cycle, head.income);
            billGroup.SetActive(false); resultGroup.SetActive(true);
            unlockBox.SetActive(false);
            resultText.text = $"<color=#c0392b>미납… 수익의 {Mathf.RoundToInt((float)rate * 100)}% (−{U.Fmt(penalty)}골드)를 떼어 갔어요</color>";
            resultMeta.text = (penalty > take ? $"모자란 {U.Fmt(penalty - take)}골드는 체납금으로 다음 라운드 수익에서 자동 차감돼요\n" : "")
                + $"다음 미납 시 {Mathf.RoundToInt((float)TaxRate(G.tax.unpaid) * 100)}% 차감 · 다음 세금: 사이클 {G.tax.cycle} (최소 {U.Fmt(TaxAmount(G.tax.cycle))})";
        }
    }
}
