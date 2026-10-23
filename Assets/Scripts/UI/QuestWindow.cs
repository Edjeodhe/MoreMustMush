using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 의뢰 창: 일일 3개 · 주간 3개 · 세계수 경험치 (규칙은 Game.Quests). MainGame의 의뢰 버튼이 연다.
    // 씬에 배치된 QuestPanel이 있으면 그것을 쓴다. 이름으로 찾는 것:
    //   Exit(닫기) · Daily_Reset · Weekly_Reset(초기화까지 남은 시간) · Tree_Exp(세계수 경험치)
    //   Quest_D0~D2 · Quest_W0~W2 (한 줄) — 안의 Text(내용) · Progress(진행도) · Button_Claim(받기, 안의 글자 = 보상)
    // 배치된 창이 없으면 임시 창을 만든다 — 창이 배치되면 자동으로 그쪽을 쓴다.
    public class QuestWindow
    {
        public const string PANEL = "QuestPanel";
        const int ROWS = 6;   // 0~2 일일, 3~5 주간

        class Row { public GameObject go; public TMP_Text text, prog, claimText; public Button claim; }

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        readonly Row[] rows = new Row[ROWS];
        TMP_Text dailyReset, weeklyReset, treeExp;
        TMP_Text title, dailyLabel, weeklyLabel, treeLabel;   // 임시 창의 고정 글자 (언어가 바뀌면 다시 쓴다)
        long shownSec = -1;

        public static QuestWindow For(Scene scene, UnityAction onClose)
        {
            var w = new QuestWindow();
            var placed = SceneUI.Find(scene, PANEL);
            if (placed != null) w.WirePlaced(placed, onClose);
            else w.BuildTemp(scene, onClose);
            if (w.Root != null) w.Root.SetActive(false);
            return w;
        }

        static string Slot(int i) => (i < 3 ? "d" : "w") + (i % 3);

        void WirePlaced(Transform root, UnityAction onClose)
        {
            Root = root.gameObject;
            var exit = SceneUI.Find(root, "Exit");
            if (exit != null) SceneUI.Bind(exit, onClose);
            dailyReset = SceneUI.TextIn(SceneUI.Find(root, "Daily_Reset"));
            weeklyReset = SceneUI.TextIn(SceneUI.Find(root, "Weekly_Reset"));
            treeExp = SceneUI.TextIn(SceneUI.Find(root, "Tree_Exp"));
            for (int i = 0; i < ROWS; i++)
            {
                var t = SceneUI.Find(root, "Quest_" + Slot(i).ToUpperInvariant());
                if (t == null) { Debug.LogWarning($"[Quest] 배치된 QuestPanel 안에 'Quest_{Slot(i).ToUpperInvariant()}' 줄이 없어요"); continue; }
                var claim = SceneUI.Find(t, "Button_Claim");
                rows[i] = new Row
                {
                    go = t.gameObject,
                    text = SceneUI.TextIn(SceneUI.Find(t, "Text")),
                    prog = SceneUI.TextIn(SceneUI.Find(t, "Progress")),
                    claim = claim != null ? claim.GetComponent<Button>() : null,
                    claimText = SceneUI.TextIn(claim),
                };
                int k = i;
                if (claim != null) SceneUI.Bind(claim, () => Claim(k));
            }
        }

        // ===== 임시 창 (배치 전) =====
        // 그림은 Resources/UI (세계수 탭 창과 같은 세트): 창 panel_wood · 제목 ribbon · 닫기 btn_close · 의뢰 줄 panel_paper · 세계수 줄 panel_mint
        // 받기 버튼 = 받을 수 있으면 btn_go(크림 글자), 아니면 btn_wood(진한 글자, 흐리게)
        const float ROW_H = 80, BOX_W = 1240;
        bool temp;   // 임시 창이면 받기 버튼 그림을 상태마다 바꾼다

        void BuildTemp(Scene scene, UnityAction onClose)
        {
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) { Debug.LogWarning("[Quest] 캔버스가 없어 의뢰 창을 만들 수 없어요"); return; }
            temp = true;
            Root = TempDialog.Dim(canvas, PANEL);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            box.transform.SetParent(Root.transform, false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(BOX_W, 0);
            TempDialog.Skin(box.GetComponent<Image>(), "panel_wood", TempDialog.BOX);
            var v = box.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(56, 56, 96, 50); v.spacing = 8;   // 위는 리본 자리
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 리본·닫기는 창 위 끝에 고정 (레이아웃 밖)
            title = TempDialog.Ribbon(box.transform, "", Vector2.zero, 560);
            Pin(title.transform.parent as RectTransform, new Vector2(0, -28));   // 위 끝에 걸치되 화면 밖으로 안 나가게
            var close = TempDialog.SkinButton(box.transform, "Exit", "", onClose, "btn_close", 1, out var closeT);
            closeT.gameObject.SetActive(false);
            close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var cr = (RectTransform)close.transform; cr.sizeDelta = new Vector2(96, 92);
            Pin(cr, new Vector2(BOX_W / 2 - 40, -12));

            Line(box.transform, "Daily", ROW_H, out dailyLabel, out dailyReset, 36, null);
            for (int i = 0; i < 3; i++) rows[i] = QuestLine(box.transform, i);
            Line(box.transform, "Weekly", ROW_H, out weeklyLabel, out weeklyReset, 36, null);
            for (int i = 3; i < ROWS; i++) rows[i] = QuestLine(box.transform, i);
            Line(box.transform, "Tree", ROW_H, out treeLabel, out treeExp, 32, "panel_mint");
        }

        // 창 위 가운데를 기준으로 놓는다 (y = 위 끝에서)
        static void Pin(RectTransform rt, Vector2 fromTop)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.anchoredPosition = fromTop;
        }

        // 왼쪽 글자 + 오른쪽 값 한 줄 (back = 줄 배경 그림, null이면 구역 제목: 진한 갈색 굵은 글자)
        static GameObject Line(Transform parent, string name, float h, out TMP_Text left, out TMP_Text right, float size, string back)
        {
            var row = HRow(parent, name, h, back);
            left = Label(row.transform, "Text", size, TextAlignmentOptions.MidlineLeft, -1);
            right = Label(row.transform, "Value", size * 0.8f, TextAlignmentOptions.MidlineRight, 300);
            if (back == null) { left.color = SECTION; right.color = NOTE; }
            else row.GetComponent<HorizontalLayoutGroup>().padding.right = 26;   // 버튼 없는 줄은 값이 테두리에 붙지 않게
            return row;
        }
        static readonly Color SECTION = new Color(0.45f, 0.28f, 0.15f), NOTE = new Color(0.6f, 0.48f, 0.35f);

        Row QuestLine(Transform parent, int i)
        {
            var go = HRow(parent, "Quest_" + Slot(i).ToUpperInvariant(), ROW_H, "panel_paper");
            var r = new Row { go = go };
            r.text = Label(go.transform, "Text", 32, TextAlignmentOptions.MidlineLeft, -1);
            r.prog = Label(go.transform, "Progress", 30, TextAlignmentOptions.Center, 170);
            int k = i;
            var b = TempDialog.SkinButton(go.transform, "Button_Claim", "", () => Claim(k), "btn_wood", 30, out var ct);
            TempDialog.FitBorder((Image)b.targetGraphic, ROW_H - 12);
            r.claimText = ct;
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 240;
            r.claim = b;
            return r;
        }

        static GameObject HRow(Transform parent, string name, float h, string back)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = h;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 16; hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            if (back != null)
            {
                var bg = row.AddComponent<Image>();
                TempDialog.Skin(bg, back, new Color(1f, 0.98f, 0.94f));
                TempDialog.FitBorder(bg, h);
                hl.padding = new RectOffset(26, 8, 6, 6);   // 버튼은 줄 오른쪽 끝 안쪽에
            }
            else hl.padding = new RectOffset(6, 10, 10, 0);
            return row;
        }

        // width < 0 = 남은 폭을 다 쓴다
        static TMP_Text Label(Transform parent, string name, float size, TextAlignmentOptions align, float width)
        {
            var t = TempDialog.MakeText(parent, name, "", Vector2.zero, Vector2.zero, size);
            t.alignment = align;
            var le = t.gameObject.AddComponent<LayoutElement>();
            if (width < 0) le.flexibleWidth = 1; else le.preferredWidth = width;
            return t;
        }

        // ===== 그리기 =====
        public void Open() { Render(); }

        public void Render()
        {
            if (Root == null || G == null) return;
            QuestEnsure();
            UIUtil.SetText(title, Loc.T("의뢰"));
            UIUtil.SetText(dailyLabel, Loc.T("<b>일일 의뢰</b>"));
            UIUtil.SetText(weeklyLabel, Loc.T("<b>주간 의뢰</b>"));
            UIUtil.SetText(treeLabel, Loc.T("세계수 경험치"));
            for (int i = 0; i < ROWS; i++)
            {
                var r = rows[i];
                if (r == null) continue;
                var list = i < 3 ? G.quest.daily : G.quest.weekly;
                int k = i % 3;
                bool has = k < list.Count && QuestDefOf(list[k]) != null;
                UIUtil.Show(r.go, has);
                if (!has) continue;
                var p = list[k]; var d = QuestDefOf(p);
                UIUtil.SetText(r.text, QuestText(p));
                UIUtil.SetText(r.prog, $"{U.Fmt(p.prog)}/{U.Fmt(d.target)}");
                bool can = QuestClaimable(p);
                if (r.claim != null) r.claim.interactable = can;
                if (temp && r.claim != null) ClaimSkin(r, can);
                UIUtil.SetText(r.claimText, p.claimed ? Loc.T("완료") : Loc.F("+{0} 경험치", d.exp));
            }
            UIUtil.SetText(treeExp, StageMax() ? Loc.T("최대 단계") : $"{U.Fmt(Math.Floor(TreeExp()))}/{U.Fmt(TreeExpNeed())}");
            shownSec = -1;
            Tick();
        }

        // 임시 창 받기 버튼: 받을 수 있으면 초록(크림 글자), 아니면 나무(진한 글자)
        static void ClaimSkin(Row r, bool can)
        {
            string sp = can ? "btn_go" : "btn_wood";
            var img = r.claim.targetGraphic as Image;
            if (img != null && (img.sprite == null || img.sprite.name != sp)) { TempDialog.Skin(img, sp, new Color(0.55f, 0.36f, 0.2f)); TempDialog.FitBorder(img, ROW_H - 12); }
            TempDialog.SkinButtonText(r.claimText, sp);
        }

        // 열려 있는 동안 초기화까지 남은 시간 (초가 바뀔 때만), 자정을 넘기면 새 의뢰로 다시 그린다 (시계를 되돌린 경우는 QuestEnsure처럼 무시)
        public void Tick()
        {
            if (!IsOpen || G == null) return;
            double now = Now();
            long sec = (long)Math.Floor(now / 1000);
            if (sec == shownSec) return;
            long day = KstDay(now);
            if (shownSec >= 0 && day > G.quest.day) { Render(); return; }
            shownSec = sec;
            UIUtil.SetText(dailyReset, Loc.F("<size=80%>초기화까지 {0}</size>", U.FmtTime(Until(day + 1, now))));
            UIUtil.SetText(weeklyReset, Loc.F("<size=80%>초기화까지 {0}</size>", U.FmtTime(Until((KstWeek(day) + 1) * 7 - 3, now))));
        }

        void Claim(int i)
        {
            var exp = QuestClaim(Slot(i));
            if (exp == null) return;
            Snd.Ui();
            App.Notify(Loc.F("세계수 경험치 +{0}", exp.Value));
            Render();
        }

        // 그 KST 날짜(day) 0시까지 남은 초
        static double Until(long day, double now) => Math.Max(0, (day * DAY_MS - KST_OFFSET_MS - now) / 1000);
    }
}
