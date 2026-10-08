using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // Mushroom farm rules: critter requests (hearts · evolution · diamonds), critter star grades and owned effects,
    // buildings (fixed footprints on a tile grid, built by a free critter over real time), the mushroom tree, idle harvest
    // reward, and 균사석 speed-ups.
    // Times are real-time epoch milliseconds like Date.now(). Screen state (critters walking, job queue) is FarmView.
    public static partial class Game
    {
        public static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static string Mmss(long s) => s >= 3600 ? $"{s / 3600}:{s % 3600 / 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";

        // ===== 꼬마 =====
        public static List<Special> FarmOwned() => SPECIALS.Where(k => HasSpecial(k.id)).ToList();

        // 잡은 꼬마는 부탁 시각이 없으면 만들어 준다 (자동 입주). 나무 버섯 자리도 채운다
        public static void FarmEnsure()
        {
            foreach (var k in FarmOwned())
                if (!G.farm.next.ContainsKey(k.id)) { G.farm.next[k.id] = Now() + FARM.first * 1000; G.farm.kind[k.id] = "water"; }
            TreeEnsure();
        }

        public static bool FarmReady(string id) => G.farm.next.TryGetValue(id, out var t) && Now() >= t;
        public static int FarmPetCount() => G?.farm == null ? 0 : FarmOwned().Count(k => FarmReady(k.id));
        public static int FarmReadyCount() => FarmPetCount() + TreeRipeCount();   // 균사 트리 버튼 알림: 꼬마 부탁 + 다 자란 나무 버섯

        // 호감도·진화
        public static int EvoOf(string id) => Math.Min(FARM.evoMax, G.farm.evo.TryGetValue(id, out var e) ? e : 0);
        public static int HeartPer(string id) => FARM.per[EvoOf(id)];
        static int Love(string id) => G.farm.love.TryGetValue(id, out var l) ? l : 0;
        public static int HeartsOf(string id) => Math.Min(FARM.hearts, Love(id) / HeartPer(id));
        public static float HeartFrac(string id) => HeartsOf(id) >= FARM.hearts ? 0 : (float)(Love(id) % HeartPer(id)) / HeartPer(id);
        public static int HeartLeft(string id) => HeartsOf(id) >= FARM.hearts ? 0 : HeartPer(id) - Love(id) % HeartPer(id);
        public static bool CanEvolve(string id) => HeartsOf(id) >= FARM.hearts && EvoOf(id) < FARM.evoMax;
        public static string EvoName(Special k, int e = -1)
        {
            if (e < 0) e = EvoOf(k.id);
            return e == 0 ? k.n : e == 1 ? k.n.Replace("꼬마", "버섯") : k.n.Replace("꼬마", "대왕버섯");
        }
        public static string HeartStr(string id) => new string('♥', HeartsOf(id)) + new string('♡', FARM.hearts - HeartsOf(id));

        public static void FarmSchedule(string id)
        {
            G.farm.next[id] = Now() + U.Rand(FARM.wait[0], FARM.wait[1]) * FARM.evoWait[EvoOf(id)] * (1 - BStat("wait")) * 1000;
            G.farm.kind[id] = U.PickWeighted(FARM_KINDS, k => k.w).id;
        }

        public static FarmKind FarmKindOf(string id) => G.farm.kind.TryGetValue(id, out var k) && FARM_KIND.TryGetValue(k ?? "", out var fk) ? fk : FARM_KIND["water"];
        public static int FarmReward(string id) => Math.Max(1, (int)U.JsRound(FarmKindOf(id).gem * FARM.evoGem[EvoOf(id)]));

        public static string FarmLeftText(string id)
        {
            if (FarmReady(id)) return "부탁 있음!";
            long s = Math.Max(0, (long)Math.Ceiling(((G.farm.next.TryGetValue(id, out var t) ? t : Now()) - Now()) / 1000));
            return Mmss(s);
        }

        // 부탁 들어주기 → gem 균사석, bonus 호감도 보너스, dia 다이아몬드, heart 새 칸을 채움, full 5칸 다 참
        public struct Serve { public int gem, bonus, dia; public bool heart, full; }
        public static Serve? FarmServe(string id)
        {
            if (!FarmReady(id)) return null;
            int h0 = HeartsOf(id), gem = FarmReward(id);
            int bonus = U.Chance(FARM.bonusP * h0) ? U.RandI(1, 2 + EvoOf(id)) : 0;
            G.gem += gem + bonus;
            if (h0 < FARM.hearts) G.farm.love[id] = Love(id) + 1;
            FarmSchedule(id);
            int h1 = HeartsOf(id);
            bool heart = h1 > h0;
            int dia = (U.Chance(FARM.diaP + BStat("dia") + Fx("dia")) ? 1 : 0) + (heart ? FARM.diaHeart : 0);
            G.dia += dia;
            return new Serve { gem = gem, bonus = bonus, dia = dia, heart = heart, full = h1 >= FARM.hearts && h0 < FARM.hearts };
        }

        public static bool FarmEvolve(string id)
        {
            if (!HasSpecial(id) || !CanEvolve(id)) return false;
            G.farm.evo[id] = EvoOf(id) + 1; G.farm.love[id] = 0;
            SaveGame();
            return true;
        }

        // ===== 꼬마 별 등급 · 보유 효과 =====
        public static int CStarOf(string id) => G?.farm?.star != null && G.farm.star.TryGetValue(id, out var s) ? Math.Min(CSTAR.max, s) : 0;
        public static (double gold, int gem)? StarCost(string id) { int s = CStarOf(id); return s >= CSTAR.max ? ((double, int)?)null : (CSTAR.gold[s], CSTAR.gem[s]); }
        public static bool StarUp(string id)
        {
            var c = StarCost(id);
            if (!HasSpecial(id) || c == null || G.gold < c.Value.gold || G.gem < c.Value.gem) return false;
            G.gold -= c.Value.gold; G.gem -= c.Value.gem; G.farm.star[id] = CStarOf(id) + 1;
            SaveGame();
            return true;
        }

        // 꼬마 한 마리의 보유 효과 값 (안 잡았으면 0)
        public static float FxOf(string critter, int star = -1)
        {
            if (!CRITTER_FX.TryGetValue(critter, out var f)) return 0;
            if (star < 0) { if (G == null || !HasSpecial(critter)) return 0; star = CStarOf(critter); }
            return f.v0 + f.vs * star;
        }
        // 보유 효과 합 (key가 같은 꼬마 모두; "...Self" 효과는 그 꼬마가 일할 때만 FxOf로 따로 본다)
        public static float Fx(string key) { float v = 0; foreach (var kv in CRITTER_FX) if (kv.Value.key == key) v += FxOf(kv.Key); return v; }
        public static string FxText(string critter, int star = -1) => string.Format(CRITTER_FX[critter].fmt, U.Pct(FxOf(critter, star < 0 ? CStarOf(critter) : star)));

        // 별 등급 공통 능력치: 이 꼬마의 건설 속도 · 밭일 속도
        public static float BuildSpeed(string critter) => 1 + CSTAR.build * CStarOf(critter) + Fx("build");
        public static float WorkSpeed(string critter) => FARM.evoSpd[EvoOf(critter)] * (1 + CSTAR.work * CStarOf(critter) + Fx("work"));

        // ===== 건축 =====
        public static int BuiltCount(string id) => G.farm.blds.Count(b => b.id == id);          // 짓는 중 포함
        public static bool IsBuilding(string critter) { double now = Now(); return G.farm.blds.Any(b => !b.done && now < b.at && b.critter == critter); }
        public static List<Special> FreeCritters() => FarmOwned().Where(k => !IsBuilding(k.id)).ToList();
        // 완성된 건물 능력치 합. 농장 밖에 있는 동안 완성 시각이 지난 건물도 친다 (done 표시는 농장 화면의 BuildTick이 함)
        public static float BStat(string stat)
        {
            if (G?.farm?.blds == null) return 0;
            float v = 0; double now = -1;
            foreach (var b in G.farm.blds)
            {
                if (!b.done) { if (now < 0) now = Now(); if (now < b.at) continue; }
                if (BUILDING[b.id].stat == stat) v += BUILDING[b.id].val;
            }
            return v;
        }

        public static double BuildSeconds(Building b, string critter)
        {
            double t = b.time * (1 - BStat("build")) * NF.md_chef(Lv("md_chef"));
            if (critter != null && CRITTER_FX[critter].key == "buildSelf") t *= Math.Max(0.2, 1 - FxOf(critter));
            return t / (critter != null ? BuildSpeed(critter) : 1 + Fx("build"));
        }

        // 건물이 차지하는 화면 영역 (픽셀, 왼쪽 위 x·y와 너비·높이)
        public static Rect BuildRect(Building b, int gx, int gy) => new Rect(GRID.x0 + gx * GRID.cell, GRID.y0 + gy * GRID.cell, b.w * GRID.cell, b.h * GRID.cell);

        static bool InFence(float x, float y) { float dx = (x - GRID.ecx) / GRID.erx, dy = (y - GRID.ecy) / GRID.ery; return dx * dx + dy * dy <= 1; }

        // 격자 한 칸을 쓸 수 있나: 칸 가운데가 울타리 안 · 나무 둘레 밖 · 다른 건물이 없음 (skipUid = 옮기는 중인 자기 자신)
        public static bool TileOk(int gx, int gy, int skipUid = 0)
        {
            if (gx < 0 || gy < 0 || gx >= GRID.cols || gy >= GRID.rows) return false;
            float cx = GRID.x0 + (gx + 0.5f) * GRID.cell, cy = GRID.y0 + (gy + 0.5f) * GRID.cell;
            if (!InFence(cx, cy)) return false;
            if (cx > TREE.zoneX0 && cx < TREE.zoneX1 && cy > TREE.zoneY0 && cy < TREE.zoneY1) return false;
            foreach (var o in G.farm.blds)
                if (o.uid != skipUid && gx >= o.gx && gx < o.gx + BUILDING[o.id].w && gy >= o.gy && gy < o.gy + BUILDING[o.id].h) return false;
            return true;
        }

        // 그 자리에 놓을 수 있나: 건물이 덮는 칸이 모두 쓸 수 있어야 한다
        public static bool CanPlace(Building b, int gx, int gy, int skipUid = 0)
        {
            if (gx < 0 || gy < 0 || gx + b.w > GRID.cols || gy + b.h > GRID.rows) return false;
            for (int y = gy; y < gy + b.h; y++) for (int x = gx; x < gx + b.w; x++) if (!TileOk(x, y, skipUid)) return false;
            return true;
        }

        // 놓을 만한 첫 자리 (가운데에 가까운 순)
        public static Vector2Int? FirstSpot(Building b)
        {
            Vector2Int? best = null; float bd = float.MaxValue;
            for (int gy = 0; gy <= GRID.rows - b.h; gy++)
                for (int gx = 0; gx <= GRID.cols - b.w; gx++)
                {
                    if (!CanPlace(b, gx, gy)) continue;
                    var c = BuildRect(b, gx, gy).center;
                    float d = (c - new Vector2(TREE.x, 700)).sqrMagnitude;
                    if (d < bd) { bd = d; best = new Vector2Int(gx, gy); }
                }
            return best;
        }

        public enum BuildFail { None, Max, Dia, NoCritter, Spot }
        public static BuildFail CanBuild(string id)
        {
            var b = BUILDING[id];
            if (BuiltCount(id) >= b.max) return BuildFail.Max;
            if (G.dia < b.price) return BuildFail.Dia;
            if (FreeCritters().Count == 0) return BuildFail.NoCritter;
            return BuildFail.None;
        }

        // 건설 시작: 쉬고 있는 꼬마 중 무작위 한 마리가 맡는다. 맡은 꼬마의 id를 돌려준다
        public static SaveData.Bld BuildStart(string id, int gx, int gy, IList<string> prefer = null)
        {
            var b = BUILDING[id];
            if (CanBuild(id) != BuildFail.None || !CanPlace(b, gx, gy)) return null;
            var free = FreeCritters().Select(k => k.id).ToList();
            string critter = U.Pick(prefer != null && prefer.Any(free.Contains) ? prefer.Where(free.Contains).ToList() : free);
            double now = Now();
            G.dia -= b.price;
            var bld = new SaveData.Bld { uid = G.farm.nextUid++, id = id, gx = gx, gy = gy, critter = critter, t0 = now, at = now + BuildSeconds(b, critter) * 1000 };
            G.farm.blds.Add(bld);
            SaveGame();
            return bld;
        }

        // 지은(짓는 중인) 건물 옮기기: 자기 자리는 빈 칸으로 본다. 비용·시간은 들지 않는다
        public static bool BuildMove(int uid, int gx, int gy)
        {
            var b = G.farm.blds.FirstOrDefault(x => x.uid == uid);
            if (b == null || !CanPlace(BUILDING[b.id], gx, gy, uid)) return false;
            b.gx = gx; b.gy = gy;
            SaveGame();
            return true;
        }

        // 다 지은 건물을 완성 처리하고 돌려준다 (꼬마는 다시 쉰다)
        public static List<SaveData.Bld> BuildTick()
        {
            var done = new List<SaveData.Bld>();
            if (G?.farm?.blds == null) return done;
            double now = Now();
            foreach (var b in G.farm.blds) if (!b.done && now >= b.at) { b.done = true; done.Add(b); }
            if (done.Count > 0) SaveGame();
            return done;
        }

        public static string BuildLeftText(SaveData.Bld b) => Mmss(Math.Max(0, (long)Math.Ceiling((b.at - Now()) / 1000)));

        // ===== 버섯 나무 =====
        public static int TreeLv() => G?.farm?.tree == null ? 1 : G.farm.tree.lv;
        public static double FruitSeconds(string kind) => FRUITS[kind].time * Math.Pow(TREE.speedPerLv, TreeLv() - 1) * NF.gm_farm(Lv("gm_farm"))
            * (1 - BStat("grow")) * Math.Max(0.2, 1 - Fx("grow"));
        public static SaveData.Fruit NewFruit()
        {
            string kind = U.PickWeighted(FRUITS.Keys.ToList(), k => FRUITS[k].w);
            double now = Now();
            return new SaveData.Fruit { kind = kind, t0 = now, at = now + FruitSeconds(kind) * 1000 };
        }
        // 나무 레벨에 맞게 버섯 자리를 채운다 (새 자리는 바로 자라기 시작)
        public static void TreeEnsure()
        {
            var sl = G.farm.tree.slots; int n = TREE.Slots(TreeLv());
            while (sl.Count < n) sl.Add(NewFruit());
            if (sl.Count > n) sl.RemoveRange(n, sl.Count - n);
        }
        public static bool FruitRipe(SaveData.Fruit f) => f != null && Now() >= f.at;
        public static int TreeRipeCount() => G?.farm?.tree?.slots == null ? 0 : G.farm.tree.slots.Count(FruitRipe);
        public static double TreeYieldMul() => 1 + TREE.yieldPerLv * (TreeLv() - 1);

        // 버섯 따기: 균사석 (나무 레벨·보유 효과·건물·노드, 소수점은 확률로) + 포자 (0~1 + 보너스)
        public static (int gem, int spore) PickYield(string kind, string critter)
        {
            double mul = TreeYieldMul() * (1 + BStat("fieldGem") + Fx("fieldGem") + (NF.gm_gourmet(Lv("gm_gourmet")) - 1)
                         + (critter != null && CRITTER_FX[critter].key == "fieldGemSelf" ? FxOf(critter) : 0));
            double g = FRUITS[kind].gem * mul;
            int gem = (int)Math.Floor(g) + (U.Chance((float)(g - Math.Floor(g))) ? 1 : 0);
            int sp = U.RandI(0, 1) + (U.Chance((float)NF.gm_spore(Lv("gm_spore"))) ? 2 : 0) + (U.Chance(BStat("spore") + Fx("spore")) ? 1 : 0);
            return (gem, sp);
        }

        // 딴 자리에는 새 버섯이 바로 자라기 시작한다
        public static (int gem, int spore)? PickFruit(int slot, string critter)
        {
            var sl = G.farm.tree.slots;
            if (slot < 0 || slot >= sl.Count || !FruitRipe(sl[slot])) return null;
            var y = PickYield(sl[slot].kind, critter);
            G.gem += y.gem; G.spore += y.spore;
            sl[slot] = NewFruit();
            return y;
        }

        public static double TreeUpCost() => TreeLv() >= TREE.maxLv ? 0 : TREE.Cost(TreeLv());
        public static bool TreeUp()
        {
            double c = TreeUpCost();
            if (c <= 0 || G.gold < c) return false;
            G.gold -= c; G.farm.tree.lv++;
            TreeEnsure(); SaveGame();
            return true;
        }

        // ===== 균사석 가속 =====
        // 나무: 아직 자라는 버섯을 모두 지금 다 자라게 (남은 시간 합 TREE.accelSec초마다 균사석 1개)
        public static int TreeAccelCost()
        {
            double now = Now(), left = 0;
            foreach (var f in G.farm.tree.slots) left += Math.Max(0, f.at - now) / 1000;
            return left <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(left / TREE.accelSec));
        }
        public static bool TreeAccel()
        {
            int c = TreeAccelCost();
            if (c <= 0 || G.gem < c) return false;
            G.gem -= c; double now = Now();
            foreach (var f in G.farm.tree.slots) if (f.at > now) f.at = now;
            SaveGame();
            return true;
        }
        // 건물: 지금 바로 완성 (남은 시간 BUILD_ACCEL_SEC초마다 균사석 1개)
        public static int BuildAccelCost(SaveData.Bld b) { double left = Math.Max(0, (b.at - Now()) / 1000); return b.done || left <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(left / BUILD_ACCEL_SEC)); }
        public static bool BuildAccel(int uid)
        {
            var b = G.farm.blds.FirstOrDefault(x => x.uid == uid);
            if (b == null) return false;
            int c = BuildAccelCost(b);
            if (c <= 0 || G.gem < c) return false;
            G.gem -= c; b.at = Now();
            SaveGame();
            return true;
        }

        public static string TimeText(double s) => s >= 3600 ? $"{(int)(s / 3600)}시간 {U.JsRound(s % 3600 / 60)}분" : s >= 60 ? $"{U.JsRound(s / 60)}분" : $"{Math.Ceiling(s)}초";

        // ===== 자동 수확 보상 =====
        public static void AutoEnsure() { if (G.autoT <= 0) G.autoT = Now(); FixClock(); }

        // 기기 시계가 앞서 있다가 뒤로 맞춰지면 미래 시각에 멈춘 타이머를 지금 기준으로 당긴다 (남은 시간은 그대로). 정상 시계면 아무것도 안 함
        public static void FixClock()
        {
            if (G?.farm == null) return;
            double now = Now();
            if (G.autoT > now) G.autoT = now;
            if (G.farm.tree?.slots != null)
                foreach (var f in G.farm.tree.slots) if (f.t0 > now) { f.at = now + (f.at - f.t0); f.t0 = now; }
            foreach (var b in G.farm.blds) if (!b.done && b.t0 > now) { b.at = now + (b.at - b.t0); b.t0 = now; }
            double maxWait = now + Math.Max(FARM.first, FARM.wait[1]) * 1000;   // 부탁 대기는 이보다 길 수 없다
            foreach (var id in new List<string>(G.farm.next.Keys)) if (G.farm.next[id] > maxWait) G.farm.next[id] = maxWait;
        }
        public static double AutoHours() => G == null || G.autoT <= 0 ? 0 : Math.Min(AUTO.maxHours, Math.Max(0, (Now() - G.autoT) / 3600000.0));
        public static double AutoMul() => (1 + BStat("auto")) * NF.gm_herb(Lv("gm_herb"));
        // 시간당 보상
        public static (double gold, double gem, double dia) AutoRates()
        {
            double m = AutoMul();
            double gold = Math.Max(AUTO.goldMin, StageGold(StageNow()) * AUTO.goldStageK) * (1 + Fx("autoGold")) * m;
            return (gold, AUTO.gemH * (1 + Fx("autoGem")) * m, AUTO.diaH * m);
        }
        public static (double gold, int gem, int dia) AutoReward()
        {
            var r = AutoRates(); double h = AutoHours();
            return (Math.Floor(r.gold * h), (int)Math.Floor(r.gem * h), (int)Math.Floor(r.dia * h));
        }
        public static bool AutoReady() => AutoHours() * 3600 >= AUTO.minClaim;

        public static (double gold, int gem, int dia)? ClaimAuto()
        {
            if (!AutoReady()) return null;
            var r = AutoReward();
            G.gold += r.gold; G.gem += r.gem; G.dia += r.dia; G.autoT = Now();
            SaveGame();
            return (r.gold, r.gem, r.dia);
        }
    }
}
