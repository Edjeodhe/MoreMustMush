using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 세계수 탭 창: 성장 · 꼬마(부탁 포함) · 공방 · 꾸미기. OutGame의 Button_Group 탭이 연다.
    // 내용과 버튼 동작은 씬 분리 전 FarmScreen.Tabs.cs · GameFlow의 wt* 액션(ed6b8aa^)을 옮긴 것. 규칙은 Game.Farm*.
    // 씬에 배치된 FarmTabPanel이 있으면 그것을 쓴다. 이름으로 찾는 것: Title(제목 글자) · Exit(닫기) · Content(줄이 들어갈 자리, 보통 ScrollRect 안)
    // 줄 하나 = 그림 · 글자 · 버튼 몇 개 (코드가 Content 안에 만든다). 배치된 창이 없으면 임시 창을 만든다.
    public class FarmTabWindow
    {
        public const string PANEL = "FarmTabPanel";
        public enum Tab { Grow, Kid, Craft, Deco }

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public Tab Current { get; private set; }

        FarmView farm;
        Action changed;            // 재화·정령 칸 등 바깥 화면을 다시 그린다
        TMP_Text title;
        RectTransform content;
        ScrollRect scroll;
        string selKid; int selDeco = -1;
        float tickT;

        public static FarmTabWindow For(Scene scene, FarmView farm, Action changed)
        {
            var w = new FarmTabWindow { farm = farm, changed = changed };
            style = SceneUI.TextIn(SceneUI.Find(scene, "Button_Group"));   // 탭 버튼 글자 = 이 창 글자의 폰트 기준
            var placed = SceneUI.Find(scene, PANEL);
            if (placed != null) w.WirePlaced(placed);
            else w.BuildTemp(scene);
            if (w.Root != null) w.Root.SetActive(false);
            return w;
        }

        public void SetFarm(FarmView f) => farm = f;

        public void Open(Tab t)
        {
            if (Root == null) return;
            Current = t;
            Root.SetActive(true);
            Root.transform.SetAsLastSibling();
            Render();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        // 언어가 바뀌면 열린 창을 그 언어로 다시 그린다 (OutGameController가 Loc.Changed에서 부른다)
        public void Refresh() { if (IsOpen) Render(); }

        public void Close() { if (Root != null && Root.activeSelf) { Root.SetActive(false); changed?.Invoke(); } }

        // 공방 제작 시간처럼 흐르는 글자: 열려 있을 때 1초마다
        public void Tick()
        {
            if (!IsOpen || (tickT -= Time.unscaledDeltaTime) > 0) return;
            tickT = 1;
            if (Current == Tab.Craft && G?.farm != null && G.farm.crafts.Count > 0) Render(false);
        }

        void WirePlaced(Transform root)
        {
            Root = root.gameObject;
            title = SceneUI.TextIn(SceneUI.Find(root, "Title"));
            var exit = SceneUI.Find(root, "Exit");
            if (exit != null) SceneUI.Bind(exit, () => { Snd.Ui(); Close(); });
            content = SceneUI.Need(root, "Content") as RectTransform;
            scroll = root.GetComponentInChildren<ScrollRect>(true);
            if (content != null && content.GetComponent<VerticalLayoutGroup>() == null) ListLayout(content.gameObject);
        }

        // ===== 임시 창 (배치 전): 화면 오른쪽 큰 상자 · 제목 · 스크롤 목록 · 닫기 =====
        // 그림은 Art/Dream/UI (OutGame에 배치된 것과 같은 세트): 창틀 panel_wood · 제목 ribbon · 줄 panel_paper/mint/accent · 버튼 btn_go/orange/wood · 닫기 btn_close
        static readonly Color INK = new Color(0.29f, 0.2f, 0.15f), BTN_TEXT = Color.white, OFF_TINT = new Color(0.8f, 0.78f, 0.75f, 0.85f);
        static Sprite UI(string n) => SpriteDB.Get("UI/" + n);
        // 세계수 창 아이콘 (icons_farm 시트: f_*). 아직 없으면 비슷한 기존 아이콘
        static Sprite Ico(string name, string fallback) => SpriteDB.Icon(name) ?? SpriteDB.Icon(fallback) ?? UI(fallback);

        // 글자 폰트: 한국어·영어는 배치한 UI(탭 버튼)와 같은 폰트, 그 밖의 언어는 그 언어 본문 폰트 (다시 그릴 때마다 맞춘다)
        static TMP_Text style;
        static TMP_Text Txt(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, bool stretch = false)
        {
            var t = TempDialog.MakeText(parent, name, text, pos, size, fontSize, stretch);
            LangFont(t);
            return t;
        }

        // 한국어·영어 = 배치한 UI와 같은 폰트 (ONEMobilePOP). 그 밖의 언어는 POP에 글자가 없어 대체 폰트로 빌려 오면 자간이 벌어지니 그 언어 본문 폰트를 바로 쓴다
        static void LangFont(TMP_Text t)
        {
            if (t == null) return;
            var f = LocFonts.KoEnLang(Loc.Lang) ? style?.font : LocFonts.I?.FaceFor(Loc.Lang, LocFonts.Role.Body)?.font ?? style?.font;
            if (f != null && t.font != f) { t.font = f; t.fontSharedMaterial = f.material; }
        }
        static void Skin(Image im, string sprite, Color fallback)
        {
            var sp = UI(sprite);
            if (sp == null) { im.color = fallback; return; }
            im.sprite = sp; im.color = Color.white;
            im.type = sp.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        }

        void BuildTemp(Scene scene)
        {
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) { Debug.LogWarning("[FarmTab] 캔버스가 없어 세계수 창을 만들 수 없어요"); return; }
            var dim = new GameObject(PANEL, typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            TempDialog.Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.05f, 0.5f);
            Root = dim;

            // 오른쪽 탭 버튼(Button_Group)을 가리지 않게 왼쪽으로 치우친 나무 창
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(dim.transform, false);
            var br = box.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(-150, -40); br.sizeDelta = new Vector2(1380, 860);
            Skin(box.GetComponent<Image>(), "panel_wood", new Color(0.98f, 0.93f, 0.8f));

            var rib = new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
            rib.transform.SetParent(box.transform, false);
            var rr = rib.GetComponent<RectTransform>();
            // 그림(328×132)이 가로로 너무 늘어나지 않게 짧게 (옛 900×140)
            rr.anchoredPosition = new Vector2(0, 425); rr.sizeDelta = new Vector2(520, 130);
            var ri = rib.GetComponent<Image>(); Skin(ri, "ribbon", new Color(0.97f, 0.66f, 0.45f)); ri.type = Image.Type.Simple; ri.raycastTarget = false;
            title = Txt(rib.transform, "Title", "", new Vector2(0, 12), new Vector2(400, 64), 40);
            title.color = Color.white; title.enableAutoSizing = true; title.fontSizeMin = 24; title.fontSizeMax = 40;

            var close = new GameObject("Exit", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(box.transform, false);
            var cr = close.GetComponent<RectTransform>();
            cr.anchoredPosition = new Vector2(660, 400); cr.sizeDelta = new Vector2(96, 92);
            var ci = close.GetComponent<Image>(); Skin(ci, "btn_close", new Color(0.9f, 0.4f, 0.35f)); ci.type = Image.Type.Simple;
            close.GetComponent<Button>().onClick.AddListener(() => { Snd.Ui(); Close(); });

            var view = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            view.transform.SetParent(box.transform, false);
            var vr = view.GetComponent<RectTransform>();
            vr.anchoredPosition = new Vector2(0, -30); vr.sizeDelta = new Vector2(1290, 740);
            view.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            view.GetComponent<Mask>().showMaskGraphic = false;
            var c = new GameObject("Content", typeof(RectTransform));
            c.transform.SetParent(view.transform, false);
            content = c.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            ListLayout(c);
            scroll = view.GetComponent<ScrollRect>();
            scroll.content = content; scroll.horizontal = false; scroll.scrollSensitivity = 40; scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        static void ListLayout(GameObject go)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 8, 16); v.spacing = 10;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // kind: go(초록, 기본) · gem(주황, 균사석을 쓰는 일) · wood(나무, 바꾸기·선물 같은 가벼운 일)
        static GameObject Btn(Transform parent, string name, string label, UnityAction action, string kind, bool on)
        {
            var b = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            b.transform.SetParent(parent, false);
            var img = b.GetComponent<Image>();
            Skin(img, on ? (kind == "gem" ? "btn_orange" : kind == "wood" ? "btn_wood" : "btn_go") : "btn_wood", new Color(0.55f, 0.36f, 0.2f));
            var btn = b.GetComponent<Button>(); btn.targetGraphic = img; if (action != null) btn.onClick.AddListener(action);
            var cb = btn.colors; cb.disabledColor = OFF_TINT; btn.colors = cb;
            btn.interactable = on;
            var text = Txt(b.transform, "Label", label, Vector2.zero, Vector2.zero, 28, true);
            var tr = text.rectTransform; tr.offsetMin = new Vector2(14, 6); tr.offsetMax = new Vector2(-14, -8);
            text.color = on && kind != "wood" ? BTN_TEXT : INK; text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = 28;
            if (!on) text.alpha = 0.6f;
            return b;
        }

        // ===== 줄 =====
        const string BG = "panel_paper", BG_ON = "panel_mint", BG_OFF = "off", BG_GOLD = "panel_accent";   // 줄 배경 그림 (off = 흐린 종이)
        static string G_(double v) => UIUtil.Ic("gold") + U.Fmt(v);
        static string Gem(double v) => UIUtil.Ic("gem") + U.Fmt(v);
        static string Gray(string s) => "<color=#9a7a5a>" + s + "</color>";
        static string Small(string s) => "<size=85%>" + s + "</size>";

        struct B
        {
            public string label, kind; public UnityAction act; public bool on;
            public B(string l, UnityAction a, bool o, string k = null) { label = l; act = a; on = o; kind = k ?? (l != null && l.Contains("gem") ? "gem" : "go"); }
        }

        // 다음 Line에 붙일 진행 막대 (0~1, 막대 위 글자)
        (float v, string label)? bar;
        void NextBar(double v, string label) => bar = ((float)Math.Max(0, Math.Min(1, v)), label);

        static void Bar(Transform parent, float v, string label)
        {
            var back = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            back.transform.SetParent(parent, false);
            var le = back.GetComponent<LayoutElement>(); le.preferredHeight = 30; le.preferredWidth = 520; le.flexibleWidth = 0;
            Skin(back.GetComponent<Image>(), "progress_back", new Color(0.85f, 0.8f, 0.72f));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(back.transform, false);
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(v, 1); fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(-3, -3);
            Skin(fill.GetComponent<Image>(), "progress_fill", new Color(0.5f, 0.75f, 0.4f));
            fill.SetActive(v > 0.01f);
            if (string.IsNullOrEmpty(label)) return;
            var t = Txt(back.transform, "Label", label, Vector2.zero, Vector2.zero, 20, true);
            t.color = INK; t.fontStyle = FontStyles.Bold;
        }

        // 구역 제목: 굵은 글자 + 덩굴 구분선 (오른쪽에 짧은 설명)
        void Section(string text, string note = null)
        {
            var row = new GameObject("Section", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            row.GetComponent<LayoutElement>().preferredHeight = 58;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 14, 0); h.spacing = 14; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var t = Txt(row.transform, "Text", "<b>" + text + "</b>", Vector2.zero, Vector2.zero, 32);
            t.color = new Color(0.45f, 0.28f, 0.15f); t.alignment = TextAlignmentOptions.MidlineLeft;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0;
            var vine = new GameObject("Vine", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            vine.transform.SetParent(row.transform, false);
            var vi = vine.GetComponent<Image>(); Skin(vi, "divider", Color.clear); vi.type = Image.Type.Simple; vi.preserveAspect = true; vi.raycastTarget = false;
            var vle = vine.GetComponent<LayoutElement>(); vle.preferredWidth = 150; vle.preferredHeight = 40;
            var n = Txt(row.transform, "Note", note == null ? "" : Small(Gray(note)), Vector2.zero, Vector2.zero, 26);
            n.alignment = TextAlignmentOptions.MidlineRight;
            n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        void Line(string text, Sprite icon, string back, params B[] btns)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(content, false);
            var bgi = row.GetComponent<Image>();
            Skin(bgi, back == BG_OFF ? BG : back, new Color(1f, 0.98f, 0.94f));
            if (back == BG_OFF) bgi.color = new Color(1, 1, 1, 0.55f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(26, 22, 18, 18); h.spacing = 18;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            if (icon != null)
            {
                // 둥근 배지 위의 아이콘 (그림 크기가 제각각이어도 줄마다 같은 자리·같은 크기)
                var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                badge.transform.SetParent(row.transform, false);
                var bi = badge.GetComponent<Image>(); Skin(bi, "badge_round", new Color(1, 1, 1, 0.4f)); bi.type = Image.Type.Simple; bi.raycastTarget = false;
                var ble = badge.GetComponent<LayoutElement>(); ble.preferredWidth = ble.preferredHeight = 92; ble.flexibleWidth = 0;
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                ig.transform.SetParent(badge.transform, false);
                var ir = ig.GetComponent<RectTransform>(); TempDialog.Stretch(ir); ir.offsetMin = new Vector2(10, 10); ir.offsetMax = new Vector2(-10, -10);
                var im = ig.GetComponent<Image>(); im.sprite = icon; im.preserveAspect = true; im.raycastTarget = false;
            }
            // 글자 + (있으면) 진행 막대
            var body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            body.transform.SetParent(row.transform, false);
            body.GetComponent<LayoutElement>().flexibleWidth = 1;
            var bv = body.GetComponent<VerticalLayoutGroup>();
            bv.spacing = 8; bv.childControlWidth = bv.childControlHeight = true; bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
            var t = Txt(body.transform, "Text", text, Vector2.zero, Vector2.zero, 29);
            t.alignment = TextAlignmentOptions.MidlineLeft; t.richText = true; t.color = INK; t.lineSpacing = 4;
            if (bar != null) Bar(body.transform, bar.Value.v, bar.Value.label);
            bar = null;
            if (btns.Length == 0) return;
            var col = new GameObject("Buttons", typeof(RectTransform), typeof(GridLayoutGroup));
            col.transform.SetParent(row.transform, false);
            var g = col.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(236, 76); g.spacing = new Vector2(8, 6);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = btns.Length > 2 ? 2 : 1;
            g.childAlignment = TextAnchor.MiddleRight;
            foreach (var b in btns) Btn(col.transform, "Btn", b.label, b.act, b.kind, b.on && b.act != null);
            int rows = (btns.Length + g.constraintCount - 1) / g.constraintCount;
            var cle = col.AddComponent<LayoutElement>();
            cle.preferredWidth = g.constraintCount * 244; cle.preferredHeight = rows * 82;
        }

        void Render(bool full = true)
        {
            if (Root == null || content == null || G?.farm == null) return;
            float keep = scroll != null ? scroll.verticalNormalizedPosition : 1;
            for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject);
            content.DetachChildren();
            LangFont(title);   // 창과 함께 한 번 만든 제목도 지금 언어 폰트로
            switch (Current)
            {
                case Tab.Kid: TabKid(); break;
                case Tab.Craft: TabCraft(); break;
                case Tab.Deco: TabDeco(); break;
                default: TabGrow(); break;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (scroll != null) scroll.verticalNormalizedPosition = keep;
        }

        // 버튼을 누른 뒤: 다시 그리고 바깥 화면도
        void After() { Render(); changed?.Invoke(); }

        // ===== 성장: 나이테 · 진화 · 공물 / 수확 / 가호 =====
        void TabGrow()
        {
            int s = Stage();
            UIUtil.SetText(title, Loc.F("세계수 · {0}단계 {1}", s, WT.StageName(s)));
            var tree = SpriteDB.Get("Farm/Tree/tree_" + WT.Picture(s));

            Section(Loc.T("성장"));
            double rc = RingCost();
            NextBar((double)Ring() / WT.ringMax, Loc.F("나이테 Lv.{0}/{1}", Ring(), WT.ringMax));
            Line(Loc.T("<b>나이테</b>") + "  " + Small(Gray(Loc.F("나무버섯 성장 +{0}", U.Pct(WT.ringGrow * (Ring() - 1))))),
                Ico("f_ring", "mushtree"), BG, rc > 0 ? new B(Loc.F("성장 {0}", G_(rc)), DoRing, G.gold >= rc) : new B(Loc.T("최대"), null, false));

            if (!StageMax())
            {
                var why = CanEvolve();
                var join = s < CRITTER_JOIN.Length ? SPC[CRITTER_JOIN[s]] : null;
                string cost = G_(WT.evoGold[s]) + (WT.evoGem[s] > 0 ? "  " + Gem(WT.evoGem[s]) : "");
                string reward = Loc.F("슬롯 {0}→{1} · 장식 {2}→{3}", WT.mushSlots[s - 1], WT.mushSlots[s], WT.decoSlots[s - 1], WT.decoSlots[s]) + (join != null ? " · " + Loc.F("{0} 해금", join.n) : "");
                NextBar(TreeExp() / Math.Max(1, TreeExpNeed()), Loc.F("경험치 {0}/{1}", U.Fmt(Math.Floor(TreeExp())), U.Fmt(TreeExpNeed())));
                Line(Loc.F("<b>진화 → {0}단계 {1}</b>", s + 1, WT.StageName(s + 1)) + "  " + Small(cost) + "\n" + Small(Gray(reward)),
                    SpriteDB.Get("Farm/Tree/tree_" + WT.Picture(s + 1)), why == GrowFail.None ? BG_GOLD : BG,
                    new B(why == GrowFail.None ? Loc.T("진화!") : why == GrowFail.Ring ? Loc.T("나이테 부족") : why == GrowFail.Exp ? Loc.T("경험치 부족") : why == GrowFail.Gold ? Loc.T("골드 부족") : Loc.T("균사석 부족"), DoEvolve, why == GrowFail.None));

                double tv = TributeAllValue();
                string pref = string.Join(" · ", WT_PREFER[s - 1].Select(n => SP_BY_NAME[n]).Select(sp => sp.n + Gray(" " + StoreOf(sp.id))));
                Line(Loc.F("<b>공물</b>  창고 버섯 {0}개 → 경험치 +{1}", U.Fmt(StoreTotal()), U.Fmt(Math.Floor(tv))) + "\n" + Small(Gray(Loc.F("선호 ×2: {0}", pref))),
                    Ico("f_tribute", "basket"), BG, new B(Loc.T("모두 바치기"), DoTribute, tv > 0));
            }
            else Line(Loc.T("<b>아르카나 세계수</b> · 최종 단계"), tree, BG_GOLD);

            Section(Loc.T("수확"), Loc.F("보따리 최대 {0}", TimeText(BagCapSec())));
            var zs = ZoneSlots(); var rate = BagRate();
            Line(Loc.F("<b>나무버섯 슬롯 {0}개</b>", MushSlotCount()) + "  " + Small(Loc.F("시간당 {0}  {1}", G_(rate.gold * 3600), Gem(rate.gem * 3600)))
                + "\n" + Small(string.Join(" · ", ZONES.Select(z => ZoneOpen(z.id) ? z.n + " " + zs[z.id] : Gray(Loc.F("{0} {1}단계", z.n, z.open))))),
                SpriteDB.Icon("auto_harvest"), BG);

            // 가호: 받은 것 + 다음 하나만 (8줄을 다 늘어놓지 않는다)
            Section(Loc.T("세계수의 가호"), Loc.T("라운드 영구 적용"));
            for (int i = 0; i < BLESS.Length && i <= s; i++)
            {
                bool got = i < s;
                Line((got ? "<color=#2a9a74>" : "<color=#a89a86>") + Loc.F("<b>{0}단계 {1}</b>  {2}", i + 1, WT.StageName(i + 1), BLESS[i].text) + "</color>",
                    got ? Ico("f_bless", "medal") : SpriteDB.Icon("lock"), got ? BG : BG_OFF);
            }
        }

        void DoRing()
        {
            if (!RingUp()) { Snd.Err(); App.Notify(Loc.T("골드 부족")); return; }
            Snd.Buy(); App.Notify(Loc.F("나이테 Lv.{0}", Ring())); After();
        }

        void DoEvolve()
        {
            var why = CanEvolve();
            if (!Evolve(out var opened))
            {
                Snd.Err();
                App.Notify(why == GrowFail.Ring ? Loc.F("나이테 Lv.{0} 필요", WT.ringMax) : why == GrowFail.Exp ? Loc.T("경험치 부족") : why == GrowFail.Gem ? Loc.T("균사석 부족") : Loc.T("골드 부족"));
                return;
            }
            Snd.Record();
            if (farm != null) farm.TreeGrew(Loc.F("{0}단계 {1}!", Stage(), WT.StageName(Stage())));
            string msg = Loc.F("세계수 {0}단계 {1}! · 가호: {2}", Stage(), WT.StageName(Stage()), BLESS[Stage() - 1].text);
            if (opened != null) foreach (var k in opened) msg += "\n" + Loc.F("{0} 합류 가능", k.n);
            App.Notify(msg);
            if (farm != null) farm.Sync(false);
            After();
        }

        void DoTribute()
        {
            double exp = TributeAll();
            if (exp <= 0) { Snd.Err(); App.Notify(Loc.T("바칠 버섯 없음")); return; }
            Snd.Record();
            if (farm != null) farm.TreeGrew(Loc.F("경험치 +{0}", U.Fmt(Math.Floor(exp))));
            App.Notify(Loc.F("공물 · 경험치 +{0}", U.Fmt(Math.Floor(exp))) + (CanEvolve() == GrowFail.None ? "\n" + Loc.T("진화 가능!") : ""));
            After();
        }

        // ===== 꼬마: 부탁 / 구역 · 정령 (Lv · 진화 · 구역 · 선물) =====
        void TabKid()
        {
            UIUtil.SetText(title, Loc.F("꼬마 {0}/{1} · Lv 합계 {2}", KidsOwned().Count, KIDS.Length, KLvSum()));
            Requests();

            int cap = ZoneCapacity();
            int next = KLV.sumAt.FirstOrDefault(a => a > KLvSum());
            string nextTxt = next > 0 ? Loc.F("Lv 합계 {0}: 장식 슬롯 +1", next) + (next == KLV.sumAt[1] || next == KLV.sumAt[3] ? " · " + Loc.T("구역 정원 +1") : next == KLV.sumAt[4] ? " · " + Loc.T("칭호 '꼬마 대장'") : "") : Loc.T("칭호 '꼬마 대장'");
            Section(Loc.T("꼬마"), nextTxt);
            Line(Loc.T("<b>구역</b>") + "  " + Small(string.Join(" · ", ZONES.Select(z => ZoneOpen(z.id) ? Loc.F("{0} {1}/{2}", z.n, KidsIn(z.id).Count, cap) : Gray(Loc.F("{0} {1}단계", z.n, z.open)))))
                + "\n" + Small(Gray(Loc.T("선호 구역 수확 +50% · 빈 구역은 수확 안 함"))), Ico("f_zone", "mushtree"), BG);

            foreach (var k in KIDS)
            {
                var sp = SPC[k.id];
                var icon = SpriteDB.Get("Characters/Kids/" + k.id) ?? SpriteDB.Get("Characters/Critters/" + k.id + "_ref");
                if (!HasSpecial(k.id))
                {
                    if (!KidOpen(k.id)) { Line(Gray(Loc.F("<b>{0}</b>  세계수 {1}단계 해금", sp.n, JoinLv(k.id))), SpriteDB.Icon("lock"), BG_OFF); continue; }
                    var uf = CanUnlockKid(k.id);
                    Line(Loc.F("<b>{0}</b>  합류 가능", sp.n) + "\n" + Small(Gray(k.traitText)), icon, uf == KidFail.None ? BG_GOLD : BG,
                        new B(uf == KidFail.Gem ? Loc.T("균사석 부족") : Loc.F("데려오기 {0}", Gem(UnlockGem(k.id))), () => DoUnlock(k.id), uf == KidFail.None));
                    continue;
                }
                int lv = KLv(k.id), aw = Awaken(k.id), lvCap = KLV.Cap(aw);
                var aff = AffOf(k.id); string zone = ZoneOf(k.id);
                double mul = zone != null ? KidMul(k.id, zone) : 0;
                int step = KidStep(k.id);
                NextBar(aff.lv < AFF.max ? aff.cur / Math.Max(1, aff.need) : 1, Loc.F("호감도 Lv.{0}", aff.lv));
                string text = Loc.F("<b>{0}</b> <color=#e8a900>{1}</color>  Lv.{2}/{3}", sp.n, new string('★', aw), lv, lvCap)
                    + "\n" + Small((zone != null ? Loc.F("{0} · 수확 ×{1}", ZONE[zone].n, U.FmtN(mul)) : Gray(Loc.F("쉬는 중 · 선호 {0}", ZONE[k.zone].n))) + Gray(" · " + k.traitText))
                    + (step > 0 ? "\n" + Small("<color=#2a9a74>" + KidBonusText(k, step) + "</color>") : "");
                double lc = KLvCost(k.id); var lf = CanKidLvUp(k.id); var af = CanAwaken(k.id);
                string lvLabel = lf == KidFail.Max ? Loc.T("Lv 최대") : lf == KidFail.Cap ? Loc.T("진화 필요") : Loc.F("Lv업 {0}", G_(lc));
                string awLabel = aw >= KLV.awakenMax ? Loc.T("진화 최대")
                    : af == KidFail.Stage ? Loc.F("★{0} · {1}단계", aw + 1, AwakenStage(k.id))
                    : af == KidFail.Lv ? Loc.F("★{0} · Lv.{1}", aw + 1, 10 * (aw + 1))
                    : Loc.F("진화 ★{0} {1}", aw + 1, Gem(KLV.awakenGem[aw]));
                bool hasGift = G.farm.store.Count > 0;
                string id = k.id;
                Line(text, icon, id == selKid ? BG_ON : BG,
                    new B(lvLabel, () => DoKidLv(id), lf == KidFail.None),
                    new B(awLabel, () => DoAwaken(id), af == KidFail.None),
                    new B(Loc.F("구역: {0}", zone != null ? ZONE[zone].n : Loc.T("쉬는 중")), () => DoZone(id), true, "wood"),
                    new B(Loc.T("선물"), () => DoGift(id), hasGift, "wood"));
            }
        }

        void Requests()
        {
            ReqEnsure();
            Section(Loc.T("꼬마의 부탁"), Loc.F("보상 {0} · 호감도 +{1} · 매일 3개", G_(ReqGold()), REQ.aff));
            if (G.farm.reqs.Count == 0) Line(Small(Gray(Loc.T("아직 부탁 없음"))), Ico("f_request", "quest"), BG_OFF);
            for (int i = 0; i < G.farm.reqs.Count; i++)
            {
                var r = G.farm.reqs[i]; var sp = SP[r.sp]; int have = Math.Min(StoreOf(r.sp), r.n); int k = i;
                NextBar(r.done ? 1 : (double)have / Math.Max(1, r.n), r.done ? Loc.T("완료!") : $"{have}/{r.n}");
                Line(Loc.F("<b>{0}</b>  {1} ×{2}", SPC[r.kid].n, sp.n, r.n), SpriteDB.Single(sp.id), r.done ? BG_OFF : ReqReady(r) ? BG_GOLD : BG,
                    r.done ? new B(Loc.T("완료!"), null, false) : new B(Loc.T("들어주기"), () => DoReq(k), ReqReady(r)));
            }
            int wd = Math.Min(G.farm.weekDone, REQ.weekGoal);
            NextBar((double)wd / REQ.weekGoal, Loc.F("이번 주 {0}/{1}", wd, REQ.weekGoal));
            Line(Loc.F("<b>꼬마의 상자</b>  {0}", Gem(REQ.weekGem)), Ico("f_box", "trophy"), WeekReady() ? BG_GOLD : BG,
                G.farm.weekClaimed ? new B(Loc.T("받음"), null, false) : new B(Loc.T("받기"), DoWeek, WeekReady()));
        }

        void DoUnlock(string id)
        {
            if (!UnlockKid(id)) { Snd.Err(); App.Notify(Loc.T("균사석 부족")); return; }
            Snd.Record(); selKid = id; App.Notify(Loc.F("{0} 합류!", SPC[id].n));
            if (farm != null) farm.Sync(false);
            After();
        }

        void DoKidLv(string id)
        {
            if (!KidLvUp(id)) { Snd.Err(); App.Notify(CanKidLvUp(id) == KidFail.Cap ? Loc.T("진화 필요") : Loc.T("골드 부족")); return; }
            Snd.Buy(); selKid = id;
            if (farm != null) farm.Cheer(id, Loc.T("힘이 솟아나!"), $"Lv.{KLv(id)}");
            After();
        }

        void DoAwaken(string id)
        {
            var why = CanAwaken(id);
            if (!KidAwaken(id))
            {
                Snd.Err();
                App.Notify(why == KidFail.Stage ? Loc.F("★{1} 진화: 세계수 {0}단계 필요", AwakenStage(id), Awaken(id) + 1)
                    : why == KidFail.Lv ? Loc.F("Lv.{0} 필요", 10 * (Awaken(id) + 1)) : Loc.T("균사석 부족"));
                return;
            }
            Snd.Record(); selKid = id;
            if (farm != null) { farm.Cheer(id, Loc.T("반짝반짝… 진화했어!"), new string('★', Awaken(id))); farm.Sync(false); }
            App.Notify(Loc.F("{0} 진화 ★{1}! {2}", SPC[id].n, Awaken(id), KidBonusText(KID[id], KidStep(id))));
            After();
        }

        void DoZone(string id)
        {
            // 쉼 → 열린 구역(자리 있는 곳) 차례로 → 쉼
            string cur = ZoneOf(id);
            var order = new List<string> { null };
            foreach (var z in ZONES) if (ZoneOpen(z.id)) order.Add(z.id);
            int i0 = order.IndexOf(cur);
            for (int step = 1; step <= order.Count; step++)
            {
                string next = order[(i0 + step) % order.Count];
                if (next == null || next == cur || ZoneRoom(next)) { Assign(id, next); break; }
            }
            Snd.Ui(); selKid = id;
            if (farm != null) farm.Cheer(id, ZoneOf(id) != null ? Loc.F("{0}(으)로 갈게!", ZONE[ZoneOf(id)].n) : Loc.T("잠깐 쉴게~"), null);
            After();
        }

        void DoGift(string id)
        {
            // 좋아하는 버섯을 모두, 없으면 창고에 가장 많은 버섯 10개
            var k = KID[id]; string sp = null; int n = 0;
            foreach (var name in k.fav) { var sid = SP_BY_NAME[name].id; if (StoreOf(sid) > n) { sp = sid; n = StoreOf(sid); } }
            if (sp == null) { foreach (var kv in G.farm.store) if (kv.Value > n) { sp = kv.Key; n = kv.Value; } n = Math.Min(n, 10); }
            int up = sp == null ? -1 : Gift(id, sp, n);
            if (up < 0) { Snd.Err(); App.Notify(Loc.T("선물할 버섯 없음")); return; }
            Snd.Buy(); selKid = id;
            if (farm != null) farm.Cheer(id, IsFav(id, sp) ? Loc.T("이거 제일 좋아해! 고마워!") : U.Pick(FARM_LINES["thanks"]), up > 0 ? Loc.F("호감도 Lv.{0}!", AffLv(id)) : null);
            App.Notify(Loc.F("{0}에게 {1} ×{2}", SPC[id].n, SP[sp].n, n) + (up > 0 ? " · " + Loc.F("호감도 Lv.{0}!", AffLv(id)) : ""));
            After();
        }

        void DoReq(int i)
        {
            var gold = ReqDo(i);
            if (gold == null) { Snd.Err(); App.Notify(Loc.T("버섯 부족")); return; }
            Snd.Record();
            if (farm != null) farm.Cheer(G.farm.reqs[i].kid, U.Pick(FARM_LINES["thanks"]), null);
            App.Notify(Loc.F("부탁 완료 · {0}+{1}", UIUtil.Ic("gold"), U.Fmt(gold.Value)));
            After();
        }

        void DoWeek()
        {
            if (!WeekClaim()) { Snd.Err(); return; }
            Snd.Record(); App.Notify(Loc.F("꼬마의 상자! {0}+{1}", UIUtil.Ic("gem"), REQ.weekGem)); After();
        }

        // ===== 공방: 제작 중 / 레시피 =====
        void TabCraft()
        {
            if (!CraftOpen())
            {
                UIUtil.SetText(title, Loc.T("꼬마 공방"));
                NextBar((double)CodexCount() / Math.Max(1, WT.codexForCraft), Loc.F("도감 {0}/{1}종", CodexCount(), WT.codexForCraft));
                Line(Loc.T("<b>공방 잠김</b>") + "  " + Small(Gray(Loc.T("세계수 장식 제작"))), SpriteDB.Icon("book"), BG_OFF);
                return;
            }
            int slots = WT.CraftSlots(Stage());
            UIUtil.SetText(title, Loc.T("꼬마 공방"));
            Section(Loc.T("제작 중"), Loc.F("슬롯 {0}/{1} · 즉시 완료 오늘 {2}회", G.farm.crafts.Count, slots, RushLeftToday()));
            if (G.farm.crafts.Count == 0) Line(Small(Gray(Loc.T("제작 중인 장식 없음"))), Ico("f_craft", "clock"), BG_OFF);
            for (int i = 0; i < G.farm.crafts.Count; i++)
            {
                var c = G.farm.crafts[i]; var d = DECO[c.deco]; bool done = CraftDone(c); int k = i;
                double left = Math.Max(0, (c.at - Now()) / 1000), total = Math.Max(1, (c.at - c.t0) / 1000);
                NextBar(done ? 1 : 1 - left / total, done ? Loc.T("완성!") : Mmss((long)Math.Ceiling(left)));
                Line(Loc.F("<b>{0}</b>", d.n), SpriteDB.Get("Farm/Props/" + d.art), done ? BG_GOLD : BG,
                    done ? new B(Loc.T("받기"), () => DoClaim(k), true) : new B(Loc.F("즉시 {0}", Gem(RushCost(c))), () => DoRush(k), RushLeftToday() > 0 && G.gem >= RushCost(c)));
            }

            var locked = KIDS.Where(k => !HasSpecial(k.id)).Select(k => SPC[k.id].n).ToList();
            Section(Loc.T("레시피"), locked.Count > 0 ? Loc.F("합류하면 열림: {0}", string.Join(" · ", locked)) : null);
            foreach (var k in KIDS.Where(k => HasSpecial(k.id)))
                foreach (var d in DECOS.Where(x => x.of == k.id))
                {
                    var g = DECO_GRADES[d.grade]; var f = CanCraft(d.id); string id = d.id;
                    string own = DecoOwned(d.id) ? Loc.F("보유 ★{0}", DecoStar(d.id)) : Loc.T("미보유");
                    string text = Loc.F("<b>{0}</b>", d.n) + "  " + Small(Gray(SPC[k.id].n + " · " + g.n + " · " + (d.hang ? Loc.T("걸이형") : Loc.T("설치형")) + " · " + own))
                        + "\n" + Small(DecoFxText(d)) + "  " + Small(Gray(TimeText(CraftSeconds(d)) + " · " + Loc.F("아름다움 {0}", g.beauty)));
                    string cost = G_(g.gold) + (g.gem > 0 ? " " + Gem(g.gem) : "");
                    Line(text, SpriteDB.Get("Farm/Props/" + d.art), BG,
                        new B(f == CraftFail.Slots ? Loc.T("슬롯 없음") : f == CraftFail.Gold ? Loc.T("골드 부족") : f == CraftFail.Gem ? Loc.T("균사석 부족") : Loc.F("제작 {0}", cost), () => DoCraft(id), f == CraftFail.None, "go"));
                }
        }

        void DoCraft(string id)
        {
            var why = CanCraft(id);
            if (!CraftStart(id)) { Snd.Err(); App.Notify(why == CraftFail.Slots ? Loc.T("제작 슬롯 가득") : why == CraftFail.Gem ? Loc.T("균사석 부족") : why == CraftFail.Closed ? Loc.T("공방 미해금") : Loc.T("골드 부족")); return; }
            Snd.Buy(); App.Notify(Loc.F("{0} 제작 시작 · {1}", DECO[id].n, TimeText(CraftSeconds(DECO[id])))); After();
        }

        void DoClaim(int i)
        {
            var r = CraftClaim(i);
            if (r == null) { Snd.Err(); return; }
            Snd.Record();
            App.Notify(r.Value.isNew ? Loc.F("{0} 완성!", r.Value.d.n) : Loc.F("{0} 완성! · 강화 재료 +1", r.Value.d.n));
            After();
        }

        void DoRush(int i)
        {
            if (!CraftRush(i))
            {
                Snd.Err();
                bool done = i >= 0 && i < G.farm.crafts.Count && RushCost(G.farm.crafts[i]) <= 0;
                App.Notify(done ? Loc.T("이미 완성") : RushLeftToday() <= 0 ? Loc.T("오늘 즉시 완료 소진") : Loc.T("균사석 부족"));
                Render(); return;
            }
            Snd.Buy(); After();
        }

        // ===== 꾸미기: 아름다움 / 장식 슬롯 / 세트 / 보유 장식 =====
        void TabDeco()
        {
            double b = Beauty();
            UIUtil.SetText(title, Loc.T("꾸미기"));
            var nextStep = BEAUTY.FirstOrDefault(s => b < s.at);
            Section(Loc.T("아름다움"), Loc.T("보유 장식 합계"));
            NextBar(nextStep == null ? 1 : b / nextStep.at, nextStep == null ? Loc.F("{0}점 · 최대", U.Fmt(b)) : Loc.F("{0}/{1}점", U.Fmt(b), nextStep.at));
            string got = string.Join(" · ", BEAUTY.Where(s => b >= s.at).Select(s => s.text));
            Line(Loc.F("<b>아름다움 {0}점</b>", U.Fmt(b)) + (got.Length > 0 ? "\n" + Small("<color=#2a9a74>" + got + "</color>") : "")
                + (nextStep != null ? "\n" + Small(Gray(Loc.F("다음 {0}점: {1}", nextStep.at, nextStep.text))) : ""), Ico("f_beauty", "star"), BG);

            DecoSlotsEnsure();
            int n = DecoSlotCount();
            Section(Loc.T("장식 슬롯"), Loc.F("{0}칸", n));
            for (int i = 0; i < n; i++)
            {
                string id = G.farm.decoSlots[i]; var d = id != null ? DECO[id] : null; int k = i;
                bool hang = DecoSlotHang(i);
                bool any = DECOS.Any(x => x.hang == hang && DecoOwned(x.id));
                string where = hang ? Loc.T("걸이형 · 가지·수관") : Loc.T("설치형 · 뿌리 주변");
                string text = d != null
                    ? Loc.F("<b>{0}</b> <color=#e8a900>★{1}</color>", d.n, DecoStar(id)) + "  " + Small(Gray(where)) + "\n" + Small("<color=#2a9a74>" + DecoFxText(d, DecoMul(id)) + "</color>")
                    : Loc.F("<b>슬롯 {0}</b>", i + 1) + "  " + Small(Gray(where)) + "\n" + Small(Gray(any ? Loc.T("빈 슬롯") : Loc.T("보유 장식 없음 · 공방에서 제작")));
                var place = new B(Loc.T("바꾸기 ▶"), () => DoPlace(k, true), any, "wood");
                if (d != null) Line(text, SpriteDB.Get("Farm/Props/" + d.art), i == selDeco ? BG_ON : BG, place, new B(Loc.T("비우기"), () => DoPlace(k, false), true, "wood"));
                else Line(text, Ico("f_slot", "lock"), i == selDeco ? BG_ON : BG_OFF, place);   // 빈 슬롯엔 비우기 버튼이 없다
            }

            var owned = KIDS.Where(k => HasSpecial(k.id)).ToList();
            Section(Loc.T("세트 효과"), Loc.F("같은 꼬마 장식 3종: 수확 +{0} · 특별 보너스 +1단계", U.Pct(DECO_SET_YIELD)));
            if (owned.Count > 0)
                Line(Small(string.Join("   ", owned.Select(k =>
                {
                    int placed = DECOS.Count(d => d.of == k.id && DecoPlaced(d.id));
                    return (placed >= 3 ? "<color=#2a9a74>● " : "<color=#8a6a4a>· ") + SPC[k.id].n + " " + placed + "/3</color>";
                }))), Ico("f_set", "medal"), BG);

            var mine = DECOS.Where(x => DecoOwned(x.id)).ToList();
            Section(Loc.T("보유 장식"), Loc.F("{0}종", mine.Count));
            if (mine.Count == 0) Line(Small(Gray(Loc.T("보유 장식 없음 · 공방에서 제작"))), Ico("f_craft", "hammer"), BG_OFF);
            foreach (var d in mine)
            {
                var c = EnhanceCost(d.id); var f = CanEnhance(d.id); string id = d.id;
                string text = Loc.F("<b>{0}</b> <color=#e8a900>{1}</color>", d.n, new string('★', DecoStar(d.id))) + "  " + Small(Gray(Loc.F("재료 {0}", DecoDup(d.id)) + (DecoPlaced(d.id) ? " · " + Loc.T("배치 중") : "")))
                    + "\n" + Small(DecoFxText(d, DecoMul(d.id)));
                string cost = c != null ? " " + G_(c.Value.gold) + (c.Value.gem > 0 ? " " + Gem(c.Value.gem) : "") : "";
                Line(text, SpriteDB.Get("Farm/Props/" + d.art), BG,
                    new B(f == EnhFail.Max ? Loc.T("★ 최대") : f == EnhFail.Dup ? Loc.T("재료 없음") : Loc.F("강화 ★{0}", DecoStar(d.id) + 1) + cost, () => DoEnhance(id), f == EnhFail.None, "go"));
            }
        }

        void DoPlace(int slot, bool place)
        {
            if (slot < 0 || slot >= G.farm.decoSlots.Count) return;
            string cur = G.farm.decoSlots[slot], next = null;
            if (place)
            {
                // 이 슬롯에 맞는 가진 장식을 차례로 (다른 슬롯에 있던 것은 옮겨 온다)
                var cand = new List<string>();
                foreach (var d in DECOS) if (d.hang == DecoSlotHang(slot) && DecoOwned(d.id)) cand.Add(d.id);
                if (cand.Count == 0) { Snd.Err(); return; }
                next = cand[(cand.IndexOf(cur) + 1) % cand.Count];
            }
            if (!PlaceDeco(slot, next, out bool first)) { Snd.Err(); return; }
            Snd.Ui(); selDeco = slot;
            if (first) { Snd.Record(); App.Notify(Loc.F("첫 배치! {0} · {1}+{2} · {3} 호감도 +{4}", DECO[next].n, UIUtil.Ic("gem"), DECO_FIRST_GEM, SPC[DECO[next].of].n, AFF.placeGift)); }
            if (next != null && KidSetDone(DECO[next].of)) App.Notify(Loc.F("{0} 장식 세트 완성! 수확량 +{1} · 특별 보너스 +1단계", SPC[DECO[next].of].n, U.Pct(DECO_SET_YIELD)));
            if (farm != null) farm.Sync(false);
            After();
        }

        void DoEnhance(string id)
        {
            if (!Enhance(id)) { Snd.Err(); App.Notify(CanEnhance(id) == EnhFail.Dup ? Loc.T("강화 재료 부족 (같은 장식 1개)") : Loc.T("재화 부족")); return; }
            Snd.Record(); App.Notify(Loc.F("{0} ★{1}! {2}", DECO[id].n, DecoStar(id), DecoFxText(DECO[id], DecoMul(id)))); After();
        }
    }
}
