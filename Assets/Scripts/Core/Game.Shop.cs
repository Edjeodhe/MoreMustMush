using System;
using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;
using Quest = MoreMush.SaveData.Quest;

namespace MoreMush
{
    // Mushroom shop (sell · village quests · spore shop) and workshop rules (prototype "버섯 상점", "마을 의뢰",
    // "포자 상점", "공방" sections).
    public static partial class Game
    {
        // ===== 판매 =====
        // 판매해도 도감 기록·별(G.codex)은 줄지 않는다. 창고(G.inv)만 줄어든다.
        public static List<Species> ShopList(string tab) => SPECIES
            .Where(sp => InvCount(sp.id) >= 1 && (tab == "all" || sp.c == tab))
            .OrderBy(sp => Array.IndexOf(CAT_KEYS, sp.c)).ThenByDescending(sp => sp.t).ThenBy(sp => sp.idx).ToList();

        // 팔고 받은 (개수, 골드)
        public static (double n, double gold) SellMush(IEnumerable<(string id, double cnt)> ids)
        {
            double pm = PriceMul(), gold = 0, n = 0;
            foreach (var (id, cnt) in ids)
            {
                double k = Math.Min(Math.Floor(cnt), InvCount(id));
                if (k < 1) continue;
                G.inv[id] -= k; if (G.inv[id] < 1e-9) G.inv.Remove(id);
                gold += k * UnitPrice(SP[id], pm); n += k;
            }
            if (n == 0) return (0, 0);
            gold = Math.Floor(gold);
            G.gold += gold;
            SaveGame();
            return (n, gold);
        }

        // ===== 마을 의뢰 =====
        // 주민이 특정 버섯을 원한다. 판매가의 QUEST_MUL배 골드를 준다. 3개씩, QUEST_EVERY 라운드마다 새 의뢰.
        public static Npc QuestNpc(Quest q) => NPC.TryGetValue(q.npc ?? "", out var n) ? n : NPCS[0];

        static Quest MakeQuest()
        {
            var pool = SPECIES.Where(sp => Harvested(sp.id) && !G.quests.Any(q => q.id == sp.id)).ToList();
            if (pool.Count == 0) return null;
            var sp = U.Pick(pool);
            double cnt = Math.Max(1, Math.Ceiling(QUEST_N[sp.t] * Math.Pow(1.1, StageNow() - 1) * U.Rand(0.8f, 1.3f)));
            var busy = G.quests.Select(q => QuestNpc(q).id).ToList();   // 같은 주민이 겹치지 않게
            var free = NPCS.Where(n => !busy.Contains(n.id)).ToList();
            var npc = U.Pick(free.Count > 0 ? free : NPCS);
            return new Quest { id = sp.id, cnt = cnt, npc = npc.id, line = U.RandI(0, npc.lines.Length - 1) };
        }

        public static void RefreshQuests()
        {
            G.quests = G.quests.Where(q => SP.ContainsKey(q.id)).ToList();
            while (G.quests.Count < 3) { var q = MakeQuest(); if (q == null) break; G.quests.Add(q); }
        }

        public static double QuestMul() => QUEST_MUL * NF.ed_trade(Lv("ed_trade"));
        public static double QuestReward(Quest q) => Math.Floor(q.cnt * UnitPrice(SP[q.id], PriceMul()) * QuestMul());
        public static bool QuestReady(Quest q) => InvCount(q.id) >= q.cnt;
        public static int QuestReadyCount() => G?.quests == null ? 0 : G.quests.Count(QuestReady);

        // 받은 골드. 못 하면 null
        public static double? DoQuest(int i)
        {
            if (i < 0 || i >= G.quests.Count || !QuestReady(G.quests[i])) return null;
            var q = G.quests[i];
            double g = QuestReward(q);
            G.inv[q.id] -= q.cnt;
            G.gold += g;
            G.quests.RemoveAt(i); RefreshQuests();
            SaveGame();
            return g;
        }

        // 일괄 완료: 지금 전달할 수 있는 의뢰를 전부 (새로 들어온 의뢰도 바로 가능하면 계속)
        public static (int n, double gold) QuestAll()
        {
            int n = 0; double gold = 0;
            for (int guard = 0; guard < 50; guard++)
            {
                int i = G.quests.FindIndex(QuestReady);
                if (i < 0) break;
                var q = G.quests[i]; double g = QuestReward(q);
                G.inv[q.id] -= q.cnt; G.gold += g; gold += g; n++;
                G.quests.RemoveAt(i); RefreshQuests();
            }
            if (n > 0) SaveGame();
            return (n, gold);
        }

        // ===== 포자 상점 =====
        // 버섯 포자를 골드로 산다. 값은 스테이지에 맞춰 오른다 (진행도에 비례)
        public static double SporePrice() => Math.Max(20, Math.Ceiling(StageGold(StageNow()) * 0.15 / 5) * 5);
        public static double SporeMax() => Math.Floor(G.gold / SporePrice());

        public static bool BuySpore(double n)
        {
            double pr = SporePrice();
            if (n < 1 || G.gold < n * pr) return false;
            G.gold -= n * pr; G.spore += n;
            SaveGame();
            return true;
        }

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
            G.gold -= h.unlockGold; G.hv[id] = 1; G.hvOn[id] = true;
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

        // 켜고 끄기. 마지막 하나는 끌 수 없다 (false)
        public static bool HvToggle(string id)
        {
            if (HvStar(id) == 0) return true;
            bool on = HvOn(id);
            if (on && ActiveHvs().Count <= 1) return false;
            G.hvOn[id] = !on;
            SaveGame();
            return true;
        }

        public static double WeatherChance(Weather w)
        {
            if (!WeatherOpen(w)) return 0;
            return w.p / WEATHERS.Where(WeatherOpen).Sum(x => x.p);
        }
        public static string FavText(Weather w) =>
            w.fav != null ? string.Join(", ", w.fav) + $" ×{U.FmtN(w.favMul)}" : w.favTier >= 0 ? $"{TIERS[w.favTier].name} 전체 ×{U.FmtN(w.favMul)}" : "-";
    }
}
