using System.Collections.Generic;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // 슬라이드 바 (전투 기획서 Ver05 · 01. 슬라이드 바). 바·분신 바는 같은 규칙을 따른다 (설치형 바는 2026-10-13 삭제: 숲 장치 기획에 없는 설치물):
    // 위에서 내려오는 수확기만 위로 튕기고(가운데 90°, 왼끝 150°, 오른끝 30°, 사이는 선형), 아래에서 올라오면 통과하되 바 버프는 준다.
    public class BarSeg { public float x, y, len, life; public bool clone; }

    public partial class RoundSim
    {
        public readonly List<BarSeg> extraBars = new List<BarSeg>();   // 분신 바
        public const float BAR_MAX_ANGLE = 60;   // 바 끝에서 수직으로부터 기우는 각도

        // 분신 바 (분신술): 맵 양 끝 중 본 바에서 먼 빈자리. 길이는 본 바와 같다. 최대 개수는 거울 분신술 (1 → 2)
        void SpawnCloneBar(float dur)
        {
            int n = 0; foreach (var o in extraBars) if (o.clone) n++;
            if (n >= st.cloneMax) return;
            float best = -1, bx = 0;
            foreach (float x in new[] { FX0 + barLen / 2, FX1 - barLen / 2 })
            {
                bool used = false; foreach (var o in extraBars) if (o.clone && Mathf.Abs(o.x - x) < 10) used = true;
                if (used) continue;
                float d = Mathf.Abs(x - barX);
                if (d > best) { best = d; bx = x; }
            }
            if (best < 0) return;
            extraBars.Add(new BarSeg { x = bx, y = BAR_Y, len = barLen, life = dur, clone = true });
        }

        void UpdateBars(float h)
        {
            for (int i = extraBars.Count - 1; i >= 0; i--)
            {
                var s = extraBars[i];
                if (s.life < 0) continue;
                s.life -= h;
                if (s.life <= 0) extraBars.RemoveAt(i);
            }
        }

        // 한 서브스텝의 바 판정. py: 이번 스텝 전 y
        void BarCollide(Ball b, float py)
        {
            BarCross(b, py, barX, BAR_Y, barLen, true);
            for (int i = 0; i < extraBars.Count; i++) { var s = extraBars[i]; BarCross(b, py, s.x, s.y, s.len, false); }
        }

        void BarCross(Ball b, float py, float x, float y, float len, bool main)
        {
            if (Mathf.Abs(b.x - x) > len / 2 + b.r * 0.6f) return;
            float top = y - BAR_T / 2;
            if (b.dy > 0 && b.y + b.r >= top && py + b.r <= top + 2) { BarHit(b, x, y, len, main); return; }
            // 아래에서 올라와 통과 (끼임 방지). 버프는 준다
            if (b.dy < 0 && py >= y && b.y < y) BarBuffs(b);
        }

        void BarHit(Ball b, float x, float y, float len, bool main)
        {
            float tt = U.Clamp((b.x - x) / (len / 2), -1, 1), a = tt * BAR_MAX_ANGLE * Mathf.Deg2Rad;
            b.dx = Mathf.Sin(a); b.dy = -Mathf.Cos(a);
            b.y = y - BAR_T / 2 - b.r;
            b.spd = Mathf.Max(b.spd, (float)(st.launch * st.barSpd));
            b.combo++;
            if (main)
            {
                barFx = 0.15f; barKick = 1; barHitX = b.x;
                // landing dust and a small burst: the bar takes the hit
                AddFx(FxShape.Dust, b.x, y - 2, 70, 0.32f, new Color(1, 0.95f, 0.82f, 0.85f), 0, 20);
                AddFx(FxShape.ImpactRing, b.x, y - 6, 54, 0.22f, new Color(1, 0.93f, 0.7f, 0.9f), FxR(0, 360), 0);
                if (b.perm) shake = Mathf.Max(shake, 0.06f);
            }
            Snd.Bar();
            SawBounce(b);
            for (int i = 0; i < 5; i++) AddPart(b.x, y - 8, U.Rand(-120, 120), U.Rand(-200, -60), 0.35f, U.Hex("#ffe9a8"), 3);
            BarBuffs(b);
            if (b.homeT > 0) HomeAim(b);
        }

        // 바 버프 (영구 핀볼만, 균사 트리 A안): 사냥 준비·가속·관통·유도·약손·지혜·피버는 바에 맞을 때마다, 분열·분신술은 확률로
        void BarBuffs(Ball b)
        {
            if (!b.perm) return;
            foreach (var id in ALWAYS_BUFFS) if (BuffOwned(id)) ApplyBuff(b, id, false);
            if (st.splitDur > 0 && U.Chance((float)SPLIT_P)) ApplyBuff(b, "md_split", true);
            if (st.cloneDur > 0 && U.Chance((float)CLONE_P)) ApplyBuff(b, "ps_clone", true);
        }

        static readonly string[] ALWAYS_BUFFS = { "power", "md_spring", "md_pierce", "md_homing", "md_heal", "md_wisdom", "md_fever" };
        static readonly string[] ALL_BUFFS = { "power", "md_spring", "md_pierce", "md_homing", "md_heal", "md_wisdom", "md_fever", "md_split", "ps_clone" };

        bool BuffOwned(string id) => id switch
        {
            "power" => st.powFlat > 0, "md_spring" => st.springDur > 0, "md_pierce" => st.pierceDur > 0, "md_homing" => st.homingDur > 0,
            "md_heal" => st.healDur > 0, "md_wisdom" => st.wisdomP > 0, "md_fever" => st.feverDur > 0, "md_split" => st.splitDur > 0, "ps_clone" => st.cloneDur > 0,
            _ => false,
        };

        // label: 이름표를 띄운다 (확률로 걸린 것만. 늘 걸리는 버프는 아이콘만 반짝)
        void ApplyBuff(Ball b, string id, bool label)
        {
            switch (id)
            {
                case "power": b.powT = POWER_DUR; break;
                case "md_spring": b.accel = (float)st.springDur; b.accelMul = SPRING_MUL; break;
                case "md_pierce": b.pierceT = (float)st.pierceDur; break;
                case "md_homing": b.homeT = (float)st.homingDur; break;
                case "md_heal": b.clear = (float)st.healDur; b.slow = 0; b.weak = 0; break;
                case "md_wisdom": b.wisT = WISDOM_DUR; break;
                case "md_fever": b.feverT = (float)st.feverDur; break;
                case "md_split":
                    // [분열]: 꼬마 수확기(공격력 10%)가 버프 시간 동안
                    foreach (var k in SpawnTemp(b.x, b.y - 4, 1, (float)st.splitDur, true, Mathf.Atan2(b.dy, b.dx))) k.statMul = KID_STAT;
                    break;
                case "ps_clone": SpawnCloneBar((float)st.cloneDur); break;
            }
            if (label) SkillFx(id, b.x, b.y); else Flash(id);
        }

        // 라운드 시작 (전설의 할아버지): 가진 바 버프마다 확률로 바로 건다
        void StartBuffs()
        {
            if (st.legendP <= 0) return;
            for (int i = 0, n = balls.Count; i < n; i++)   // 분열이 꼬마 수확기를 더하므로 시작할 때의 수만큼만
            {
                var b = balls[i];
                if (!b.perm) continue;
                foreach (var id in ALL_BUFFS) if (BuffOwned(id) && U.Chance((float)st.legendP)) ApplyBuff(b, id, true);
            }
        }

        // 수확기 스킬(이독제독·포자 폭발·연속 번개·회오리) 발동률 + [지혜] 버프
        double SkP(double p, Ball b) => p <= 0 ? 0 : p + (b != null && b.wisT > 0 ? st.wisdomP : 0);

        // 유도: 직선 거리로 가장 가까운 군락(등급 무관). 보스 스테이지에서는 살아 있는 보스. 숲 장치·바는 보지 않는다
        void HomeAim(Ball b)
        {
            float tx = 0, ty = 0, bd = float.MaxValue;
            bool found = false;
            foreach (var m in bosses) if (!m.dead) { float d = U.D2(b.x, b.y, m.x, m.y); if (d < bd) { bd = d; tx = m.x; ty = m.y; found = true; } }
            if (!found)
                foreach (var c in colonies) { float d = U.D2(b.x, b.y, c.x, c.y); if (d < bd) { bd = d; tx = c.x; ty = c.y; found = true; } }
            if (!found) return;
            float a = Mathf.Atan2(ty - b.y, tx - b.x);
            b.dx = Mathf.Cos(a); b.dy = Mathf.Sin(a); FixDir(b);
            if (b.dy > 0) b.dy = -b.dy;   // 바에서는 늘 위로
        }
    }
}
