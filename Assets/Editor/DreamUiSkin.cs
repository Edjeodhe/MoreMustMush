using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.EditorTools
{
    // 배치한 UI(MainMenu·MainGame·OutGame·Stage)에 몽환 수채화 그림을 입힌다 (2026-10-16).
    // 위치·크기·이름·계층은 건드리지 않고 Image의 그림·색, 그 위 글자색만 바꾼다. 다시 돌려도 같다.
    // 예외 하나: MainMenu 타이틀 글자(MainLog)를 로고 그림(UI/logo)으로 바꾼다 — 같은 자리에 Logo 이미지를 두고 글자는 끈다.
    public static class DreamUiSkin
    {
        static readonly Color INK = Hex("#4a3426"), CREAM = Hex("#fff8e7"), LINE = Hex("#8b6b4e80");
        // 초록(btn_go)·주황(btn_orange) 위 크림 글자는 대비 1.8:1이라 흐리다 → 같은 계열 진한 글자 (6:1 이상, TempDialog.SkinButtonText와 같은 색)
        static readonly Color GREEN_INK = Hex("#1f4017"), ORANGE_INK = Hex("#61290a");

        // 경로 = 캔버스부터 이름 경로, 끝이 '*'이면 그 앞으로 시작하는 모든 것. 위에서부터 처음 맞는 규칙 하나만 쓴다
        class Rule
        {
            public string path, sprite; public Color? color, text; public bool aspect, tab, hide; public float fs; public string on = "UI/rail_active";
            public Rule(string path, string sprite) { this.path = path; this.sprite = sprite; }
        }
        static Rule R(string path, string sprite, Color? text = null, Color? color = null, bool aspect = false, bool tab = false, bool hide = false, float fs = 0, string on = "UI/rail_active")
            => new Rule(path, sprite) { text = text, color = color, aspect = aspect, tab = tab, hide = hide, fs = fs, on = on };

        static readonly Dictionary<string, Rule[]> RULES = new Dictionary<string, Rule[]>
        {
            ["MainMenu"] = new[]
            {
                R("Canvas/Menu_BG", "Backgrounds/bg_title"),
                R("Canvas/Menu_BG/Panel", "UI/btn_round"),
                R("Canvas/Menu_BG/Panel/Button_Lang", "Icons/lang", aspect: true),
                R("Canvas/Button_Start", "UI/btn_go", GREEN_INK, fs: 44),
                R("Canvas/Button_Reset", "UI/btn_danger", CREAM, fs: 36),
                R("Canvas/Button_Option", "UI/btn_wood", INK, fs: 36),
                R("Canvas/Button_Exit", "UI/btn_wood", INK, fs: 36),
            },
            ["MainGame"] = new[]
            {
                R("Canvas/MainBG/Gold_UI_BG", "UI/bar_top"),
                R("Canvas/MainBG/Gold_UI_BG/Gold_Img", "Icons/gold", INK, aspect: true),
                R("Canvas/MainBG/Gold_UI_BG/Ston_Img", "Icons/gem", INK, aspect: true),
                R("Canvas/MainBG/Button_Option", "UI/btn_round"),
                R("Canvas/MainBG/Button_Option/Img", "Icons/option", aspect: true),
                R("Canvas/MainBG/Panel", "UI/btn_round"),
                R("Canvas/MainBG/Panel/Button_Lang", "Icons/lang", aspect: true),
                R("Canvas/MainBG/Stage_Image", "UI/panel_wood", INK),
                R("Canvas/MainBG/Stage_Image/Button_StageStart", "UI/btn_go", GREEN_INK),
                R("Canvas/MainBG/Button_Harvesters", "UI/btn_wood", INK),
                R("Canvas/MainBG/Button_MushDex", "UI/btn_wood", INK),
                R("Canvas/MainBG/Button_MushTree", "UI/btn_wood", INK),
                R("Canvas/Harvesters/Exit", "UI/btn_close", aspect: true),
                R("Canvas/Harvesters/Harvesters (*/Panel (1)", "UI/panel_mint", INK),
                R("Canvas/Harvesters/Harvesters (*/Panel", "UI/Dex/tag_wood", INK),
                R("Canvas/Harvesters/Harvesters (*/Button_Buy", "UI/btn_go", GREEN_INK),
                R("Canvas/Harvesters/Harvesters (*/Button_Upg", "UI/btn_orange", ORANGE_INK),
                R("Canvas/Harvesters/Harvesters (*/Image", null),   // 수확기 그림은 코드가 넣는다
                R("Canvas/Harvesters/Harvesters (*", "UI/panel_paper", INK),
                R("Canvas/Harvesters", "UI/panel_wood"),
                R("Canvas/MushDex/Exit", "UI/btn_close", aspect: true),
                R("Canvas/MushDex/Tab/*", "UI/Dex/dex_tab", INK, tab: true, on: "UI/Dex/dex_tab_on"),
                R("Canvas/MushDex/Mush_Dex_Group/*/Star*", "Icons/star", aspect: true),
                R("Canvas/MushDex/Mush_Dex_Group/*/Image", null, INK),       // 버섯 그림은 코드가 넣는다
                R("Canvas/MushDex/Mush_Dex_Group/*", "UI/Dex/dex_card", INK),
                R("Canvas/MushDex/Image/Start_Group/*", "Icons/star", aspect: true),
                R("Canvas/MushDex/Image/ScrollBar/Sliding Area/Handle", "UI/progress_fill", color: Hex("#c9a77c")),
                R("Canvas/MushDex/Image/ScrollBar", "UI/progress_back"),
                R("Canvas/MushDex/Image/Line*", null, color: LINE),
                R("Canvas/MushDex/Image/Image", null),
                R("Canvas/MushDex/Image/Frame", "UI/Dex/dex_frame"),
                R("Canvas/MushDex/Image", "UI/Dex/dex_page", INK),
                R("Canvas/MushDex", "UI/Dex/dex_window"),
                R("Canvas/TreeTooltip", "UI/panel_paper"),
                R("Canvas/Quset", "UI/btn_orange", ORANGE_INK),
            },
            ["OutGame"] = new[]
            {
                R("Canvas/UP_Panel/Gold", "Icons/gold", INK, aspect: true),
                R("Canvas/UP_Panel/수확석", "Icons/gem", INK, aspect: true),
                R("Canvas/UP_Panel", "UI/bar_top", INK),
                R("Canvas/Panel/Button_Bag", "UI/basket", INK, aspect: true),
                R("Canvas/Panel", "UI/panel_paper", INK),
                R("Canvas/Down_Panel/Image", null),            // 꼬마 칸·접기 손잡이: 아래에서 따로
                R("Canvas/Down_Panel", "UI/panel_wood"),
                R("Canvas/Down_hidden_Panel/*", null),
                R("Canvas/Down_hidden_Panel", "UI/panel_wood"),
                R("Canvas/Button_Group/*", "UI/rail_item", INK, tab: true),
            },
            ["Stage"] = new[]
            {
                R("Canvas/Stage_BG/Stage_Gold/Gold_Sprite", "Icons/gold", aspect: true),
                R("Canvas/Stage_BG/Stage_Gold/Earned_Gold", null, INK, color: new Color(1, 1, 1, 0)),
                R("Canvas/Stage_BG/Stage_Info/Weather", "Icons/w_clear", aspect: true),
                R("Canvas/Stage_BG/Stage_Info/*", null),
                R("Canvas/Stage_BG/Stage_Score", "UI/panel_paper", INK),
                R("Canvas/Stage_BG/Stage_Gold", "UI/chip", INK),
                R("Canvas/Stage_BG/Stage_Timer", "UI/chip", INK),
                R("Canvas/Stage_BG/Stage_Info", "UI/chip", INK),
                R("Canvas/Stage_BG", null),   // 꺼 둔 전체 판 (클릭을 가로채서 껐음)
            },
        };

        static SortedDictionary<string, string> art;
        static Sprite Sp(string key)
        {
            if (key == null) return null;
            var s = art.TryGetValue(key, out var p) ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
            if (s == null) Debug.LogWarning("[DreamUiSkin] 그림 없음: " + key);
            return s;
        }

        [MenuItem("MoreMush/UI/수채화 UI 입히기 (4개 씬)")]
        public static void ApplyAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var back = EditorSceneManager.GetActiveScene().path;
            foreach (var name in RULES.Keys)
            {
                var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity", OpenSceneMode.Single);
                Debug.Log($"[DreamUiSkin] {name}: {Apply(scene, name)}개");
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        }

        public static int Apply(UnityEngine.SceneManagement.Scene scene, string name)
        {
            art = MoreMushSetup.ResolvedArt();
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    var rule = Match(AnimationUtility.CalculateTransformPath(img.transform, null), RULES[name]);
                    if (rule == null) continue;
                    Skin(img, rule); n++;
                }
            if (name == "MainMenu") Logo(scene);
            if (name == "OutGame") OutGameExtras(scene);
            if (name == "MainGame") { DexFrame(scene); DexCards(scene); }
            FitButtons(scene);
            ScaleTags(scene);
            if (name == "MainGame") AlignMainGame(scene);
            if (name == "MainMenu") RoundIcon(Find(scene, "Canvas/Menu_BG/Panel"), Find(scene, "Canvas/Menu_BG/Panel/Button_Lang"), null, 110);
            EditorSceneManager.MarkSceneDirty(scene);
            return n;
        }

        static Rule Match(string path, Rule[] rules)
        {
            foreach (var r in rules)
                if (Glob(path, r.path)) return r;
            return null;
        }

        // '*'는 '/'를 넘지 않는 아무 글자
        static bool Glob(string s, string pat)
        {
            int i = 0, j = 0, si = -1, sj = -1;
            while (i < s.Length)
            {
                if (j < pat.Length && pat[j] == '*') { sj = j++; si = i; continue; }
                if (j < pat.Length && pat[j] == s[i]) { i++; j++; continue; }
                if (sj >= 0 && s[si] != '/') { i = ++si; j = sj + 1; continue; }
                return false;
            }
            while (j < pat.Length && pat[j] == '*') j++;
            return j == pat.Length;
        }

        static void Skin(Image img, Rule r)
        {
            Undo.RecordObject(img, "Dream UI");
            if (r.hide) { img.enabled = false; return; }
            var sp = Sp(r.sprite);
            if (sp != null)
            {
                img.sprite = sp;
                bool sliced = sp.border != Vector4.zero && !r.aspect;
                img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                img.preserveAspect = r.aspect;
                if (sliced) img.pixelsPerUnitMultiplier = 1;
                img.color = Color.white;
            }
            if (r.color != null) img.color = r.color.Value;
            if (r.tab && img.TryGetComponent<Button>(out var b))
            {
                // 지금 탭은 interactable=false로 표시한다 (OutGame·도감 코드) → 눌린 모양 = rail_active
                Undo.RecordObject(b, "Dream UI");
                b.transition = Selectable.Transition.SpriteSwap;
                var st = b.spriteState;
                st.disabledSprite = st.pressedSprite = Sp(r.on);
                st.highlightedSprite = st.selectedSprite = null;
                b.spriteState = st;
            }
            if (r.text != null)
                foreach (Transform c in img.transform)
                    if (c.TryGetComponent<TMP_Text>(out var t)) { Undo.RecordObject(t, "Dream UI"); t.color = r.text.Value; if (r.fs > 0) t.fontSize = r.fs; }
            EditorUtility.SetDirty(img);
        }

        // 타이틀 글자 자리에 로고 그림
        static void Logo(UnityEngine.SceneManagement.Scene scene)
        {
            var bg = Find(scene, "Canvas/Menu_BG");
            var title = bg?.Find("MainLog");
            if (title == null) return;
            var logo = bg.Find("Logo");
            if (logo == null)
            {
                var go = new GameObject("Logo", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Dream UI");
                logo = go.transform;
                logo.SetParent(bg, false);
                logo.SetSiblingIndex(title.GetSiblingIndex());
            }
            var src = (RectTransform)title; var dst = (RectTransform)logo;
            dst.anchorMin = src.anchorMin; dst.anchorMax = src.anchorMax; dst.pivot = src.pivot;
            dst.anchoredPosition = src.anchoredPosition + new Vector2(0, 110);   // 시작 버튼과 겹치지 않게
            dst.sizeDelta = new Vector2(640, 317);   // logo.png 1512×748 비율
            var img = logo.GetComponent<Image>();
            img.sprite = Sp("UI/logo"); img.preserveAspect = true; img.raycastTarget = false; img.color = Color.white;
            title.gameObject.SetActive(false);
        }

        static void OutGameExtras(UnityEngine.SceneManagement.Scene scene)
        {
            // 위 막대 안 글자(보관 시간 등)도 진한 글자로
            var up = Find(scene, "Canvas/UP_Panel");
            if (up != null) foreach (var t in up.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "Dream UI"); t.color = INK; }
            var bag = Find(scene, "Canvas/Panel");
            if (bag != null) foreach (var t in bag.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "Dream UI"); t.color = INK; }
            // 접기 손잡이(검은 세로 막대)
            var down = Find(scene, "Canvas/Down_Panel");
            if (down != null)
                foreach (Transform c in down)
                    if (c.TryGetComponent<Image>(out var img) && img.sprite == null)
                    { Undo.RecordObject(img, "Dream UI"); img.sprite = Sp("UI/chip"); img.type = Image.Type.Sliced; img.color = Hex("#a07850"); }
        }

        // ===== 버튼 크기를 글자에 맞춘다 (9-slice라 늘려도 테두리 그대로) =====
        // 높이 = 글자 크기 × FIT_H, 폭 = 모든 언어 번역 중 가장 긴 문구 + 양옆 여백. 같은 부모 아래 글자 버튼끼리는 같은 크기.
        // 가운데(피벗) 자리는 그대로 두고 크기만 바꾼다. 글자 없는 버튼(닫기·지구본·톱니)과 그림 버튼(보따리)은 건드리지 않는다.
        const float FIT_H = 2.8f, FIT_PAD = 1.9f, FIT_MIN_W = 2.6f, FIT_GAP = 16, FIT_IN_X = 0.75f, FIT_IN_Y = 0.2f, FIT_H_LABEL = 1.9f, LABEL_W = 0.8f;

        static void FitButtons(UnityEngine.SceneManagement.Scene scene)
        {
            var groups = new Dictionary<(Transform, bool), List<(RectTransform rt, Vector2 size)>>();   // (부모, 라벨인가)
            foreach (var root in scene.GetRootGameObjects())
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    var b = img;
                    bool label = IsLabel(img.transform);
                    if (!label && !img.TryGetComponent<Button>(out _)) continue;
                    if (img.sprite == null || img.type != Image.Type.Sliced) continue;
                    TMP_Text t = null;
                    foreach (Transform c in b.transform) if (c.TryGetComponent(out t)) break;
                    if (t == null || string.IsNullOrWhiteSpace(t.text)) continue;
                    // 글자는 테두리 안쪽에서 한 줄로 (넘치면 TextFit이 칸 안에서 줄인다)
                    Undo.RecordObject(t, "Dream UI");
                    float inX = t.fontSize * FIT_IN_X * (label ? 1.8f : 1);   // 이름판은 양 끝 못 자리까지 비운다
                    t.margin = new Vector4(Mathf.Round(inX), Mathf.Round(t.fontSize * FIT_IN_Y), Mathf.Round(inX), Mathf.Round(t.fontSize * FIT_IN_Y));
                    if (label) t.fontStyle = FontStyles.Bold;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    t.alignment = TextAlignmentOptions.Center;
                    float w = 0;
                    foreach (var s in AllLangs(t)) w = Mathf.Max(w, t.GetPreferredValues(s, 9999, 9999).x);
                    float fs = t.fontSize, h = Mathf.Round(fs * (label ? FIT_H_LABEL : FIT_H));
                    var size = new Vector2(Mathf.Round(Mathf.Max(w + fs * FIT_PAD * 2, h * FIT_MIN_W)), h);
                    // 이름 라벨은 코드가 카드마다 다른 이름을 넣으므로 글자 대신 카드 폭에 맞춘 일정한 띠 (넘치면 TextFit이 줄인다)
                    // (카드·라벨에 가로 배율이 걸려 있어도 화면 폭 기준 80%가 되게 배율 비로 나눈다)
                    if (label) size.x = Mathf.Round(((RectTransform)b.transform.parent).rect.width * LABEL_W
                        * b.transform.parent.lossyScale.x / Mathf.Max(1e-6f, b.transform.lossyScale.x));
                    if (!groups.TryGetValue((b.transform.parent, label), out var g)) groups[(b.transform.parent, label)] = g = new List<(RectTransform, Vector2)>();
                    g.Add(((RectTransform)b.transform, size));
                }
            // 같은 줄(높이가 겹치는) 옆 버튼과 겹치지 않게: 가운데 사이 거리의 절반 − 틈까지만 (넘치는 언어는 TextFit이 글자를 줄인다)
            var all = new List<RectTransform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                    if (img.TryGetComponent<Button>(out _) || IsLabel(img.transform)) all.Add((RectTransform)img.transform);
            // 축 0 = 폭(같은 줄 옆 버튼), 1 = 높이(같은 열 위아래 버튼)
            float HalfCap(RectTransform rt, Vector2 size, int axis)
            {
                var c = rt.GetComponentInParent<Canvas>()?.rootCanvas.transform;
                if (c == null) return 1e9f;
                // 이 버튼 부모의 좌표계로 잰다 (카드처럼 배율이 걸린 곳도 크기와 같은 단위)
                var par = rt.parent;
                Vector2 p = par.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
                float cap = 1e9f;
                foreach (var o in all)
                {
                    if (o == rt || !o.gameObject.activeInHierarchy && rt.gameObject.activeInHierarchy || o.IsChildOf(rt) || rt.IsChildOf(o)) continue;
                    if (o.GetComponentInParent<Canvas>()?.rootCanvas.transform != c) continue;
                    // 이웃 = 같은 부모 안의 버튼. 캔버스에 바로 놓인 버튼(의뢰 등)은 켜져 있는 다른 버튼과도 비교.
                    // (꺼 둔 팝업끼리, 실행 중 코드가 다시 놓는 공방 카드끼리는 씬 위치가 실제와 달라 비교하지 않는다)
                    bool canvasLevel = TopUnder(o, c) == o || TopUnder(rt, c) == rt;
                    if (o.parent != rt.parent && !(canvasLevel && o.gameObject.activeInHierarchy && rt.gameObject.activeInHierarchy)) continue;
                    Vector2 q = par.InverseTransformPoint(o.TransformPoint(o.rect.center));
                    Vector2 os = o.rect.size * (o.lossyScale.x / Mathf.Max(1e-6f, par.lossyScale.x));
                    int other = 1 - axis;
                    if (Mathf.Abs(q[other] - p[other]) >= (size[other] + os[other]) / 2) continue;   // 다른 줄·열
                    if (Mathf.Abs(q[axis] - p[axis]) < Mathf.Abs(q[other] - p[other])) continue;           // 그 축의 이웃이 아님
                    cap = Mathf.Min(cap, Mathf.Abs(q[axis] - p[axis]) / 2 - FIT_GAP / 2);
                }
                return cap;
            }
            foreach (var g in groups.Values)
            {
                var max = Vector2.zero;
                foreach (var e in g) max = Vector2.Max(max, e.size);
                foreach (var e in g) max.y = Mathf.Min(max.y, HalfCap(e.rt, max, 1) * 2);
                foreach (var e in g) max.x = Mathf.Min(max.x, HalfCap(e.rt, max, 0) * 2);
                foreach (var e in g)
                {
                    Undo.RecordObject(e.rt, "Dream UI");
                    // 앵커가 한 점이면 sizeDelta가 곧 크기
                    if (e.rt.anchorMin == e.rt.anchorMax) e.rt.sizeDelta = max;
                    else { e.rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, max.x); e.rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, max.y); }
                }
            }
        }

        // 버튼은 아니지만 글자에 맞춰 늘릴 이름 라벨 (공방 카드 이름)
        static readonly string[] LABELS = { "Canvas/Harvesters/Harvesters (*/Panel" };
        static bool IsLabel(Transform t)
        {
            string p = AnimationUtility.CalculateTransformPath(t, null);
            foreach (var l in LABELS) if (Glob(p, l)) return true;
            return false;
        }

        // 이 글자의 모든 언어 문구 (LocText.key = 한국어 원문, 없으면 지금 글자만)
        static System.Reflection.MethodInfo loadTable;
        static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
        static IEnumerable<string> AllLangs(TMP_Text t)
        {
            yield return t.text;
            if (t.transform.parent.name == "Quset") { yield return "Quests (3)"; yield return "의뢰 (3)"; }   // 글자를 코드가 넣는다 (MainGameController)
            var lt = t.GetComponent<LocText>();
            if (lt == null || string.IsNullOrEmpty(lt.key)) yield break;
            yield return lt.key;
            loadTable ??= typeof(Loc).GetMethod("LoadTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            foreach (var lang in Loc.LANGS)
            {
                if (!tables.TryGetValue(lang, out var tb)) tables[lang] = tb = loadTable?.Invoke(null, new object[] { lang }) as Dictionary<string, string>;
                if (tb != null && tb.TryGetValue(lt.key, out var s) && !string.IsNullOrEmpty(s)) yield return s;
            }
        }

        const float FRAME_SIDE = 212, FRAME_INNER = 0.6f;

        // 도감 상세 창의 버섯 그림 뒤에 액자 (그림보다 조금 크게, 그림 바로 뒤 순서)
        static void DexFrame(UnityEngine.SceneManagement.Scene scene)
        {
            var page = Find(scene, "Canvas/MushDex/Image");
            var pic = page?.Find("Image") as RectTransform;
            if (pic == null) return;
            var fr = page.Find("Frame") as RectTransform;
            if (fr == null)
            {
                var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Dream UI");
                fr = (RectTransform)go.transform;
                fr.SetParent(page, false);
            }
            fr.SetAsFirstSibling();   // 페이지 안 맨 뒤 (버섯 그림·글자 아래)
            fr.anchorMin = pic.anchorMin; fr.anchorMax = pic.anchorMax; fr.pivot = pic.pivot;
            fr.anchoredPosition = pic.anchoredPosition;
            fr.sizeDelta = Vector2.one * FRAME_SIDE;
            // 버섯 그림은 액자 안쪽 매트 안에만 (액자 나무 테두리를 넘지 않게)
            Undo.RecordObject(pic, "Dream UI");
            pic.anchorMin = fr.anchorMin; pic.anchorMax = fr.anchorMax; pic.pivot = fr.pivot;
            pic.anchoredPosition = fr.anchoredPosition;
            pic.sizeDelta = Vector2.one * Mathf.Round(FRAME_SIDE * FRAME_INNER);
            if (pic.TryGetComponent<Image>(out var pi)) { Undo.RecordObject(pi, "Dream UI"); pi.preserveAspect = true; pi.raycastTarget = false; }
            var img = fr.GetComponent<Image>();
            img.sprite = Sp("UI/Dex/dex_frame"); img.preserveAspect = true; img.raycastTarget = false; img.color = Color.white;
        }

        // ===== 위치 맞추기 =====
        // 상단: 설정·언어 = 같은 크기 둥근 버튼 + 가운데 아이콘, 세로 중심을 재화 막대에 맞춘다. 하단: 의뢰 버튼을 이동 버튼 줄에 맞춘다
        const float ROUND = 96, ROUND_ICON = 0.66f;

        static void AlignMainGame(UnityEngine.SceneManagement.Scene scene)
        {
            var bar = Find(scene, "Canvas/MainBG/Gold_UI_BG") as RectTransform;
            float top = bar != null ? Center(bar).y : float.NaN;
            RoundIcon(Find(scene, "Canvas/MainBG/Button_Option"), Find(scene, "Canvas/MainBG/Button_Option/Img"), top, ROUND);
            RoundIcon(Find(scene, "Canvas/MainBG/Panel"), Find(scene, "Canvas/MainBG/Panel/Button_Lang"), top, ROUND);
            var nav = Find(scene, "Canvas/MainBG/Button_MushTree") as RectTransform;
            var quest = Find(scene, "Canvas/Quset") as RectTransform;
            if (nav != null && quest != null)
            {
                Undo.RecordObject(quest, "Dream UI");
                quest.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nav.rect.height);
                MoveTo(quest, new Vector2(Center(quest).x, Center(nav).y));
            }
        }

        // 둥근 판(holder)을 size×size로, 아이콘을 그 가운데 size×ROUND_ICON로. y가 숫자면 판의 세로 중심을 그 자리로
        static void RoundIcon(Transform holder, Transform icon, float? y, float size)
        {
            if (holder == null || icon == null) return;
            var h = (RectTransform)holder; var i = (RectTransform)icon;
            Undo.RecordObject(h, "Dream UI"); Undo.RecordObject(i, "Dream UI");
            var c = Center(h);
            h.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            h.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
            MoveTo(h, new Vector2(c.x, y is float v && !float.IsNaN(v) ? v : c.y));
            i.anchorMin = i.anchorMax = i.pivot = new Vector2(0.5f, 0.5f);
            i.anchoredPosition = Vector2.zero;
            i.sizeDelta = Vector2.one * Mathf.Round(size * ROUND_ICON);
            if (holder.TryGetComponent<Image>(out var hi)) { hi.enabled = true; hi.preserveAspect = true; hi.type = Image.Type.Simple; }
            if (icon.TryGetComponent<Image>(out var ii)) { ii.preserveAspect = true; ii.type = Image.Type.Simple; }
        }

        static Vector2 Center(RectTransform rt)
        {
            var c = rt.GetComponentInParent<Canvas>().rootCanvas.transform;
            return c.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
        }

        // 캔버스 기준 가운데를 p로 (캔버스 안 배율이 1이라 차이만큼 anchoredPosition을 옮기면 된다)
        static void MoveTo(RectTransform rt, Vector2 p) => rt.anchoredPosition += p - Center(rt);

        // 도감 카드 한 장 안 배치 (카드 180×200 기준): 테이프 아래 버섯 그림 · 이름 라벨 · 별 한 줄, 모두 카드 안쪽 여백 안
        static readonly Vector2 CARD_PIC = new Vector2(0, 24), CARD_PIC_SIZE = new Vector2(120, 96);
        static readonly Vector2 CARD_TAG = new Vector2(0, -38), CARD_TAG_SIZE = new Vector2(170, 38);
        const float CARD_STAR_Y = -70, CARD_STAR = 20, CARD_STAR_GAP = 24, CARD_NAME_FS = 19;

        static void DexCards(UnityEngine.SceneManagement.Scene scene)
        {
            var group = Find(scene, "Canvas/MushDex/Mush_Dex_Group");
            if (group == null) return;
            foreach (Transform card in group)
            {
                var pic = card.Find("Image") as RectTransform;
                var nameT = FindDeep(card, "Name");
                if (pic == null || nameT == null) continue;
                Undo.RecordObject(pic, "Dream UI");
                Place(pic, CARD_PIC, CARD_PIC_SIZE);
                if (pic.TryGetComponent<Image>(out var pi)) { pi.preserveAspect = true; pi.raycastTarget = false; }

                var tag = card.Find("NameTag") as RectTransform;
                if (tag == null)
                {
                    var go = new GameObject("NameTag", typeof(RectTransform), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(go, "Dream UI");
                    tag = (RectTransform)go.transform;
                    tag.SetParent(card, false);
                }
                tag.SetSiblingIndex(pic.GetSiblingIndex() + 1);
                Place(tag, CARD_TAG, CARD_TAG_SIZE);
                var ti = tag.GetComponent<Image>();
                ti.sprite = Sp("UI/Dex/tag_wood"); ti.type = Image.Type.Sliced; ti.raycastTarget = false; ti.color = Color.white;

                var nr = (RectTransform)nameT;
                Undo.SetTransformParent(nr, tag, "Dream UI");
                nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one; nr.pivot = new Vector2(0.5f, 0.5f);
                nr.offsetMin = new Vector2(22, 2); nr.offsetMax = new Vector2(-22, -2);   // 양 끝 못 자리를 비운다
                if (nameT.TryGetComponent<TMP_Text>(out var t))
                {
                    Undo.RecordObject(t, "Dream UI");
                    t.fontSize = CARD_NAME_FS; t.color = INK; t.alignment = TextAlignmentOptions.Center;
                    t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false; t.fontStyle = FontStyles.Bold;
                }

                int k = 0; var stars = new List<RectTransform>();
                foreach (Transform c in card) if (c.name.StartsWith("Star")) stars.Add((RectTransform)c);
                foreach (var st in stars)
                {
                    Undo.RecordObject(st, "Dream UI");
                    Place(st, new Vector2((k++ - (stars.Count - 1) / 2f) * CARD_STAR_GAP, CARD_STAR_Y), Vector2.one * CARD_STAR);
                }
            }
        }

        // 캔버스 바로 아래 조상 (창·팝업 단위)
        static Transform TopUnder(Transform t, Transform canvas)
        {
            while (t.parent != null && t.parent != canvas) t = t.parent;
            return t;
        }

        // 이름표(tag_*)는 끝 장식이 커서, 칸 높이에 맞춰 9-slice 테두리 배율을 키운다 (그림 높이 = 칸 높이가 되도록)
        static void ScaleTags(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.sprite.name.StartsWith("tag_") || img.type != Image.Type.Sliced) continue;
                    float h = ((RectTransform)img.transform).rect.height;
                    if (h <= 0) continue;
                    Undo.RecordObject(img, "Dream UI");
                    img.pixelsPerUnitMultiplier = Mathf.Max(1, img.sprite.rect.height / h);
                }
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static Transform Find(UnityEngine.SceneManagement.Scene scene, string path)
        {
            int k = path.IndexOf('/');
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == path.Substring(0, k)) return r.transform.Find(path.Substring(k + 1));
            return null;
        }

        static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    }
}
