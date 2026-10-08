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
        public int dropMul = 1, qs;
        public string spore;
        public int viewId = -1;   // RoundView sprite slot
    }

    public class Colony
    {
        public int id;
        public Species sp;
        public float x, y, R, vx, vy, turn;
        public Shape shape;
        public bool wall, wander;
        public string side;
        public List<Shroom> members = new List<Shroom>();
        public int alive;
    }

    public class Device
    {
        public string type;
        public float x, y, r, hitT;                 // stump
        public float x0, y0, x1, y1;                // moss, stream
        public bool hz;                             // moss, acorn orientation
        public Vector2 a, b;                        // mole
        public float fx, fy;                        // stream flow
        public Vector2[] pts; public int[] lit; public float offT;   // acorn
        public int viewId = -1;

        readonly List<Shape> shapes = new List<Shape>(3);

        // Borrowed buffer: callers may read it, but must not retain or modify its contents.
        public List<Shape> Shapes()
        {
            int count = type == "mole" ? 2 : type == "acorn" ? (pts?.Length ?? 0) :
                type == "stump" || type == "moss" || type == "stream" ? 1 : 0;
            while (shapes.Count < count) shapes.Add(new Shape());
            if (shapes.Count > count) shapes.RemoveRange(count, shapes.Count - count);
            for (int i = 0; i < count; i++)
            {
                var s = shapes[i]; s.circle = type != "moss" && type != "stream";
                if (!s.circle) { s.x0 = x0; s.y0 = y0; s.x1 = x1; s.y1 = y1; }
                else if (type == "stump") { s.x = x; s.y = y; s.r = r; }
                else { var p = type == "mole" ? (i == 0 ? a : b) : pts[i]; s.x = p.x; s.y = p.y; s.r = type == "mole" ? 30 : 18; }
            }
            return shapes;
        }
    }

    public class Ball
    {
        public string hv; public int hs;
        public float x, y, dx, dy = -1, spd, r, life, maxLife;
        public bool perm, inJelly, dead, noSplit;
        public int combo, sharpen;
        public float accel, accelMul = 1, moss, slow, weak, clear, sharpMul = 1, magnet, magRate, mole;
        public float jf = 1, jTick, bladeT, bladeFx, ang, lastHitT, stumpT;
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
    public class Part { public float x, y, vx, vy, t, life, size; public Color col; }
    public class FloatText { public float x, y, t; public double v; public int tier; public bool crit, golden, coin; public string view; }   // view: 화면이 만든 글자 (시뮬레이션은 읽지 않음)
    public class Label { public string text; public float x, y, size, t; public string col; }
    public class Flyer { public float x, y, sx, sy, tx, ty, t, dur; public Species sp; public bool golden, clock, done; public string cat; }
    public class Ember { public float x, y, vx, vy, t, life, s; }
    public class SpecialMush { public Special kind; public float x, y, r, t, max, hitCd, squish, vx, vy, hopT, appear; public double hp, maxHp; }
    public class GiantWarn { public float x, y, t; }
    public class FeverMsg { public string text, sub; public float t; public int lv; public int pal = -1; public bool noIcon; }
    class Queued { public float t; public Action f; }
}
