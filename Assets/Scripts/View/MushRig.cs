using UnityEngine;
using static MoreMush.SpiritRig2;

namespace MoreMush
{
    // 버섯 리그 (★0·★1 전용, 2026-10-18 웹 작업장 Tools/rig-web/mush_rig.js → 2026-10-19 C# 이식): 사람형 리그2(SpiritRig2)와 따로.
    // 몸 = 갓 + 대(얼굴) + 작은 발 둘 (+ ★1 몸 옆 동그란 손 둘). 무릎·사람 걷기·옆/뒷모습은 쓰지 않는다 (정면 + 뒤집기).
    // 맛: 통통 튀며 이동(폴짝 걸음) + 뒤뚱(좌우 기울기), 찌그러짐/늘어남이 핵심, 갓은 몸보다 늦게 따라와 출렁(관성), 착지 때 갓이 눌렸다 튐.
    // 동작 이름은 사람형과 같다 (게임이 같은 상태를 넘긴다). 상수 = spirit_rig2.json "mushMotion" (없으면 MushMotion 기본값).
    //
    // 계층 (템플릿 슬롯, Unity MCP로 만든 것): Mush(이 컴포넌트) ─ Root(발바닥 가운데: 뒤집기·공중·들림 회전)
    //       ─ Body(발바닥 축: 기울기·찌그러짐) ─ Stem(+Face) · NubPivot[i] ─ Nub[i] ─ NubSprite · Cap(갓 테두리 축) ─ CapSprite · Prop(짐)
    //       Foot[i] (Root 공간: 땅에 붙는다) ─ FootRot ─ FootSprite
    // 그리는 순서: 갓 -10 (대 뒤: 대 윗부분이 주름 속) · 발 -5 · 대 0 · 얼굴 10 · 손 20 · 짐 30
    public class MushRig : MonoBehaviour
    {
        [Header("계층 (템플릿 슬롯)")]
        public Transform root;
        public Transform body, capT;
        public SpriteRenderer stemSR, stemFace, capSR, propSR;
        public Transform[] nubPivot = new Transform[2], nubT = new Transform[2], foot = new Transform[2], footRot = new Transform[2];
        public SpriteRenderer[] nubSR = new SpriteRenderer[2], shoeSR = new SpriteRenderer[2];

        [System.NonSerialized] public int baseOrder;
        [System.NonSerialized] public float flip = 1, scale = 1;
        [System.NonSerialized] public Vector2 vel, grabAccel;
        [System.NonSerialized] public bool autoFidget = true, keepFront;
        [System.NonSerialized] public string waveSide = "R";
        [System.NonSerialized] public MushMotion M;

        public Rig2Look look { get; private set; }
        public string motion { get; private set; } = "idle";
        public MushPose pose { get; private set; }
        public SpiritRig2.WalkInfo walkInfo;

        string outfit = "";
        float motionT, time, climbS, stepPh, blendT = 99, pokeT = 99, H = 1, L = 1;
        MushPose prevPose;
        Rng rng = new Rng(11);
        string fidget; float fidgetT, fidgetWait = 2.5f;
        float blinkT = 2.2f; bool blink2;
        string shownFace;
        Vector2 capJ;
        readonly Vector2[] ankleRest = new Vector2[2];
        Spring sSq, sLean, sShift, sAir, capBob; Pendulum capP, hang;
        Spring[][] sNub;
        Vector2 prevCap, prevCapV; bool hasPrevCap, hasPrevCapV;

        void Awake() { rng = new Rng((uint)Random.Range(1, int.MaxValue)); Init(); }
        void Init() { if (M == null) { M = Rig2Data.Mush; ResetState(); } }

        public void ResetState()
        {
            time = 0; motionT = 0; climbS = 0; stepPh = 0;
            prevPose = null; blendT = 99; pose = new MushPose();
            sSq = new Spring(); sSq.Snap(1); sLean = new Spring(); sShift = new Spring(); sAir = new Spring();
            capP = new Pendulum(); capBob = new Spring(); hang = new Pendulum();
            sNub = new[] { new[] { new Spring(), new Spring(), new Spring() }, new[] { new Spring(), new Spring(), new Spring() } };
            hasPrevCap = hasPrevCapV = false;
            blinkT = 2.2f; blink2 = false; pokeT = 99; fidget = null; fidgetT = 0; fidgetWait = 2.5f;
            shownFace = null;
        }

        static Sprite Spr(string name) => string.IsNullOrEmpty(name) ? null : SpriteDB.Get("Characters/", name);
        bool HasNubs => look != null && look.nubL != null && !string.IsNullOrEmpty(look.nubL.sprite);
        static void Set(Transform t, float px, float py, float rot, float sx = 1, float sy = 1)
        {
            t.localPosition = new Vector3(px, py, 0);
            t.localRotation = Quaternion.Euler(0, 0, rot);
            t.localScale = new Vector3(sx, sy, 1);
        }
        static Vector2 V(float[] a) => a != null && a.Length >= 2 ? new Vector2(a[0], a[1]) : Vector2.zero;

        public void Apply(Rig2Look l, string outfit)
        {
            Init();
            look = l; this.outfit = outfit ?? "";
            Set(body, 0, 0, 0);
            stemSR.sprite = Spr(l.stem.sprite);
            Set(stemSR.transform, l.stem.x, l.stem.y, 0);
            stemFace.sprite = Spr(l.face + "normal");
            Set(stemFace.transform, 0, 0, 0);
            Vector2 C = V(l.capJoint), T = V(l.capTop);
            capJ = C;
            Set(capT, C.x, C.y, 0);
            capSR.sprite = Spr(l.cap.sprite);
            Set(capSR.transform, l.cap.x - C.x, l.cap.y - C.y, 0);
            var prop = Spr("Rig2/prop_basket");
            propSR.sprite = prop;
            float pw = prop != null ? prop.rect.width / prop.pixelsPerUnit : 1, ps = (l.height * 0.32f) / pw;
            Set(propSR.transform, T.x - C.x, T.y - C.y - 0.05f, 0, ps, ps);
            propSR.enabled = false;
            for (int i = 0; i < 2; i++)
            {
                var np = i == 0 ? l.nubL : l.nubR; var pv = i == 0 ? l.nubPivotL : l.nubPivotR;
                bool hasNub = np != null && !string.IsNullOrEmpty(np.sprite);
                nubPivot[i].gameObject.SetActive(hasNub);
                if (hasNub)
                {
                    Set(nubPivot[i], pv[0], pv[1], 0);
                    Set(nubT[i], 0, 0, 0);
                    nubSR[i].sprite = Spr(np.sprite);
                    Set(nubSR[i].transform, np.x - pv[0], np.y - pv[1], 0);
                }
                var fp = i == 0 ? l.footL : l.footR; var ank = V(i == 0 ? l.ankle : l.ankleR);
                foot[i].gameObject.SetActive(fp != null);
                if (fp != null)
                {
                    Set(foot[i], ank.x, ank.y, 0);
                    Set(footRot[i], 0, 0, 0);
                    shoeSR[i].sprite = Spr(fp.sprite);
                    Set(shoeSR[i].transform, fp.x - ank.x, fp.y - ank.y, 0);
                }
            }
            ankleRest[0] = V(l.ankle); ankleRest[1] = V(l.ankleR);
            H = l.height; L = Mathf.Max(0.2f, C.y * 0.5f);
            // 그리는 순서
            capSR.sortingOrder = baseOrder - 10; stemSR.sortingOrder = baseOrder; stemFace.sortingOrder = baseOrder + 10; propSR.sortingOrder = baseOrder + 30;
            for (int i = 0; i < 2; i++) { nubSR[i].sortingOrder = baseOrder + 20; shoeSR[i].sortingOrder = baseOrder - 5; }
            shownFace = null;
        }

        public void Play(string name)
        {
            if (name == motion && Rig2Motions.Loop(name)) return;
            prevPose = pose?.Clone(); blendT = 0;
            motion = name; motionT = 0; fidget = null;
            if (name == "held") { hang.a = 0; hang.v = 0; }
        }
        public void Poke() { pokeT = 0; }

        // 게임 프레임: 1/60초 이하 단계로 나눠 돈다 (웹 작업장과 같은 결과)
        public void Step(float dt)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 60 - 1e-3f), 1, 4);
            for (int i = 0; i < n; i++) Tick(dt / n);
        }

        public void Tick(float dt)
        {
            if (look == null || M == null) return;
            dt = Mathf.Min(dt, 0.05f);
            time += dt; motionT += dt; blendT += dt; pokeT += dt;
            var info = Rig2Motions.Get(motion);
            if (info != null && !info.loop && motionT >= info.len) Play("idle");
            if (motion == "idle" && autoFidget)
            {
                if (fidget == null) { fidgetWait -= dt; if (fidgetWait <= 0) { fidget = Rig2Motions.Fidgets[rng.Range(0, Rig2Motions.Fidgets.Length)]; fidgetT = 0; } }
                else { fidgetT += dt; if (fidgetT >= Rig2Motions.Get(fidget).len) { fidget = null; fidgetWait = rng.Range(M.fidgetMin, M.fidgetMax); } }
            }
            var p = new MushPose();
            Pose(motion, motionT, p, dt);
            if (motion == "idle" && fidget != null)
            {
                var q = p.Clone(); Pose(fidget, fidgetT, q, dt);
                float e = Smooth(Mathf.Min(fidgetT, Rig2Motions.Get(fidget).len - fidgetT) / 0.2f);
                p = MushPose.Mix(p, q, e); p.face = q.face;
            }
            if (prevPose != null && blendT < M.blend) p = MushPose.Mix(prevPose, p, Smooth(blendT / M.blend));
            pose = p;
            Solve(p, dt);
            UpdateFace(p, dt);
        }

        // ───── 동작 (mush_rig.js Pose) ─────
        void Pose(string name, float t, MushPose p, float dt)
        {
            var info = Rig2Motions.Get(name);
            float u = info != null && info.len > 0 ? Mathf.Clamp01(t / info.len) : 0;
            switch (name)
            {
                case "idle": PIdle(t, p); break;
                case "walk": PWalk(t, p, dt, false); break;
                case "carry": PWalk(t, p, dt, true); break;
                case "work": PWork(t, p); break;
                case "climb": PClimb(t, p, dt, 1, false); break;
                case "climbDown": PClimb(t, p, dt, -1, false); break;
                case "ladder": PClimb(t, p, dt, 1, true); break;
                case "ladderDown": PClimb(t, p, dt, -1, true); break;
                case "hop": PHop(Frac(t / M.hopPeriod), p, M.hopH, false); break;
                case "hopOnce": PIdle(t, p); PHop(u, p, M.hopH * 0.7f, false); break;
                case "jump": PHop(u, p, M.jumpH, true); break;
                case "surprise": PSurprise(u, p); break;
                case "spin": PSpin(u, p); break;
                case "land": PLand(u, p); break;
                case "dance": PDance(t, p); break;
                case "heart": PHeart(t, p); break;
                case "sleep": PSleep(t, p); break;
                case "held": PHeld(t, p); break;
                case "fall": PFall(t, p); break;
                case "wave": PIdle(t, p); PWave(u, t, p); break;
                case "stretch": PIdle(t, p); PStretch(u, p); break;
                case "look": PIdle(t, p); PLook(u, p); break;
                case "yawn": PIdle(t, p); PYawn(u, p); break;
                default: PIdle(t, p); break;
            }
        }

        // 숨: 늘었다 줄고 (부피 유지라 가로는 반대로), 아주 천천히 좌우로
        void PIdle(float t, MushPose p)
        {
            p.sqY = 1 + Mathf.Sin(t * 2 * Mathf.PI / M.breathPeriod) * M.breathAmt;
            p.lean = Mathf.Sin(t * 0.7f) * M.idleSway;
            p.nubY[0] = p.nubY[1] = Mathf.Sin(t * 2 * Mathf.PI / M.breathPeriod) * 0.01f;
            p.face = "normal";
        }

        // 걷기: 한 걸음마다 폴짝 (뜰 때 늘어나고, 닿을 때 찌그러짐) + 디딘 쪽으로 뒤뚱 기울기 + 가는 쪽으로 살짝 숙임.
        // 발은 번갈아 들고, 땅에 있는 발은 제자리 (몸이 그 위로 튐)
        void PWalk(float t, MushPose p, float dt, bool carry)
        {
            float rate = carry ? M.carryRate : M.stepRate;
            stepPh += dt * rate;
            int k = Mathf.FloorToInt(stepPh); float u = Frac(stepPh), side = k % 2 == 0 ? 1 : -1;
            float hop = (carry ? M.carryHop : M.stepHop) * H;
            float air = Bump(Seg(u, 0.12f, 0.88f));
            p.air = air * hop;
            float land = u < 0.12f ? 1 - u / 0.12f : u > 0.88f ? (u - 0.88f) / 0.12f : 0;   // 닿는 순간
            p.sqY = 1 - land * M.landSquash * (carry ? 1.4f : 1) + air * M.airStretch;
            p.lean = side * M.waddle * Mathf.Sin(u * Mathf.PI) + (keepFront ? 0 : M.walkLean);
            p.shiftX = side * 0.02f * H * Mathf.Sin(u * Mathf.PI);
            int up = side > 0 ? 0 : 1;                                                       // 오른쪽으로 기울 땐 왼발을 든다
            float lift = Bump(Seg(u, 0.05f, 0.8f)) * M.footStep * L;
            p.footY[up] = lift;
            p.footX[up] = (keepFront ? 0 : 0.06f) * H * (u - 0.5f);
            p.footRot[0] = p.footRot[1] = 0;
            if (HasNubs)
            {                                                                                 // ★1: 손을 번갈아 파닥
                p.nubRot[0] = Mathf.Sin(stepPh * Mathf.PI) * 18; p.nubRot[1] = -Mathf.Sin(stepPh * Mathf.PI) * 18;
                p.nubY[0] = p.nubY[1] = air * 0.03f * H;
            }
            if (carry)
            {
                p.prop = 1; p.face = "effort";
                if (HasNubs) { p.nubY[0] = p.nubY[1] = 0.42f * H; p.nubX[0] = 0.06f * H; p.nubX[1] = -0.06f * H; p.nubRot[0] = -60; p.nubRot[1] = 60; }   // 손으로 짐을 받침
            }
            else p.face = "normal";
            walkInfo = new SpiritRig2.WalkInfo { speed = M.walkSpeed * H, cadence = rate, D = 0, stepLen = M.walkSpeed * H / rate };
        }

        // 일하기: 꾸벅 숙였다 펴며 줍기. ★1은 손을 번갈아 위로 뻗음
        void PWork(float t, MushPose p)
        {
            float c = t / 1.0f, u = Frac(c); int k = Mathf.FloorToInt(c);
            float dip = Bump(Seg(u, 0.1f, 0.6f));
            p.lean = 14 * dip; p.sqY = 1 - 0.1f * dip + 0.06f * Bump(Seg(u, 0.6f, 0.9f));
            p.air = Bump(Seg(u, 0.62f, 0.9f)) * 0.03f * H;
            if (HasNubs)
            {
                int n = k % 2 == 0 ? 0 : 1; float sg = n == 0 ? -1 : 1;
                p.nubY[n] = 0.25f * H * Bump(Seg(u, 0.55f, 1)); p.nubRot[n] = sg * 40 * Bump(Seg(u, 0.55f, 1));
                p.nubX[n] = -sg * 0.05f * H * dip;
            }
            p.face = dip > 0.3f ? "effort" : "normal";
        }

        // 오르기: 폴짝폴짝 한 칸씩. 웅크림 → 위로 팡 (늘어남) → 다음 가로대에 착지 (찌그러짐). 사다리(뒤 그림 없음) = 정면, 줄기 = 줄기 쪽으로 기울임
        void PClimb(float t, MushPose p, float dt, int dir, bool ladder)
        {
            float G = ladder ? M.rungGap : M.trunkGap;
            const float period = 0.55f;
            float ph = t / period, u = Frac(ph);
            float crouch = Bump(Seg(u, 0, 0.3f)), air = Seg(u, 0.25f, 0.8f), land = Bump(Seg(u, 0.78f, 1));
            float step = G * 2;                                                               // 한 번에 두 칸
            // 몸이 뜬 동안 사다리(세상)가 한 걸음만큼 내려간다 → 손발이 다음 칸에 닿는다
            climbS = dir * (Mathf.Floor(ph) + Smooth(air)) * step;
            p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * M.climbHop * step : 0;
            p.sqY = 1 - crouch * 0.16f - land * 0.14f + (air > 0 && air < 0.5f ? Bump(air / 0.5f) * 0.14f : 0);
            p.lean = ladder ? 0 : M.climbLean;
            p.footY[0] = p.footY[1] = Bump(air) * 0.04f * H;
            if (HasNubs) { p.nubY[0] = p.nubY[1] = (0.2f + 0.15f * Bump(air)) * H; p.nubRot[0] = -50; p.nubRot[1] = 50; if (!ladder) { p.nubX[0] = p.nubX[1] = 0.08f * H; } }
            p.face = "effort";
        }

        // 폴짝/점프 (u 0~1): 웅크림(예비) → 늘어나며 뜀 → 공중(발 모음) → 착지 찌그러짐. 갓 출렁은 스프링이
        void PHop(float u, MushPose p, float h, bool big)
        {
            float crouch = Bump(Seg(u, 0, 0.28f)), land = Bump(Seg(u, 0.8f, 1)), air = Seg(u, 0.24f, 0.82f);
            p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * h * H : 0;
            p.sqY = 1 - crouch * M.crouch - land * M.crouch * (big ? 1.1f : 0.8f) + (air > 0 && air < 0.45f ? Bump(air / 0.45f) * (big ? 0.22f : 0.12f) : 0);
            p.footY[0] = p.footY[1] = Bump(air) * 0.05f * H;
            if (HasNubs) { float up = Smooth(Seg(u, 0.2f, 0.35f)) * (1 - Smooth(Seg(u, 0.75f, 0.9f))); p.nubY[0] = p.nubY[1] = up * 0.22f * H; p.nubRot[0] = -60 * up; p.nubRot[1] = 60 * up; }
            p.face = u < 0.2f ? "normal" : "happy";
            p.grounded = p.air <= 0;
        }

        void PSurprise(float u, MushPose p)
        {
            float pop = Seg(u, 0, 0.3f);
            p.air = pop < 1 ? 4 * pop * (1 - pop) * 0.08f * H : 0;
            float e = Smooth(Seg(u, 0, 0.1f)) * (1 - Smooth(Seg(u, 0.5f, 1)));
            p.sqY = 1 + 0.16f * e - Bump(Seg(u, 0.3f, 0.5f)) * 0.12f;
            if (HasNubs) { p.nubY[0] = p.nubY[1] = 0.3f * H * e; p.nubRot[0] = -80 * e; p.nubRot[1] = 80 * e; }
            p.face = "surprise";
        }

        void PSpin(float u, MushPose p)
        {
            float a = Seg(u, 0.05f, 0.6f);
            p.spin = Smooth(u); p.air = a > 0 && a < 1 ? 4 * a * (1 - a) * 0.1f * H : 0;
            p.sqY = 1 + 0.06f * Bump(u); p.face = "happy";
        }

        void PLand(float u, MushPose p)
        {
            float hit = Mathf.Exp(-u * 6);
            p.sqY = 1 - hit * M.landDip; p.lean = 0; p.face = u < 0.35f ? "surprise" : "happy";
            if (HasNubs) { p.nubY[0] = p.nubY[1] = hit * 0.15f * H; }
        }

        void PDance(float t, MushPose p)
        {
            float b = t / M.danceBeat, u = Frac(b); int k = Mathf.FloorToInt(b); float sg = k % 2 == 0 ? 1 : -1;
            float bu = Bump(u);
            p.sqY = 1 - bu * bu * 0.12f + Bump(Seg(u, 0.4f, 0.8f)) * 0.06f;
            p.air = Bump(Seg(u, 0.35f, 0.9f)) * 0.04f * H;
            p.lean = Mathf.Sin(Mathf.PI * b) * 10;
            if (HasNubs) { p.nubY[sg > 0 ? 0 : 1] = 0.3f * H; p.nubRot[0] = sg > 0 ? -70 : 10; p.nubRot[1] = sg > 0 ? -10 : 70; }
            p.footY[sg > 0 ? 0 : 1] = Bump(Seg(u, 0.1f, 0.6f)) * 0.12f * L;
            p.face = "happy";
        }

        void PHeart(float t, MushPose p)
        {
            float s = Mathf.Sin(t * 3);
            p.lean = s * 6; p.sqY = 1 + Mathf.Abs(Mathf.Sin(t * 6)) * 0.04f;
            if (HasNubs) { p.nubX[0] = 0.12f * H; p.nubX[1] = -0.12f * H; p.nubY[0] = p.nubY[1] = 0.08f * H; }
            p.face = "happy";
        }

        void PSleep(float t, MushPose p)
        {
            float br = Mathf.Sin(t * 2 * Mathf.PI / 4);
            p.sqY = 1 + br * 0.04f; p.lean = M.sleepLean + br; p.capRot = 6 + br * 2;
            p.face = "blink";
        }

        // 들림: 갓 꼭대기에 매달려 대롱대롱 (진자) · 몸이 무게로 늘어남 · 발 동동 · ★1 손 파닥
        void PHeld(float t, MushPose p)
        {
            p.hang = true; p.grounded = false;
            p.sqY = 1.14f + Mathf.Sin(t * 9) * 0.03f;
            float k1 = Mathf.Sin(t * 14), k2 = Mathf.Sin(t * 14 + 2.4f);
            p.footY[0] = Mathf.Max(0, k1) * M.heldKick * H - 0.03f * H; p.footY[1] = Mathf.Max(0, k2) * M.heldKick * H - 0.03f * H;
            p.lean = Mathf.Sin(t * 7) * 6;
            if (HasNubs) { p.nubRot[0] = -40 + Mathf.Sin(t * 22) * 35; p.nubRot[1] = 40 - Mathf.Sin(t * 22 + 1) * 35; p.nubY[0] = p.nubY[1] = 0.12f * H; }
            p.face = Mathf.FloorToInt(t * 2.2f) % 3 == 2 ? "effort" : "surprise";
        }

        void PFall(float t, MushPose p)
        {
            p.grounded = false; p.sqY = 1.12f;
            p.footY[0] = -0.02f * H + Mathf.Max(0, Mathf.Sin(t * 16)) * 0.04f * H; p.footY[1] = -0.02f * H + Mathf.Max(0, -Mathf.Sin(t * 16)) * 0.04f * H;
            if (HasNubs) { p.nubY[0] = p.nubY[1] = 0.3f * H; p.nubRot[0] = -90 + Mathf.Sin(t * 26) * 10; p.nubRot[1] = 90 - Mathf.Sin(t * 26) * 10; }
            p.face = "surprise";
        }

        // 손 흔들기: ★1 = 한 손을 들어 흔듦, ★0 = 몸을 좌우로 흔들며 살짝 폴짝
        void PWave(float u, float t, MushPose p)
        {
            float e = Smooth(Seg(u, 0.05f, 0.25f)) * (1 - Smooth(Seg(u, 0.8f, 1)));
            if (HasNubs)
            {
                int n = waveSide == "L" ? 0 : 1; float sg = n == 0 ? -1 : 1;
                p.nubY[n] = 0.28f * H * e; p.nubX[n] = sg * 0.02f * H * e; p.nubRot[n] = sg * (60 + Mathf.Sin(t * 16) * 30) * e;
                p.lean += sg * 4 * e;
            }
            else
            {
                p.lean += Mathf.Sin(t * 10) * 9 * e; p.air += Mathf.Abs(Mathf.Sin(t * 10)) * 0.02f * H * e;
            }
            p.face = "happy";
        }

        void PStretch(float u, MushPose p)
        {
            float e = Smooth(Seg(u, 0.05f, 0.4f)) * (1 - Smooth(Seg(u, 0.7f, 1)));
            p.sqY = 1 + 0.16f * e; p.lean = 0;
            if (HasNubs) { p.nubY[0] = p.nubY[1] = 0.3f * H * e; p.nubRot[0] = -80 * e; p.nubRot[1] = 80 * e; }
            p.face = e > 0.4f ? "blink" : "normal";
        }

        void PLook(float u, MushPose p)
        {
            float a = Smooth(Seg(u, 0.05f, 0.25f)) - Smooth(Seg(u, 0.4f, 0.6f)) * 2 + Smooth(Seg(u, 0.75f, 0.95f));
            p.lean += a * 9; p.shiftX += a * 0.02f * H; p.face = "normal";
        }

        void PYawn(float u, MushPose p)
        {
            float e = Smooth(Seg(u, 0.1f, 0.35f)) * (1 - Smooth(Seg(u, 0.7f, 0.95f)));
            p.lean += -6 * e; p.sqY = 1 + 0.08f * e;
            if (HasNubs) { p.nubY[0] = 0.18f * H * e; p.nubX[0] = 0.08f * H * e; p.nubRot[0] = -50 * e; }
            p.face = e > 0.3f ? "blink" : "normal";
        }

        // ───── 풀기 (mush_rig.js Solve) ─────
        void Solve(MushPose p, float dt)
        {
            var l = look;
            sSq.Step(p.sqY, M.squashK, M.squashZ, dt);                            // 젤리: 넘쳤다 돌아옴
            sLean.Step(p.lean, M.leanK, M.leanZ, dt);
            sShift.Step(p.shiftX, M.leanK, M.leanZ, dt);
            sAir.Step(p.air, 900, 1, dt);
            float air = p.air > 0 ? p.air : Mathf.Max(0, sAir.x);
            float spinC = Mathf.Cos(p.spin * 2 * Mathf.PI);
            float rootRot = 0; var rootPos = new Vector2(0, air);
            if (p.hang)
            {
                var capTop = V(l.capTop);
                hang.Step(0, 26, 0.12f, -grabAccel.x * 4, dt);
                rootRot = Mathf.Clamp(hang.a, -55, 55);
                float r = rootRot * Mathf.Deg2Rad;
                rootPos = capTop - new Vector2(capTop.x * Mathf.Cos(r) - capTop.y * Mathf.Sin(r), capTop.x * Mathf.Sin(r) + capTop.y * Mathf.Cos(r));
            }
            Set(root, rootPos.x, rootPos.y, rootRot, scale * flip * (Mathf.Abs(spinC) < 0.06f ? 0.06f * Mathf.Sign(spinC) : spinC), scale);
            // 몸: 발바닥 축으로 기울고(lean + = 오른쪽/가는 쪽) 찌그러짐 (부피 유지: 가로 = 1/√세로)
            float sy = Mathf.Clamp(sSq.x, 0.55f, 1.6f), sx = Mathf.Pow(1 / sy, 0.5f * M.volume);   // volume 1 = 부피 그대로
            float jig = pokeT < 1.2f ? 0.14f * Mathf.Exp(-5 * pokeT) * Mathf.Sin(pokeT * 26) : 0;
            Set(body, sShift.x, 0, -sLean.x, sx * (1 + jig * 0.4f), sy * (1 - jig * 0.4f));
            stemFace.enabled = spinC >= 0;
            // 갓: 진자 (몸 기울기를 늦게 따라감 + 갓 테두리 점의 가속도) + 세로 출렁 (가속도로 눌렸다 튐)
            var cp = RootSpace(capT) + new Vector2(vel.x * time, 0);
            var v = hasPrevCap ? (cp - prevCap) / Mathf.Max(dt, 1e-4f) : Vector2.zero;
            var a = hasPrevCapV ? (v - prevCapV) / Mathf.Max(dt, 1e-4f) : Vector2.zero;
            prevCap = cp; prevCapV = v; hasPrevCap = hasPrevCapV = true;
            float ax = Mathf.Clamp(a.x, -80, 80), ay = Mathf.Clamp(a.y, -80, 80);
            // 갓의 기울기(세상 기준)는 몸 기울기를 늦게 따라간다 → 몸에 붙은 갓의 자기 회전 = (늦은 기울기 - 몸 기울기). capFollow = 따라가는 정도
            capP.Step(sLean.x * M.capFollow + p.capRot, M.capK, M.capZ, ax * M.capInertia * 10, dt);   // + = 오른쪽으로 (시계 방향)
            capBob.Step(0, M.capBobK, M.capBobZ, dt);
            capBob.v += -ay * M.capBobInertia * dt * 60;
            float bob = Mathf.Clamp(capBob.x, -0.25f, 0.25f);
            float cs = Mathf.Clamp(-bob * M.capSquash, -0.14f, 0.14f) + jig * 0.6f;
            // 갓은 몸 찌그러짐을 그대로 받지 않고 자기 출렁만 (단단한 갓)
            Set(capT, capJ.x, capJ.y + bob * 0.4f * H * 0.1f, -Mathf.Clamp(capP.a - sLean.x, -M.capMaxRot, M.capMaxRot), (1 + cs) / sx, (1 - cs) / sy);
            propSR.enabled = p.prop > 0.5f;
            // 손 (★1): 붙는 점 기준 위치·회전, 조금 늦게
            for (int i = 0; i < 2; i++)
            {
                if (!nubPivot[i].gameObject.activeSelf) continue;
                var s = sNub[i];
                s[0].Step(p.nubX[i], M.nubK, M.nubZ, dt); s[1].Step(p.nubY[i], M.nubK, M.nubZ, dt); s[2].Step(p.nubRot[i], M.nubK, M.nubZ, dt);
                Set(nubT[i], s[0].x / sx, s[1].x / sy, s[2].x, 1 / sx, 1 / sy);           // 손은 몸 찌그러짐을 덜 받는다
            }
            // 발: 땅에 붙는다 (몸이 찌그러지면 발 간격이 벌어짐). 공중이면 몸을 따라감
            for (int i = 0; i < 2; i++)
            {
                if (!foot[i].gameObject.activeSelf) continue;
                var rest = ankleRest[i];
                float y = rest.y + p.footY[i];
                Set(foot[i], rest.x * sx + p.footX[i] + sShift.x * 0.5f, y, p.footRot[i]);
            }
        }

        // root 바깥 공간 (발바닥 원점, root 변환 적용 후)
        Vector2 RootSpace(Transform t)
        {
            var m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
            for (var x = t.parent; x != null && x != transform; x = x.parent) m = Matrix4x4.TRS(x.localPosition, x.localRotation, x.localScale) * m;
            return m.MultiplyPoint3x4(Vector3.zero);
        }

        void UpdateFace(MushPose p, float dt)
        {
            string face = p.face ?? "normal";
            blinkT -= dt;
            if (blinkT <= 0) { blink2 = !blink2 && rng.value < 0.25f; blinkT = blink2 ? 0.28f : rng.Range(2.2f, 4.8f); }
            if (pokeT < 0.25f) face = "surprise"; else if (pokeT < 1.1f) face = "happy";
            if (blinkT < 0.13f && (face == "normal" || face == "effort")) face = "blink";
            if (face == shownFace) return;
            shownFace = face;
            stemFace.sprite = Spr(look.face + face) ?? Spr(look.face + "normal");
        }
    }

    // 버섯 리그(★0·★1) 동작 상수 (Tools/rig-web에서 맞춘 값 = mush_rig.js MUSH_DEFAULTS). spirit_rig2.json "mushMotion"이 있으면 그 값을 쓴다
    [System.Serializable, UnityEngine.Scripting.Preserve]
    public class MushMotion
    {
        public float blend = 0.18f;
        // 몸 젤리 (찌그러짐 스프링: 감쇠가 낮을수록 출렁) · 기울기 · 공중
        public float squashK = 260, squashZ = 0.28f, leanK = 180, leanZ = 0.55f, volume = 0.8f;
        // 갓: 진자(옆 흔들림) · 세로 출렁(눌림) — 몸보다 늦게
        public float capK = 70, capZ = 0.24f, capInertia = 2, capFollow = 0.92f, capMaxRot = 4, capBobK = 220, capBobZ = 0.24f, capBobInertia = 0.01f, capSquash = 0.8f;
        // 손 (★1): 늦게 따라옴
        public float nubK = 260, nubZ = 0.45f;
        // 대기
        public float breathPeriod = 2.6f, breathAmt = 0.025f, idleSway = 2, fidgetMin = 3, fidgetMax = 7;
        // 걷기 (폴짝 + 뒤뚱)
        public float walkSpeed = 0.9f, stepRate = 4.2f, stepHop = 0.045f, waddle = 7, walkLean = 5, landSquash = 0.12f, airStretch = 0.07f, footStep = 0.18f;
        public float carryRate = 3.2f, carryHop = 0.025f;
        // 뛰기·착지·폴짝
        public float jumpH = 0.42f, crouch = 0.2f, hopH = 0.16f, hopPeriod = 0.5f, landDip = 0.3f;
        // 오르기 (폴짝폴짝 튀어 오르기)
        public float rungGap = 0.26f, ladderHalfW = 0.95f, trunkReach = 0.55f, trunkGap = 0.3f, climbHop = 0.55f, climbLean = 12;
        // 기타
        public float danceBeat = 0.45f, sleepLean = 8, heldKick = 0.12f;
    }

    // 버섯 리그 목표 자세 (mush_rig.js MUSH_POSE_KEYS). [0] = L · [1] = R
    public class MushPose
    {
        public float air, sqY = 1, lean, shiftX, capRot, capY, spin, prop;
        public float[] nubX = new float[2], nubY = new float[2], nubRot = new float[2], footX = new float[2], footY = new float[2], footRot = new float[2];
        public string face = "normal";
        public bool hang, grounded = true;

        public MushPose Clone()
        {
            var o = (MushPose)MemberwiseClone();
            o.nubX = (float[])nubX.Clone(); o.nubY = (float[])nubY.Clone(); o.nubRot = (float[])nubRot.Clone();
            o.footX = (float[])footX.Clone(); o.footY = (float[])footY.Clone(); o.footRot = (float[])footRot.Clone();
            return o;
        }

        static float Lu(float a, float b, float t) => a + (b - a) * t;
        static void Lu(float[] o, float[] a, float[] b, float t) { o[0] = Lu(a[0], b[0], t); o[1] = Lu(a[1], b[1], t); }
        public static MushPose Mix(MushPose a, MushPose b, float t)
        {
            var o = b.Clone();
            o.air = Lu(a.air, b.air, t); o.sqY = Lu(a.sqY, b.sqY, t); o.lean = Lu(a.lean, b.lean, t); o.shiftX = Lu(a.shiftX, b.shiftX, t);
            o.capRot = Lu(a.capRot, b.capRot, t); o.capY = Lu(a.capY, b.capY, t); o.spin = Lu(a.spin, b.spin, t); o.prop = Lu(a.prop, b.prop, t);
            Lu(o.nubX, a.nubX, b.nubX, t); Lu(o.nubY, a.nubY, b.nubY, t); Lu(o.nubRot, a.nubRot, b.nubRot, t);
            Lu(o.footX, a.footX, b.footX, t); Lu(o.footY, a.footY, b.footY, t); Lu(o.footRot, a.footRot, b.footRot, t);
            o.face = t < 0.5f ? a.face : b.face; o.hang = b.hang; o.grounded = b.grounded;
            return o;
        }
    }
}
