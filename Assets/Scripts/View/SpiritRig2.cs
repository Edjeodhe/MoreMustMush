using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MoreMush
{
    // 정령 리그2 (2026-10-18 웹 작업장 Tools/rig-web/rig2.js → 2026-10-19 C# 이식): 버섯 정령 ★2·★3 사람형 관절 리그.
    // rig2.js와 이름·계층·계산 순서를 1:1로 맞췄다 (unity.js의 Transform/SpriteRenderer/Mathf 흉내 → 진짜 Unity).
    // 웹에서 고친 움직임은 rig2.js를 고치고 여기도 같은 줄을 고친다.
    //
    // 데이터: Assets/Resources/Rig/spirit_rig2.json  { looks: [Rig2Look...], motion: Rig2Motion, mushMotion: MushMotion }
    //   좌표 = 리그 단위 (그림 px / 100), 원점 = 발바닥 가운데, y 위. 부품 x,y = 서 있는 자세(바인드)에서 그림 중심.
    //   팔·다리는 보는 쪽 왼쪽(L) 하나만 적고, 오른쪽은 어깨/엉덩이를 축으로 뒤집어 쓴다 (그림도 좌우 반전).
    //
    // 계층 (템플릿 슬롯, Unity MCP로 만든 것. 코드는 만들지 않는다):
    //   Rig2(이 컴포넌트: 크기·자리는 CritterRig가) ─ Root(발바닥, 크기·찌그러짐·공중) ─ Hips(허리: 골반 이동·기울기)
    //      ├ Skirt(허리 축 진자) · Chest(허리 축 상체 굽힘·숨) ─ Collar
    //      │     ├ Neck(목 축 고개) ─ Bounce(갓 통통) ─ HeadSprite ─ Face   /  HairRoot(정수리 축 진자) ─ Hair
    //      │     └ Shoulder[i](뒤집기만) ─ Upper[i](어깨 각) ─ Elbow[i] ─ Wrist[i]
    //      └ Hip[i](뒤집기만) ─ Leg[i](무릎 없는 예전 다리)
    //   Thigh[i] · Shin[i] (Root 공간, IK 결과)   Foot[i](Root 공간, 발목 목표 위치) ─ FootRot[i] ─ Shoe   ← 발은 땅에 붙인다
    // 그리는 순서 (sortingOrder, 뒤→앞): 뒷머리 -80 · 먼 쪽 팔 -70~-50 · 다리 -30 · 신발 -20 · 치마 0 · 가슴 10 · 머리 40 · 얼굴 50 · 팔 80~100 · 목 프릴 120
    // ★0·★1은 MushRig (MushRig.cs). rig2.js의 SolveHeadless·Joints·Reapply는 웹 전용이라 옮기지 않았다.
    //
    // 움직임 = ① 동작 함수가 목표 자세(Rig2Pose)를 만든다 → ② 동작이 바뀌면 이전 자세에서 섞는다(blend)
    //          → ③ 팔·몸은 스프링으로 부드럽게(팔꿈치는 조금 늦게 = 겹침) → ④ 2차 움직임(머리카락·치마·손)은
    //          관절점의 실제 가속도로 흔들리는 진자 (무게감·관성·따라오는 동작) → ⑤ 다리는 IK로 발을 땅에 붙인다.
    public class SpiritRig2 : MonoBehaviour
    {
        [Header("계층 (템플릿 슬롯)")]
        public Transform root;
        public Transform hips, skirtT, chest, neck, bounce, hairT;
        public SpriteRenderer skirtSR, chestSR, collarSR, headSR, faceSR, hairSR;
        public Transform[] shoulder = new Transform[2], upper = new Transform[2], elbow = new Transform[2], wrist = new Transform[2];
        public SpriteRenderer[] upperSR = new SpriteRenderer[2], foreSR = new SpriteRenderer[2], handSR = new SpriteRenderer[2];
        public Transform[] hip = new Transform[2], thigh = new Transform[2], thighT = new Transform[2], shinT = new Transform[2], foot = new Transform[2], footRot = new Transform[2];
        public SpriteRenderer[] legSR = new SpriteRenderer[2], thighSR = new SpriteRenderer[2], shinSR = new SpriteRenderer[2], shoeSR = new SpriteRenderer[2];
        [Header("2026-10-19 (비어 있으면 그 기능만 꺼짐)")]
        public SpriteRenderer capOverSR;                                   // 갓 겉만 오린 그림(<머리>_cap): 머리카락 위에 갓을 다시 덮는다 (headSR 자식)
        public Transform[] sleeveT = new Transform[2];                     // 소매 (shoulder 자식): 위팔 회전의 sleeveFollow만큼 돈다
        public SpriteRenderer[] sleeveSR = new SpriteRenderer[2];
        public MeshFilter hairSkinMF, hairSkinLowMF;                       // 머리카락 한 장 뼈 사슬 스킨 (neck 자식, 재질 = Sprites-Default). Low = 옆모습 어깨 아래 줄 (몸통 뒤)
        public MeshRenderer hairSkinMR, hairSkinLowMR;

        // ───── 바깥에서 정하는 값 ─────
        [System.NonSerialized] public int baseOrder;               // sortingOrder 기준 (CritterRig 갓 그림 순서)
        [System.NonSerialized] public float flip = 1, scale = 1;
        [System.NonSerialized] public Vector2 vel;                 // 이동 속도 (리그 단위/초, 바깥에서): 머리카락이 뒤로 날린다
        [System.NonSerialized] public Vector2 grabAccel;           // 들고 흔드는 손의 가속도 (들림)
        [System.NonSerialized] public bool autoFidget = true, keepFront, holdOneShot;
        [System.NonSerialized] public string waveSide = "R";

        [System.NonSerialized] public Rig2Motion M;               // 동작 상수 (spirit_rig2.json "motion", 없으면 기본값)
        public Rig2Look look { get; private set; }
        public string motion { get; private set; } = "idle";
        public Rig2Pose pose { get; private set; }
        public WalkInfo walkInfo;                                  // 걷기 속도 (바깥에서 vel을 정할 때)
        public struct WalkInfo { public float speed, cadence, D, stepLen; }

        Rig2Look lv; string outfit = "", viewKey = "front";
        float motionT, time, gait, climbS, blendT = 99, pokeT = 99;
        Rig2Pose prevPose;
        Rng rng = new Rng(7);
        string fidget; float fidgetT, fidgetWait = 2.5f;
        float blinkT = 2.2f; bool blink2;
        string shownFace; Rig2Look shownFaceLook;

        // 바인드 값 (ApplyHeaded)
        float mR = -1, lift, upperLen, foreLen, legLen, thighLen, shinLen, L = 1, H = 1, turnPinchNow;
        Vector2 hipBind, legBind, thighBind, shinBind;
        bool kneeOn;
        readonly Vector2[] ankleRest = new Vector2[2];
        readonly float[] legStretch = { 1, 1 };

        Spring[] sRaise, sBend, sHip; Spring sChest, sHead, sTurn, sAir, sSqX, sSqY;
        Pendulum hairP, skirtP, capP, hang; Pendulum[] handP;
        // 머리카락 뼈 사슬 (rig2.js hairBone·MakeHairSkin): 뼈 0 = hairT, 1… 는 계산만 (목 공간 행렬)
        const int MaxBones = 6, SkinStrip = 3;
        readonly Pendulum[] hairSegP = new Pendulum[MaxBones];
        readonly float[] boneRot = new float[MaxBones];
        readonly Vector2[] boneRest = new Vector2[MaxBones];               // 쉬는 자리 (목 공간)
        readonly Vector2[] boneOff = new Vector2[MaxBones];                // 윗뼈에서 이 뼈까지
        readonly Matrix4x4[] boneM = new Matrix4x4[MaxBones];
        readonly Vector2[] bonePrevP = new Vector2[MaxBones], bonePrevV = new Vector2[MaxBones];
        bool hairSkin, boneHasPrev; int nBones = 1;
        Sprite skinSprite; float skinHairY; int skinSplit = -1;
        Mesh skinMesh, skinLowMesh; Vector3[] skinRestV, skinV; MaterialPropertyBlock skinMPB;
        readonly float[] prevForeRot = new float[2], prevForeVel = new float[2];
        // 진자 가속도용 관절점 (hair · waist · w0 · w1)
        readonly Vector2[] prevPts = new Vector2[4], prevVel = new Vector2[4];
        bool hasPrevPts, hasPrevVel;

        void Awake() { rng = new Rng((uint)Random.Range(1, int.MaxValue)); Init(); }
        void Init() { if (M == null) { M = Rig2Data.Motion; ResetState(); } }

        public void ResetState()
        {
            time = 0; motionT = 0; gait = 0; climbS = 0;
            prevPose = null; blendT = 99; pose = Rig2Pose.Rest(M);
            sRaise = new[] { new Spring(), new Spring() }; sBend = new[] { new Spring(), new Spring() };
            sHip = new[] { new Spring(), new Spring(), new Spring() }; sChest = new Spring(); sHead = new Spring(); sTurn = new Spring();
            sAir = new Spring(); sSqX = new Spring(); sSqY = new Spring();
            sSqX.Snap(1); sSqY.Snap(1);
            for (int i = 0; i < 2; i++) { sRaise[i].Snap(M.armRest); sBend[i].Snap(M.elbowRest); }
            for (int i = 0; i < MaxBones; i++) { hairSegP[i] = new Pendulum(); boneRot[i] = 0; }
            boneHasPrev = false;
            hairP = new Pendulum(); skirtP = new Pendulum(); handP = new[] { new Pendulum(), new Pendulum() }; capP = new Pendulum();
            hang = new Pendulum();
            hasPrevPts = hasPrevVel = false; prevForeRot[0] = prevForeRot[1] = prevForeVel[0] = prevForeVel[1] = 0;
            blinkT = 2.2f; blink2 = false; pokeT = 99; fidget = null; fidgetT = 0; fidgetWait = 2.5f;
            shownFace = null;
        }

        static Sprite Spr(string name) => string.IsNullOrEmpty(name) ? null : SpriteDB.Get("Characters/", name);
        // 작업복(outfit)이 있으면 그 그림, 없으면 기본
        Sprite OutfitSprite(string name) => (outfit != "" && !string.IsNullOrEmpty(name) ? Spr(name + "_" + outfit) : null) ?? Spr(name);

        static void Set(Transform t, float px, float py, float rot, float sx = 1, float sy = 1)
        {
            t.localPosition = new Vector3(px, py, 0);
            t.localRotation = Quaternion.Euler(0, 0, rot);
            t.localScale = new Vector3(sx, sy, 1);
        }
        static void Rot(Transform t, float deg) => t.localRotation = Quaternion.Euler(0, 0, deg);
        static Vector2 V(float[] a) => a != null && a.Length >= 2 ? new Vector2(a[0], a[1]) : Vector2.zero;
        void Order(SpriteRenderer sr, int o) => sr.sortingOrder = baseOrder + o;

        // ───── 모습 세우기 (rig2.js Apply) ─────
        public void Apply(Rig2Look l, string outfit)
        {
            Init();
            look = l; this.outfit = outfit ?? "";
            for (int i = 0; i < 2; i++) foot[i].gameObject.SetActive(l.shoe != null && !string.IsNullOrEmpty(l.shoe.sprite));
            for (int i = 0; i < 2; i++) { thighT[i].gameObject.SetActive(true); shinT[i].gameObject.SetActive(true); }
            viewKey = "front"; lv = l;
            ApplyHeaded(l, true);
            shownFace = null;
            // 고정 순서 (SolveHeaded가 바꾸는 것은 거기서)
            Order(skirtSR, 0); Order(chestSR, 10); Order(collarSR, 120); Order(headSR, 40); Order(faceSR, 50); Order(hairSR, -80);
            if (capOverSR != null) Order(capOverSR, 47);
        }

        // ───── 보는 방향 (정면 front · 옆 side · 뒤 back) ─────
        // 옆·뒤 그림은 look.side / look.back 에 정면과 같은 이름의 칸만 덮어쓴다 (없으면 정면 값). 옆모습은 오른쪽을 보고, 왼쪽은 root 뒤집기.
        Rig2Look ViewData(string key) => key == "front" ? look : look.View(key) ?? look;
        bool HasView(string key) => key == "front" || (look != null && look.View(key) != null);
        void SetView(string key)
        {
            if (!HasView(key)) key = "front";
            if (key == viewKey) return;
            viewKey = key; lv = ViewData(key);
            ApplyHeaded(lv, key != "side");          // 옆모습은 두 팔다리가 같은 쪽을 본다 (뒤집지 않음)
        }

        void ApplyHeaded(Rig2Look l, bool mirror)
        {
            mR = mirror ? -1 : 1;
            Vector2 W = V(l.waist), N = V(l.neck), SL = V(l.shoulderL), E = V(l.elbow), Wr = V(l.wrist), HL = V(l.hipL), A = V(l.ankle);
            Set(hips, W.x, W.y + lift, 0);
            Set(skirtT, 0, 0, 0);
            skirtSR.sprite = OutfitSprite(l.skirt.sprite);
            Set(skirtSR.transform, l.skirt.x - W.x, l.skirt.y - W.y, 0);
            Set(chest, 0, 0, 0);
            chestSR.sprite = OutfitSprite(l.chest.sprite);
            Set(chestSR.transform, l.chest.x - W.x, l.chest.y - W.y, 0);
            collarSR.sprite = l.collar != null && !string.IsNullOrEmpty(l.collar.sprite) ? OutfitSprite(l.collar.sprite) : null;
            if (l.collar != null) Set(collarSR.transform, l.collar.x - W.x, l.collar.y - W.y, 0);
            Set(neck, N.x - W.x, N.y - W.y, 0);
            Set(bounce, 0, 0, 0);
            headSR.sprite = Spr(l.head.sprite);
            Set(headSR.transform, l.head.x - N.x, l.head.y - N.y, 0);
            faceSR.sprite = !string.IsNullOrEmpty(l.face) ? Spr(l.face + "normal") : null;
            Set(faceSR.transform, 0, 0, 0);
            if (capOverSR != null) { capOverSR.sprite = l.head != null && !string.IsNullOrEmpty(l.head.sprite) ? Spr(l.head.sprite + "_cap") : null; Set(capOverSR.transform, 0, 0, 0); }
            var HR0 = V(l.hairRoot);
            Set(hairT, HR0.x - N.x, HR0.y - N.y, 0);
            // 머리카락: hairBones(2개 이상)와 스킨 슬롯이 있으면 뼈 사슬 스킨, 없으면 예전처럼 한 장을 뿌리 진자로
            var hsp = Spr(l.hair?.sprite);
            hairSkin = l.hairBones != null && l.hairBones.Length >= 2 && hsp != null && hairSkinMF != null && hairSkinMR != null;
            hairSR.sprite = hairSkin ? null : hsp;
            Set(hairSR.transform, (l.hair?.x ?? 0) - HR0.x, (l.hair?.y ?? 0) - HR0.y, 0);
            SetupHairSkin(l, N, SL, hairSkin ? hsp : null);
            // 길이 (IK)
            upperLen = (E - SL).magnitude; foreLen = (Wr - E).magnitude;
            // 무릎: 허벅지(엉덩이→무릎) · 정강이(무릎→발목).
            //  lift가 적혀 있으면: 몸(엉덩이 위)을 lift만큼 올려 그리고, 다리 그림·무릎·발목은 땅 좌표 그대로 (긴 다리 그림)
            //  없으면(예전 잘라 쓴 다리): legScale만큼 다리를 늘이고 몸을 그만큼 올린다
            float ls = l.legScale is float lsv && lsv != 0 ? lsv : 1; bool liftArt = l.lift.HasValue;
            lift = liftArt ? l.lift.Value : (HL.y - A.y) * (ls - 1);
            var HLb = liftArt ? HL + new Vector2(0, lift) : HL;            // 다리 바인드의 엉덩이
            hipBind = HLb;
            legBind = A - HLb; legLen = legBind.magnitude;
            var K = l.knee != null ? V(l.knee) : (HLb + A) * 0.5f;
            kneeOn = l.thigh != null && !string.IsNullOrEmpty(l.thigh.sprite) && Spr(l.thigh.sprite) != null;
            thighBind = K - HLb; shinBind = A - K;
            float lk = liftArt ? 1 : ls;
            thighLen = thighBind.magnitude * lk; shinLen = shinBind.magnitude * lk;
            L = Mathf.Max(0.2f, HL.y + lift - (l.sole ?? 0));      // 다리 길이 (lift를 더한 엉덩이 ~ 발바닥): 걸음·점프 단위
            H = l.height;
            for (int i = 0; i < 2; i++)
            {
                float m = i == 0 ? 1 : mR;
                Vector2 S = V(i == 0 ? l.shoulderL : l.shoulderR), Hp = V(i == 0 ? l.hipL : l.hipR);
                Set(shoulder[i], S.x - W.x, S.y - W.y, 0, m, 1);
                Set(upper[i], 0, 0, 0);
                upperSR[i].sprite = Spr(l.upperArm.sprite);
                Set(upperSR[i].transform, l.upperArm.x - SL.x, l.upperArm.y - SL.y, 0);
                Set(elbow[i], E.x - SL.x, E.y - SL.y, 0);
                foreSR[i].sprite = Spr(l.foreArm.sprite);
                Set(foreSR[i].transform, l.foreArm.x - E.x, l.foreArm.y - E.y, 0);
                Set(wrist[i], Wr.x - E.x, Wr.y - E.y, 0);
                handSR[i].sprite = Spr(l.hand.sprite);
                Set(handSR[i].transform, l.hand.x - Wr.x, l.hand.y - Wr.y, 0);
                if (sleeveT[i] != null && sleeveSR[i] != null)
                {
                    Set(sleeveT[i], 0, 0, 0);
                    sleeveSR[i].sprite = l.sleeve != null && !string.IsNullOrEmpty(l.sleeve.sprite) ? OutfitSprite(l.sleeve.sprite) : null;
                    if (l.sleeve != null) Set(sleeveSR[i].transform, l.sleeve.x - SL.x, l.sleeve.y - SL.y, 0);
                }
                Set(hip[i], Hp.x - W.x, Hp.y - W.y, 0, m, 1);
                Set(thigh[i], 0, 0, 0);
                legSR[i].sprite = kneeOn ? null : Spr(l.leg?.sprite);
                if (l.leg != null) Set(legSR[i].transform, l.leg.x - HL.x, l.leg.y - HL.y, 0);
                thighSR[i].sprite = kneeOn ? Spr(l.thigh.sprite) : null;
                Set(thighSR[i].transform, l.thigh != null ? l.thigh.x - HLb.x : 0, l.thigh != null ? l.thigh.y - HLb.y : 0, 0);
                shinSR[i].sprite = kneeOn ? Spr(l.shin.sprite) : null;
                Set(shinSR[i].transform, l.shin != null ? l.shin.x - K.x : 0, l.shin != null ? l.shin.y - K.y : 0, 0);
                Set(foot[i], 0, 0, 0, m, 1);
                Set(footRot[i], 0, 0, 0);
                shoeSR[i].sprite = Spr(l.shoe.sprite);
                Set(shoeSR[i].transform, l.shoe.x - A.x, l.shoe.y - A.y, 0);
            }
            ankleRest[0] = A; ankleRest[1] = new Vector2(V(l.hipR).x + mR * (A.x - HL.x), A.y);
        }

        // ───── 동작 바꾸기: 지금 자세에서 섞어 넘어간다 ─────
        public void Play(string name)
        {
            if (name == motion && Rig2Motions.Loop(name)) return;
            prevPose = pose?.Clone(); blendT = 0;
            motion = name; motionT = 0; fidget = null;
            if (name == "held") { hang.a = 0; hang.v = 0; }
        }
        public void Poke() { pokeT = 0; }

        // 게임 프레임: 웹 작업장처럼 1/60초 이하 단계로 나눠 돈다 (스프링·진자가 같은 결과가 되게)
        public void Step(float dt)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 60 - 1e-3f), 1, 4);
            for (int i = 0; i < n; i++) Tick(dt / n);
        }

        // ───── 매 프레임 (rig2.js Tick) ─────
        public void Tick(float dt)
        {
            if (look == null || M == null) return;
            dt = Mathf.Min(dt, 0.05f);
            time += dt; motionT += dt; blendT += dt; pokeT += dt;
            var info = Rig2Motions.Get(motion);
            if (info != null && !info.loop && motionT >= info.len && !holdOneShot) Play("idle");

            // 대기 중 가끔 짧은 행동 (손 흔들기·기지개·두리번·폴짝·하품)
            if (motion == "idle" && autoFidget)
            {
                if (fidget == null)
                {
                    fidgetWait -= dt;
                    if (fidgetWait <= 0) { fidget = Rig2Motions.Fidgets[rng.Range(0, Rig2Motions.Fidgets.Length)]; fidgetT = 0; }
                }
                else
                {
                    fidgetT += dt;
                    if (fidgetT >= Rig2Motions.Get(fidget).len) { fidget = null; fidgetWait = rng.Range(M.fidgetMin, M.fidgetMax); }
                }
            }

            // ① 목표 자세
            var p = Rig2Pose.Rest(M);
            Pose(motion, motionT, p, dt);
            if (motion == "idle" && fidget != null)
            {
                var q = p.Clone();
                Pose(fidget, fidgetT, q, dt);
                float e = Smooth(Mathf.Min(fidgetT, Rig2Motions.Get(fidget).len - fidgetT) / 0.25f);
                p = Rig2Pose.Mix(p, q, e); p.face = q.face;
            }
            // ② 섞기
            float bl = prevPose != null && Mathf.Abs(prevPose.yaw - p.yaw) > 30 ? Mathf.Max(M.blend, M.turnTime) : M.blend;   // 돌아설 땐 더 천천히
            if (prevPose != null && blendT < bl) p = Rig2Pose.Mix(prevPose, p, Smooth(blendT / bl));
            pose = p;
            // ③④⑤
            SolveHeaded(p, dt);
            UpdateFace(p, dt);
        }

        // ───── 작은 함수들 ─────
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        public static float Bump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);           // 0 → 1 → 0
        public static float Frac(float t) => t - Mathf.Floor(t);
        public static float Seg(float u, float a, float b) => Mathf.Clamp01((u - a) / (b - a)); // u가 a~b 사이 어디인지 0~1

        // ───── 동작 함수: 목표 자세 ─────
        void Pose(string name, float t, Rig2Pose p, float dt)
        {
            var info = Rig2Motions.Get(name);
            float u = info != null && info.len > 0 ? Mathf.Clamp01(t / info.len) : 0;
            switch (name)
            {
                case "idle": PoseIdle(t, p); break;
                case "walk": PoseWalk(t, p, dt, false); break;
                case "carry": PoseWalk(t, p, dt, true); break;
                case "work": PoseWork(t, p); break;
                case "climb": PoseClimbing(t, p, dt, 1, false); break;
                case "climbDown": PoseClimbing(t, p, dt, -1, false); break;
                case "ladder": PoseClimbing(t, p, dt, 1, true); break;
                case "ladderDown": PoseClimbing(t, p, dt, -1, true); break;
                case "hop": PoseHop(Frac(t / M.hopPeriod), p, M.hopH); break;
                case "hopOnce": PoseIdle(t, p); PoseHop(u, p, M.hopH); break;
                case "jump": PoseJump(u, p); break;
                case "surprise": PoseSurprise(u, p); break;
                case "spin": PoseSpin(u, p); break;
                case "land": PoseLand(u, p); break;
                case "dance": PoseDance(t, p); break;
                case "heart": PoseHeart(t, p); break;
                case "sleep": PoseSleep(t, p); break;
                case "held": PoseHeld(t, p); break;
                case "fall": PoseFall(t, p); break;
                case "wave": PoseIdle(t, p); PoseWave(u, t, p); break;
                case "stretch": PoseIdle(t, p); PoseStretch(u, p); break;
                case "look": PoseIdle(t, p); PoseLook(u, p); break;
                case "yawn": PoseIdle(t, p); PoseYawn(u, p); break;
                default: PoseIdle(t, p); break;
            }
        }

        // 대기: 숨쉬기(가슴이 부풀며 어깨가 오름) + 천천히 무게 옮기기(콘트라포스토: 디딘 쪽으로 골반, 반대쪽 골반이 내려감, 상체는 반대로)
        void PoseIdle(float t, Rig2Pose p)
        {
            float br = Mathf.Sin(t * 2 * Mathf.PI / M.breathPeriod);
            p.breath = br;
            p.raise[0] += br * 1.2f; p.raise[1] += br * 1.2f;
            p.bend[0] += br * 2; p.bend[1] += br * 2;
            float sh = (float)(System.Math.Tanh(2.5 * Mathf.Sin(t * 2 * Mathf.PI / M.shiftPeriod + 0.7f)) / System.Math.Tanh(2.5));   // 오래 머물고 부드럽게 넘어감
            p.hipX = sh * M.shiftX * L;
            p.hipRot = -sh * M.shiftTilt;                   // 디딘 쪽(+x)이면 반대쪽(-x) 골반이 내려감 → 반시계
            p.hipY = -Mathf.Abs(sh) * 0.02f * L;
            p.chestRot = sh * M.shiftTilt * 0.7f;           // 상체는 반대로 (균형)
            p.headRot = -sh * 1.5f + Mathf.Sin(t * 0.9f + 1.3f) * M.idleHead;
            int free = sh > 0 ? 0 : 1;                      // 쉬는 다리: 발을 살짝 안으로
            p.footX[free] += (sh > 0 ? 1 : -1) * Mathf.Abs(sh) * 0.04f * L;
            p.face = "normal";
        }

        // 걷기 (나르기 = 무거운 걷기). 발 디딤(stance) 동안 발목 목표는 이동 속도로 뒤로 미끄러져 "땅에 붙어" 보이고,
        // 발을 뗀 동안(swing) 들어서 앞으로. 골반 높이는 디딘 다리가 막대처럼 서는 역진자 → 한 걸음에 두 번 오르내림.
        void PoseWalk(float t, Rig2Pose p, float dt, bool carry)
        {
            float speed = (carry ? M.carrySpeed : M.walkSpeed) * H;                                       // 리그 단위/초
            float cadence = Mathf.Clamp(speed / (M.strideMax * L), M.cadenceMin, M.cadenceMax) * (carry ? 0.85f : 1);   // 걸음/초
            gait += dt * cadence / 2;                                                                     // 한 주기 = 두 걸음
            float ph = Frac(gait), beta = M.stance;
            float D = speed * beta * 2 / cadence;                                                         // 디딘 동안 발이 뒤로 가는 거리
            float T = keepFront ? 0 : M.walkTurn;                                                          // 몸을 가는 쪽으로 돌린 정도 (0 정면 ~ 1 옆)
            walkInfo = new WalkInfo { speed = speed, cadence = cadence, D = D, stepLen = speed / cadence };
            var fwd = new float[2];
            float Lg = kneeOn ? thighLen + shinLen : legLen;
            float hipH = Lg;                                    // 디딘 다리가 허락하는 엉덩이 높이 (역진자)
            for (int i = 0; i < 2; i++)
            {
                float q = Frac(ph + (i == 0 ? 0 : 0.5f));
                float d, lf = 0, rot = 0;
                if (q < beta)
                {
                    // 디딤: 뒤꿈치 닿기(발끝 듦) → 무릎으로 받기 → 발바닥 → 뒤꿈치 들고 발끝으로 밀기. 발목은 땅에 붙어 뒤로
                    float s = q / beta;
                    d = D * (0.5f - s);
                    rot = s < 0.15f ? M.toeUp * (1 - s / 0.15f) : s > 0.6f ? -M.toeOff * Smooth(Seg(s, 0.6f, 1)) : 0;
                    lf = s > 0.6f ? M.heelLift * L * Smooth(Seg(s, 0.6f, 1)) : 0;                     // 뒤꿈치가 들리면 발목이 올라감
                    float reach = Mathf.Sqrt(Mathf.Max(0, Lg * Lg - d * d)) + lf;                       // 이 발로 설 수 있는 엉덩이 높이
                    float dip = M.walkDip * L * (carry ? M.carryDip : 1) * Mathf.Sin(Mathf.Clamp01(s / 0.35f) * Mathf.PI);   // 닿은 직후 무릎으로 받기
                    hipH = Mathf.Min(hipH, reach - dip);
                }
                else
                {
                    // 뗌: 발끝 떼기 → 무릎을 굽혀 발을 뒤로 들어 올림(초반에 가장 높이) → 다리를 앞으로 뻗어 뒤꿈치부터
                    float s = (q - beta) / (1 - beta);
                    d = -D / 2 + D * Smooth(s);
                    float pk = M.liftPeak;
                    lf = M.liftH * L * (carry ? 0.75f : 1) * (s < pk ? Mathf.Sin(s / pk * Mathf.PI / 2) : Mathf.Cos((s - pk) / (1 - pk) * Mathf.PI / 2));
                    rot = s < 0.25f ? -M.toeOff * (1 - s / 0.25f) : s > 0.7f ? M.toeUp * Smooth(Seg(s, 0.7f, 1)) : 0;
                }
                fwd[i] = d / (D / 2);
                p.front[i] = d;
                p.footX[i] = (i == 0 ? 1 : -1) * M.footIn * L;
                p.footY[i] = lf;
                p.footZ[i] = d;                                 // 앞으로 (화면 투영은 SolveHeaded가 yaw로)
                p.footRot[i] = rot;
            }
            // 방향: 걸을 땐 가는 쪽 옆모습 (옆 그림이 있으면), "걸을 때 정면 유지"면 정면에 가깝게
            bool sideWalk = !keepFront && HasView("side");
            p.yaw = keepFront ? 0 : sideWalk ? 90 : M.walkYaw;
            float restH = kneeOn ? (V((lv ?? look).hipL).y + lift - ankleRest[0].y) : Lg;
            float dropMax = Mathf.Max(0, restH - hipH) + M.kneeSoft * L;                                  // 무릎을 살짝 굽힌 채 걷는다
            // 골반: 디딘 발 위로 무게 (옆으로 흔들림), 뗀 쪽 골반이 내려감, 상체는 반대로 + 가는 쪽으로 숙임
            float w = -Mathf.Cos(2 * Mathf.PI * (ph - beta / 2));                                         // 왼발 디딤 한가운데 -1, 오른발 +1
            p.hipY = -dropMax;
            p.hipX = w * M.walkSway * L * (1 - 0.6f * T);
            p.hipRot = M.walkTilt * w;
            p.chestRot = -p.hipRot * M.walkCounter - (carry ? M.carryLean : M.walkLean) * T;
            if (sideWalk)
            {                                                   // 옆에서 보면 옆 흔들림·골반 기울기는 안 보이고, 앞으로 숙임이 보인다
                p.hipX = 0; p.hipRot = 0; p.chestRot = -(carry ? M.carryLean : M.walkLean) * 1.2f;
            }
            p.headRot = 0;
            p.headTurn = T;
            // 팔: 다리와 반대로 흔들기. 앞으로 간 팔은 팔꿈치를 굽히고 짧아 보인다 (원근)
            for (int i = 0; i < 2; i++)
            {
                float af = -fwd[i];                             // 왼팔은 왼발과 반대
                float toward = i == 0 ? -1 : 1;                 // 앞(+x)으로 = 왼팔은 안쪽(-), 오른팔은 바깥(+)
                p.swing[i] = carry ? 30 + af * 4 : af * M.armSwingSide;          // 옆모습: 앞뒤로 흔들기
                if (carry)
                {
                    p.raise[i] = M.carryRaise + af * 3; p.bend[i] = M.carryBend;
                }
                else
                {
                    p.raise[i] = M.armRest + toward * af * M.armSwing * T + Mathf.Abs(af) * 3;
                    p.bend[i] = M.elbowRest + Mathf.Max(0, af) * M.armBendFwd;
                    p.fore[i] = 1 - Mathf.Max(0, af) * M.armFore * (1 - T);
                }
            }
            p.face = carry ? "effort" : "normal";
            if (carry) p.headRot = -4;
        }

        // 일하기: 한 손씩 위로 뻗어(발끝으로 서며) 따서 아래 바구니로. 다른 손은 바구니를 든다
        void PoseWork(float t, Rig2Pose p)
        {
            float c = t / M.workPeriod, u = Frac(c); int k = Mathf.FloorToInt(c);
            int side = k % 2 == 0 ? 0 : 1, other = 1 - side; float sg = side == 0 ? -1 : 1;
            float reach = Smooth(Seg(u, 0.05f, 0.35f)) * (1 - Smooth(Seg(u, 0.55f, 0.85f)));       // 뻗기 → 잡고 → 내리기
            float pull = Bump(Seg(u, 0.5f, 0.9f));
            p.raise[side] = Mathf.Lerp(M.armRest, M.workReach, reach);
            p.bend[side] = Mathf.Lerp(M.elbowRest, 18, reach) + pull * 80;
            p.wrist[side] = -20 * reach;
            p.fore[side] = 1 + 0.25f * reach;
            p.raise[other] = 16; p.bend[other] = 75;
            p.hipY = reach * M.workRise * L - pull * 0.05f * L;                                     // 발끝으로 서고, 내릴 때 살짝 앉음
            p.footRot[0] = p.footRot[1] = 0;
            p.hipRot = sg * -3 * reach; p.chestRot = sg * 6 * reach;                                 // 뻗는 쪽으로 몸이 늘어남
            p.headRot = sg * 9 * reach;
            p.face = reach > 0.3f ? "effort" : "normal";
        }

        // 손발 엇갈려 오르기 공용 (사다리 = 뒷모습, 줄기·덩굴 = 옆모습). dir 1 오르기 · -1 내려오기 (같은 동작을 거꾸로).
        // 잡은 손·디딘 발은 세상(가로대)에 붙어 있어 몸이 오르는 만큼 리그 안에서 아래로 내려가고, 놓은 손발은 두 칸 위 가로대로 옮겨 간다.
        // 오른손+왼발이 같이, 다음에 왼손+오른발. 손이 당기는 동안 몸이 오르고 발이 다음 가로대를 딛는다.
        void PoseClimbing(float t, Rig2Pose p, float dt, int dir, bool ladder)
        {
            float G = ladder ? M.rungGap : M.trunkGap;
            climbS += dir * (ladder ? M.ladderSpeed : M.trunkSpeed) * dt;
            float S = climbS, beta = 0.62f;
            string vk = ladder ? "back" : "side";
            var vd = ViewData(vk); float lft = vd.lift ?? lift;
            bool side = vk == "side";
            p.yaw = ladder ? 180 : 90; p.armScreen = true; p.grounded = true;
            float shY = V(vd.shoulderL).y + lft + 0.04f * L;
            float yTop = shY + (ladder ? M.ladderReachUp : 0.36f);                  // 손이 잡는 가장 높은 가로대 (어깨 위)
            float ph = S / (2 * G);
            for (int i = 0; i < 2; i++)
            {
                // 손: 왼손 0 · 오른손 0.5 박자
                float q = Frac(ph + (i == 0 ? 0 : 0.5f));
                float y, outA = 0;
                if (q < beta) y = yTop - 2 * G * q;                                  // 잡고 끌어내림 (= 몸이 오름)
                else { float u = (q - beta) / (1 - beta); y = yTop - 2 * G * beta + 2 * G * beta * Smooth(u); outA = Bump(u); }   // 놓고 두 칸 위로 (살짝 바깥으로 돌려)
                var Sh = V(i == 0 ? vd.shoulderL : vd.shoulderR) + new Vector2(0, lft);
                float tx, ty = y - Sh.y;
                if (side) tx = M.trunkReach - outA * 0.08f;                          // 옆모습: 앞(+x)의 줄기를 잡는다
                else tx = (i == 0 ? -1 : 1) * (M.ladderHalfW - Mathf.Abs(Sh.x) - 0.06f + outA * 0.07f);   // 뒷모습: 난간 쪽 (긴 머리 밖으로 손이 보이게)
                var local = new Vector2(side || i == 0 ? tx : -tx, ty);              // 오른팔(뒤집힌 공간)은 x를 뒤집어 준다
                float fore = ladder ? M.ladderArmStretch : 1;                         // 짧은 팔: 사다리에선 늘여서 머리 옆 가로대까지 (늘이기)
                ArmIK(local * (1 / fore), side ? 1 : -1, out float raise, out float bend);   // 팔꿈치는 아래로 (옆: 아래·뒤, 뒤: 아래·바깥)
                p.raise[i] = raise; p.bend[i] = bend; p.wrist[i] = side ? 25 : -25 + outA * 20;
                p.fore[i] = fore;
                // 발: 손과 엇갈려 (오른손 0.5 ↔ 왼발 0.5)
                float qf = Frac(ph + (i == 0 ? 0.5f : 0));
                var rest = ankleRest[i];
                float sole = vd.sole ?? 0, ankleH = rest.y - sole;
                float hipY = V(vd.hipL).y + lft;
                float want = hipY - thighLen * 0.95f - shinLen * 0.55f;              // 무릎을 굽혀 딛는 높이
                float fTop = yTop - G * Mathf.Ceil((yTop - want) / G);               // 발도 같은 가로대 줄에
                float fy, lift2 = 0;
                if (qf < beta) fy = fTop - 2 * G * qf;
                else { float u = (qf - beta) / (1 - beta); fy = fTop - 2 * G * beta + 2 * G * beta * Smooth(u); lift2 = Bump(u); }
                p.footY[i] = fy + ankleH - rest.y;
                p.footX[i] = side ? 0 : (i == 0 ? 1 : -1) * 0.03f * L;
                p.footZ[i] = side ? M.trunkReach * 0.75f - lift2 * 0.05f : 0.04f + lift2 * 0.08f;   // 무릎이 앞으로 나왔다 디딤
                p.footRot[i] = side ? 10 : 0;
            }
            float w = Mathf.Sin(2 * Mathf.PI * ph);
            p.hipX = side ? 0.04f * L : w * 0.045f * L; p.hipRot = side ? -3 : w * 3; p.chestRot = side ? -7 : -w * 2.5f;
            p.hipY = Mathf.Abs(Mathf.Cos(2 * Mathf.PI * ph)) * 0.02f * L;            // 당길 때 살짝 솟음
            p.headRot = side ? 4 : -w * 2;
            p.face = "effort";
        }

        // 폴짝 한 번 (u 0~1): 웅크림(예비 동작) → 차고 늘어남 → 공중(발 모음) → 착지 찌그러짐 → 회복
        void PoseHop(float u, Rig2Pose p, float h)
        {
            float crouch = Bump(Seg(u, 0, 0.3f)), land = Bump(Seg(u, 0.78f, 1));
            float air = Seg(u, 0.25f, 0.8f);
            p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * h * H : 0;
            p.hipY = -(crouch * 0.6f + land * 0.5f) * M.jumpCrouch * L;
            p.sqY = 1 - crouch * 0.07f - land * 0.09f + (air > 0 && air < 0.4f ? Bump(air / 0.4f) * 0.06f : 0);
            p.sqX = 1 / Mathf.Sqrt(Mathf.Max(0.5f, p.sqY));
            float tuck = Bump(air);
            p.footY[0] = p.footY[1] = tuck * 0.18f * L; p.footX[0] = 0.05f * L * tuck; p.footX[1] = -0.05f * L * tuck;
            p.raise[0] = p.raise[1] = Mathf.Lerp(p.raise[0], 70, tuck) - crouch * 4;
            p.bend[0] = p.bend[1] = 20 + crouch * 25;
            p.face = "happy";
            p.grounded = p.air <= 0;
        }

        void PoseJump(float u, Rig2Pose p)
        {
            float crouch = Bump(Seg(u, 0, 0.26f)), land = Bump(Seg(u, 0.8f, 1));
            float air = Seg(u, 0.22f, 0.82f);
            p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * M.jumpH * H : 0;
            p.hipY = -(crouch + land * 0.8f) * M.jumpCrouch * L;
            p.sqY = 1 - crouch * 0.1f - land * M.landSquash * 0.7f + (air > 0 && air < 0.35f ? Bump(air / 0.35f) * 0.1f : 0);
            p.sqX = 1 / Mathf.Sqrt(Mathf.Max(0.5f, p.sqY));
            float up = Smooth(Seg(u, 0.18f, 0.32f)) * (1 - Smooth(Seg(u, 0.72f, 0.9f)));
            p.raise[0] = p.raise[1] = Mathf.Lerp(Mathf.Lerp(7, -10, crouch), 155, up);             // 웅크리며 팔을 뒤로 → 차면서 위로 휘두름
            p.bend[0] = p.bend[1] = 15 + crouch * 30;
            float tuck = Bump(air);
            p.footY[0] = p.footY[1] = tuck * 0.3f * L; p.footX[0] = 0.06f * L * tuck; p.footX[1] = -0.06f * L * tuck;
            p.footRot[0] = p.footRot[1] = tuck * 12;
            p.chestRot = 0; p.headRot = -crouch * 4;
            p.face = u < 0.18f ? "normal" : "happy";
            p.grounded = p.air <= 0;
        }

        void PoseSurprise(float u, Rig2Pose p)
        {
            float pop = Seg(u, 0, 0.35f);
            p.air = pop < 1 ? 4 * pop * (1 - pop) * M.surpriseH * H : 0;                      // 예비 동작 없이 움찔 뜀
            float fling = Smooth(Seg(u, 0, 0.12f)) * (1 - Smooth(Seg(u, 0.55f, 1)));
            p.raise[0] = p.raise[1] = Mathf.Lerp(7, 125, fling); p.bend[0] = p.bend[1] = Mathf.Lerp(14, 40, fling);
            p.wrist[0] = p.wrist[1] = -25 * fling;
            p.sqY = 1 + fling * 0.06f; p.sqX = 1 - fling * 0.03f;
            p.headRot = 0; p.chestRot = 0;
            p.hipY = -Bump(Seg(u, 0.35f, 0.6f)) * 0.12f * L;
            p.face = "surprise";
            p.grounded = p.air <= 0;
        }

        // 돌기: 제자리에서 한 바퀴 (그림이 앞모습뿐이라 가로로 줄였다 뒤집는다. 뒤를 볼 땐 뒷머리가 얼굴 앞)
        void PoseSpin(float u, Rig2Pose p)
        {
            p.spin = Smooth(u);
            float a = Seg(u, 0.05f, 0.6f);
            p.air = a > 0 && a < 1 ? 4 * a * (1 - a) * 0.1f * H : 0;
            p.raise[0] = p.raise[1] = 60 * Bump(u); p.bend[0] = p.bend[1] = 25;
            p.face = "happy";
            p.grounded = p.air <= 0;
        }

        // 착지: 쿵 → 무릎으로 받고(골반이 내려감) → 찌그러졌다 → 넘쳤다가 돌아옴 (스프링이 해 준다). 팔은 관성으로 아래로 갔다 올라옴
        void PoseLand(float u, Rig2Pose p)
        {
            float hit = Mathf.Exp(-u * 7) * (u > 0 ? 1 : 0);
            p.hipY = -hit * M.landDip * L;
            p.sqY = 1 - hit * M.landSquash; p.sqX = 1 + hit * M.landSquash * 0.7f;
            p.raise[0] = p.raise[1] = 7 + hit * 40; p.bend[0] = p.bend[1] = 14 + hit * 30;
            p.headRot = 0; p.footX[0] = 0.04f * L * hit; p.footX[1] = -0.04f * L * hit;
            p.face = u < 0.4f ? "surprise" : "happy";
        }

        // 춤: 박자마다 무릎으로 통통, 골반을 좌우로, 팔을 번갈아 위로, 발을 번갈아 든다
        void PoseDance(float t, Rig2Pose p)
        {
            float b = t / M.danceBeat, u = Frac(b); int k = Mathf.FloorToInt(b); float sg = k % 2 == 0 ? 1 : -1;
            float down = Bump(u); down *= down;
            p.hipY = -down * 0.12f * L;
            float sway = Mathf.Sin(Mathf.PI * b);                                               // 두 박자에 한 번 좌우
            p.hipX = sway * M.danceSway * L; p.hipRot = -sway * 6; p.chestRot = sway * 9;
            p.headRot = sway * 8;
            p.raise[0] = sg > 0 ? 145 : 40; p.raise[1] = sg > 0 ? 40 : 145;
            p.bend[0] = sg > 0 ? 40 : 70; p.bend[1] = sg > 0 ? 70 : 40;
            p.wrist[0] = p.wrist[1] = sway * 20;
            float lift = Bump(Seg(u, 0.1f, 0.7f)) * 0.16f * L;
            if (sg > 0) p.footY[1] = lift; else p.footY[0] = lift;
            p.face = "happy";
        }

        // 하트: 손을 가슴에 모으고 발끝으로 살랑살랑, 고개를 갸웃
        void PoseHeart(float t, Rig2Pose p)
        {
            float s = Mathf.Sin(t * 3.2f);
            for (int i = 0; i < 2; i++)
            {
                ArmIK(new Vector2(0.38f, -0.32f), 0, out float raise, out float bend);
                p.raise[i] = raise; p.bend[i] = bend; p.wrist[i] = 25;
            }
            p.hipX = s * 0.04f * L; p.hipRot = -s * 2; p.chestRot = s * M.heartSway * 0.5f; p.headRot = s * M.heartSway + 6;
            p.hipY = (0.04f + 0.03f * Mathf.Abs(s)) * L;
            p.footRot[0] = p.footRot[1] = -6;
            p.face = "happy";
        }

        // 잠: 선 채로 꾸벅꾸벅. 깊은 숨, 무릎이 풀리고 팔이 늘어지고 고개가 떨어졌다 가끔 번쩍
        void PoseSleep(float t, Rig2Pose p)
        {
            float br = Mathf.Sin(t * 2 * Mathf.PI / 4.2f);
            p.breath = br * 2.2f;
            float nod = Frac(t / 3.6f), jerk = nod > 0.9f ? Bump(Seg(nod, 0.9f, 1)) : 0;
            p.headRot = M.sleepDroop * (Smooth(nod / 0.9f) * 0.6f + 0.4f) * (1 - jerk * 0.8f);
            p.chestRot = 4 + br; p.hipY = -0.07f * L; p.hipRot = 2;
            p.raise[0] = p.raise[1] = 2; p.bend[0] = p.bend[1] = 6;
            p.face = "blink";
        }

        // 들림: 갓을 잡혀 매달림. 진자로 흔들리고(손 가속도), 다리를 동동, 팔을 버둥버둥, 몸을 비튼다
        void PoseHeld(float t, Rig2Pose p)
        {
            p.hang = true; p.grounded = false;
            float w = t * 15;
            p.raise[0] = 70 + Mathf.Sin(w) * M.heldFlail; p.raise[1] = 70 + Mathf.Sin(w + 2.4f) * M.heldFlail;
            p.bend[0] = 35 + Mathf.Sin(w + 1.3f) * 40; p.bend[1] = 35 + Mathf.Sin(w + 3.7f) * 40;
            p.hipRot = Mathf.Sin(t * 9) * 7; p.chestRot = -Mathf.Sin(t * 9) * 4; p.headRot = Mathf.Sin(t * 11 + 1) * 5;
            float k1 = Mathf.Sin(t * 13), k2 = Mathf.Sin(t * 13 + 2.2f);
            p.footY[0] = Mathf.Max(0, k1) * 0.25f * L - 0.12f * L; p.footY[1] = Mathf.Max(0, k2) * 0.25f * L - 0.12f * L;   // 매달려 다리가 늘어지고 동동
            p.footX[0] = -k1 * 0.08f * L; p.footX[1] = k2 * 0.08f * L;
            p.footRot[0] = k1 * 15; p.footRot[1] = -k2 * 15;
            p.sqY = 1.03f; p.sqX = 0.98f;
            p.face = Mathf.FloorToInt(t * 2.2f) % 3 == 2 ? "effort" : "surprise";
        }

        void PoseFall(float t, Rig2Pose p)
        {
            p.grounded = false;
            p.raise[0] = 160 + Mathf.Sin(t * 26) * 8; p.raise[1] = 160 + Mathf.Sin(t * 26 + 1.5f) * 8;
            p.bend[0] = p.bend[1] = 12;
            p.footY[0] = -0.08f * L + Mathf.Max(0, Mathf.Sin(t * 18)) * 0.08f * L; p.footY[1] = -0.08f * L + Mathf.Max(0, -Mathf.Sin(t * 18)) * 0.08f * L;
            p.footX[0] = -0.04f * L; p.footX[1] = 0.04f * L;
            p.sqY = 1.05f; p.sqX = 0.97f;
            p.face = "surprise";
        }

        // 손 흔들기: 예비 동작(살짝 내림) → 들어 올림(넘쳤다 섬) → 팔꿈치로 흔들고 손목이 따라옴 → 내림
        void PoseWave(float u, float t, Rig2Pose p)
        {
            int side = waveSide == "L" ? 0 : 1; float sg = side == 0 ? -1 : 1;
            float pre = Bump(Seg(u, 0, 0.12f)), up = Smooth(Seg(u, 0.08f, 0.3f)) * (1 - Smooth(Seg(u, 0.82f, 1)));
            p.raise[side] = Mathf.Lerp(7 - pre * 5, 150, up);
            p.fore[side] = 1 + 0.15f * up;
            p.bend[side] = Mathf.Lerp(14, 32 + Mathf.Sin(t * 15) * 28, up);
            p.chestRot -= sg * 3 * up; p.hipRot += sg * 1.5f * up; p.headRot += sg * 6 * up;
            p.face = "happy";
        }

        void PoseStretch(float u, Rig2Pose p)
        {
            float e = Smooth(Seg(u, 0.05f, 0.4f)) * (1 - Smooth(Seg(u, 0.7f, 1)));
            p.raise[0] = p.raise[1] = Mathf.Lerp(p.raise[0], 168, e); p.bend[0] = p.bend[1] = Mathf.Lerp(p.bend[0], 8, e);
            p.fore[0] = p.fore[1] = 1 + 0.35f * e;                                                // 쭉 뻗는 팔은 늘어난다 (늘이기·찌그러뜨리기)
            p.wrist[0] = p.wrist[1] = -15 * e;
            p.hipY = e * 0.1f * L; p.sqY = 1 + e * 0.04f; p.sqX = 1 - e * 0.02f; p.breath = 1.5f * e;
            p.footRot[0] = p.footRot[1] = 0;
            p.hipRot = 0; p.chestRot = 0; p.headRot = Mathf.Sin(u * Mathf.PI * 2) * 3;
            p.face = e > 0.4f ? "blink" : "normal";
            if (u > 0.85f) p.hipY -= Bump(Seg(u, 0.85f, 1)) * 0.04f * L;                         // 풀면서 살짝 처짐
        }

        void PoseLook(float u, Rig2Pose p)
        {
            float a = Smooth(Seg(u, 0.05f, 0.25f)) - Smooth(Seg(u, 0.4f, 0.6f)) * 2 + Smooth(Seg(u, 0.75f, 0.95f));
            p.headTurn = a; p.headRot += -a * 6; p.chestRot += -a * 2;
            p.face = "normal";
        }

        void PoseYawn(float u, Rig2Pose p)
        {
            float e = Smooth(Seg(u, 0.1f, 0.35f)) * (1 - Smooth(Seg(u, 0.7f, 0.95f)));
            ArmIK(new Vector2(0.55f, 0.95f), 0, out float raise, out float bend);                 // 왼손을 입으로
            p.raise[0] = Mathf.Lerp(p.raise[0], raise, e); p.bend[0] = Mathf.Lerp(p.bend[0], bend, e);
            p.raise[1] = Mathf.Lerp(p.raise[1], 20, e);
            p.headRot += -7 * e; p.breath = 1.8f * e; p.hipY += 0.03f * L * e;
            p.face = e > 0.3f ? "blink" : "normal";
        }

        // 팔 IK: 어깨 공간(왼팔 기준, 오른팔은 뒤집혀 같은 값)의 목표 점 → 어깨 각(raise)·팔꿈치 각(bend)
        // prefer 1 = 팔꿈치가 바깥(-x)으로 굽는 해 (정면 기본), -1 = 반대 (옆모습에서 팔꿈치가 아래·뒤로)
        void ArmIK(Vector2 target, float prefer, out float raise, out float bend)
        {
            float a = upperLen, b = foreLen, sg = prefer < 0 ? -1 : 1;
            float d = Mathf.Clamp(target.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            float cosB = Mathf.Clamp((a * a + b * b - d * d) / (2 * a * b), -1, 1);
            bend = sg * (180 - Mathf.Acos(cosB) * Mathf.Rad2Deg);                                  // 0 = 쭉 폄
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1);
            float toT = Mathf.Atan2(target.x, -target.y) * Mathf.Rad2Deg;                          // 아래(0)에서 +x 쪽으로 (반시계 +)
            float ang = toT - sg * Mathf.Acos(cosA) * Mathf.Rad2Deg;
            raise = -ang;                                                                          // raise + = 바깥(-x)으로 = 시계
        }

        // ───── ③④⑤ ★2·★3 풀기 (rig2.js SolveHeaded) ─────
        void SolveHeaded(Rig2Pose p, float dt)
        {
            // 보는 방향: yaw 0 정면 · 90 옆 · 180 뒤. 경계(45·135)를 지날 때 그림을 바꾸고, 그 순간 가로로 살짝 좁혀(돌아서는 느낌) 팍 바뀌어 보이지 않게
            float yawDeg = p.yaw;
            SetView(yawDeg < 45 ? "front" : yawDeg < 135 ? "side" : "back");
            string vk = viewKey; var l = lv;
            float baseYaw = vk == "front" ? 0 : vk == "side" ? 90 : 180;
            float resYaw = Mathf.Clamp(yawDeg - baseYaw, -M.maxResYaw, M.maxResYaw);           // 그 그림에서 더 돌아간 정도 (다리 투영·얼굴 옮김)
            float pinch = Mathf.Max(0, 1 - Mathf.Min(Mathf.Abs(yawDeg - 45), Mathf.Abs(yawDeg - 135)) / M.turnPinchW);
            turnPinchNow = pinch;
            // ③ 몸 스프링 (자세가 갑자기 바뀌어도 무게 있게)
            sHip[0].Step(p.hipX, M.bodyK, M.bodyZ, dt); sHip[1].Step(p.hipY, M.bodyK, M.bodyZ, dt); sHip[2].Step(p.hipRot, M.bodyK, M.bodyZ, dt);
            sChest.Step(p.chestRot, M.bodyK * 0.7f, M.bodyZ, dt);
            sTurn.Step(p.headTurn, M.headK, 0.9f, dt);
            sAir.Step(p.air, 900, 1, dt);
            sSqX.Step(p.sqX, M.squashK, M.squashZ, dt); sSqY.Step(p.sqY, M.squashK, M.squashZ, dt);
            for (int i = 0; i < 2; i++)
            {
                sRaise[i].Step(ArmTarget(p, i, vk), M.armK, M.armZ, dt);
                sBend[i].Step(p.bend[i], M.elbowK, M.elbowZ, dt);                                    // 팔꿈치는 조금 늦게 (겹침)
            }
            float spinC = Mathf.Cos(p.spin * 2 * Mathf.PI); bool back = spinC < 0;
            float air = p.air > 0 ? p.air : Mathf.Max(0, sAir.x);

            // root: 공중 높이 · 찌그러짐(발바닥 축) · 돌기 · 들림
            float rootRot = 0; var rootPos = new Vector2(0, air);
            if (p.hang)
            {
                // 갓 꼭대기에 매달린 진자: 손 가속도가 밀어 흔든다 (g/길이 = hangK)
                var capTop = V(l.capTop) + new Vector2(0, lift);
                hang.Step(0, M.hangK, M.hangZ, -grabAccel.x * 4, dt);
                rootRot = Mathf.Clamp(hang.a, -50, 50);
                float r = rootRot * Mathf.Deg2Rad;
                var ct = new Vector2(capTop.x * Mathf.Cos(r) - capTop.y * Mathf.Sin(r), capTop.x * Mathf.Sin(r) + capTop.y * Mathf.Cos(r));
                rootPos = capTop - ct;                         // 갓 꼭대기가 서 있을 때 자리에 머문다
            }
            Set(root, rootPos.x, rootPos.y, rootRot, scale * flip * sSqX.x * (1 - M.turnPinch * pinch) * (Mathf.Abs(spinC) < 0.06f ? 0.06f * Mathf.Sign(spinC) : spinC), scale * sSqY.x);

            // 골반 · 상체 · 숨
            var W = V(l.waist);
            Set(hips, W.x + sHip[0].x, W.y + lift + sHip[1].x, sHip[2].x);
            Set(chest, 0, 0, sChest.x, 1, 1 + p.breath * M.breathAmt);
            // 머리: 몸이 기울어도 수평을 지키려 하고(headStab), 스프링으로 늦게 따라와 흔들림
            float chestWorld = sHip[2].x + sChest.x + rootRot;
            sHead.Step(p.headRot - chestWorld * M.headStab, M.headK, M.headZ, dt);
            Rot(neck, sHead.x);
            // 갓 통통 (누르면)
            float jig = pokeT < 1.2f ? 0.16f * Mathf.Exp(-5 * pokeT) * Mathf.Sin(pokeT * 26) : 0;
            // 고개 돌림: 얼굴 층을 옆으로, 뒷머리는 반대로 조금 (그림 한 장으로 3/4 느낌)
            float turn = Mathf.Clamp(sTurn.x + (vk == "front" ? Mathf.Clamp(resYaw / 45, -1, 1) * 0.9f : 0), -1, 1);
            // 머리 전체가 돈다: Bounce(갓 통통) 노드 = 머리·얼굴의 공통 부모. 얼굴 층은 그 안에서 아주 조금만
            Set(bounce, turn * M.headTurnShift, 0, -turn * M.headTurnTilt, (1 + jig) * (1 - M.headTurnSquash * Mathf.Abs(turn)), 1 - jig);
            faceSR.transform.localPosition = new Vector3(turn * M.faceShift, 0, 0);
            faceSR.enabled = !back && vk != "back";
            // 뒷머리: 정면 = 맨 뒤 · 옆 = 몸통·먼 팔 앞, 머리·가까운 팔 뒤 · 뒤 = 맨 위 (등을 덮음)
            // (뒷모습에서 머리카락은 팔·몸 위, 갓 아래 = 갓 테두리 밑에서 머리카락이 나온다)
            // 뒷모습: 머리카락은 팔·몸·머리 그림 위 45, 갓 덮개 47 아래 → 머리카락이 갓 테두리 밑에서 나와 맨 앞에 보인다
            Order(hairSR, vk == "back" ? 45 : vk == "side" ? 15 : back ? 130 : -80);
            // 뼈 사슬 머리카락: 정면 = 몸 뒤(-80, 뒤돌면 130) · 옆 = 위 45 / 어깨 아래 줄 5(가슴 뒤) · 뒤 = 45
            if (hairSkin)
            {
                hairSkinMR.sortingOrder = baseOrder + (vk == "front" ? (back ? 130 : -80) : 45);
                if (hairSkinLowMR != null) hairSkinLowMR.sortingOrder = baseOrder + 5;
            }

            // 팔
            // 먼 쪽 팔다리는 몸 뒤로: 정면에서 가는 쪽으로 돌면 반대쪽, 옆모습은 오른쪽(i=1)이 먼 쪽, 뒷모습은 두 팔 다 몸 앞쪽(=화면에선 뒤)
            int farSide = vk == "side" ? 1 : turn > 0.25f ? 0 : turn < -0.25f ? 1 : -1;
            for (int i = 0; i < 2; i++)
            {
                Rot(upper[i], -sRaise[i].x);
                upper[i].localScale = new Vector3(1, p.fore[i], 1);
                Rot(elbow[i], sBend[i].x);
                bool far = i == farSide;
                // 뒷모습 (뒤→앞): 사다리 · 다리 · 몸통/치마 · 팔 · 머리(뒤통수·갓) · 뒷머리 (머리카락에 가려지는 팔은 가려진다)
                if (vk == "back") { Order(upperSR[i], 20); Order(foreSR[i], 21); Order(handSR[i], 22); }
                else { Order(upperSR[i], far ? -70 : 80); Order(foreSR[i], far ? -60 : 90); Order(handSR[i], far ? -50 : 100); }
                // 소매: 위팔 바로 위 (아래팔·손보다는 아래), 위팔 회전의 sleeveFollow만큼 따라 돈다
                if (sleeveSR[i] != null && sleeveT[i] != null) { sleeveSR[i].sortingOrder = upperSR[i].sortingOrder + 5; Rot(sleeveT[i], -sRaise[i].x * M.sleeveFollow); }
            }

            // ⑤ 다리 IK (무릎 2뼈, 3D → 화면): 발목 목표 = 쉬는 발목 + (옆 X, 높이 Y, 앞 Z). 무릎은 앞(+Z)으로 굽는다.
            //    3D 점을 보는 방향(yaw: 0 정면 · 90 옆)으로 화면에 투영 → 정면에선 무릎 굽힘이 다리가 짧아지는 것으로, 옆에선 꺾임으로 보인다.
            legStretch[0] = legStretch[1] = 1;
            float yaw = resYaw * Mathf.Deg2Rad, cy = Mathf.Cos(yaw), sy = vk == "side" ? 1 : Mathf.Sin(yaw), dk = M.depthK;
            // 3D(옆 X, 위 Y, 앞 Z) → 화면. 정면: 앞이 카메라 쪽(조금 아래로) · 옆: 앞이 화면 +x · 뒤: 앞이 화면 안쪽(조금 위로)
            Vector2 Proj(float x, float y, float z) =>
                vk == "side" ? new Vector2(x + z, y)
                : vk == "back" ? new Vector2(x - z * Mathf.Sin(yaw), y + z * dk)
                : new Vector2(x * cy + z * sy, y - (z * cy - x * sy) * dk);
            var depthOf = new float[2];
            for (int i = 0; i < 2; i++)
            {
                float m = i == 0 ? 1 : -1;
                var rest = ankleRest[i];
                var Hs = RootLocal(hip[i]);                    // 엉덩이 (화면 = root 안쪽 공간)
                var hipRest = V(i == 0 ? l.hipL : l.hipR) + new Vector2(0, lift);
                // 발목 목표 (엉덩이 기준 3D). 땅에 있으면 발목은 땅 좌표에 고정, 공중·들림이면 엉덩이를 따라간다
                float fx = p.footX[i], fy = p.footY[i], fz = p.footZ[i];
                float vx, vy, vz = fz;
                if (p.grounded) { vx = rest.x + fx - Hs.x; vy = rest.y + fy - Hs.y; }
                else { vx = rest.x - hipRest.x + fx; vy = rest.y - hipRest.y + fy; }
                float a = kneeOn ? thighLen : legLen * 0.5f, b = kneeOn ? shinLen : legLen * 0.5f;
                float d = Mathf.Sqrt(vx * vx + vy * vy + vz * vz);
                float dMax = (a + b) * 0.999f, dMin = Mathf.Abs(a - b) + 1e-3f;
                legStretch[i] = d / (a + b);
                if (d > dMax || d < dMin) { float k = Mathf.Clamp(d, dMin, dMax) / Mathf.Max(d, 1e-6f); vx *= k; vy *= k; vz *= k; d = Mathf.Clamp(d, dMin, dMax); }
                // 무릎 방향: 다리 축에 수직이면서 앞(+Z) 쪽 (조금 바깥으로: kneeOut)
                float ux = vx / d, uy = vy / d, uz = vz / d;
                float wx = vk == "side" ? 0 : -m * M.kneeOut, wy = 0, wz = 1;
                float dot = wx * ux + wy * uy + wz * uz;
                wx -= dot * ux; wy -= dot * uy; wz -= dot * uz;
                float wl = Mathf.Sqrt(wx * wx + wy * wy + wz * wz); if (wl == 0) wl = 1; wx /= wl; wy /= wl; wz /= wl;
                float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1), sinA = Mathf.Sqrt(1 - cosA * cosA);
                float kx = a * (cosA * ux + sinA * wx), ky = a * (cosA * uy + sinA * wy), kz = a * (cosA * uz + sinA * wz);
                Vector2 Ks = Hs + Proj(kx, ky, kz), As = Hs + Proj(vx, vy, vz);
                depthOf[i] = vk == "side" ? -i : vk == "back" ? -vz : vz * cy - vx * sy;
                if (kneeOn)
                {
                    SetBone(thighT[i], Hs, Ks, thighBind, m);
                    SetBone(shinT[i], Ks, As, shinBind, m);
                }
                else
                {
                    // 무릎 그림이 없으면 예전처럼 한 장 (엉덩이 → 발목)
                    Vector2 bind = new Vector2(legBind.x * m, legBind.y), dd = As - Hs;
                    float worldAng = (Mathf.Atan2(dd.y, dd.x) - Mathf.Atan2(bind.y, bind.x)) * Mathf.Rad2Deg;
                    Rot(thigh[i], m * (worldAng - sHip[2].x));
                    thigh[i].localScale = new Vector3(1, dd.magnitude / legLen, 1);
                }
                Set(foot[i], As.x, As.y, 0, m, 1);
                // 발: 땅에선 평평 + 뒤꿈치 닿기(발끝 듦)·발끝 떼기, 공중에선 정강이를 따라 늘어짐. 발 굴림은 옆에서 잘 보인다
                float shinAng = Mathf.Atan2(As.y - Ks.y, As.x - Ks.x) * Mathf.Rad2Deg + 90;
                Rot(footRot[i], m * p.footRot[i] * (vk == "side" ? 1 : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(yaw))) + (p.grounded ? 0 : shinAng * 0.5f));
            }
            // 앞(카메라 쪽)에 있는 다리가 앞에 그려진다
            for (int i = 0; i < 2; i++)
            {
                bool front = depthOf[i] > depthOf[1 - i] + 1e-3f, far = i == farSide;
                int bo = far ? -48 : front ? -28 : -34;
                Order(thighSR[i], bo); Order(shinSR[i], bo + 1); Order(legSR[i], bo);
                Order(shoeSR[i], bo + 4);
            }

            // ④ 2차 움직임: 관절점의 실제 가속도로 흔들리는 진자 (root 공간 + 바깥 이동)
            Accel(dt, RootSpace(hairT), RootSpace(hips), RootSpace(wrist[0]), RootSpace(wrist[1]));
            Vector2 accHair = accOut[0], accWaist = accOut[1];
            float headWorld = chestWorld + sHead.x;
            hairP.Step(-headWorld * M.hairGravity - vel.x * M.hairDrag * 2, M.hairK, M.hairZ, -accHair.x * M.hairInertia * 10, dt);
            // 뼈 사슬이면 뿌리는 조금만 돌고(정수리·목덜미가 머리에서 떨어지지 않게) 흔들림은 아래 뼈가 나눠 맡는다
            float rootMax = hairSkin ? M.hairSegRootMax : 35;
            float rootRotNow = Mathf.Clamp(hairP.a, -rootMax, rootMax);
            hairT.localRotation = Quaternion.Euler(0, 0, rootRotNow);
            hairT.localPosition = new Vector3(V(l.hairRoot).x - V(l.neck).x - turn * M.hairParallax, hairT.localPosition.y, 0);
            hairT.localScale = new Vector3(1, hairSkin ? 1 : 1 + Mathf.Clamp(-accHair.y * M.hairStretch, -0.08f, 0.08f), 1);
            if (hairSkin) { SolveHairBones(headWorld + rootRotNow, dt); DeformHairSkin(); }
            float hipsWorld = sHip[2].x + rootRot;
            skirtP.Step(-hipsWorld * M.skirtGravity, M.skirtK, M.skirtZ, -accWaist.x * M.skirtInertia * 10, dt);
            float spread = (foot[1].localPosition.x - foot[0].localPosition.x) - (ankleRest[1].x - ankleRest[0].x);
            Set(skirtT, 0, 0, Mathf.Clamp(skirtP.a, -20, 20), 1 + Mathf.Clamp(spread / L, -0.3f, 0.5f) * M.skirtFlare, 1 + Mathf.Clamp(-accWaist.y * 0.004f, -0.05f, 0.05f));
            for (int i = 0; i < 2; i++)
            {
                // 손: 아래팔이 도는 만큼 반대로 늦게 (손목 진자)
                var em = RootSpaceMatrix(elbow[i]);
                float foreRot = Mathf.Atan2(em.m10, em.m00) * Mathf.Rad2Deg;
                float v = Mathf.DeltaAngle(prevForeRot[i], foreRot) / Mathf.Max(dt, 1e-4f);
                float aa = (v - prevForeVel[i]) / Mathf.Max(dt, 1e-4f);
                prevForeRot[i] = foreRot; prevForeVel[i] = v;
                handP[i].Step(p.wrist[i], M.handK, M.handZ, -Mathf.Clamp(aa, -4e4f, 4e4f) * M.handInertia * (i == 0 ? 1 : -1) * flip, dt);
                Rot(wrist[i], Mathf.Clamp(handP[i].a, -60, 60));
            }
        }

        // 팔 목표 각 (화면, 시계 + = 왼팔이 바깥으로). 정면·뒤는 raise 그대로, 옆모습은 앞뒤 흔들기(swing)가 화면 회전이 된다
        // (앞 = +x = 반시계). 들어 올린 팔(raise)도 옆에서는 앞-위로. armScreen이면 동작이 이미 화면 각으로 준 값 (IK)
        float ArmTarget(Rig2Pose p, int i, string vk)
        {
            float raise = p.raise[i];
            if (vk != "side" || p.armScreen) return raise;
            return -(p.swing[i] + Mathf.Max(0, raise - M.armRest));
        }

        // 뼈 하나를 화면 두 점 사이에 놓기 (root 공간): 바인드 방향(왼쪽 기준, 오른쪽은 뒤집음)에서 돌리고, 길이 비율만큼 늘인다
        static void SetBone(Transform t, Vector2 from, Vector2 to, Vector2 bind, float m)
        {
            float dx = to.x - from.x, dy = to.y - from.y, len2 = dx * dx + dy * dy;
            // 세로로만 늘이므로 늘인 바인드 벡터 (m·bx, s·by)의 길이가 목표 길이가 되게 s를 고른다
            float s = Mathf.Sqrt(Mathf.Max(0.04f, (len2 - bind.x * bind.x) / Mathf.Max(1e-6f, bind.y * bind.y)));
            float ang = (Mathf.Atan2(dy, dx) - Mathf.Atan2(s * bind.y, m * bind.x)) * Mathf.Rad2Deg;
            Set(t, from.x, from.y, ang, m, s);
        }

        // root 안쪽 공간 (발이 사는 공간): t에서 root 바로 아래까지의 로컬 행렬 곱 (rig2.js RootLocalMatrix와 같다)
        Matrix4x4 RootLocalMatrix(Transform t)
        {
            var m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
            for (var x = t.parent; x != null && x != root; x = x.parent) m = Matrix4x4.TRS(x.localPosition, x.localRotation, x.localScale) * m;
            return m;
        }
        Vector2 RootLocal(Transform t) => RootLocalMatrix(t).MultiplyPoint3x4(Vector3.zero);
        // root 바깥 공간 (발바닥 원점, root 변환 적용 후) → 진자 가속도 계산용
        Matrix4x4 RootSpaceMatrix(Transform t) => Matrix4x4.TRS(root.localPosition, root.localRotation, root.localScale) * RootLocalMatrix(t);
        Vector2 RootSpace(Transform t) => RootSpaceMatrix(t).MultiplyPoint3x4(Vector3.zero);

        readonly Vector2[] accOut = new Vector2[4];
        void Accel(float dt, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var pts = new[] { p0, p1, p2, p3 };
            var off = vel * time;
            for (int k = 0; k < 4; k++)
            {
                var P = pts[k] + off;
                var v = hasPrevPts ? (P - prevPts[k]) / Mathf.Max(dt, 1e-4f) : Vector2.zero;
                var v0 = hasPrevVel ? prevVel[k] : v;
                var a = (v - v0) / Mathf.Max(dt, 1e-4f);
                accOut[k] = new Vector2(Mathf.Clamp(a.x, -60, 60), Mathf.Clamp(a.y, -60, 60));
                prevVel[k] = v; prevPts[k] = P;
            }
            hasPrevPts = hasPrevVel = true;
        }

        // ───── 머리카락 뼈 사슬 스킨 (rig2.js MakeHairSkin · unity.js drawSkinned) ─────
        // 그림 한 장을 가로 띠(SkinStrip px)로 나눈 메시: 줄마다 정점 2개(왼끝·오른끝). 매 프레임 줄마다 뼈 변형을 섞어 정점을 옮긴다.
        void SetupHairSkin(Rig2Look l, Vector2 N, Vector2 SL, Sprite sp)
        {
            skinSprite = sp; skinSplit = -1; nBones = 1;
            if (hairSkinMR != null) hairSkinMR.enabled = sp != null;
            if (hairSkinLowMR != null) hairSkinLowMR.enabled = false;
            if (sp == null) return;
            nBones = Mathf.Min(MaxBones, l.hairBones.Length);
            for (int i = 0; i < nBones; i++)
            {
                boneRest[i] = V(l.hairBones[i]) - N;
                boneOff[i] = i == 0 ? boneRest[0] : boneRest[i] - boneRest[i - 1];
                boneRot[i] = 0;
            }
            Set(hairT, boneRest[0].x, boneRest[0].y, 0);
            skinHairY = l.hair.y - N.y;
            Set(hairSkinMF.transform, l.hair.x - N.x, skinHairY, 0);
            if (hairSkinLowMF != null) Set(hairSkinLowMF.transform, l.hair.x - N.x, skinHairY, 0);
            // 정점 (그림 공간, 피벗 원점) · UV
            Rect tr = sp.textureRect; float ppu = sp.pixelsPerUnit, w = tr.width, h = tr.height;
            Vector2 pv = sp.pivot;
            float tw = sp.texture.width, th = sp.texture.height;
            int rows = Mathf.CeilToInt(h / SkinStrip) + 1;
            skinRestV = new Vector3[rows * 2]; skinV = new Vector3[rows * 2];
            var uv = new Vector2[rows * 2];
            for (int r = 0; r < rows; r++)
            {
                float py = Mathf.Min(r * SkinStrip, h);                 // 위에서 내려온 픽셀 줄
                float y = (h - py - pv.y) / ppu;
                skinRestV[r * 2] = new Vector3(-pv.x / ppu, y, 0); skinRestV[r * 2 + 1] = new Vector3((w - pv.x) / ppu, y, 0);
                uv[r * 2] = new Vector2(tr.x / tw, (tr.y + h - py) / th);
                uv[r * 2 + 1] = new Vector2((tr.x + w) / tw, (tr.y + h - py) / th);
            }
            // 옆모습: 어깨 윗선(shoulderL + hairSplitUp) 높이 줄에서 위(45)·아래(5, 몸통 뒤) 두 메시로 나눈다
            if (viewKey == "side" && hairSkinLowMF != null && hairSkinLowMR != null)
            {
                float top = skinHairY + (h - pv.y) / ppu, ySplit = SL.y + M.hairSplitUp - N.y;
                skinSplit = Mathf.Clamp(Mathf.RoundToInt((top - ySplit) * ppu / SkinStrip), 0, rows - 1);
            }
            skinMesh = BuildStripMesh(skinMesh, uv, 0, skinSplit >= 0 ? skinSplit : rows - 1);
            hairSkinMF.sharedMesh = skinMesh;
            if (skinSplit >= 0)
            {
                skinLowMesh = BuildStripMesh(skinLowMesh, uv, skinSplit, rows - 1);
                hairSkinLowMF.sharedMesh = skinLowMesh; hairSkinLowMR.enabled = true;
            }
            if (skinMPB == null) skinMPB = new MaterialPropertyBlock();
            skinMPB.SetTexture("_MainTex", sp.texture);
            hairSkinMR.SetPropertyBlock(skinMPB);
            if (hairSkinLowMR != null) hairSkinLowMR.SetPropertyBlock(skinMPB);
            boneHasPrev = false;
            ComputeBones();
            DeformHairSkin();
        }

        // 줄 r0~r1 사이 띠 메시 (정점은 전체 배열 그대로, 삼각형만 그 범위)
        Mesh BuildStripMesh(Mesh m, Vector2[] uv, int r0, int r1)
        {
            if (m == null) { m = new Mesh { name = "HairSkin" }; m.MarkDynamic(); }
            m.Clear();
            m.vertices = skinRestV; m.uv = uv;
            var tri = new List<int>();
            for (int r = r0; r < r1; r++) { int a = r * 2; tri.Add(a); tri.Add(a + 2); tri.Add(a + 1); tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3); }
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        void ComputeBones()
        {
            boneM[0] = Matrix4x4.TRS(hairT.localPosition, hairT.localRotation, hairT.localScale);
            for (int i = 1; i < nBones; i++) boneM[i] = boneM[i - 1] * Matrix4x4.TRS(boneOff[i], Quaternion.Euler(0, 0, boneRot[i]), Vector3.one);
        }

        // 뼈 1… : 윗뼈의 세상 각도만큼 아래로 늘어지려 하고(중력), 그 관절의 가속도에 밀린다. 아래 뼈일수록 무르다(k × hairSegK^n)
        void SolveHairBones(float rootWorld, float dt)
        {
            ComputeBones();
            var neckRoot = RootSpaceMatrix(neck);
            float parentWorld = rootWorld;
            for (int i = 1; i < nBones; i++)
            {
                Vector2 P = (Vector2)(neckRoot * boneM[i]).MultiplyPoint3x4(Vector3.zero) + vel * time;
                Vector2 v = boneHasPrev ? (P - bonePrevP[i]) / Mathf.Max(dt, 1e-4f) : Vector2.zero;
                Vector2 a = boneHasPrev ? (v - bonePrevV[i]) / Mathf.Max(dt, 1e-4f) : Vector2.zero;
                bonePrevP[i] = P; bonePrevV[i] = v;
                float k = M.hairK * Mathf.Pow(M.hairSegK, i);
                hairSegP[i - 1].Step(-parentWorld * M.hairGravity - vel.x * M.hairDrag, k, M.hairSegZ, -Mathf.Clamp(a.x, -60, 60) * M.hairSegInertia * 10, dt);
                boneRot[i] = Mathf.Clamp(hairSegP[i - 1].a, -M.hairSegMax, M.hairSegMax);
                parentWorld += boneRot[i];
            }
            boneHasPrev = true;
            ComputeBones();
        }

        // 줄마다: 정수리 띠(뼈 0 위 headBand)까지는 머리에 붙음, 그 아래는 "뼈 k 구간의 가운데"끼리 부드럽게 섞는다
        void DeformHairSkin()
        {
            if (!hairSkin || skinSprite == null || skinRestV == null || skinMesh == null) return;
            int n = nBones;
            var Ms = new Matrix4x4[n + 1]; var c = new float[n + 1];
            Ms[0] = Matrix4x4.identity; c[0] = boneRest[0].y + M.hairSkinHeadBand;
            for (int k = 0; k < n; k++)
            {
                Ms[k + 1] = boneM[k] * Matrix4x4.Translate(-(Vector3)boneRest[k]);
                c[k + 1] = k < n - 1 ? (boneRest[k].y + boneRest[k + 1].y) / 2 : boneRest[k].y - M.hairSkinTail;
            }
            var loc = Matrix4x4.Translate(hairSkinMF.transform.localPosition);
            var locInv = loc.inverse;
            for (int vi = 0; vi < skinRestV.Length; vi += 2)
            {
                float y = skinHairY + skinRestV[vi].y;                  // 목 공간 높이
                Matrix4x4 T = Ms[n];
                if (y >= c[0]) T = Ms[0];
                else
                    for (int i = 0; i < n; i++)
                        if (y >= c[i + 1])
                        {
                            float t = (c[i] - y) / (c[i] - c[i + 1]); t = t * t * (3 - 2 * t);
                            T = LerpM(Ms[i], Ms[i + 1], t);
                            break;
                        }
                var X = locInv * T * loc;
                skinV[vi] = X.MultiplyPoint3x4(skinRestV[vi]); skinV[vi + 1] = X.MultiplyPoint3x4(skinRestV[vi + 1]);
            }
            skinMesh.vertices = skinV; skinMesh.RecalculateBounds();
            if (skinSplit >= 0 && skinLowMesh != null) { skinLowMesh.vertices = skinV; skinLowMesh.RecalculateBounds(); }
        }

        static Matrix4x4 LerpM(Matrix4x4 a, Matrix4x4 b, float t)
        {
            var o = Matrix4x4.identity;
            o.m00 = Mathf.Lerp(a.m00, b.m00, t); o.m01 = Mathf.Lerp(a.m01, b.m01, t); o.m03 = Mathf.Lerp(a.m03, b.m03, t);
            o.m10 = Mathf.Lerp(a.m10, b.m10, t); o.m11 = Mathf.Lerp(a.m11, b.m11, t); o.m13 = Mathf.Lerp(a.m13, b.m13, t);
            return o;
        }

        // 표정: 동작이 정한 얼굴 + 깜빡임(가끔 두 번) + 누른 직후 놀람→웃음
        void UpdateFace(Rig2Pose p, float dt)
        {
            string face = p.face ?? "normal";
            blinkT -= dt;
            if (blinkT <= 0) { blink2 = !blink2 && rng.value < 0.25f; blinkT = blink2 ? 0.28f : rng.Range(2.2f, 4.8f); }
            bool blinking = blinkT < 0.13f;
            if (pokeT < 0.25f) face = "surprise"; else if (pokeT < 1.1f) face = "happy";
            if (blinking && (face == "normal" || face == "effort")) face = "blink";
            var l = lv ?? look;
            if (face == shownFace && l == shownFaceLook) return;   // 같은 얼굴이면 그림을 다시 찾지 않는다
            shownFace = face; shownFaceLook = l;
            // 옆·뒤 그림은 얼굴이 머리 그림에 있다 (face "")
            faceSR.sprite = !string.IsNullOrEmpty(l.face) ? Spr(l.face + face) ?? Spr(l.face + "normal") : null;
        }
    }

    // ───────────────────────── 동작 상수 (JSON "motion") ─────────────────────────
    // 단위: 각도 = 도, 길이 = ×다리 길이(L: 엉덩이~발바닥) 또는 ×키(H), 시간 = 초. 스프링 = k(강도) · z(감쇠비, 1 = 넘치지 않음)
    // 리그2(★2·★3) 동작 상수 (Tools/rig-web에서 맞춘 값 = rig2.js MOTION_DEFAULTS). spirit_rig2.json "motion"이 있으면 그 값을 쓴다
    [System.Serializable, UnityEngine.Scripting.Preserve]
    public class Rig2Motion
    {
        public float blend = 0.22f;
        public float turnTime = 0.4f, turnPinch = 0.32f, turnPinchW = 16, maxResYaw = 35;
        public float armSwingSide = 24, rungGap = 0.26f, ladderSpeed = 0.55f, ladderReachUp = 0.3f, ladderArmStretch = 1.3f, ladderHalfW = 0.95f, trunkGap = 0.3f, trunkSpeed = 0.5f, trunkReach = 0.32f;
        // 스프링
        public float armK = 260, armZ = 0.9f, elbowK = 170, elbowZ = 0.7f, bodyK = 300, bodyZ = 0.9f, headK = 120, headZ = 0.62f;
        public float hairK = 38, hairZ = 0.32f, hairInertia = 9, hairGravity = 0.8f, hairDrag = 0.5f, hairStretch = 0.015f;
        public float skirtK = 85, skirtZ = 0.38f, skirtInertia = 6, skirtGravity = 0.55f, skirtFlare = 0.1f;
        public float handK = 140, handZ = 0.45f, handInertia = 0.05f;
        public float capK = 160, capZ = 0.28f, capInertia = 8, capSquash = 0.05f;
        public float squashK = 320, squashZ = 0.32f;
        public float headStab = 0.7f;
        // 머리카락 뼈 사슬 (hairBones) · 소매
        public float hairSegK = 0.72f, hairSegZ = 0.34f, hairSegInertia = 5, hairSegMax = 12, hairSegRootMax = 5, hairSkinHeadBand = -0.1f, hairSkinTail = 0.45f, hairSplitUp = 0.12f;
        public float sleeveFollow = 0.55f;
        // 고개 돌림
        public float faceShift = 0.04f, hairParallax = 0.03f, headTurnShift = 0.07f, headTurnSquash = 0.07f, headTurnTilt = 4;
        // 숨쉬기 · 무게 옮기기 (대기)
        public float armRest = 7, elbowRest = 14;
        public float breathPeriod = 3.4f, breathAmt = 0.016f, shiftPeriod = 8, shiftX = 0.07f, shiftTilt = 3.5f, idleHead = 2.2f;
        public float fidgetMin = 3, fidgetMax = 7;
        // 걷기
        public float walkSpeed = 0.9f, strideMax = 0.9f, cadenceMin = 2.2f, cadenceMax = 5, stance = 0.56f, liftH = 0.28f;
        public float walkDip = 0.05f, walkSway = 0.06f, walkTilt = 4, walkCounter = 0.6f, walkLean = 4, walkTurn = 0.3f, walkYaw = 10, depthK = 0.32f, footIn = 0, toeOff = 16, toeUp = 14, heelLift = 0.07f, kneeSoft = 0.03f, liftPeak = 0.38f, kneeOut = 0.12f;
        public float armSwing = 20, armBendFwd = 26, armFore = 0.14f;
        public float carrySpeed = 0.7f, carryLean = 8, carryDip = 1.7f, carryRaise = 26, carryBend = 112;
        // 일하기 (따기)
        public float workPeriod = 1.15f, workReach = 150, workRise = 0.1f;
        // 뛰기 · 놀람 · 폴짝 · 착지
        public float jumpH = 0.3f, jumpCrouch = 0.3f, surpriseH = 0.07f, hopH = 0.12f, hopPeriod = 0.42f, landDip = 0.35f, landSquash = 0.2f;
        // 춤 · 하트 · 잠 · 들림 · 떨어짐 · 오르기
        public float danceBeat = 0.48f, danceSway = 0.1f, heartSway = 7, sleepDroop = 18;
        public float heldKick = 28, heldFlail = 45, hangK = 26, hangZ = 0.12f;
        public float climbSpeed = 0.55f, climbReach = 0.62f, climbStep = 0.3f;
    }

    // 동작 목록: loop = 계속 · len = 한 번 (초). FarmCritter가 넘기는 이름 + 대기 중 짧은 행동(fidget) + 착지·폴짝 (rig2.js MOTIONS)
    public static class Rig2Motions
    {
        public class Info { public bool loop; public float len; public bool fidget; }
        static Info Loop_() => new Info { loop = true };
        static Info Once(float len, bool fidget = false) => new Info { len = len, fidget = fidget };
        public static readonly Dictionary<string, Info> All = new Dictionary<string, Info>
        {
            ["idle"] = Loop_(), ["walk"] = Loop_(), ["carry"] = Loop_(), ["work"] = Loop_(),
            ["climb"] = Loop_(), ["climbDown"] = Loop_(), ["ladder"] = Loop_(), ["ladderDown"] = Loop_(),
            ["hop"] = Loop_(), ["dance"] = Loop_(), ["heart"] = Loop_(), ["sleep"] = Loop_(), ["held"] = Loop_(), ["fall"] = Loop_(),
            ["jump"] = Once(0.9f), ["surprise"] = Once(0.9f), ["spin"] = Once(0.8f), ["land"] = Once(0.7f),
            ["wave"] = Once(1.7f, true), ["stretch"] = Once(1.9f, true), ["look"] = Once(2.2f, true), ["hopOnce"] = Once(0.75f, true), ["yawn"] = Once(2.3f, true),
        };
        public static readonly string[] Fidgets = { "wave", "stretch", "look", "hopOnce", "yawn" };
        public static Info Get(string name) => name != null && All.TryGetValue(name, out var i) ? i : null;
        public static bool Loop(string name) { var i = Get(name); return i == null || i.loop; }
    }

    // 목표 자세. 각도: raise + = 팔을 바깥(위)으로, bend + = 팔꿈치를 안으로 굽힘, foot X/Y = 쉴 때 발목 자리에서 옮김(리그 단위). [0] = L · [1] = R
    public class Rig2Pose
    {
        public float air, sqX = 1, sqY = 1, hipX, hipY, hipRot, chestRot, breath, headRot, headTurn, spin, capRot, yaw;
        public float[] raise = new float[2], bend = new float[2], wrist = new float[2], fore = { 1, 1 },
            footX = new float[2], footY = new float[2], footZ = new float[2], footRot = new float[2], front = new float[2], swing = new float[2];
        public string face = "normal";
        public bool hang, grounded = true, armScreen;

        public static Rig2Pose Rest(Rig2Motion M)
        {
            var p = new Rig2Pose();
            p.raise[0] = p.raise[1] = M.armRest; p.bend[0] = p.bend[1] = M.elbowRest;
            return p;
        }

        public Rig2Pose Clone()
        {
            var o = (Rig2Pose)MemberwiseClone();
            o.raise = (float[])raise.Clone(); o.bend = (float[])bend.Clone(); o.wrist = (float[])wrist.Clone(); o.fore = (float[])fore.Clone();
            o.footX = (float[])footX.Clone(); o.footY = (float[])footY.Clone(); o.footZ = (float[])footZ.Clone(); o.footRot = (float[])footRot.Clone();
            o.front = (float[])front.Clone(); o.swing = (float[])swing.Clone();
            return o;
        }

        static float Lu(float a, float b, float t) => a + (b - a) * t;
        static void Lu(float[] o, float[] a, float[] b, float t) { o[0] = Lu(a[0], b[0], t); o[1] = Lu(a[1], b[1], t); }
        public static Rig2Pose Mix(Rig2Pose a, Rig2Pose b, float t)
        {
            var o = b.Clone();
            o.air = Lu(a.air, b.air, t); o.sqX = Lu(a.sqX, b.sqX, t); o.sqY = Lu(a.sqY, b.sqY, t);
            o.hipX = Lu(a.hipX, b.hipX, t); o.hipY = Lu(a.hipY, b.hipY, t); o.hipRot = Lu(a.hipRot, b.hipRot, t);
            o.chestRot = Lu(a.chestRot, b.chestRot, t); o.breath = Lu(a.breath, b.breath, t); o.headRot = Lu(a.headRot, b.headRot, t);
            o.headTurn = Lu(a.headTurn, b.headTurn, t); o.spin = Lu(a.spin, b.spin, t); o.capRot = Lu(a.capRot, b.capRot, t); o.yaw = Lu(a.yaw, b.yaw, t);
            Lu(o.raise, a.raise, b.raise, t); Lu(o.bend, a.bend, b.bend, t); Lu(o.wrist, a.wrist, b.wrist, t); Lu(o.fore, a.fore, b.fore, t);
            Lu(o.footX, a.footX, b.footX, t); Lu(o.footY, a.footY, b.footY, t); Lu(o.footZ, a.footZ, b.footZ, t); Lu(o.footRot, a.footRot, b.footRot, t);
            Lu(o.front, a.front, b.front, t); Lu(o.swing, a.swing, b.swing, t);
            o.face = t < 0.5f ? a.face : b.face;
            o.hang = b.hang; o.grounded = b.grounded; o.armScreen = t < 0.5f ? a.armScreen : b.armScreen;
            return o;
        }
    }

    // 스프링 (목표를 따라감): c = 2·z·√k
    public class Spring
    {
        public float x, v;
        public void Step(float target, float k, float z, float dt) { float c = 2 * z * Mathf.Sqrt(k); v += ((target - x) * k - v * c) * dt; x += v * dt; }
        public void Snap(float val) { x = val; v = 0; }
    }
    // 진자: 매달린 점의 가속도(force)로 흔들림 (머리카락·치마·손·갓)
    public class Pendulum
    {
        public float a, v;
        public void Step(float target, float k, float z, float force, float dt) { float c = 2 * z * Mathf.Sqrt(k); v += ((target - a) * k - v * c + force) * dt; a += v * dt; }
    }

    // 결정적 난수 (unity.js Rng: xorshift32). Range(a, b)는 a·b가 둘 다 정수면 정수를 준다 (웹과 같게)
    public class Rng
    {
        uint s;
        public Rng(uint seed) { s = seed == 0 ? 1 : seed; }
        public float value { get { uint x = s; x ^= x << 13; x ^= x >> 17; x ^= x << 5; s = x; return (float)(s / 4294967296.0); } }
        public int Range(int a, int b) => a + Mathf.FloorToInt(value * (b - a));
        public float Range(float a, float b) => a == Mathf.Round(a) && b == Mathf.Round(b) ? a + Mathf.Floor(value * (b - a)) : a + value * (b - a);
    }

    // ───────────────────────── 데이터 (spirit_rig2.json) ─────────────────────────
    [System.Serializable, UnityEngine.Scripting.Preserve] public class Rig2Part { public string sprite; public float x, y; [UnityEngine.Scripting.Preserve] public Rig2Part() { } }
    [UnityEngine.Scripting.Preserve]
    public class Rig2Look
    {
        [UnityEngine.Scripting.Preserve] public Rig2Look() { }   // Newtonsoft가 만든다 (WebGL 코드 줄이기에서 지워지지 않게)
        public string id, kind, face;
        public int form;
        public float height, k = 1, gradeScale = 1;
        public float[][] hairBones;                                        // 머리카락 뼈 사슬 (첫 뼈 = hairRoot)
        public Rig2Part sleeve;                                            // 소매 (옆·뒷모습)
        // ★2·★3
        public Rig2Part chest, skirt, collar, head, hair, upperArm, foreArm, hand, leg, thigh, shin, shoe;
        public float[] waist, neck, hairRoot, capTop, shoulderL, shoulderR, elbow, wrist, hipL, hipR, knee, ankle;
        public float? sole, legScale, lift;
        // ★0·★1
        public Rig2Part cap, stem, footL, footR, nubL, nubR;
        public float[] ankleR, capJoint, nubPivotL, nubPivotR;

        [Newtonsoft.Json.JsonIgnore] JObject src;
        [Newtonsoft.Json.JsonIgnore] readonly Dictionary<string, Rig2Look> views = new Dictionary<string, Rig2Look>();

        public static Rig2Look From(JObject o) { var l = o.ToObject<Rig2Look>(); l.src = o; return l; }
        // 옆(side)·뒤(back) 모습: 정면 값 위에 그 칸만 덮어쓴 것 (rig2.js ViewData). 없으면 null
        public Rig2Look View(string key)
        {
            if (src == null || !(src[key] is JObject over)) return null;
            if (views.TryGetValue(key, out var v)) return v;
            var merged = (JObject)src.DeepClone();
            merged.Remove("side"); merged.Remove("back");
            foreach (var prop in over.Properties()) merged[prop.Name] = prop.Value.DeepClone();
            v = merged.ToObject<Rig2Look>();
            views[key] = v;
            return v;
        }
    }

    public static class Rig2Data
    {
        static Dictionary<string, Rig2Look> looks;
        static Rig2Motion motion; static MushMotion mush;
        public static Rig2Motion Motion { get { Load(); return motion; } }
        public static MushMotion Mush { get { Load(); return mush; } }
        public static Rig2Look Look(string id) { Load(); return id != null && looks.TryGetValue(id, out var l) ? l : null; }
        public static IEnumerable<Rig2Look> All() { Load(); return looks.Values; }

        static void Load()
        {
            if (looks != null) return;
            looks = new Dictionary<string, Rig2Look>(); motion = new Rig2Motion(); mush = new MushMotion();
            var ta = Resources.Load<TextAsset>("Rig/spirit_rig2");
            if (ta == null) return;
            var doc = JObject.Parse(ta.text);
            if (doc["looks"] is JArray arr) foreach (var o in arr) if (o is JObject jo) { var l = Rig2Look.From(jo); looks[l.id] = l; }
            // 상수는 하나라도 바꾸면 전부 들어간다 (웹 저장). 없는 칸은 기본값
            if (doc["motion"] is JObject m) Newtonsoft.Json.JsonConvert.PopulateObject(m.ToString(), motion);
            if (doc["mushMotion"] is JObject mm) Newtonsoft.Json.JsonConvert.PopulateObject(mm.ToString(), mush);
        }
    }
}
