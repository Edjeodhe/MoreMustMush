using System;
using System.Linq;
using static MoreMush.Defs;

namespace MoreMush
{
    // Workshop rules (prototype "공방" section): harvester unlock · level · on/off, and the weather table.
    // 버섯 상점(판매·마을 의뢰·포자 상점)은 v3에서 없어졌다.
    public static partial class Game
    {
        // ===== 공방 =====
        public static Cost? HvLevelCost(Harvester h)
        {
            int L = G.hv.TryGetValue(h.id, out var s) ? s : 0;
            if (L == 0 || L >= 5) return null;
            return new Cost { gold = Math.Ceiling((h.lvGold > 0 ? h.lvGold : h.unlockGold) * HV_LV_MUL[L]) };
        }
        public static int HvStar(string id) => G.hv.TryGetValue(id, out var s) ? s : 0;
        public static bool HvOn(string id) => HvStar(id) > 0 && (!G.hvOn.TryGetValue(id, out var on) || on);

        public static bool HvUnlock(string id)
        {
            var h = HV[id];
            if (HvStar(id) > 0 || h.unlockGold < 0 || G.gold < h.unlockGold) return false;
            // 빈 장착 칸이 있을 때만 바로 켠다 (칸을 넘기면 라운드를 시작할 수 없으니)
            bool room = OnHvs().Count < HvSlots();
            G.gold -= h.unlockGold; G.hv[id] = 1; G.hvOn[id] = room;
            SaveGame();
            return true;
        }

        public static bool HvLevel(string id)
        {
            var c = HvLevelCost(HV[id]);
            if (c == null || !CanAfford(c.Value)) return false;
            Pay(c.Value); G.hv[id]++;
            SaveGame();
            return true;
        }

        // 켜고 끄기. 다 꺼도 빈 칸은 기본 수확기가 채운다. 장착 칸을 넘겨 켤 수는 있지만 라운드 시작이 막힌다 (HvOver)
        public static void HvToggle(string id)
        {
            if (HvStar(id) == 0) return;
            G.hvOn[id] = !HvOn(id);
            SaveGame();
        }

        public static double WeatherChance(Weather w)
        {
            if (!WeatherOpen(w)) return 0;
            return w.p / WEATHERS.Where(WeatherOpen).Sum(x => x.p);
        }
        public static string FavText(Weather w) =>
            w.fav != null ? string.Join(", ", w.fav.Select(n => SP_BY_NAME[n].n)) + $" ×{U.FmtN(w.favMul)}" : w.favTier >= 0 ? Loc.F("{0} 전체 ×{1}", TIERS[w.favTier].name, U.FmtN(w.favMul)) : "-";
    }
}
