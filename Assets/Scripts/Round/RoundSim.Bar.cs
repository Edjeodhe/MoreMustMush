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

        // 분신 바: 필드 가운데·왼쪽·오른쪽 중 본 바에서 가장 먼 빈자리
        void SpawnCloneBar(int P)
        {
            int n = 0; foreach (var o in extraBars) if (o.clone) n++;
            if (n >= BAR_CLONE_MAX) return;
            float best = -1, bx = 0;
            foreach (float k in new[] { 0.2f, 0.5f, 0.8f })
            {
                float x = WW * k;
                bool used = false; foreach (var o in extraBars) if (o.clone && Mathf.Abs(o.x - x) < 10) used = true;
                if (used) continue;
                float d = Mathf.Abs(x - barX);
                if (d > best) { best = d; bx = x; }
            }
            if (best < 0) return;
            extraBars.Add(new BarSeg { x = bx, y = BAR_Y, len = barLen, life = (float)(BAR_CLONE_DUR + P + st.barDur), clone = true });
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
            if (main) barFx = 0.15f;
            Snd.Bar();
            SawBounce(b);
            for (int i = 0; i < 5; i++) AddPart(b.x, y - 8, U.Rand(-120, 120), U.Rand(-200, -60), 0.35f, U.Hex("#ffe9a8"), 3);
            BarBuffs(b);
            if (b.homeT > 0) HomeAim(b);
        }

        // 바 버프·바 스킬 (영구 핀볼만)
        void BarBuffs(Ball b)
        {
            if (!b.perm) return;
            var sk = st.sk;
            bool Proc(string id) => sk[id].on && U.Chance((float)SkP(id, b));
            bool On(string id) => sk[id].on;
            float Dur(string id) => (float)((BarSkillDur(sk[id].P) + st.barDur) * st.buffMul);
            if (On("power")) { b.powT = Dur("power"); b.powLv = sk["power"].P; Flash("power"); }
            if (On("clear")) { b.clear = Dur("clear"); b.slow = 0; b.weak = 0; b.fog = 0; Flash("clear"); }
            if (Proc("accel")) { b.accel = 3 * (float)st.buffMul; b.accelMul = 1 + 0.15f * (float)sk["accel"].pm; SkillFx("accel", b.x, b.y); }
            if (Proc("sharpen")) { b.sharpen = Mathf.CeilToInt(3 * (float)st.buffMul); b.sharpMul = 2 * (float)sk["sharpen"].pm; SkillFx("sharpen", b.x, b.y); }
            if (Proc("pierce")) { b.pierceT = Dur("pierce"); SkillFx("pierce", b.x, b.y); }
            if (Proc("homing")) { b.homeT = Dur("homing"); SkillFx("homing", b.x, b.y); }
            if (Proc("split")) { SpawnTemp(b.x, b.y - 4, 1 + sk["split"].P, 6, false, Mathf.Atan2(b.dy, b.dx)); SkillFx("split", b.x, b.y); }
            if (Proc("barclone")) { SpawnCloneBar(sk["barclone"].P); SkillFx("barclone", b.x, b.y); }
            if (st.kidP > 0 && U.Chance((float)st.kidP)) { SpawnTemp(b.x, b.y - 4, 1, 8, true, -Mathf.PI / 2 + U.Rand(-0.7f, 0.7f)); SkillFx("md_kid", b.x, b.y); }
        }

        // 스킬 발동률: 기운 북돋기 버프 중이면 +POWER_SKILL × (1 + 위력)
        double SkP(string id, Ball b) => st.sk[id].p + (SKILLS[id].kind == "bar" ? st.barRate : 0) + (b != null && b.powT > 0 ? POWER_SKILL * (1 + b.powLv) + st.powSkill : 0);

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
