using System;
using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;

namespace MoreMush
{
    // 꼬마버섯 세계수 규칙 1: 성장(나이테·공물·진화), 정령(해금·레벨·각성=진화 3단계·구역 배치·호감도), 나무버섯 자동 수확과 수확 보따리.
    // 세계수가 한 단계 자랄 때마다 새 정령이 열리고(균사석으로 데려온다) 이미 있는 정령의 다음 진화가 열린다 (2026-10-16 사용자).
    // 공방·장식·부탁·라운드 보너스는 Game.Farm.Deco.cs. 시각은 실제 시간 ms(Date.now()와 같음).
    // 수확량을 바꾸는 일(레벨 업·배치·장식 등)은 바꾸기 전에 BagSettle()로 그때까지의 수확을 보따리에 넣는다.
    public static partial class Game
    {
        public static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static string Mmss(long s) => s >= 3600 ? $"{s / 3600}:{s % 3600 / 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        public static string TimeText(double s) => s >= 3600 ? Loc.F("{0}시간 {1}분", (int)(s / 3600), U.JsRound(s % 3600 / 60)) : s >= 60 ? Loc.F("{0}분", U.JsRound(s / 60)) : Loc.F("{0}초", Math.Ceiling(s));

        static SaveData.Farm F => G.farm;

        // ===== 성장 =====
        public static int Stage() => G?.farm == null ? 1 : F.stage;
        public static int Ring() => G?.farm == null ? 1 : F.ring;
        public static bool StageMax() => Stage() >= WT.stages;
        public static double TreeExp() => G?.farm == null ? 0 : F.exp;
        public static double TreeExpNeed() => StageMax() ? 0 : WT.exp[Stage()];
        public static double RingCost() => Ring() >= WT.ringMax ? 0 : Math.Ceiling(WT.RingCost(Stage(), Ring()));

        public enum GrowFail { None, Max, Gold, Ring, Exp, Gem }
        public static GrowFail CanRingUp() => Ring() >= WT.ringMax ? GrowFail.Max : G.gold < RingCost() ? GrowFail.Gold : GrowFail.None;
        public static bool RingUp()
        {
            if (CanRingUp() != GrowFail.None) return false;
            BagSettle();
            double c = RingCost();
            G.gold -= c; F.ring++;
            Telemetry.TreeUp("ring", F.stage * 100 + F.ring, c, 0);
            SaveGame();
            return true;
        }

        public static GrowFail CanEvolve()
        {
            if (StageMax()) return GrowFail.Max;
            if (Ring() < WT.ringMax) return GrowFail.Ring;
            if (TreeExp() < TreeExpNeed()) return GrowFail.Exp;
            if (G.gold < WT.evoGold[Stage()]) return GrowFail.Gold;
            return G.gem < WT.evoGem[Stage()] ? GrowFail.Gem : GrowFail.None;
        }

        // 진화: 경험치·골드·균사석을 내고 다음 단계로 (나이테 Lv.1). opened = 이번 단계에서 새로 열린 정령 (균사석으로 데려온다).
        // 이번 단계에서 다음 진화(각성)가 열리는 정령도 생긴다 (KLV.EvoStage)
        public static bool Evolve(out List<Special> opened)
        {
            opened = null;
            if (CanEvolve() != GrowFail.None) return false;
            BagSettle();
            int s = Stage();
            G.gold -= WT.evoGold[s]; G.gem -= WT.evoGem[s]; F.exp -= WT.exp[s];
            F.stage++; F.ring = 1;
            Telemetry.TreeUp("stage", F.stage, WT.evoGold[s], WT.evoGem[s]);
            FarmJoin();
            opened = SPECIALS.Where(k => JoinLv(k.id) == F.stage && !HasSpecial(k.id)).ToList();
            // 이번 단계에 열린 구역: 그곳이 선호 구역인 쉬는 꼬마를 보낸다 (합류할 때는 아직 닫혀 있던 꼬마)
            foreach (var k in KidsOwned())
                if (ZoneOf(k.id) == null && ZONE[k.zone].open == F.stage && ZoneRoom(k.zone)) F.zone[k.id] = k.zone;
            DecoSlotsEnsure();
            SaveGame();
            return true;
        }

        // 공물: 창고 버섯을 바쳐 경험치 (그 단계가 원하는 버섯은 ×2). 마지막 단계에서는 바칠 수 없다
        public static bool IsPrefer(string sp, int stage = 0) { if (stage <= 0) stage = Stage(); return Array.IndexOf(WT_PREFER[stage - 1], SP[sp].nKo) >= 0; }
        public static double TributeValue(string sp) => TIER_VALUE[SP[sp].t] * (IsPrefer(sp) ? TRIBUTE_PREFER : 1);
        public static int StoreOf(string sp) => G?.farm?.store != null && F.store.TryGetValue(sp, out var n) ? n : 0;
        public static int StoreTotal() => G?.farm?.store == null ? 0 : F.store.Values.Sum();
        public static double TributeAllValue() => StageMax() ? 0 : F.store.Sum(kv => TributeValue(kv.Key) * kv.Value);

        // 창고 버섯을 모두 바친다 → 얻은 경험치 (0 = 바칠 것 없음)
        public static double TributeAll()
        {
            double exp = TributeAllValue();
            if (exp <= 0) return 0;
            F.store.Clear(); F.exp += exp;
            Telemetry.TreeAct("tribute", null, exp, 0, 0);
            SaveGame();
            return exp;
        }

        // 라운드에서 딴 버섯을 창고에 (도감 수확 수와 같은 단위: 버섯 1개)
        public static void StoreAdd(IReadOnlyDictionary<string, int> picks)
        {
            if (G?.farm == null || picks == null) return;
            foreach (var kv in picks) if (kv.Value > 0 && SP.ContainsKey(kv.Key)) F.store[kv.Key] = StoreOf(kv.Key) + kv.Value;
        }
        static bool StoreTake(string sp, int n)
        {
            if (n <= 0 || StoreOf(sp) < n) return false;
            int left = StoreOf(sp) - n;
            if (left > 0) F.store[sp] = left; else F.store.Remove(sp);
            return true;
        }

        // ===== 꼬마 =====
        public static List<Special> FarmOwned() => SPECIALS.Where(k => KID.ContainsKey(k.id) && HasSpecial(k.id)).ToList();
        public static List<Kid> KidsOwned() => KIDS.Where(k => HasSpecial(k.id)).ToList();

        // 무료 정령(해금 균사석 0 = 첫 정령)만 세계수 단계에 맞춰 저절로 합류시키고, 새로 들어온 정령을 돌려준다.
        // 나머지는 단계가 열리면 균사석으로 데려온다 (UnlockKid). 예전 세이브에서 이미 합류한 정령은 그대로 둔다
        public static List<Special> FarmJoin()
        {
            var joined = new List<Special>();
            if (G == null) return joined;
            for (int i = 0; i < CRITTER_JOIN.Length && i < Stage(); i++)
            {
                string id = CRITTER_JOIN[i];
                if (HasSpecial(id) || UNLOCK_GEM[i] > 0) continue;
                Join(id); joined.Add(SPC[id]);
            }
            return joined;
        }

        // 합류: 선호 구역이 열려 있고 자리가 있으면 거기로 간다
        static void Join(string id)
        {
            G.specials[id] = true;
            if (KID.TryGetValue(id, out var k) && ZoneOpen(k.zone) && ZoneRoom(k.zone)) F.zone[id] = k.zone;
        }

        // ===== 정령 해금 (세계수 단계가 열고, 균사석으로 데려온다) =====
        public static bool KidOpen(string kid) => JoinLv(kid) > 0 && Stage() >= JoinLv(kid);
        public static KidFail CanUnlockKid(string kid)
        {
            if (JoinLv(kid) <= 0) return KidFail.NotOwned;
            if (HasSpecial(kid)) return KidFail.Owned;
            if (!KidOpen(kid)) return KidFail.Stage;
            return G.gem < UnlockGem(kid) ? KidFail.Gem : KidFail.None;
        }
        public static bool UnlockKid(string kid)
        {
            if (CanUnlockKid(kid) != KidFail.None) return false;
            BagSettle();
            int c = UnlockGem(kid);
            G.gem -= c;
            Join(kid);
            Telemetry.TreeUp("unlock_" + kid, JoinLv(kid), 0, c);
            SaveGame();
            return true;
        }

        public static int KLv(string kid) => G?.farm?.klv != null && F.klv.TryGetValue(kid, out var l) ? l : 1;
        public static int Awaken(string kid) => G?.farm?.awaken != null && F.awaken.TryGetValue(kid, out var a) ? a : 0;
        public static int KLvSum() => KidsOwned().Sum(k => KLv(k.id));
        public static int KLvSteps() => KLV.sumAt.Count(a => KLvSum() >= a);

        public enum KidFail { None, NotOwned, Max, Cap, Gold, Gem, Lv, Owned, Stage }   // Stage = 세계수 단계가 아직 낮다
        public static double KLvCost(string kid) => KLv(kid) >= KLV.max ? 0 : KLV.Cost(KLv(kid));
        public static KidFail CanKidLvUp(string kid)
        {
            if (!HasSpecial(kid)) return KidFail.NotOwned;
            if (KLv(kid) >= KLV.max) return KidFail.Max;
            if (KLv(kid) >= KLV.Cap(Awaken(kid))) return KidFail.Cap;
            return G.gold < KLvCost(kid) ? KidFail.Gold : KidFail.None;
        }
        public static bool KidLvUp(string kid)
        {
            if (CanKidLvUp(kid) != KidFail.None) return false;
            BagSettle();
            double c = KLvCost(kid);
            G.gold -= c; F.klv[kid] = KLv(kid) + 1;
            Telemetry.TreeUp("klv_" + kid, F.klv[kid], c, 0);
            DecoSlotsEnsure();   // Lv 합계 보상 (장식 슬롯)
            SaveGame();
            return true;
        }

        // 각성 = 정령 진화 (★1~3): Lv.10·20·30에서 멈춘 성장을 풀고 특별 보너스 단계를 올린다.
        // 세계수가 그 진화의 단계(KLV.EvoStage)에 올라 있어야 하고, 균사석만 낸다
        public static int AwakenStage(string kid) => KLV.EvoStage(kid, Awaken(kid) + 1);   // 다음 진화가 열리는 세계수 단계
        public static KidFail CanAwaken(string kid)
        {
            if (!HasSpecial(kid)) return KidFail.NotOwned;
            int a = Awaken(kid);
            if (a >= KLV.awakenMax) return KidFail.Max;
            if (Stage() < AwakenStage(kid)) return KidFail.Stage;
            if (KLv(kid) < 10 * (a + 1)) return KidFail.Lv;
            return G.gem < KLV.awakenGem[a] ? KidFail.Gem : KidFail.None;
        }
        public static bool KidAwaken(string kid)
        {
            if (CanAwaken(kid) != KidFail.None) return false;
            BagSettle();
            int a = Awaken(kid);
            G.gem -= KLV.awakenGem[a]; F.awaken[kid] = a + 1;
            Telemetry.TreeUp("awaken_" + kid, a + 1, 0, KLV.awakenGem[a]);
            SaveGame();
            return true;
        }

        // 호감도: 점수를 쌓아 Lv.1~10
        public static double AffPts(string kid) => G?.farm?.aff != null && F.aff.TryGetValue(kid, out var p) ? p : 0;
        public static (int lv, double cur, double need) AffOf(string kid)
        {
            double p = AffPts(kid); int lv = 1;
            while (lv < AFF.max && p >= AFF.Need(lv)) { p -= AFF.Need(lv); lv++; }
            return (lv, p, lv >= AFF.max ? 0 : AFF.Need(lv));
        }
        public static int AffLv(string kid) => AffOf(kid).lv;
        static void AffAdd(string kid, double pts) { BagSettle(); F.aff[kid] = AffPts(kid) + pts; }
        public static bool IsFav(string kid, string sp) => KID.TryGetValue(kid, out var k) && Array.IndexOf(k.fav, SP[sp].nKo) >= 0;
        public static double GiftValue(string kid, string sp) => TIER_VALUE[SP[sp].t] * (IsFav(kid, sp) ? GIFT_FAV : 1);

        // 선물: 창고 버섯 n개 → 호감도. 오른 호감도 Lv를 돌려준다 (못 주면 -1)
        public static int Gift(string kid, string sp, int n)
        {
            if (!HasSpecial(kid) || !KID.ContainsKey(kid) || !SP.ContainsKey(sp ?? "") || !StoreTake(sp, n)) return -1;
            int lv0 = AffLv(kid);
            AffAdd(kid, GiftValue(kid, sp) * n);
            Telemetry.TreeAct("gift", kid, GiftValue(kid, sp) * n, n, 0);
            SaveGame();
            return AffLv(kid) - lv0;
        }

        // ===== 구역 배치 =====
        public static bool ZoneOpen(string zone) => ZONE.TryGetValue(zone ?? "", out var z) && Stage() >= z.open;
        public static string ZoneOf(string kid) => G?.farm?.zone != null && F.zone.TryGetValue(kid, out var z) && ZoneOpen(z) ? z : null;
        public static List<string> KidsIn(string zone) => KidsOwned().Where(k => ZoneOf(k.id) == zone).Select(k => k.id).ToList();
        public static int ZoneCapacity() => HARV.ZoneCap(Stage()) + (KLvSum() >= KLV.sumAt[1] ? 1 : 0) + (KLvSum() >= KLV.sumAt[3] ? 1 : 0);
        public static bool ZoneRoom(string zone) => KidsIn(zone).Count < ZoneCapacity();

        // 꼬마를 구역에 보낸다 (zone = null이면 쉬게 한다)
        public static bool Assign(string kid, string zone)
        {
            if (!HasSpecial(kid) || !KID.ContainsKey(kid)) return false;
            if (zone != null && (!ZoneOpen(zone) || (ZoneOf(kid) != zone && !ZoneRoom(zone)))) return false;
            BagSettle();
            if (zone == null) F.zone.Remove(kid); else F.zone[kid] = zone;
            SaveGame();
            return true;
        }

        // ===== 나무버섯 슬롯 =====
        public static int MushSlotCount() => WT.mushSlots[Stage() - 1] + (int)BeautyValue("slots");

        // 열린 구역마다 나무버섯 슬롯 수 (비율 뿌리 4·줄기 3·가지 3·수관 2, 남는 자리는 비율 큰 순서)
        public static Dictionary<string, int> ZoneSlots()
        {
            var open = ZONES.Where(z => ZoneOpen(z.id)).ToList();
            int total = MushSlotCount(), wsum = open.Sum(z => z.weight);
            var o = open.ToDictionary(z => z.id, z => total * z.weight / wsum);
            int left = total - o.Values.Sum();
            foreach (var z in open.OrderByDescending(z => total * z.weight % wsum).ThenBy(z => Array.IndexOf(ZONES, z))) { if (left <= 0) break; o[z.id]++; left--; }
            foreach (var z in ZONES) if (!o.ContainsKey(z.id)) o[z.id] = 0;
            return o;
        }

        // 구역 슬롯 i에 돋는 버섯 (구역의 버섯을 번갈아, 아직 등장 단계가 아닌 버섯은 앞의 것으로)
        public static TreeMush SlotMush(string zone, int i)
        {
            var avail = ZONE[zone].mush.Select(id => TMUSH[id]).Where(m => Stage() >= m.from).ToList();
            if (avail.Count == 0) avail.Add(TMUSH["shelf"]);
            return avail[i % avail.Count];
        }

        // ===== 수확량 =====
        // 나무버섯 성장 속도 (구역마다): 나이테 · 장식 · 이슬 꼬마 · 균사 트리 노드
        public static double GrowMul(string zone)
        {
            double m = 1 + WT.ringGrow * (Ring() - 1) + DecoSum("grow") + (KidsIn(zone).Contains("dew") ? KID["dew"].tv : 0);
            return m / NF.gm_farm(Lv("gm_farm"));
        }

        // 꼬마 하나의 처리 속도 (수확 횟수/초): 4초에 1번 × 속도 × 운반 × 연쇄
        public static double KidPickRate(string kid, string zone)
        {
            var k = KID[kid];
            double speed = (1 + (k.trait == "speed" ? k.tv : 0)) * (k.trait == "wind" ? k.tv : 1) * (1 + DecoSum("move")) / Math.Max(0.2, 1 - DecoSum("pick"));
            double carry = (k.trait == "carry" ? k.tv : 1) + (zone == "root" ? DecoSum("carryRoot") : 0);
            double chain = k.trait == "chain" ? 1 + k.tv : 1;
            return speed * carry * chain / HARV.sec;
        }

        // 꼬마 하나의 수확량 배율 (기획서 분당 수확량 공식): 레벨 · 각성 · 선호 구역 · 장식·세트 보너스 합 · 호감도 대성공 · 행운
        public static double KidMul(string kid, string zone)
        {
            var k = KID[kid];
            double add = DecoSum("self", kid) + (KidSetDone(kid) ? DECO_SET_YIELD : 0) + DecoSum("allKid") + BeautyValue("allKid")
                         + (KidsAssigned().Contains("star") ? KID["star"].tv : 0) + (k.trait == "wind" && ZONE[zone].high ? k.tv2 : 0);
            return (1 + HARV.perLv * (KLv(kid) - 1)) * KLV.awakenMul[Awaken(kid)] * (1 + (k.zone == zone ? HARV.prefer : 0)) * (1 + add)
                   * (1 + AFF.greatPerLv * AffLv(kid)) * (1 + DecoSum("lucky"));
        }
        static List<string> KidsAssigned() => KidsOwned().Where(k => ZoneOf(k.id) != null).Select(k => k.id).ToList();

        public struct Rate { public double gold, gem, n; }

        // 구역 하나의 초당 수확: 슬롯마다 (자라는 시간 + 따는 시간)에 한 번. 꼬마 처리량이 모자라면 그만큼만.
        // 수관 발광 자리는 6단계부터 5% 확률로 황금 나무버섯이 돋는다 (기댓값으로 계산)
        public static Rate ZoneRate(string zone, int slots)
        {
            var kids = KidsIn(zone);
            if (kids.Count == 0 || slots <= 0) return default;
            double cap = kids.Sum(k => KidPickRate(k, zone)), grow = GrowMul(zone), pick = HARV.sec * kids.Count / cap;   // 평균 따는 시간
            double supply = 0, gold = 0, gem = 0;
            var share = kids.ToDictionary(k => k, k => KidPickRate(k, zone) / cap);
            for (int i = 0; i < slots; i++)
            {
                var m = SlotMush(zone, i);
                var parts = new List<(TreeMush m, double p)> { (m, 1) };
                if (zone == "crown" && Stage() >= TMUSH["golden"].from) parts = new List<(TreeMush, double)> { (m, 1 - GOLDEN_IN_CROWN), (TMUSH["golden"], GOLDEN_IN_CROWN) };
                double cycle = parts.Sum(x => x.p * (x.m.time / grow + pick));
                supply += 1 / cycle;
                foreach (var (mm, p) in parts)
                    foreach (var k in kids)
                    {
                        double w = p / cycle * share[k] * KidMul(k, zone);
                        double typeMul = (mm.id == "shelf" ? 1 + DecoSum("shelf") : 1) * (mm.id == "glow" ? 1 + DecoSum("glowUp") : 1)
                                         * (mm.id == "glow" && KID[k].trait == "glow" ? KID[k].tv : 1);
                        gold += w * typeMul * mm.gold;
                        gem += w * typeMul * mm.gem * (1 + (KID[k].trait == "gem" ? KID[k].tv : 0) + (KID[k].trait == "glow" ? KID[k].tv2 : 0));
                    }
            }
            double eff = Math.Min(1, cap / supply);
            double n = supply * eff;
            gem += n * NF.gm_spore(Lv("gm_spore")) * 2;   // 균사 트리 노드: 딸 때 확률로 균사석 +2
            return new Rate { gold = gold * eff, gem = gem * eff, n = n };
        }

        // 세계수 전체의 초당 수확 (보따리에 쌓이는 양)
        public static Rate BagRate()
        {
            var r = new Rate();
            if (G?.farm == null) return r;
            bool began = MemoBegin();
            try
            {
                foreach (var kv in ZoneSlots())
                {
                    var z = ZoneRate(kv.Key, kv.Value);
                    r.gold += z.gold; r.gem += z.gem; r.n += z.n;
                }
                double all = (1 + DecoSum("bagEff")) * NF.gm_herb(Lv("gm_herb"));
                double mul = WT.bagMul[Math.Min(Stage(), WT.bagMul.Length) - 1];
                r.gold = BagGold(r.gold * all * (1 + DecoSum("treeGold")));
                r.gem *= all * mul * (1 + DecoSum("gem")) * NF.gm_gourmet(Lv("gm_gourmet"));
                r.n *= all * mul;
            }
            finally { MemoEnd(began); }
            return r;
        }

        // 보따리 골드/초: 일반 판 골드/초(평활) × 단계 비율 × 효율항(기준 배치 대비 나무버섯 골드, 1 위로는 제곱근·상한 bagEffMax).
        // 판 기록이 없으면 0 — 골드는 직접 플레이해야 기준이 오른다
        static double BagGold(double raw)
        {
            if (!(raw > 0) || !(G.bagGoldPerSec > 0)) return 0;   // NaN도 0으로
            int i = Math.Min(Stage(), WT.bagRatio.Length) - 1;
            double x = raw * 3600 / WT.bagRefGoldH[i];
            double eff = x <= 1 ? x : Math.Min(WT.bagEffMax, Math.Sqrt(x));
            return G.bagGoldPerSec * WT.bagRatio[i] * eff * BagCodexMul();
        }

        // 도감 보너스 (골드만): 모은 종 비율만큼, 도감을 다 채우면 ×(1 + bagCodexMax)
        public static double BagCodexMul() => SP_TOTAL <= 0 ? 1 : 1 + WT.bagCodexMax * SPECIES.Count(sp => Harvested(sp.id)) / SP_TOTAL;

        // 일반 판이 끝날 때 보따리 골드 기준을 갱신한다. 그때까지 쌓인 몫은 먼저 옛 기준으로 넣는다
        public static void BagBaseUpdate(double roundGold, double timeMax)
        {
            if (G == null || !(roundGold >= 0) || !(timeMax > 0) || double.IsInfinity(roundGold) || double.IsInfinity(timeMax)) return;
            BagSettle();
            double now = roundGold / (timeMax + WT.bagRoundGap);
            G.bagGoldPerSec = G.bagGoldPerSec <= 0 ? now : G.bagGoldPerSec * WT.bagBaseKeep + now * (1 - WT.bagBaseKeep);
        }

        // ===== 수확 보따리 =====
        public static double BagCapSec() => (HARV.bagHours + (HasSpecial("rock") ? KID["rock"].tv2 : 0) + DecoSum("bagH")) * 3600;

        // 지금까지의 수확을 보따리에 넣는다 (보관 시간이 다 차면 더 쌓이지 않는다).
        // 세계수가 열린 것으로 기록되기 전(Unlock.Newly)에는 시계만 지금으로 당겨 둔다 — 해금 전 시간은 쌓이지 않는다
        public static void BagSettle()
        {
            if (G?.farm == null) return;
            var b = F.bag; double now = Now();
            if (b.t <= 0 || b.t > now || !Unlock.Opened(Unlock.WORLDTREE)) { b.t = now; return; }
            double dt = (now - b.t) / 1000, use = Math.Max(0, Math.Min(dt, BagCapSec() - b.sec));
            b.t = now;
            if (use <= 0) return;
            var r = BagRate();
            if (r.gold <= 0 && r.gem <= 0) return;   // 받을 게 없으면 보관 시간도 채우지 않는다 (꽉 찬 빈 보따리로 막히지 않게)
            b.gold += r.gold * use; b.gem += r.gem * use; b.n += r.n * use; b.sec += use;
        }
        // 세계수가 막 열렸을 때: 해금 전에 쌓였던 보따리를 비우고 지금부터 다시 잰다
        public static void BagRestart()
        {
            if (G?.farm == null) return;
            F.bag = new SaveData.Bag { t = Now() };
        }

        public static bool BagFull() => G?.farm != null && F.bag.sec >= BagCapSec() - 1;
        public static bool BagReady() => G?.farm != null && (Math.Floor(F.bag.gold) >= HARV.bagMinClaim || Math.Floor(F.bag.gem) >= 1);

        // 보따리 받기 → (골드, 균사석, 나무버섯 개수). 소수점 아래는 보따리에 남는다
        public static (double gold, int gem, int n)? BagClaim()
        {
            BagSettle();
            if (!BagReady()) return null;
            var b = F.bag;
            double gold = Math.Floor(b.gold); int gem = (int)Math.Floor(b.gem), n = (int)Math.Floor(b.n);
            G.gold += gold; G.gem += gem;
            b.gold -= gold; b.gem -= gem; b.n -= n; b.sec = 0;
            SaveGame();
            return (gold, gem, n);
        }

        // ===== 시작·시계 =====
        // 새 게임·이어하기·복귀 때: 꼬마 합류 · 보따리 시계 · 장식 슬롯 · 부탁
        public static void FarmEnsure()
        {
            if (G?.farm == null) return;
            FixClock();
            FarmJoin();
            BagSettle();
            DecoSlotsEnsure();
            ReqEnsure();
        }

        // 기기 시계가 앞서 있다가 뒤로 맞춰지면 미래 시각에 멈춘 타이머를 지금 기준으로 당긴다 (남은 시간은 그대로)
        public static void FixClock()
        {
            if (G?.farm == null) return;
            double now = Now();
            if (F.bag.t > now) F.bag.t = now;
            foreach (var c in F.crafts) if (c.t0 > now) { c.at = now + (c.at - c.t0); c.t0 = now; }
        }
    }
}
