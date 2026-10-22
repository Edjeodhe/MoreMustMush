using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 아래 꼬마 칸에서 꼬마를 누르면: 세계수 화면이 그 꼬마에게 다가가 따라다니고(FarmView.FocusKid), 화면 오른쪽에 이 카드가 뜬다.
    // 프리팹 Resources/UI/KidInfoCard: Card / NamePill/Text · Stars · Lv · Hearts/Heart0~4 · Bar(Fill·Label) · Zone · Trait · Button_Zone · Button_Gift · Exit
    // 구역 = 쉼 → 열린 구역(자리 있는 곳) 차례로, 선물 = 꼬마 창 호감도 팝업과 같은 규칙(KidWindow)
    public class KidInfoCard
    {
        public readonly GameObject root;
        readonly System.Action changed;
        readonly KidWindow kids;
        FarmView farm;
        string id;

        public bool IsOpen => root != null && root.activeSelf;
        public string Kid => IsOpen ? id : null;

        public KidInfoCard(GameObject root, KidWindow kids, System.Action changed)
        {
            this.root = root; this.kids = kids; this.changed = changed;
            if (root == null) return;
            SceneUI.Bind(SceneUI.Find(root.transform, "Exit"), () => { Snd.Ui(); Close(); });
            SceneUI.Bind(SceneUI.Find(root.transform, "Button_Zone"), Zone);
            SceneUI.Bind(SceneUI.Find(root.transform, "Button_Gift"), () => { Snd.Ui(); kids?.Open(); kids?.OpenAffection(id); });
        }

        public void SetFarm(FarmView f) => farm = f;

        public void Show(string kid)
        {
            if (root == null || !HasSpecial(kid)) return;
            id = kid;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            farm?.FocusKid(kid);
            Render();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            farm?.Unfocus();
            changed?.Invoke();
        }

        // 화면이 꼬마를 따라가지 않게 되면(휠·끌기) 카드는 그대로 둔다. 바깥 창이 바뀌면 다시 그린다
        public void Refresh() { if (IsOpen) Render(); }

        void Render()
        {
            if (G?.farm == null || id == null) return;
            var t = root.transform;
            var k = KID[id]; var a = AffOf(id); int aw = Awaken(id);
            UIUtil.SetText(FarmWin.Text(SceneUI.Find(t, "NamePill"), "Text"), SPC[id].n);
            UIUtil.SetText(FarmWin.Text(t, "Stars"), aw > 0 ? new string('★', aw) + FarmWin.Col(new string('★', KLV.awakenMax - aw), "#d9c9b0") : FarmWin.Col(new string('★', KLV.awakenMax), "#d9c9b0"));
            UIUtil.SetText(FarmWin.Text(t, "Lv"), Loc.F("Lv. {0} / {1}", KLv(id), KLV.Cap(aw)) + "   " + Loc.F("호감도 {0}", a.lv));
            FarmWin.Hearts(SceneUI.Find(t, "Hearts"), a.lv);
            bool max = a.lv >= AFF.max;
            FarmWin.Bar(SceneUI.Find(t, "Bar"), max ? 1 : (float)(a.cur / System.Math.Max(1, a.need)), max ? "MAX" : Loc.F("다음 호감도까지 {0}", U.Fmt(System.Math.Ceiling(a.need - a.cur))));
            string zone = ZoneOf(id);
            UIUtil.SetText(FarmWin.Text(t, "Zone"), zone != null ? Loc.F("{0} · 수확 ×{1}", ZONE[zone].n, U.FmtN(KidMul(id, zone))) : FarmWin.Col(Loc.F("쉬는 중 · 선호 {0}", ZONE[k.zone].n), FarmWin.GRAY));
            UIUtil.SetText(FarmWin.Text(t, "Trait"), FarmWin.Col(k.traitText, FarmWin.GRAY));
            FarmWin.Btn(SceneUI.Find(t, "Button_Zone")?.GetComponent<Button>(), Loc.F("구역: {0}", zone != null ? ZONE[zone].n : Loc.T("쉬는 중")), true, "btn_cream");
            FarmWin.Btn(SceneUI.Find(t, "Button_Gift")?.GetComponent<Button>(), Loc.T("호감도"), true, "btn_leaf");
        }

        void Zone()
        {
            string cur = ZoneOf(id);
            var order = new System.Collections.Generic.List<string> { null };
            foreach (var z in ZONES) if (ZoneOpen(z.id)) order.Add(z.id);
            int i0 = order.IndexOf(cur);
            for (int step = 1; step <= order.Count; step++)
            {
                string next = order[(i0 + step) % order.Count];
                if (next == null || next == cur || ZoneRoom(next)) { Assign(id, next); break; }
            }
            Snd.Ui();
            farm?.Cheer(id, ZoneOf(id) != null ? Loc.F("{0}(으)로 갈게!", ZONE[ZoneOf(id)].n) : Loc.T("잠깐 쉴게~"), null);
            farm?.Sync(false);
            Render(); changed?.Invoke();
        }
    }
}
