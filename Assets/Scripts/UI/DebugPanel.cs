using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 개발자 패널 (` 키) + 치트 단축키 (Shift + 키). 프리팹 Resources/Dev/DevPanel 루트에 붙는다 — DevOnly.ON일 때만 App이 한 번 복제해 씬을 넘어 살려 둔다.
    // 루트(Canvas)는 늘 켜져 있어 키를 받고, 여닫는 건 window 자식이다. 버튼은 UIAction act "dbg-*" (arg는 배속·프리셋 값)
    // 출시 빌드(에디터·Development Build·MOREMUSH_DEV가 아님)에는 동작 코드가 들어가지 않는다. 직렬화 필드는 빌드 간 레이아웃이 같도록 #if 밖에 둔다
    public class DebugPanel : MonoBehaviour
    {
        public GameObject window;
        public TMP_Text info;
        public TMP_Text keys;   // 치트 키 안내 (CHEATS 표로 채운다)

#if MOREMUSH_DEV || DEVELOPMENT_BUILD || UNITY_EDITOR
        float fpsAcc; int fpsN; int fps = 60;

        // 치트 단축키: Shift를 누른 채로 (게임 키 F 배속 · M 소리 · Esc와 겹치지 않게). 웹(itch)에서도 캔버스를 한 번 클릭하면 받는다
        struct Cheat { public Key key; public string act, arg, label; public Cheat(Key k, string a, string g, string l) { key = k; act = a; arg = g; label = l; } }
        static readonly Cheat[] CHEATS =
        {
            new Cheat(Key.Digit1, "dbg-res", "10", "골드 ×10 (최소 1천)"),
            new Cheat(Key.Digit2, "dbg-gem", "100", "균사석 +100"),
            new Cheat(Key.Digit3, "dbg-rain", null, "골드 자동 증가 (초당 +10%) 켜기/끄기"),
            new Cheat(Key.Digit4, "dbg-store", "50", "창고 버섯 +50 (해금한 종마다)"),
            new Cheat(Key.Digit5, "dbg-seeds", null, "버섯 전부 해금 켜기/끄기"),
            new Cheat(Key.Digit6, "dbg-tree", null, "세계수 경험치 채우기 (다음 진화까지)"),
            new Cheat(Key.Digit7, "dbg-ff", "3600", "시간 +1시간 (보따리·공방)"),
            new Cheat(Key.Digit8, "dbg-bag", null, "방치 보상 가득 채워 바로 받기"),
            new Cheat(Key.Digit9, "dbg-stage", null, "스테이지 +10"),
            new Cheat(Key.Digit0, "dbg-bulk", null, "싼 강화부터 일괄 구매"),
            new Cheat(Key.K, "dbg-specials", null, "정령(꼬마) 모두 합류"),
            new Cheat(Key.S, "dbg-spd", "cycle", "라운드 배속 1→2→4→8"),
            new Cheat(Key.I, "dbg-inf", null, "라운드 시간 무한 켜기/끄기"),
        };

        const double RAIN_RATE = 0.1, RAIN_MIN = 1000, RAIN_SAVE_SEC = 5;   // 골드 자동 증가: 초당 지금 골드의 10% (적어도 1천), 5초마다 저장
        bool rain; float rainSave;

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
            if (keys != null)
            {
                var sb = new System.Text.StringBuilder("치트 키 (Shift + 키)");
                foreach (var c in CHEATS) sb.Append('\n').Append(KeyName(c.key)).Append("  ").Append(c.label);
                keys.text = sb.ToString();
            }
        }

        static string KeyName(Key k) => k >= Key.Digit1 && k <= Key.Digit0 ? (k == Key.Digit0 ? "0" : ((int)(k - Key.Digit1) + 1).ToString()) : k.ToString();

        public void Toggle() { DBG.open = !window.activeSelf; window.SetActive(DBG.open); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) Toggle();
            if (kb != null && kb.shiftKey.isPressed)
                foreach (var c in CHEATS)
                    if (kb[c.key].wasPressedThisFrame) { Debug.Log($"[치트] Shift+{KeyName(c.key)} {c.label}"); Do(c.act, c.arg); break; }
            Rain();
            if (!window.activeSelf) return;

            fpsAcc += Time.unscaledDeltaTime; fpsN++;
            if (fpsAcc > 0.5f) { fps = Mathf.RoundToInt(fpsN / fpsAcc); fpsAcc = 0; fpsN = 0; }
            var R = StageController.I != null ? StageController.I.Round : null;
            info.text = $"FPS {fps}" + (R != null ? " · " + Loc.F("수확기 {0} · 버섯 {1} · 군락지 {2}/{3}", R.balls.Count, R.shrooms.Count, R.colonies.Count, R.st.maxCol) : "")
                + "\n" + Loc.F("배속 {0} · 해금 {1} · 무한 {2} · 밸런스 {3}", DBG.speed, DBG.allSeeds ? "ON" : "off", DBG.infinite ? "ON" : "off", TUNE.n)
                + (G != null ? "\n" + Loc.F("스테이지 {0} · 골드 {1} · 균사석 {2}", StageNow(), U.Fmt(G.gold), U.Fmt(G.gem)) : "")
                + (rain ? "\n골드 자동 증가 ON" : "")
                + HarvesterSpeedText(R);
        }

        // 골드 자동 증가: 화면 골드는 매 프레임 G.gold를 읽으므로 값만 올리고, 저장은 몇 초마다
        void Rain()
        {
            if (!rain || G == null) return;
            float dt = Time.unscaledDeltaTime;
            G.gold += System.Math.Max(RAIN_MIN, G.gold * RAIN_RATE) * dt;
            if ((rainSave -= dt) <= 0) { rainSave = (float)RAIN_SAVE_SEC; SaveGame(); }
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
                // 골드·균사석은 화면이 매 프레임 읽으므로 씬을 다시 열지 않고 저장만 한다
                case "dbg-res":
                    double mul = double.TryParse(arg, out var m) ? m : 1000;
                    G.gold = System.Math.Max(G.gold, 100) * mul; SaveGame(); App.Notify($"골드 ×{mul} → {U.Fmt(G.gold)}");
                    break;
                case "dbg-gem":
                    int add = int.TryParse(arg, out var a) ? a : 100;
                    G.gem += add; SaveGame(); App.Notify(Loc.F("균사석 +{0}", add) + $" → {U.Fmt(G.gem)}");
                    break;
                case "dbg-rain":
                    rain = !rain; rainSave = 0; if (!rain) SaveGame();
                    App.Notify(rain ? "골드 자동 증가 켬" : "골드 자동 증가 끔");
                    break;
                case "dbg-spd":
                    DBG.speed = arg == "cycle" ? (DBG.speed >= 8 ? 1 : DBG.speed * 2) : int.Parse(arg);
                    App.Notify($"라운드 배속 ×{DBG.speed}");
                    break;
                case "dbg-inf": DBG.infinite = !DBG.infinite; App.Notify(DBG.infinite ? "라운드 시간 무한 켬" : "라운드 시간 무한 끔"); break;
                case "dbg-seeds":
                    DBG.allSeeds = !DBG.allSeeds;
                    if (DBG.allSeeds) foreach (var s in SPECIES) if (!s.init) G.seeds[s.id] = true;
                    Saved(DBG.allSeeds ? Loc.T("버섯 전부 해금") : "버섯 전부 해금 끔");
                    break;
                case "dbg-store":
                {
                    int n = int.TryParse(arg, out var k) ? k : 50, kinds = 0;
                    var picks = new System.Collections.Generic.Dictionary<string, int>();
                    foreach (var s in SPECIES) if (IsUnlockedSp(s)) { picks[s.id] = n; kinds++; }
                    StoreAdd(picks);
                    Saved($"창고 버섯 +{n} × {kinds}종 (창고 {StoreTotal()}개)");
                    break;
                }
                case "dbg-tree":
                    if (StageMax()) { App.Notify("세계수가 이미 마지막 단계예요"); return; }
                    G.farm.exp = System.Math.Max(G.farm.exp, TreeExpNeed());
                    Saved($"세계수 경험치 {U.Fmt(G.farm.exp)}/{U.Fmt(TreeExpNeed())}");
                    break;
                case "dbg-ff":
                {
                    double sec = double.TryParse(arg, out var t) ? t : 3600;
                    double g0 = G.farm.bag.gold, m0 = G.farm.bag.gem;
                    FastForward(sec);
                    Saved($"시간 +{TimeText(sec)} · 보따리 +{U.Fmt(G.farm.bag.gold - g0)}G +{U.Fmt(G.farm.bag.gem - m0)} 균사석 ({TimeText(G.farm.bag.sec)}/{TimeText(BagCapSec())})");
                    break;
                }
                case "dbg-bag":
                {
                    BagSettle();
                    FastForward(System.Math.Max(0, BagCapSec() - G.farm.bag.sec));
                    var got = BagClaim();
                    if (got == null) { Snd.Err(); Saved("받을 방치 보상이 없어요 (구역에 정령을 배치하세요)"); return; }
                    Saved($"방치 보상 받음: +{U.Fmt(got.Value.gold)}G +{got.Value.gem} 균사석 (나무버섯 {got.Value.n}개)");
                    break;
                }
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

        // 시간 빨리 감기: 지금까지의 수확을 정산한 뒤, 보따리 시계와 공방 제작 시각을 sec만큼 과거로 옮기고 다시 정산한다
        // (그 시간 동안 자리를 비운 것과 같다. 보따리는 보관 시간까지만 쌓인다)
        static void FastForward(double sec)
        {
            if (sec <= 0) return;
            BagSettle();
            var f = G.farm; double ms = sec * 1000;
            f.bag.t -= ms;
            foreach (var c in f.crafts) { c.t0 -= ms; c.at -= ms; }
            BagSettle();
        }

        // 세이브를 고친 뒤: 저장하고, 라운드 밖이면 지금 씬을 다시 열어 트리·상점이 새 값으로 그려지게 한다
        // (라운드 중에 다시 열면 판이 처음부터 시작되므로 Stage는 그대로 둔다 — 골드는 화면이 매 프레임 읽는다)
        static void Saved(string msg)
        {
            SaveGame();
            string scene = SceneManager.GetActiveScene().name;
            if (scene != Scenes.Stage && scene != Scenes.MainMenu) App.Go(scene);
            if (msg != null) { Debug.Log("[치트] " + msg); App.Notify(msg); }
        }
#else
        void Awake() => gameObject.SetActive(false);
#endif
    }
}
