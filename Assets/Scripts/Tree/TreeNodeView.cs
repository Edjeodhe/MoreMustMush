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

        // 밝은 바탕(두들 4차) 위 배지: 강화 가능 = 하늘색 · 재화 부족 = 산호 · 최대 = 새싹 초록
        static readonly Color BUY = U.Hex("#5aa8e0"), POOR = U.Hex("#e8806e"), MAX = U.Hex("#7cc46a");
        static readonly Color TXT_BUY = U.Hex("#2f78b0"), TXT_POOR = U.Hex("#b85a48"), TXT_MAX = U.Hex("#4a8a3a"), INK = U.Hex("#4a3a2a");

        public static float Radius(Node n) => n.core ? 26 : n.sub ? 16 : 24;

        // 지난 프레임에 그린 상태. 이것이 같고 자라는 애니메이션도 없으면 선·레벨 칸·속 그림·이름표를 다시 만들지 않는다
        // (노드 108개가 매 프레임 LineRenderer 점 수천 개와 이름표 문자열을 새로 만들고 있었다)
        bool? shownHidden;
        string shownS, shownSt; int shownL = -1; bool shownHover, shownLabel; string shownLang;
        static readonly Vector3[] linkBuf = new Vector3[21], arcBuf = new Vector3[11];   // SetPositions가 복사하므로 노드끼리 같이 써도 된다

        static void Show(Component c, bool on) => UIUtil.Show(c, on);

        // 밝은 바탕용 이름표 재질: 짙은 글자 + 크림색 얇은 테두리 (폰트마다 하나를 만들어 모든 노드가 같이 쓴다)
        static readonly System.Collections.Generic.Dictionary<TMP_FontAsset, Material> lightMats = new System.Collections.Generic.Dictionary<TMP_FontAsset, Material>();
        static Material LightMat(TMP_FontAsset f)
        {
            if (f == null) return null;
            if (!lightMats.TryGetValue(f, out var m))
            {
                m = new Material(f.material);
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(1, 0.98f, 0.92f, 1));
                m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
                lightMats[f] = m;
            }
            return m;
        }

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
            bool hidden = s == "hidden" || Tutorial.HidesNode(n);
            // 숨김이 바뀔 때만 자식을 모두 켜고 끈다 (예전엔 매 프레임 켰다가 아래에서 다시 꺼서 TMP가 등록·해제를 반복했다)
            if (shownHidden != hidden) { shownHidden = hidden; foreach (Transform c in transform) c.gameObject.SetActive(!hidden); shownS = null; }
            if (hidden) return;
            int L = Lv(n.id);
            bool more = !AtMax(n, L);
            var cost = more ? NodeCost(n, L) : default;
            bool afford = more && CanAfford(cost);
            string st = s == "locked" ? "locked" : s == "max" ? "max" : afford ? "buy" : "poor";
            Color col = st == "buy" ? BUY : st == "poor" ? POOR : MAX;
            var brc = U.Hex(CATS[n.br].color);
            float r = Radius(n);
            var me = TreePos;
            float sc = hover ? 1.15f : 1;
            bool locked = st == "locked";
            bool labelOn = !locked && (!n.sub || hover || camZ > 0.8f || n.core);
            bool dirty = growAnim >= 0 || s != shownS || st != shownSt || L != shownL || hover != shownHover || labelOn != shownLabel || shownLang != Loc.Lang;
            shownS = s; shownSt = st; shownL = L; shownHover = hover; shownLabel = labelOn; shownLang = Loc.Lang;

            // 연결 균사 (부모 → 나)
            bool owned = L > 0;
            bool hasLink = !n.core && n.parent != null;   // 시작 노드는 부모 줄이 없다
            Show(link, hasLink);
            if (hasLink && dirty)
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
                    linkBuf[i] = Art.P(x - me.x, y - me.y);
                }
                link.SetPositions(linkBuf);
                int depth = Mathf.Max(1, Mathf.RoundToInt(me.magnitude / 150));
                link.widthMultiplier = Mathf.Max(2, 9 - depth * 1.2f) / Art.PPU;
                var lc = owned ? new Color(1, 0.99f, 0.94f, 0.95f) : s == "avail" ? new Color(1, 0.98f, 0.9f, 0.6f) : new Color(1, 0.98f, 0.9f, 0.28f);
                link.startColor = link.endColor = lc;
                link.textureMode = owned ? LineTextureMode.Stretch : LineTextureMode.Tile;
            }

            glow.transform.localScale = pulse.transform.localScale = body.transform.localScale = border.transform.localScale = Vector3.one * sc;
            Show(qmark, locked);
            Show(badge, !locked);
            Show(inner, !locked);
            Show(label, labelOn);
            Show(line2, labelOn);

            body.transform.localScale = Vector3.one * (r * 2 / Art.PPU) * sc;
            border.transform.localScale = Vector3.one * ((r * 2 + 4) / Art.PPU) * sc;
            if (locked)
            {
                body.color = new Color(0.86f, 0.84f, 0.79f, 0.85f);
                border.color = new Color(0.7f, 0.66f, 0.58f, 0.7f);
                glow.enabled = pulse.enabled = false;
                qmark.fontSize = r * 0.9f / 10f;
                foreach (var a in levelArcs) if (a != null) Show(a, false);
                return;
            }
            // 바깥 빛: 강화 가능 = 파랗게 숨쉬는 빛 · 최대 = 은은한 금빛 · 균사석 = 청록
            glow.enabled = st == "buy" || st == "max" || n.gem;
            if (glow.enabled)
            {
                float k = st == "buy" ? 0.55f + 0.35f * Mathf.Sin(now * 4 + me.x * 0.01f) : 0.45f;
                Color gc = n.gem ? new Color(125 / 255f, 1, 200 / 255f, 0.25f + 0.1f * Mathf.Sin(now * 3 + me.x)) : st == "buy" ? new Color(120 / 255f, 190 / 255f, 1, k * 0.4f) : new Color(170 / 255f, 230 / 255f, 140 / 255f, k * 0.35f);
                glow.color = gc;
                glow.transform.localScale = Vector3.one * ((r + (n.gem ? 22 : 16)) * 2 / Art.PPU) * sc;
            }
            pulse.enabled = st == "buy";
            if (pulse.enabled) { pulse.color = new Color(col.r, col.g, col.b, 0.35f + 0.35f * Mathf.Sin(now * 4 + me.x * 0.01f)); pulse.transform.localScale = Vector3.one * ((r + 11) * 2 / Art.PPU) * sc; }
            body.color = n.gem ? U.Hex("#e2f6ee") : st == "max" ? U.Hex("#f4f9e8") : U.Hex("#fdf6e6");
            border.color = col;
            if (!dirty) return;   // 아래(레벨 칸·속 그림·배지·이름표)는 상태가 바뀔 때만

            // 레벨 칸
            for (int i = 0; i < levelArcs.Length; i++)
            {
                var a = levelArcs[i];
                if (a == null) continue;
                bool show = n.max > 1 && i < n.max;
                Show(a, show);
                if (!show) continue;
                float gap = 0.12f, seg = U.TAU / n.max, a0 = -Mathf.PI / 2 + i * seg + gap / 2, a1 = a0 + seg - gap;
                const int K = 10;
                a.positionCount = K + 1;
                for (int j = 0; j <= K; j++) { float ang = Mathf.Lerp(a0, a1, j / (float)K); arcBuf[j] = Art.P(Mathf.Cos(ang) * (r + 6) * sc, Mathf.Sin(ang) * (r + 6) * sc); }
                a.SetPositions(arcBuf);
                a.startColor = a.endColor = i < L ? MAX : new Color(0.55f, 0.48f, 0.38f, 0.25f);
            }

            // 속 그림
            float alpha = st == "poor" ? 0.55f : 1;
            if (n.core) { inner.sprite = SpriteDB.Get("Icons/core_", n.br); Art.FitPx(inner, r * 1.3f * sc); inner.color = new Color(1, 1, 1, alpha); }
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
            badgeText.color = Color.white;

            // 이름표 두 줄
            if (labelOn)
            {
                var lm = LightMat(label.font);
                if (lm != null && label.fontSharedMaterial != lm) { label.fontSharedMaterial = lm; line2.fontSharedMaterial = LightMat(line2.font); }
                if (n.core)
                {
                    label.text = ""; line2.text = st == "max" ? Loc.T("열림") : $"{U.Fmt(cost.gold)}G";
                    line2.color = st == "buy" ? TXT_BUY : st == "poor" ? TXT_POOR : TXT_MAX;
                    line2.transform.localPosition = Art.P(0, r + 14);
                }
                else
                {
                    label.text = n.sub ? n.subLabel : n.n;
                    label.fontSize = (n.sub ? 15 : 18) / 10f;
                    label.color = n.gem ? U.Hex("#2f8a6a") : st == "poor" ? U.Hex("#8a7a66") : INK;
                    label.transform.localPosition = Art.P(0, r + 14);
                    string lvTxt = n.inf ? $"Lv {L}" : n.max > 1 ? $"Lv {L}/{n.max}" : "";   // 싱크: 레벨 칸 없이 'Lv N' (사용자 결정 2026-10-13)
                    string costTxt = cost.gold > 0 && cost.gem > 0 ? Loc.F("{0}G + 균사석 {1}", U.Fmt(cost.gold), U.Fmt(cost.gem)) : cost.gold > 0 ? $"{U.Fmt(cost.gold)}G" : cost.gem > 0 ? Loc.F("균사석 {0}", U.Fmt(cost.gem)) : "";
                    line2.text = st == "max" ? (n.inf ? $"MAX {L}" : n.max > 1 ? $"MAX {L}/{n.max}" : "MAX") : $"{lvTxt}{(lvTxt != "" ? " · " : "")}{costTxt}";
                    line2.color = st == "buy" ? TXT_BUY : st == "poor" ? TXT_POOR : TXT_MAX;
                    line2.transform.localPosition = Art.P(0, r + 32);
                }
            }
        }
    }
}
