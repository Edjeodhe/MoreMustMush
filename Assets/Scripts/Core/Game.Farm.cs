using System;
using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;

namespace MoreMush
{
    // Mushroom farm rules (prototype "버섯 농장", "농장 꾸미기", "버섯 밭", "꼬마 식당" sections): critter requests,
    // hearts and evolution, decorations, field plots, recipes. Times are real-time epoch milliseconds like Date.now().
    // The screen state (critters walking, job and order queues) lives in FarmView.
    public static partial class Game
    {
        public static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ===== 목장 꼬마 =====
        public static List<Special> FarmOwned() => SPECIALS.Where(k => HasSpecial(k.id)).ToList();

        // 잡은 꼬마는 부탁 시각이 없으면 만들어 준다 (자동 입주)
        public static void FarmEnsure()
        {
            foreach (var k in FarmOwned())
                if (!G.farm.next.ContainsKey(k.id)) { G.farm.next[k.id] = Now() + FARM.first * 1000; G.farm.kind[k.id] = "water"; }
        }

        public static bool FarmReady(string id) => G.farm.next.TryGetValue(id, out var t) && Now() >= t;
        public static int FarmPetCount() => G?.farm == null ? 0 : FarmOwned().Count(k => FarmReady(k.id));
        public static int FarmReadyCount() => FarmPetCount() + FieldRipeCount();   // 균사 트리 버튼 알림: 꼬마 부탁 + 다 자란 작물

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
            G.farm.next[id] = Now() + U.Rand(FARM.wait[0], FARM.wait[1]) * FARM.evoWait[EvoOf(id)] * 1000;
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
        public static string Mmss(long s) => $"{s / 60}:{s % 60:00}";

        // 부탁 들어주기 → gem 기본, bonus 호감도 보너스, heart 새 칸을 채움, full 5칸 다 참 (화면 효과는 FarmView)
        public struct Serve { public int gem, bonus; public bool heart, full; }
        public static Serve? FarmServe(string id)
        {
            if (!FarmReady(id)) return null;
            int h0 = HeartsOf(id), gem = FarmReward(id);
            int bonus = U.Chance(FARM.bonusP * h0) ? U.RandI(1, 2 + EvoOf(id)) : 0;
            G.gem += gem + bonus;
            if (h0 < FARM.hearts) G.farm.love[id] = Love(id) + 1;
            FarmSchedule(id);
            int h1 = HeartsOf(id);
            return new Serve { gem = gem, bonus = bonus, heart = h1 > h0, full = h1 >= FARM.hearts && h0 < FARM.hearts };
        }

        public static bool FarmEvolve(string id)
        {
            if (!HasSpecial(id) || !CanEvolve(id)) return false;
            G.farm.evo[id] = EvoOf(id) + 1; G.farm.love[id] = 0;
            SaveGame();
            return true;
        }

        // ===== 농장 꾸미기 =====
        public static bool DecoOwn(string id) => G?.decor != null && G.decor.own.TryGetValue(id, out var b) && b;
        public static bool DecoOn(string id) => G?.decor != null && G.decor.on.TryGetValue(id, out var b) && b;

        public static bool BuyDeco(string id)
        {
            var d = DECO[id];
            if (DecoOwn(id) || G.gem < d.price) return false;
            G.gem -= d.price; G.decor.own[id] = true; G.decor.on[id] = true;
            SaveGame();
            return true;
        }

        public static void PlaceDeco(string id)
        {
            if (!DecoOwn(id)) return;
            G.decor.on[id] = !DecoOn(id);
            SaveGame();
        }

        // ===== 버섯 밭 =====
        public static Dictionary<string, double> CropNeed(string c) => new Dictionary<string, double>
        { ["spore"] = CROPS[c].spore, [c] = Math.Ceiling(CROPS[c].need * Math.Pow(1.1, StageNow() - 1)) };
        public static double CropTime(string c) => CROPS[c].time * NF.gm_farm(Lv("gm_farm"));
        public static bool CanPlant(string c) { var n = CropNeed(c); return G.spore >= n["spore"] && InvTotal(c) >= n[c]; }
        public static int FieldOff(int n) => (FIELD.max - n) / 2;
        public static bool FieldOpen(int r, int c) { int n = G.farm.size, o = FieldOff(n); return r >= o && r < o + n && c >= o && c < o + n; }
        public static bool TileRipe(SaveData.Plot p) => p != null && p.s == "grow" && Now() >= p.at;
        public static SaveData.Plot PlotAt(string key) => G.farm.plots.TryGetValue(key, out var p) ? p : null;
        public static IEnumerable<(int r, int c, string key)> FieldKeys()
        {
            int o = FieldOff(G.farm.size);
            for (int r = o; r < o + G.farm.size; r++) for (int c = o; c < o + G.farm.size; c++) yield return (r, c, r + "," + c);
        }
        public static int FieldRipeCount() => G?.farm?.plots == null ? 0 : G.farm.plots.Values.Count(TileRipe);

        public static double ExpandCost() => FIELD.expand.TryGetValue(G.farm.size, out var c) ? c : 0;
        public static bool FieldExpand()
        {
            double c = ExpandCost();
            if (c <= 0 || G.gold < c) return false;
            G.gold -= c; G.farm.size++; SaveGame();
            return true;
        }

        public static bool ToolUp()
        {
            if (G.farm.tool + 1 >= TOOLS.Length) return false;
            var T = TOOLS[G.farm.tool + 1];
            if (G.gold < T.cost) return false;
            G.gold -= T.cost; G.farm.tool++; SaveGame();
            return true;
        }

        public static string TimeText(double s) => s >= 3600 ? $"{(int)(s / 3600)}시간 {U.JsRound(s % 3600 / 60)}분" : s >= 60 ? $"{U.JsRound(s / 60)}분" : $"{Math.Ceiling(s)}초";

        // ===== 꼬마 식당 =====
        public static readonly Dictionary<string, (string n, string d, string col)> RECIPE_CAT = new Dictionary<string, (string, string, string)>
        {
            ["ed"] = ("식용 요리", "회복·버프", "#f28c28"),
            ["md"] = ("약용 요리", "치유·상태이상 해제", "#3fae4a"),
            ["ps"] = ("독 요리", "공격·디버프 부여", "#9b4fd1"),
            ["mix"] = ("상위 요리", "여러 버섯 + 균사석", "#2a9a74"),
        };

        // 요리 재료 개수: 분류 재료는 스테이지마다 ×1.1
        public static Dictionary<string, double> RecipeNeed(Recipe rc)
        {
            double m = Math.Pow(1.1, StageNow() - 1);
            return rc.need.ToDictionary(kv => kv.Key, kv => CATS.ContainsKey(kv.Key) ? Math.Ceiling(kv.Value * m) : kv.Value);
        }
        public static double NeedHave(string k) => CATS.ContainsKey(k) ? InvTotal(k) : k == "spore" ? G.spore : k == "gem" ? G.gem : k == "gold" ? G.gold : 0;
        public static bool DishOn(string id) => G.dishes.Contains(id);
        public static int DishRounds(Recipe rc) => rc.cat == "md" ? 1 + (int)NF.gm_herb(Lv("gm_herb")) : 1;
        public static double ChefMul(Recipe rc) => NF.md_chef(Lv("md_chef")) * (rc.cat == "ed" ? NF.gm_gourmet(Lv("gm_gourmet")) : 1);

        // 재료가 다 있나 (조리 중인지는 FarmView가 따로 본다)
        public static bool HasIngredients(Recipe rc) => RecipeNeed(rc).All(kv => NeedHave(kv.Key) >= kv.Value);

        public static void PayIngredients(Dictionary<string, double> need)
        {
            foreach (var kv in need)
            {
                if (CATS.ContainsKey(kv.Key)) ConsumeCat(kv.Key, kv.Value);
                else if (kv.Key == "spore") G.spore -= kv.Value;
                else if (kv.Key == "gem") G.gem -= kv.Value;
                else if (kv.Key == "gold") G.gold -= kv.Value;
            }
        }

        // 분류 재료는 판매가가 싼 버섯부터 쓴다
        public static void ConsumeCat(string cat, double n)
        {
            var list = SPECIES.Where(sp => sp.c == cat && InvCount(sp.id) > 0).OrderBy(sp => sp.t).ThenByDescending(sp => InvCount(sp.id)).ToList();
            foreach (var sp in list)
            {
                if (n <= 0) break;
                double k = Math.Min(n, InvCount(sp.id));
                G.inv[sp.id] -= k; n -= k;
            }
        }

        // 다 만든 요리를 상에 차린다 (다음 라운드에 먹는다)
        public static void ServeDish(string rid)
        {
            if (DishOn(rid)) return;
            G.dishes.Add(rid); G.dishLeft[rid] = DishRounds(RECIPE[rid]);
        }
    }
}
