using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 공방 창 "작은 제작소" (2026-10-20 시안 concept/ui_mock_workshop.png). 프리팹 Resources/UI/WorkshopWindow:
    //   Box / Title · Exit · Info
    //   Recipes / Header · Scroll / Content / Recipe (Name · Pic/Thumb · Desc/Text · MatTitle · Mats/Mat0~2(Icon · Count) · Button_Craft)
    //   Slots / Header · Scroll / Content / Slot (Num/Text · Pic/Thumb · Lock · Name · Bar(Fill) · Time · Status · Done/Text · Button_Rush · Button_Claim · Button_Unlock)
    // 재료 = 골드 · 균사석 · 시간(가진 양/필요 양). 규칙은 Game.Farm.Deco (CanCraft·CraftStart·CraftClaim·CraftRush). 슬롯은 세계수 단계로 열린다.
    public class WorkshopWindow
    {
        const int MAX_SLOTS = 5;
        public readonly GameObject root;
        readonly System.Action changed;
        readonly TMP_Text info;
        readonly Transform recipe, slot;
        readonly List<Transform> recipes = new List<Transform>(), slots = new List<Transform>();

        public bool IsOpen => root != null && root.activeSelf;

        public WorkshopWindow(GameObject root, System.Action changed)
        {
            this.root = root; this.changed = changed;
            if (root == null) return;
            SceneUI.Bind(SceneUI.Find(root.transform, "Exit"), () => { Snd.Ui(); Close(); });
            info = FarmWin.Text(root.transform, "Info");
            recipe = SceneUI.Need(root.transform, "Recipe");
            slot = SceneUI.Need(root.transform, "Slot");
        }

        public void Open() { if (root == null) return; root.SetActive(true); root.transform.SetAsLastSibling(); Render(); }
        public void Close() { if (!IsOpen) return; root.SetActive(false); changed?.Invoke(); }
        public void Refresh() { if (IsOpen) Render(); }
        public void Tick() { if (IsOpen && G?.farm != null) RenderSlots(); }   // 남은 시간 (1초마다)
        void After() { Render(); changed?.Invoke(); }

        // 만들 수 있는 것 → 재화가 모자란 것 → 레시피가 잠긴 것
        static List<Deco> Order() => DECOS.OrderBy(d => !RecipeOpen(d) ? 2 : CanCraft(d.id) == CraftFail.None ? 0 : 1).ThenBy(d => d.grade).ToList();

        void Render()
        {
            if (G?.farm == null) return;
            bool open = CraftOpen();
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(root.transform, "Title"), "Text"), Loc.T("작은 제작소"));
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(SceneUI.Find(root.transform, "Recipes"), "Header"), "Text"), Loc.T("제작 목록"));
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(SceneUI.Find(root.transform, "Slots"), "Header"), "Text"), Loc.T("제작 슬롯"));
            UIUtil.SetText(info, open ? Loc.F("제작 {0}/{1} · 즉시 완료 오늘 {2}회", G.farm.crafts.Count, WT.CraftSlots(Stage()), RushLeftToday())
                : Loc.F("도감 {0}/{1}종을 모으면 꼬마 공방이 열려요", CodexCount(), WT.codexForCraft));
            var list = Order();
            FarmWin.Grow(recipes, recipe, list.Count);
            for (int i = 0; i < list.Count; i++) Recipe(recipes[i], list[i], open);
            RenderSlots();
        }

        void Recipe(Transform t, Deco d, bool open)
        {
            bool rec = RecipeOpen(d);
            var g = DECO_GRADES[d.grade];
            UIUtil.SetText(FarmWin.Text(t, "Name"), rec ? d.n : "???");
            var th = FarmWin.Img(SceneUI.Find(t, "Pic"), "Thumb");
            if (th != null) { th.sprite = SpriteDB.Get("Farm/Props/" + d.art); th.preserveAspect = true; th.color = rec ? Color.white : new Color(0.2f, 0.18f, 0.16f, 0.6f); }
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(t, "Desc"), "Text"), rec ? DecoFxText(d) : Loc.F("{0} 합류 시 해금", SPC[d.of].n));
            UIUtil.SetText(FarmWin.Text(t, "MatTitle"), Loc.T("필요 재료"));
            var mats = SceneUI.Find(t, "Mats");
            Mat(mats, 0, SpriteDB.Icon("gold"), U.Fmt(G.gold), U.Fmt(g.gold), G.gold >= g.gold);
            Mat(mats, 1, SpriteDB.Icon("gem"), U.Fmt(G.gem), U.Fmt(g.gem), G.gem >= g.gem);
            Mat(mats, 2, SpriteDB.Icon("clock"), null, TimeText(CraftSeconds(d)), true);
            var f = open ? CanCraft(d.id) : CraftFail.Closed;
            var b = SceneUI.Find(t, "Button_Craft")?.GetComponent<Button>();
            if (b != null)
            {
                b.onClick.RemoveAllListeners(); string id = d.id;
                b.onClick.AddListener(() => Craft(id));
                FarmWin.Btn(b, f == CraftFail.None ? Loc.T("제작") : FailText(f), f == CraftFail.None, f == CraftFail.None ? "btn_leaf" : "btn_cream");
            }
        }

        // 재료 칸: 아이콘 + "가진/필요" (모자라면 빨강). have = null이면 필요 양만 (시간)
        static void Mat(Transform mats, int i, Sprite icon, string have, string need, bool ok)
        {
            var m = mats != null ? mats.Find("Mat" + i) : null;
            if (m == null) return;
            var im = FarmWin.Img(m, "Icon"); if (im != null) { im.sprite = icon; im.preserveAspect = true; }
            UIUtil.SetText(FarmWin.Text(m, "Count"), have == null ? need : FarmWin.Col(have, ok ? FarmWin.GREEN : FarmWin.RED) + "<size=80%>/" + need + "</size>");
        }

        void RenderSlots()
        {
            FarmWin.Grow(slots, slot, MAX_SLOTS);
            int open = WT.CraftSlots(Stage());
            for (int i = 0; i < MAX_SLOTS; i++) Slot(slots[i], i, i < open);
        }

        void Slot(Transform t, int i, bool open)
        {
            var c = open && i < G.farm.crafts.Count ? G.farm.crafts[i] : null;
            var d = c != null ? DECO[c.deco] : null;
            bool done = CraftDone(c);
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(t, "Num"), "Text"), (i + 1).ToString());
            var th = FarmWin.Img(SceneUI.Find(t, "Pic"), "Thumb");
            if (th != null) { th.enabled = d != null; if (d != null) { th.sprite = SpriteDB.Get("Farm/Props/" + d.art); th.preserveAspect = true; } }
            UIUtil.Show(SceneUI.Find(t, "Lock")?.gameObject, !open);
            UIUtil.SetText(FarmWin.Text(t, "Name"), !open ? Loc.T("잠긴 제작 슬롯") : d != null ? d.n : Loc.T("빈 칸"));
            double left = c != null ? System.Math.Max(0, (c.at - Now()) / 1000) : 0;
            var bar = SceneUI.Find(t, "Bar");
            UIUtil.Show(bar?.gameObject, d != null && !done);
            UIUtil.Show(SceneUI.Find(t, "Time")?.gameObject, d != null && !done);
            if (d != null && !done)
            {
                FarmWin.Bar(bar, (float)System.Math.Clamp((Now() - c.t0) / System.Math.Max(1, c.at - c.t0), 0, 1), null);
                UIUtil.SetText(FarmWin.Text(t, "Time"), Mmss((long)System.Math.Ceiling(left)));
            }
            int unlockAt = Enumerable.Range(1, WT.stages).FirstOrDefault(s => WT.CraftSlots(s) > i);
            UIUtil.SetText(FarmWin.Text(t, "Status"), !open ? Loc.F("세계수 {0}단계에 열려요", unlockAt) : d == null ? Loc.T("레시피를 골라 제작해요") : done ? Loc.T("기다리던 장식이 완성됐어요.") : Loc.T("제작 중…"));
            UIUtil.Show(SceneUI.Find(t, "Done")?.gameObject, done);
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(t, "Done"), "Text"), Loc.T("완료!"));
            var claim = SceneUI.Find(t, "Button_Claim")?.GetComponent<Button>();
            UIUtil.Show(claim?.gameObject, done);
            if (claim != null && done) { claim.onClick.RemoveAllListeners(); claim.onClick.AddListener(() => Claim(i)); FarmWin.Btn(claim, Loc.T("받기"), true, "btn_leaf"); }
            var rush = SceneUI.Find(t, "Button_Rush")?.GetComponent<Button>();
            UIUtil.Show(rush?.gameObject, d != null && !done);
            if (rush != null && d != null && !done)
            {
                int cost = RushCost(c);
                rush.onClick.RemoveAllListeners(); rush.onClick.AddListener(() => Rush(i));
                FarmWin.Btn(rush, Loc.T("가속") + " " + FarmWin.Gem(cost), RushLeftToday() > 0 && G.gem >= cost, "btn_cream");
            }
            var un = SceneUI.Find(t, "Button_Unlock")?.GetComponent<Button>();
            UIUtil.Show(un?.gameObject, !open);
            if (un != null && !open)
            {
                un.onClick.RemoveAllListeners();
                un.onClick.AddListener(() => { Snd.Err(); App.Notify(Loc.F("세계수가 {0}단계가 되면 제작 슬롯이 열려요", unlockAt)); });
                FarmWin.Btn(un, Loc.T("해금"), true, "btn_cream");
            }
        }

        void Craft(string id)
        {
            var f = CanCraft(id);
            if (f != CraftFail.None) { Snd.Err(); App.Notify(FailText(f)); return; }
            CraftStart(id); Snd.Buy(); After();
        }

        void Claim(int i)
        {
            var r = CraftClaim(i);
            if (r == null) { Snd.Err(); return; }
            Snd.Record();
            App.Notify(r.Value.isNew ? Loc.F("{0} 완성! 꾸미기에서 세계수에 놓아요", r.Value.d.n) : Loc.F("{0} 완성! 강화 재료로 보관했어요", r.Value.d.n));
            After();
        }

        void Rush(int i)
        {
            if (!CraftRush(i)) { Snd.Err(); App.Notify(RushLeftToday() <= 0 ? Loc.T("오늘 즉시 완료를 다 썼어요") : Loc.T("균사석이 부족해요")); return; }
            Snd.Buy(); After();
        }

        static string FailText(CraftFail f) => f switch
        {
            CraftFail.Closed => Loc.F("도감 {0}/{1}종을 모으면 꼬마 공방이 열려요", CodexCount(), WT.codexForCraft),
            CraftFail.Recipe => Loc.T("꼬마가 합류하면 열려요"),
            CraftFail.Slots => Loc.T("슬롯 없음"),
            CraftFail.Gold => Loc.T("골드 부족"),
            CraftFail.Gem => Loc.T("균사석 부족"),
            _ => "",
        };
    }
}
