using System.Linq;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One mycelium tree node (prototype drawTree node + its link to the parent).
    // The node's transform position IS its tree position: move it in the scene to re-layout the tree.
    public class TreeNodeView : MonoBehaviour
    {
        public string nodeId;
        public SpriteRenderer glow, pulse, body, border, inner, badge;
        public TextMeshPro badgeText, label, line2, qmark;
        public LineRenderer link;
        public LineRenderer[] levelArcs = new LineRenderer[5];

        [HideInInspector] public float bend;
        [HideInInspector] public Vector2[] hairs;
        public float growAnim = -1;   // >=0 while the link is growing after a purchase
        public Node Node => NODE[nodeId];
        public Vector2 TreePos => new Vector2(transform.localPosition.x * Art.PPU, -transform.localPosition.y * Art.PPU);

        static readonly Color BUY = U.Hex("#4fb4ff"), POOR = U.Hex("#ff5a4a"), MAX = U.Hex("#ffd23a");
        static readonly Color TXT_BUY = U.Hex("#9fd6ff"), TXT_POOR = U.Hex("#ff9a8a");

        public static float Radius(Node n) => n.core ? 26 : n.sub ? 16 : 24;

        void Awake()
        {
            var r = U.Seeded(nodeId.GetHashCode() & 0xffff);
            bend = (float)(r() - 0.5) * 0.5f;
            hairs = new Vector2[3];
            for (int i = 0; i < 3; i++) hairs[i] = new Vector2((float)(r() * U.TAU), 22 + (float)r() * 40);
        }


        public void Draw(Vector2 parentPos, float now, bool hover, float camZ, float dt)
        {
            var n = Node;
            string s = NodeState(n);
            bool hidden = s == "hidden";
            foreach (Transform c in transform) c.gameObject.SetActive(!hidden);
            if (hidden) return;
            int L = Lv(n.id);
            var cost = L < n.max ? NodeCost(n, L) : default;
            bool afford = L < n.max && CanAfford(cost);
            string st = s == "locked" ? "locked" : s == "max" ? "max" : afford ? "buy" : "poor";
            Color col = st == "buy" ? BUY : st == "poor" ? POOR : MAX;
            var brc = U.Hex(CATS[n.br].color);
            float r = Radius(n);
            var me = TreePos;
            float sc = hover ? 1.15f : 1;

            // 연결 균사 (부모 → 나)
            bool owned = L > 0;
            link.gameObject.SetActive(!n.core);
            if (!n.core)
            {
                Vector2 p = parentPos;
                float mx = (p.x + me.x) / 2 - (me.y - p.y) * bend, my = (p.y + me.y) / 2 + (me.x - p.x) * bend;
                float k = 1;
                if (growAnim >= 0) { growAnim += dt; k = Mathf.Min(1, growAnim / 0.6f); if (k >= 1) growAnim = -1; }
                const int N = 20;
                link.positionCount = N + 1;
                for (int i = 0; i <= N; i++)
                {
                    float t = i / (float)N * k;
                    float x = (1 - t) * (1 - t) * p.x + 2 * (1 - t) * t * mx + t * t * me.x, y = (1 - t) * (1 - t) * p.y + 2 * (1 - t) * t * my + t * t * me.y;
                    if (owned && k < 1) { x += Mathf.Sin(t * 20 + now * 10) * 3; y += Mathf.Cos(t * 20 + now * 10) * 3; }
                    link.SetPosition(i, Art.P(x - me.x, y - me.y));
                }
                int depth = Mathf.Max(1, Mathf.RoundToInt(me.magnitude / 150));
                link.widthMultiplier = Mathf.Max(2, 9 - depth * 1.2f) / Art.PPU;
                var lc = owned ? U.Hex("#f2e6c8") : s == "avail" ? new Color(240 / 255f, 225 / 255f, 195 / 255f, 0.55f) : new Color(240 / 255f, 225 / 255f, 195 / 255f, 0.22f);
                link.startColor = link.endColor = lc;
                link.textureMode = owned ? LineTextureMode.Stretch : LineTextureMode.Tile;
            }

            glow.transform.localScale = pulse.transform.localScale = body.transform.localScale = border.transform.localScale = Vector3.one * sc;
            bool locked = st == "locked";
            qmark.gameObject.SetActive(locked);
            badge.gameObject.SetActive(!locked);
            inner.gameObject.SetActive(!locked);
            label.gameObject.SetActive(!locked && (!n.sub || hover || camZ > 0.8f || n.core));
            line2.gameObject.SetActive(label.gameObject.activeSelf);

            body.transform.localScale = Vector3.one * (r * 2 / Art.PPU) * sc;
            border.transform.localScale = Vector3.one * ((r * 2 + 4) / Art.PPU) * sc;
            if (locked)
            {
                body.color = new Color(30 / 255f, 20 / 255f, 12 / 255f, 0.8f);
                border.color = new Color(220 / 255f, 210 / 255f, 190 / 255f, 0.5f);
                glow.enabled = pulse.enabled = false;
                qmark.fontSize = r * 0.9f / 10f;
                foreach (var a in levelArcs) if (a != null) a.gameObject.SetActive(false);
                return;
            }
            // 바깥 빛: 강화 가능 = 파랗게 숨쉬는 빛 · 최대 = 은은한 금빛 · 균사석 = 청록
            glow.enabled = st == "buy" || st == "max" || n.gem;
            if (glow.enabled)
            {
                float k = st == "buy" ? 0.55f + 0.35f * Mathf.Sin(now * 4 + me.x * 0.01f) : 0.45f;
                Color gc = n.gem ? new Color(125 / 255f, 1, 200 / 255f, 0.3f + 0.15f * Mathf.Sin(now * 3 + me.x)) : st == "buy" ? new Color(79 / 255f, 180 / 255f, 1, k * 0.55f) : new Color(1, 210 / 255f, 58 / 255f, k * 0.5f);
                glow.color = gc;
                glow.transform.localScale = Vector3.one * ((r + (n.gem ? 22 : 16)) * 2 / Art.PPU) * sc;
            }
            pulse.enabled = st == "buy";
            if (pulse.enabled) { pulse.color = new Color(col.r, col.g, col.b, 0.35f + 0.35f * Mathf.Sin(now * 4 + me.x * 0.01f)); pulse.transform.localScale = Vector3.one * ((r + 11) * 2 / Art.PPU) * sc; }
            body.color = n.gem ? U.Hex("#1e2a2a") : st == "max" ? U.Hex("#3a2a18") : U.Hex("#2e2016");
            border.color = col;

            // 레벨 칸
            for (int i = 0; i < levelArcs.Length; i++)
            {
                var a = levelArcs[i];
                if (a == null) continue;
                bool show = n.max > 1 && i < n.max;
                a.gameObject.SetActive(show);
                if (!show) continue;
                float gap = 0.12f, seg = U.TAU / n.max, a0 = -Mathf.PI / 2 + i * seg + gap / 2, a1 = a0 + seg - gap;
                const int K = 10;
                a.positionCount = K + 1;
                for (int j = 0; j <= K; j++) { float ang = Mathf.Lerp(a0, a1, j / (float)K); a.SetPosition(j, Art.P(Mathf.Cos(ang) * (r + 6) * sc, Mathf.Sin(ang) * (r + 6) * sc)); }
                a.startColor = a.endColor = i < L ? (st == "max" ? MAX : U.Hex("#f2e6c8")) : new Color(242 / 255f, 230 / 255f, 200 / 255f, 0.18f);
            }

            // 속 그림
            float alpha = st == "poor" ? 0.55f : 1;
            if (n.core) { inner.sprite = SpriteDB.Icon("core_" + n.br); Art.FitPx(inner, r * 1.3f * sc); inner.color = new Color(1, 1, 1, alpha); }
            else if (n.gem) { inner.sprite = SpriteDB.Icon("gem"); Art.FitPx(inner, r * (L > 0 ? 1.4f : 1.05f) * sc); inner.color = new Color(1, 1, 1, alpha); }
            else if (n.skill != null || n.icon != null) { inner.sprite = SpriteDB.Icon(n.icon ?? SKILLS[n.skill].icon); Art.FitPx(inner, r * (L > 0 ? 1.45f : 1.1f) * sc); inner.color = new Color(1, 1, 1, L > 0 ? alpha : 0.6f * alpha); }
            else if (L == 0) { inner.sprite = Art.Circle; inner.transform.localScale = Vector3.one * (r * 0.6f / Art.PPU) * sc; inner.color = new Color(brc.r, brc.g, brc.b, alpha); }
            else { inner.sprite = SpriteDB.Icon(n.br); Art.FitPx(inner, r * (st == "max" ? 1.6f : 1.25f) * sc); inner.color = new Color(1, 1, 1, alpha); }

            // 오른쪽 위 배지
            float bR = n.sub ? 7 : 9;
            badge.transform.localPosition = Art.P(r * 0.78f * sc, -r * 0.78f * sc, -0.01f);
            badge.transform.localScale = Vector3.one * (bR * 2 / Art.PPU);
            badge.color = col;
            badgeText.text = st == "buy" ? "▲" : st == "poor" ? "!" : "★";
            badgeText.color = st == "max" ? U.Hex("#5a3a00") : Color.white;

            // 이름표 두 줄
            if (label.gameObject.activeSelf)
            {
                if (n.core)
                {
                    label.text = ""; line2.text = st == "max" ? "열림" : $"{U.Fmt(cost.gold)}G";
                    line2.color = st == "buy" ? TXT_BUY : st == "poor" ? TXT_POOR : MAX;
                    line2.transform.localPosition = Art.P(0, r + 14);
                }
                else
                {
                    label.text = n.sub ? n.n.Substring(n.n.LastIndexOf(' ') + 1) : n.n;
                    label.fontSize = (n.sub ? 15 : 18) / 10f;
                    label.color = n.gem ? U.Hex("#a8f5d4") : st == "poor" ? U.Hex("#cbbfa8") : U.Hex("#fff6e0");
                    label.transform.localPosition = Art.P(0, r + 14);
                    string lvTxt = n.max > 1 ? $"Lv {L}/{n.max}" : "";
                    string costTxt = string.Join(" + ", new[] { cost.gold > 0 ? $"{U.Fmt(cost.gold)}G" : "", cost.gem > 0 ? $"균사석 {U.Fmt(cost.gem)}" : "" }.Where(x => x != ""));
                    line2.text = st == "max" ? (n.max > 1 ? $"MAX {L}/{n.max}" : "MAX") : $"{lvTxt}{(lvTxt != "" ? " · " : "")}{costTxt}";
                    line2.color = st == "buy" ? TXT_BUY : st == "poor" ? TXT_POOR : MAX;
                    line2.transform.localPosition = Art.P(0, r + 32);
                }
            }
        }
    }
}
