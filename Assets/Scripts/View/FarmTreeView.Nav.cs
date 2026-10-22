using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 세계수 도면 (2026-10-20, Resources/Rig/tree_layout.json): 나무 그림마다 그림 비율 좌표(u 오른쪽, v 아래)로 적은 자리들.
    // 그림보다 도면이 먼저다 — 도면을 정하고 그 도면을 가이드로 나무 그림을 새로 뽑는다(Tools/tree-layout).
    //   nodes/edges 정령 길 (2026-10-16 tree_nav.json에서 옮김):
    //     node: ground = 땅에서 들어오는 곳 · spot = 머물며 노는 곳 (발판·버섯 계단·그네·수관 위)
    //     edge: walk 발판·가지 위를 걷기 · climb 사다리·줄기·밧줄 오르기 · hop 선반버섯 계단을 폴짝
    //   mush: 구역(root·trunk·branch·crown)별 나무버섯 자리, 앞에서부터 슬롯 순서대로 쓴다 (모자라면 Layout의 공식 자리)
    //   deco: 장식 id별 자리 — 그 단계에 열리는 장식(합류한 꼬마의 장식 3종씩)이 모두 제 자리를 가진다.
    //         걸이형은 고리(그림 위 끝)가, 설치형은 바닥(그림 아래 끝)이 이 점에 닿는다. s = 크기 배율(기본 1)
    // 편집: node Tools/tree-layout/serve.mjs (웹 편집기) · 확인·가이드 그림: python Tools/tree-layout/make_guide.py
    // 도면이 없는 그림은 오를 곳이 없고(새싹 tree_0), 버섯·장식은 공식 자리에 놓인다.
    public partial class FarmTreeView
    {
        [System.Serializable] public class NavNode { public string id; public float u, v; public bool ground, spot; }

        public class Nav
        {
            public NavNode[] nodes;
            public readonly List<(int to, string kind)>[] adj;
            public Nav(NavNode[] n) { nodes = n; adj = new List<(int, string)>[n.Length]; for (int i = 0; i < n.Length; i++) adj[i] = new List<(int, string)>(); }
        }
        public class DecoSpot { public float u, v, s = 1; }
        public class PicLayout
        {
            public Nav nav;
            public float foot = 1;   // 그림에서 땅에 닿는 선 (v): 이 선이 밑동 WT.y에 온다 (fit_spots.py가 그림에서 잰다)
            public readonly Dictionary<string, List<Vector2>> mush = new Dictionary<string, List<Vector2>>();
            public readonly Dictionary<string, DecoSpot> deco = new Dictionary<string, DecoSpot>();
        }
        static Dictionary<int, PicLayout> pics;

        static void LoadLayout()
        {
            pics = new Dictionary<int, PicLayout>();
            var ta = Resources.Load<TextAsset>("Rig/tree_layout");
            if (ta == null) return;
            var root = JObject.Parse(ta.text);
            foreach (JObject p in (JArray)root["pics"])
            {
                var pl = new PicLayout { foot = p["foot"] != null ? (float)p["foot"] : 1 };
                var nodes = p["nodes"]?.ToObject<NavNode[]>();
                if (nodes != null && nodes.Length > 0)
                {
                    var nav = new Nav(nodes);
                    var index = new Dictionary<string, int>();
                    for (int i = 0; i < nodes.Length; i++) index[nodes[i].id] = i;
                    foreach (var e in (JArray)p["edges"] ?? new JArray())
                    {
                        if (!index.TryGetValue((string)e[0], out int a) || !index.TryGetValue((string)e[1], out int b)) continue;
                        string kind = (string)e[2];
                        nav.adj[a].Add((b, kind)); nav.adj[b].Add((a, kind));
                    }
                    pl.nav = nav;
                }
                if (p["mush"] is JObject mush)
                    foreach (var z in mush.Properties())
                        pl.mush[z.Name] = ((JArray)z.Value).Select(q => new Vector2((float)q[0], (float)q[1])).ToList();
                if (p["deco"] is JObject deco)
                    foreach (var d in deco.Properties())
                    {
                        var q = (JArray)d.Value;
                        pl.deco[d.Name] = new DecoSpot { u = (float)q[0], v = (float)q[1], s = q.Count > 2 ? (float)q[2] : 1 };
                    }
                pics[(int)p["pic"]] = pl;
            }
        }

        // 그림 하나의 도면 (Layout()을 부르지 않는다: Layout 안에서도 쓴다)
        static PicLayout PicData(int pic)
        {
            if (pics == null) LoadLayout();
            return pics.TryGetValue(pic, out var pl) ? pl : null;
        }

        // 장식이 놓이는 구역 (꾸미기 창의 뿌리·줄기·가지·수관 탭). 설치형은 땅에 놓이니 뿌리, 걸이형은 지금 단계 도면의 걸이 자리를
        // 높이 순으로 세 몫으로 나눠 아래부터 줄기·가지·수관 (도면의 걸이 자리는 나무 높이 33~65%에 몰려 있어 구역 높이 Zone.cy로 자르면
        // 수관이 비었다). 도면에 자리가 없으면 가지
        public static string DecoZone(Deco d)
        {
            if (!d.hang) return ZONES[0].id;
            var at = PicData(WT.Picture(Stage()));
            if (at == null || !at.deco.TryGetValue(d.id, out var s)) return "branch";
            int below = 0, n = 0;
            foreach (var x in DECOS)
                if (x.hang && at.deco.TryGetValue(x.id, out var o)) { n++; if (o.v > s.v || (o.v == s.v && string.CompareOrdinal(x.id, d.id) < 0)) below++; }   // v가 클수록 아래
            int k = Mathf.Min(2, below * 3 / Mathf.Max(1, n));
            return ZONES[1 + k].id;
        }

        // 구역에 놓이는 장식 자리들의 평균 높이 (밑동 0 ~ 꼭대기 1). 꾸미기 창이 그 높이로 화면을 옮긴다. 자리가 없으면 Zone.cy
        public static float DecoZoneHeight(string zone)
        {
            var at = PicData(WT.Picture(Stage()));
            float sum = 0; int n = 0;
            if (at != null && at.foot > 0)
                foreach (var d in DECOS)
                    if (at.deco.TryGetValue(d.id, out var s) && DecoZone(d) == zone) { sum += (at.foot - s.v) / at.foot; n++; }
            return n > 0 ? sum / n : ZONE.TryGetValue(zone ?? "", out var z) ? z.cy : 0;
        }

        // 나무 길: 정령은 WT.climbFrom(4)단계부터 나무에 오른다 (그 전 단계는 도면에 길이 있어도 없는 것으로)
        public Nav CurrentNav()
        {
            Layout();
            return Stage() >= WT.climbFrom ? PicData(WT.Picture(Stage()))?.nav : null;
        }

        // ===== 정령 크기와 손 닿는 범위 (도면 맞춤 Tools/tree-layout/fit_spots.py와 같은 값) =====
        // 서 있는 정령: 화각 1배에서 110 × 155 무대 px (플레이에서 잰 값), FarmCritter가 화각^0.75로 키운다
        public const float SPIRIT_W = 110, SPIRIT_H = 155;
        public static Vector2 SpiritPx => new Vector2(SPIRIT_W, SPIRIT_H) * Mathf.Pow(FarmView.Zoom, 0.75f);
        // 발(feet)에 선 정령이 p를 딸 수 있나: 옆으로 0.8폭, 위로 1.1키, 아래로 0.3키
        public static bool Reach(Vector2 feet, Vector2 p)
        {
            var s = SpiritPx;
            return Mathf.Abs(p.x - feet.x) <= 0.8f * s.x && p.y >= feet.y - 1.1f * s.y && p.y <= feet.y + 0.3f * s.y;
        }
        // 땅에서 딸 때 서는 높이: 밑동부터 나무 높이 5%까지 (뿌리 둔덕 위). 그 위로 1.1키까지 닿는다
        public float GroundFeetY => WT.y - 0.05f * Height;
        public bool GroundReach(Vector2 p) => p.y >= GroundFeetY - 1.1f * SpiritPx.y;

        // 나무버섯 slot을 딸 마디: 땅에서 닿으면 -1 (땅에서 딴다). 아니면 손 닿는 마디 중 가장 가까운 곳 (다른 정령이 있는 마디는 피하고,
        // 닿는 마디가 없으면 그냥 가장 가까운 마디). 길이 없으면(4단계 전) -1
        public int WorkNode(int slot, ICollection<int> skip)
        {
            var nav = CurrentNav(); if (nav == null || slot < 0 || slot >= slots.Count) return -1;
            var p = slots[slot].pos;
            if (GroundReach(p)) return -1;
            int best = -1, bestAny = -1; float bd = 1e18f, bdAny = 1e18f;
            for (int i = 0; i < nav.nodes.Length; i++)
            {
                if (nav.nodes[i].ground) continue;
                var f = NavPos(i); float d = (f - p).sqrMagnitude;
                if (d < bdAny && (skip == null || !skip.Contains(i))) { bdAny = d; bestAny = i; }
                if (d < bd && Reach(f, p) && (skip == null || !skip.Contains(i))) { bd = d; best = i; }
            }
            return best >= 0 ? best : bestAny;
        }

        // 그림 비율 → 무대 px (나무 그림은 밑동(WT.x, WT.y)에 아래 가운데로 붙어 있다)
        Vector2 UV(float u, float v) => new Vector2(WT.x + (u - 0.5f) * widthPx, WT.y - (footV - v) * heightPx);   // v = foot가 밑동
        public Vector2 NavPos(NavNode n) { Layout(); return UV(n.u, n.v); }
        public Vector2 NavPos(int i) { var nav = CurrentNav(); return nav == null ? new Vector2(WT.x, WT.y) : NavPos(nav.nodes[i]); }

        // 가장 가까운 노드 (spotOnly면 머무는 곳만, skip = 다른 정령이 차지한 마디)
        public int NearestNav(Vector2 p, bool spotOnly, float maxDist = 1e9f, ICollection<int> skip = null)
        {
            var nav = CurrentNav(); if (nav == null) return -1;
            int best = -1; float bd = maxDist * maxDist;
            for (int i = 0; i < nav.nodes.Length; i++)
            {
                if (spotOnly && !nav.nodes[i].spot) continue;
                if (skip != null && skip.Contains(i)) continue;
                float d = (NavPos(i) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        // 가장 가까운 땅 입구에서 to까지 (또는 from → to) 노드 경로 (BFS: 마디 수가 가장 적은 길). 없으면 null
        public List<int> NavPath(int from, int to)
        {
            var nav = CurrentNav(); if (nav == null || from < 0 || to < 0) return null;
            var prev = new int[nav.nodes.Length];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var q = new Queue<int>(); q.Enqueue(from); prev[from] = -1;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == to) break;
                foreach (var (n, _) in nav.adj[c]) if (prev[n] == -2) { prev[n] = c; q.Enqueue(n); }
            }
            if (prev[to] == -2) return null;
            var path = new List<int>();
            for (int c = to; c != -1; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        // 노드에서 땅 입구까지 가장 짧은 길
        public List<int> NavPathToGround(int from)
        {
            var nav = CurrentNav(); if (nav == null || from < 0) return null;
            List<int> best = null;
            for (int i = 0; i < nav.nodes.Length; i++)
            {
                if (!nav.nodes[i].ground) continue;
                var p = NavPath(from, i);
                if (p != null && (best == null || p.Count < best.Count)) best = p;
            }
            return best;
        }

        public string NavEdge(int a, int b)
        {
            var nav = CurrentNav(); if (nav == null) return "walk";
            foreach (var (n, k) in nav.adj[a]) if (n == b) return k;
            return "walk";
        }

        // 머무는 곳 하나 (다른 정령이 차지하지 않은 곳 아무 데나, 없으면 -1), 그리고 그곳으로 가는 땅 입구
        // 도면의 머무는 곳은 정령 한 마리 크기만큼 서로 떨어져 있다 (Tools/tree-layout/fit_spots.py) → 한 곳에 하나씩이면 겹치지 않는다
        public int RandomSpot(ICollection<int> skip = null)
        {
            var nav = CurrentNav(); if (nav == null) return -1;
            var spots = new List<int>();
            for (int i = 0; i < nav.nodes.Length; i++) if (nav.nodes[i].spot && (skip == null || !skip.Contains(i))) spots.Add(i);
            return spots.Count == 0 ? -1 : spots[Random.Range(0, spots.Count)];
        }
        public int GroundFor(int spot)
        {
            var nav = CurrentNav(); if (nav == null) return -1;
            int best = -1, bl = int.MaxValue;
            for (int i = 0; i < nav.nodes.Length; i++)
            {
                if (!nav.nodes[i].ground) continue;
                var p = NavPath(i, spot);
                if (p != null && p.Count < bl) { bl = p.Count; best = i; }
            }
            return best;
        }
    }
}
