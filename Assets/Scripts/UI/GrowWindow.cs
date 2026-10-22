using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 세계수 성장 창 (2026-10-20 시안 concept/ui_mock_grow.png). 프리팹 Resources/UI/GrowWindow:
    //   Box / Title · Exit
    //   TreeCard (Pic/Tree · Lv/Text) — 지금 단계의 세계수 그림 (진화할 때마다 그 단계 그림으로 바뀐다)
    //   Effects (Title · Text) — 현재 효과
    //   Evolve (Icon · Title · Text · Bar(Fill·Label) · Button)
    //   Row_Ring · Row_Tribute · Row_Bless (각각 Icon · Title · Sub · Bar · Text · Button)
    // 규칙은 Game.Farm (RingUp · Evolve · TributeAll · BLESS). 가호 [확인] = 단계별 가호 목록 팝업.
    public class GrowWindow
    {
        public readonly GameObject root;
        readonly System.Action changed;
        readonly FarmListPopup popup;
        FarmView farm;
        readonly Image tree; readonly TMP_Text lv, effects;
        readonly Transform evo, ring, tribute, bless;

        public bool IsOpen => root != null && root.activeSelf;

        public GrowWindow(GameObject root, FarmListPopup popup, System.Action changed)
        {
            this.root = root; this.popup = popup; this.changed = changed;
            if (root == null) return;
            var r = root.transform;
            SceneUI.Bind(SceneUI.Find(r, "Exit"), () => { Snd.Ui(); Close(); });
            tree = FarmWin.Img(SceneUI.Find(r, "TreeCard"), "Tree");
            lv = FarmWin.Text(SceneUI.Find(SceneUI.Find(r, "TreeCard"), "Lv"), "Text");
            effects = FarmWin.Text(SceneUI.Find(r, "Effects"), "Text");
            evo = SceneUI.Find(r, "Evolve"); ring = SceneUI.Find(r, "Row_Ring"); tribute = SceneUI.Find(r, "Row_Tribute"); bless = SceneUI.Find(r, "Row_Bless");
            SceneUI.Bind(SceneUI.Find(evo, "Button"), DoEvolve);
            SceneUI.Bind(SceneUI.Find(ring, "Button"), DoRing);
            SceneUI.Bind(SceneUI.Find(tribute, "Button"), DoTribute);
            SceneUI.Bind(SceneUI.Find(bless, "Button"), () => { Snd.Ui(); OpenBless(); });
        }

        public void SetFarm(FarmView f) => farm = f;
        public void Open() { if (root == null) return; root.SetActive(true); root.transform.SetAsLastSibling(); Render(); }
        public void Close() { if (!IsOpen) return; popup?.Close(); root.SetActive(false); changed?.Invoke(); }
        public void Refresh() { if (IsOpen) Render(); }
        void After() { Render(); farm?.Sync(false); changed?.Invoke(); }

        static void Row(Transform t, string title, string sub, float bar, string barLabel, string text, string btn, bool on, string sprite)
        {
            if (t == null) return;
            UIUtil.SetText(FarmWin.Text(t, "Title"), title);
            var s = SceneUI.Find(t, "Sub"); UIUtil.Show(s?.gameObject, sub != null); if (sub != null) UIUtil.SetText(SceneUI.TextIn(s), sub);
            var b = SceneUI.Find(t, "Bar"); UIUtil.Show(b?.gameObject, bar >= 0); if (bar >= 0) FarmWin.Bar(b, bar, barLabel);
            UIUtil.SetText(FarmWin.Text(t, "Text"), text);
            FarmWin.Btn(SceneUI.Find(t, "Button")?.GetComponent<Button>(), btn, on, sprite);
        }

        void Render()
        {
            if (G?.farm == null) return;
            int s = Stage();
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(root.transform, "Title"), "Text"), Loc.T("세계수 성장"));
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(root.transform, "Effects"), "Title"), Loc.T("현재 효과"));
            if (tree != null) { tree.sprite = SpriteDB.Get("Farm/Tree/tree_" + WT.Picture(s)); tree.preserveAspect = true; }
            UIUtil.SetText(lv, Loc.F("{0}단계 {1}", s, WT.StageName(s)));

            var rate = BagRate();
            string got = s >= 1 && s - 1 < BLESS.Length ? BLESS[s - 1].text : "";
            UIUtil.SetText(effects, Loc.F("나무버섯 성장 +{0}", U.Pct(WT.ringGrow * (Ring() - 1)))
                + "\n" + Loc.F("시간당 {0}  {1}", FarmWin.Gold(rate.gold * 3600), FarmWin.Gem(rate.gem * 3600))
                + "\n" + Loc.F("나무버섯 슬롯 {0}개 · 장식 {1}칸", MushSlotCount(), DecoSlotCount())
                + (got.Length > 0 ? "\n" + FarmWin.Col(got, FarmWin.GREEN) : ""));

            // 진화
            if (!StageMax())
            {
                var why = CanEvolve();
                var join = s < CRITTER_JOIN.Length ? SPC[CRITTER_JOIN[s]] : null;
                string reward = Loc.F("슬롯 {0}→{1} · 장식 {2}→{3}", WT.mushSlots[s - 1], WT.mushSlots[s], WT.decoSlots[s - 1], WT.decoSlots[s]) + (join != null ? " · " + Loc.F("{0} 해금", join.n) : "");
                string cost = FarmWin.Gold(WT.evoGold[s]) + (WT.evoGem[s] > 0 ? "  " + FarmWin.Gem(WT.evoGem[s]) : "");
                Row(evo, Loc.F("세계수 진화 → {0}단계", s + 1), null, (float)(TreeExp() / System.Math.Max(1, TreeExpNeed())), Loc.F("경험치 {0}/{1}", U.Fmt(System.Math.Floor(TreeExp())), U.Fmt(TreeExpNeed())),
                    reward + "\n" + cost,
                    why == GrowFail.None ? Loc.T("진화!") : why == GrowFail.Ring ? Loc.T("나이테 부족") : why == GrowFail.Exp ? Loc.T("경험치 부족") : why == GrowFail.Gold ? Loc.T("골드 부족") : Loc.T("균사석 부족"),
                    why == GrowFail.None, why == GrowFail.None ? "btn_leaf" : "btn_cream");
            }
            else Row(evo, Loc.T("아르카나 세계수"), null, -1, null, Loc.T("최종 단계"), Loc.T("최대"), false, "btn_cream");

            double rc = RingCost();
            Row(ring, Loc.T("나이테"), Loc.F("Lv. {0} / {1}", Ring(), WT.ringMax), (float)Ring() / WT.ringMax, null,
                Loc.F("나무버섯 성장 +{0}", U.Pct(WT.ringGrow * (Ring() - 1))) + (rc > 0 ? FarmWin.Col("  → +" + U.Pct(WT.ringGrow * Ring()), FarmWin.GREEN) : ""),
                rc > 0 ? Loc.T("성장") + "\n<size=75%>" + FarmWin.Gold(rc) + "</size>" : Loc.T("최대"), rc > 0 && G.gold >= rc, "btn_leaf");

            double tv = StageMax() ? 0 : TributeAllValue();
            string pref = StageMax() ? "" : string.Join(" · ", WT_PREFER[s - 1].Select(n => SP_BY_NAME[n]).Select(sp => sp.n));
            Row(tribute, Loc.T("공물"), null, -1, null,
                Loc.F("창고 버섯 {0}개 → 경험치 +{1}", U.Fmt(StoreTotal()), U.Fmt(System.Math.Floor(tv))) + (pref.Length > 0 ? "\n" + FarmWin.Col(Loc.F("선호 ×2: {0}", pref), FarmWin.GRAY) : ""),
                Loc.T("바치기"), tv > 0, "btn_cream");

            string nextBless = s < BLESS.Length ? Loc.F("다음 {0}단계: {1}", s + 1, BLESS[s].text) : Loc.T("모든 가호를 받았어요");
            Row(bless, Loc.T("세계수의 가호"), null, -1, null,
                Loc.F("받은 가호 {0}/{1}", System.Math.Min(s, BLESS.Length), BLESS.Length) + "\n" + FarmWin.Col(nextBless, FarmWin.GRAY), Loc.T("확인"), true, "btn_cream");
        }

        void OpenBless()
        {
            popup?.Open(Loc.T("세계수의 가호"), "ic_blessing", p =>
            {
                int s = Stage();
                for (int i = 0; i < BLESS.Length; i++)
                {
                    bool got = i < s;
                    p.Row().Pic(SpriteDB.Get("Farm/Tree/tree_" + WT.Picture(i + 1)), !got)
                        .Text(Loc.F("{0}단계 {1}", i + 1, WT.StageName(i + 1)), got ? FarmWin.Col(BLESS[i].text, FarmWin.GREEN) : FarmWin.Col(BLESS[i].text, FarmWin.GRAY),
                              got ? FarmWin.Col(Loc.T("라운드 영구 적용"), FarmWin.GRAY) : FarmWin.Col(Loc.F("세계수 {0}단계에 받아요", i + 1), FarmWin.GRAY));
                }
            });
        }

        void DoRing()
        {
            if (!RingUp()) { Snd.Err(); App.Notify(Loc.T("골드 부족")); return; }
            Snd.Buy(); App.Notify(Loc.F("나이테 Lv.{0}", Ring())); After();
        }

        void DoEvolve()
        {
            var why = CanEvolve();
            if (!Evolve(out var opened))
            {
                Snd.Err();
                App.Notify(why == GrowFail.Ring ? Loc.F("나이테 Lv.{0} 필요", WT.ringMax) : why == GrowFail.Exp ? Loc.T("경험치 부족") : why == GrowFail.Gem ? Loc.T("균사석 부족") : Loc.T("골드 부족"));
                return;
            }
            Snd.Record();
            farm?.TreeGrew(Loc.F("{0}단계 {1}!", Stage(), WT.StageName(Stage())));
            string msg = Loc.F("세계수 {0}단계 {1}! · 가호: {2}", Stage(), WT.StageName(Stage()), BLESS[Stage() - 1].text);
            if (opened != null) foreach (var k in opened) msg += "\n" + Loc.F("{0} 합류 가능", k.n);
            App.Notify(msg);
            After();
        }

        void DoTribute()
        {
            double exp = TributeAll();
            if (exp <= 0) { Snd.Err(); App.Notify(Loc.T("바칠 버섯 없음")); return; }
            Snd.Record();
            farm?.TreeGrew(Loc.F("경험치 +{0}", U.Fmt(System.Math.Floor(exp))));
            App.Notify(Loc.F("공물 · 경험치 +{0}", U.Fmt(System.Math.Floor(exp))) + (CanEvolve() == GrowFail.None ? "\n" + Loc.T("진화 가능!") : ""));
            After();
        }
    }
}
