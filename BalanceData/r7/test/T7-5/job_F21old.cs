// T7-B: T7-3 끝 상태(표시 55)에서 판을 다시 돌려 군락·성능·재생·거대 버섯·날씨를 잰다 (구매 없음, 실제 세이브 안 건드림)
// 한 틱에 (상태 시드, 날씨) 하나, 판 K개. 결과는 B_rounds.csv 에 줄마다 덧붙인다.
string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/r7/test/T7-5/";
string stDir = outDir + "mid21/";
string tag = "F21old"; bool oldHp = true; bool zeroDur = false; int Kn = 12;   // 출력 파일 접두
int K = 0;
K = Kn; var weathers = new string[] { "clear", "rain" };
bool forceGiant = true;
float skill = 0.6f;
if (System.IO.File.Exists(outDir + tag + "_running.txt") && System.IO.File.ReadAllText(outDir + tag + "_running.txt").StartsWith("start")) return "already running";
System.IO.File.WriteAllText(outDir + tag + "_running.txt", "start " + System.DateTime.Now.ToString("HH:mm:ss"));
System.IO.File.WriteAllText(outDir + tag + "_rounds.csv", "state,weather,k,stage,theme,max,start,colS,planMs,avgCols,net,harvests,made,dur,attempts,fails,frameMax,frameP99,attFrameMean,attFrameMax,delayMean,extraDelayMean,births,giantFallback,giantChecked\n");
var BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
var miArea = typeof(MoreMush.RoundSim).GetMethod("AreaFree", BF, null, new System.Type[] { typeof(MoreMush.Shape), typeof(MoreMush.Colony) }, null);
var miGiant = typeof(MoreMush.RoundSim).GetMethod("FindGiantSpot", BF);
var fiNoSpawn = typeof(MoreMush.RoundSim).GetField("NO_SPAWN_Y", BF);
double[] savedGt = (double[])MoreMush.Defs.GIANT_TIER_HP.Clone();
if (oldHp) { MoreMush.Defs.GIANT_TIER_HP[0]=1; MoreMush.Defs.GIANT_TIER_HP[1]=4; MoreMush.Defs.GIANT_TIER_HP[2]=12; MoreMush.Defs.GIANT_TIER_HP[3]=40; }
int ji = 0; int nJobs = 6 * weathers.Length;
System.Action tick = null;
tick = () =>
{
    try
    {
        if (ji >= nJobs)
        {
            System.IO.File.WriteAllText(outDir + tag + "_running.txt", "stopped");
            for (int q=0;q<4;q++) MoreMush.Defs.GIANT_TIER_HP[q]=savedGt[q];
            System.IO.File.WriteAllText(outDir + tag + "_done.txt", System.DateTime.Now.ToString("HH:mm:ss") + " gt=" + string.Join(",", MoreMush.Defs.GIANT_TIER_HP));
            UnityEditor.EditorApplication.update -= tick.Invoke;
            return;
        }
        int sd = ji / weathers.Length + 1; string wth = weathers[ji % weathers.Length]; ji++;
        string json = System.IO.File.ReadAllText(stDir + "state_k06_s" + sd + ".json");
        var sb = new System.Text.StringBuilder();
        MoreMush.EditorTools.BalanceSim.Sandbox<int>(() =>
        {
            for (int k = 0; k < K; k++)
            {
                var g = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(json);
                // 다음 판이 일반 판이 되게 (보스 판 피함)
                if (zeroDur) { g.nodes["ps_dur"] = 0; g.nodes["ps_dur2"] = 0; }
                MoreMush.Game.G = g;
                if (MoreMush.Game.BossRoundNext()) g.bossOn = false;
                g.theme = MoreMush.EditorTools.BalanceSim.PickZone();
                int seedv = sd * 1000 + 500 + k;
                var rnd = new System.Random(seedv); UnityEngine.Random.InitState(seedv);
                string w = wth;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var R = MoreMush.RoundSim.Start(w, g.theme, null);
                double planMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                int startCols = R.colonies.Count; float colS = R.colS; int maxCol = R.st.maxCol; int stage = R.st.stage; double dur = R.st.duration;
                if (forceGiant && R.giantState != "pending") R.giantState = "pending";
                R.Launch(UnityEngine.Mathf.Atan2(-1f, (float)(rnd.NextDouble() * 0.8 - 0.4)));
                float h = 1 / 30f, sim = 0, noise = 0, nt = 0;
                var frameMs = new System.Collections.Generic.List<double>(); var attMs = new System.Collections.Generic.List<double>();
                double colSum = 0; int colFrames = 0; int attempts = 0, fails = 0, births = 0;
                var prevSet = new System.Collections.Generic.HashSet<MoreMush.Colony>(R.colonies);
                var removedAt = new System.Collections.Generic.Queue<float>();
                var delays = new System.Collections.Generic.List<double>();
                int giantFb = 0, giantChk = 0; bool giantChecked = false; MoreMush.Shroom gS = null; int gTier = -1; double gHp = 0; float gT0 = -1, gKillT = -1; string gSp = "";
                while (!(R.phase == "end" && R.endT <= 0) && sim < 300)
                {
                    nt -= h;
                    if (nt <= 0) { noise = (float)(rnd.NextDouble() * 2 - 1) * skill * R.barLen; nt = 0.5f; }
                    MoreMush.Ball tgt = null;
                    foreach (var b in R.balls) if (b.perm && !b.dead && b.dy > 0 && (tgt == null || b.y > tgt.y)) tgt = b;
                    float tx = tgt != null ? tgt.x + noise : MoreMush.RoundSim.WW / 2;
                    tx = UnityEngine.Mathf.Clamp(tx, MoreMush.RoundSim.FX0 + R.barLen / 2, MoreMush.RoundSim.FX1 - R.barLen / 2);
                    R.barX = UnityEngine.Mathf.Lerp(R.barX, tx, 1 - UnityEngine.Mathf.Exp(-18 * h));
                    // 이번 프레임에 재생 대기열에서 시도가 일어날 항목 수 (값 <= h 이면 이번 프레임 UpdateEffects에서 시도)
                    int due = 0; if (R.phase == "run") for (int i = 0; i < R.regen.Count; i++) if (R.regen[i] - h <= 0) due++;
                    if (forceGiant && !giantChecked && R.giantState == "pending" && R.runT >= 3.9f)
                    {
                        giantChecked = true; giantChk++;
                        var pp = (UnityEngine.Vector2)miGiant.Invoke(R, null);
                        float cx = MoreMush.RoundSim.WW / 2, cy = (float)fiNoSpawn.GetValue(null) / 2;
                        if (System.Math.Abs(pp.x - cx) < 0.01f && System.Math.Abs(pp.y - cy) < 0.01f)
                        {
                            bool free = (bool)miArea.Invoke(R, new object[] { MoreMush.Shape.C(cx, cy, 90), null });
                            if (!free) giantFb++;
                        }
                    }
                    long f0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    R.SimFrame(h); sim += h;
                    if (R.giant != null && gS == null) { { int ft = k % 4; var cand = new System.Collections.Generic.List<MoreMush.Defs.Species>(); foreach (var spp in MoreMush.Defs.SPECIES) if (spp.t == ft && (spp.theme == null || spp.theme == R.st.theme.id)) cand.Add(spp); if (cand.Count > 0) { var ns = cand[(k / 4) % cand.Count]; double nhp = MoreMush.Defs.TIERS[0].hp * 40 * MoreMush.Defs.GIANT_TIER_HP[ft] * R.st.shroomHp; R.giant.sp = ns; R.giant.spore = ns.spore; R.giant.hp = nhp; R.giant.maxHp = nhp; } } gS = R.giant; gTier = gS.sp.t; gHp = gS.maxHp; gT0 = R.runT; gSp = gS.sp.nKo; }
                    if (gS != null && gKillT < 0 && (gS.dead || R.giant == null)) gKillT = R.runT;
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - f0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    frameMs.Add(ms);
                    if (due > 0) { attMs.Add(ms); attempts += due; }
                    if (R.phase == "run")
                    {
                        colSum += R.colonies.Count; colFrames++;
                        // 실패 = 이번 프레임 끝에 값이 정확히 0.5인 항목 (폭풍 L3 재생 0.25x2도 0.5라 그 칸은 실패 수를 못 센다)
                        if (due > 0 && System.Math.Abs(R.st.regen - 0.5) > 1e-6) for (int i = 0; i < R.regen.Count; i++) if (R.regen[i] == 0.5f) fails++;
                        var cur = new System.Collections.Generic.HashSet<MoreMush.Colony>(R.colonies);
                        foreach (var c in prevSet) if (!cur.Contains(c)) removedAt.Enqueue(R.runT);
                        foreach (var c in cur) if (!prevSet.Contains(c)) { births++; if (removedAt.Count > 0) delays.Add(R.runT - removedAt.Dequeue()); }
                        prevSet = cur;
                    }
                }
                var sm = R.Finish();
                frameMs.Sort(); double p99 = frameMs[(int)(frameMs.Count * 0.99)]; double fmax = frameMs[frameMs.Count - 1];
                double am = 0, amax = 0; foreach (var v in attMs) { am += v; if (v > amax) amax = v; } if (attMs.Count > 0) am /= attMs.Count;
                double dm = 0; foreach (var v in delays) dm += v; if (delays.Count > 0) dm /= delays.Count;
                sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7:F3},{8:F2},{9:F2},{10:F0},{11},{12},{13:F0},{14},{15},{16:F2},{17:F2},{18:F2},{19:F2},{20:F2},{21:F2},{22},{23},{24},{25},{26:F0},{27:F1},{28:F1},{29}",
                    sd, wth, k, stage, g.theme, maxCol, startCols, colS, planMs, colFrames > 0 ? colSum / colFrames : 0, sm.net, sm.harvests, R.shroomsMade, dur, attempts, fails, fmax, p99, am, amax, dm, dm - R.st.regen, births, giantFb, giantChk, gTier, gHp, gT0, gKillT, gSp));
            }
            return 0;
        });
        System.IO.File.AppendAllText(outDir + tag + "_rounds.csv", sb.ToString());
        System.IO.File.WriteAllText(outDir + tag + "_progress.txt", ji + "/" + nJobs + " " + System.DateTime.Now.ToString("HH:mm:ss"));
    }
    catch (System.Exception e)
    {
        for (int q=0;q<4;q++) MoreMush.Defs.GIANT_TIER_HP[q]=savedGt[q];
        System.IO.File.WriteAllText(outDir + tag + "_error.txt", e.ToString());
        System.IO.File.WriteAllText(outDir + tag + "_running.txt", "stopped");
        UnityEditor.EditorApplication.update -= tick.Invoke;
    }
};
UnityEditor.EditorApplication.update += tick.Invoke;
return "started";
