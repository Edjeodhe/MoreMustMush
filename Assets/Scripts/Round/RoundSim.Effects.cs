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
        // ===== 스킬 효과 =====
        void SpawnCloud(float x, float y, string type)
        {
            clouds.Add(new Cloud { x = x, y = y, r = 120, t = 4, max = 4, slow = type == "slow" || type == "both", weak = type == "weak" || type == "both" });
            Snd.Tone(140, 0.3f, Snd.Wave.Sine, 0.3f, 0.6f, "puff", 0.1f);
        }

        void BurstAt(float x, float y, float rad, double dmg, Ball b, string col, bool quiet)
        {
            rings.Add(new Ring { x = x, y = y, r = rad, dur = quiet ? 0.2f : 0.35f, col = col, quiet = quiet });
            foreach (var m in QueryCircle(x, y, rad + 30))
            {
                float reach = rad + m.r * 0.5f;
                if (U.D2(x, y, m.x, m.y) < reach * reach) Damage(m, dmg, b, "skill");
            }
        }

        void ChainBolt(Shroom m0, Ball b)
        {
            int P = st.sk["bolt"].P, n = 2 + P;
            double dmg = st.atk * st.sk["bolt"].pm * st.skillDmg * Wk(b);
            var hit = new HashSet<Shroom> { m0 };
            var pts = new List<Vector2> { new Vector2(m0.x, m0.y) };
            float cx = m0.x, cy = m0.y;
            for (int i = 0; i < n; i++)
            {
                Shroom best = null; float bd = 280 * 280;
                foreach (var m in QueryCircle(cx, cy, 280)) { if (hit.Contains(m)) continue; float dd = U.D2(cx, cy, m.x, m.y); if (dd < bd) { bd = dd; best = m; } }
                if (best == null) break;
                hit.Add(best); pts.Add(new Vector2(best.x, best.y));
                float tx = best.x, ty = best.y;
                Damage(best, dmg, b, "skill");
                cx = tx; cy = ty;
            }
            bolts.Add(new Bolt { pts = pts, t = 0.3f });
            Snd.Skill(900);
        }

        void SpawnTornado(Ball b, string side)
        {
            if (tornados.Count >= 4) return;
            var sk = st.sk["tornado"]; float hh = (float)NF.tornado_h(sk.P);
            float dir = side == "l" ? 1 : side == "r" ? -1 : (b.x < 960 ? 1 : -1);
            tornados.Add(new Tornado { y = U.Clamp(b.y, FY0 + hh / 2, NO_SPAWN_Y), h = hh, x = dir > 0 ? FX0 : FX1, dir = dir, dmg = st.atk * 2 * sk.pm * st.skillDmg * Wk(b), b = b });
            SkillFx("tornado", b.x, b.y); Snd.Skill(420);
        }

        void MakeWave(float x, float y, int depth, Ball b)
        {
            var sk = st.sk["shock"];
            waves.Add(new Wave { x = x, y = y, max = (float)NF.shock_r(sk.P), dmg = st.atk * sk.pm * st.skillDmg * Wk(b), depth = depth, b = b });
            if (depth > maxChain) maxChain = depth;
            if (depth > 1) AddLabel($"연쇄 {depth}", x, y - 30, "#ffd0a0", 18 + depth);
            Snd.Skill(260 + depth * 40);
        }

        // 균사석 낙하: 살아 있는 버섯 하나를 골라 그 자리에 결정이 떨어져 범위 피해
        void CrystalMeteor()
        {
            var b = balls.FirstOrDefault(x => x.perm) ?? balls.FirstOrDefault();
            var alive = shrooms.Where(m => !m.dead).ToList();
            if (b == null || alive.Count == 0) return;
            var m = U.Pick(alive); var M = st.meteor.Value;
            if (bolts.Count < 40) bolts.Add(new Bolt { pts = new List<Vector2> { new Vector2(m.x + 60, m.y - 420), new Vector2(m.x + 30, m.y - 200), new Vector2(m.x, m.y) }, t = 0.18f, big = true, col = "#8ff0c8" });
            if (parts.Count < 200)
                for (int i = 0; i < 14; i++) { float a = U.Rand(0, U.TAU), v = U.Rand(80, 320); AddPart(m.x, m.y, Mathf.Cos(a) * v, Mathf.Sin(a) * v - 60, U.Rand(0.3f, 0.6f), U.Hex(i % 2 == 1 ? "#8ff0c8" : "#b8a8e0"), 5); }
            BurstAt(m.x, m.y, (float)M.r, st.atk * M.dmg * st.skillDmg, b, "#8ff0c8", false);
            SkillFx("gm_meteor", m.x, m.y);
            Snd.Skill(520);
        }

        // ===== 특수 버섯 =====
        public void SpawnSpecial()
        {
            var pool = SPECIALS.Where(s => !HasSpecial(s.id)).ToList();
            var kind = U.Pick(pool.Count > 0 ? pool : SPECIALS.ToList());
            Vector2? p = null;
            for (int a = 0; a < 40 && p == null; a++)
            {
                float x = U.Rand(FX0 + 90, FX1 - 90), y = U.Rand(FY0 + 90, NO_SPAWN_Y - 70);
                if (AreaFree(new List<Shape> { Shape.C(x, y, 50) })) p = new Vector2(x, y);
            }
            var pos = p ?? new Vector2(U.Rand(FX0 + 100, FX1 - 100), U.Rand(FY0 + 100, NO_SPAWN_Y - 100));
            double hp = st.atk * 2;   // 수확기로 직접 2번 맞히면 잡힌다
            float life = (float)st.specialLife;
            special = new SpecialMush { kind = kind, x = pos.x, y = pos.y, r = 50, hp = hp, maxHp = hp, t = life, max = life, hopT = 0.4f };
            AddLabel($"{kind.n} 등장! {life}초 안에 잡아요!", pos.x, pos.y - 80, kind.c2, 34);
            shake = Mathf.Max(shake, 0.3f); Snd.Rare();
            for (int i = 0; i < 20; i++) AddPart(pos.x, pos.y, U.Rand(-200, 200), U.Rand(-260, -40), 0.6f, i % 2 == 1 ? U.Hex(kind.c1) : Color.white, 5);
        }

        void HitSpecial(SpecialMush sm, double amt, Ball b)
        {
            sm.hp -= amt; sm.squish = 1;
            AddScore(TIERS[2].score * ScoreMulFor(null, b), sm.x, sm.y - sm.r, 2, false, false);
            for (int i = 0; i < 6; i++) AddPart(sm.x, sm.y, U.Rand(-150, 150), U.Rand(-200, -40), 0.4f, U.Hex(sm.kind.c1), 4);
            if (sm.hp <= 0) CatchSpecial(sm);
        }

        void CatchSpecial(SpecialMush sm)
        {
            special = null;
            var k = sm.kind;
            if (!HasSpecial(k.id))
            {
                G.specials[k.id] = true; specialGot = k.id; SaveGame();
                AddLabel($"{k.n} 포획! 영구 능력 획득", sm.x, sm.y - 70, "#fff7c2", 38);
                AddLabel(k.n + " · 버섯 농장에 들어와요!", sm.x, sm.y - 20, k.c2, 24);
            }
            else
            {
                double bonus = Math.Ceiling(StageGold(StageNow()) * 0.3);
                bonusGold += bonus;
                AddLabel($"{k.n} 포획! 보너스 +{U.Fmt(bonus)}골드", sm.x, sm.y - 70, "#ffe36e", 34);
            }
            flash = Mathf.Max(flash, 0.6f); shake = Mathf.Max(shake, 0.6f);
            hitstop = Mathf.Max(hitstop, 0.15f); hitstopCd = 0.6f;
            rings.Add(new Ring { x = sm.x, y = sm.y, r = 160, dur = 0.5f, col = k.c1 });
            for (int i = 0; i < 50; i++) { float a = U.Rand(0, U.TAU), s = U.Rand(80, 500); AddPart(sm.x, sm.y, Mathf.Cos(a) * s, Mathf.Sin(a) * s - 80, U.Rand(0.4f, 0.9f), U.Hex(i % 3 != 0 ? k.c1 : k.c2), 6); }
            Snd.Record();
        }

        void UpdateSpecial(float h)
        {
            if (specialAt >= 0 && runT >= specialAt && special == null) { specialAt = -1; SpawnSpecial(); }
            var sm = special;
            if (sm == null) return;
            sm.t -= h; sm.hitCd -= h; sm.appear = Mathf.Min(1, sm.appear + h * 3); sm.squish = Mathf.Max(0, sm.squish - h * 5);
            sm.hopT -= h;
            if (sm.hopT <= 0) { sm.hopT = U.Rand(0.6f, 1.1f); float a = U.Rand(0, U.TAU); sm.vx = Mathf.Cos(a) * 70; sm.vy = Mathf.Sin(a) * 70; }
            sm.x += sm.vx * h; sm.y += sm.vy * h;
            if (sm.x < FX0 + sm.r || sm.x > FX1 - sm.r) { sm.vx = -sm.vx; sm.x = U.Clamp(sm.x, FX0 + sm.r, FX1 - sm.r); }
            if (sm.y < FY0 + sm.r || sm.y > NO_SPAWN_Y - sm.r) { sm.vy = -sm.vy; sm.y = U.Clamp(sm.y, FY0 + sm.r, NO_SPAWN_Y - sm.r); }
            if (sm.t <= 0)
            {
                special = null;
                AddLabel($"{U.Iga(sm.kind.n)} 숲으로 도망쳤어요…", sm.x, sm.y - 50, "#e8dcc4", 28);
                for (int i = 0; i < 16; i++) AddPart(sm.x, sm.y, U.Rand(-80, 80), U.Rand(-120, -20), 0.6f, new Color(230 / 255f, 230 / 255f, 220 / 255f, 0.9f), 8);
            }
        }

        Vector2 FindGiantSpot()
        {
            for (int rad = 0; rad < 800; rad += 40)
            {
                int k = Mathf.Max(1, Mathf.RoundToInt(rad / 25f));
                for (int i = 0; i < k; i++)
                {
                    float a = (float)i / k * U.TAU + rad, x = WW / 2 + Mathf.Cos(a) * rad, y = NO_SPAWN_Y / 2 + Mathf.Sin(a) * rad * 0.6f;
                    if (x - 90 < FX0 || x + 90 > FX1 || y - 90 < FY0 || y + 90 > NO_SPAWN_Y) continue;
                    if (AreaFree(new List<Shape> { Shape.C(x, y, 90) })) return new Vector2(x, y);
                }
            }
            return new Vector2(WW / 2, NO_SPAWN_Y / 2);
        }

        void UpdateEffects(float h)
        {
            runT += h;
            // 군락지 재생
            for (int i = 0; i < regen.Count; i++) regen[i] -= h;
            for (int i = regen.Count - 1; i >= 0; i--)
                if (regen[i] <= 0) { if (SpawnColony(false)) regen.RemoveAt(i); else regen[i] = 0.5f; }
            int missing = st.maxCol - colonies.Count - regen.Count;
            for (int i = 0; i < missing; i++) regen.Add((float)st.regen);
            // 포자 구름
            foreach (var c in clouds) c.t -= h;
            clouds.RemoveAll(c => c.t <= 0);
            // 충격파
            foreach (var w in waves.ToList())
            {
                w.t += h; w.r = w.max * Mathf.Min(1, w.t / 0.22f);
                foreach (var m in QueryCircle(w.x, w.y, w.r + 30))
                {
                    if (w.hit.Contains(m)) continue;
                    float reach = w.r + m.r * 0.5f;
                    if (U.D2(w.x, w.y, m.x, m.y) < reach * reach) { w.hit.Add(m); Damage(m, w.dmg, w.b, "shock", 1, w); }
                }
            }
            waves.RemoveAll(w => w.t >= 0.4f);
            // 회오리
            foreach (var tn in tornados.ToList())
            {
                tn.x += tn.dir * 2200 * h;
                foreach (var m in shrooms.ToList())
                {
                    if (m.dead || tn.hit.Contains(m)) continue;
                    if (Mathf.Abs(m.y - tn.y) < tn.h / 2 + m.r && Mathf.Abs(m.x - tn.x) < 40 + m.r) { tn.hit.Add(m); Damage(m, tn.dmg, tn.b, "skill"); }
                }
            }
            tornados.RemoveAll(tn => !(tn.x > FX0 - 60 && tn.x < FX1 + 60));
            // 지연 실행
            foreach (var q in queue) q.t -= h;
            var ready = queue.Where(q => q.t <= 0).ToList();
            queue.RemoveAll(q => q.t <= 0);
            foreach (var q in ready) q.f();
            // 거대 버섯
            if (giantState == "pending" && runT >= 4)
            {
                var p = FindGiantSpot(); giantWarn = new GiantWarn { x = p.x, y = p.y }; giantState = "warn"; shake = 1;
                Snd.Tone(60, 0.8f, Snd.Wave.Saw, 0.4f, 0.7f);
            }
            else if (giantState == "warn")
            {
                giantWarn.t += h; shake = Mathf.Max(shake, 0.5f);
                if (runT >= 5)
                {
                    var sp = PickSpecies(false) ?? SPECIES[0]; var T = TIERS[sp.t];
                    double hp = T.hp * 40 * st.shroomHp;
                    var g = new Shroom { sp = sp, x = giantWarn.x, y = giantWarn.y, r = 90, hp = hp, maxHp = hp, giant = true, golden = U.Chance((float)st.golden), solid = true, spore = sp.spore, grow = 0.5f };
                    giant = g; shrooms.Add(g); giantWarn = null; giantState = "up";
                    AddLabel($"거대 {sp.n} 등장!", g.x, g.y - 130, "#ffffff", 40);
                    for (int i = 0; i < 40; i++) AddPart(g.x + U.Rand(-90, 90), g.y + U.Rand(40, 90), U.Rand(-150, 150), U.Rand(-300, -80), 0.8f, U.Hex("#8a6a4a"), 6);
                }
            }
            // 천둥번개 날씨
            if (st.wId == "thunder")
            {
                lightningT -= h;
                if (lightningT <= 0)
                {
                    lightningT = U.Rand(2.5f, 4.5f);
                    var alive = shrooms.Where(m => !m.dead).ToList();
                    if (alive.Count > 0)
                    {
                        var m = U.Pick(alive);
                        bolts.Add(new Bolt { pts = new List<Vector2> { new Vector2(m.x + U.Rand(-80, 80), 0), new Vector2(m.x + U.Rand(-30, 30), m.y * 0.5f), new Vector2(m.x, m.y) }, t = 0.35f, big = true });
                        flash = Mathf.Max(flash, 0.3f);
                        BurstAt(m.x, m.y, 70, st.atk * 2 * st.skillDmg, null, "#fff36b", true);
                        Snd.Skill(80);
                    }
                }
            }
            // 도토리
            if (acornT > 0) acornT -= h;
            foreach (var d in devices) if (d.type == "acorn" && d.offT > 0) { d.offT -= h; if (d.offT <= 0) d.lit = new int[3]; }
            foreach (var d in devices) if (d.hitT > 0) d.hitT -= h;
        }

        void UpdateFx(float h)
        {
            foreach (var p in parts) { p.t += h; p.x += p.vx * h; p.y += p.vy * h; p.vy += 500 * h; p.vx *= 0.97f; }
            parts.RemoveAll(p => p.t >= p.life);
            foreach (var tx in texts) { tx.t += h; tx.y -= 50 * h; }
            texts.RemoveAll(tx => tx.t >= (tx.tier >= 2 ? 1.2f : 0.8f));
            foreach (var l in labels) { l.t += h; l.y -= 25 * h; }
            labels.RemoveAll(l => l.t >= 1.4f);
            foreach (var r in rings) r.t += h;
            rings.RemoveAll(r => r.t >= r.dur);
            foreach (var bo in bolts) bo.t -= h;
            bolts.RemoveAll(bo => bo.t <= 0);
            foreach (var f in flyers)
            {
                f.t += h;
                if (f.t < 0) continue;
                if (!f.clock) { var s = HUD_SLOTS[f.cat]; f.tx = s.x / ZOOM; f.ty = s.y / ZOOM; }
                float k = Mathf.Min(1, f.t / f.dur), e = k * k * (3 - 2 * k);
                f.x = U.Lerp(f.sx, f.tx, e) + Mathf.Sin(k * Mathf.PI) * -60 * (f.sx > f.tx ? -1 : 1) * 0.3f;
                f.y = U.Lerp(f.sy, f.ty, e) - Mathf.Sin(k * Mathf.PI) * 120;
                if (k >= 1) { f.done = true; if (!f.clock) slotBump[f.cat] = 1; }
            }
            flyers.RemoveAll(f => f.done);
            foreach (var k in skillFlash.Keys.ToList()) skillFlash[k] -= h;
            scoreBump = Mathf.Max(0, scoreBump - h * 5);
            saleBump = Mathf.Max(0, saleBump - h * 5);
            foreach (var c in CAT_KEYS) slotBump[c] = Mathf.Max(0, slotBump[c] - h * 5);
            codexShake = Mathf.Max(0, codexShake - h * 1.5f);
            flashCd -= h;
            UpdateCombo(h);
            shake = Mathf.Max(0, shake - h * 2.5f);
            hitstopCd -= h;
            flash = Mathf.Max(0, flash - h * 2);
            barFx = Mathf.Max(0, barFx - h);
        }

        public double CurRec(string k) => k switch { "score" => score, "combo" => maxCombo, "harvest" => harvests, "balls" => maxBalls, _ => maxChain };

        void CheckRecords()
        {
            if (G.rounds == 0) return;
            foreach (var k in RECORD_KEYS)
            {
                double prev = G.rec.Get(k);
                if (!newRec.Contains(k) && prev > 0 && CurRec(k) > prev)
                {
                    newRec.Add(k);
                    AddLabel("신기록! " + RECORD_NAMES[k], WW / 2, WH * 0.28f, "#ffef7a", 40);
                    Snd.Record();
                }
            }
        }

        // 한 프레임 진행 (h: 게임 시간)
        public void SimFrame(float h)
        {
            t += h;
            if (phase == "run")
            {
                if (!DBG.infinite) timeLeft -= h;
                festOn = st.sk["fest"].on && timeLeft <= 5;
                if (timeLeft <= 0) { timeLeft = 0; phase = "end"; endT = 1.5f; festOn = false; Snd.Record(); }
            }
            foreach (var m in shrooms)
            {
                if (m.grow < 1) m.grow = Mathf.Min(1, m.grow + h / 0.35f);
                if (m.sporeCd > 0) m.sporeCd -= h;
                if (m.squish > 0) m.squish = Mathf.Max(0, m.squish - h * 5);
            }
            if (phase == "run")
            {
                UpdateWander(h);
                RebuildGrid();
                foreach (var b in balls.ToList()) UpdateBall(b, h);
                balls.RemoveAll(b => b.dead);
                if (balls.Count > maxBalls) maxBalls = balls.Count;
                UpdateEffects(h);
                UpdateSpecial(h);
                if (st.meteor.HasValue)
                {
                    meteorT = (meteorT ?? (float)st.meteor.Value.interval) - h;
                    if (meteorT <= 0) { meteorT = (float)st.meteor.Value.interval; CrystalMeteor(); }
                }
                shrooms.RemoveAll(m => m.dead);
                CheckRecords();
            }
            else if (phase == "end") endT -= h;
            UpdateFx(h);
        }

        // ===== 콤보·피버 =====
        double FeverMul() => fever > 0 ? FEVER[fever - 1].mul : 1;
        public double FeverMulPublic => FeverMul();

        void ComboHit()
        {
            chain = chainT > 0 ? chain + 1 : 1;
            chainT = (float)st.comboWin; chainBump = 1;
            if (chain > maxCombo) maxCombo = chain;
            int fi = FlameIdx(chain);
            flamePal = fi;
            if (chain % FLAME_STEP == 0)
            {
                feverMsg = new FeverMsg { text = "SUPER COMBO!", sub = $"{U.Fmt(chain)} COMBO", lv = 2, pal = fi, noIcon = true };
                flash = Mathf.Max(flash, 0.3f);
                Snd.Record(); Snd.Tone(260 + fi * 60, 0.6f, Snd.Wave.Saw, 0.3f, 3);
            }
            int lv = FeverLv(chain);
            if (lv > fever)
            {
                fever = lv; feverMsg = new FeverMsg { text = FEVER[lv - 1].n, lv = lv };
                flash = Mathf.Max(flash, 0.5f); shake = Mathf.Max(shake, 0.4f);
                Snd.Record(); Snd.Tone(lv == 2 ? 220 : 180, 0.5f, Snd.Wave.Saw, 0.35f, 3);
            }
        }

        void UpdateCombo(float h)
        {
            if (chainT > 0) { chainT -= h; if (chainT <= 0) { chain = 0; fever = 0; } }
            chainBump = Mathf.Max(0, chainBump - h * 6);
            feverK += (fever - feverK) * Mathf.Min(1, h * (fever > feverK ? 6 : 2.5f));
            if (chain > 0) flameInt += (FlameIntensity(chain) - flameInt) * Mathf.Min(1, h * 4);
            if (feverMsg != null) { feverMsg.t += h; if (feverMsg.t > 1.6f) feverMsg = null; }
            // 불티 (화면 좌표)
            float k = feverK;
            if (k > 0.05f)
            {
                int n = UnityEngine.Random.value < k * 0.45f ? 1 : 0;
                for (int i = 0; i < n && embers.Count < 60; i++)
                {
                    float side = UnityEngine.Random.value, sp = U.Rand(160, 380) * (0.7f + k * 0.3f);
                    if (side < 0.4f) embers.Add(new Ember { x = U.Rand(0, W), y = H + 5, vx = U.Rand(-40, 40), vy = -sp, life = U.Rand(0.8f, 1.6f), s = U.Rand(2, 5) });
                    else if (side < 0.6f) embers.Add(new Ember { x = U.Rand(0, W), y = -5, vx = U.Rand(-40, 40), vy = sp * 0.6f, life = U.Rand(0.6f, 1.2f), s = U.Rand(2, 4) });
                    else if (side < 0.8f) embers.Add(new Ember { x = -5, y = U.Rand(0, H), vx = sp * 0.6f, vy = U.Rand(-60, 20), life = U.Rand(0.6f, 1.2f), s = U.Rand(2, 4) });
                    else embers.Add(new Ember { x = W + 5, y = U.Rand(0, H), vx = -sp * 0.6f, vy = U.Rand(-60, 20), life = U.Rand(0.6f, 1.2f), s = U.Rand(2, 4) });
                }
            }
            foreach (var e in embers) { e.t += h; e.x += e.vx * h + Mathf.Sin((e.t + e.s) * 6) * 30 * h; e.y += e.vy * h; }
            embers.RemoveAll(e => e.t >= e.life);
        }

        // ===== 정산 =====
        public class Summary
        {
            public double score, scoreGold, bonusGold, value, net, goldMul, coinGold;
            public Dictionary<string, double> gains;
            public List<string> newSpecies, newGolden, unlocks;
            public List<(string id, int s)> newStars;
            public string specialGot, newTheme;
            public List<(string k, double old, double v)> recs = new List<(string, double, double)>();
            public bool goldHv;
            public int harvests;
        }

        // 점수 → 골드, 수확한 버섯 → 창고 (prototype finishRound)
        public Summary Finish()
        {
            double scoreGold = Math.Floor(score * st.goldMul);
            double bonus = Math.Floor(bonusGold);
            double val = Math.Floor(value);
            G.gold += scoreGold + bonus;
            var s = new Summary { score = score, scoreGold = scoreGold, bonusGold = bonus, value = val, net = scoreGold + bonus, goldMul = st.goldMul, gains = new Dictionary<string, double>(gains), harvests = harvests, coinGold = Math.Floor(coinGold) };
            foreach (var kv in bag) G.inv[kv.Key] = (G.inv.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            foreach (var k in RECORD_KEYS) { double cur = CurRec(k), old = G.rec.Get(k); if (cur > old) { s.recs.Add((k, old, cur)); G.rec.Set(k, cur); } }
            G.rounds++;
            if (G.rounds % QUEST_EVERY == 0) G.quests.Clear();   // 마을 의뢰는 몇 라운드마다 새로
            if (GoldenCount() >= 15 && !G.hv.ContainsKey("gold")) { G.hv["gold"] = 1; G.hvOn["gold"] = true; s.goldHv = true; }
            var newTheme = THEMES.FirstOrDefault(th => th.from > 1 && th.from == StageNow());
            if (newTheme != null) G.theme = newTheme.id;
            s.unlocks = CheckUnlocks();
            SaveGame();
            s.newSpecies = newSpecies.ToList(); s.newGolden = newGolden.ToList(); s.newStars = newStars.ToList(); s.specialGot = specialGot; s.newTheme = newTheme?.id;
            R = null;
            return s;
        }
    }
}
