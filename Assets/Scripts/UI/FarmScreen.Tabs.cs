using System;
using System.Linq;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 세계수 화면 오른쪽 패널의 탭 내용. 버튼은 GameFlow의 wt* 액션으로 간다.
    public partial class FarmScreen
    {
        const string BG = "#fffaf0", BG_ON = "#e6f6dc", BG_OFF = "#efe6d4", BG_GOLD = "#fff3c8";
        static string G_(double v) => UIUtil.Ic("gold") + U.Fmt(v);
        static string Gem(double v) => UIUtil.Ic("gem") + U.Fmt(v);
        static string Gray(string s) => "<color=#8a6a4a>" + s + "</color>";
        static string Small(string s) => "<size=85%>" + s + "</size>";

        void RenderTab()
        {
            switch (tab)
            {
                case "kid": TabKid(); break;
                case "req": TabReq(); break;
                case "craft": TabCraft(); break;
                case "deco": TabDeco(); break;
                default: TabGrow(); break;
            }
            EndList();
        }

        // ===== 성장: 나이테 · 진화 · 공물 · 가호 =====
        void TabGrow()
        {
            int s = Stage();
            BeginList(Loc.F("성장 · {0}단계 {1}", s, WT.StageName(s)));
            var tree = SpriteDB.Get("Farm/Tree/tree_" + WT.Picture(s));

            double rc = RingCost();
            Line(Loc.F("<b>나이테 Lv.{0}/{1}</b>", Ring(), WT.ringMax) + "\n" + Small(Loc.F("나무버섯 성장 속도 +{0} (레벨마다 +{1})", U.Pct(WT.ringGrow * (Ring() - 1)), U.Pct(WT.ringGrow))),
                tree, BG, rc > 0 ? (Loc.F("성장 {0}", G_(rc)), "wtring", (string)null, G.gold >= rc) : (Loc.T("최대"), "none", (string)null, false));

            if (!StageMax())
            {
                var why = CanEvolve();
                string need = Loc.F("경험치 {0}/{1}", U.Fmt(Math.Floor(TreeExp())), U.Fmt(TreeExpNeed())) + " · " + Loc.F("나이테 Lv.{0}", WT.ringMax) + " · " + G_(WT.evoGold[s]) + (WT.evoGem[s] > 0 ? " · " + Gem(WT.evoGem[s]) : "");
                var join = s < CRITTER_JOIN.Length ? SPC[CRITTER_JOIN[s]] : null;
                string reward = Loc.F("나무버섯 슬롯 {0} → {1} · 장식 슬롯 {2} → {3}", WT.mushSlots[s - 1], WT.mushSlots[s], WT.decoSlots[s - 1], WT.decoSlots[s])
                    + (join != null ? "\n" + Loc.F("{0} 합류 · 장식 레시피 3종", join.n) : "") + "\n" + Loc.F("가호: {0}", BLESS[s].text);
                Line(Loc.F("<b>진화 → {0}단계 {1}</b>", s + 1, WT.StageName(s + 1)) + "\n" + Small(need) + "\n" + Small(Gray(reward)), null, why == GrowFail.None ? BG_GOLD : BG,
                    (why == GrowFail.None ? Loc.T("진화!") : why == GrowFail.Ring ? Loc.T("나이테 부족") : why == GrowFail.Exp ? Loc.T("경험치 부족") : why == GrowFail.Gold ? Loc.T("골드 부족") : Loc.T("균사석 부족"), "wtevolve", (string)null, why == GrowFail.None));

                double tv = TributeAllValue();
                string pref = string.Join(" · ", WT_PREFER[s - 1].Select(n => SP_BY_NAME[n]).Select(sp => sp.n + " " + Gray("(" + StoreOf(sp.id) + ")")));
                Line(Loc.F("<b>공물</b> · 창고 버섯 {0}개 → 경험치 +{1}", U.Fmt(StoreTotal()), U.Fmt(Math.Floor(tv))) + "\n" + Small(Loc.F("이 단계가 원하는 버섯 (공물 ×2): {0}", pref))
                    + "\n" + Small(Gray(Loc.T("창고 버섯은 라운드에서 딴 버섯이에요. 꼬마 선물·부탁에도 써요"))),
                    null, BG, (Loc.T("모두 바치기"), "wttribute", (string)null, tv > 0));
            }
            else Line(Loc.T("<b>아르카나 세계수</b> · 가장 큰 나무예요"), null, BG_GOLD);

            var zs = ZoneSlots(); var rate = BagRate();
            Line(Loc.F("<b>나무버섯 슬롯 {0}개</b>", MushSlotCount()) + "\n" + Small(string.Join(" · ", ZONES.Select(z => ZoneOpen(z.id) ? z.n + " " + zs[z.id] : Gray(Loc.F("{0} ({1}단계)", z.n, z.open)))))
                + "\n" + Small(Loc.F("시간당 {0} {1} · 보따리 최대 {2}", G_(rate.gold * 3600), Gem(rate.gem * 3600), TimeText(BagCapSec()))), null, BG);

            string bl = string.Join("\n", BLESS.Select((b, i) => (i < s ? "<color=#2a9a74>● " : "<color=#a89a86>· ") + Loc.F("{0}단계 {1}", i + 1, WT.StageName(i + 1)) + ": " + b.text + "</color>"));
            Line(Loc.T("<b>세계수의 가호</b> (라운드에 영구 적용)") + "\n" + Small(bl), null, BG);
        }

        // ===== 꼬마: 레벨 · 각성 · 구역 · 선물 =====
        void TabKid()
        {
            BeginList(Loc.F("꼬마 {0}/{1} · Lv 합계 {2}", KidsOwned().Count, KIDS.Length, KLvSum()));
            int cap = ZoneCapacity();
            Line(Loc.T("<b>구역 배치</b>") + "\n" + Small(string.Join(" · ", ZONES.Select(z => ZoneOpen(z.id) ? Loc.F("{0} {1}/{2}", z.n, KidsIn(z.id).Count, cap) : Gray(Loc.F("{0} ({1}단계)", z.n, z.open)))))
                + "\n" + Small(Gray(Loc.T("구역 버튼으로 꼬마를 보내요. 선호 구역이면 수확량 +50%. 구역에 꼬마가 없으면 그 구역 버섯은 따지 않아요"))), null, BG);
            int next = KLV.sumAt.FirstOrDefault(a => a > KLvSum());
            if (next > 0) Line(Loc.F("Lv 합계 {0} 달성 보상: 장식 슬롯 +1", next) + (next == KLV.sumAt[1] || next == KLV.sumAt[3] ? " · " + Loc.T("구역 정원 +1") : next == KLV.sumAt[4] ? " · " + Loc.T("칭호 '꼬마 대장'") : ""), null, BG_OFF);
            else Line(Loc.T("칭호 <b>'꼬마 대장'</b> · Lv 합계 보상을 모두 받았어요"), null, BG_GOLD);

            foreach (var k in KIDS)
            {
                var sp = SPC[k.id];
                var icon = SpriteDB.Get("Characters/Critters/" + k.id + "_ref");
                if (!HasSpecial(k.id))
                {
                    Line(Gray(Loc.F("<b>{0}</b> · 세계수 {1}단계에 합류", sp.n, JoinLv(k.id))) + "\n" + Small(Gray(k.traitText)), icon, BG_OFF);
                    continue;
                }
                int lv = KLv(k.id), aw = Awaken(k.id), lvCap = KLV.Cap(aw);
                var aff = AffOf(k.id); string zone = ZoneOf(k.id);
                double mul = zone != null ? KidMul(k.id, zone) : 0;
                int step = KidStep(k.id);
                string text = Loc.F("<b>{0}</b> <color=#e8a900>{1}</color> Lv.{2}/{3}", sp.n, new string('★', aw), lv, lvCap)
                    + "  " + Small(Loc.F("호감도 Lv.{0}", aff.lv) + (aff.lv < AFF.max ? Gray($" {U.Fmt(Math.Floor(aff.cur))}/{U.Fmt(aff.need)}") : ""))
                    + "\n" + Small(Loc.F("구역: {0} (선호 {1})", zone != null ? ZONE[zone].n : Loc.T("쉬는 중"), ZONE[k.zone].n) + (zone != null ? " · " + Loc.F("수확 ×{0}", U.FmtN(mul)) : ""))
                    + "\n" + Small(Gray(Loc.F("특성: {0}", k.traitText)))
                    + "\n" + Small(step > 0 ? "<color=#2a9a74>" + KidBonusText(k, step) + "</color>" : Gray(Loc.T("특별 보너스: 각성하거나 장식 세트를 완성하면 생겨요")))
                    + "\n" + Small(Gray(Loc.F("좋아하는 버섯: {0} ({1})", string.Join(" · ", k.fav.Select(n => SP_BY_NAME[n].n)), k.favWhere)));
                double lc = KLvCost(k.id); var lf = CanKidLvUp(k.id); var af = CanAwaken(k.id);
                string lvLabel = lf == KidFail.Max ? Loc.T("Lv 최대") : lf == KidFail.Cap ? Loc.T("각성 필요") : Loc.F("Lv업 {0}", G_(lc));
                string awLabel = aw >= KLV.awakenMax ? Loc.T("각성 최대") : af == KidFail.Lv ? Loc.F("각성 Lv.{0}", 10 * (aw + 1)) : Loc.F("각성 {0}", Gem(KLV.awakenGem[aw]));
                bool hasGift = G.farm.store.Count > 0;
                Line(text, icon, k.id == selKid ? BG_ON : BG,
                    (lvLabel, "wtkidlv", k.id, lf == KidFail.None),
                    (awLabel, "wtawaken", k.id, af == KidFail.None),
                    (Loc.F("구역: {0}", zone != null ? ZONE[zone].n : Loc.T("쉬는 중")), "wtzone", k.id, true),
                    (Loc.T("선물"), "wtgift", k.id, hasGift));
            }
        }

        // ===== 부탁: 일일 부탁 3개 · 주간 상자 =====
        void TabReq()
        {
            ReqEnsure();
            BeginList(Loc.T("꼬마의 부탁"));
            if (G.farm.reqs.Count == 0) Line(Loc.T("라운드에서 버섯을 따 오면 꼬마들이 부탁을 해요"), null, BG_OFF);
            for (int i = 0; i < G.farm.reqs.Count; i++)
            {
                var r = G.farm.reqs[i]; var sp = SP[r.sp]; int have = StoreOf(r.sp);
                string text = Loc.F("<b>{0}</b>: \"{1} {2}개 갖다 줘!\"", SPC[r.kid].n, sp.n, r.n) + "\n" + Small(Loc.F("창고 {0}/{1}", Math.Min(have, r.n), r.n) + " · " + Loc.F("보상 {0} · 호감도 +{1}", G_(ReqGold()), REQ.aff));
                Line(text, SpriteDB.Single(sp.id), r.done ? BG_OFF : ReqReady(r) ? BG_GOLD : BG,
                    r.done ? (Loc.T("완료!"), "none", (string)null, false) : (Loc.T("들어주기"), "wtreq", i.ToString(), ReqReady(r)));
            }
            Line(Loc.F("<b>꼬마의 상자</b> · 이번 주 부탁 {0}/{1}", Math.Min(G.farm.weekDone, REQ.weekGoal), REQ.weekGoal) + "\n" + Small(Loc.F("보상 {0}", Gem(REQ.weekGem))), null, WeekReady() ? BG_GOLD : BG,
                G.farm.weekClaimed ? (Loc.T("받음"), "none", (string)null, false) : (Loc.T("받기"), "wtweek", (string)null, WeekReady()));
            Line(Small(Gray(Loc.T("부탁은 매일 3개씩 새로 와요. 좋아하는 버섯은 지역·날씨에 따라 잘 나와요"))), null, BG_OFF);
        }

        // ===== 공방: 제작 중 · 레시피 =====
        void TabCraft()
        {
            if (!CraftOpen())
            {
                BeginList(Loc.T("꼬마 공방"));
                Line(Loc.F("도감 {0}/{1}종을 모으면 꼬마 공방이 열려요", CodexCount(), WT.codexForCraft) + "\n" + Small(Gray(Loc.T("공방에서는 세계수를 꾸미는 장식을 만들어요"))), SpriteDB.Icon("book"), BG_OFF);
                return;
            }
            int slots = WT.CraftSlots(Stage());
            BeginList(Loc.F("꼬마 공방 · 제작 {0}/{1} · 즉시 완료 오늘 {2}회", G.farm.crafts.Count, slots, RushLeftToday()));
            for (int i = 0; i < G.farm.crafts.Count; i++)
            {
                var c = G.farm.crafts[i]; var d = DECO[c.deco]; bool done = CraftDone(c);
                string text = Loc.F("<b>{0}</b> {1}", d.n, done ? "<color=#2a9a74>" + Loc.T("완성!") + "</color>" : Loc.F("제작 중 {0}", Mmss((long)Math.Ceiling((c.at - Now()) / 1000))));
                Line(text, SpriteDB.Get("Farm/Props/" + d.art), done ? BG_GOLD : BG,
                    done ? (Loc.T("받기"), "wtclaim", i.ToString(), true) : (Loc.F("즉시 {0}", Gem(RushCost(c))), "wtrush", i.ToString(), RushLeftToday() > 0 && G.gem >= RushCost(c)));
            }
            foreach (var k in KIDS)
            {
                if (!HasSpecial(k.id)) { Line(Gray(Loc.F("{0} 합류 시 레시피 3종", SPC[k.id].n)), null, BG_OFF); continue; }
                foreach (var d in DECOS.Where(x => x.of == k.id))
                {
                    var g = DECO_GRADES[d.grade]; var f = CanCraft(d.id);
                    string own = DecoOwned(d.id) ? Loc.F("보유 ★{0} · 재료 {1}", DecoStar(d.id), DecoDup(d.id)) : Loc.T("미보유");
                    string text = Loc.F("<b>{0}</b> {1}", d.n, Small(Gray(g.n + " · " + (d.hang ? Loc.T("걸이형") : Loc.T("설치형")) + " · " + own)))
                        + "\n" + Small(DecoFxText(d)) + "\n" + Small(Gray(G_(g.gold) + " " + Gem(g.gem) + " · " + TimeText(CraftSeconds(d)) + " · " + Loc.F("아름다움 {0}", g.beauty)));
                    Line(text, SpriteDB.Get("Farm/Props/" + d.art), BG,
                        (f == CraftFail.Slots ? Loc.T("슬롯 없음") : f == CraftFail.Gold ? Loc.T("골드 부족") : f == CraftFail.Gem ? Loc.T("균사석 부족") : Loc.T("제작"), "wtcraft", d.id, f == CraftFail.None));
                }
            }
        }

        // ===== 꾸미기: 아름다움 · 장식 슬롯 · 강화 · 세트 =====
        void TabDeco()
        {
            double b = Beauty();
            BeginList(Loc.F("꾸미기 · 아름다움 {0}점", U.Fmt(b)));
            string steps = string.Join("\n", BEAUTY.Select(s => (b >= s.at ? "<color=#2a9a74>● " : "<color=#a89a86>· ") + Loc.F("{0}점: {1}", s.at, s.text) + "</color>"));
            Line(Loc.T("<b>아름다움 점수</b> (가진 장식 전체)") + "\n" + Small(steps), null, BG);

            DecoSlotsEnsure();
            int n = DecoSlotCount();
            for (int i = 0; i < n; i++)
            {
                string id = G.farm.decoSlots[i]; var d = id != null ? DECO[id] : null;
                bool hang = DecoSlotHang(i);
                bool any = DECOS.Any(x => x.hang == hang && DecoOwned(x.id));
                string text = Loc.F("<b>슬롯 {0}</b> {1}", i + 1, Small(Gray(hang ? Loc.T("걸이형 · 가지·수관") : Loc.T("설치형 · 뿌리 주변"))))
                    + "\n" + (d != null ? Loc.F("{0} ★{1}", d.n, DecoStar(id)) + "\n" + Small("<color=#2a9a74>" + DecoFxText(d, DecoMul(id)) + "</color>") : Small(Gray(any ? Loc.T("비어 있어요 · 바꾸기로 장식을 놓아요") : Loc.T("놓을 수 있는 장식이 없어요 (공방에서 만들어요)"))));
                var place = (Loc.T("바꾸기 ▶"), "wtplace", i.ToString(), any);
                if (d != null) Line(text, SpriteDB.Get("Farm/Props/" + d.art), i == selDeco ? BG_ON : BG, place, (Loc.T("비우기"), "wtunplace", i.ToString(), true));
                else Line(text, null, i == selDeco ? BG_ON : BG_OFF, place);   // 빈 슬롯엔 비우기 버튼이 없다
            }

            string sets = string.Join("\n", KIDS.Where(k => HasSpecial(k.id)).Select(k =>
            {
                int placed = DECOS.Count(d => d.of == k.id && DecoPlaced(d.id));
                return (placed >= 3 ? "<color=#2a9a74>● " : "<color=#8a6a4a>· ") + Loc.F("{0} 세트 {1}/3", SPC[k.id].n, placed) + "</color>";
            }));
            Line(Loc.F("<b>세트 효과</b> · 같은 꼬마 장식 3종을 모두 놓으면 그 꼬마 수확량 +{0} · 특별 보너스 +1단계", U.Pct(DECO_SET_YIELD)) + (sets.Length > 0 ? "\n" + Small(sets) : ""), null, BG);

            foreach (var d in DECOS.Where(x => DecoOwned(x.id)))
            {
                var c = EnhanceCost(d.id); var f = CanEnhance(d.id);
                string text = Loc.F("<b>{0}</b> <color=#e8a900>{1}</color>", d.n, new string('★', DecoStar(d.id))) + " " + Small(Gray(Loc.F("재료 {0}", DecoDup(d.id)) + (DecoPlaced(d.id) ? " · " + Loc.T("배치 중") : "")))
                    + "\n" + Small(DecoFxText(d, DecoMul(d.id))) + (c != null ? "\n" + Small(Gray(Loc.F("강화: 같은 장식 1 · {0}", G_(c.Value.gold) + (c.Value.gem > 0 ? " " + Gem(c.Value.gem) : "")))) : "");
                Line(text, SpriteDB.Get("Farm/Props/" + d.art), BG,
                    (f == EnhFail.Max ? Loc.T("★ 최대") : f == EnhFail.Dup ? Loc.T("재료 없음") : Loc.F("강화 ★{0}", DecoStar(d.id) + 1), "wtenh", d.id, f == EnhFail.None));
            }
        }
    }
}
