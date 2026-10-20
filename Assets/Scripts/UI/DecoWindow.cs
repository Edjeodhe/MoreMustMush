using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 꾸미기 창 (2026-10-20, 쿠키런 킹덤 꾸미기 참고): 화면 아래 판 하나 — 위에 구역 탭 4개(뿌리·줄기·가지·수관)를 가로로 펼치고,
    // 탭을 누르면 그 구역에 놓이는 장식이 아래 줄에 카드로 가로로 늘어선다. 나무는 위쪽에 그대로 보이고, 고른 구역으로 화면이 다가간다.
    // 장식의 구역 = 지금 단계 도면에서 그 장식 자리의 높이 (FarmTreeView.DecoZone). 규칙(슬롯·배치·강화)은 Game.Farm.Deco 그대로:
    // 배치하면 그 종류(설치형·걸이형)의 빈 슬롯에 들어가고, 빈 슬롯이 없으면 알려 준다.
    //
    // 프리팹 Resources/UI/DecoPanel (Unity MCP로 만든 것, 이름으로 찾음):
    //   Sheet / Tabs / Tab              구역 탭 템플릿 1장 → 구역 수만큼 복제. 안: Icon(RawImage, 나무 그림의 그 구역 부분) · Label · Count (TMP)
    //   Sheet / Info (TMP)              아름다움 · 설치형/걸이형 자리
    //   Sheet / Exit                    닫기
    //   Sheet / Scroll / Content / Card 장식 카드 템플릿 1장 → 장식 수만큼 복제
    //     카드 안: Thumb(Image) · Badge(배치 중) · Name · Stars · Kind · Effect (TMP) · Button_Main · Button_Sub (글자 포함)
    //   Sheet / Empty (TMP)             그 구역에 놓을 장식이 없을 때
    public class DecoWindow
    {
        const string PREFAB = "UI/DecoPanel";
        static readonly Color ON = Color.white, DIM = new Color(1, 1, 1, 0.45f);

        public readonly GameObject root;
        readonly System.Action changed;
        FarmView farm;
        readonly TMP_Text info, empty;
        readonly ScrollRect scroll;
        readonly List<ZoneTab> tabs = new List<ZoneTab>();
        readonly List<Card> cards = new List<Card>();
        string zone = "root";

        public bool IsOpen => root != null && root.activeSelf;

        public static DecoWindow For(UnityEngine.SceneManagement.Scene scene, System.Action changed)
        {
            var canvas = TempDialog.RootCanvas(scene);
            var prefab = Resources.Load<GameObject>(PREFAB);
            if (canvas == null || prefab == null) { Debug.LogWarning("[Deco] 꾸미기 창 프리팹(Resources/" + PREFAB + ") 또는 캔버스가 없어요"); return null; }
            var go = Object.Instantiate(prefab, canvas.transform, false);
            go.name = prefab.name;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) TextLangFont.Register(t);
            return new DecoWindow(go, changed);
        }

        DecoWindow(GameObject root, System.Action changed)
        {
            this.root = root;
            this.changed = changed;
            info = SceneUI.TextIn(SceneUI.Find(root.transform, "Info"));
            empty = SceneUI.TextIn(SceneUI.Find(root.transform, "Empty"));
            scroll = root.GetComponentInChildren<ScrollRect>(true);
            SceneUI.Bind(SceneUI.Find(root.transform, "Exit"), () => { Snd.Ui(); Close(); });

            var tab = SceneUI.Need(root.transform, "Tab");
            if (tab != null)
                for (int i = 0; i < ZONES.Length; i++)
                    tabs.Add(new ZoneTab(this, i == 0 ? tab : Clone(tab, i), ZONES[i].id));
            var card = SceneUI.Need(root.transform, "Card");
            if (card != null)
                for (int i = 0; i < DECOS.Length; i++)
                    cards.Add(new Card(this, i == 0 ? card : Clone(card, i)));
            root.SetActive(false);
        }

        static Transform Clone(Transform t, int i)
        {
            var c = Object.Instantiate(t.gameObject, t.parent, false).transform;
            c.name = t.name + " " + i;
            foreach (var x in c.GetComponentsInChildren<TMP_Text>(true)) TextLangFont.Register(x);
            return c;
        }

        public void SetFarm(FarmView f) => farm = f;

        public void Open()
        {
            if (root == null) return;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Pick(zone, false);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            farm?.Unfocus();
            changed?.Invoke();
        }

        public void Refresh() { if (IsOpen) Render(); }

        void Pick(string z, bool sound)
        {
            if (sound) Snd.Ui();
            zone = z;
            farm?.FocusZone(z);
            Render();
            if (scroll != null) scroll.horizontalNormalizedPosition = 0;
        }

        // 구역의 장식: 놓인 것 → 가진 것 → 아직 없는 것, 같은 무리에서는 정의 순서
        List<Deco> InZone(string z) => DECOS.Where(d => FarmTreeView.DecoZone(d) == z)
            .OrderBy(d => DecoPlaced(d.id) ? 0 : DecoOwned(d.id) ? 1 : 2).ToList();

        static int Free(bool hang)
        {
            DecoSlotsEnsure();
            int n = 0, all = DecoSlotCount();
            for (int i = 0; i < all; i++) if (DecoSlotHang(i) == hang && G.farm.decoSlots[i] == null) n++;
            return n;
        }
        static int Slots(bool hang) { int n = 0, all = DecoSlotCount(); for (int i = 0; i < all; i++) if (DecoSlotHang(i) == hang) n++; return n; }

        void Render()
        {
            if (G?.farm == null) return;
            UIUtil.SetText(info, Loc.F("아름다움 {0}점", U.Fmt(Beauty())) + "   "
                + Loc.F("설치형 {0}/{1}", Slots(false) - Free(false), Slots(false)) + "  ·  " + Loc.F("걸이형 {0}/{1}", Slots(true) - Free(true), Slots(true)));
            foreach (var t in tabs) t.Render(t.zone == zone, InZone(t.zone));
            var list = InZone(zone);
            for (int i = 0; i < cards.Count; i++) cards[i].Render(i < list.Count ? list[i] : null);
            UIUtil.Show(empty, list.Count == 0);
            if (list.Count == 0) UIUtil.SetText(empty, Loc.F("{0}에 놓이는 장식이 없어요", ZONE[zone].n));
        }

        void After() { Render(); farm?.Sync(false); changed?.Invoke(); }

        void Place(Deco d)
        {
            if (!DecoOwned(d.id)) { Snd.Err(); App.Notify(Loc.T("보유 장식 없음 · 공방에서 제작")); return; }
            DecoSlotsEnsure();
            int slot = -1;
            for (int i = 0; i < DecoSlotCount(); i++) if (DecoSlotHang(i) == d.hang && G.farm.decoSlots[i] == null) { slot = i; break; }
            if (slot < 0) { Snd.Err(); App.Notify(d.hang ? Loc.T("걸이형 자리가 꽉 찼어요 · 다른 장식을 빼 주세요") : Loc.T("설치형 자리가 꽉 찼어요 · 다른 장식을 빼 주세요")); return; }
            if (!PlaceDeco(slot, d.id, out bool first)) { Snd.Err(); return; }
            Snd.Ui();
            if (first) { Snd.Record(); App.Notify(Loc.F("첫 배치! {0} · {1}+{2} · {3} 호감도 +{4}", d.n, UIUtil.Ic("gem"), DECO_FIRST_GEM, SPC[d.of].n, AFF.placeGift)); }
            if (KidSetDone(d.of)) App.Notify(Loc.F("{0} 장식 세트 완성! 수확량 +{1} · 특별 보너스 +1단계", SPC[d.of].n, U.Pct(DECO_SET_YIELD)));
            After();
        }

        void Remove(Deco d)
        {
            int slot = G.farm.decoSlots.IndexOf(d.id);
            if (slot < 0 || !PlaceDeco(slot, null, out _)) { Snd.Err(); return; }
            Snd.Ui(); After();
        }

        void Upgrade(Deco d)
        {
            if (!Enhance(d.id)) { Snd.Err(); App.Notify(CanEnhance(d.id) == EnhFail.Dup ? Loc.T("강화 재료 부족 (같은 장식 1개)") : Loc.T("재화 부족")); return; }
            Snd.Record(); App.Notify(Loc.F("{0} ★{1}! {2}", d.n, DecoStar(d.id), DecoFxText(d, DecoMul(d.id)))); After();
        }

        // ===== 구역 탭: 나무 그림에서 그 구역 높이를 잘라 아이콘으로 · 이름 · 놓인 수/장식 수 =====
        class ZoneTab
        {
            public readonly string zone;
            readonly Image bg; readonly Button btn; readonly RawImage icon; readonly TMP_Text label, count;
            int shownPic = -1;

            public ZoneTab(DecoWindow w, Transform t, string zone)
            {
                this.zone = zone;
                bg = t.GetComponent<Image>();
                btn = SceneUI.Bind(t, () => w.Pick(zone, true));
                icon = SceneUI.Find(t, "Icon")?.GetComponent<RawImage>();
                label = SceneUI.TextIn(SceneUI.Find(t, "Label"));
                count = SceneUI.TextIn(SceneUI.Find(t, "Count"));
            }

            public void Render(bool on, List<Deco> list)
            {
                var sp = SpriteDB.Get(on ? "UI/rail_active" : "UI/rail_item");
                if (bg != null && sp != null && bg.sprite != sp) bg.sprite = sp;
                if (bg != null) bg.transform.localScale = on ? new Vector3(1.06f, 1.06f, 1) : Vector3.one;
                UIUtil.SetText(label, ZONE[zone].n);
                if (label != null) label.color = on ? Color.white : new Color(0.36f, 0.24f, 0.15f);
                int placed = list.Count(d => DecoPlaced(d.id));
                UIUtil.SetText(count, list.Count == 0 ? "-" : placed + "/" + list.Count);
                if (count != null) count.color = on ? new Color(1, 0.97f, 0.85f) : new Color(0.55f, 0.42f, 0.3f);
                Icon();
            }

            // 지금 단계 나무 그림에서 구역 높이 둘레를 정사각형으로 (구역이 어디인지 그림으로 바로 알게)
            void Icon()
            {
                if (icon == null) return;
                int pic = WT.Picture(Stage());
                if (pic == shownPic) return;
                shownPic = pic;
                var sp = SpriteDB.Get("Farm/Tree/tree_" + pic);
                if (sp == null) { icon.enabled = false; return; }
                icon.enabled = true;
                var tex = sp.texture; var r = sp.textureRect;
                float cy = ZONE[zone].cy, h = Mathf.Min(r.width, r.height) * 0.42f;
                float y = r.y + Mathf.Clamp(cy * r.height * 0.92f + r.height * 0.04f, h / 2, r.height - h / 2) - h / 2;
                float x = r.x + r.width / 2 - h / 2;
                icon.texture = tex;
                icon.uvRect = new Rect(x / tex.width, y / tex.height, h / tex.width, h / tex.height);
            }
        }

        // ===== 장식 카드 =====
        class Card
        {
            readonly DecoWindow w;
            readonly GameObject go; readonly Image bg, thumb; readonly GameObject badge;
            readonly TMP_Text name, stars, kind, effect, mainLabel, subLabel;
            readonly Button main, sub;
            Deco d;

            public Card(DecoWindow w, Transform t)
            {
                this.w = w;
                go = t.gameObject;
                bg = t.GetComponent<Image>();
                thumb = SceneUI.Find(t, "Thumb")?.GetComponent<Image>();
                badge = SceneUI.Find(t, "Badge")?.gameObject;
                name = SceneUI.TextIn(SceneUI.Find(t, "Name"));
                stars = SceneUI.TextIn(SceneUI.Find(t, "Stars"));
                kind = SceneUI.TextIn(SceneUI.Find(t, "Kind"));
                effect = SceneUI.TextIn(SceneUI.Find(t, "Effect"));
                var m = SceneUI.Find(t, "Button_Main"); var s = SceneUI.Find(t, "Button_Sub");
                main = SceneUI.Bind(m, () => { if (d == null) return; if (DecoPlaced(d.id)) w.Remove(d); else w.Place(d); });
                sub = SceneUI.Bind(s, () => { if (d != null) w.Upgrade(d); });
                mainLabel = SceneUI.TextIn(m); subLabel = SceneUI.TextIn(s);
            }

            public void Render(Deco deco)
            {
                d = deco;
                go.SetActive(d != null);
                if (d == null) return;
                bool own = DecoOwned(d.id), placed = DecoPlaced(d.id);
                if (bg != null) { var sp = SpriteDB.Get(placed ? "UI/panel_mint" : "UI/panel_paper"); if (sp != null) bg.sprite = sp; bg.color = own ? ON : new Color(1, 1, 1, 0.75f); }
                if (thumb != null)
                {
                    thumb.sprite = SpriteDB.Get("Farm/Props/" + d.art);
                    thumb.color = own ? ON : new Color(0.25f, 0.22f, 0.2f, 0.55f);   // 아직 없으면 실루엣
                }
                UIUtil.Show(badge, placed);
                UIUtil.SetText(name, d.n);
                UIUtil.SetText(stars, own ? new string('★', DecoStar(d.id)) + "<color=#d9c9b0>" + new string('★', DECO_STAR_MAX - DecoStar(d.id)) + "</color>" : DECO_GRADES[d.grade].n);
                UIUtil.SetText(kind, (d.hang ? Loc.T("걸이형") : Loc.T("설치형")) + " · " + Loc.F("{0} 세트", SPC[d.of].n));
                UIUtil.SetText(effect, own ? DecoFxText(d, DecoMul(d.id)) : Loc.T("공방에서 제작"));
                if (effect != null) effect.color = own ? new Color(0.16f, 0.55f, 0.42f) : new Color(0.6f, 0.47f, 0.35f);

                // 큰 버튼: 놓기(초록) / 빼기(나무) / 미보유(흐림)
                if (main != null)
                {
                    main.interactable = own;
                    var img = main.targetGraphic as Image;
                    var sp = SpriteDB.Get(placed ? "UI/btn_wood" : "UI/btn_go");
                    if (img != null && sp != null) img.sprite = sp;
                    UIUtil.SetText(mainLabel, !own ? Loc.T("미보유") : placed ? Loc.T("빼기") : Loc.T("놓기"));
                    if (mainLabel != null) mainLabel.color = placed || !own ? new Color(0.36f, 0.24f, 0.15f) : new Color(0.13f, 0.33f, 0.13f);
                }
                if (sub != null)
                {
                    var f = own ? CanEnhance(d.id) : EnhFail.Max;
                    sub.gameObject.SetActive(own);
                    sub.interactable = f == EnhFail.None;
                    var c = EnhanceCost(d.id);
                    UIUtil.SetText(subLabel, f == EnhFail.Max ? Loc.T("★ 최대") : f == EnhFail.Dup ? Loc.F("강화 재료 {0}", DecoDup(d.id))
                        : Loc.F("강화 ★{0}", DecoStar(d.id) + 1) + (c != null ? "\n<size=80%>" + UIUtil.Ic("gold") + U.Fmt(c.Value.gold) + (c.Value.gem > 0 ? " " + UIUtil.Ic("gem") + c.Value.gem : "") + "</size>" : ""));
                    if (subLabel != null) { subLabel.textWrappingMode = TextWrappingModes.NoWrap; subLabel.lineSpacing = -18; }   // 두 줄: 강화 ★n / 비용
                }
            }
        }
    }
}
