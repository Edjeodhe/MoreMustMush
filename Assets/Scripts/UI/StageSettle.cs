using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;

namespace MoreMush
{
    // 라운드 정산 창 (Stage 씬). 씬에 배치한 정산 UI가 없어(StageController.Finished 구독자 없음) 코드로 만든다 — TempDialog와 같은 임시 창.
    // 옛 SettleScreen(삭제됨)의 내용: 제목 · 딴 버섯(분류별) · 점수→골드 · 보너스 골드 · 받은 골드 · 수확 수 · 보스/스테이지 줄 · 신기록 · 균사 트리로 버튼
    public static class StageSettle
    {
        static readonly Color Ink = new Color(0.23f, 0.14f, 0.08f), Btn = new Color(0.55f, 0.36f, 0.2f);
        const float W = 900, H = 760;

        public static void Show(UnityEngine.SceneManagement.Scene scene, RoundSim.Summary s, System.Action onClose)
        {
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) { Debug.LogWarning("[StageSettle] 캔버스가 없어 정산 창 없이 돌아가요"); onClose(); return; }

            var dim = new GameObject("StageSettle", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            dim.transform.SetAsLastSibling();
            TempDialog.Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(dim.transform, false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(W, H);
            Skin(box.GetComponent<Image>(), "panel_wood", new Color(0.98f, 0.93f, 0.8f));

            var title = TempDialog.MakeText(box.transform, "Title", s.aborted ? Loc.T("수확 중단") : Loc.T("수확 완료!"), new Vector2(0, H / 2 - 80), new Vector2(W - 120, 90), 56);
            title.color = Ink;

            var body = TempDialog.MakeText(box.transform, "Body", Body(s), new Vector2(0, 10), new Vector2(W - 140, H - 300), 34);
            body.color = Ink; body.alignment = TextAlignmentOptions.Top;
            body.enableAutoSizing = true; body.fontSizeMin = 20; body.fontSizeMax = 34;
            LocFonts.Apply(body, LocFonts.Role.Body, -1);

            var b = new GameObject("Button_ToTree", typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(box.transform, false);
            var br = b.GetComponent<RectTransform>();
            br.anchoredPosition = new Vector2(0, -H / 2 + 80); br.sizeDelta = new Vector2(420, 96);
            var img = b.GetComponent<Image>(); img.color = Btn;
            var btn = b.GetComponent<Button>(); btn.targetGraphic = img;
            var label = TempDialog.MakeText(b.transform, "Label", Loc.T("균사 트리로 ▶"), Vector2.zero, Vector2.zero, 36, true);
            label.color = Color.white;
            bool closed = false;
            btn.onClick.AddListener(() => { if (closed) return; closed = true; Object.Destroy(dim); onClose(); });

            Snd.Buy();
        }

        static string Body(RoundSim.Summary s)
        {
            var lines = new List<string>
            {
                string.Join("   ", CAT_KEYS.Select(c => $"{CATS[c].name} +{U.Fmt(s.gains[c])}")),
                Loc.F("얻은 점수 → 골드 (×{0})", U.FmtN(s.goldMul)) + "  +" + U.Fmt(s.scoreGold),
            };
            if (s.bonusGold > 0) lines.Add(Loc.T("보너스 골드") + "  +" + U.Fmt(s.bonusGold));
            lines.Add($"<b>{Loc.T("받은 골드")}  {U.Fmt(s.net)}</b>");
            lines.Add(Loc.F("수확 {0}번", U.Fmt(s.harvests)));
            var stageLine = StageLine(s);
            if (stageLine != null) lines.Add("\n" + stageLine);
            foreach (var r in s.recs)
                lines.Add(Loc.F("신기록! {0} {1}", RECORD_NAMES[r.k], U.Fmt(r.v)) + (r.old > 0 ? " " + Loc.F("(이전 {0})", U.Fmt(r.old)) : ""));
            return string.Join("\n", lines);
        }

        static string StageLine(RoundSim.Summary s)
        {
            if (s.bossId != null)
            {
                var b = BOSS[s.bossId];
                return s.bossCleared
                    ? Loc.F("보스 {0} 처치! 스테이지 {1} 열림", b.n, StageLabel(s.nextStage))
                    : $"<color=#d04a3a>{Loc.F("보스 {0}에게 졌어요… 골드 −{1}", b.n, U.Fmt(s.bossPenalty))}</color>";
            }
            if (s.aborted) return Loc.T("수확이 도중에 중단되어 스테이지가 완료되지 않습니다.");
            return s.nextStage > s.stage ? Loc.F("스테이지 {0} 완료 → 스테이지 {1}", StageLabel(s.stage), StageLabel(s.nextStage)) : null;
        }

        static void Skin(Image im, string sprite, Color fallback)
        {
            var sp = SpriteDB.Get("UI/" + sprite);
            if (sp == null) { im.color = fallback; return; }
            im.sprite = sp; im.color = Color.white;
            im.type = sp.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        }
    }
}
