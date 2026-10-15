using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MoreMush.EditorTools
{
    // PC desktop layout for the two hub screens (resource_test, 2026-10-15, approved mockup Art/Raw_Doodle/ui_pc.png):
    // slim top bar (title left, currencies right), left navigation rail, right docked window, big primary button
    // bottom-right and a shortcut hint strip along the bottom. Also restyles every UI panel/button on the canvas:
    // pastel beige panels with a thin soft edge instead of the heavy 8px wood outline, white labels on the saturated
    // buttons. Edits the scene hierarchy in place; safe to run again.
    public static class PcLayout
    {
        const float W = 1920, H = 1080, TOP_H = 64, PAD = 16, RAIL_W = 232, ENTRY_H = 50, ENTRY_GAP = 8, HINT_H = 44;
        const float PANEL_W = 560;
        static readonly Color INK = Hex("#4a3a2a"), EDGE = Hex("#c9b494"), CLEAR = new Color(1, 1, 1, 0);

        static SortedDictionary<string, string> art;
        static Sprite Sp(string key) => art.TryGetValue(key, out var p) ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;

        [MenuItem("MoreMush/UI/Apply PC Layout")]
        public static void Apply()
        {
            art = MoreMushSetup.ResolvedArt();
            var scene = EditorSceneManager.GetActiveScene();
            var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
            var log = new List<string>();

            Title(canvas.Find("TitleScreen"), scene, log);
            TreeHub(canvas.Find("TreeScreen"), log);
            FarmHub(canvas.Find("FarmScreen"), log);
            RoundHud(canvas.Find("RoundUI/RoundHUD"), log);
            Restyle(canvas, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PcLayout] " + string.Join(" · ", log));
        }

        // ===== title (concept Art/Raw_Doodle/concept/title.png) =====
        // logo image upper center-right, a big start button with the small buttons under it, grandpa and three critters
        // in the lower left of the backdrop; no window box, the flat meadow shows through.
        const float TITLE_CX = 1180;
        static void Title(Transform ts, UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            if (ts == null) { log.Add("TitleScreen 없음"); return; }
            var box = (RectTransform)ts.Find("Box");
            TL(box, 0, 0, W, H); SetImage(box, null, CLEAR); Off<Outline>(box);
            box.Find("Icons").gameObject.SetActive(false);
            box.Find("Subtitle").gameObject.SetActive(false);
            var textLogo = (RectTransform)box.Find("Logo");
            var logo = Child(box, "LogoImage"); logo.SetSiblingIndex(textLogo.GetSiblingIndex());
            var li = Get<Image>(logo); li.sprite = Sp("UI/logo"); li.type = Image.Type.Simple; li.preserveAspect = true; li.raycastTarget = false; li.color = Color.white; li.enabled = li.sprite != null;
            TL(logo, TITLE_CX - 420, 40, 840, 470);
            textLogo.gameObject.SetActive(li.sprite == null);   // the text title stays only until the logo art exists
            TL(textLogo, TITLE_CX - 500, 150, 1000, 150);

            var buttons = (RectTransform)box.Find("Buttons");
            TL(buttons, 0, 0, W, H);
            foreach (var lg in buttons.GetComponents<HorizontalOrVerticalLayoutGroup>()) { Undo.RecordObject(lg, "pc"); lg.enabled = false; }
            Off<ContentSizeFitter>(buttons);
            foreach (var n in new[] { "NewGame", "Continue" })
            {
                var b = (RectTransform)buttons.Find(n);
                TL(b, TITLE_CX - 230, 540, 460, 104);
                SetImage(b, "UI/btn_go", Color.white); Off<Outline>(b); LabelColor(b, Color.white, 44);
            }
            string[] small = { "Options", "Records", "Reset", "Quit" };
            var row = small.Select(n => buttons.Find(n) as RectTransform).Where(b => b != null).ToList();
            float sw = 140, gap = 14, x0 = TITLE_CX - (row.Count * sw + (row.Count - 1) * gap) / 2;
            for (int i = 0; i < row.Count; i++)
            {
                TL(row[i], x0 + i * (sw + gap), 666, sw, 64);
                SetImage(row[i], "UI/btn_wood", Color.white); Off<Outline>(row[i]); LabelColor(row[i], INK, 26);
            }
            var howto = (RectTransform)box.Find("Howto");
            BL(howto, 460, 18, 1200, 40);
            foreach (var t in howto.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "pc"); t.color = new Color(INK.r, INK.g, INK.b, 0.65f); t.fontSize = 18; t.alignment = TextAlignmentOptions.Bottom; }

            // backdrop: grandpa lower left, three critters hopping beside him
            var world = scene.GetRootGameObjects().First(g => g.name == "World").transform;
            var bd = world.Find("TitleBackdrop");
            var gp = bd.Find("Grandpa");
            Undo.RecordObject(gp, "pc"); gp.localPosition = new Vector3(3.8f, -9.7f, 0); gp.localScale = new Vector3(1.15f, 1.15f, 1);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/CritterRig.prefab");
            var group = bd.Find("Critters");
            if (group == null) { group = new GameObject("Critters").transform; Undo.RegisterCreatedObjectUndo(group.gameObject, "pc"); group.SetParent(bd, false); }
            string[] kinds = { "fire", "dew", "star" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var c = group.Find("Critter_" + kinds[i]);
                if (c == null)
                {
                    c = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, group)).transform; c.name = "Critter_" + kinds[i];
                    Undo.RegisterCreatedObjectUndo(c.gameObject, "pc");
                }
                Undo.RecordObject(c, "pc");
                c.localPosition = new Vector3(6.4f + i * 1.6f, -8.85f + (i == 1 ? 0.15f : 0), 0); c.localScale = new Vector3(1.5f, 1.5f, 1);
                var rig = c.GetComponent<CritterRig>(); Undo.RecordObject(rig, "pc"); rig.kind = kinds[i]; rig.motion = i == 1 ? "dance" : "idle";
                var sg = c.GetComponent<UnityEngine.Rendering.SortingGroup>(); if (sg == null) sg = Undo.AddComponent<UnityEngine.Rendering.SortingGroup>(c.gameObject);
                sg.sortingOrder = 20 + i;
            }
            log.Add("타이틀 배치");
        }

        // ===== round HUD (concept Art/Raw_Doodle/concept/round.png): one slim score card top-left, slim time bar,
        // light chips, a butter-yellow weather banner (important), small beige buttons; the field stays open =====
        static void RoundHud(Transform hud, List<string> log)
        {
            if (hud == null) { log.Add("RoundHUD 없음"); return; }
            var score = (RectTransform)hud.Find("ScorePanel");
            TL(score, 16, 14, 300, 318); SetImage(score, "UI/panel_wood", Color.white); Off<Outline>(score);
            int i = 0;
            foreach (var n in new[] { "Slot_ed", "Slot_md", "Slot_ps" })
            {
                var sl = (RectTransform)hud.Find(n);
                TL(sl, 30, 136 + i * 50, 272, 46); SetImage(sl, "UI/chip", Color.white); Off<Outline>(sl); i++;
                foreach (var t in sl.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "pc"); t.color = INK; }
            }
            var val = (RectTransform)hud.Find("ValuePanel");
            TL(val, 30, 136 + 3 * 50 + 6, 272, 46); SetImage(val, "UI/chip", new Color(1f, 0.93f, 0.7f)); Off<Outline>(val);
            foreach (var t in score.GetComponentsInChildren<TMP_Text>(true).Concat(val.GetComponentsInChildren<TMP_Text>(true))) { Undo.RecordObject(t, "pc"); t.color = INK; }

            var tb = (RectTransform)hud.Find("TimeBar"); SetImage(tb, "UI/progress_back", Color.white); Off<Outline>(tb);
            SetImage(tb.Find("Track"), "UI/rail_item", Color.white);
            foreach (var t in tb.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "pc"); t.color = INK; }
            var stat = hud.Find("StatBar"); SetImage(stat, "UI/chip", new Color(1, 1, 1, 0.9f)); Off<Outline>(stat);
            foreach (var t in stat.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "pc"); t.color = INK; }
            var banner = hud.Find("WeatherBanner"); SetImage(banner, "UI/panel_accent", Color.white);
            var corner = hud.Find("Corner"); SetImage(corner, "UI/panel_paper", Color.white); Off<Outline>(corner);
            foreach (var t in corner.GetComponentsInChildren<TMP_Text>(true)) if (t.name == "Region" || t.name == "Hint") { Undo.RecordObject(t, "pc"); t.color = INK; }
            foreach (var n in new[] { "PauseButton", "CodexButton" })
            {
                var b = hud.Find(n); SetImage(b, "UI/btn_wood", Color.white); Off<Outline>(b); LabelColor(b, INK, 26);
            }
            foreach (var t in hud.Find("AimHint").GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "pc"); t.color = INK; }
            log.Add("라운드 HUD");
        }

        // ===== upgrade tree hub =====
        static void TreeHub(Transform ts, List<string> log)
        {
            if (ts == null) { log.Add("TreeScreen 없음"); return; }
            // top bar: full width, title (stage) left, currencies right
            var top = (RectTransform)ts.Find("Topbar");
            TL(top, 0, 0, W, TOP_H);
            SetImage(top, "UI/bar_top", Color.white);
            Off<Outline>(top); Off<ContentSizeFitter>(top);
            var hl = top.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset((int)(RAIL_W + PAD * 2), (int)PAD, 8, 8); hl.spacing = 12;
            hl.childAlignment = TextAnchor.MiddleLeft; hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            var stage = top.Find("Stage"); SetImage(stage, null, CLEAR); LE(stage, -1, -1, flex: 1);
            foreach (var n in new[] { "Gold", "Gem" }) { var c = top.Find(n); SetImage(c, "UI/panel_paper", Color.white); LE(c, 190, -1); }
            var sep = top.Find("Sep"); if (sep) sep.gameObject.SetActive(false);
            foreach (var t in top.GetComponentsInChildren<TMP_Text>(true)) t.color = INK;

            // left rail: every hub button, top to bottom
            var rail = Child(ts, "Rail");
            rail.SetSiblingIndex(top.GetSiblingIndex() + 1);
            TL(rail, PAD, TOP_H + PAD, RAIL_W, H - TOP_H - PAD * 2 - HINT_H);
            var vl = Get<VerticalLayoutGroup>(rail);
            vl.spacing = ENTRY_GAP; vl.childAlignment = TextAnchor.UpperLeft; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var buttons = ts.Find("Buttons");
            string[] order = { "Farm", "Quests", "Skin", "Codex", "Workshop", "AutoHarvest", "Bulk", "Records", "Options", "Title" };
            for (int i = 0; i < order.Length; i++)
            {
                var b = Deep(ts, order[i]);
                if (b == null) { log.Add("버튼 없음 " + order[i]); continue; }
                b.SetParent(rail, false); b.SetSiblingIndex(i);
                RailEntry(b);
            }
            // primary action bottom-right, boss toggle above it
            var go = Deep(ts, "Go");
            if (go != null)
            {
                go.SetParent(ts, false);
                BR(go, PAD, HINT_H + PAD, 340, 76);
                SetImage(go, "UI/btn_go", Color.white);
                LabelColor(go, Color.white, 30);
            }
            var boss = ts.Find("BossButton");
            if (boss != null) { BR(boss, PAD, HINT_H + PAD + 76 + 12, 340, 64); }
            if (buttons != null) buttons.gameObject.SetActive(false);
            // legend: top-right under the bar
            var legend = (RectTransform)ts.Find("Legend");
            if (legend != null) { TR(legend, PAD, TOP_H + PAD, legend.rect.width, legend.rect.height); SetImage(legend, "UI/panel_paper", new Color(1, 1, 1, 0.92f)); foreach (var t in legend.GetComponentsInChildren<TMP_Text>(true)) if (t.name.StartsWith("Label") || t.name == "Head" || t.name == "GemLabel") t.color = INK; }

            Hints(ts, "<b>Space</b> 수확하러 가기    <b>W</b> 세계수    <b>Q</b> 의뢰    <b>S</b> 스킨    <b>C</b> 도감    <b>B</b> 공방    <b>R</b> 수확 보따리    <b>O</b> 옵션    <b>휠</b> 확대·축소    <b>드래그</b> 이동",
                new[] { (Key.Space, "go", ""), (Key.W, "farm", ""), (Key.Q, "quests", ""), (Key.S, "skinshop", ""), (Key.C, "codex", ""), (Key.B, "workshop", ""), (Key.R, "wtbag", ""), (Key.O, "options", "") });
            log.Add("균사 트리 PC 배치");
        }

        // ===== world tree hub =====
        static void FarmHub(Transform fs, List<string> log)
        {
            if (fs == null) { log.Add("FarmScreen 없음"); return; }
            var bar = Child(fs, "TopBar"); bar.SetSiblingIndex(0);
            TL(bar, 0, 0, W, TOP_H);
            var bi = Get<Image>(bar); bi.sprite = Sp("UI/bar_top"); bi.type = Image.Type.Sliced; bi.color = Color.white; bi.raycastTarget = false;

            var titleBox = fs.Find("TitleBox"); TL((RectTransform)titleBox, RAIL_W + PAD * 2, 6, 760, 52); SetImage(titleBox, null, CLEAR);
            foreach (var t in titleBox.GetComponentsInChildren<TMP_Text>(true)) { t.color = INK; t.fontSize = 30; t.alignment = TextAlignmentOptions.MidlineLeft; }
            var statusBox = fs.Find("StatusBox"); TL((RectTransform)statusBox, RAIL_W + PAD * 2 + 770, 14, 520, 36); SetImage(statusBox, null, CLEAR);
            foreach (var t in statusBox.GetComponentsInChildren<TMP_Text>(true)) { t.color = INK; t.fontSize = 20; t.alignment = TextAlignmentOptions.MidlineLeft; }
            var gem = (RectTransform)fs.Find("GemBox"); TR(gem, PAD, 8, 190, 48); SetImage(gem, "UI/panel_paper", Color.white); Off<Outline>(gem);
            var gold = (RectTransform)fs.Find("GoldBox"); TR(gold, PAD + 190 + 12, 8, 190, 48); SetImage(gold, "UI/panel_paper", Color.white); Off<Outline>(gold);
            foreach (var t in gem.GetComponentsInChildren<TMP_Text>(true).Concat(gold.GetComponentsInChildren<TMP_Text>(true))) t.color = INK;

            // left rail: back, the five tabs (filled by FarmScreen into Chips), then the extra buttons
            float y = TOP_H + PAD;
            var back = fs.Find("Bottom/Back"); TL((RectTransform)back, PAD, y, RAIL_W, ENTRY_H); RailEntry(back); y += ENTRY_H + ENTRY_GAP * 3;
            var chips = fs.Find("Bottom/Chips"); TL((RectTransform)chips, PAD, y, RAIL_W, 5 * 58); SetImage(chips, null, CLEAR); y += 5 * 58 + ENTRY_GAP * 2;
            foreach (var n in new[] { "Skin", "Build", "TreeToggle" })
            {
                var b = fs.Find(n); TL((RectTransform)b, PAD, y, RAIL_W, ENTRY_H); RailEntry(b); y += ENTRY_H + ENTRY_GAP;
            }
            // right docked window + reward button under it
            var panel = (RectTransform)fs.Find("TreePanel");
            TL(panel, W - PAD - PANEL_W, TOP_H + PAD, PANEL_W, H - TOP_H - PAD * 3 - HINT_H - 72);
            SetImage(panel, "UI/panel_wood", Color.white); Off<Outline>(panel);
            var care = (RectTransform)fs.Find("Bottom/CareAll");
            TL(care, W - PAD - PANEL_W, H - HINT_H - PAD - 64, PANEL_W, 64);
            SetImage(care, "UI/btn_go", Color.white); LabelColor(care, Color.white, 26);

            Hints(fs, "<b>1</b>~<b>5</b> 탭    <b>R</b> 수확 보따리 받기    <b>S</b> 스킨    <b>C</b> 도감    <b>Esc</b> 균사 트리로",
                new[] { (Key.Digit1, "wttab", "grow"), (Key.Digit2, "wttab", "kid"), (Key.Digit3, "wttab", "req"), (Key.Digit4, "wttab", "craft"), (Key.Digit5, "wttab", "deco"),
                        (Key.R, "wtbag", ""), (Key.S, "skinshop", ""), (Key.C, "codex", ""), (Key.Escape, "totree2", "") });
            log.Add("세계수 PC 배치");
        }

        // ===== whole canvas: thin soft edges, white labels on saturated buttons =====
        static void Restyle(Transform canvas, List<string> log)
        {
            int outl = 0, labels = 0;
            foreach (var o in canvas.GetComponentsInChildren<Outline>(true))
            {
                var img = o.GetComponent<Image>();
                if (img == null || img.sprite == null) continue;
                string n = img.sprite.name;
                if (n.StartsWith("panel_") || n.StartsWith("btn_") || n == "badge_round")
                {
                    Undo.RecordObject(o, "pc"); o.effectColor = new Color(EDGE.r, EDGE.g, EDGE.b, 0.8f); o.effectDistance = new Vector2(2, -2); outl++;
                }
            }
            foreach (var img in canvas.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null || img.GetComponent<Button>() == null) continue;
                string n = img.sprite.name;
                bool vivid = n == "btn_go" || n == "btn_danger" || n == "btn_orange" || n == "btn_blue";
                if (!vivid) continue;
                foreach (var t in img.GetComponentsInChildren<TMP_Text>(true)) { if (t.transform.parent != img.transform) continue; Undo.RecordObject(t, "pc"); t.color = Color.white; labels++; }
            }
            log.Add($"테두리 {outl}개 · 진한 버튼 글자 {labels}개");
        }

        // ===== pieces =====
        static void RailEntry(Transform b)
        {
            SetImage(b, "UI/rail_item", Color.white);
            LE(b, -1, ENTRY_H);
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.transform.parent != b) continue;
                Undo.RecordObject(t, "pc"); t.color = INK; t.fontSize = 22; t.alignment = TextAlignmentOptions.MidlineLeft;
                var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(20, 0); rt.offsetMax = new Vector2(-8, 0);
            }
        }

        static void Hints(Transform screen, string text, (Key key, string act, string arg)[] binds)
        {
            var strip = Child(screen, "HintBar");
            BL(strip, 0, 0, W, HINT_H);
            var img = Get<Image>(strip); img.sprite = Sp("UI/bar_top"); img.type = Image.Type.Sliced; img.color = Color.white; img.raycastTarget = false;
            var label = strip.Find("Text") as RectTransform;
            if (label == null)
            {
                // clone an existing scene label so font, LocText and material presets come along
                var src = screen.root.GetComponentsInChildren<TMP_Text>(true).First(t => t.GetComponent<LocText>() != null && t is TextMeshProUGUI);
                var go = Object.Instantiate(src.gameObject, strip); go.name = "Text";
                foreach (Transform c in go.transform) Object.DestroyImmediate(c.gameObject);
                Undo.RegisterCreatedObjectUndo(go, "pc");
                label = (RectTransform)go.transform;
            }
            label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one; label.offsetMin = new Vector2(PAD + 8, 0); label.offsetMax = new Vector2(-PAD, 0);
            var tmp = label.GetComponent<TMP_Text>();
            tmp.text = text; tmp.fontSize = 18; tmp.color = INK; tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.enableWordWrapping = false; tmp.raycastTarget = false;
            var lt = label.GetComponent<LocText>(); if (lt != null) { lt.key = text; lt.authored = text; }
            var sc = Get<HubShortcuts>((RectTransform)screen);
            sc.binds = binds.Select(b => new HubShortcuts.Bind { key = b.key, act = b.act, arg = string.IsNullOrEmpty(b.arg) ? null : b.arg }).ToArray();
            EditorUtility.SetDirty(sc);
        }

        static void SetImage(Transform t, string key, Color c)
        {
            var img = t.GetComponent<Image>(); if (img == null) return;
            Undo.RecordObject(img, "pc");
            if (key != null) { var s = Sp(key); if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; } }
            img.color = c;
        }

        static void LabelColor(Transform b, Color c, float size)
        {
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true)) { if (t.transform.parent != b) continue; Undo.RecordObject(t, "pc"); t.color = c; t.fontSize = size; }
        }

        static void LE(Transform t, float w, float h, float flex = -1)
        {
            var le = Get<LayoutElement>((RectTransform)t);
            le.ignoreLayout = false; le.preferredWidth = w; le.preferredHeight = h; le.minHeight = h; le.flexibleWidth = flex;
        }

        static void Off<T>(Transform t, bool off = true) where T : Behaviour { var c = t.GetComponent<T>(); if (c != null) { Undo.RecordObject(c, "pc"); c.enabled = !off; } }

        static T Get<T>(RectTransform t) where T : Component { var c = t.GetComponent<T>(); if (c == null) c = Undo.AddComponent<T>(t.gameObject); else Undo.RecordObject(c, "pc"); return c; }
        static T Get<T>(Transform t) where T : Component => Get<T>((RectTransform)t);

        static RectTransform Child(Transform parent, string name)
        {
            var t = parent.Find(name) as RectTransform;
            if (t != null) return t;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "pc");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Transform Deep(Transform t, string name)
        {
            foreach (Transform c in t) { if (c.name == name) return c; var r = Deep(c, name); if (r != null) return r; }
            return null;
        }

        // rects in canvas pixels: TL = from top-left, TR = right margin from top-right, BR / BL = from the bottom corners
        static void TL(RectTransform rt, float x, float y, float w, float h) => Set(rt, new Vector2(0, 1), new Vector2(x, -y), w, h);
        static void TR(RectTransform rt, float r, float y, float w, float h) => Set(rt, new Vector2(1, 1), new Vector2(-r, -y), w, h);
        static void BR(Transform t, float r, float b, float w, float h) => Set((RectTransform)t, new Vector2(1, 0), new Vector2(-r, b), w, h);
        static void BL(RectTransform rt, float x, float b, float w, float h) => Set(rt, new Vector2(0, 0), new Vector2(x, b), w, h);
        static void Set(RectTransform rt, Vector2 corner, Vector2 pos, float w, float h)
        {
            Undo.RecordObject(rt, "pc");
            rt.anchorMin = rt.anchorMax = rt.pivot = corner;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(w, h); rt.localScale = Vector3.one;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
