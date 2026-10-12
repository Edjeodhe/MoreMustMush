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
                + "\n" + Loc.F("배속 {0} · 해금 {1} · 무한 {2} · 밸런스 {3}", DBG.speed, DBG.allSeeds ? "ON" : "off", DBG.infinite ? "ON" : "off", TUNE.n);
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
                case "dbg-stage": if (G != null) { G.rounds += 10; G.theme = LatestTheme().id; CheckUnlocks(); SaveGame(); flow.tree.Refresh(); flow.ShowToast(Loc.F("스테이지 {0} · {1}", StageNow(), LatestTheme().n)); } break;
                case "dbg-specials": if (G != null) { foreach (var k in SPECIALS) G.specials[k.id] = true; SaveGame(); flow.ShowToast(Loc.T("특수 버섯 10종을 모두 잡았어요")); } break;
                case "dbg-gem": if (G != null) { G.gem += 100; SaveGame(); flow.tree.Refresh(); flow.ShowToast(Loc.T("균사석 +100")); } break;
                case "dbg-preset": ApplyPreset(arg); flow.ShowToast(Loc.F("밸런스: {0} (다음 라운드부터)", TUNE.n)); break;
            }
        }
    }
}
