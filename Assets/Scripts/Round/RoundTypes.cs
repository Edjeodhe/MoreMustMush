using System;
using System.Collections.Generic;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // Simulation entities of a harvest round. Coordinates are prototype field pixels (x right, y down).
    public class Shape
    {
        public bool circle;
        public float x, y, r, x0, y0, x1, y1;
        public static Shape C(float x, float y, float r) => new Shape { circle = true, x = x, y = y, r = r };
        public static Shape Rect(float x0, float y0, float x1, float y1) => new Shape { circle = false, x0 = x0, y0 = y0, x1 = x1, y1 = y1 };
        public float CX => circle ? x : (x0 + x1) / 2;
        public float CY => circle ? y : (y0 + y1) / 2;
    }

    public class Shroom
    {
        public Species sp;
        public float x, y, ox, oy, r, squish, grow, sporeCd;
        public double hp, maxHp;
        public Colony col;
        public bool golden, cluster, solid, jelly, dead, wall, giant;
        public bool boss, bossClone;   // 보스 본체·분신 (환상 버섯)
        public BossDef bossDef;        // 보스면 그 정의 (그림·발광 색)
        public int dropMul = 1, qs;
        public string spore;
        public int viewId = -1;   // RoundView sprite slot
        public double hpLag = -1;  // HP bar trail: drains toward hp after a hit (view only)
        public float hitT = 9;     // seconds since this mushroom was last hit (HP bar pop / fade, hit flash)
        public float kx, ky;       // knock direction of the last hit (view only: the mushroom leans away and springs back)
    }

    public class Colony
    {
        public int id;
        public Species sp;
        public float x, y, R, vx, vy, turn;
        public Shape shape;
        public bool wall, wander;
        public int slot = -1;          // 초반 스테이지(RoundSim.lone)의 군락 자리. 같은 자리는 늘 같은 종으로 다시 생긴다
        public string side;
        public List<Shroom> members = new List<Shroom>();
        public int alive;
    }

    public class Device
    {
        public string type;
        public float x, y, r, hitT;                 // stump
        public float x0, y0, x1, y1;                // moss
        public bool hz;                             // moss, acorn orientation
        public Vector2[] pts; public int[] lit;     // acorn
        public bool gone;                           // acorn: 세 개 다 켜져 버프를 준 뒤 사라진다
        public int viewId = -1;

        readonly List<Shape> shapes = new List<Shape>(3);

        // Borrowed buffer: callers may read it, but must not retain or modify its contents.
        public List<Shape> Shapes()
        {
            int count = type == "acorn" ? (pts?.Length ?? 0) : type == "stump" || type == "moss" ? 1 : 0;
            while (shapes.Count < count) shapes.Add(new Shape());
            if (shapes.Count > count) shapes.RemoveRange(count, shapes.Count - count);
            for (int i = 0; i < count; i++)
            {
                var s = shapes[i]; s.circle = type != "moss";
                if (!s.circle) { s.x0 = x0; s.y0 = y0; s.x1 = x1; s.y1 = y1; }
                else if (type == "stump") { s.x = x; s.y = y; s.r = r; }
                else { var p = pts[i]; s.x = p.x; s.y = p.y; s.r = 18; }
            }
            return shapes;
        }
    }

    public class Ball
    {
        public string hv; public int hs;
        public float x, y, dx, dy = -1, spd, r, life, maxLife;
        public bool perm, inJelly, dead, noSplit;
        public float statMul = 1;   // 분신(스킬로 생긴 임시 수확기)은 기본 수확기의 CLONE_STAT 배 공격력
        public int combo, sharpen;
        public int hits;            // HUD 콤보: 바닥에 떨어지기 전까지 버섯을 때린 수 (combo는 바 튕김 수, 점수 배율용)
        public float accel, accelMul = 1, moss, slow, weak, clear, sharpMul = 1, magnet, magRate, stumpAtk;
        public float jf = 1, jTick, bladeT, bladeFx, ang, lastHitT, stumpT;
        public float pierceT, homeT, powT; public int powLv;
        public int sawStk, hitsLeft;   // 수리검 튕기기 누적 · 분열 소형 수확기가 사라지기까지 남은 충돌 (0 = 제한 없음)   // 바 스킬: 관통·유도·기운 북돋기 남은 시간
        public Shroom lastHit;
        public List<Vector2> trail = new List<Vector2>();
        public List<Shroom> inside = new List<Shroom>();
        public List<Shroom> insideNext = new List<Shroom>();   // StepBall이 inside와 번갈아 쓰는 버퍼
    }

    public class Cloud { public float x, y, r, t, max; public bool slow, weak; }
    public class Wave { public float x, y, r, max, t; public double dmg; public int depth; public HashSet<Shroom> hit = new HashSet<Shroom>(); public bool child; public Ball b; }
    public class Tornado { public float y, h, x, dir; public double dmg; public HashSet<Shroom> hit = new HashSet<Shroom>(); public Ball b; }
    public class Bolt { public List<Vector2> pts; public float t; public bool slash, big; public string col; public float jitterSeed; }
    public class Ring { public float x, y, r, t, dur; public string col; public bool quiet; }
    public class Part { public float x, y, vx, vy, t, life, size; public Color col; public byte shape; }   // shape: FxShape
    // one-shot sprite effects (hit bursts, spawn puffs, bar dust); the view draws them, the sim only ages them
    public enum FxShape : byte { Dot, Chip, Leaf, Impact, ImpactRing, Puff, Dust, Streak, Sprout, Drop, HitAnim, Twinkle, DotRing }
    public class Fx { public FxShape shape; public float x, y, t, dur, size, rot, spin, rise; public Color col; public bool flip; }
    public class FloatText { public float x, y, vx, vy, t; public double v; public int tier; public bool crit, golden, coin, dmg; public string view; }   // dmg: v가 점수가 아니라 준 피해 · view: 화면이 만든 글자 (시뮬레이션은 읽지 않음)
    // 콤보 숫자 (데미지 스킨): 수확기 하나에 하나, 버섯을 맞힐 때마다 n·crit·자리를 새로 쓰고 t를 0으로 되돌린다 (화면이 다시 튀게 그린다)
    public class Label { public string text; public float x, y, size, t; public string col; public string view; }   // view: 화면이 만든 글자 (숫자만 그림 글자)
    public class Flyer { public float x, y, sx, sy, tx, ty, t, dur; public Species sp; public bool golden, clock, done; public string cat; }
    public class Ember { public float x, y, vx, vy, t, life, s; }
    public class GiantWarn { public float x, y, t; }
    public class FeverMsg { public string text, sub; public float t; public int lv, n; public int pal = -1; public bool noIcon; }
    class Queued { public float t; public Action f; }
}
