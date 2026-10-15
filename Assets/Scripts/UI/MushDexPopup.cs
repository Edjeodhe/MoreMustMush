using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 버섯 도감 팝업. 배치한 MushDex에 기능만 잇는다 (이름으로 찾음):
    //   Tab/Button_Mush_1~4 = 분류 탭 (식용 · 약용 · 독버섯 · 꼬마)
    //   Mush_Dex_Group 안의 카드들 = 도감 칸. 등록한(처음 수확한) 순서대로 앞 칸부터 채우고, 남는 칸은 비운다.
    //     카드 구성: Image(그림) · Image/Name(이름) · Star ×5(별)
    //     등록한 종이 칸보다 많으면 카드 위에서 마우스 휠로 한 줄씩 내린다
    //   Image = 상세 창 (카드를 누르면 그 버섯)
    //     Text (TMP) 이름 · (1) 등급·분류 · Image 그림 · Start_Group 별 ×5
    //     (2) 첫 제목 · (4) 설명 · (3) 둘째 제목 · (5) 기록 (길면 ScrollBar·마우스 휠로 내린다)
    // 다시 그리기는 팝업을 열 때 · 탭·카드를 누를 때 · 언어를 바꿀 때만.
    public class MushDexPopup
    {
        const string KID_TAB = "kid";                                        // 꼬마 탭 (특수 버섯)
        static readonly string[] TABS = { "ed", "md", "ps", KID_TAB };
        static readonly string[] TAB_NAMES = { "식용", "약용", "독버섯", "꼬마" };
        const float SCROLL_STEP = 60;     // 상세 창 휠 한 칸에 내려가는 거리
        const float SCROLL_PAD = 16;      // 상세 창 아래쪽 여백
        const float MASK_RIGHT = -40;     // 상세 창 마스크를 오른쪽으로 넓혀 바깥에 붙은 스크롤바는 가리지 않게
        static readonly Color STAR_OFF = new Color32(0xd8, 0xcc, 0xb4, 0xff);
        static readonly Color CARD_SEL = new Color32(0xff, 0xf3, 0xb0, 0xff);

        public readonly GameObject root;
        readonly RectTransform rootRt;
        readonly Button[] tabs = new Button[TABS.Length];
        readonly TMP_Text[] tabTexts = new TMP_Text[TABS.Length];
        readonly List<Card> cards = new List<Card>();
        readonly Detail detail;
        readonly List<string> ids = new List<string>();   // 지금 탭에 등록된 것 (등록 순)

        int tab;
        int row;              // 카드 맨 위 줄이 몇 번째 줄인가
        int cols = 1;         // 카드 한 줄 칸 수
        string selected;
        bool dirty;           // 언어가 바뀌었다 — 언어 폰트가 바뀐 뒤(다음 프레임) 다시 그린다
        readonly Camera cam;

        public MushDexPopup(GameObject root)
        {
            this.root = root;
            rootRt = root.transform as RectTransform;
            var canvas = root.GetComponentInParent<Canvas>(true);
            cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            for (int i = 0; i < TABS.Length; i++)
            {
                int k = i;
                var b = SceneUI.Need(root.transform, "Button_Mush_" + (i + 1));
                tabs[i] = SceneUI.Bind(b, () => { Snd.Ui(); SetTab(k); });
                tabTexts[i] = SceneUI.TextIn(b);
            }
            var group = SceneUI.Need(root.transform, "Mush_Dex_Group");
            if (group != null)
            {
                foreach (Transform t in group) cards.Add(new Card(this, t));
                // 화면에 놓인 순서 (위 → 아래, 왼쪽 → 오른쪽) = 채우는 순서
                cards.Sort((a, b) => Mathf.Abs(a.Y - b.Y) > 1 ? b.Y.CompareTo(a.Y) : Mathf.Abs(a.X - b.X) > 1 ? a.X.CompareTo(b.X) : a.Index.CompareTo(b.Index));
                cols = 0;
                while (cols < cards.Count && Mathf.Abs(cards[cols].Y - cards[0].Y) <= 1) cols++;
                cols = Mathf.Max(1, cols);
            }
            var panel = root.transform.Find("Image");
            if (panel == null) Debug.LogWarning("[UI] MushDex 안에서 상세 창 'Image'를 찾지 못했어요");
            else detail = new Detail(panel);
        }

        public bool IsOpen => root != null && root.activeSelf;

        // 팝업을 열 때 (MainGameController가 부른다)
        public void Open()
        {
            detail?.PrepareScroll();
            SetTab(tab);
        }

        void SetTab(int i)
        {
            tab = i; row = 0;
            Collect();
            if (selected == null || !ids.Contains(selected)) selected = ids.Count > 0 ? ids[0] : null;
            Render();
        }

        // 지금 탭에서 등록한 것을 등록 순으로
        void Collect()
        {
            ids.Clear();
            if (G == null) return;
            if (TABS[tab] == KID_TAB)
            {
                // 꼬마는 세계수 단계 순서대로 합류하니 그 순서가 곧 등록 순서 (아직 얻을 수 없는 꼬마는 맨 뒤)
                var owned = new List<Special>();
                foreach (var k in SPECIALS) if (HasSpecial(k.id)) owned.Add(k);
                owned.Sort((a, b) => JoinOrder(a.id).CompareTo(JoinOrder(b.id)));
                foreach (var k in owned) ids.Add(k.id);
                return;
            }
            var list = new List<Species>();
            foreach (var sp in SPECIES) if (sp.c == TABS[tab] && Harvested(sp.id)) list.Add(sp);
            // 처음 수확한 라운드 순, 같은 라운드면 도감 번호 순
            list.Sort((a, b) =>
            {
                int c = G.codex[a.id].first.CompareTo(G.codex[b.id].first);
                return c != 0 ? c : SPECIES.IndexOf(a).CompareTo(SPECIES.IndexOf(b));
            });
            foreach (var sp in list) ids.Add(sp.id);
        }

        static int JoinOrder(string id) { int lv = JoinLv(id); return lv > 0 ? lv : int.MaxValue; }

        bool IsKid => TABS[tab] == KID_TAB;

        public void Render()
        {
            if (G == null || !IsOpen) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].interactable = i != tab;   // 지금 탭은 눌린 모양
                UIUtil.SetText(tabTexts[i], Loc.T(TAB_NAMES[i]));
            }
            RenderCards();
            detail?.Render(selected, IsKid);
        }

        void RenderCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                int n = row * cols + i;
                cards[i].Render(n < ids.Count ? ids[n] : null, IsKid, selected);
            }
        }

        void Select(string id)
        {
            if (id == null || id == selected) return;
            Snd.Ui();
            selected = id;
            RenderCards();
            detail?.Render(selected, IsKid);
        }

        // 언어를 바꿨을 때. 글 길이 재기는 언어 폰트가 바뀐 뒤여야 해서 다음 Tick에서 그린다
        public void LangChanged() => dirty = true;

        // 열려 있는 동안 매 프레임 (마우스 휠: 상세 창 위면 상세 글, 아니면 카드 줄)
        public void Tick()
        {
            if (!IsOpen) return;
            if (dirty) { dirty = false; Render(); }
            var mouse = Mouse.current;
            if (mouse == null) return;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;
            int dir = wheel > 0 ? -1 : 1;   // 휠을 아래로 굴리면 내려간다
            var pos = mouse.position.ReadValue();
            if (detail != null && detail.CanScroll && detail.Contains(pos, cam)) { detail.ScrollBy(dir * SCROLL_STEP); return; }
            if (!RectTransformUtility.RectangleContainsScreenPoint(rootRt, pos, cam)) return;
            int rows = Mathf.CeilToInt(ids.Count / (float)cols);
            int maxRow = Mathf.Max(0, rows - Mathf.CeilToInt(cards.Count / (float)cols));
            int next = Mathf.Clamp(row + dir, 0, maxRow);
            if (next == row) return;
            row = next;
            RenderCards();
        }

        // 그림: 일반 버섯은 한 송이 그림, 꼬마는 캐릭터 그림. 황금이면 황금 효과
        static void SetIcon(Image img, string id, bool kid)
        {
            if (img == null) return;
            if (id == null) { UIUtil.SetImage(img, null); return; }
            bool gold = !kid && G.codex.TryGetValue(id, out var e) && e.gold;
            UIUtil.SetImage(img, kid ? SpriteDB.Get($"Characters/Critters/{id}_ref") : SpriteDB.Single(id), gold ? 3 : 0);
            img.color = Color.white;   // 배치할 때 넣은 검은 자리 표시 색을 지운다
        }

        static void SetStars(Image[] stars, Color on, int s)
        {
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < s ? on : STAR_OFF;
        }

        static Image[] StarsIn(Transform parent)
        {
            var list = new List<Image>();
            foreach (Transform c in parent)
                if (c.name.StartsWith("Star") && c.TryGetComponent<Image>(out var img)) list.Add(img);
            list.Sort((a, b) => a.rectTransform.anchoredPosition.x.CompareTo(b.rectTransform.anchoredPosition.x));
            return list.ToArray();
        }

        // ===== 도감 칸 하나 =====
        class Card
        {
            readonly RectTransform rt;
            readonly Image bg, icon;
            readonly TMP_Text name;
            readonly Image[] stars;
            readonly Color bgColor, starColor;
            readonly Button button;
            string id;

            public float X => rt.anchoredPosition.x;
            public float Y => rt.anchoredPosition.y;
            public int Index => rt.GetSiblingIndex();

            public Card(MushDexPopup dex, Transform t)
            {
                rt = (RectTransform)t;
                bg = t.GetComponent<Image>();
                bgColor = bg != null ? bg.color : Color.white;
                icon = t.Find("Image")?.GetComponent<Image>();
                name = SceneUI.Need(t, "Name")?.GetComponent<TMP_Text>();
                stars = StarsIn(t);
                starColor = stars.Length > 0 ? stars[0].color : Color.yellow;
                // 카드는 배치할 때 버튼이 없어서 누름만 받게 붙인다 (모양은 그대로)
                if (!t.TryGetComponent(out button)) { button = t.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None; }
                button.onClick.AddListener(() => dex.Select(id));
            }

            public void Render(string id, bool kid, string selected)
            {
                this.id = id;
                button.interactable = id != null;
                if (bg != null) bg.color = id != null && id == selected ? CARD_SEL : bgColor;
                SetIcon(icon, id, kid);
                UIUtil.SetText(name, id == null ? "" : kid ? SPC[id].n : SP[id].n);
                bool showStars = id != null && !kid;   // 꼬마는 별(수확 등급)이 없다
                foreach (var s in stars) UIUtil.Show(s, showStars);
                if (showStars) SetStars(stars, starColor, StarOf(id));
            }
        }

        // ===== 상세 창 =====
        class Detail
        {
            readonly RectTransform panel;
            readonly TMP_Text title, sub, head1, head2, body1, body2;
            readonly Image icon;
            readonly Image[] stars;
            readonly Color starColor;
            readonly Scrollbar bar;
            readonly List<RectTransform> content = new List<RectTransform>();   // 스크롤로 함께 움직이는 것 (스크롤바 빼고 전부)
            readonly List<Vector2> basePos = new List<Vector2>();
            readonly float body2Top, body2BaseH;
            float overflow, offset;

            public Detail(Transform t)
            {
                panel = (RectTransform)t;
                title = Text(t, "Text (TMP)");
                sub = Text(t, "Text (TMP) (1)");
                head1 = Text(t, "Text (TMP) (2)");
                head2 = Text(t, "Text (TMP) (3)");
                body1 = Text(t, "Text (TMP) (4)");
                body2 = Text(t, "Text (TMP) (5)");
                icon = t.Find("Image")?.GetComponent<Image>();
                var sg = SceneUI.Need(t, "Start_Group");
                stars = sg != null ? StarsIn(sg) : new Image[0];
                starColor = stars.Length > 0 ? stars[0].color : Color.yellow;
                bar = SceneUI.Need(t, "ScrollBar")?.GetComponent<Scrollbar>();
                if (bar != null) bar.onValueChanged.AddListener(v => Apply(v * overflow));
                foreach (Transform c in t)
                    if (bar == null || c != bar.transform) { content.Add((RectTransform)c); basePos.Add(((RectTransform)c).anchoredPosition); }
                if (body2 != null) { var r = body2.rectTransform; body2BaseH = r.rect.height; body2Top = r.anchoredPosition.y + body2BaseH * (1 - r.pivot.y); }
                // 아래로 넘친 글은 창 안에서만 보이게 (배치한 창에 가리개 컴포넌트만 붙인다)
                if (!t.TryGetComponent<RectMask2D>(out var mask)) mask = t.gameObject.AddComponent<RectMask2D>();
                mask.padding = new Vector4(0, 0, MASK_RIGHT, 0);
            }

            static TMP_Text Text(Transform t, string n)
            {
                var c = t.Find(n);
                if (c == null) Debug.LogWarning($"[UI] 상세 창에서 '{n}'을(를) 찾지 못했어요");
                return c != null ? c.GetComponent<TMP_Text>() : null;
            }

            // 마지막 글(기록)은 줄이지 않고 길이만큼 늘여서 스크롤로 본다. TextFit이 씬을 연 뒤 켠 자동 크기를 여기서 끈다
            public void PrepareScroll()
            {
                if (body2 == null || !body2.enableAutoSizing) return;
                body2.enableAutoSizing = false;
                body2.fontSize = body2.fontSizeMax;
                body2.overflowMode = TextOverflowModes.Overflow;
            }

            public bool CanScroll => overflow > 0;

            public bool Contains(Vector2 screen, Camera cam) => RectTransformUtility.RectangleContainsScreenPoint(panel, screen, cam);

            public void ScrollBy(float dy)
            {
                if (overflow <= 0) return;
                float v = Mathf.Clamp(offset + dy, 0, overflow);
                if (bar != null) bar.value = v / overflow;   // onValueChanged가 Apply한다
                else Apply(v);
            }

            void Apply(float v)
            {
                offset = Mathf.Clamp(v, 0, overflow);
                for (int i = 0; i < content.Count; i++) content[i].anchoredPosition = basePos[i] + new Vector2(0, offset);
            }

            public void Render(string id, bool kid)
            {
                if (id == null) Empty();
                else if (kid) RenderKid(SPC[id]);
                else RenderSpecies(SP[id]);
                Relayout();
            }

            void Empty()
            {
                SetIcon(icon, null, false);
                foreach (var s in stars) UIUtil.Show(s, false);
                foreach (var t in new[] { title, sub, body1, body2 }) UIUtil.SetText(t, "");
                Headings();
            }

            void Headings()
            {
                UIUtil.SetText(head1, Loc.T("설명"));
                UIUtil.SetText(head2, Loc.T("기록"));
            }

            void RenderSpecies(Species sp)
            {
                var e = G.codex[sp.id];
                int s = StarOf(sp.id);
                SetIcon(icon, sp.id, false);
                foreach (var st in stars) UIUtil.Show(st, true);
                SetStars(stars, starColor, s);
                UIUtil.SetText(title, sp.n);
                UIUtil.SetText(sub, $"{TIERS[sp.t].name} · {CATS[sp.c].name}");
                Headings();

                string trait = sp.jelly ? TRAIT_TEXT["jelly"] : sp.spore != null ? TRAIT_TEXT[sp.spore] : sp.wander ? Loc.F("{0} · 항상 떠돌이", TRAIT_TEXT["wander"]) : "";
                UIUtil.SetText(body1, trait != "" ? sp.d + "\n" + Loc.F("특성: {0}", trait) : sp.d);

                double per = AbPerStar(sp);
                string next = s < 5 ? Loc.F("다음 별까지 {0}개 수확", U.Fmt(STAR_N[s] - e.n)) : Loc.T("최대 5성");
                string ab = Loc.F("★ 별 능력: <b>{0}</b> 별마다 {1} · 지금 {2}", AB[sp.ab].n, AbFmt(sp.ab, per), AbFmt(sp.ab, per * s))
                    + (s < 5 ? " " + Loc.F("(5성 {0})", AbFmt(sp.ab, per * 5)) : "");
                string rec = Loc.F("처음 딴 라운드 {0} · 누적 수확 {1}개 · 창고 {2}개 · 황금 {3} · 거대 {4}", e.first, U.Fmt(e.n), U.Fmt(StoreOf(sp.id)), e.gold ? "○" : "×", e.giant ? "○" : "×");
                UIUtil.SetText(body2, next + "\n\n" + ab + "\n\n" + rec + "\n\n" + UnlockText(sp));
            }

            void RenderKid(Special k)
            {
                SetIcon(icon, k.id, true);
                foreach (var st in stars) UIUtil.Show(st, false);
                UIUtil.SetText(title, k.n);
                UIUtil.SetText(sub, Loc.T("특수 버섯"));
                Headings();
                UIUtil.SetText(body1, Loc.T("숲을 깡총깡총 뛰어다니던 캐릭터 버섯. 지금은 세계수에서 나무버섯을 따고 있어요."));
                // 특성 글이 곧 능력 설명이라 따로 덧붙이지 않는다 (세계수에 없는 꼬마는 능력 대신 안내 문구)
                UIUtil.SetText(body2, KID.TryGetValue(k.id, out var kd) ? Loc.F("보유 중 · Lv.{0} ★{1} · 특성: {2}", KLv(k.id), Awaken(k.id), kd.traitText) : k.perk);
            }

            // 기록 글 길이에 맞춰 칸을 늘이고, 창 아래로 넘친 만큼 스크롤 범위를 잡는다 (맨 위로 돌아간다)
            void Relayout()
            {
                float bottom = 0;
                if (body2 != null)
                {
                    var r = body2.rectTransform;
                    float h = Mathf.Max(body2BaseH, body2.GetPreferredValues(body2.text, r.rect.width, 0).y);
                    r.sizeDelta = new Vector2(r.sizeDelta.x, r.sizeDelta.y + (h - r.rect.height));
                    int i = content.IndexOf(r);
                    var p = new Vector2(basePos[i].x, body2Top - h * (1 - r.pivot.y));
                    basePos[i] = p;
                    // anchoredPosition은 앵커 기준이라 창 좌표(피벗 기준)로 옮긴다
                    bottom = body2Top - h + Mathf.Lerp(panel.rect.yMin, panel.rect.yMax, r.anchorMin.y);
                }
                float panelBottom = panel.rect.yMin + SCROLL_PAD;
                overflow = Mathf.Max(0, panelBottom - bottom);
                if (bar != null)
                {
                    bar.size = overflow > 0 ? panel.rect.height / (panel.rect.height + overflow) : 1;
                    bar.interactable = overflow > 0;
                    bar.SetValueWithoutNotify(0);
                }
                Apply(0);
            }
        }
    }
}
