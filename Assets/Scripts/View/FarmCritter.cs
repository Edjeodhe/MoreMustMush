using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One farm critter (prototype drawCritter + drawSay + request bubble): the critter rig on a feet pivot,
    // shadow, evolution glow and orbiting stars, held tool, hover label, speech bubble and request bubble.
    // The object sits at the critter's feet; children are laid out in the template slot (FarmView.critterTemplate).
    public class FarmCritter : MonoBehaviour
    {
        public Transform body;                  // feet pivot: hop offset, rotation, squash, facing
        public CritterRig rig;
        public SortingGroup rigGroup;
        public SpriteRenderer shadow, glow, surprise, tool;
        public SpriteRenderer builder;          // child of body: hammering builder picture shown instead of the rig while building
        public SpriteRenderer[] stars = new SpriteRenderer[3];
        [Header("Hover label")]
        public GameObject label; public SpriteRenderer labelBack; public TMP_Text labelText;
        [Header("Speech bubble")]
        public GameObject say; public SpriteRenderer sayBack, sayTail; public TMP_Text sayText;
        [Header("Request bubble")]
        public GameObject req; public SpriteRenderer reqBack, reqTail, reqIcon, reqDot; public TMP_Text reqMark;

        const float P = Art.PPU;
        const int SAY_ORDER = 5100;             // 말풍선 배경 순서 (꼬리 −1, 글자 +1), 말풍선마다 3씩 위로
        string shownSay, shownLabel;

        // 말풍선 자리 (농장 px, y 아래로). FarmView가 겹침을 풀고 PlaceSay로 놓는다
        [System.NonSerialized] public Rect sayRect;
        float sayFeetX, sayFeetY;

        public void Draw(FarmView.Critter p, float now, string hat, bool hovered, FarmKind request)
        {
            int e = EvoOf(p.id);
            float R0 = 44 * (1 + 0.15f * e);
            float ox = 0, oy = 0, rot = 0, sx = 1, sy = 1;
            float at = p.animT, ad = p.anim != null ? FARM_ANIMS[p.anim] : 1, u = at / ad;
            if (p.anim == null)
            {
                if (p.state == "walk") { oy = -Mathf.Abs(Mathf.Sin(p.hop)) * 8; sy = 1 + Mathf.Sin(p.hop * 2) * 0.03f; }
                else if (p.state == "work") { oy = -Mathf.Abs(Mathf.Sin(now * 12 + p.x)) * 9; sy = 1 + Mathf.Sin(now * 24 + p.x) * 0.05f; sx = 2 - sy; }
                else { sy = 1 + Mathf.Sin(now * 3 + p.x) * 0.025f; sx = 2 - sy; }
            }
            switch (p.anim)
            {
                case "jump": oy = -Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2)) * (70 - u * 30); sy = u > 0.9f ? 0.85f : 1; break;
                case "spin": rot = u * U.TAU; oy = -Mathf.Sin(u * Mathf.PI) * 20; break;
                case "dance": rot = Mathf.Sin(at * 14) * 0.3f; ox = Mathf.Sin(at * 7) * 12; oy = -Mathf.Abs(Mathf.Sin(at * 14)) * 10; break;
                case "heart": sy = 1 + Mathf.Sin(at * 16) * 0.08f; sx = 2 - sy; break;
                case "sleep": sy = 0.86f; sx = 1.1f; rot = 0.25f * p.face; break;
                case "surprise": oy = -Mathf.Min(1, u * 6) * 26 * (1 - u); ox = Mathf.Sin(at * 50) * 4 * (1 - u); break;
            }

            // 건설 중: 리그 대신 망치질 그림 (내려치는 프레임에서 살짝 찌그러진다)
            var bsp = p.bframe >= 0 ? SpriteDB.Get(SpriteDB.Key("Characters/Critters/Build/build_", p.id), p.bframe == 0 ? "_0" : "_1") : null;
            bool hammering = bsp != null;
            if (hammering && p.bframe == 1) { sy = 0.94f; sx = 1.06f; }

            transform.localPosition = Art.P(p.x, p.y);
            int order = 1000 + Mathf.RoundToInt(p.y);
            body.localPosition = new Vector3(ox / P, -oy / P, 0);
            body.localRotation = Quaternion.Euler(0, 0, -rot * Mathf.Rad2Deg);
            float k = R0 / rig.baseRadius;
            body.localScale = new Vector3(sx * p.face * k, sy * k, 1);
            rig.SetLook(p.id, CharSkinOn(p.id)?.id, hat);
            rigGroup.sortingOrder = order;
            if (rig.gameObject.activeSelf == hammering) rig.gameObject.SetActive(!hammering);
            builder.enabled = hammering;
            if (hammering)
            {
                if (builder.sprite != bsp) builder.sprite = bsp;
                float s = BUILDER.w * rig.baseRadius / P / bsp.bounds.size.x;   // body space: rig.baseRadius units
                builder.transform.localScale = new Vector3(s, s, 1);
                builder.transform.localPosition = new Vector3(0, bsp.bounds.size.y * s / 2, 0);
                builder.sortingOrder = order;
            }

            shadow.transform.localPosition = new Vector3(ox / P, -2 / P, 0);
            shadow.transform.localScale = new Vector3(R0 * 1.5f * (1 + oy / 200) / P, R0 * 0.44f / P, 1);

            // 진화: 발밑 빛 · 2단 진화: 둘레를 도는 별
            glow.enabled = e > 0;
            if (e > 0)
            {
                glow.color = e > 1 ? new Color(1, 0.86f, 0.35f, 0.55f) : new Color(0.63f, 1, 0.82f, 0.45f);
                glow.transform.localScale = new Vector3(R0 * 2.6f / P, R0 * 0.95f / P, 1);
            }
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].enabled = e > 1;
                if (e <= 1) continue;
                float a = now * 2 + i * U.TAU / 3;
                stars[i].transform.localPosition = new Vector3(Mathf.Cos(a) * R0 * 1.05f / P, -(-R0 * 0.9f + Mathf.Sin(a) * R0 * 0.35f) / P, 0);
                stars[i].sortingOrder = Mathf.Sin(a) > 0 ? order + 1 : order - 1;
            }
            surprise.enabled = p.anim == "surprise";
            if (surprise.enabled) surprise.transform.localPosition = new Vector3(34 / P, -(-90 + oy) / P, 0);

            // 일하는 중: 손에 든 도구
            bool work = p.state == "work" && p.tool != null;
            tool.enabled = work;
            if (work)
            {
                float sw = Mathf.Sin(now * 14 + p.x) * 0.6f;
                tool.sprite = SpriteDB.Get(p.tool == "till" ? "Farm/Field/tool_0" : p.tool == "hammer" ? "Icons/hammer" : p.tool == "seed" ? "Icons/spore" : "UI/basket");
                tool.transform.localPosition = new Vector3(p.face * R0 * 0.75f / P, -(-R0 * 0.55f + oy) / P, 0);
                tool.transform.localRotation = Quaternion.Euler(0, 0, -sw * p.face * Mathf.Rad2Deg);
                Art.FitPx(tool, R0 * (p.tool == "till" ? 1.3f : p.tool == "hammer" ? 0.95f : 0.75f));
                var ts = tool.transform.localScale; ts.x = Mathf.Abs(ts.x) * p.face; tool.transform.localScale = ts;
                tool.sortingOrder = order + 2;
            }

            // 마우스를 올리면 이름·호감도
            UIUtil.Show(label, hovered);
            if (hovered)
            {
                string s = $"{EvoName(p.kind)} {HeartStr(p.id)}";
                if (shownLabel != s) { shownLabel = s; labelText.text = s; labelText.ForceMeshUpdate(); }
                Art.SlicedPx(labelBack, labelText.preferredWidth * P + 20, 30);
            }

            // 말풍선
            bool talking = p.sayT > 0;
            UIUtil.Show(say, talking);
            if (talking)
            {
                float a = Mathf.Clamp01(Mathf.Min(1, Mathf.Min(p.sayT * 3, (2.8f - p.sayT) * 8 + 0.2f)));
                if (shownSay != p.say) { shownSay = p.say; sayText.text = p.say; sayText.ForceMeshUpdate(); }
                float w = Mathf.Min(420, sayText.preferredWidth * P + 34);
                float bx = Mathf.Clamp(p.x - w / 2, 10, W - w - 10), by = -R0 * 2 - 78;
                sayRect = new Rect(bx, p.y + by, w, 50); sayFeetX = p.x; sayFeetY = p.y;
                Art.SlicedPx(sayBack, w, 50);
                PlaceSay(0, 0);
                SetA(sayBack, a); SetA(sayTail, a); sayText.alpha = a;
            }

            // 부탁 말풍선 (목장)
            bool ask = request != null && !talking;
            UIUtil.Show(req, ask);
            if (ask)
            {
                float by = -R0 * 2 - 74 + Mathf.Sin(now * 5 + p.x) * 5;
                req.transform.localPosition = new Vector3(0, -(by + 31) / P, 0);
                var icon = SpriteDB.Get("Farm/Icons/", request.icon);
                if (reqIcon.sprite != icon) { reqIcon.sprite = icon; Art.FitPx(reqIcon, 44); }
            }
        }

        // 말풍선을 lift(px)만큼 위로 올리고, slot번째 그리기 순서를 준다 (겹쳐도 한 말풍선이 다른 것을 통째로 덮게).
        public void PlaceSay(float lift, int slot)
        {
            float cx = sayRect.center.x;
            say.transform.localPosition = new Vector3((cx - sayFeetX) / P, -(sayRect.y - lift - sayFeetY + 25) / P, 0);
            sayTail.transform.localPosition = new Vector3((sayFeetX - cx) / P, -33 / P, 0);
            int o = SAY_ORDER + slot * 3;
            sayTail.sortingOrder = o - 1; sayBack.sortingOrder = o; if (sayText is TextMeshPro tm) tm.sortingOrder = o + 1;
        }

        static void SetA(SpriteRenderer sr, float a) { var c = sr.color; c.a = a; sr.color = c; }
    }
}
