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

        void BurstAt(float x, float y, float rad, double dmg, Ball b, string col, bool quiet, string src)
        {
            rings.Add(new Ring { x = x, y = y, r = rad, dur = quiet ? 0.2f : 0.35f, col = col, quiet = quiet });
            var q = QueryCircle(x, y, rad + 30);
            foreach (var m in q)
            {
                float reach = rad + m.r * 0.5f;
                if (U.D2(x, y, m.x, m.y) < reach * reach) Damage(m, dmg, b, "skill", src: src);
            }
            FreeQuery(q);
        }

        void ChainBolt(Shroom m0, Ball b, string src)
        {
            int P = st.sk["bolt"].P, n = 2 + P;
            double dmg = st.atk * st.sk["bolt"].pm * st.skillDmg * Wk(b);
            var hit = new HashSet<Shroom> { m0 };
            var pts = new List<Vector2> { new Vector2(m0.x, m0.y) };
            float cx = m0.x, cy = m0.y;
            for (int i = 0; i < n; i++)
            {
                Shroom best = null; float bd = 280 * 280;
                var q = QueryCircle(cx, cy, 280);
                foreach (var m in q) { if (hit.Contains(m)) continue; float dd = U.D2(cx, cy, m.x, m.y); if (dd < bd) { bd = dd; best = m; } }
                FreeQuery(q);
                if (best == null) break;
                hit.Add(best); pts.Add(new Vector2(best.x, best.y));
                float tx = best.x, ty = best.y;
                Damage(best, dmg, b, "skill", src: src);
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
            if (depth > 1) AddLabel(Loc.F("연쇄 {0}", depth), x, y - 30, "#ffd0a0", 18 + depth);
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
            BurstAt(m.x, m.y, (float)M.r, st.atk * M.dmg * st.skillDmg, b, "#8ff0c8", false, "meteor");
            SkillFx("gm_meteor", m.x, m.y);
            Snd.Skill(520);
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
                    if (AreaFree(Shape.C(x, y, 90))) return new Vector2(x, y);
                }
            }
            return new Vector2(WW / 2, NO_SPAWN_Y / 2);
        }

        void UpdateEffects(float h)
        {
            runT += h;
            // 군락지 재생 (보스 라운드에는 없음)
            if (!BossRound)
            {
                for (int i = 0; i < regen.Count; i++) regen[i] -= h;
                for (int i = regen.Count - 1; i >= 0; i--)
                    if (regen[i] <= 0) { if (SpawnColony(false)) regen.RemoveAt(i); else regen[i] = 0.5f; }
                int missing = MaxCol - colonies.Count - regen.Count;
                for (int i = 0; i < missing; i++) regen.Add((float)st.regen);
            }
            // 포자 구름
            foreach (var c in clouds) c.t -= h;
            clouds.RemoveAll(c => c.t <= 0);
            // 충격파
            // 아래 루프들은 시작할 때의 개수만큼만 돈다. 루프 안에서는 리스트에 더해지기만 하므로(빼기는 루프 뒤 RemoveAll) 예전 ToList() 사본과 같은 항목·같은 순서다
            for (int wi = 0, wn = waves.Count; wi < wn; wi++)
            {
                var w = waves[wi];
                w.t += h; w.r = w.max * Mathf.Min(1, w.t / 0.22f);
                var q = QueryCircle(w.x, w.y, w.r + 30);
                foreach (var m in q)
                {
                    if (w.hit.Contains(m)) continue;
                    float reach = w.r + m.r * 0.5f;
                    if (U.D2(w.x, w.y, m.x, m.y) < reach * reach) { w.hit.Add(m); Damage(m, w.dmg, w.b, "shock", 1, w, src: "shock"); }
                }
                FreeQuery(q);
            }
            waves.RemoveAll(w => w.t >= 0.4f);
            // 회오리
            for (int ti = 0, tnN = tornados.Count; ti < tnN; ti++)
            {
                var tn = tornados[ti];
                tn.x += tn.dir * 2200 * h;
                for (int mi = 0, mn = shrooms.Count; mi < mn; mi++)
                {
                    var m = shrooms[mi];
                    if (m.dead || tn.hit.Contains(m)) continue;
                    if (Mathf.Abs(m.y - tn.y) < tn.h / 2 + m.r && Mathf.Abs(m.x - tn.x) < 40 + m.r) { tn.hit.Add(m); Damage(m, tn.dmg, tn.b, "skill", src: "tornado"); }
                }
            }
            tornados.RemoveAll(tn => !(tn.x > FX0 - 60 && tn.x < FX1 + 60));
            // 지연 실행
            foreach (var q in queue) q.t -= h;
            if (queue.Count > 0)   // 비어 있으면 Where/ToList를 만들지 않는다
            {
                var ready = queue.Where(q => q.t <= 0).ToList();
                queue.RemoveAll(q => q.t <= 0);
                foreach (var q in ready) q.f();
            }
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
                    AddLabel(Loc.F("거대 {0} 등장!", sp.n), g.x, g.y - 130, "#ffffff", 40);
                    for (int i = 0; i < 40; i++) AddPart(g.x + U.Rand(-90, 90), g.y + U.Rand(40, 90), U.Rand(-150, 150), U.Rand(-300, -80), 0.8f, U.Hex("#8a6a4a"), 6);
                }
            }
            // 도토리
            if (acornT > 0) acornT -= h;
            devices.RemoveAll(d => d.gone);
            foreach (var d in devices) if (d.hitT > 0) d.hitT -= h;
        }

        void UpdateFx(float h)
        {
            foreach (var p in parts) { p.t += h; p.x += p.vx * h; p.y += p.vy * h; p.vy += (p.shape == (byte)FxShape.Chip ? 900 : 500) * h; p.vx *= 0.97f; }
            parts.RemoveAll(p => p.t >= p.life);
            foreach (var f in fxs) { f.t += h; f.y -= f.rise * h; f.rot += f.spin * h; }
            fxs.RemoveAll(f => f.t >= f.dur);
            barKick = Mathf.Max(0, barKick - h * 4);
            if (popStopCd > 0) popStopCd -= h;
            // numbers jump up, slow down and drift aside instead of sliding up at a constant speed
            foreach (var tx in texts) { tx.t += h; tx.x += tx.vx * h; tx.y += tx.vy * h; tx.vy = Mathf.Min(tx.vy + 300 * h, -20); tx.vx *= 0.94f; }
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
                if (!f.clock && harvestTargets.TryGetValue(f.cat, out var s)) { f.tx = s.x / ZOOM; f.ty = s.y / ZOOM; }
                float k = Mathf.Min(1, f.t / f.dur), e = k * k * (3 - 2 * k);
                f.x = U.Lerp(f.sx, f.tx, e) + Mathf.Sin(k * Mathf.PI) * -60 * (f.sx > f.tx ? -1 : 1) * 0.3f;
                f.y = U.Lerp(f.sy, f.ty, e) - Mathf.Sin(k * Mathf.PI) * 120;
                if (k >= 1) { f.done = true; if (!f.clock) slotBump[f.cat] = 1; }
            }
            flyers.RemoveAll(f => f.done);
            for (int i = 0; i < flashKeys.Count; i++) skillFlash[flashKeys[i]] -= h;
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
                    AddLabel(Loc.F("신기록! {0}", RECORD_NAMES[k]), WW / 2, WH * 0.28f, "#ffef7a", 40);
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
                m.hitT += h;
                if (m.hpLag > m.hp && m.hitT > 0.25f) m.hpLag = Math.Max(m.hp, m.hpLag - m.maxHp * h * 1.6);
            }
            if (phase == "run")
            {
                UpdateWander(h);
                RebuildGrid();
                for (int i = 0, n = balls.Count; i < n; i++) UpdateBall(balls[i], h);   // 루프 안에서는 더해지기만 한다 (빼기는 아래 RemoveAll)
                balls.RemoveAll(b => b.dead);
                if (balls.Count > maxBalls) maxBalls = balls.Count;
                UpdateEffects(h);
                UpdateBoss(h);
                UpdateBars(h);
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
            public string newTheme;
            public List<(string k, double old, double v)> recs = new List<(string, double, double)>();
            public bool goldHv;
            public int harvests;
            public int stored;                  // 세계수 창고에 넣은 버섯 수
            public string bossId;              // 보스 라운드였다면 그 보스
            public bool bossCleared;
            public double bossPenalty;          // 보스 실패로 깎인 골드
            public double bossGold; public int bossGem, bossShrooms;   // 보스 처치 보상 (골드·균사석·더 받은 버섯 수)
            public int stage, nextStage;
            public bool aborted;                // "라운드 끝내고 정산하기"로 중간에 끝냄 → 스테이지를 올리지 않는다 (번 골드는 받는다)
            public Dictionary<string, DmgStat> dmgBySrc, dmgByHv;   // 데미지 통계 (RoundSim.Stats)
        }

        // 지금까지 모은 점수·보너스로 받을 정산 골드 (라운드 중 HUD가 보여 준다)
        public double GoldSoFar => Math.Floor(score * st.goldMul) + Math.Floor(bonusGold);

        // 딴 버섯을 세계수 창고에 (도감 수확 수와 같은 단위). 보스 처치 보상 버섯도 같은 식으로 더하고, 세계수 버섯 획득량 보너스를 곱한다.
        // 난수를 쓰지 않는다 (봇 시뮬의 난수 순서가 그대로) → 넣은 개수
        int StoreRound(bool bossReward)
        {
            var add = new Dictionary<string, int>();
            int total = 0;
            foreach (var kv in picks)
            {
                double n = kv.Value + (bossReward ? (int)(kv.Value * BOSS_REWARD_MUL) : 0);
                int v = (int)U.JsRound(n * (1 + st.tree.harvest));
                if (v <= 0) continue;
                add[kv.Key] = v; total += v;
            }
            StoreAdd(add);
            return total;
        }

        // 점수 → 골드 (prototype finishRound). 딴 버섯은 도감·의뢰 진행과 세계수 창고(StoreRound)에 쓰인다
        public static Summary LastSummary;   // 마지막 정산 (밸런스 시뮬이 데미지 통계를 읽는다)
        public Summary Finish()
        {
            double scoreGold = Math.Floor(score * st.goldMul);
            double bonus = Math.Floor(bonusGold);
            // 보스에게 지면 이번 판 골드 ×BOSS_FAIL_GOLD
            double penalty = BossFailed ? Math.Ceiling((scoreGold + bonus) * (1 - BOSS_FAIL_GOLD)) : 0;
            // 보스를 쓰러뜨리면 "이번 판 골드와 직전 일반 판 골드 중 큰 쪽"의 BOSS_REWARD_MUL배를 더 받고 균사석도 받는다
            // (보스 타격 골드는 BOSS_SCORE_MUL로 줄었고, 처치하면 판이 바로 끝나므로 직전 일반 판이 기준)
            double bossGold = BossRound && bossCleared ? Math.Floor(Math.Max(scoreGold + bonus, G.lastNormalGold) * BOSS_REWARD_MUL) : 0;
            int bossGem = BossRound && bossCleared ? BossGem(G.stage) + (int)st.bossGemAdd : 0;
            int bossShrooms = BossRound && bossCleared ? BossShroomReward() : 0;
            G.gold += scoreGold + bonus - penalty + bossGold;
            G.gem += bossGem;
            // 중간에 끝낸 판: 스테이지를 완료하지 않는다. 보스 처치 보상 기준(직전 일반 판 골드)도 짧은 판으로 덮지 않는다
            bool aborted = earlyExitSec >= 0 && !(BossRound && bossCleared);
            if (!BossRound && !aborted)
            {
                G.lastNormalGold = scoreGold + bonus;   // 보스 처치 보상 기준 (보스가 안 나온 판: 일반 스테이지 + 보스 스테이지 파밍 판)
                if (!DBG.infinite) BagBaseUpdate(scoreGold + bonus, timeMax);   // 수확 보따리 골드 기준 (정산 뒤 갱신)
            }
            var s = new Summary { aborted = aborted, score = score, scoreGold = scoreGold, bonusGold = bonus, value = scoreGold + bonus, net = scoreGold + bonus - penalty + bossGold, goldMul = st.goldMul, gains = new Dictionary<string, double>(gains), harvests = harvests, coinGold = Math.Floor(coinGold), bossId = st.boss?.id, bossCleared = bossCleared, bossPenalty = penalty, bossGold = bossGold, bossGem = bossGem, bossShrooms = bossShrooms, stage = G.stage, dmgBySrc = Copy(dmgBySrc), dmgByHv = Copy(dmgByHv) };
            LogDamage(G.stage); LastSummary = s;
            foreach (var k in RECORD_KEYS) { double cur = CurRec(k), old = G.rec.Get(k); if (cur > old) { s.recs.Add((k, old, cur)); G.rec.Set(k, cur); } }
            G.rounds++;
            // 스테이지: 보스 스테이지는 보스를 쓰러뜨려야 넘어간다. 지면 그 스테이지에 머물고 보스 도전이 꺼진다(파밍)
            if (BossRound && !bossCleared) { G.bossOn = false; G.bossFailed = true; }   // 처음 지면 보스 도전 버튼이 열린다 (Unlock.BOSS)
            else if (!aborted && (!IsBossStage(G.stage) || BossRound)) G.stage++;
            s.nextStage = G.stage;
            QuestOnRound(harvests, picks, maxCombo);
            s.stored = StoreRound(bossShrooms > 0);
            if (GoldenCount() >= 15 && !G.hv.ContainsKey("gold")) { G.hvOn["gold"] = OnHvs().Count < HvSlots(); G.hv["gold"] = 1; s.goldHv = true; }
            var newTheme = s.nextStage > s.stage ? THEMES.FirstOrDefault(th => th.from > 1 && th.from == StageNow()) : null;
            if (newTheme != null) G.theme = newTheme.id;
            s.unlocks = CheckUnlocks();
            SaveGame();
            s.newSpecies = newSpecies.ToList(); s.newGolden = newGolden.ToList(); s.newStars = newStars.ToList(); s.newTheme = newTheme?.id;
            // 서버 로그 stage_end: 결과(보스: 처치 clear / 시간 초과 fail, 둘 다 "지금 끝내기"면 early_exit), 보스 남은 체력 % (분신 포함 합)
            string result = aborted ? "early_exit" : BossRound && !bossCleared ? "fail" : "clear";
            double bossMax = bosses.Sum(b => b.maxHp), bossHpPct = bossMax > 0 ? 100 * bosses.Sum(b => Math.Max(0, b.hp)) / bossMax : 0;
            Telemetry.StageEnd(G, s.stage, result, BossRound, score, s.net, earlyExitSec, bossHpPct, s.newSpecies);
            if (G.rounds == 1) Telemetry.Ftue(Telemetry.FTUE_FIRST_ROUND_END);
            R = null;
            return s;
        }
    }
}
