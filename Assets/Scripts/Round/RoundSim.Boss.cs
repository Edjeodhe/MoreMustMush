using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 보스 라운드 (전투 기획서 Ver05 · 03. 보스 기믹). 군락지는 평소처럼 나오고, 필드 위쪽 가운데에 보스가 선다.
    // 제한 시간 안에 보스(환상 버섯은 분신까지)를 모두 쓰러뜨리면 클리어. 클리어해도 라운드는 시간 끝까지 이어진다.
    public partial class RoundSim
    {
        public readonly List<Shroom> bosses = new List<Shroom>();   // 본체 + 분신. 죽은 것도 남겨 둔다 (클리어 판정·화면용)
        public bool bossCleared;
        // 보스 등장 연출 시계 (보스가 나온 뒤 흐른 시간, 없으면 -1). 화면이 BOSS_TITLE_AT에 "보스 등장!" 글자를 띄우고, 글자가 박히는 순간 화면이 흔들린다
        public float bossIntroT = -1;
        public const float BOSS_IN_DUR = 0.5f, BOSS_TITLE_AT = 0.15f, BOSS_TITLE_LAND = 0.2f;
        // 보스 실루엣(#463765)이 걷히는 구간 (등장 시계 기준): REVEAL_AT까지 실루엣, BOSS_IN_DUR에 원래 색
        public const float BOSS_REVEAL_AT = 0.2f;
        // 격파: 마지막 보스가 쓰러지면 게임이 멈추고 BOSS_DEATH_DUR 동안 격파 연출, BOSS_POP_AT에 보스가 터지고(기존 연출),
        // 끝나면 보스·연출을 모두 지우고 "수확 끝!" (사용자 2026-11-11)
        public const float BOSS_DEATH_DUR = 0.5f, BOSS_POP_AT = 0.3f;
        public float bossDeathT = -1;   // 격파 연출 시계 (없으면 -1)
        Shroom bossDying;

        // 보스 몸의 실루엣 정도 (1 = #463765로 덮임, 0 = 원래 색). 분신·늦게 나온 몸은 0
        public float BossDark(Shroom m) => !m.boss || m.bossClone || bossIntroT < 0 ? 0 : 1 - Mathf.Clamp01((bossIntroT - BOSS_REVEAL_AT) / (BOSS_IN_DUR - BOSS_REVEAL_AT));
        public float bossClearT;   // 클리어 연출 시각 (runT)
        float bossHealT, bossPurgeT;
        int bossSplits;
        float moverDir = 1;
        readonly Shape bossArea = Shape.C(0, 0, 0), moverLane = Shape.Rect(0, 0, 0, 0);

        public BossDef Boss => st.boss;
        public Shroom BossMain => bosses.Count > 0 ? bosses[0] : null;
        public bool BossRound => st.boss != null;
        public bool BossFailed => BossRound && !bossCleared;

        void SpawnBoss(BossDef def)
        {
            float x = WW / 2, y = FY0 + (NO_SPAWN_Y - FY0) * 0.3f;
            double hp = st.bossHp;
            bosses.Add(MakeBossBody(def, x, y, hp, false));
            bossHealT = CLEANER_INT; bossPurgeT = ANGEL_PURGE_INT;
            // 등장 연출: 보스 뒤 플립북 0.3초 + 0.15초 뒤 화면 앞 "보스 등장!" 글자 그림 (예전 "보스 {0} 등장!" 글자 대신, 사용자 2026-11-11)
            bossIntroT = 0;
            AddFx(FxShape.BossIn, x, y, BOSS_R * 2 * 2.6f, BOSS_IN_DUR, Color.white, 0, 0);
            AddLabel(def.gimmick, x, y - BOSS_R - 20, def.col, 24);
        }

        Shroom MakeBossBody(BossDef def, float x, float y, double hp, bool clone)
        {
            var m = new Shroom
            {
                sp = SP_BY_NAME[def.look], x = x, y = y, r = clone ? BOSS_R * 0.8f : BOSS_R, hp = hp, maxHp = hp,
                boss = true, bossClone = clone, bossDef = def, solid = true, grow = clone ? 0.3f : 1,
            };
            shrooms.Add(m);
            return m;
        }

        // 군락지·장치·떠돌이 군락이 보스 자리(움직이는 버섯은 가로 길 전체)를 피한다
        bool BossBlocks(Shape shape) => BossBlocker(shape) != null;

        // 겹친 보스 자리(재사용 도형, 다음 호출에서 바뀜). 없으면 null
        Shape BossBlocker(Shape shape)
        {
            foreach (var b in bosses)
            {
                if (b.dead) continue;
                bossArea.x = b.x; bossArea.y = b.y; bossArea.r = b.r;
                if (Overlap(shape, bossArea, 10)) return bossArea;
            }
            if (st.boss != null && st.boss.id == "mover" && BossMain != null && !BossMain.dead)
            {
                var m = BossMain;
                moverLane.x0 = FX0; moverLane.x1 = FX1; moverLane.y0 = m.y - m.r; moverLane.y1 = m.y + m.r;
                if (Overlap(shape, moverLane, 10)) return moverLane;
            }
            return null;
        }

        void UpdateBoss(float h)
        {
            if (!BossRound || bossCleared) return;
            var main = BossMain;
            switch (st.boss.id)
            {
                case "cleaner":
                    bossHealT -= h;
                    if (bossHealT <= 0)
                    {
                        bossHealT += CLEANER_INT;
                        if (!main.dead && main.hp < main.maxHp)
                        {
                            main.hp = System.Math.Min(main.maxHp, main.hp + main.maxHp * CLEANER_HEAL);
                            AddLabel(Loc.F("+{0}% 회복", Mathf.RoundToInt(CLEANER_HEAL * 100)), main.x, main.y - main.r - 40, "#7ad07a", 26);
                            for (int i = 0; i < 10; i++) AddPart(main.x + U.Rand(-main.r, main.r) * 0.6f, main.y, U.Rand(-30, 30), U.Rand(-160, -60), 0.6f, U.Hex("#9ff09a"), 5);
                        }
                    }
                    break;
                case "angel":
                    // ANGEL_PURGE_INT초마다 필드의 임시 수확기(분열·분신·꼬마 수확기 등)를 모두 없앤다. 영구 수확기는 그대로
                    bossPurgeT -= h;
                    if (bossPurgeT <= 0 && !main.dead)
                    {
                        bossPurgeT += ANGEL_PURGE_INT;
                        int n = 0;
                        foreach (var b in balls)
                        {
                            if (b.perm || b.dead) continue;
                            b.dead = true; n++;
                            for (int i = 0; i < 6; i++) AddPart(b.x, b.y, U.Rand(-120, 120), U.Rand(-160, -20), 0.45f, U.Hex(i % 2 == 0 ? st.boss.col : "#ffffff"), 4);
                        }
                        if (n > 0)
                        {
                            AddLabel(Loc.F("임시 수확기 {0}개 소멸!", n), main.x, main.y - main.r - 40, st.boss.col, 28);
                            Snd.Tone(110, 0.4f, Snd.Wave.Sine, 0.3f, 0.5f, "puff", 0.1f);
                        }
                    }
                    break;
                case "mover":
                    if (!main.dead)
                    {
                        main.x += moverDir * MOVER_SPD * h;
                        if (main.x - main.r < FX0) { main.x = FX0 + main.r; moverDir = 1; }
                        else if (main.x + main.r > FX1) { main.x = FX1 - main.r; moverDir = -1; }
                    }
                    break;
            }
        }

        // 환상 버섯: 본체 체력이 66%·33%를 지날 때마다 분신 하나 (본체의 지금 체력을 복제, 분신은 분신을 부르지 않음)
        void BossDamaged(Shroom m)
        {
            if (st.boss.id != "phantom" || m.bossClone || m.dead || m.hp <= 0) return;   // 한 방에 쓰러지면 분신 없음
            while (bossSplits < PHANTOM_SPLIT.Length && m.hp <= m.maxHp * PHANTOM_SPLIT[bossSplits])
            {
                bossSplits++;
                var p = PhantomSpot(m);
                var c = MakeBossBody(st.boss, p.x, p.y, m.hp, true);
                bosses.Add(c);   // 분신도 보스 목록에 넣는다: 본체·분신이 모두 쓰러져야 클리어 (BossDown), 이름·체력 글자도 붙는다
                AddLabel(Loc.T("분신 소환!"), c.x, c.y - c.r - 30, st.boss.col, 30);
                for (int i = 0; i < 24; i++) AddPart(c.x, c.y, U.Rand(-220, 220), U.Rand(-260, -40), 0.6f, U.Hex(i % 2 == 0 ? st.boss.col : "#ffffff"), 6);
                shake = Mathf.Max(shake, 0.4f); Snd.Rare();
            }
        }

        // 분신 자리: x는 본체와 다르고(한 몸 이상 떨어짐), y는 본체보다 위
        Vector2 PhantomSpot(Shroom m)
        {
            float r = BOSS_R * 0.8f;
            for (int a = 0; a < 30; a++)
            {
                float side = U.Chance(0.5f) ? 1 : -1;
                float x = m.x + side * U.Rand(m.r + r + 20, m.r + r + 420), y = m.y - U.Rand(40, 160);
                if (x - r < FX0 || x + r > FX1 || y - r < FY0) continue;
                bool hit = false;
                foreach (var b in bosses) if (!b.dead && U.D2(x, y, b.x, b.y) < (b.r + r) * (b.r + r)) { hit = true; break; }
                if (!hit) return new Vector2(x, y);
            }
            float fx = m.x < WW / 2 ? Mathf.Min(FX1 - r, m.x + m.r + r + 40) : Mathf.Max(FX0 + r, m.x - m.r - r - 40);
            return new Vector2(fx, Mathf.Max(FY0 + r, m.y - 60));
        }

        // 보스 처치 보상의 버섯: 이번 판에 딴 버섯을 종류별로 BOSS_REWARD_MUL배 더 (도감 수확 수·별 진행에 들어간다)
        int BossShroomReward()
        {
            int added = 0;
            foreach (var kv in picks)
            {
                if (!SP.TryGetValue(kv.Key, out var sp)) continue;
                int extra = (int)(kv.Value * BOSS_REWARD_MUL);
                if (extra <= 0) continue;
                int star0 = StarOf(sp.id);
                if (!G.codex.TryGetValue(sp.id, out var e)) e = G.codex[sp.id] = new SaveData.CodexEntry();
                e.n += extra; added += extra;
                int star1 = StarOf(sp.id);
                if (star1 <= star0) continue;
                newStars.RemoveAll(s => s.Item1 == sp.id);   // 라운드 중에 이미 오른 종은 카드 한 장만
                newStars.Add((sp.id, star1));
            }
            foreach (var c in CAT_KEYS) gains[c] *= 1 + BOSS_REWARD_MUL;
            return added;
        }

        // 레전더리 버섯은 보스 전용: 보스를 쓰러뜨리면 보스 분류의 레전더리 BOSS_LEGEND_N개 (은둔 고수가 수확량을 올린다)
        void BossLegendDrop(Shroom m)
        {
            var pool = SPECIES.Where(s => s.t == 3 && s.c == st.boss.cat).ToList();
            if (pool.Count == 0) pool = SPECIES.Where(s => s.t == 3).ToList();
            if (pool.Count == 0) return;
            var sp = U.Pick(pool);
            double drop = BOSS_LEGEND_N * st.harvestMul * st.handMul[3];
            gains[sp.c] += drop;
            bag[sp.id] = (bag.TryGetValue(sp.id, out var bv) ? bv : 0) + drop;
            picks[sp.id] = (picks.TryGetValue(sp.id, out var pc) ? pc : 0) + BOSS_LEGEND_N;
            harvests += BOSS_LEGEND_N; killT[3] += BOSS_LEGEND_N;
            if (!G.codex.TryGetValue(sp.id, out var e)) e = G.codex[sp.id] = new SaveData.CodexEntry();
            if (e.n <= 0) { e.first = G.rounds + 1; newSpecies.Add(sp.id); codexShake = 1; }
            int star0 = StarOf(sp.id);
            e.n += BOSS_LEGEND_N;
            int star1 = StarOf(sp.id);
            if (star1 > star0) newStars.Add((sp.id, star1));
            AddLabel(Loc.F("레전더리 {0} ×{1}!", sp.n, BOSS_LEGEND_N), m.x, m.y - m.r - 70, TIERS[3].color, 32);
            for (int i = 0; i < 6 && flyers.Count < 220; i++)
                flyers.Add(new Flyer { x = m.x, y = m.y, sx = m.x, sy = m.y, t = -i * 0.05f, dur = U.Rand(0.5f, 0.8f), sp = sp, cat = sp.c });
        }

        void BossDown(Shroom m, Ball b)
        {
            if (m.dead || m == bossDying) return;
            int left = 0; bool mainLeft = false;
            foreach (var x in bosses) if (!x.dead && x != m) { left++; if (!x.bossClone) mainLeft = true; }
            if (left > 0 || bossCleared)
            {
                // 분신·본체가 남았다: 그 몸만 바로 터진다 (격파 플립북 + 기존 터짐)
                AddFx(FxShape.BossOut, m.x, m.y, m.r * 2 * 3f, BOSS_DEATH_DUR, Color.white, 0, 0);
                BossPop(m);
                ComboHit();
                if (bossCleared) return;   // 이미 클리어한 뒤에 남은 몸을 쓰러뜨려도 다시 클리어하지 않는다
                Snd.Rare();
                AddLabel(mainLeft ? Loc.F("본체가 남았어요! (남은 {0})", left) : Loc.F("분신이 {0}마리 남았어요!", left), m.x, m.y - m.r - 30, "#ffffff", 28);
                return;
            }
            // 마지막 보스: 게임이 멈추고 보스 뒤에 격파 플립북. 터짐·보상은 BOSS_POP_AT에, 정리와 "수확 끝!"은 BOSS_DEATH_DUR에 (StepBossDeath)
            m.hp = 0;
            bossDying = m; bossDeathT = 0;
            bossCleared = true; bossClearT = runT;
            AddFx(FxShape.BossOut, m.x, m.y, m.r * 2 * 3f, BOSS_DEATH_DUR, Color.white, 0, 0);
            ComboHit();
            shake = Mathf.Max(shake, 0.4f);
            Snd.Record();
        }

        // 보스가 터지는 기존 연출: 조각이 사방으로, 보스 색 고리
        void BossPop(Shroom m)
        {
            m.dead = true; m.hp = 0;
            var col = U.Hex(st.boss.col);
            for (int i = 0; i < 50; i++) { float a = U.Rand(0, U.TAU), s = U.Rand(80, 600); AddPart(m.x, m.y, Mathf.Cos(a) * s, Mathf.Sin(a) * s - 60, U.Rand(0.3f, 0.8f), i % 3 != 0 ? col : Color.white, 7); }
            rings.Add(new Ring { x = m.x, y = m.y, r = m.r * 2.4f, dur = 0.45f, col = st.boss.col });
        }

        // 격파 연출 동안: 게임(시간·수확기·버섯)은 멈추고 연출만 흐른다 (SimFrame이 부른다)
        void StepBossDeath(float h)
        {
            float was = bossDeathT; bossDeathT += h;
            if (was < BOSS_POP_AT && bossDeathT >= BOSS_POP_AT)
            {
                BossPop(bossDying);
                BossLegendDrop(bossDying);
                shake = 1; flash = Mathf.Max(flash, 0.35f);
            }
            if (bossDeathT < BOSS_DEATH_DUR) return;
            // 끝: 보스도 다른 연출도 없는 화면에서 "수확 끝!" (끝 연출 1.5초 뒤 정산, 시간이 다 된 때와 같음)
            bossDeathT = -1; bossDying = null;
            shrooms.RemoveAll(x => x.dead);
            foreach (var f in fxs) fxFree.Push(f);
            fxs.Clear();
            foreach (var p in parts) partFree.Push(p);
            parts.Clear();
            rings.Clear(); labels.Clear(); texts.Clear(); flyers.Clear(); bolts.Clear(); embers.Clear();
            feverMsg = null; shake = 0; flash = 0;
            if (phase == "run") { phase = "end"; endT = 1.5f; festOn = false; }
        }
    }
}
