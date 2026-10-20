using System;
using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;

namespace MoreMush
{
    // 꼬마버섯 세계수 규칙 2: 꼬마 공방(장식 제작·즉시 완료), 장식(배치·세트·강화), 아름다움 점수, 일일 부탁·주간 상자,
    // 세계수가 라운드에 주는 보너스(가호·꼬마 특별 보너스·장식·아름다움).
    public static partial class Game
    {
        // ===== 장식 =====
        public static int DecoStar(string id) => G?.farm?.decoStar != null && F.decoStar.TryGetValue(id, out var s) ? s : 0;
        public static bool DecoOwned(string id) => DecoStar(id) > 0;
        public static int DecoDup(string id) => G?.farm?.decoDup != null && F.decoDup.TryGetValue(id, out var n) ? n : 0;
        public static bool DecoPlaced(string id) => G?.farm?.decoSlots != null && F.decoSlots.Contains(id);
        public static double DecoMul(string id) => DECO_STAR_MUL[Math.Max(1, DecoStar(id)) - 1] * BeautyValue("decoMul", 1);

        // 배치한 장식의 효과 합 (owner를 주면 그 꼬마의 장식만)
        public static double DecoSum(string fx, string owner = null)
        {
            if (G?.farm?.decoSlots == null) return 0;
            if (memoOn && decoMemo.TryGetValue((fx, owner), out var m)) return m;
            double v = 0;
            foreach (var id in F.decoSlots)
                if (id != null && DECO.TryGetValue(id, out var d) && d.fx == fx && (owner == null || d.of == owner)) v += d.v * DecoMul(id);
            if (memoOn) decoMemo[(fx, owner)] = v;
            return v;
        }

        // BagRate 한 번 안에서는 장식 합·아름다움이 바뀌지 않는다 → 계산 중에만 기억해 둔다 (꼬마·슬롯마다 다시 세지 않게)
        static bool memoOn; static double beautyMemo = -1;
        static readonly Dictionary<(string fx, string owner), double> decoMemo = new Dictionary<(string, string), double>();
        static bool MemoBegin() { if (memoOn) return false; memoOn = true; decoMemo.Clear(); beautyMemo = -1; return true; }
        static void MemoEnd(bool began) { if (!began) return; memoOn = false; decoMemo.Clear(); beautyMemo = -1; }

        // 세트: 그 꼬마의 장식 3종을 모두 배치
        public static bool KidSetDone(string kid) => DECOS.Where(d => d.of == kid).All(d => DecoPlaced(d.id));

        // 장식 슬롯: 단계별 수 + 꼬마 Lv 합계 보상. 짝수 번째는 설치형(뿌리 주변), 홀수 번째는 걸이형(가지·수관)
        public static int DecoSlotCount() => WT.decoSlots[Stage() - 1] + KLvSteps();
        public static bool DecoSlotHang(int i) => i % 2 == 1;
        public static void DecoSlotsEnsure()
        {
            if (G?.farm == null) return;
            var l = F.decoSlots; int n = DecoSlotCount();
            while (l.Count < n) l.Add(null);
        }

        public enum DecoFail { None, NotOwned, Slot, Type }
        public static DecoFail CanPlace(int slot, string id)
        {
            if (slot < 0 || slot >= DecoSlotCount()) return DecoFail.Slot;
            if (id == null) return DecoFail.None;
            if (!DecoOwned(id)) return DecoFail.NotOwned;
            return DECO[id].hang != DecoSlotHang(slot) ? DecoFail.Type : DecoFail.None;
        }

        // 슬롯에 장식을 놓는다 (id = null이면 비운다). 다른 슬롯에 있던 같은 장식은 옮겨 온다.
        // 처음 놓는 장식이면 균사석과 주인 꼬마 호감도 → firstGift
        public static bool PlaceDeco(int slot, string id, out bool firstGift)
        {
            firstGift = false;
            DecoSlotsEnsure();
            if (CanPlace(slot, id) != DecoFail.None) return false;
            BagSettle();
            var l = F.decoSlots;
            if (id != null) { int old = l.IndexOf(id); if (old >= 0) l[old] = null; }
            l[slot] = id;
            if (id != null && !F.decoFirst.ContainsKey(id))
            {
                F.decoFirst[id] = true; firstGift = true;
                G.gem += DECO_FIRST_GEM;
                if (HasSpecial(DECO[id].of)) AffAdd(DECO[id].of, AFF.placeGift);
            }
            SaveGame();
            return true;
        }

        // 강화: 같은 장식 1개 + 골드 (★4부터 균사석도)
        public static (double gold, int gem)? EnhanceCost(string id)
        {
            int s = DecoStar(id);
            if (s <= 0 || s >= DECO_STAR_MAX) return null;
            var g = DECO_GRADES[DECO[id].grade];
            return (g.gold * s, s + 1 >= 4 ? g.gem * (s - 2) : 0);
        }
        public enum EnhFail { None, Max, Dup, Gold, Gem }
        public static EnhFail CanEnhance(string id)
        {
            var c = EnhanceCost(id);
            if (c == null) return EnhFail.Max;
            if (DecoDup(id) < 1) return EnhFail.Dup;
            if (G.gold < c.Value.gold) return EnhFail.Gold;
            return G.gem < c.Value.gem ? EnhFail.Gem : EnhFail.None;
        }
        public static bool Enhance(string id)
        {
            if (CanEnhance(id) != EnhFail.None) return false;
            BagSettle();
            var c = EnhanceCost(id).Value;
            G.gold -= c.gold; G.gem -= c.gem;
            if (DecoDup(id) > 1) F.decoDup[id] = DecoDup(id) - 1; else F.decoDup.Remove(id);
            F.decoStar[id] = DecoStar(id) + 1;
            Telemetry.TreeUp("deco_" + id, F.decoStar[id], c.gold, c.gem);
            SaveGame();
            return true;
        }

        // ===== 아름다움 점수 =====
        public static double Beauty()
        {
            if (G?.farm?.decoStar == null) return 0;
            if (memoOn && beautyMemo >= 0) return beautyMemo;
            double b = 0;
            foreach (var kv in F.decoStar) if (DECO.TryGetValue(kv.Key, out var d)) b += DECO_GRADES[d.grade].beauty * DECO_STAR_MUL[Math.Min(DECO_STAR_MAX, kv.Value) - 1];
            // 꼬마 왕관(아름다움 ×1.1)은 강화 배율만 본다 — DecoMul은 아름다움 2000점 보상을 보므로 여기서 부르면 서로를 부른다
            double mul = 0;
            foreach (var id in F.decoSlots) if (id != null && DECO.TryGetValue(id, out var d) && d.fx == "beautyMul") mul += d.v * DECO_STAR_MUL[Math.Max(1, DecoStar(id)) - 1];
            double r = Math.Floor(b * (1 + mul));
            if (memoOn) beautyMemo = r;
            return r;
        }
        // 달성한 아름다움 보상 값 (key가 같은 단계의 값, 없으면 dflt)
        public static double BeautyValue(string key, double dflt = 0)
        {
            if (G?.farm?.decoStar == null || F.decoStar.Count == 0) return dflt;
            double b = Beauty();
            foreach (var s in BEAUTY) if (s.key == key && b >= s.at) return s.v;
            return dflt;
        }

        // ===== 꼬마 공방 =====
        public static bool CraftOpen() => CodexCount() >= WT.codexForCraft;
        public static bool RecipeOpen(Deco d) => HasSpecial(d.of);
        public static double CraftSeconds(Deco d) => DECO_GRADES[d.grade].hours * 3600 * NF.md_chef(Lv("md_chef"));

        public enum CraftFail { None, Closed, Recipe, Slots, Gold, Gem }
        public static CraftFail CanCraft(string id)
        {
            if (!CraftOpen()) return CraftFail.Closed;
            if (!DECO.TryGetValue(id ?? "", out var d) || !RecipeOpen(d)) return CraftFail.Recipe;
            if (F.crafts.Count >= WT.CraftSlots(Stage())) return CraftFail.Slots;
            var g = DECO_GRADES[d.grade];
            if (G.gold < g.gold) return CraftFail.Gold;
            return G.gem < g.gem ? CraftFail.Gem : CraftFail.None;
        }
        public static bool CraftStart(string id)
        {
            if (CanCraft(id) != CraftFail.None) return false;
            var d = DECO[id]; var g = DECO_GRADES[d.grade]; double now = Now();
            G.gold -= g.gold; G.gem -= g.gem;
            F.crafts.Add(new SaveData.Craft { deco = id, t0 = now, at = now + CraftSeconds(d) * 1000 });
            Telemetry.TreeBuild(id, g.gold, g.gem);
            SaveGame();
            return true;
        }
        public static bool CraftDone(SaveData.Craft c) => c != null && Now() >= c.at;
        public static int CraftDoneCount() => G?.farm?.crafts == null ? 0 : F.crafts.Count(CraftDone);

        // 완성된 장식 받기 → (장식, 새로 얻었나). 이미 있으면 강화 재료로 쌓인다
        public static (Deco d, bool isNew)? CraftClaim(int i)
        {
            if (i < 0 || i >= F.crafts.Count || !CraftDone(F.crafts[i])) return null;
            BagSettle();
            var d = DECO[F.crafts[i].deco];
            F.crafts.RemoveAt(i);
            bool isNew = !DecoOwned(d.id);
            if (isNew) F.decoStar[d.id] = 1; else F.decoDup[d.id] = DecoDup(d.id) + 1;
            SaveGame();
            return (d, isNew);
        }

        // 즉시 완료: 남은 10분마다 균사석 1, 하루 5회
        public static int RushLeftToday() { long day = KstDay(Now()); return day > F.rushDay ? WT.rushPerDay : Math.Max(0, WT.rushPerDay - F.rushUsed); }
        public static int RushCost(SaveData.Craft c) { double left = Math.Max(0, (c.at - Now()) / 1000); return left <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(left / WT.rushSec)); }
        public static bool CraftRush(int i)
        {
            if (i < 0 || i >= F.crafts.Count || RushLeftToday() <= 0) return false;
            var c = F.crafts[i]; int cost = RushCost(c);
            if (cost <= 0 || G.gem < cost) return false;
            long day = KstDay(Now());
            if (day > F.rushDay) { F.rushDay = day; F.rushUsed = 0; }   // 시계를 되돌려도 횟수가 초기화되지 않게
            G.gem -= cost; c.at = Now(); F.rushUsed++;
            Telemetry.TreeAct("craft_rush", c.deco, 0, 0, cost);
            SaveGame();
            return true;
        }

        // ===== 일일 부탁 · 주간 상자 =====
        public static void ReqEnsure()
        {
            if (G?.farm == null) return;
            long day = KstDay(Now()), week = KstWeek(day);
            if (week > F.reqWeek) { F.reqWeek = week; F.weekDone = 0; F.weekClaimed = false; }
            if (day > F.reqDay || F.reqs.Count == 0) { if (RollReqs()) F.reqDay = day; }
        }

        // 꼬마마다 좋아하는 버섯(딴 적 있는 것), 없으면 딴 적 있는 버섯 중에서. 딴 버섯이 아직 없으면 다음에 뽑는다
        static bool RollReqs()
        {
            var kids = KidsOwned(); var known = SPECIES.Where(sp => Harvested(sp.id)).ToList();
            if (kids.Count == 0 || known.Count == 0) return false;
            var list = new List<SaveData.Req>(); var pool = new List<Kid>(kids);
            for (int i = 0; i < REQ.perDay; i++)
            {
                if (pool.Count == 0) pool.AddRange(kids);
                var k = U.Pick(pool); pool.Remove(k);
                var fav = k.fav.Select(n => SP_BY_NAME[n]).Where(sp => Harvested(sp.id)).ToList();
                var sp = fav.Count > 0 ? U.Pick(fav) : U.Pick(known);
                list.Add(new SaveData.Req { kid = k.id, sp = sp.id, n = REQ.count[sp.t] });
            }
            F.reqs = list;
            return true;
        }

        public static double ReqGold() => Math.Ceiling(WT.RingCost(Stage(), 1) * REQ.goldMul);
        public static bool ReqReady(SaveData.Req r) => r != null && !r.done && StoreOf(r.sp) >= r.n;
        public static int ReqReadyCount() => G?.farm?.reqs == null ? 0 : F.reqs.Count(ReqReady);

        // 부탁 들어주기 → 받은 골드 (못 하면 null)
        public static double? ReqDo(int i)
        {
            if (i < 0 || i >= F.reqs.Count || !ReqReady(F.reqs[i])) return null;
            var r = F.reqs[i];
            StoreTake(r.sp, r.n);
            r.done = true;
            double gold = ReqGold();
            G.gold += gold;
            if (HasSpecial(r.kid)) AffAdd(r.kid, REQ.aff);
            F.weekDone++;
            SaveGame();
            return gold;
        }
        public static bool WeekReady() => G?.farm != null && !F.weekClaimed && F.weekDone >= REQ.weekGoal;
        public static bool WeekClaim()
        {
            if (!WeekReady()) return false;
            F.weekClaimed = true; G.gem += REQ.weekGem;
            SaveGame();
            return true;
        }

        // 세계수 화면 버튼의 알림 수: 보따리 가득 · 들어줄 수 있는 부탁 · 완성된 장식
        public static int FarmReadyCount() => G?.farm == null ? 0 : (BagFull() ? 1 : 0) + ReqReadyCount() + CraftDoneCount() + (WeekReady() ? 1 : 0);

        // ===== 라운드 보너스 =====
        // harvest 창고 버섯 획득 % · gold 라운드 골드 % · cols 군락지 수 % · colsN 군락지 수(개) · dur 제한시간(초) · spd 핀볼 속도 % · atk 공격력 %
        // crit 치명타 %p · bar 슬라이드 바 % · spark 연쇄 번개 확률 · goldenMul 황금 변종 배율 · goldenP 황금 변종 %p · skill 스킬 발동률 %
        public class TreeBonus { public double harvest, gold, cols, colsN, dur, spd, atk, crit, bar, spark, goldenMul = 1, goldenP, skill; }

        // 특별 보너스 단계 = 각성 ★ + 세트(1, 각성과 따로 쌓인다)
        public static int KidStep(string kid) => HasSpecial(kid) ? Awaken(kid) + (KidSetDone(kid) ? 1 : 0) : 0;

        public static TreeBonus RoundBonus()
        {
            var b = new TreeBonus();
            if (G?.farm == null) return b;
            int s = Stage();
            double bm = s >= WT.stages ? BLESS[WT.stages - 1].v : 1;   // 8단계: 모든 가호 ×1.5
            double goldenMul = 1;
            for (int i = 0; i < Math.Min(s, BLESS.Length); i++)
            {
                var g = BLESS[i];
                switch (g.key)
                {
                    case "harvest": b.harvest += g.v * bm; break;
                    case "gold": b.gold += g.v * bm; break;
                    case "colsN": b.colsN += g.v * bm; break;
                    case "dur": b.dur += g.v * bm; break;
                    case "skill": b.skill += g.v * bm; break;
                    case "goldenMul": goldenMul *= 1 + (g.v - 1) * bm; break;
                }
            }
            foreach (var k in KIDS)
            {
                int st = KidStep(k.id); if (st <= 0) continue;
                double v = k.bv * st;
                switch (k.bonus)
                {
                    case "cols": b.cols += v; break;
                    case "dur": b.dur += v; break;
                    case "spd": b.spd += v; break;
                    case "atk": b.atk += v; break;
                    case "bar": b.bar += v; break;
                    case "spark": b.spark += v; break;
                    case "golden": goldenMul *= 1 + v; break;
                    case "skill": b.skill += v; break;
                }
            }
            b.dur += DecoSum("dur"); b.spd += DecoSum("spd"); b.atk += DecoSum("atk"); b.crit += DecoSum("crit"); b.bar += DecoSum("bar");
            b.spark += DecoSum("spark"); b.goldenP += DecoSum("golden"); b.skill += DecoSum("skill");
            b.gold += BeautyValue("gold");
            b.goldenMul = goldenMul;
            return b;
        }
    }
}
