using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 꼬마 공방(장식 제작) 팝업 — 초안. 규칙은 Game.Farm.Deco.cs(CanCraft·CraftStart·CraftClaim·CraftRush)를 그대로 쓴다.
    // 레시피 해금(꼬마 합류) → 재료 투입(골드·균사석) → 제작 대기(1~4시간, 균사석 즉시 완료) → 완성·수령 → 꾸미기 탭에서 배치.
    //
    // 씬 배치 (이름으로 찾음, OutGame의 Workshop_Panel 아래):
    //   Recipe_List / Recipe_Card   레시피 카드 템플릿 1장 — 코드가 장식 수(24)만큼 복제한다
    //     카드 안: Icon(Image) · Name · Desc · Cost (TMP) · Button_Craft(글자 포함)
    //   Craft_List / Craft_Slot     제작 칸 템플릿 1장 — 코드가 최대 칸 수(5)만큼 복제하고 열린 칸만 보인다
    //     칸 안: Icon(Image) · Name · Time (TMP) · Bar(Image, Filled, 선택) · Button_Claim · Button_Rush(글자 포함)
    //   Info (TMP, 선택)            "제작 1/2 · 즉시 완료 오늘 5회" · 공방이 닫혀 있으면 여는 조건
    // 하나라도 없으면 그 부분만 건너뛴다 (경고 로그).
    public class WorkshopPanel
    {
        const int MAX_SLOTS = 5;   // WT.CraftSlots 최대 (2 + 4·6·8단계)

        public readonly GameObject root;
        readonly System.Action changed;   // 재화가 바뀌었다 (상단 재화 표시 갱신)
        readonly TMP_Text info;
        readonly List<Recipe> recipes = new List<Recipe>();
        readonly List<Slot> slots = new List<Slot>();

        public WorkshopPanel(GameObject root, System.Action onChanged)
        {
            this.root = root;
            changed = onChanged;
            info = SceneUI.Find(root.transform, "Info")?.GetComponent<TMP_Text>();

            var card = SceneUI.Need(root.transform, "Recipe_Card");
            if (card != null)
                for (int i = 0; i < DECOS.Length; i++)
                    recipes.Add(new Recipe(this, i == 0 ? card : Clone(card, i), DECOS[i]));

            var slot = SceneUI.Need(root.transform, "Craft_Slot");
            if (slot != null)
                for (int i = 0; i < MAX_SLOTS; i++)
                    slots.Add(new Slot(this, i == 0 ? slot : Clone(slot, i), i));
        }

        public bool IsOpen => root != null && root.activeInHierarchy;

        public void Render()
        {
            if (G?.farm == null || !IsOpen) return;
            bool open = CraftOpen();
            UIUtil.SetText(info, open
                ? Loc.F("꼬마 공방 · 제작 {0}/{1} · 즉시 완료 오늘 {2}회", G.farm.crafts.Count, WT.CraftSlots(Stage()), RushLeftToday())
                : Loc.F("도감 {0}/{1}종을 모으면 꼬마 공방이 열려요", CodexCount(), WT.codexForCraft));
            foreach (var r in recipes) r.Render(open);
            foreach (var s in slots) s.Render();
        }

        // 제작 칸의 남은 시간만 갱신 (OutGameController가 1초마다)
        public void Tick() { if (IsOpen && G?.farm != null) foreach (var s in slots) s.Render(); }

        void Craft(string id)
        {
            var f = CanCraft(id);
            if (f != CraftFail.None) { Snd.Err(); App.Notify(FailText(f)); return; }
            CraftStart(id);
            Snd.Buy();
            AfterChange();
        }

        void Claim(int i)
        {
            var r = CraftClaim(i);
            if (r == null) { Snd.Err(); return; }
            Snd.Record();
            App.Notify(r.Value.isNew ? Loc.F("{0} 완성! 꾸미기에서 세계수에 놓아요", r.Value.d.n) : Loc.F("{0} 완성! 강화 재료로 보관했어요", r.Value.d.n));
            AfterChange();
        }

        void Rush(int i)
        {
            if (!CraftRush(i)) { Snd.Err(); App.Notify(RushLeftToday() <= 0 ? Loc.T("오늘 즉시 완료를 다 썼어요") : Loc.T("균사석이 부족해요")); return; }
            Snd.Buy();
            AfterChange();
        }

        void AfterChange() { Render(); changed?.Invoke(); }

        static string FailText(CraftFail f) => f switch
        {
            CraftFail.Closed => Loc.F("도감 {0}/{1}종을 모으면 꼬마 공방이 열려요", CodexCount(), WT.codexForCraft),
            CraftFail.Recipe => Loc.T("꼬마가 합류하면 열려요"),
            CraftFail.Slots => Loc.T("슬롯 없음"),
            CraftFail.Gold => Loc.T("골드 부족"),
            CraftFail.Gem => Loc.T("균사석 부족"),
            _ => "",
        };

        static Transform Clone(Transform template, int i)
        {
            var t = Object.Instantiate(template.gameObject, template.parent, false).transform;
            t.name = template.name + " (" + i + ")";
            return t;
        }

        static Sprite Art(Deco d) => SpriteDB.Get("Farm/Props/" + d.art);
        static string Gold(double v) => UIUtil.Ic("gold") + U.Fmt(v);
        static string Gem(int v) => UIUtil.Ic("gem") + v;

        // ===== 레시피 카드 하나 =====
        class Recipe
        {
            readonly Deco d;
            readonly Image icon;
            readonly TMP_Text name, desc, cost, btnText;
            readonly Button btn;

            public Recipe(WorkshopPanel p, Transform t, Deco d)
            {
                this.d = d;
                icon = SceneUI.Need(t, "Icon")?.GetComponent<Image>();
                name = SceneUI.Need(t, "Name")?.GetComponent<TMP_Text>();
                desc = SceneUI.Need(t, "Desc")?.GetComponent<TMP_Text>();
                cost = SceneUI.Need(t, "Cost")?.GetComponent<TMP_Text>();
                var b = SceneUI.Need(t, "Button_Craft");
                btn = SceneUI.Bind(b, () => p.Craft(d.id));
                btnText = SceneUI.TextIn(b);
                if (icon != null) { icon.sprite = Art(d); icon.preserveAspect = true; }
            }

            public void Render(bool open)
            {
                bool recipe = RecipeOpen(d);
                var g = DECO_GRADES[d.grade];
                if (icon != null) icon.color = recipe ? Color.white : new Color(0, 0, 0, 0.55f);   // 잠긴 레시피는 실루엣
                UIUtil.SetText(name, recipe ? d.n : "???");
                // 꼬마 · 등급 · 걸이형/설치형 + 효과 + 보유
                string own = DecoOwned(d.id) ? Loc.F("보유 ★{0} · 재료 {1}", DecoStar(d.id), DecoDup(d.id)) : Loc.T("미보유");
                UIUtil.SetText(desc, recipe
                    ? $"{SPC[d.of].n} · {g.n} · {(d.hang ? Loc.T("걸이형") : Loc.T("설치형"))}\n{DecoFxText(d)}\n{own}"
                    : Loc.F("{0} 합류 시 해금", SPC[d.of].n));
                UIUtil.SetText(cost, $"{Gold(g.gold)} {Gem(g.gem)} · {TimeText(CraftSeconds(d))}");
                var f = open ? CanCraft(d.id) : CraftFail.Closed;
                if (btn != null) btn.interactable = f == CraftFail.None;
                UIUtil.SetText(btnText, f == CraftFail.None ? Loc.T("제작") : FailText(f));
            }
        }

        // ===== 제작 칸 하나 =====
        class Slot
        {
            readonly int i;
            readonly GameObject go;
            readonly Image icon, bar;
            readonly TMP_Text name, time, rushText;
            readonly Button claim, rush;

            public Slot(WorkshopPanel p, Transform t, int i)
            {
                this.i = i;
                go = t.gameObject;
                icon = SceneUI.Need(t, "Icon")?.GetComponent<Image>();
                bar = SceneUI.Find(t, "Bar")?.GetComponent<Image>();
                name = SceneUI.Need(t, "Name")?.GetComponent<TMP_Text>();
                time = SceneUI.Need(t, "Time")?.GetComponent<TMP_Text>();
                claim = SceneUI.Bind(SceneUI.Need(t, "Button_Claim"), () => p.Claim(i));
                var r = SceneUI.Need(t, "Button_Rush");
                rush = SceneUI.Bind(r, () => p.Rush(i));
                rushText = SceneUI.TextIn(r);
            }

            public void Render()
            {
                UIUtil.Show(go, i < WT.CraftSlots(Stage()));   // 단계가 올라 열린 칸만
                if (!go.activeSelf) return;
                var c = i < G.farm.crafts.Count ? G.farm.crafts[i] : null;
                var d = c != null ? DECO[c.deco] : null;
                bool done = CraftDone(c);
                if (icon != null) { icon.enabled = d != null; icon.preserveAspect = true; if (d != null) icon.sprite = Art(d); }
                UIUtil.SetText(name, d != null ? d.n : Loc.T("빈 칸"));
                double left = c != null ? System.Math.Max(0, (c.at - Now()) / 1000) : 0;
                UIUtil.SetText(time, d == null ? "" : done ? Loc.T("완성!") : Mmss((long)System.Math.Ceiling(left)));
                if (bar != null) bar.fillAmount = c == null ? 0 : done ? 1 : (float)System.Math.Clamp((Now() - c.t0) / System.Math.Max(1, c.at - c.t0), 0, 1);
                UIUtil.Show(claim, done);
                UIUtil.Show(rush, d != null && !done);
                if (d != null && !done)
                {
                    int cost = RushCost(c);
                    if (rush != null) rush.interactable = RushLeftToday() > 0 && G.gem >= cost;
                    UIUtil.SetText(rushText, Loc.F("즉시 {0}", Gem(cost)));
                }
            }
        }
    }
}
