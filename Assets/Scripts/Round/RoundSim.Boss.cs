using System.Collections.Generic;
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
        public float bossClearT;   // 클리어 연출 시각 (runT)
        float bossHealT, bossFogT;
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
            bossHealT = CLEANER_INT; bossFogT = 1.5f;
            AddLabel(Loc.F("보스 {0} 등장!", def.n), x, y - BOSS_R - 60, "#ffffff", 44);
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
                    bossFogT -= h;
                    if (bossFogT <= 0 && !main.dead)
                    {
                        bossFogT += FOG_INT;
                        // 보스 둘레 한 곳에 독 안개 (필드 아래쪽 바 근처까지는 내려오지 않는다)
                        float a = U.Rand(0, U.TAU), d = U.Rand(180, 520);
                        float fx = U.Clamp(main.x + Mathf.Cos(a) * d, FX0 + FOG_R, FX1 - FOG_R), fy = U.Clamp(main.y + Mathf.Abs(Mathf.Sin(a)) * d, FY0 + FOG_R, NO_SPAWN_Y - FOG_R * 0.5f);
                        clouds.Add(new Cloud { x = fx, y = fy, r = FOG_R, t = FOG_LIFE, max = FOG_LIFE, fog = true });
                        Snd.Tone(110, 0.4f, Snd.Wave.Sine, 0.3f, 0.5f, "puff", 0.1f);
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

        void BossDown(Shroom m, Ball b)
        {
            m.dead = true; m.hp = 0;
            var col = U.Hex(st.boss.col);
            for (int i = 0; i < 50; i++) { float a = U.Rand(0, U.TAU), s = U.Rand(80, 600); AddPart(m.x, m.y, Mathf.Cos(a) * s, Mathf.Sin(a) * s - 60, U.Rand(0.3f, 0.8f), i % 3 != 0 ? col : Color.white, 7); }
            rings.Add(new Ring { x = m.x, y = m.y, r = m.r * 2.4f, dur = 0.45f, col = st.boss.col });
            ComboHit();
            foreach (var x in bosses) if (!x.dead) { Snd.Rare(); AddLabel(Loc.T("분신이 남았어요!"), m.x, m.y - m.r - 30, "#ffffff", 28); return; }
            bossCleared = true; bossClearT = runT;
            hitstop = 0.35f; hitstopCd = 0.8f; flash = 0.9f; shake = 1;
            AddLabel(Loc.F("보스 {0} 처치!", st.boss.n), WW / 2, WH * 0.3f, "#fff7c2", 54);
            AddLabel(Loc.T("다음 스테이지가 열렸어요"), WW / 2, WH * 0.3f + 60, "#ffe36e", 28);
            clouds.RemoveAll(c => c.fog);
            Snd.Record();
        }
    }
}
