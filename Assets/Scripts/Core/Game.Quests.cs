using System;
using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;
using QuestProg = MoreMush.SaveData.QuestProg;

namespace MoreMush
{
    // Daily · weekly quests (시스템 기획서 03. 의뢰 시스템). 완료하면 세계수 나무 경험치를 받는다.
    public static partial class Game
    {
        // KST 날짜 번호 (1970-01-01 = 0). 실제 시각(ms, UTC)에 9시간을 더해 자정을 맞춘다
        public static long KstDay(double ms) => (long)Math.Floor((ms + KST_OFFSET_MS) / DAY_MS);
        // 월요일에 시작하는 주 번호. 1970-01-01은 목요일이라 +3을 하면 월요일마다 바뀐다
        public static long KstWeek(long day) => (long)Math.Floor((day + 3) / 7.0);

        // 날짜가 바뀌었으면 의뢰를 새로 뽑는다. 화면을 열 때·라운드가 끝날 때 부른다
        public static void QuestEnsure()
        {
            var q = G.quest;
            long day = KstDay(Now()), week = KstWeek(day);
            // 앞으로 갈 때만 새로 뽑는다 (기기 시계를 되돌려 보상을 다시 받지 못하게)
            if (day > q.day) { q.day = day; q.daily = RollDaily(); }
            else if (q.daily.Count < DAILY_COUNT) q.daily.AddRange(RollDaily(q.daily).Take(DAILY_COUNT - q.daily.Count));   // 로드 때 지워진 의뢰 채우기
            if (week > q.week) { q.week = week; q.weekly = WEEKLY_QUESTS.Select(d => new QuestProg { id = d.id }).ToList(); }
        }

        static List<QuestProg> RollDaily(List<QuestProg> keep = null)
        {
            var pool = DAILY_QUESTS.Where(d => keep == null || keep.All(k => k.id != d.id)).ToList();
            var known = SPECIES.Where(sp => Harvested(sp.id)).ToList();
            if (known.Count == 0) pool.RemoveAll(d => d.kind == "species");   // 아직 딴 버섯이 없으면 지정 버섯 의뢰는 빼고
            var picked = new List<QuestProg>();
            while (picked.Count < DAILY_COUNT && pool.Count > 0)
            {
                var d = U.Pick(pool); pool.Remove(d);
                picked.Add(new QuestProg { id = d.id, arg = d.kind == "species" ? U.Pick(known).id : null });
            }
            return picked;
        }

        public static QuestDef QuestDefOf(QuestProg p) => QUEST.TryGetValue(p.id ?? "", out var d) ? d : null;
        public static bool QuestDone(QuestProg p) => QuestDefOf(p) is QuestDef d && p.prog >= d.target;
        public static bool QuestClaimable(QuestProg p) => !p.claimed && QuestDone(p);
        public static int QuestClaimableCount() => G?.quest == null ? 0 : G.quest.daily.Concat(G.quest.weekly).Count(QuestClaimable);

        public static string QuestText(QuestProg p)
        {
            var d = QuestDefOf(p);
            if (d == null) return "";
            string m = p.arg != null && SP.TryGetValue(p.arg, out var sp) ? sp.n : "";
            return d.text.Replace("{n}", U.Fmt(d.target)).Replace("{m}", m);
        }

        // 라운드 결과로 진행도를 올린다. picks: 이번 판 버섯별 수확 횟수
        public static void QuestOnRound(int harvests, IReadOnlyDictionary<string, int> picks, int maxCombo)
        {
            QuestEnsure();
            foreach (var p in G.quest.daily.Concat(G.quest.weekly))
            {
                var d = QuestDefOf(p);
                if (d == null || p.claimed) continue;
                int add = d.kind switch
                {
                    "rounds" => 1,
                    "harvest" => harvests,
                    "species" => p.arg != null && picks.TryGetValue(p.arg, out var n) ? n : 0,
                    "combo" => maxCombo >= d.target ? d.target : 0,
                    _ => 0,
                };
                p.prog = (int)Math.Min(d.target, (long)p.prog + add);
            }
        }

        // 받은 경험치. 못 받으면 null. slot: "d0".."d2" 일일, "w0".."w2" 주간
        public static int? QuestClaim(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length < 2 || !int.TryParse(slot.Substring(1), out int i)) return null;
            var list = slot[0] == 'd' ? G.quest.daily : slot[0] == 'w' ? G.quest.weekly : null;
            if (list == null || i < 0 || i >= list.Count || !QuestClaimable(list[i])) return null;
            var p = list[i]; int exp = QuestDefOf(p).exp;
            p.claimed = true;
            G.farm.tree.exp += exp;
            Telemetry.QuestClear(p.id, slot[0] == 'd' ? "daily" : "weekly", exp);
            if (slot[0] == 'd')
                foreach (var w in G.quest.weekly)
                    if (!w.claimed && QuestDefOf(w)?.kind == "daily") w.prog = Math.Min(QuestDefOf(w).target, w.prog + 1);
            SaveGame();
            return exp;
        }
    }
}
