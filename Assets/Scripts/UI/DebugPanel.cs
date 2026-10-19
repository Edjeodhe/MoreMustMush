using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 개발자 패널 (` 키). 프리팹 Resources/Dev/DevPanel 루트에 붙는다 — DevOnly.ON일 때만 App이 한 번 복제해 씬을 넘어 살려 둔다.
    // 루트(Canvas)는 늘 켜져 있어 키를 받고, 여닫는 건 window 자식이다. 버튼은 UIAction act "dbg-*" (arg는 배속·프리셋 값)
    public class DebugPanel : MonoBehaviour
    {
        public GameObject window;
        public TMP_Text info;
        float fpsAcc; int fpsN; int fps = 60;

        void Awake()
        {
            foreach (var a in GetComponentsInChildren<UIAction>(true))
            {
                var ua = a;
                var b = ua.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => Do(ua.act, ua.arg));
            }
            window.SetActive(DBG.open);
        }

        public void Toggle() { DBG.open = !window.activeSelf; window.SetActive(DBG.open); }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) Toggle();
            if (!window.activeSelf) return;

            fpsAcc += Time.unscaledDeltaTime; fpsN++;
            if (fpsAcc > 0.5f) { fps = Mathf.RoundToInt(fpsN / fpsAcc); fpsAcc = 0; fpsN = 0; }
            var R = StageController.I != null ? StageController.I.Round : null;
            info.text = $"FPS {fps}" + (R != null ? " · " + Loc.F("수확기 {0} · 버섯 {1} · 군락지 {2}/{3}", R.balls.Count, R.shrooms.Count, R.colonies.Count, R.st.maxCol) : "")
                + "\n" + Loc.F("배속 {0} · 해금 {1} · 무한 {2} · 밸런스 {3}", DBG.speed, DBG.allSeeds ? "ON" : "off", DBG.infinite ? "ON" : "off", TUNE.n)
                + (G != null ? "\n" + Loc.F("스테이지 {0} · 골드 {1} · 균사석 {2}", StageNow(), U.Fmt(G.gold), U.Fmt(G.gem)) : "")
                + HarvesterSpeedText(R);
        }

        // 수확기(핀볼) 속도 수치: 영구 핀볼은 수확기별로, 임시 핀볼은 한데 묶어서 현재 속도(px/s)를 보여 준다
        RoundSim trackedRound; float roundMin = float.MaxValue, roundMax;
        readonly System.Text.StringBuilder speedSb = new System.Text.StringBuilder();
        readonly System.Collections.Generic.Dictionary<string, (float sum, float max, int n)> speedByHv = new System.Collections.Generic.Dictionary<string, (float, float, int)>();
        string HarvesterSpeedText(RoundSim R)
        {
            if (R == null || R.balls.Count == 0) return "";
            speedByHv.Clear();
            float tmpSum = 0, tmpMax = 0; int tmpN = 0;
            foreach (var b in R.balls)
            {
                if (!b.perm) { tmpSum += b.spd; tmpMax = Mathf.Max(tmpMax, b.spd); tmpN++; continue; }
                speedByHv.TryGetValue(b.hv, out var e);
                speedByHv[b.hv] = (e.sum + b.spd, Mathf.Max(e.max, b.spd), e.n + 1);
            }
            // 이번 라운드에서 영구 수확기 속도가 오간 범위와, 지금 걸린 속도 효과(가속·느림) 수
            if (R != trackedRound) { trackedRound = R; roundMin = float.MaxValue; roundMax = 0; }
            int nAccel = 0, nSlow = 0;
            foreach (var b in R.balls)
            {
                if (!b.perm) continue;
                roundMin = Mathf.Min(roundMin, b.spd); roundMax = Mathf.Max(roundMax, b.spd);
                if (b.accel > 0) nAccel++;
                if (b.slow > 0) nSlow++;
            }
            var sb = speedSb; sb.Clear();
            sb.Append('\n').Append(Loc.F("수확기 속도 (px/초) · 발사 {0} · 최저 {1} · 배율 ×{2}", U.JsRound(R.st.launch), U.JsRound(R.st.minSpd), U.FmtN(R.st.spdMul, 2)));
            sb.Append('\n').Append(Loc.F("이번 판 범위 {0} ~ {1} · 가속 {2} · 느림 {3}", roundMin == float.MaxValue ? 0 : Mathf.RoundToInt(roundMin), Mathf.RoundToInt(roundMax), nAccel, nSlow));
            foreach (var kv in speedByHv)
                sb.Append('\n').Append(HV.TryGetValue(kv.Key, out var h) ? h.n : kv.Key).Append(" ×").Append(kv.Value.n)
                  .Append("  ").Append(Loc.T("지금")).Append(' ').Append(Mathf.RoundToInt(kv.Value.sum / kv.Value.n))
                  .Append("  ").Append(Loc.T("최고")).Append(' ').Append(Mathf.RoundToInt(kv.Value.max));
            if (tmpN > 0) sb.Append('\n').Append(Loc.T("임시 수확기")).Append(" ×").Append(tmpN).Append("  ").Append(Loc.T("지금")).Append(' ').Append(Mathf.RoundToInt(tmpSum / tmpN)).Append("  ").Append(Loc.T("최고")).Append(' ').Append(Mathf.RoundToInt(tmpMax));
            return sb.ToString();
        }

        public void Do(string act, string arg)
        {
            Snd.Ui();
            // 세이브를 고치는 버튼은 게임에 들어간 뒤에만 (시작 화면에서는 세이브가 아직 없을 수 있다)
            bool needSave = act != "dbg-spd" && act != "dbg-inf" && act != "dbg-preset";
            if (needSave && G == null) { App.Notify(Loc.T("게임에 들어간 뒤에 쓸 수 있어요")); return; }
            switch (act)
            {
                case "dbg-res": G.gold = System.Math.Max(G.gold, 100) * 1000; Saved(Loc.T("골드 ×1000")); break;
                case "dbg-gem": G.gem += 100; Saved(Loc.T("균사석 +100")); break;
                case "dbg-spd": DBG.speed = int.Parse(arg); break;
                case "dbg-inf": DBG.infinite = !DBG.infinite; break;
                case "dbg-seeds":
                    DBG.allSeeds = !DBG.allSeeds;
                    if (DBG.allSeeds) foreach (var s in SPECIES) if (!s.init) G.seeds[s.id] = true;
                    Saved(DBG.allSeeds ? Loc.T("버섯 전부 해금") : null);
                    break;
                case "dbg-stage":
                    G.rounds += 10; G.stage += 10; G.theme = LatestTheme().id; CheckUnlocks();
                    Saved(Loc.F("스테이지 {0} · {1}", StageNow(), LatestTheme().n));
                    break;
                case "dbg-specials": foreach (var k in SPECIALS) G.specials[k.id] = true; Saved(Loc.T("꼬마를 모두 합류시켰어요")); break;
                case "dbg-bulk":
                    var res = BulkBuy(CAT_KEYS, 0, false);
                    if (res.levels == 0) { Snd.Err(); App.Notify(Loc.T("강화할 수 있는 게 없어요")); return; }
                    Snd.Buy(); Saved(Loc.F("{0}단계 강화 완료! (−{1}골드)", res.levels, U.Fmt(res.spent)));
                    break;
                case "dbg-preset": ApplyPreset(arg); App.Notify(Loc.F("밸런스: {0} (다음 라운드부터)", TUNE.n)); break;
            }
        }

        // 세이브를 고친 뒤: 저장하고, 라운드 밖이면 지금 씬을 다시 열어 트리·상점이 새 값으로 그려지게 한다
        // (라운드 중에 다시 열면 판이 처음부터 시작되므로 Stage는 그대로 둔다 — 골드는 화면이 매 프레임 읽는다)
        static void Saved(string msg)
        {
            SaveGame();
            string scene = SceneManager.GetActiveScene().name;
            if (scene != Scenes.Stage && scene != Scenes.MainMenu) App.Go(scene);
            if (msg != null) App.Notify(msg);
        }
    }
}
