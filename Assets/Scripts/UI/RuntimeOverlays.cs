using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 씬에 두지 않고 코드로 만드는 화면 위 UI: 버전 번호 · 엔딩(가안). 자기 캔버스를 따로 써서 다른 UI보다 위에 그린다
    public static class RuntimeOverlays
    {
        const int VERSION_ORDER = 900, ENDING_ORDER = 1000;
        static readonly Vector2 REF = new Vector2(1920, 1080);

        static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = REF; sc.matchWidthOrHeight = 0.5f;
            return c;
        }

        static TMP_Text MakeText(Transform parent, string text, float size, Color col)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            LocFonts.Apply(t, LocFonts.Role.Title, -1);
            t.text = text; t.fontSize = size; t.color = col; t.raycastTarget = false;
            return t;
        }

        // ===== 버전 번호 (Player Settings > Version) — 모든 화면 왼쪽 아래 =====
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ShowVersion()
        {
            if (GameObject.Find("VersionCanvas") != null) return;   // 씬을 다시 불러도 하나만
            var c = MakeCanvas("VersionCanvas", VERSION_ORDER);
            Object.DontDestroyOnLoad(c.gameObject);
            var t = MakeText(c.transform, $"Ver {Application.version}", 22, new Color(1, 1, 1, 0.6f));
            t.alignment = TextAlignmentOptions.BottomLeft;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(12, 8); rt.sizeDelta = new Vector2(400, 40);
        }

        // ===== 데미지 통계 (정산 화면 버튼) — 출처(직접 타격·스킬·능력)별 · 수확기별. 아무 데나 누르면 닫힌다 =====
        static GameObject dmgStats;
        public static void ShowDamageStats(RoundSim.Summary s)
        {
            if (dmgStats != null || s?.dmgBySrc == null) return;
            var c = MakeCanvas("DmgStatsCanvas", ENDING_ORDER);
            dmgStats = c.gameObject;
            var dim = MakeClickToClose(c.transform, new Color(0, 0, 0, 0.6f), () => { Object.Destroy(dmgStats); dmgStats = null; });
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(dim.transform, false);
            card.GetComponent<Image>().color = U.Hex("#fff6e4");
            var crt = (RectTransform)card.transform; crt.sizeDelta = new Vector2(1200, 900);
            var t = MakeText(card.transform, DamageStatsText(s), 30, U.Hex("#4a3420"));
            t.alignment = TextAlignmentOptions.TopLeft; t.enableWordWrapping = false;
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(48, 40); rt.offsetMax = new Vector2(-48, -40);
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 30;
        }

        static string DamageStatsText(RoundSim.Summary s)
        {
            double total = 0; foreach (var v in s.dmgBySrc.Values) total += v.dmg;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=140%><b>{Loc.T("데미지 통계")}</b></size>   <color=#8a6a4a>{Loc.F("총 피해 {0}", U.Fmt(total))}</color>\n\n");
            void Table(string title, System.Collections.Generic.Dictionary<string, DmgStat> d, System.Func<string, string> name)
            {
                sb.Append($"<b>{title}</b>\n<color=#8a6a4a><size=85%>{Loc.T("이름")}<pos=45%>{Loc.T("피해 비율")}<pos=62%>{Loc.T("피해")}<pos=78%>{Loc.T("타격")}<pos=89%>{Loc.T("처치")}</size></color>\n");
                foreach (var kv in System.Linq.Enumerable.OrderByDescending(d, x => x.Value.dmg))
                {
                    double pct = total > 0 ? kv.Value.dmg / total * 100 : 0;
                    sb.Append($"{name(kv.Key)}<pos=45%>{pct:0.0}%<pos=62%>{U.Fmt(kv.Value.dmg)}<pos=78%>{kv.Value.hits}<pos=89%>{kv.Value.kills}\n");
                }
                sb.Append('\n');
            }
            Table(Loc.T("스킬·능력별"), s.dmgBySrc, RoundSim.DmgSrcName);
            Table(Loc.T("수확기별"), s.dmgByHv, RoundSim.DmgHvName);
            sb.Append($"<size=80%><color=#8a6a4a>{Loc.T("화면을 누르면 닫혀요")}</color></size>");
            return sb.ToString();
        }

        static GameObject MakeClickToClose(Transform parent, Color col, UnityEngine.Events.UnityAction close)
        {
            var bg = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            bg.transform.SetParent(parent, false);
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = col;
            bg.GetComponent<Button>().onClick.AddListener(() => { Snd.Ui(); close(); });
            return bg;
        }

        // ===== 엔딩 (가안: 글자만) — 도감을 다 채우면 한 번. 아무 데나 누르면 닫힌다 =====
        static GameObject ending;
        public static void ShowEnding(int speciesTotal)
        {
            if (ending != null) return;
            var c = MakeCanvas("EndingCanvas", ENDING_ORDER);
            ending = c.gameObject;
            var bg = MakeClickToClose(c.transform, new Color(0, 0, 0, 0.85f), () => { Object.Destroy(ending); ending = null; });
            var t = MakeText(bg.transform, $"{Loc.T("엔딩")}\n<size=35%>{Loc.F("버섯 도감 {0}종 완성!", speciesTotal)}</size>\n\n<size=22%><color=#c8c8c8>{Loc.T("화면을 누르면 계속해요")}</color></size>", 140, Color.white);
            t.alignment = TextAlignmentOptions.Center;
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
