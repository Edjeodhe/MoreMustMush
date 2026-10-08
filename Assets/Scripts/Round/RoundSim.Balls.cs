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
                x = x, y = y, spd = (float)st.launch, r = (float)st.ballR * (small ? 0.65f : 1), perm = perm, life = life, maxLife = life,
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
                var b = MakeBall(x, y, false, null, life * (float)st.tempLife, small);
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
        }

        static float Wk(Ball b) => b != null && b.weak > 0 ? 0.5f : 1;

        float EffMul(Ball b)
        {
            float m = b.jf;
            if (b.accel > 0) m *= b.accelMul;
            if (b.moss > 0) m *= 1 + 0.4f * (float)st.devMul;
            if (b.slow > 0) m *= 0.65f;
            return m;
        }

        void UpdateBall(Ball b, float h)
        {
            b.accel -= h; b.moss -= h; b.slow -= h; b.weak -= h; b.clear -= h; b.magnet -= h; b.mole -= h; b.stumpT -= h; b.bladeFx -= h;
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
                            Damage(m, dmg, b, "blade", 0.5);
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
            foreach (var d in devices)
                if (d.type == "stream" && b.x > d.x0 && b.x < d.x1 && b.y > d.y0 && b.y < d.y1) { b.x += d.fx * 250 * h; b.y += d.fy * 250 * h; }
            // 벽
            if (b.x - b.r < FX0) { b.x = FX0 + b.r; b.dx = Mathf.Abs(b.dx); FixDir(b); WallHit(b, "l"); }
            else if (b.x + b.r > FX1) { b.x = FX1 - b.r; b.dx = -Mathf.Abs(b.dx); FixDir(b); WallHit(b, "r"); }
            if (b.y - b.r < FY0) { b.y = FY0 + b.r; b.dy = Mathf.Abs(b.dy); FixDir(b); WallHit(b, "t"); }
            else if (b.y + b.r > FY1) { b.y = FY1 - b.r; b.dy = -Mathf.Abs(b.dy); FixDir(b); b.combo = 0; WallHit(b, "b"); }
            // 슬라이드 바 (위에서 내려오는 핀볼만)
            float top = BAR_Y - BAR_T / 2;
            if (b.dy > 0 && b.y + b.r >= top && py + b.r <= top + 2 && Mathf.Abs(b.x - barX) <= barLen / 2 + b.r * 0.6f) BarHit(b);
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
                var pk = st.sk["pierce"];
                if (pk.on && U.Chance((float)pk.p)) { inside.Add(m); DirectHit(m, b, pk.pm); SkillFx("pierce", b.x, b.y); continue; }
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
                    foreach (var m in jl) Damage(m, st.atk * 0.5 * Wk(b), b, "jelly", 0.5);
                }
            }
            else
            {
                b.inJelly = false;
                if (b.jf < 1) b.jf = Mathf.Min(1, b.jf + (1 - (float)st.jellySlow) * h);
            }
            // 특수 버섯 (튕기면서 맞는다)
            var sm = special;
            if (sm != null && sm.appear >= 0.5f)
            {
                float ddx = b.x - sm.x, ddy = b.y - sm.y, rr = b.r + sm.r, dd = ddx * ddx + ddy * ddy;
                if (dd < rr * rr)
                {
                    float dl = Mathf.Sqrt(dd); if (dl == 0) dl = 0.01f;
                    float nx = ddx / dl, ny = ddy / dl, dot = b.dx * nx + b.dy * ny;
                    if (dot < 0) { b.dx -= 2 * dot * nx; b.dy -= 2 * dot * ny; FixDir(b); }
                    b.x = sm.x + nx * (rr + 0.5f); b.y = sm.y + ny * (rr + 0.5f);
                    if (sm.hitCd <= 0) { sm.hitCd = 0.05f; HitSpecial(sm, st.atk * Wk(b) * (st.fire ? 1.3 : 1) * (b.sharpen > 0 ? b.sharpMul : 1), b); }
                }
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
                        b.spd = (float)st.launch * 1.5f;
                        if (b.stumpT <= 0)
                        {
                            b.stumpT = 0.05f; d.hitT = 0.2f;
                            AddScore((TIERS[0].score + st.scoreFlat) * ScoreMulFor(null, b) * 5 * st.devMul, d.x, d.y - d.r, 0, false, false);
                            Snd.Bumper();
                        }
                    }
                }
                else if (d.type == "moss")
                {
                    if (b.x > d.x0 && b.x < d.x1 && b.y > d.y0 && b.y < d.y1) b.moss = 2;
                }
                else if (d.type == "mole")
                {
                    if (b.mole <= 0)
                    {
                        // a 구멍 → b, 아니면 b 구멍 → a (예전 순서 그대로, 배열을 만들지 않게 펼쳐 씀)
                        if (U.D2(b.x, b.y, d.a.x, d.a.y) < d.r * d.r) MoleWarp(b, d, d.b);
                        else if (U.D2(b.x, b.y, d.b.x, d.b.y) < d.r * d.r) MoleWarp(b, d, d.a);
                    }
                }
                else if (d.type == "acorn")
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var p = d.pts[i];
                        if (d.lit[i] == 0 && U.D2(b.x, b.y, p.x, p.y) < (18 * DEV_SCALE + b.r) * (18 * DEV_SCALE + b.r))
                        {
                            d.lit[i] = 1; Snd.Tone(700 + i * 150, 0.08f, Snd.Wave.Triangle, 0.4f);
                            if (d.lit[0] > 0 && d.lit[1] > 0 && d.lit[2] > 0 && d.offT <= 0)
                            {
                                acornT = 5 * (float)st.devMul; d.offT = 1;
                                AddLabel("도토리 점등! 점수 ×2", (d.pts[0].x + d.pts[2].x) / 2, d.pts[1].y - 40, "#ffe070", 30);
                            }
                        }
                    }
                }
            }
        }

        void WallHit(Ball b, string side)
        {
            // 무손실 반사만으로는 같은 궤도를 반복하므로 ±4° 흔들어 준다
            float a = Mathf.Atan2(b.dy, b.dx) + U.Rand(-0.07f, 0.07f);
            b.dx = Mathf.Cos(a); b.dy = Mathf.Sin(a); FixDir(b);
            Snd.Wall();
            var sk = st.sk["tornado"];
            if (sk.on && U.Chance((float)sk.p)) SpawnTornado(b, side);
        }

        void BarHit(Ball b)
        {
            float tt = U.Clamp((b.x - barX) / (barLen / 2), -1, 1), a = tt * 65 * Mathf.Deg2Rad;
            b.dx = Mathf.Sin(a); b.dy = -Mathf.Cos(a);
            b.y = BAR_Y - BAR_T / 2 - b.r;
            b.spd = Mathf.Max(b.spd, (float)st.launch);
            b.combo++;
            barFx = 0.15f;
            Snd.Bar();
            for (int i = 0; i < 5; i++) AddPart(b.x, BAR_Y - 8, U.Rand(-120, 120), U.Rand(-200, -60), 0.35f, U.Hex("#ffe9a8"), 3);
            if (!b.perm) return;   // 임시 핀볼은 바 버프를 받지 않음
            var sk = st.sk;
            bool Proc(string id) => sk[id].on && U.Chance((float)sk[id].p);
            if (Proc("accel")) { b.accel = 3 * (float)st.buffMul; b.accelMul = 1 + 0.15f * (float)sk["accel"].pm; SkillFx("accel", b.x, b.y); }
            if (Proc("sharpen")) { b.sharpen = Mathf.CeilToInt(3 * (float)st.buffMul); b.sharpMul = 2 * (float)sk["sharpen"].pm; SkillFx("sharpen", b.x, b.y); }
            if (Proc("split")) { SpawnTemp(b.x, b.y - 4, 1 + sk["split"].P, 6, false, Mathf.Atan2(b.dy, b.dx)); SkillFx("split", b.x, b.y); }
            if (Proc("sip")) { AddTime(0.3f * (float)sk["sip"].pm, "sip", b.x, b.y); SkillFx("sip", b.x, b.y); }
            if (Proc("clear")) { b.clear = 1.5f * (float)sk["clear"].pm * (float)st.buffMul; b.slow = 0; b.weak = 0; SkillFx("clear", b.x, b.y); }
            if (st.kidP > 0 && U.Chance((float)st.kidP)) { SpawnTemp(b.x, b.y - 4, 1, 8, true, -Mathf.PI / 2 + U.Rand(-0.7f, 0.7f)); SkillFx("md_kid", b.x, b.y); }
        }

        void SkillFx(string id, float x, float y)
        {
            Flash(id);
            if (skillLabelT.TryGetValue(id, out var lt) && lt > t) return;   // 같은 스킬 이름은 1.5초에 한 번만
            skillLabelT[id] = t + 1.5f;
            string n = null, col = "#ffffff";
            if (SKILLS.TryGetValue(id, out var s)) { n = s.n; col = s.col; }
            else if (NODE.TryGetValue(id, out var nd)) { n = nd.n; col = nd.col ?? "#ffffff"; }
            else if (id == "spark") { n = "찌릿 번개"; col = "#ffe03a"; }
            else if (id == "hv_split") { n = "세포 분열"; col = "#ff8a80"; }
            if (n != null) AddLabel(n + "!", x, y - 30, col, 20);
        }

        // ===== 피해·점수·수확 =====
        double ScoreMulFor(Shroom m, Ball b)
        {
            double x = st.scoreMul * (1 + (b != null ? b.combo : 0) * st.comboK);
            if (m != null)
            {
                if (m.golden) x *= 10;
                if (m.col != null && m.col.wander) x *= st.wanderMul;
                if (m.giant) x *= 3;
            }
            if (acornT > 0) x *= 2;
            if (festOn) x *= st.festMul;
            x *= FeverMul();
            return x;
        }

        void Damage(Shroom m, double amt, Ball b, string kind, double scoreF = 1, Wave wave = null, bool forceCrit = false)
        {
            if (m.dead) return;
            if (m.cluster || m.giant) amt *= st.heavyMul;
            bool crit = forceCrit || U.Chance((float)st.crit);
            m.hp -= amt * (crit ? st.critMul : 1);
            m.squish = 1;
            double sc = (TIERS[m.sp.t].score + st.scoreFlat) * ScoreMulFor(m, b) * (crit ? st.critMul : 1) * scoreF;
            AddScore(sc, m.x, m.y - m.r, m.giant ? 3 : m.sp.t, crit, m.golden);
            if (m.hp <= 0) Harvest(m, b, kind, wave);
        }

        void DirectHit(Shroom m, Ball b, double mul)
        {
            double amt = st.atk * Wk(b) * mul * (st.fire ? 1.3 : 1) * (runT < 3 ? st.rushMul : 1);
            if (b.sharpen > 0) { amt *= b.sharpMul; b.sharpen--; }
            if (st.fire && parts.Count < 120)
                for (int i = 0; i < 2; i++) AddPart(m.x + U.Rand(-m.r, m.r) * 0.5f, m.y, U.Rand(-40, 40), U.Rand(-160, -60), 0.35f, U.Hex(i % 2 == 1 ? "#ff7a2a" : "#ffd34a"), 4);
            string hv = b.hv; var a = HV[hv].ab(b.hs);
            if (hv == "gold" && !m.golden && !m.dead && U.Chance((float)a["p"]))
            {
                m.golden = true; AddLabel("황금!", m.x, m.y - m.r - 20, "#ffd23a", 22);
                for (int i = 0; i < 8; i++) AddPart(m.x, m.y, U.Rand(-120, 120), U.Rand(-160, -40), 0.5f, U.Hex("#ffe36e"), 4);
            }
            if (hv == "coin")
            {
                double g = TIERS[m.sp.t].drop * SELL_PRICE * st.priceMul * a["k"] * st.zoneMul;
                bonusGold += g; coinGold += g;
                if (texts.Count < 20) texts.Add(new FloatText { x = m.x + U.Rand(-10, 10), y = m.y - m.r - 18, v = g, coin = true });
                Snd.Tone(1200, 0.05f, Snd.Wave.Triangle, 0.25f, 1.3f, "coin", 0.05f);
            }
            Damage(m, amt, b, "direct", 1, null, hv == "saw" && m.sp.t >= 1 && U.Chance((float)a["p"]));
            Snd.Hit();
            if (hv == "bell") BurstAt(m.x, m.y, (float)a["r"], st.atk * a["dmg"] * Wk(b), b, "#e8c08a", true);
            // 분열로 생긴 핀볼은 다시 분열하지 않는다 (연쇄 폭주 방지)
            if (hv == "spore" && !b.noSplit && U.Chance((float)a["p"]))
            {
                foreach (var c in SpawnTemp(b.x, b.y, 1, (float)a["life"], true, Mathf.Atan2(b.dy, b.dx) + U.Rand(-0.8f, 0.8f))) c.noSplit = true;
                SkillFx("hv_split", b.x, b.y);
            }
            if (st.sparkP > 0 && U.Chance((float)st.sparkP)) { ChainBolt(m, b); SkillFx("spark", b.x, b.y); }
            if (m.spore != null && m.sporeCd <= 0 && U.Chance((float)st.sporeP)) { m.sporeCd = 2; SpawnCloud(m.x, m.y, m.spore); }
            var sk = st.sk;
            if (sk["burst"].on && U.Chance((float)sk["burst"].p)) { BurstAt(b.x, b.y, (float)st.burstR, st.atk * 2 * sk["burst"].pm * st.skillDmg * Wk(b), b, "#ffb347", false); SkillFx("burst", b.x, b.y); Snd.Skill(180); }
            if (sk["tspore"].on && U.Chance((float)sk["tspore"].p)) { AddTime(0.3f * (float)sk["tspore"].pm, "tspore", b.x, b.y); Flash("tspore"); }
            if (sk["magnet"].on && U.Chance((float)sk["magnet"].p)) { b.magnet = 1; b.magRate = 20 * (float)sk["magnet"].pm; SkillFx("magnet", b.x, b.y); }
            if (sk["bolt"].on && U.Chance((float)(sk["bolt"].p * (st.wId == "thunder" ? 2 : 1)))) { ChainBolt(m, b); SkillFx("bolt", b.x, b.y); }
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
            value += n * UnitPrice(sp, st.priceMul); saleBump = 1;
            if (flyers.Count < 120) flyers.Add(new Flyer { x = m.x, y = m.y, sx = m.x, sy = m.y, dur = 0.5f, sp = sp, cat = sp.c });
        }

        readonly List<Shroom> jellyBuf = new List<Shroom>();   // StepBall에서 지금 젤리 안에 있는 버섯 (서브스텝마다 새 리스트를 만들지 않게)

        // 두더지 굴: 들어간 반대편 구멍(to)으로 튀어나온다
        void MoleWarp(Ball b, Device d, Vector2 to)
        {
            b.x = to.x + b.dx * (d.r + b.r + 4); b.y = to.y + b.dy * (d.r + b.r + 4); b.mole = 1; b.trail.Clear();
            for (int i = 0; i < 8; i++) AddPart(to.x, to.y, U.Rand(-80, 80), U.Rand(-80, 80), 0.4f, U.Hex("#8a6a4a"), 4);
            Snd.Tone(200, 0.12f, Snd.Wave.Sine, 0.4f, 2.5f, "mole");
        }

        void SamCleave(Shroom m0, Ball b)
        {
            Shroom best = null; float bd = 160 * 160;
            var q = QueryCircle(m0.x, m0.y, 160);
            foreach (var m in q) { if (m.dead || m == m0) continue; float dd = U.D2(m0.x, m0.y, m.x, m.y); if (dd < bd) { bd = dd; best = m; } }
            FreeQuery(q);
            if (best == null) return;
            bolts.Add(new Bolt { pts = new List<Vector2> { new Vector2(m0.x, m0.y), new Vector2(best.x, best.y) }, t = 0.2f, slash = true });
            Damage(best, st.atk * 2 * Wk(b), b, "skill");
        }

        void Harvest(Shroom m, Ball b, string kind, Wave wave)
        {
            if (m.dead) return;
            m.dead = true; m.hp = 0;
            var sp = m.sp;
            double drop = m.dropMul * st.harvestMul * (m.golden ? 5 : 1) * (m.giant ? 30 : 1) * (sp.c == "ps" ? st.poisonMul : 1) * (festOn ? st.festMul : 1);
            if (st.multiP > 0 && U.Chance((float)st.multiP)) { drop *= 3; AddLabel("×3 배수 획득!", m.x, m.y - m.r - 34, "#ffe36e", 24); }
            gains[sp.c] += drop;
            bag[sp.id] = (bag.TryGetValue(sp.id, out var bv) ? bv : 0) + drop;
            harvests++; killT[sp.t]++;
            ComboHit();
            value += drop * UnitPrice(sp, st.priceMul);
            saleBump = 1;
            int star0 = StarOf(sp.id);
            if (!G.codex.TryGetValue(sp.id, out var e)) e = G.codex[sp.id] = new SaveData.CodexEntry();
            if (e.n <= 0)
            {
                e.first = G.rounds + 1; newSpecies.Add(sp.id); codexShake = 1;
                AddLabel("새 버섯! " + sp.n, m.x, m.y - 50, "#fff7c2", 30);
            }
            e.n++;
            int star1 = StarOf(sp.id);
            if (star1 > star0)
            {
                newStars.Add((sp.id, star1));
                AddLabel($"{new string('★', star1)} {sp.n} {star1}성! {AB[sp.ab].n} {AbFmt(sp.ab, AbPerStar(sp))}", m.x, m.y - 80, "#ffe36e", 26);
            }
            if (m.golden && !e.gold) { e.gold = true; newGolden.Add(sp.id); AddLabel("황금 " + sp.n + "!", m.x, m.y - 80, "#ffd23a", 30); }
            if (m.giant) e.giant = true;
            // 연출
            var col = U.Hex(CATS[sp.c].color);
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
            if (m.giant) { hitstop = 0.3f; hitstopCd = 0.8f; flash = 0.8f; shake = 1; Snd.Rare(); giant = null; AddLabel("거대 버섯 수확!", m.x, m.y - 120, "#ffffff", 44); }
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
            if (b != null && b.hv == "sam" && kind != "skill" && U.Chance((float)HV["sam"].ab(b.hs)["p"])) queue.Add(new Queued { t = 0.05f, f = () => SamCleave(m, b) });
            var sk = st.sk;
            if (kind == "shock")
            {
                if (wave != null && !wave.child && wave.depth < 8)
                {
                    wave.child = true; float x = m.x, y = m.y; int dp = wave.depth + 1; var wb = wave.b;
                    queue.Add(new Queued { t = 0.08f, f = () => MakeWave(x, y, dp, wb) });
                }
            }
            else if (sk["shock"].on && U.Chance((float)sk["shock"].p)) { MakeWave(m.x, m.y, 1, b); SkillFx("shock", m.x, m.y); }
            if (sk["clone"].on && U.Chance((float)sk["clone"].p)) { SpawnTemp(m.x, m.y, 1 + sk["clone"].P, 4, true, null); SkillFx("clone", m.x, m.y); }
            if (sk["device"].on && deviceSpawned < 10 && U.Chance((float)sk["device"].p))
            {
                var d = PlaceDevice(U.Pick(DEVICE_TYPES), new Vector2(m.x, m.y));
                if (d != null) { deviceSpawned++; SkillFx("device", m.x, m.y); }
            }
        }

        void AddScore(double v, float x, float y, int tier, bool crit, bool golden)
        {
            score += v; scoreBump = 1;
            if (texts.Count > 25 && tier < 2 && !golden) return;
            if (texts.Count > 45) return;
            texts.Add(new FloatText { x = x + U.Rand(-8, 8), y = y, v = v, tier = tier, crit = crit, golden = golden });
        }

        public void AddPart(float x, float y, float vx, float vy, float life, Color col, float size)
        {
            if (parts.Count > 260) return;
            parts.Add(new Part { x = x, y = y, vx = vx, vy = vy, life = life, col = col, size = size });
        }

        public void AddLabel(string text, float x, float y, string col, float size)
        {
            if (labels.Count > 10) labels.RemoveAt(0);
            labels.Add(new Label { text = text, x = U.Clamp(x, 120, WW - 120), y = U.Clamp(y, 80, WH - 60), col = col, size = size });
        }

        void AddTime(float amount, string kind, float x, float y)
        {
            float cap = kind == "tspore" ? 15 : 10;
            float used = kind == "tspore" ? tsporeUsed : sipUsed;
            float a = Mathf.Min(amount, cap - used);
            if (a <= 0) return;
            if (kind == "tspore") tsporeUsed += a; else sipUsed += a;
            timeLeft += a; timeMax = Mathf.Max(timeMax, timeLeft);
            if (flyers.Count < 90) flyers.Add(new Flyer { x = x, y = y, sx = x, sy = y, dur = 0.6f, clock = true, tx = 960 / ZOOM, ty = 32 / ZOOM });
            AddLabel($"+{U.FmtN(a, 1)}초", x, y - 20, "#9ee6ff", 20);
        }
    }
}
