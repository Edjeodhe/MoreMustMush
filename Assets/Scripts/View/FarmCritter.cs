using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // One world-tree critter (prototype drawCritter + drawSay): the critter rig on a feet pivot,
    // shadow, awakening glow and orbiting stars, held tool, hover label (name · ★ · Lv) and speech bubble. (요청 말풍선 Req는 쓰지 않는다)
    // The object sits at the critter's feet; children are laid out in the template slot (FarmView.critterTemplate).
    public class FarmCritter : MonoBehaviour
    {
        public Transform body;                  // feet pivot: hop offset, rotation, squash, facing
        public CritterRig rig;
        public SortingGroup rigGroup;
        public SpriteRenderer shadow, glow, surprise, tool;
        public SpriteRenderer builder;          // child of body: (예전 건설 그림, 쓰지 않는다)
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

        public void Draw(FarmView.Critter p, float now, string hat, bool hovered)
        {
            int e = Awaken(p.id);   // 각성 ★: 1 발밑 빛 · 2~3 둘레를 도는 별
            float R0 = 40 * (1 + 0.1f * e);
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

            transform.localPosition = Art.P(p.x, p.y);
            int order = 1000 + Mathf.RoundToInt(p.y);
            body.localPosition = new Vector3(ox / P, -oy / P, 0);
            body.localRotation = Quaternion.Euler(0, 0, -rot * Mathf.Rad2Deg);
            float k = R0 / rig.baseRadius;
            body.localScale = new Vector3(sx * p.face * k, sy * k, 1);
            rig.SetLook(p.id, CharSkinOn(p.id)?.id, hat);
            rig.motion = p.anim ?? p.state;
            rigGroup.sortingOrder = order;
            if (!rig.gameObject.activeSelf) rig.gameObject.SetActive(true);
            builder.enabled = false;

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
                tool.sprite = SpriteDB.Get("UI/basket");
                tool.transform.localPosition = new Vector3(p.face * R0 * 0.75f / P, -(-R0 * 0.55f + oy) / P, 0);
                tool.transform.localRotation = Quaternion.Euler(0, 0, -sw * p.face * Mathf.Rad2Deg);
                Art.FitPx(tool, R0 * 0.75f);
                var ts = tool.transform.localScale; ts.x = Mathf.Abs(ts.x) * p.face; tool.transform.localScale = ts;
                tool.sortingOrder = order + 2;
            }

            // 마우스를 올리면 이름·호감도
            UIUtil.Show(label, hovered);
            if (hovered)
            {
                string s = Loc.F("{0} {1} Lv.{2}", p.kind.n, new string('★', Awaken(p.id)), KLv(p.id));
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

            UIUtil.Show(req, false);
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
