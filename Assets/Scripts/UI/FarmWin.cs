using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush
{
    // 세계수 창들(공방·꼬마·성장·꾸미기·꼬마 정보와 그 팝업)이 함께 쓰는 작은 도구. 창은 모두 Unity MCP로 만든 프리팹(Resources/UI/*)이고,
    // 코드는 프리팹을 캔버스에 한 번 꺼내 두고 템플릿 칸만 복제한다. 그림은 Resources/UI/Window (시안 2026-10-20과 같은 세트).
    public static class FarmWin
    {
        public static GameObject Spawn(Scene scene, string prefab)
        {
            var canvas = TempDialog.RootCanvas(scene);
            var p = Resources.Load<GameObject>("Prefabs/UI/" + prefab);
            if (canvas == null || p == null) { Debug.LogWarning("[FarmWin] 프리팹 Resources/Prefabs/UI/" + prefab + " 또는 캔버스가 없어요"); return null; }
            var go = Object.Instantiate(p, canvas.transform, false);
            go.name = p.name;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) TextLangFont.Register(t);
            go.SetActive(false);
            return go;
        }

        public static Transform Clone(Transform t, int i)
        {
            var c = Object.Instantiate(t.gameObject, t.parent, false).transform;
            c.name = t.name + " " + i;
            foreach (var x in c.GetComponentsInChildren<TMP_Text>(true)) TextLangFont.Register(x);
            return c;
        }

        // 템플릿 하나를 n칸으로 (모자라면 복제해서 늘린다). 첫 칸은 템플릿 자신
        public static void Grow(System.Collections.Generic.List<Transform> rows, Transform template, int n)
        {
            if (template == null) return;
            if (rows.Count == 0) rows.Add(template);
            while (rows.Count < n) rows.Add(Clone(template, rows.Count));
            for (int i = 0; i < rows.Count; i++) rows[i].gameObject.SetActive(i < n);
        }

        public static TMP_Text Text(Transform root, string name) => SceneUI.TextIn(SceneUI.Find(root, name));
        public static Image Img(Transform root, string name) => SceneUI.Find(root, name)?.GetComponent<Image>();
        public static Sprite Ui(string name) => SpriteDB.Get("UI/Window/" + name) ?? SpriteDB.Get("UI/" + name);

        // 하트 5개 = 호감도 Lv 0~10 (하트 하나에 2 Lv, 홀수면 마지막 하트를 반투명으로). 하트들 = Hearts 아래 Image들
        public static void Hearts(Transform hearts, int lv, int max = 10)
        {
            if (hearts == null) return;
            Sprite full = Ui("ic_heart_full"), empty = Ui("ic_heart_empty");
            int n = hearts.childCount, per = Mathf.Max(1, max / Mathf.Max(1, n));
            for (int i = 0; i < n; i++)
            {
                var im = hearts.GetChild(i).GetComponent<Image>();
                if (im == null) continue;
                int got = lv - i * per;
                im.sprite = got > 0 ? full : empty;
                im.color = got > 0 && got < per ? new Color(1, 1, 1, 0.55f) : Color.white;
            }
        }

        // 진행 막대: bar 아래 Fill(가로로 늘어나는 그림)과 Label(글자, 선택)
        public static void Bar(Transform bar, float v, string label)
        {
            if (bar == null) return;
            var fill = bar.Find("Fill") as RectTransform;
            if (fill != null) { v = Mathf.Clamp01(v); fill.anchorMax = new Vector2(v, 1); fill.gameObject.SetActive(v > 0.01f); }
            var t = SceneUI.TextIn(bar.Find("Label"));
            if (t != null) UIUtil.SetText(t, label ?? "");
        }

        // 버튼 글자·켜짐. sprite를 주면 버튼 그림도 바꾼다 (btn_leaf = 할 수 있음, btn_cream = 보조·못 함)
        public static void Btn(Button b, string label, bool on, string sprite = null)
        {
            if (b == null) return;
            b.interactable = on;
            if (sprite != null && b.targetGraphic is Image im) { var s = Ui(sprite); if (s != null) im.sprite = s; }
            var t = SceneUI.TextIn(b.transform);
            if (t != null)
            {
                UIUtil.SetText(t, label);
                bool green = b.targetGraphic is Image gi && gi.sprite != null && gi.sprite.name.Contains("leaf");
                t.color = green ? Color.white : new Color(0.36f, 0.24f, 0.15f);
                t.alpha = on ? 1 : 0.55f;
            }
        }

        public static string Gold(double v) => UIUtil.Ic("gold") + U.Fmt(v);
        public static string Gem(double v) => UIUtil.Ic("gem") + U.Fmt(v);
        public const string GREEN = "#2a8a64", GRAY = "#9a7a5a", RED = "#c0503a";
        public static string Col(string s, string col) => "<color=" + col + ">" + s + "</color>";
    }
}
