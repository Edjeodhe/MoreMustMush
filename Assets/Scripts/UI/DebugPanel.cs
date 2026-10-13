using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Debug panel (` key, prototype renderDebug). Buttons carry UIAction "dbg-*".
    public class DebugPanel : MonoBehaviour
    {
        public TMP_Text info;
        float fpsAcc; int fpsN; int fps = 60;

        // 씬에 꺼진 채 저장돼 있다. 예전 Awake(SetActive(false))는 첫 Toggle의 켜기를 바로 되돌려서 세 번 눌러야 열렸다
        public void Toggle() { DBG.open = !gameObject.activeSelf; gameObject.SetActive(DBG.open); transform.SetAsLastSibling(); }

        void Update()
        {
            fpsAcc += Time.unscaledDeltaTime; fpsN++;
            if (fpsAcc > 0.5f) { fps = Mathf.RoundToInt(fpsN / fpsAcc); fpsAcc = 0; fpsN = 0; }
            var R = RoundSim.R;
            info.text = $"FPS {fps}" + (R != null ? " · " + Loc.F("핀볼 {0} · 버섯 {1} · 군락지 {2}/{3}", R.balls.Count, R.shrooms.Count, R.colonies.Count, R.st.maxCol) : "")
                + "\n" + Loc.F("배속 {0} · 해금 {1} · 무한 {2} · 밸런스 {3}", DBG.speed, DBG.allSeeds ? "ON" : "off", DBG.infinite ? "ON" : "off", TUNE.n)
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
            // 이번 라운드에서 영구 핀볼 속도가 오간 범위와, 지금 걸린 속도 효과(가속·느림·안개) 수
            if (R != trackedRound) { trackedRound = R; roundMin = float.MaxValue; roundMax = 0; }
            int nAccel = 0, nSlow = 0, nFog = 0;
            foreach (var b in R.balls)
            {
                if (!b.perm) continue;
                roundMin = Mathf.Min(roundMin, b.spd); roundMax = Mathf.Max(roundMax, b.spd);
                if (b.accel > 0) nAccel++;
                if (b.slow > 0) nSlow++;
                if (b.fog > 0) nFog++;
            }
            var sb = speedSb; sb.Clear();
            sb.Append('\n').Append(Loc.F("수확기 속도 (px/초) · 발사 {0} · 최저 {1} · 배율 ×{2}", U.JsRound(R.st.launch), U.JsRound(R.st.minSpd), U.FmtN(R.st.spdMul, 2)));
            sb.Append('\n').Append(Loc.F("이번 판 범위 {0} ~ {1} · 가속 {2} · 느림 {3} · 안개 {4}", roundMin == float.MaxValue ? 0 : Mathf.RoundToInt(roundMin), Mathf.RoundToInt(roundMax), nAccel, nSlow, nFog));
            foreach (var kv in speedByHv)
                sb.Append('\n').Append(HV.TryGetValue(kv.Key, out var h) ? h.n : kv.Key).Append(" ×").Append(kv.Value.n)
                  .Append("  ").Append(Loc.T("지금")).Append(' ').Append(Mathf.RoundToInt(kv.Value.sum / kv.Value.n))
                  .Append("  ").Append(Loc.T("최고")).Append(' ').Append(Mathf.RoundToInt(kv.Value.max));
            if (tmpN > 0) sb.Append('\n').Append(Loc.T("임시 핀볼")).Append(" ×").Append(tmpN).Append("  ").Append(Loc.T("지금")).Append(' ').Append(Mathf.RoundToInt(tmpSum / tmpN)).Append("  ").Append(Loc.T("최고")).Append(' ').Append(Mathf.RoundToInt(tmpMax));
            return sb.ToString();
        }

        public void Do(string act, string arg)
        {
            var flow = GameFlow.I;
            switch (act)
            {
                case "dbg-res": if (G != null) { G.gold = System.Math.Max(G.gold, 100) * 1000; SaveGame(); flow.tree.Refresh(); flow.ShowToast(Loc.T("골드 ×1000")); } break;
                case "dbg-spd": DBG.speed = int.Parse(arg); break;
                case "dbg-seeds": DBG.allSeeds = !DBG.allSeeds; if (G != null && DBG.allSeeds) { foreach (var s in SPECIES) if (!s.init) G.seeds[s.id] = true; SaveGame(); } flow.tree.Refresh(); break;
                case "dbg-inf": DBG.infinite = !DBG.infinite; break;
                case "dbg-special": if (RoundSim.R != null && RoundSim.R.phase == "run" && RoundSim.R.special == null) RoundSim.R.SpawnSpecial(); else flow.ShowToast(Loc.T("라운드 진행 중에만 소환할 수 있어요")); break;
                case "dbg-stage": if (G != null) { G.rounds += 10; G.stage += 10; G.theme = LatestTheme().id; CheckUnlocks(); SaveGame(); flow.tree.Refresh(); flow.ShowToast(Loc.F("스테이지 {0} · {1}", StageNow(), LatestTheme().n)); } break;
                case "dbg-specials": if (G != null) { foreach (var k in SPECIALS) G.specials[k.id] = true; SaveGame(); flow.ShowToast(Loc.T("특수 버섯 10종을 모두 잡았어요")); } break;
                case "dbg-gem": if (G != null) { G.gem += 100; SaveGame(); flow.tree.Refresh(); flow.ShowToast(Loc.T("균사석 +100")); } break;
                case "dbg-preset": ApplyPreset(arg); flow.ShowToast(Loc.F("밸런스: {0} (다음 라운드부터)", TUNE.n)); break;
            }
        }
    }
}
