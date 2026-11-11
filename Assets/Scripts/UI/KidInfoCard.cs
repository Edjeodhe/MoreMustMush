using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 아래 꼬마 칸에서 꼬마를 누르면: 세계수 화면이 그 꼬마에게 다가가 따라다니고(FarmView.FocusKid), 화면 오른쪽에 이 카드가 뜬다.
    // 프리팹 Resources/UI/KidInfoCard: Card / NamePill/Text · Stars · Lv · Hearts/Heart0~4 · Bar(Fill·Label) · Zone · Trait · Button_Zone · Button_Gift · Exit · Zone_Button ×5
    // Button_Zone = 카드 왼쪽 구역 버튼(Zone_Button)들을 펴고 접는다. Zone_Button은 글자(휴식 · 구역 이름 원문)로 어느 구역인지 안다 — 누르면 그 구역으로 보낸다
    // 선물 = 꼬마 창 호감도 팝업과 같은 규칙(KidWindow)
    public class KidInfoCard
    {
        const string REST_KO = "휴식";   // Zone_Button 글자: 쉬게 하기
        static readonly Color CUR_ZONE = new Color(1f, 0.93f, 0.62f);   // 지금 구역 버튼 (배치한 색에 곱한다)
        const float LOCKED_ALPHA = 0.5f;

        class ZoneBtn { public Button b; public TMP_Text label; public string zone, ko; public Color color; }

        public readonly GameObject root;
        readonly System.Action changed;
        readonly KidWindow kids;
        readonly List<ZoneBtn> zoneBtns = new List<ZoneBtn>();
        FarmView farm;
        string id;
        bool picking;   // 구역 버튼들이 펴져 있는가

        public bool IsOpen => root != null && root.activeSelf;
        public string Kid => IsOpen ? id : null;

        public KidInfoCard(GameObject root, KidWindow kids, System.Action changed)
        {
            this.root = root; this.kids = kids; this.changed = changed;
            if (root == null) return;
            SceneUI.Bind(SceneUI.Find(root.transform, "Exit"), () => { Snd.Ui(); Close(); });
            SceneUI.Bind(SceneUI.Find(root.transform, "Button_Zone"), () => { Snd.Ui(); SetPicking(!picking); });
            SceneUI.Bind(SceneUI.Find(root.transform, "Button_Gift"), () => { Snd.Ui(); kids?.Open(); kids?.OpenAffection(id); });
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b.name != "Zone_Button") continue;
                var label = SceneUI.TextIn(b.transform);
                string ko = label != null ? label.text.Trim() : "";
                string zone = null;
                if (ko != REST_KO)
                {
                    foreach (var z in ZONES) if (z.nKo == ko) zone = z.id;
                    if (zone == null) { Debug.LogWarning($"[KidInfoCard] Zone_Button 글자 '{ko}'에 맞는 구역이 없어요 (휴식 · 뿌리 · 줄기 · 가지 · 수관)"); continue; }
                }
                var zb = new ZoneBtn { b = b, label = label, zone = zone, ko = ko, color = b.targetGraphic != null ? b.targetGraphic.color : Color.white };
                b.onClick.AddListener(() => Pick(zb.zone));
                zoneBtns.Add(zb);
            }
            SetPicking(false);
        }

        public void SetFarm(FarmView f) => farm = f;

        public void Show(string kid)
        {
            if (root == null || !HasSpecial(kid)) return;
            id = kid;
            picking = false;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            farm?.FocusKid(kid);
            Render();
        }

        public void Close()
        {
            if (!IsOpen) return;
            SetPicking(false);
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
            RenderZoneBtns();
        }

        void SetPicking(bool on)
        {
            picking = on;
            RenderZoneBtns();
        }

        // 구역 버튼: 펴져 있을 때만 보인다. 지금 구역 = 노란 빛, 아직 안 열린 구역 = 흐리게 (누르면 열리는 조건을 알린다)
        void RenderZoneBtns()
        {
            string cur = G?.farm != null && id != null ? ZoneOf(id) : null;
            foreach (var zb in zoneBtns)
            {
                UIUtil.Show(zb.b.gameObject, picking);
                if (!picking) continue;
                bool open = zb.zone == null || ZoneOpen(zb.zone);
                UIUtil.SetText(zb.label, zb.zone != null ? ZONE[zb.zone].n : Loc.T(zb.ko));
                if (zb.label != null) zb.label.alpha = open ? 1 : LOCKED_ALPHA;
                if (zb.b.targetGraphic != null) zb.b.targetGraphic.color = zb.zone == cur ? zb.color * CUR_ZONE : zb.color;
            }
        }

        // 고른 구역으로 보낸다 (zone = null이면 쉬게 한다)
        void Pick(string zone)
        {
            if (G?.farm == null || id == null) return;
            string cur = ZoneOf(id);
            if (zone == cur) { Snd.Ui(); SetPicking(false); return; }
            if (zone != null)
            {
                var z = ZONE[zone];
                if (!ZoneOpen(zone)) { Snd.Err(); App.Notify(Loc.F("세계수가 {0}단계({1})가 되면 열려요", z.open, WT.StageName(z.open))); return; }
                if (!ZoneRoom(zone)) { Snd.Err(); App.Notify(Loc.F("{0}에 자리가 없어요 (최대 {1})", z.n, ZoneCapacity())); return; }
            }
            if (!Assign(id, zone)) { Snd.Err(); return; }
            Snd.Ui();
            farm?.Cheer(id, zone != null ? Loc.F("{0}(으)로 갈게!", ZONE[zone].n) : Loc.T("잠깐 쉴게~"), null);
            farm?.Sync(false);
            picking = false;
            Render(); changed?.Invoke();
        }
    }
}
