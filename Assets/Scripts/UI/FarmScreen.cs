using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 꼬마버섯 세계수 화면 HUD (신규콘텐츠 기획서 Ver03 화면 구성): 상단 제목·재화·보관 시간, 오른쪽 패널의 탭
    // (성장 · 꼬마 · 부탁 · 공방 · 꾸미기), 수확 보따리 받기 버튼. 위쪽 버튼·패널 틀은 씬에 있고, 탭과 목록 줄은
    // 처음 그릴 때 씬의 버튼·글자를 복제해 만든다 (UIKit). 목록 내용은 FarmScreen.Tabs.cs.
    public partial class FarmScreen : MonoBehaviour
    {
        [ScenePath("World/Farm")] public FarmView farm;

        [Header("Top")]
        public TMP_Text title, status;
        public TMP_Text goldText, gemText;
        public GameObject centerMsg; public TMP_Text centerTitle, centerSub;

        [Header("Buttons")]
        public Button skin, build, treeToggle;              // 스킨 · 도감 · 창고(성장 탭)
        public TMP_Text treeToggleText;
        public Button careAll; public TMP_Text careAllText; // 수확 보따리 받기

        [Header("Panel")]
        public GameObject treePanel;                        // 오른쪽 탭 내용
        public GameObject chips;                            // 아래 탭 줄

        public static readonly string[] TABS = { "grow", "kid", "req", "craft", "deco" };
        public string tab = "grow";
        public string selKid;                               // 꼬마 탭에서 강조할 꼬마
        public int selDeco = -1;                            // 꾸미기 탭에서 강조할 장식 슬롯

        bool built;
        TMP_Text head;
        RectTransform list;
        readonly List<UIRow> rows = new List<UIRow>();
        readonly List<Button> tabButtons = new List<Button>();
        Sprite rowBack;
        float tickT;
        string shownSig;

        const float HEAD_H = 56;
        float panelW;                                       // 오른쪽 창 너비 (창 크기·위치는 씬에 둔다)
        Sprite tabOff, tabOn;                               // 왼쪽 메뉴 줄 탭 바탕 (꺼짐 · 켜짐)

        void OnEnable() { tickT = 0; shownSig = null; }

        void Build()
        {
            if (built) return;
            built = true;
            // 패널: 씬에 놓인 오른쪽 창 틀 안을 비우고 제목 줄 + 목록을 채운다
            var prt = (RectTransform)treePanel.transform;
            foreach (Transform c in prt) Destroy(c.gameObject);
            panelW = prt.rect.width; float panelH = prt.rect.height;
            head = UIKit.Text(prt, status, "Head");
            head.alignment = TextAlignmentOptions.MidlineLeft; head.fontSize = 24; head.enableWordWrapping = false;
            UIKit.Place(head.rectTransform, 20, 6, panelW - 40, HEAD_H - 8);
            var area = UIKit.Place(UIKit.Rect("List", prt), 10, HEAD_H, panelW - 20, panelH - HEAD_H - 10);
            list = UIKit.Scroll(area);
            rowBack = SpriteDB.Get("UI/panel_paper");
            // 탭: 왼쪽 메뉴 줄(chips) 안에 세로로
            var crt = (RectTransform)chips.transform;
            foreach (Transform c in crt) Destroy(c.gameObject);
            tabOff = SpriteDB.Get("UI/rail_item"); tabOn = SpriteDB.Get("UI/rail_active");
            for (int i = 0; i < TABS.Length; i++)
            {
                var b = UIKit.Button(crt, skin, "Tab" + i);
                UIKit.Place((RectTransform)b.transform, 0, i * 58, crt.rect.width, 50);
                tabButtons.Add(b);
            }
            farm.onPick = OnWorldPick;
        }

        // 월드를 눌렀다: 꼬마 → 꼬마 탭 · 나무 → 성장 탭 · 장식 → 꾸미기 탭
        void OnWorldPick(string t, string arg)
        {
            tab = t;
            if (t == "kid" && arg != null) selKid = arg;
            if (t == "deco" && int.TryParse(arg, out var i)) selDeco = i;
            Render();
        }

        public void SetTab(string t) { if (System.Array.IndexOf(TABS, t) >= 0) { tab = t; list.anchoredPosition = Vector2.zero; } Render(); }

        void Update()
        {
            if (G?.farm == null) return;
            if ((tickT -= Time.unscaledDeltaTime) > 0) return;
            tickT = 1;
            if (GameFlow.I.ModalOpen) return;
            if (farm.crits.Count != FarmOwned().Count) { farm.Sync(false); }
            BagSettle();
            Render();
        }

        public void Render()
        {
            if (G?.farm == null) return;
            Build();
            int s = Stage();
            title.text = Loc.F("{0} 세계수 {1}단계 {2} <size=60%><color=#8a6a48>나이테 Lv.{3}/{4}</color></size>", UIUtil.Ic("ed"), s, WT.StageName(s), Ring(), WT.ringMax);
            var b = G.farm.bag; double cap = BagCapSec();
            var r = BagRate();
            status.text = Loc.F("보관 {0} / {1}", Mmss((long)b.sec), Mmss((long)cap)) + (BagFull() ? " <color=#ff8a7a>" + Loc.T("가득!") + "</color>" : "")
                + $" <size=85%><color=#8a6a48>· {UIUtil.Ic("gold")}{U.Fmt(r.gold * 3600)}{Loc.T("/시간")}</color></size>";
            goldText.text = $"{UIUtil.Ic("gold")}{U.Fmt(G.gold)}";
            gemText.text = $"{UIUtil.Ic("gem")}{U.Fmt(G.gem)}";
            UIUtil.Show(centerMsg, false);
            SetLabel(build, Loc.T("도감"), "codex", null, true);
            SetLabel(treeToggle, Loc.F("창고 버섯 {0}", U.Fmt(StoreTotal())), "wttab", "grow", true);
            careAll.interactable = BagReady();
            careAllText.text = Loc.F("수확 보따리 받기  <size=80%>{0}{1}</size>", UIUtil.Ic("gold"), U.Fmt(System.Math.Floor(b.gold))) + (b.gem >= 1 ? $" <size=80%>{UIUtil.Ic("gem")}{U.Fmt(System.Math.Floor(b.gem))}</size>" : "");
            var ca = careAll.GetComponent<UIAction>(); ca.act = "wtbag"; ca.arg = null;

            string[] names = { Loc.T("성장"), Loc.T("꼬마"), Loc.T("부탁"), Loc.T("공방"), Loc.T("꾸미기") };
            int[] badge = { CanEvolve() == GrowFail.None || (TributeAllValue() > 0 && !StageMax()) ? 1 : 0, 0, ReqReadyCount() + (WeekReady() ? 1 : 0), CraftDoneCount(), 0 };
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool on = TABS[i] == tab;
                SetLabel(tabButtons[i], $"<size=80%>{i + 1}</size>  " + names[i] + (badge[i] > 0 ? $" <color={(on ? "#fff3a0" : "#e8453c")}>({badge[i]})</color>" : ""), "wttab", TABS[i], true);
                if (tabOn != null && tabOff != null) tabButtons[i].image.sprite = on ? tabOn : tabOff;
                var lt = tabButtons[i].GetComponentInChildren<TMP_Text>(true); if (lt != null) lt.color = on ? Color.white : U.Hex("#4a3a2a");
            }
            RenderTab();
        }

        static void SetLabel(Button b, string text, string act, string arg, bool on)
        {
            if (b == null) return;
            var ua = b.GetComponent<UIAction>(); if (ua != null) { ua.act = act; ua.arg = arg; }
            b.interactable = on;
            UIUtil.SetText(b.GetComponentInChildren<TMP_Text>(true), text);
        }

        // ===== 목록 =====
        int used; float y;
        void BeginList(string header) { head.text = header; used = 0; y = 0; }
        void Line(string text, Sprite icon = null, string back = "#fffaf0", params (string, string, string, bool)[] btns)
        {
            if (used >= rows.Count) rows.Add(UIRow.Make(list, status, skin, rowBack));
            var row = rows[used++];
            y += row.Set(y, panelW - 20, text, icon, U.Hex(back), btns) + 8;
        }
        void EndList()
        {
            for (int i = used; i < rows.Count; i++) rows[i].rt.gameObject.SetActive(false);
            list.sizeDelta = new Vector2(0, y);
        }
    }
}
