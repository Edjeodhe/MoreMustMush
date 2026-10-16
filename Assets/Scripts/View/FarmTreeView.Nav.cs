using System.Collections.Generic;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 나무 위 길 (2026-10-16): 정령이 서고 오를 수 있는 곳을 나무 그림마다 눈으로 짚은 지도 Resources/Rig/tree_nav.json.
    //   node: 그림 비율 좌표 (u 오른쪽, v 아래) · ground = 땅에서 들어오는 곳 · spot = 머물며 노는 곳 (발판·버섯 계단·그네·수관 위)
    //   edge: walk 발판·가지 위를 걷기 · climb 사다리·줄기·밧줄 오르기 · hop 선반버섯 계단을 폴짝
    // 확인 그림: python Tools/art/tree_nav_preview.py. 지도가 없는 그림(새싹 tree_0)은 오를 곳이 없다.
    public partial class FarmTreeView
    {
        [System.Serializable] public class NavNode { public string id; public float u, v; public bool ground, spot; }
        [System.Serializable] class NavPic { public int pic; public NavNode[] nodes; public string[][] edges; }
        [System.Serializable] class NavPicRaw { public int pic; public NavNode[] nodes; }
        [System.Serializable] class NavFileRaw { public NavPicRaw[] pics; }

        public class Nav
        {
            public NavNode[] nodes;
            public readonly List<(int to, string kind)>[] adj;
            public Nav(NavNode[] n) { nodes = n; adj = new List<(int, string)>[n.Length]; for (int i = 0; i < n.Length; i++) adj[i] = new List<(int, string)>(); }
        }
        static Dictionary<int, Nav> navs;

        // JsonUtility는 배열의 배열(edges)을 못 읽어서 edges는 직접 읽는다
        static void LoadNav()
        {
            navs = new Dictionary<int, Nav>();
            var ta = Resources.Load<TextAsset>("Rig/tree_nav");
            if (ta == null) return;
            var raw = JsonUtility.FromJson<NavFileRaw>(ta.text);
            var root = Newtonsoft.Json.Linq.JObject.Parse(ta.text);
            var pics = (Newtonsoft.Json.Linq.JArray)root["pics"];
            for (int p = 0; p < raw.pics.Length; p++)
            {
                var nav = new Nav(raw.pics[p].nodes);
                var index = new Dictionary<string, int>();
                for (int i = 0; i < nav.nodes.Length; i++) index[nav.nodes[i].id] = i;
                foreach (var e in (Newtonsoft.Json.Linq.JArray)pics[p]["edges"])
                {
                    if (!index.TryGetValue((string)e[0], out int a) || !index.TryGetValue((string)e[1], out int b)) continue;
                    string kind = (string)e[2];
                    nav.adj[a].Add((b, kind)); nav.adj[b].Add((a, kind));
                }
                navs[raw.pics[p].pic] = nav;
            }
        }

        public Nav CurrentNav()
        {
            if (navs == null) LoadNav();
            Layout();
            return navs.TryGetValue(WT.Picture(Stage()), out var n) ? n : null;
        }

        // 그림 비율 → 무대 px (나무 그림은 밑동(WT.x, WT.y)에 아래 가운데로 붙어 있다)
        public Vector2 NavPos(NavNode n) { Layout(); return new Vector2(WT.x + (n.u - 0.5f) * widthPx, WT.y - (1 - n.v) * heightPx); }
        public Vector2 NavPos(int i) { var nav = CurrentNav(); return nav == null ? new Vector2(WT.x, WT.y) : NavPos(nav.nodes[i]); }

        // 가장 가까운 노드 (spotOnly면 머무는 곳만)
        public int NearestNav(Vector2 p, bool spotOnly, float maxDist = 1e9f)
        {
            var nav = CurrentNav(); if (nav == null) return -1;
            int best = -1; float bd = maxDist * maxDist;
            for (int i = 0; i < nav.nodes.Length; i++)
            {
                if (spotOnly && !nav.nodes[i].spot) continue;
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

        // 머무는 곳 하나 (아무 데나), 그리고 그곳으로 가는 땅 입구
        public int RandomSpot()
        {
            var nav = CurrentNav(); if (nav == null) return -1;
            var spots = new List<int>();
            for (int i = 0; i < nav.nodes.Length; i++) if (nav.nodes[i].spot) spots.Add(i);
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
