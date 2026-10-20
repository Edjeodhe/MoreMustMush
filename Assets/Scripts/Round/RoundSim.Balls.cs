using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    public partial class RoundSim
    {
        // ===== 핀볼 =====
        Ball MakeBall(float x, float y, bool perm, string hv = null, float life = 0, bool small = false)
        {
            hv ??= U.Pick(st.hvs);
            return new Ball
            {
                hv = hv, hs = st.hvStar.TryGetValue(hv, out var s) ? s : 1,
                x = x, y = y, spd = (float)st.launch, r = (float)st.ballR * (small ? 0.65f : 1) * (float)HV[hv].sizeMul, perm = perm, life = life, maxLife = life,
                bladeT = U.Rand(0, (float)st.bladeInt), ang = U.Rand(0, U.TAU),
            };
        }

        static void FixDir(Ball b)
        {
            float l = Mathf.Sqrt(b.dx * b.dx + b.dy * b.dy); if (l == 0) l = 1;
            b.dx /= l; b.dy /= l;
            if (Mathf.Abs(b.dy) < 0.12f)
            {
                b.dy = (b.dy < 0 ? -1 : 1) * 0.12f;
                float l2 = Mathf.Sqrt(b.dx * b.dx + b.dy * b.dy); b.dx /= l2; b.dy /= l2;
            }
        }

        List<Ball> SpawnTemp(float x, float y, int n, float life, bool small, float? baseAng)
        {
            var made = new List<Ball>();
            for (int i = 0; i < n; i++)
            {
                if (balls.Count >= MAX_BALLS) break;
                // 스킬로 생긴 수확기는 기본 수확기 모양의 분신 (공격력 CLONE_STAT 배)
                var b = MakeBall(x, y, false, BASE_HV, life * (float)st.tempLife, small);
                b.statMul = CLONE_STAT;
                float a = baseAng.HasValue ? baseAng.Value + (i - (n - 1) / 2f) * 0.4f + U.Rand(-0.15f, 0.15f) : U.Rand(0, U.TAU);
                b.dx = Mathf.Cos(a); b.dy = Mathf.Sin(a); FixDir(b);
                balls.Add(b); made.Add(b);
            }
            return made;
        }

        public void Launch(float ang)
        {
            int n = balls.Count;
            for (int i = 0; i < n; i++)
            {
                var b = balls[i];
                float off = n > 1 ? (-8 + 16f * i / (n - 1)) * Mathf.Deg2Rad : 0;
                b.dx = Mathf.Cos(ang + off); b.dy = Mathf.Sin(ang + off); FixDir(b);
                b.spd = (float)st.launch;
            }
            phase = "run";
            Snd.Launch();
            if (tutorial && !looseSpawned) { looseSpawned = true; SpawnLoose(TUTORIAL_LOOSE); }   // 튜토리얼 첫 스와이프: 낱개 버섯도 나온다
            if (G.rounds == 0) Telemetry.Ftue(Telemetry.FTUE_FIRST_LAUNCH);
        }

        // 수확기 한 개의 공격력 배율: 약화 구름 ×0.5, 분신 ×statMul, 그루터기 버프
        float Wk(Ball b)
        {
            if (b == null) return 1;
            float m = (b.weak > 0 ? 0.5f : 1) * b.statMul * (float)HV[b.hv].atkMul;
            if (b.stumpAtk > 0) m *= 1 + (STUMP_ATK_MUL - 1) * (float)st.devMul;
            if (b.powT > 0) m *= 1 + (float)(POWER_ATK * (1 + b.powLv) + st.powAtk);
            return m;
        }

        float EffMul(Ball b)
        {
            float m = b.jf * (float)HV[b.hv].spdMul;
            if (b.accel > 0) m *= b.accelMul;
            if (b.moss > 0) m *= 1 + 0.4f * (float)st.devMul;
            if (b.slow > 0) m *= 0.65f;
            return m;
        }

        void UpdateBall(Ball b, float h)
        {
            b.accel -= h; b.moss -= h; b.slow -= h; b.weak -= h; b.pierceT -= h; b.homeT -= h; b.powT -= h; b.clear -= h; b.magnet -= h; b.stumpAtk -= h; b.stumpT -= h; b.bladeFx -= h;
            if (!b.perm)
            {
                b.life -= h;
                if (b.life <= 0) { b.dead = true; for (int i = 0; i < 6; i++) AddPart(b.x, b.y, U.Rand(-60, 60), U.Rand(-60, 60), 0.4f, U.Hex("#dfe8f0"), 3); return; }
            }
            b.spd = Mathf.Max((float)st.minSpd, b.spd - b.spd * 0.18f * h);
            float v = b.spd * EffMul(b);
            int steps = Mathf.Min(80, Mathf.Max(1, Mathf.CeilToInt(v * h / (b.r * 0.45f))));
            float sh = h / steps;
            for (int i = 0; i < steps; i++) StepBall(b, sh);
            foreach (var c in clouds)
            {
                if (U.D2(b.x, b.y, c.x, c.y) < (c.r + b.r) * (c.r + b.r) && b.clear <= 0)
                {
                    if (c.slow) b.slow = (float)st.debuffDur;
                    if (c.weak) b.weak = (float)st.debuffDur;
                }
            }
            // 회전 칼날 (강화 트리 칼날 또는 회전칼날 수확기 고유 칼날)
            Dictionary<string, double> mill = b.hv == "mill" ? HV["mill"].ab(b.hs) : null;
            if (st.blade || mill != null)
            {
                b.bladeT -= h;
                if (b.bladeT <= 0)
                {
                    b.bladeT += st.blade ? (float)st.bladeInt : 0.35f;
                    float rr = BladeRadius(b);
                    double dmg = (st.blade ? st.bladeDmg : st.atk * 0.4) * Wk(b);
                    bool any = false;
                    var q = QueryCircle(b.x, b.y, rr + 30);
                    foreach (var m in q)
                    {
                        float reach = rr + m.r * 0.6f;
                        if (U.D2(b.x, b.y, m.x, m.y) < reach * reach)
                        {
                            Damage(m, dmg, b, "blade", 0.5, src: st.blade ? "blade" : "mill");
                            any = true;
                            if (mill != null && U.Chance((float)mill["p"])) MillChip(m);
                        }
                    }
                    FreeQuery(q);
                    if (any) b.bladeFx = 0.12f;
                }
            }
            b.ang += (v / b.r) * h * 0.35f;
            b.trail.Add(new Vector2(b.x, b.y)); if (b.trail.Count > 8) b.trail.RemoveAt(0);
        }

        void StepBall(Ball b, float h)
        {
            if (b.magnet > 0)
            {
                Shroom best = null; float bd = 1e12f;
                var q = QueryCircle(b.x, b.y, 500);
                foreach (var m in q) { if (m.jelly) continue; float dd = U.D2(b.x, b.y, m.x, m.y); if (dd < bd) { bd = dd; best = m; } }
                FreeQuery(q);
                if (best != null)
                {
                    float cur = Mathf.Atan2(b.dy, b.dx), want = Mathf.Atan2(best.y - b.y, best.x - b.x);
                    float maxd = b.magRate * h * Mathf.Deg2Rad;
                    float a = cur + U.Clamp(U.AngDiff(cur, want), -maxd, maxd);
                    b.dx = Mathf.Cos(a); b.dy = Mathf.Sin(a);
                }
            }
            float v = b.spd * EffMul(b);
            float py = b.y;
            b.x += b.dx * v * h; b.y += b.dy * v * h;
            // 벽
            if (b.x - b.r < FX0) { b.x = FX0 + b.r; b.dx = Mathf.Abs(b.dx); FixDir(b); WallHit(b, "l"); }
            else if (b.x + b.r > FX1) { b.x = FX1 - b.r; b.dx = -Mathf.Abs(b.dx); FixDir(b); WallHit(b, "r"); }
            if (b.y - b.r < FY0) { b.y = FY0 + b.r; b.dy = Mathf.Abs(b.dy); FixDir(b); WallHit(b, "t"); }
            else if (b.y + b.r > FY1) { b.y = FY1 - b.r; b.dy = -Mathf.Abs(b.dy); FixDir(b); b.combo = 0; b.hits = 0; WallHit(b, "b"); }
            // 슬라이드 바·분신 바 (RoundSim.Bar)
            BarCollide(b, py);
            // 버섯
            bool inJ = false; List<Shroom> jl = null;
            // 지난 판정의 b.inside와 번갈아 쓰는 두 번째 리스트 (서브스텝마다 새 리스트를 만들지 않게)
            var inside = b.insideNext; inside.Clear();
            var hits = QueryCircle(b.x, b.y, b.r + 2);
            foreach (var m in hits)
            {
                if (m.grow < 0.5f) continue;
                float ddx = b.x - m.x, ddy = b.y - m.y, rr = b.r + m.r, dd = ddx * ddx + ddy * ddy;
                if (dd >= rr * rr) continue;
                if (m.jelly) { inJ = true; if (jl == null) { jl = jellyBuf; jl.Clear(); } jl.Add(m); continue; }
                if (b.inside.Contains(m)) { inside.Add(m); continue; }
                if (!m.solid) { inside.Add(m); DirectHit(m, b, 1); continue; }
                // 관통 (바 스킬): 지속 중엔 군락을 튕기지 않고 뚫고 지나간다. 보스는 뚫지 못한다.
                // 관통 타격은 직접 피해만 준다: 군락을 지나며 버섯마다 폭발·번개 등이 연달아 터져 틱마다 피해가 들어가는 것처럼 폭증했다
                if (b.pierceT > 0 && !m.boss) { inside.Add(m); DirectHit(m, b, 1, false); continue; }
                float dl = Mathf.Sqrt(dd); if (dl == 0) dl = 0.01f;
                float nx = ddx / dl, ny = ddy / dl;
                float dot = b.dx * nx + b.dy * ny;
                if (dot < 0) { b.dx -= 2 * dot * nx; b.dy -= 2 * dot * ny; FixDir(b); }
                b.x = m.x + nx * (rr + 0.5f); b.y = m.y + ny * (rr + 0.5f);
                if (b.lastHit == m && t - b.lastHitT < 0.04f) continue;
                b.lastHit = m; b.lastHitT = t;
                DirectHit(m, b, 1);
            }
            FreeQuery(hits);
            b.insideNext = b.inside; b.inside = inside;
            if (inJ)
            {
                if (!b.inJelly) { b.inJelly = true; b.jTick = 0.15f; }
                b.jf = (float)st.jellySlow;
                b.jTick += h;
                if (b.jTick >= 0.15f)
                {
                    b.jTick -= 0.15f;
                    foreach (var m in jl) Damage(m, st.atk * 0.5 * Wk(b), b, "jelly", 0.5, src: "jelly");
                }
            }
            else
            {
                b.inJelly = false;
                if (b.jf < 1) b.jf = Mathf.Min(1, b.jf + (1 - (float)st.jellySlow) * h);
            }
            // 숲 장치
            foreach (var d in devices)
            {
                if (d.type == "stump")
                {
                    float ddx = b.x - d.x, ddy = b.y - d.y, rr = b.r + d.r, dd = ddx * ddx + ddy * ddy;
                    if (dd < rr * rr)
                    {
                        float dl = Mathf.Sqrt(dd); if (dl == 0) dl = 0.01f;
                        float nx = ddx / dl, ny = ddy / dl, dot = b.dx * nx + b.dy * ny;
                        if (dot < 0) { b.dx -= 2 * dot * nx; b.dy -= 2 * dot * ny; FixDir(b); }
                        b.x = d.x + nx * (rr + 0.5f); b.y = d.y + ny * (rr + 0.5f);
                        b.stumpAtk = STUMP_ATK_DUR;
                        if (b.stumpT <= 0)
                        {
                            b.stumpT = 0.05f; d.hitT = 0.2f;
                            AddScore((TIERS[0].score + st.scoreFlat) * ScoreMulFor(null, b) * STUMP_SCORE * st.devMul, d.x, d.y - d.r, 0, false, false);
                            Snd.Bumper();
                        }
                    }
                }
                else if (d.type == "moss")
                {
                    if (b.x > d.x0 && b.x < d.x1 && b.y > d.y0 && b.y < d.y1) b.moss = 2;
                }
                else if (d.type == "acorn" && !d.gone)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var p = d.pts[i];
                        if (d.lit[i] == 0 && U.D2(b.x, b.y, p.x, p.y) < (18 * DEV_SCALE + b.r) * (18 * DEV_SCALE + b.r))
                        {
                            d.lit[i] = 1; Snd.Tone(700 + i * 150, 0.08f, Snd.Wave.Triangle, 0.4f);
                            if (d.lit[0] > 0 && d.lit[1] > 0 && d.lit[2] > 0)
                            {
                                acornT = 5 * (float)st.devMul; d.gone = true;   // 한 번 버프를 주고 사라진다 (RoundSim.Effects가 치움)
                                AddLabel(Loc.T("도토리 점등! 점수 ×2"), (d.pts[0].x + d.pts[2].x) / 2, d.pts[1].y - 40, "#ffe070", 30);
                            }
                        }
                    }
                }
            }
        }

        // 임시 핀볼(분신·분열·꼬마)은 스킬을 쓰지 않는다. 공을 거치지 않는 효과(b가 null)는 그대로
        static bool Skillful(Ball b) => b == null || b.perm;

        void WallHit(Ball b, string side)
        {
            // 무손실 반사만으로는 같은 궤도를 반복하므로 ±4° 흔들어 준다
            float a = Mathf.Atan2(b.dy, b.dx) + U.Rand(-0.07f, 0.07f);
            b.dx = Mathf.Cos(a); b.dy = Mathf.Sin(a); FixDir(b);
            Snd.Wall();
            SawBounce(b);
            var sk = st.sk["tornado"];
            if (sk.on && Skillful(b) && U.Chance((float)SkP("tornado", b))) SpawnTornado(b, side);
        }

        void SkillFx(string id, float x, float y)
        {
            Flash(id);
            if (skillLabelT.TryGetValue(id, out var lt) && lt > t) return;   // 같은 스킬 이름은 1.5초에 한 번만
            skillLabelT[id] = t + 1.5f;
            string n = null, col = "#ffffff";
            if (SKILLS.TryGetValue(id, out var s)) { n = s.n; col = s.col; }
            else if (NODE.TryGetValue(id, out var nd)) { n = nd.n; col = nd.col ?? "#ffffff"; }
            else if (id == "spark") { n = Loc.T("연쇄 번개"); col = "#ffe03a"; }
            else if (id == "hv_split") { n = Loc.T("세포 분열"); col = "#ff8a80"; }
            if (n != null) AddLabel(n + "!", x, y - 30, col, 20, true);
        }

        // ===== 피해·점수·수확 =====
        double ScoreMulFor(Shroom m, Ball b)
        {
            double x = st.scoreMul * (1 + (b != null ? b.combo : 0) * st.comboK);
            if (m != null)
            {
                if (m.golden) x *= 10;
                if (m.col != null && m.col.wander) x *= st.wanderMul;
                if (m.giant || m.boss) x *= 3;
            }
            if (acornT > 0) x *= 2;
            if (festOn) x *= st.festMul;
            x *= FeverMul();
            return x;
        }

        // src: 데미지 통계 출처 (direct·burst·bolt… DmgSrcName). 비우면 kind
        void Damage(Shroom m, double amt, Ball b, string kind, double scoreF = 1, Wave wave = null, bool forceCrit = false, string src = null)
        {
            if (m.dead) return;
            if (m.cluster || m.giant || m.boss) amt *= st.heavyMul;
            if (m.boss) amt *= st.bossMul;
            bool crit = forceCrit || U.Chance((float)(st.crit + (b != null ? HV[b.hv].crit : 0)));
            double hpBefore = m.hp;
            m.hp -= amt * (crit ? st.critMul : 1);
            RecordDamage(src ?? kind, b, Math.Min(hpBefore, hpBefore - m.hp), m.hp <= 0);
            m.squish = 1;
            if (m.hpLag < 0 || m.hitT > 0.5f) m.hpLag = hpBefore;   // the trail starts where the bar was before this burst
            m.hitT = 0;
            if (crit && m.hp > 0) { shake = Mathf.Max(shake, 0.22f); AddFx(FxShape.HitAnim, m.x, m.y, m.r * 2.6f, 0.18f, new Color(1, 0.72f, 0.6f, 1), FxR(0, 360), 0); AddFx(FxShape.ImpactRing, m.x, m.y, m.r * 2.4f, 0.28f, new Color(1, 0.55f, 0.42f, 0.9f), FxR(0, 360), 0); }
            // 보스 타격 점수는 BOSS_SCORE_MUL배. 기획 의도: 보스를 못 잡은 판은 보스 전 일반 판보다 손해 → 일반 스테이지에서 성장하고 오라는 허들 (보상은 처치해야 받는다)
            double sc = (TIERS[m.sp.t].score + st.scoreFlat) * ScoreMulFor(m, b) * (crit ? st.critMul : 1) * scoreF * (m.boss ? BOSS_SCORE_MUL : 1);
            AddScore(sc, m.x, m.y - m.r, m.giant || m.boss ? 3 : m.sp.t, crit, m.golden);
            if (m.boss)
            {
                BossDamaged(m);
                if (m.hp <= 0) BossDown(m, b);
                return;
            }
            if (m.hp <= 0) Harvest(m, b, kind, wave);
        }

        // procs: 타격 때 발동하는 수확기 능력·스킬(종·분열·스파크·폭발·자석·번개)을 굴릴지 (관통 타격은 안 굴린다)
        void DirectHit(Shroom m, Ball b, double mul, bool procs = true)
        {
            b.hits++;
            double amt = st.atk * Wk(b) * mul * (st.fire ? 1.3 : 1) * (runT < 3 ? st.rushMul : 1);
            if (b.sharpen > 0) { amt *= b.sharpMul; b.sharpen--; }
            if (b.sawStk > 0) { amt *= 1 + HV["saw"].ab(b.hs)["k"] * b.sawStk; b.sawStk = 0; }
            if (st.fire && parts.Count < 120)
                for (int i = 0; i < 2; i++) AddPart(m.x + U.Rand(-m.r, m.r) * 0.5f, m.y, U.Rand(-40, 40), U.Rand(-160, -60), 0.35f, U.Hex(i % 2 == 1 ? "#ff7a2a" : "#ffd34a"), 4);
            string hv = b.hv; var a = HV[hv].ab(b.hs);
            if (hv == "gold" && !m.golden && !m.dead && U.Chance((float)a["p"]))
            {
                m.golden = true; AddLabel(Loc.T("황금!"), m.x, m.y - m.r - 20, "#ffd23a", 22);
                for (int i = 0; i < 8; i++) AddPart(m.x, m.y, U.Rand(-120, 120), U.Rand(-160, -40), 0.5f, U.Hex("#ffe36e"), 4);
            }
            if (hv == "coin")
            {
                double g = TIERS[m.sp.t].drop * SELL_PRICE * st.priceMul * a["k"] * st.zoneMul * (m.boss ? BOSS_SCORE_MUL : 1);   // 보스를 때려 얻는 골드도 같이 줄인다
                bonusGold += g; coinGold += g;
                if (texts.Count < 20) texts.Add(new FloatText { x = m.x + U.Rand(-10, 10), y = m.y - m.r - 18, v = g, coin = true });
                Snd.Tone(1200, 0.05f, Snd.Wave.Triangle, 0.25f, 1.3f, "coin", 0.05f);
            }
            HitFx(m, b);
            Damage(m, amt, b, "direct", 1, null, hv == "hoe" && (m.sp.t >= 1 || m.boss) && U.Chance((float)a["p"]), "direct");
            Snd.Hit();
            // 분열로 나온 소형 수확기는 정해진 횟수만큼 부딪히면 사라진다
            if (b.hitsLeft > 0 && --b.hitsLeft == 0) { b.dead = true; for (int i = 0; i < 6; i++) AddPart(b.x, b.y, U.Rand(-60, 60), U.Rand(-60, 60), 0.4f, U.Hex("#dfe8f0"), 3); }
            if (!procs) return;
            if (hv == "bell") BurstAt(m.x, m.y, (float)a["r"], st.atk * a["dmg"] * Wk(b), b, "#e8c08a", true, "bell");
            // 분열로 생긴 핀볼은 다시 분열하지 않는다 (연쇄 폭주 방지)
            if (hv == "spore" && !b.noSplit && U.Chance((float)a["p"])) SporeSplit(b, (int)a["hits"]);
            if (st.sparkP > 0 && Skillful(b) && U.Chance((float)st.sparkP)) { ChainBolt(m, b, "spark"); SkillFx("spark", b.x, b.y); }
            if (m.spore != null && m.sporeCd <= 0 && U.Chance((float)st.sporeP)) { m.sporeCd = 2; SpawnCloud(m.x, m.y, m.spore); }
            var sk = st.sk;
            if (sk["burst"].on && Skillful(b) && U.Chance((float)SkP("burst", b))) { BurstAt(b.x, b.y, (float)st.burstR, st.atk * 2 * sk["burst"].pm * st.skillDmg * Wk(b), b, "#ffb347", false, "burst"); SkillFx("burst", b.x, b.y); Snd.Skill(180); }
            if (sk["magnet"].on && Skillful(b) && U.Chance((float)SkP("magnet", b))) { b.magnet = 1; b.magRate = 20 * (float)sk["magnet"].pm; SkillFx("magnet", b.x, b.y); }
            if (sk["bolt"].on && Skillful(b) && U.Chance((float)SkP("bolt", b))) { ChainBolt(m, b, "bolt"); SkillFx("bolt", b.x, b.y); }
        }

        // 분열형: 능력치 50%인 작은 같은 수확기 (다시 분열하지 않음)
        void SporeSplit(Ball b, int hits)
        {
            if (balls.Count >= MAX_BALLS) return;
            var c = MakeBall(b.x, b.y, false, b.hv, 30, true);
            c.statMul = b.statMul * 0.5f; c.noSplit = true; c.hitsLeft = hits; c.hs = b.hs;
            float a = Mathf.Atan2(b.dy, b.dx) + U.Rand(-0.8f, 0.8f);
            c.dx = Mathf.Cos(a); c.dy = Mathf.Sin(a); FixDir(c);
            balls.Add(c);
            SkillFx("hv_split", b.x, b.y);
        }

        // 속도형(절삭 수리검): 벽·바에 튕길 때마다 다음 타격 피해가 쌓인다
        void SawBounce(Ball b)
        {
            if (b.hv != "saw") return;
            b.sawStk = Mathf.Min((int)HV["saw"].ab(b.hs)["max"], b.sawStk + 1);
        }

        public float BladeRadius(Ball b)
        {
            float rr = st.blade ? (float)st.bladeR : 0;
            if (b.hv == "mill") rr = Mathf.Max(rr, (float)st.ballR + (float)HV["mill"].ab(b.hs)["r"]);
            return rr * (b.perm ? 1 : b.r / (float)st.ballR);
        }

        void MillChip(Shroom m)
        {
            var sp = m.sp; double n = st.harvestMul;
            bag[sp.id] = (bag.TryGetValue(sp.id, out var v) ? v : 0) + n; gains[sp.c] += n;
            saleBump = 1;
            if (flyers.Count < 120) flyers.Add(new Flyer { x = m.x, y = m.y, sx = m.x, sy = m.y, dur = 0.5f, sp = sp, cat = sp.c });
        }

        readonly List<Shroom> jellyBuf = new List<Shroom>();   // StepBall에서 지금 젤리 안에 있는 버섯 (서브스텝마다 새 리스트를 만들지 않게)


        void SamCleave(Shroom m0, Ball b)
        {
            Shroom best = null; float bd = 160 * 160;
            var q = QueryCircle(m0.x, m0.y, 160);
            foreach (var m in q) { if (m.dead || m == m0) continue; float dd = U.D2(m0.x, m0.y, m.x, m.y); if (dd < bd) { bd = dd; best = m; } }
            FreeQuery(q);
            if (best == null) return;
            bolts.Add(new Bolt { pts = new List<Vector2> { new Vector2(m0.x, m0.y), new Vector2(best.x, best.y) }, t = 0.2f, slash = true });
            Damage(best, st.atk * 2 * Wk(b), b, "skill", src: "cleave");
        }

        void Harvest(Shroom m, Ball b, string kind, Wave wave)
        {
            if (m.dead) return;
            m.dead = true; m.hp = 0;
            // HUD 콤보: 직접 타격은 DirectHit에서 셌다. 폭발·번개 등 효과로 거둔 버섯도 그 수확기의 콤보로 센다
            // (강해질수록 효과가 필드를 비워 직접 때릴 버섯이 없어 콤보가 오히려 느리게 올랐다)
            if (b != null && kind != "direct") b.hits++;
            var sp = m.sp;
            double drop = m.dropMul * st.harvestMul * (m.golden ? 5 : 1) * (m.giant ? 30 : 1) * (sp.c == "ps" ? st.poisonMul : 1) * (festOn ? st.festMul : 1);
            if (st.multiP > 0 && U.Chance((float)st.multiP)) { drop *= 3; AddLabel(Loc.T("×3 배수 획득!"), m.x, m.y - m.r - 34, "#ffe36e", 24); }
            gains[sp.c] += drop;
            bag[sp.id] = (bag.TryGetValue(sp.id, out var bv) ? bv : 0) + drop;
            harvests++; killT[sp.t]++;
            picks[sp.id] = (picks.TryGetValue(sp.id, out var pc) ? pc : 0) + 1;
            ComboHit();
            saleBump = 1;
            int star0 = StarOf(sp.id);
            if (!G.codex.TryGetValue(sp.id, out var e)) e = G.codex[sp.id] = new SaveData.CodexEntry();
            if (e.n <= 0)
            {
                e.first = G.rounds + 1; newSpecies.Add(sp.id); codexShake = 1;
                AddLabel(Loc.F("새 버섯! {0}", sp.n), m.x, m.y - 50, "#fff7c2", 30);
            }
            e.n++;
            int star1 = StarOf(sp.id);
            if (star1 > star0)
            {
                newStars.Add((sp.id, star1));
                AddLabel(Loc.F("{0} {1} {2}성! {3} {4}", new string('★', star1), sp.n, star1, AB[sp.ab].n, AbFmt(sp.ab, AbPerStar(sp))), m.x, m.y - 80, "#ffe36e", 26);
            }
            if (m.golden && !e.gold) { e.gold = true; newGolden.Add(sp.id); AddLabel(Loc.F("황금 {0}!", sp.n), m.x, m.y - 80, "#ffd23a", 30); }
            if (m.giant) e.giant = true;
            // 연출: 버섯이 퐁 터지며 구름·조각·고리 (색은 갈래 색과 그 버섯의 갓 색)
            var col = U.Hex(CATS[sp.c].color);
            var cap = m.golden ? U.Hex("#ffd23a") : U.Hex(sp.c1);
            float big = m.giant ? 2.2f : m.cluster ? 1.5f : 1;
            AddFx(FxShape.Puff, m.x, m.y, m.r * 2.6f * big, 0.45f, new Color(cap.r * 0.5f + 0.5f, cap.g * 0.5f + 0.5f, cap.b * 0.5f + 0.5f, 0.9f), FxR(0, 360), 40);
            AddFx(FxShape.Impact, m.x, m.y, m.r * 2.2f * big, 0.22f, new Color(cap.r, cap.g, cap.b, 0.95f), FxR(0, 360), 0);
            AddFx(FxShape.ImpactRing, m.x, m.y, m.r * 3f * big, 0.32f, new Color(col.r, col.g, col.b, 0.95f), FxR(0, 360), 0);
            // the pop: a flipbook burst, a spray of twinkles (white to the cap colour), a beat of hitstop and a kick
            AddFx(FxShape.HitAnim, m.x, m.y - m.r * 0.2f, m.r * 3.4f * big, 0.2f, new Color(1, 0.97f, 0.86f, 1), FxR(0, 360), 0);
            int nt = m.giant ? 16 : m.cluster ? 9 : 5 + sp.t;
            for (int i = 0; i < nt; i++)
            {
                float ta = FxR(0, U.TAU), ts = FxR(160, m.giant ? 560 : 360);
                var tc = Color.Lerp(Color.white, cap, FxR(0.2f, 0.8f));
                AddPart(m.x, m.y, Mathf.Cos(ta) * ts, Mathf.Sin(ta) * ts - 220, FxR(0.4f, 0.7f), tc, FxR(5, 9), FxShape.Twinkle);
            }
            if (!m.giant && popStopCd <= 0) { hitstop = Mathf.Max(hitstop, m.cluster ? 0.05f : 0.025f); popStopCd = 0.22f; }
            if (!m.giant && balls.Count < 8) shake = Mathf.Max(shake, m.cluster ? 0.3f : 0.14f);
            int nc = m.giant ? 14 : m.cluster ? 8 : 4 + sp.t;
            for (int i = 0; i < nc; i++)
            {
                float ca = FxR(0, U.TAU), cs = FxR(140, m.giant ? 520 : 300);
                AddPart(m.x, m.y, Mathf.Cos(ca) * cs, Mathf.Sin(ca) * cs - 140, FxR(0.45f, 0.75f), cap, m.giant ? 9 : 6, FxShape.Chip);
            }
            int np = m.giant ? 60 : m.cluster ? 28 : 8 + sp.t * 4;
            for (int i = 0; i < np; i++)
            {
                float a = U.Rand(0, U.TAU), s = U.Rand(60, m.giant ? 600 : 260);
                AddPart(m.x, m.y, Mathf.Cos(a) * s, Mathf.Sin(a) * s - 60, U.Rand(0.3f, 0.7f), i % 3 != 0 ? col : m.golden ? U.Hex("#ffd23a") : U.Hex(sp.c1), m.giant ? 7 : 4);
            }
            int nf = m.giant ? 30 : m.cluster ? 6 + m.dropMul : 1;
            for (int i = 0; i < nf && flyers.Count < 220; i++)
            {
                float a = U.Rand(0, U.TAU), s0 = m.cluster || m.giant ? U.Rand(10, m.r) : 0;
                flyers.Add(new Flyer { x = m.x, y = m.y, sx = m.x + Mathf.Cos(a) * s0, sy = m.y + Mathf.Sin(a) * s0, t = -i * 0.025f, dur = U.Rand(0.45f, 0.7f), sp = sp, golden = m.golden, cat = sp.c });
            }
            if (m.cluster)
            {
                AddLabel($"+{U.Fmt(drop)} {CATS[sp.c].name}", m.x, m.y - m.r - 10, CATS[sp.c].light, 30);
                if (balls.Count < 8) shake = Mathf.Max(shake, 0.15f);
                rings.Add(new Ring { x = m.x, y = m.y, r = m.r * 2.2f, dur = 0.35f, col = CATS[sp.c].color });
                Snd.Tone(300, 0.18f, Snd.Wave.Sine, 0.7f, 2.2f, "heap", 0.05f);
            }
            if (m.giant) { hitstop = 0.3f; hitstopCd = 0.8f; flash = 0.8f; shake = 1; Snd.Rare(); giant = null; AddLabel(Loc.T("거대 버섯 수확!"), m.x, m.y - 120, "#ffffff", 44); }
            else if (sp.t >= 2)
            {
                if (hitstopCd <= 0) { hitstop = Mathf.Max(hitstop, sp.t == 3 ? 0.12f : 0.06f); hitstopCd = 0.6f; }
                if (flashCd <= 0) { flash = Mathf.Max(flash, sp.t == 3 ? 0.25f : 0.1f); flashCd = 1.2f; }
                Snd.Rare();
            }
            else Snd.Harvest(b != null ? b.combo : 0);
            // 군락지
            if (m.col != null)
            {
                m.col.alive--;
                if (m.col.alive <= 0) { colonies.Remove(m.col); regen.Add((float)st.regen); }
            }
            if (b != null && b.hv == "sam" && b.statMul >= 1 && kind != "skill" && U.Chance((float)HV["sam"].ab(b.hs)["p"])) queue.Add(new Queued { t = 0.05f, f = () => SamCleave(m, b) });
            var sk = st.sk;
            if (kind == "shock")
            {
                if (wave != null && !wave.child && wave.depth < 8)
                {
                    wave.child = true; float x = m.x, y = m.y; int dp = wave.depth + 1; var wb = wave.b;
                    queue.Add(new Queued { t = 0.08f, f = () => MakeWave(x, y, dp, wb) });
                }
            }
            else if (sk["shock"].on && Skillful(b) && U.Chance((float)SkP("shock", b))) { MakeWave(m.x, m.y, 1, b); SkillFx("shock", m.x, m.y); }
            if (sk["clone"].on && Skillful(b) && U.Chance((float)SkP("clone", b))) { SpawnTemp(m.x, m.y, 1 + sk["clone"].P, 4, true, null); SkillFx("clone", m.x, m.y); }
        }

        void AddScore(double v, float x, float y, int tier, bool crit, bool golden)
        {
            score += v; scoreBump = 1;
            if (texts.Count > 25 && tier < 2 && !golden) return;
            if (texts.Count > 45) return;
            texts.Add(new FloatText { x = x + U.Rand(-8, 8), y = y, vx = FxR(-40, 40), vy = crit ? -170 : -130, v = v, tier = tier, crit = crit, golden = golden });
        }

        public void AddPart(float x, float y, float vx, float vy, float life, Color col, float size, FxShape shape = FxShape.Dot)
        {
            if (parts.Count > 260) return;
            var p = partFree.Count > 0 ? partFree.Pop() : new Part();   // 사라진 조각을 다시 쓴다 (모든 칸을 새로 채운다)
            p.x = x; p.y = y; p.vx = vx; p.vy = vy; p.t = 0; p.life = life; p.col = col; p.size = size; p.shape = (byte)shape;
            parts.Add(p);
        }

        // Effects draw from their own random stream: UnityEngine.Random is the gameplay stream that BalanceSim seeds,
        // so adding or tuning effects never changes simulated results.
        static readonly System.Random fxRng = new System.Random(12345);
        public float popStopCd;   // spacing between the small harvest hitstops, so many quick pops don't stutter
        static float FxR(float a, float b) => a + (float)fxRng.NextDouble() * (b - a);
        static bool FxChance(float p) => fxRng.NextDouble() < p;

        // size: px across; rise: px/s it drifts up
        public void AddFx(FxShape shape, float x, float y, float size, float dur, Color col, float rot, float rise)
        {
            if (fxs.Count > 90) return;
            var f = fxFree.Count > 0 ? fxFree.Pop() : new Fx();   // 효과 난수(FxR·FxChance)는 예전과 같은 순서로 뽑는다
            f.shape = shape; f.x = x; f.y = y; f.t = 0; f.size = size; f.dur = dur; f.col = col; f.rot = rot; f.rise = rise; f.spin = FxR(-40, 40); f.flip = FxChance(0.5f);
            fxs.Add(f);
        }

        // a harvester hits a mushroom: a punchy burst at the contact point, tinted by the mushroom, bigger for rarer ones
        void HitFx(Shroom m, Ball b)
        {
            float dx = b.x - m.x, dy = b.y - m.y, d = Mathf.Max(1, Mathf.Sqrt(dx * dx + dy * dy));
            float cx = m.x + dx / d * m.r * 0.8f, cy = m.y + dy / d * m.r * 0.8f;
            m.kx = -dx / d; m.ky = -dy / d;   // pushed away from the harvester
            float k = 1 + m.sp.t * 0.18f + (m.giant || m.boss ? 0.6f : 0);
            // the pale field swallows white bursts: the outer burst takes the mushroom's own cap colour, a small warm
            // cream core sits on top, the streak is butter yellow
            var cap = U.Hex(m.sp.c1);
            AddFx(FxShape.Impact, cx, cy, 58 * k, 0.2f, new Color(cap.r, cap.g, cap.b, 0.95f), FxR(0, 360), 0);
            AddFx(FxShape.Impact, cx, cy, 32 * k, 0.15f, new Color(1, 0.96f, 0.82f, 1f), FxR(0, 360), 0);
            float ang = Mathf.Atan2(-dy, -dx) * Mathf.Rad2Deg;   // streak points away from the harvester
            AddFx(FxShape.Streak, cx, cy, 60 * k, 0.16f, new Color(1, 0.9f, 0.55f, 0.9f), ang, 0);
            if (parts.Count < 200)
                for (int i = 0; i < 2; i++)
                    AddPart(cx, cy, -dx / d * FxR(80, 200) + FxR(-60, 60), -dy / d * FxR(80, 200) - 80, 0.4f, U.Hex(m.sp.c1), 4, FxShape.Chip);
        }

        public void AddLabel(string text, float x, float y, string col, float size, bool skill = false)   // skill: 스킬 이름 글자 (연출 간소화 때 안 그린다)
        {
            if (labels.Count > 10) labels.RemoveAt(0);
            labels.Add(new Label { text = text, x = U.Clamp(x, 120, WW - 120), y = U.Clamp(y, 80, WH - 60), col = col, size = size, skill = skill });
        }
    }
}
