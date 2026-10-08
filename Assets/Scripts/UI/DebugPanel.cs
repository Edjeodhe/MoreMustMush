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

        void Awake() => gameObject.SetActive(false);

        public void Toggle() { DBG.open = !DBG.open; gameObject.SetActive(DBG.open); transform.SetAsLastSibling(); }

        void Update()
        {
            fpsAcc += Time.unscaledDeltaTime; fpsN++;
            if (fpsAcc > 0.5f) { fps = Mathf.RoundToInt(fpsN / fpsAcc); fpsAcc = 0; fpsN = 0; }
            var R = RoundSim.R;
            info.text = $"FPS {fps}" + (R != null ? $" · 핀볼 {R.balls.Count} · 버섯 {R.shrooms.Count} · 군락지 {R.colonies.Count}/{R.st.maxCol}" : "")
                + $"\n배속 {DBG.speed} · 해금 {(DBG.allSeeds ? "ON" : "off")} · 무한 {(DBG.infinite ? "ON" : "off")} · 밸런스 {TUNE.n}";
        }

        public void Do(string act, string arg)
        {
            var flow = GameFlow.I;
            switch (act)
            {
                case "dbg-res": if (G != null) { G.gold = System.Math.Max(G.gold, 100) * 1000; SaveGame(); flow.tree.Refresh(); flow.ShowToast("골드 ×1000"); } break;
                case "dbg-spd": DBG.speed = int.Parse(arg); break;
                case "dbg-seeds": DBG.allSeeds = !DBG.allSeeds; if (G != null && DBG.allSeeds) { foreach (var s in SPECIES) if (!s.init) G.seeds[s.id] = true; SaveGame(); } flow.tree.Refresh(); break;
                case "dbg-inf": DBG.infinite = !DBG.infinite; break;
                case "dbg-special": if (RoundSim.R != null && RoundSim.R.phase == "run" && RoundSim.R.special == null) RoundSim.R.SpawnSpecial(); else flow.ShowToast("라운드 진행 중에만 소환할 수 있어요"); break;
                case "dbg-stage": if (G != null) { G.rounds += 10; G.theme = LatestTheme().id; CheckUnlocks(); SaveGame(); flow.tree.Refresh(); flow.ShowToast($"스테이지 {StageNow()} · {LatestTheme().n}"); } break;
                case "dbg-specials": if (G != null) { foreach (var k in SPECIALS) G.specials[k.id] = true; SaveGame(); flow.ShowToast("특수 버섯 10종을 모두 잡았어요"); } break;
                case "dbg-gem": if (G != null) { G.gem += 100; G.spore += 100; G.dia += 100; SaveGame(); flow.tree.Refresh(); flow.ShowToast("균사석 +100 · 포자 +100 · 다이아몬드 +100"); } break;
                case "dbg-preset": ApplyPreset(arg); flow.ShowToast($"밸런스: {TUNE.n} (다음 라운드부터)"); break;
            }
        }
    }
}
