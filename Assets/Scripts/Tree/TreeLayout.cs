using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // Radial mycelium tree layout (prototype layoutTree + relaxTree). Coordinates are tree pixels centered on
    // the grandpa (y down). Used once to place the node objects in the scene; afterwards the scene positions win.
    public static class TreeLayout
    {
        public static readonly Dictionary<string, (float a, float span)> BRANCH = new Dictionary<string, (float, float)>
        {
            ["ed"] = (-90, 104), ["md"] = (30, 150), ["ps"] = (150, 106),
        };

        public static Dictionary<string, Vector2> Hubs()
        {
            var o = new Dictionary<string, Vector2>();
            foreach (var br in CAT_KEYS) { float a = BRANCH[br].a * Mathf.Deg2Rad; o[br] = new Vector2(Mathf.Cos(a) * 150, Mathf.Sin(a) * 150); }
            return o;
        }

        public static Dictionary<string, (Vector2 pos, int depth)> Compute()
        {
            var kids = new Dictionary<string, List<Node>>();
            foreach (var n in NODES)
            {
                if (n.core) continue;
                string p = n.parent != null && !NODE[n.parent].core ? n.parent : "hub_" + n.br;
                if (!kids.TryGetValue(p, out var l)) kids[p] = l = new List<Node>();
                l.Add(n);
            }
            int Leaves(string id) => kids.TryGetValue(id, out var k) ? k.Sum(c => Leaves(c.id)) : 1;
            var pos = new Dictionary<string, Vector2>();
            var depth = new Dictionary<string, int>();
            void Place(string id, float a0, float a1, int d, string br)
            {
                if (!kids.TryGetValue(id, out var k)) return;
                // 큰 가지를 가운데에 두어 선이 엇갈리지 않게
                var bySize = k.OrderByDescending(c => Leaves(c.id)).ToList();
                var arr = new List<Node>();
                for (int i = 0; i < bySize.Count; i++) { if (i % 2 == 1) arr.Add(bySize[i]); else arr.Insert(0, bySize[i]); }
                int tot = arr.Sum(c => Leaves(c.id));
                float a = a0;
                foreach (var c in arr)
                {
                    float w = (a1 - a0) * Leaves(c.id) / tot, mid = a + w / 2;
                    float rad = 190 + d * (br == "md" ? 150 : 135) - (c.sub ? 30 : 0);
                    pos[c.id] = new Vector2(Mathf.Cos(mid * Mathf.Deg2Rad) * rad, Mathf.Sin(mid * Mathf.Deg2Rad) * rad);
                    depth[c.id] = d;
                    Place(c.id, a, a + w, d + 1, br);
                    a += w;
                }
            }
            var hubs = Hubs();
            foreach (var br in CAT_KEYS)
            {
                var B = BRANCH[br];
                Place("hub_" + br, B.a - B.span / 2, B.a + B.span / 2, 1, br);
                pos["core_" + br] = hubs[br]; depth["core_" + br] = 0;
            }
            Relax(pos, hubs);
            return pos.ToDictionary(kv => kv.Key, kv => (kv.Value, depth[kv.Key]));
        }

        static float Gap(Node a, Node b) => a.sub && b.sub ? 96 : a.sub || b.sub ? 110 : 128;

        // 노드(이름표 포함)끼리 겹치지 않도록 서로 밀어낸다
        static void Relax(Dictionary<string, Vector2> pos, Dictionary<string, Vector2> hubs)
        {
            var fixedC = new List<(Vector2 p, float r)> { (Vector2.zero, 200) };
            foreach (var br in CAT_KEYS) fixedC.Add((hubs[br], 110));
            var L = NODES.Where(n => !n.core).ToList();
            for (int it = 0; it < 600; it++)
            {
                bool moved = false;
                for (int i = 0; i < L.Count; i++)
                {
                    var a = L[i];
                    for (int j = i + 1; j < L.Count; j++)
                    {
                        var b = L[j]; float need = Gap(a, b);
                        Vector2 d = pos[b.id] - pos[a.id]; float dist = d.magnitude;
                        if (dist >= need) continue;
                        if (dist < 0.01f) { d = Vector2.right; dist = 1; }
                        float push = (need - dist) / 2 + 0.5f; Vector2 nrm = d / dist;
                        pos[a.id] -= nrm * push; pos[b.id] += nrm * push;
                        moved = true;
                    }
                    foreach (var f in fixedC)
                    {
                        Vector2 d = pos[a.id] - f.p; float dist = d.magnitude; if (dist == 0) dist = 1;
                        if (dist < f.r) { pos[a.id] = f.p + d / dist * f.r; moved = true; }
                    }
                }
                if (!moved) break;
            }
        }
    }
}
