using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 꼬마 창 "꼬마의 하루" (2026-10-20 시안 concept/ui_mock_kids.png). 프리팹 Resources/UI/KidWindow:
    //   Box / Title · Exit · Card_Aff (Pic · Info · Hearts/Heart0~4 · Button) · Card_Grade (Pic · Info · Stars · Button)
    //   Box / ReqBox (Title · Pic · Bubble/Text · Button) · EvoBox (Title · Icon · Text · Button)
    // 카드 두 장은 시안 그림 그대로(새싹·이슬, 2026-10-20 사용자): 새싹 = 꼬마들 호감도 요약 [호감도] → 호감도 팝업,
    // 이슬 = 꼬마들 별 등급 요약 [등급] → 등급 팝업. 합류(꼬마 종류) 팝업은 아래 꼬마 칸에서 아직 없는 꼬마를 누르면 연다.
    // 꼬마의 부탁 → 일일·주간 부탁 팝업, 꼬마 진화 → 진화 팝업 (Lv업·진화 조건·균사석). 팝업은 FarmListPopup 하나를 돌려 쓴다.
    public class KidWindow
    {
        public readonly GameObject root;
        readonly System.Action changed;
        readonly FarmListPopup popup;
        FarmView farm;
        readonly Transform cardAff, cardGrade;
        readonly TMP_Text reqText, evoText;

        public bool IsOpen => root != null && root.activeSelf;

        public KidWindow(GameObject root, FarmListPopup popup, System.Action changed)
        {
            this.root = root; this.popup = popup; this.changed = changed;
            if (root == null) return;
            SceneUI.Bind(SceneUI.Find(root.transform, "Exit"), () => { Snd.Ui(); Close(); });
            cardAff = SceneUI.Need(root.transform, "Card_Aff");
            cardGrade = SceneUI.Need(root.transform, "Card_Grade");
            SceneUI.Bind(SceneUI.Find(cardAff, "Button"), () => { Snd.Ui(); OpenAffection(); });
            SceneUI.Bind(SceneUI.Find(cardGrade, "Button"), () => { Snd.Ui(); OpenGrade(); });
            var req = SceneUI.Find(root.transform, "ReqBox");
            reqText = FarmWin.Text(SceneUI.Find(req, "Bubble"), "Text");
            SceneUI.Bind(SceneUI.Find(req, "Button"), () => { Snd.Ui(); OpenRequests(); });
            var evo = SceneUI.Find(root.transform, "EvoBox");
            evoText = FarmWin.Text(evo, "Text");
            SceneUI.Bind(SceneUI.Find(evo, "Button"), () => { Snd.Ui(); OpenEvolve(); });
        }

        public void SetFarm(FarmView f) => farm = f;

        public void Open()
        {
            if (root == null) return;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Render();
        }
        public void Close() { if (!IsOpen) return; popup?.Close(); root.SetActive(false); changed?.Invoke(); }
        public void Refresh() { if (IsOpen) Render(); if (popup != null && popup.IsOpen) popup.Refresh(); }

        void After() { Render(); popup?.Refresh(); farm?.Sync(false); changed?.Invoke(); }

        // 데려온 꼬마 → 합류할 수 있는 꼬마 → 아직 잠긴 꼬마 (같은 무리는 합류 순서)
        static List<Kid> Ordered() => CRITTER_JOIN.Select(id => KID[id])
            .OrderBy(k => HasSpecial(k.id) ? 0 : KidOpen(k.id) ? 1 : 2).ToList();

        void Render()
        {
            if (G?.farm == null) return;
            // 프리팹에 박힌 글자도 지금 언어로
            var r0 = root.transform;
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(r0, "Title"), "Text"), Loc.T("꼬마의 하루"));
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(r0, "ReqBox"), "Title"), Loc.T("꼬마의 부탁"));
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(SceneUI.Find(r0, "ReqBox"), "Button"), "Label"), Loc.T("부탁 보기") + "  >");
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(r0, "EvoBox"), "Title"), Loc.T("꼬마 진화"));
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(SceneUI.Find(r0, "EvoBox"), "Button"), "Label"), Loc.T("진화") + "  >");
            var own = KidsOwned();
            int n = own.Count, affSum = own.Sum(k => AffLv(k.id)), starSum = own.Sum(k => Awaken(k.id));
            // 새싹 카드: 꼬마 수 · 호감도 합 · 하트 = 평균 호감도
            UIUtil.SetText(FarmWin.Text(cardAff, "Info"), Loc.F("꼬마 {0}명", n) + "  |  " + Loc.F("호감도 {0}", affSum));
            FarmWin.Hearts(SceneUI.Find(cardAff, "Hearts"), n > 0 ? Mathf.RoundToInt((float)affSum / n) : 0);
            FarmWin.Btn(SceneUI.Find(cardAff, "Button")?.GetComponent<Button>(), Loc.T("호감도"), true, "btn_leaf");
            // 이슬 카드: 별 합 · 별 = 평균 등급
            UIUtil.SetText(FarmWin.Text(cardGrade, "Info"), Loc.F("별 {0}/{1}", starSum, n * KLV.awakenMax));
            int avg = n > 0 ? Mathf.RoundToInt((float)starSum / n) : 0;
            UIUtil.SetText(FarmWin.Text(cardGrade, "Stars"), new string('★', avg) + FarmWin.Col(new string('★', KLV.awakenMax - avg), "#d9c9b0"));
            FarmWin.Btn(SceneUI.Find(cardGrade, "Button")?.GetComponent<Button>(), Loc.T("등급"), true, "btn_cream");

            UIUtil.SetText(reqText, Loc.T("매일 세 번, 함께하는 작은 일을 부탁할게요!"));
            int can = own.Count(k => CanAwaken(k.id) == KidFail.None);
            UIUtil.SetText(evoText, can > 0 ? Loc.F("지금 진화할 수 있는 꼬마 {0}", can) : Loc.T("한 단계 더 자라요!"));
        }

        // ===== 등급: 꼬마마다 별(진화 단계)·Lv·지금 등급 효과. 진화할 수 있으면 [진화 >] → 진화 팝업 =====
        public void OpenGrade()
        {
            popup?.Open(Loc.T("꼬마 등급"), "ic_evolve", p =>
            {
                foreach (var k in Ordered())
                {
                    string id = k.id; bool own = HasSpecial(id); int aw = Awaken(id);
                    string stars = new string('★', own ? aw : 0) + FarmWin.Col(new string('★', KLV.awakenMax - (own ? aw : 0)), "#d9c9b0");
                    var row = p.Row().Pic(UIUtil.KidPortrait(id), !own);
                    if (!own) { row.Text((KidOpen(id) ? SPC[id].n : "???") + "  " + stars, FarmWin.Col(Loc.T("아직 합류 전"), FarmWin.GRAY)); continue; }
                    row.Text(SPC[id].n + "  " + FarmWin.Col(stars, "#e8a900"),
                             Loc.F("등급 ★{0}/{1} · Lv.{2}/{3}", aw, KLV.awakenMax, KLv(id), KLV.Cap(aw)),
                             FarmWin.Col(KidStep(id) > 0 ? KidBonusText(k, KidStep(id)) : k.traitText, FarmWin.GRAY));
                    if (aw < KLV.awakenMax) row.A(Loc.T("진화") + "  >", true, () => { Snd.Ui(); OpenEvolve(); }, CanAwaken(id) == KidFail.None ? "btn_leaf" : "btn_cream");
                }
            }, Render);
        }

        // ===== 호감도: 데려온 꼬마마다 하트·다음 Lv까지 남은 양·올리는 법 + 선물 =====
        public void OpenAffection(string focus = null)
        {
            popup?.Open(Loc.T("꼬마 호감도"), "ic_affection", p =>
            {
                var own = KidsOwned();
                if (own.Count == 0) { p.Empty(Loc.T("아직 데려온 꼬마가 없어요")); return; }
                if (focus != null) own = own.OrderBy(k => k.id == focus ? 0 : 1).ToList();
                foreach (var k in own)
                {
                    var a = AffOf(k.id); string id = k.id;
                    bool max = a.lv >= AFF.max;
                    var fav = string.Join(" · ", k.fav.Select(n => SP_BY_NAME[n].n));
                    p.Row().Pic(UIUtil.KidPortrait(id))
                        .Text(SPC[id].n + "  " + FarmWin.Col(new string('★', Awaken(id)), "#e8a900"),
                              max ? FarmWin.Col(Loc.T("호감도 최대!"), FarmWin.GREEN) : Loc.F("호감도 Lv.{0} → Lv.{1}까지 {2} 남음", a.lv, a.lv + 1, U.Fmt(System.Math.Ceiling(a.need - a.cur))),
                              FarmWin.Col(Loc.F("좋아하는 버섯: {0} · 부탁 +{1}", fav, REQ.aff), FarmWin.GRAY))
                        .Hearts(a.lv)
                        .Bar(max ? 1 : (float)(a.cur / System.Math.Max(1, a.need)), max ? "MAX" : U.Fmt(System.Math.Floor(a.cur)) + " / " + U.Fmt(a.need))
                        .A(Loc.T("선물"), G.farm.store.Count > 0 && !max, () => Gift(id));
                }
            }, Render);
        }

        void Gift(string id)
        {
            // 좋아하는 버섯을 모두, 없으면 창고에 가장 많은 버섯 10개 (세계수 탭 창 선물과 같은 규칙)
            var k = KID[id]; string sp = null; int n = 0;
            foreach (var name in k.fav) { var sid = SP_BY_NAME[name].id; if (StoreOf(sid) > n) { sp = sid; n = StoreOf(sid); } }
            if (sp == null) { foreach (var kv in G.farm.store) if (kv.Value > n) { sp = kv.Key; n = kv.Value; } n = System.Math.Min(n, 10); }
            int up = sp == null ? -1 : Game.Gift(id, sp, n);
            if (up < 0) { Snd.Err(); App.Notify(Loc.T("선물할 버섯 없음")); return; }
            Snd.Buy();
            farm?.Cheer(id, IsFav(id, sp) ? Loc.T("이거 제일 좋아해! 고마워!") : U.Pick(FARM_LINES["thanks"]), up > 0 ? Loc.F("호감도 Lv.{0}!", AffLv(id)) : null);
            App.Notify(Loc.F("{0}에게 {1} ×{2}", SPC[id].n, SP[sp].n, n) + (up > 0 ? " · " + Loc.F("호감도 Lv.{0}!", AffLv(id)) : ""));
            After();
        }

        // ===== 합류: 꼬마 종류 (함께하는 꼬마 · 데려올 수 있는 꼬마 · 아직 못 만난 꼬마) =====
        public void OpenJoin(string focus = null)
        {
            popup?.Open(Loc.T("꼬마 친구들"), "ic_join", p =>
            {
                foreach (var k in Ordered().OrderBy(k => k.id == focus ? 0 : 1))
                {
                    string id = k.id; bool own = HasSpecial(id), open = KidOpen(id);
                    var row = p.Row().Pic(UIUtil.KidPortrait(id), !own && !open);
                    if (own) row.Text(SPC[id].n, FarmWin.Col(Loc.T("함께하는 중"), FarmWin.GREEN), FarmWin.Col(k.traitText, FarmWin.GRAY));
                    else if (open)
                    {
                        var f = CanUnlockKid(id);
                        row.Text(SPC[id].n, Loc.F("데려오기 {0}", FarmWin.Gem(UnlockGem(id))), FarmWin.Col(k.traitText, FarmWin.GRAY))
                           .A(f == KidFail.Gem ? Loc.T("균사석 부족") : Loc.T("합류"), f == KidFail.None, () => Unlock(id));
                    }
                    else row.Text("???", FarmWin.Col(Loc.F("세계수 {0}단계 {1}가 되면 만나요", JoinLv(id), WT.StageName(JoinLv(id))), FarmWin.GRAY));
                }
            }, Render);
        }

        void Unlock(string id)
        {
            if (!UnlockKid(id)) { Snd.Err(); App.Notify(Loc.T("균사석 부족")); return; }
            Snd.Record(); App.Notify(Loc.F("{0} 합류!", SPC[id].n));
            After();
        }

        // ===== 부탁: 일일 3개 + 주간 상자 =====
        public void OpenRequests()
        {
            popup?.Open(Loc.T("꼬마의 부탁"), "ic_request", p =>
            {
                ReqEnsure();
                p.Section(Loc.T("오늘의 부탁"));
                for (int i = 0; i < G.farm.reqs.Count; i++)
                {
                    var r = G.farm.reqs[i]; var sp = SP[r.sp]; int have = System.Math.Min(StoreOf(r.sp), r.n); int k = i;
                    p.Row().Pic(UIUtil.KidPortrait(r.kid))
                        .Text(SPC[r.kid].n, Loc.F("{0} ×{1} 모아 주기", sp.n, r.n), FarmWin.Col(Loc.F("보상 {0} · 호감도 +{1}", FarmWin.Gold(ReqGold()), REQ.aff), FarmWin.GRAY))
                        .Bar(r.done ? 1 : (float)have / System.Math.Max(1, r.n), r.done ? Loc.T("완료!") : have + "/" + r.n)
                        .A(r.done ? Loc.T("완료!") : Loc.T("들어주기"), !r.done && ReqReady(r), () => DoReq(k));
                }
                if (G.farm.reqs.Count == 0) p.Row().Text(Loc.T("아직 부탁 없음"), "");
                p.Section(Loc.T("이번 주 부탁"));
                int wd = System.Math.Min(G.farm.weekDone, REQ.weekGoal);
                p.Row().Pic(FarmWin.Ui("ic_request"))
                    .Text(Loc.T("꼬마의 상자"), Loc.F("부탁 {0}번 들어주면 {1}", REQ.weekGoal, FarmWin.Gem(REQ.weekGem)), FarmWin.Col(Loc.T("매주 월요일에 새로 시작해요"), FarmWin.GRAY))
                    .Bar((float)wd / REQ.weekGoal, Loc.F("이번 주 {0}/{1}", wd, REQ.weekGoal))
                    .A(G.farm.weekClaimed ? Loc.T("받음") : Loc.T("받기"), !G.farm.weekClaimed && WeekReady(), DoWeek);
            }, Render);
        }

        void DoReq(int i)
        {
            var gold = ReqDo(i);
            if (gold == null) { Snd.Err(); App.Notify(Loc.T("버섯 부족")); return; }
            Snd.Record();
            farm?.Cheer(G.farm.reqs[i].kid, U.Pick(FARM_LINES["thanks"]), null);
            App.Notify(Loc.F("부탁 완료 · {0}+{1}", UIUtil.Ic("gold"), U.Fmt(gold.Value)));
            After();
        }

        void DoWeek()
        {
            if (!WeekClaim()) { Snd.Err(); return; }
            Snd.Record(); App.Notify(Loc.F("꼬마의 상자! {0}+{1}", UIUtil.Ic("gem"), REQ.weekGem)); After();
        }

        // ===== 진화: 지금 모습 → 다음 모습, 조건(Lv·세계수 단계)과 균사석. Lv이 모자라면 Lv업 버튼 =====
        public void OpenEvolve()
        {
            popup?.Open(Loc.T("꼬마 진화"), "ic_evolve", p =>
            {
                var own = KidsOwned().OrderBy(k => CanAwaken(k.id) == KidFail.None ? 0 : Awaken(k.id) >= KLV.awakenMax ? 2 : 1).ToList();
                if (own.Count == 0) { p.Empty(Loc.T("아직 데려온 꼬마가 없어요")); return; }
                foreach (var k in own)
                {
                    string id = k.id; int aw = Awaken(id), lv = KLv(id);
                    var row = p.Row().Pic(UIUtil.KidPortrait(id));
                    if (aw >= KLV.awakenMax)
                    {
                        row.Text(SPC[id].n + "  " + FarmWin.Col(new string('★', aw), "#e8a900"), FarmWin.Col(Loc.T("마지막 진화까지 마쳤어요"), FarmWin.GREEN), FarmWin.Col(KidBonusText(k, KidStep(id)), FarmWin.GRAY));
                        continue;
                    }
                    var next = SpriteDB.Get("Characters/Evo/evo_" + id + "_" + (aw + 1));
                    int needLv = 10 * (aw + 1), needStage = AwakenStage(id);
                    var why = CanAwaken(id);
                    // 조건마다 채웠으면 초록, 모자라면 빨강 (기호 글자는 폰트에 없을 수 있어 색으로만)
                    string ok(bool b, string text) => FarmWin.Col(text, b ? FarmWin.GREEN : FarmWin.RED);
                    row.Pic2(next)
                       .Text(SPC[id].n + "  " + FarmWin.Col(new string('★', aw) + " > " + new string('★', aw + 1), "#e8a900"),
                             ok(lv >= needLv, Loc.F("Lv.{0}/{1}", lv, needLv)) + "   " + ok(Stage() >= needStage, Loc.F("세계수 {0}단계", needStage)) + "   " + ok(G.gem >= KLV.awakenGem[aw], FarmWin.Gem(KLV.awakenGem[aw])),
                             FarmWin.Col(Loc.T("진화하면") + " " + KidBonusText(k, KidStep(id) + 1), FarmWin.GRAY))
                       .A(why == KidFail.None ? Loc.T("진화!") : why == KidFail.Gem ? Loc.T("균사석 부족") : why == KidFail.Stage ? Loc.F("{0}단계 필요", needStage) : Loc.F("Lv.{0} 필요", needLv), why == KidFail.None, () => Awaken_(id));
                    var lf = CanKidLvUp(id);
                    if (lf != KidFail.Max && lf != KidFail.Cap) row.B(Loc.F("Lv업 {0}", FarmWin.Gold(KLvCost(id))), lf == KidFail.None, () => LvUp(id));
                }
            }, Render);
        }

        void LvUp(string id)
        {
            if (!KidLvUp(id)) { Snd.Err(); App.Notify(CanKidLvUp(id) == KidFail.Cap ? Loc.T("진화 필요") : Loc.T("골드 부족")); return; }
            Snd.Buy(); farm?.Cheer(id, Loc.T("힘이 솟아나!"), $"Lv.{KLv(id)}");
            After();
        }

        void Awaken_(string id)
        {
            if (!KidAwaken(id)) { Snd.Err(); return; }
            Snd.Record();
            farm?.Cheer(id, Loc.T("반짝반짝… 진화했어!"), new string('★', Awaken(id)));
            App.Notify(Loc.F("{0} 진화 ★{1}! {2}", SPC[id].n, Awaken(id), KidBonusText(KID[id], KidStep(id))));
            After();
        }
    }
}
