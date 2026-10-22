// 정령 리그2 (2026-10-18): 버섯 정령 관절 리그를 처음부터 다시. Unity C# (SpiritRig2.cs)로 1:1 옮길 수 있게
// 이름·계층·계산 순서를 C# 모양으로 썼다 (Transform.localPosition/localRotation/localScale, sortingOrder, Mathf).
//
// 데이터: Assets/Resources/Rig/spirit_rig2.json  { looks: [Rig2Look...], motion: Rig2Motion }
//   좌표 = 리그 단위 (그림 px / 100), 원점 = 발바닥 가운데, y 위. 부품 x,y = 서 있는 자세(바인드)에서 그림 중심.
//   팔·다리는 보는 쪽 왼쪽(L) 하나만 적고, 오른쪽은 어깨/엉덩이를 축으로 뒤집어 쓴다 (그림도 좌우 반전).
//
// 계층 (★2·★3):
//   root(발바닥, 크기·찌그러짐·공중) ─ hips(허리: 골반 이동·기울기)
//      ├ skirt(허리 축 진자) · chest(허리 축 상체 굽힘·숨) ─ collar
//      │     ├ neck(목 축 고개) ─ bounce(갓 통통) ─ head · face   /  hairT(정수리 축 진자) ─ hair
//      │     └ shoulder[i](뒤집기만) ─ upper[i](어깨 각) ─ elbow[i] ─ wrist[i]
//      └ hip[i](뒤집기만) ─ thigh[i](IK 각·길이)
//   foot[i](root 공간, 발목 목표 위치) ─ footRot[i] ─ shoe      ← 발은 땅에 붙인다 (IK 목표)
// 그리는 순서 (sortingOrder, 뒤→앞): 뒷머리 -80 · 먼 쪽 팔 -70~-50 · 다리 -30 · 신발 -20 · 치마 0 · 가슴 10 · 머리 40 · 얼굴 50 · 팔 80~100 · 목 프릴 120
//   뒷모습: 팔 20~22 · 머리 40 · 머리카락 45 · 갓 덮개(<머리>_cap) 47
// 계층 (★0·★1): root ─ body(발바닥 축 찌그러짐) ─ stem · face / capT(갓 테두리 축 진자·찌그러짐) ─ cap,  foot[i] (root 공간)
//
// 움직임 = ① 동작 함수가 목표 자세(Rig2Pose)를 만든다 → ② 동작이 바뀌면 이전 자세에서 섞는다(blend)
//          → ③ 팔·몸은 스프링으로 부드럽게(팔꿈치는 조금 늦게 = 겹침) → ④ 2차 움직임(머리카락·치마·손·갓)은
//          관절점의 실제 가속도로 흔들리는 진자 (무게감·관성·따라오는 동작) → ⑤ 다리는 IK로 발을 땅에 붙인다.
import { Mathf, Rng, Vector2, Transform, SpriteRenderer, M2 } from "./unity.js";

// ───────────────────────── 동작 상수 (Rig2Motion, JSON "motion") ─────────────────────────
// 단위: 각도 = 도, 길이 = ×다리 길이(L: 엉덩이~발바닥) 또는 ×키(H), 시간 = 초. 스프링 = k(강도) · z(감쇠비, 1 = 넘치지 않음)
export const MOTION_DEFAULTS = {
  blend: 0.22,            // 동작이 바뀔 때 섞는 시간
  turnTime: 0.4, turnPinch: 0.32, turnPinchW: 16, maxResYaw: 35,   // 돌아서기: 걸리는 시간 · 그림이 바뀌는 순간 가로로 좁히는 정도 · 그 폭(도) · 한 그림이 더 돌 수 있는 각
  armSwingSide: 24, rungGap: 0.26, ladderSpeed: 0.55, ladderReachUp: 0.3, ladderArmStretch: 1.3, ladderHalfW: 0.95, trunkGap: 0.3, trunkSpeed: 0.5, trunkReach: 0.32,
  // 스프링
  armK: 260, armZ: 0.9, elbowK: 170, elbowZ: 0.7, bodyK: 300, bodyZ: 0.9, headK: 120, headZ: 0.62,
  hairK: 38, hairZ: 0.32, hairInertia: 9, hairGravity: 0.8, hairDrag: 0.5, hairStretch: 0.015,
  // 머리카락 뼈 사슬 (모습에 hairBones가 있을 때, 지금은 옆모습): 한 장짜리 머리카락 그림을 뼈 사슬로 휘게 한다(스킨).
  // 아래 뼈일수록 무르게(늦게·크게) → 흔들림이 위에서 아래로 번진다. 정수리~뿌리 아래 조금(headBand, 음수 = 뿌리 아래)까지는 머리에 붙어 있다 → 얼굴 옆 머리카락이 얼굴에서 떨어지지 않는다
  hairSegK: 0.72, hairSegZ: 0.34, hairSegInertia: 5, hairSegMax: 12, hairSegRootMax: 5, hairSkinHeadBand: -0.1, hairSkinTail: 0.45, hairSplitUp: 0.12,
  skirtK: 85, skirtZ: 0.38, skirtInertia: 6, skirtGravity: 0.55, skirtFlare: 0.1,
  handK: 140, handZ: 0.45, handInertia: 0.05,
  capK: 160, capZ: 0.28, capInertia: 8, capSquash: 0.05,
  squashK: 320, squashZ: 0.32,
  sleeveFollow: 0.55,     // 소매가 위팔 회전을 따라 도는 비율 (1 = 같이, 0 = 어깨에 고정)
  headStab: 0.7,         // 몸이 기울어도 머리는 이만큼 수평을 지킨다
  // 고개 돌림: 머리(갓·얼굴·앞머리) 전체를 돌리듯 = 기울이고 · 가로로 조금 좁히고 · 옆으로 조금 옮긴다.
  // 얼굴 층은 머리에 붙어 있고, 3/4 느낌으로 얼굴 폭의 5% 이내만 옮긴다 (예전 0.32는 눈이 얼굴 밖으로 나가 떨어져 보였다)
  faceShift: 0.04, hairParallax: 0.03, headTurnShift: 0.07, headTurnSquash: 0.07, headTurnTilt: 4,
  // 숨쉬기 · 무게 옮기기 (대기)
  armRest: 7, elbowRest: 14,
  breathPeriod: 3.4, breathAmt: 0.016, shiftPeriod: 8, shiftX: 0.07, shiftTilt: 3.5, idleHead: 2.2,
  fidgetMin: 3, fidgetMax: 7,
  // 걷기
  walkSpeed: 0.9, strideMax: 0.9, cadenceMin: 2.2, cadenceMax: 5, stance: 0.56, liftH: 0.28,
  walkDip: 0.05, walkSway: 0.06, walkTilt: 4, walkCounter: 0.6, walkLean: 4, walkTurn: 0.3, walkYaw: 10, depthK: 0.32, footIn: 0.0, toeOff: 16, toeUp: 14, heelLift: 0.07, kneeSoft: 0.03, liftPeak: 0.38, kneeOut: 0.12,
  armSwing: 20, armBendFwd: 26, armFore: 0.14,
  carrySpeed: 0.7, carryLean: 8, carryDip: 1.7, carryRaise: 26, carryBend: 112,
  // 일하기 (따기)
  workPeriod: 1.15, workReach: 150, workRise: 0.1,
  // 뛰기 · 놀람 · 폴짝 · 착지
  jumpH: 0.3, jumpCrouch: 0.3, surpriseH: 0.07, hopH: 0.12, hopPeriod: 0.42, landDip: 0.35, landSquash: 0.2,
  // 춤 · 하트 · 잠 · 들림 · 떨어짐 · 오르기
  danceBeat: 0.48, danceSway: 0.1, heartSway: 7, sleepDroop: 18,
  heldKick: 28, heldFlail: 45, hangK: 26, hangZ: 0.12,
  climbSpeed: 0.55, climbReach: 0.62, climbStep: 0.3,
};

// 동작 목록: loop = 계속 · len = 한 번 (초). FarmCritter의 rig.motion 이름 + 대기 중 짧은 행동(fidget) + 착지·폴짝
export const MOTIONS = {
  idle: { loop: true, ko: "대기", desc: "숨쉬며 가끔 무게를 옮기고, 이따금 짧은 행동을 한다" },
  walk: { loop: true, ko: "걷기", desc: "무릎을 굽혀 발뒤꿈치부터 딛고 발끝으로 민다. 디딘 발은 땅에 붙는다" },
  carry: { loop: true, ko: "나르기", desc: "짐을 들고 무겁게 걷는다. 더 숙이고 더 깊게 내려앉는다" },
  work: { loop: true, ko: "일하기(따기)", desc: "발끝으로 서서 한 손씩 위로 뻗어 따서 바구니에 담는다" },
  climb: { loop: true, ko: "줄기 오르기(옆)", desc: "옆모습으로 줄기·덩굴을 손발 엇갈려 잡고 끌어올린다" },
  climbDown: { loop: true, ko: "줄기 내려오기(옆)", desc: "줄기 오르기를 거꾸로" },
  ladder: { loop: true, ko: "사다리 오르기(뒤)", desc: "뒷모습으로 오른손+왼발, 왼손+오른발 번갈아 가로대를 잡고 딛는다" },
  ladderDown: { loop: true, ko: "사다리 내려오기(뒤)", desc: "사다리 오르기를 거꾸로" },
  hop: { loop: true, ko: "폴짝(계단)", desc: "웅크렸다 차고 올라 발을 모으고 착지한다 (버섯 계단)" },
  dance: { loop: true, ko: "춤", desc: "박자마다 무릎으로 통통, 골반을 흔들고 팔을 번갈아 든다" },
  heart: { loop: true, ko: "하트", desc: "손을 가슴에 모으고 발끝으로 살랑살랑, 고개를 갸웃" },
  sleep: { loop: true, ko: "잠", desc: "선 채로 꾸벅꾸벅, 가끔 번쩍 깬다" },
  held: { loop: true, ko: "들림(버둥)", desc: "갓을 잡혀 매달린 채 흔들리며 팔다리를 버둥댄다" },
  fall: { loop: true, ko: "떨어짐", desc: "떨어지는 동안 팔을 들고 다리를 동동" },
  jump: { len: 0.9, ko: "점프", desc: "웅크림(예비 동작) → 차고 늘어남 → 공중 → 착지 찌그러짐" },
  surprise: { len: 0.9, ko: "놀람", desc: "예비 동작 없이 움찔 뛰며 팔을 번쩍" },
  spin: { len: 0.8, ko: "돌기", desc: "제자리에서 한 바퀴" },
  land: { len: 0.7, ko: "착지", desc: "쿵 떨어져 무릎으로 받고 찌그러졌다 돌아온다" },
  wave: { len: 1.7, ko: "손 흔들기", desc: "살짝 내렸다가 들어 올려 흔들고, 손목이 따라온다", fidget: true },
  stretch: { len: 1.9, ko: "기지개", desc: "발끝으로 서며 팔을 쭉 위로", fidget: true },
  look: { len: 2.2, ko: "두리번", desc: "고개를 왼쪽 오른쪽으로 돌려 본다", fidget: true },
  hopOnce: { len: 0.75, ko: "폴짝(한 번)", desc: "제자리에서 작게 한 번 뛴다", fidget: true },
  yawn: { len: 2.3, ko: "하품", desc: "손을 입에 대고 고개를 젖힌다", fidget: true },
  pose: { loop: true, ko: "(수동 포즈)", desc: "슬라이더로 직접 만든 자세", hidden: true },
};

// 목표 자세. 각도: raise + = 팔을 바깥(위)으로, bend + = 팔꿈치를 안으로 굽힘, foot*X/Y = 쉴 때 발목 자리에서 옮김(리그 단위)
export const POSE_KEYS = ["air", "sqX", "sqY", "hipX", "hipY", "hipRot", "chestRot", "breath", "headRot", "headTurn",
  "raiseL", "bendL", "wristL", "raiseR", "bendR", "wristR", "foreL", "foreR",
  "footLX", "footLY", "footLZ", "footLRot", "footRX", "footRY", "footRZ", "footRRot", "frontL", "frontR", "spin", "capRot", "yaw", "swingL", "swingR"];
export function RestPose(M) {
  const p = {};
  for (const k of POSE_KEYS) p[k] = 0;
  p.sqX = p.sqY = p.foreL = p.foreR = 1;
  p.raiseL = p.raiseR = M.armRest; p.bendL = p.bendR = M.elbowRest;
  p.face = "normal"; p.hang = false; p.grounded = true; p.armScreen = false;
  return p;
}

// 스프링 (목표를 따라감): c = 2·z·√k
export class Spring {
  constructor() { this.x = 0; this.v = 0; }
  Step(target, k, z, dt) { const c = 2 * z * Math.sqrt(k); this.v += ((target - this.x) * k - this.v * c) * dt; this.x += this.v * dt; }
  Snap(v) { this.x = v; this.v = 0; }
}
// 진자: 매달린 점의 가속도(force)로 흔들림 (머리카락·치마·손·갓)
export class Pendulum {
  constructor() { this.a = 0; this.v = 0; }
  Step(target, k, z, force, dt) { const c = 2 * z * Math.sqrt(k); this.v += ((target - this.a) * k - this.v * c + force) * dt; this.a += this.v * dt; }
}

const V = (a) => Vector2.of(a);
const smooth = (t) => { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); };
const bump = (t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);           // 0 → 1 → 0
const frac = (t) => t - Math.floor(t);
const seg = (u, a, b) => Mathf.Clamp01((u - a) / (b - a));              // u가 a~b 사이 어디인지 0~1

export class SpiritRig2 {
  // sprites: (name) => {img,w,h,px,py,ppu} | null  (C#: SpriteDB.Get("Characters/" + name))
  constructor(sprites, seed = 7) {
    this.sprites = sprites;
    this.M = { ...MOTION_DEFAULTS };
    this.rng = new Rng(seed);
    this.look = null; this.outfit = ""; this.flip = 1; this.scale = 1;
    this.motion = "idle"; this.motionT = 0; this.time = 0;
    this.vel = new Vector2();        // 이동 속도 (리그 단위/초, 바깥에서): 머리카락이 뒤로 날린다
    this.grabAccel = new Vector2();  // 들고 흔드는 손의 가속도 (들림)
    this.manual = null;              // 수동 포즈 (pose 동작)
    this.faceOverride = null;        // 표정 고정 (웹)
    this.autoFidget = true;
    this.fixes = { };
    this.root = new Transform("Rig2");
    this.built = false;
    this.Reset();
  }

  Reset() {
    const M = this.M;
    this.time = 0; this.motionT = 0; this.gait = 0; this.climbS = 0;
    this.prevPose = null; this.blendT = 99; this.pose = RestPose(M);
    this.sRaise = [new Spring(), new Spring()]; this.sBend = [new Spring(), new Spring()];
    this.sHip = [new Spring(), new Spring(), new Spring()]; this.sChest = new Spring(); this.sHead = new Spring(); this.sTurn = new Spring();
    this.sAir = new Spring(); this.sSqX = new Spring(); this.sSqY = new Spring();
    this.sSqX.Snap(1); this.sSqY.Snap(1);
    for (let i = 0; i < 2; i++) { this.sRaise[i].Snap(M.armRest); this.sBend[i].Snap(M.elbowRest); }
    this.hairP = new Pendulum(); this.hairSegP = [new Pendulum(), new Pendulum(), new Pendulum(), new Pendulum(), new Pendulum()]; this.skirtP = new Pendulum(); this.handP = [new Pendulum(), new Pendulum()]; this.capP = new Pendulum();
    this.capSq = new Spring(); this.hang = new Pendulum();
    this.prevPts = null; this.prevVel = null; this.prevForeRot = [0, 0]; this.prevForeVel = [0, 0];
    this.blinkT = 2.2; this.blink2 = false; this.pokeT = 99; this.fidget = null; this.fidgetT = 0; this.fidgetWait = 2.5;
    this.lastFace = "normal";
  }

  get headed() { return this.look && this.look.form >= 2; }
  Sprite(name) { return name ? this.sprites(name) : null; }
  // 작업복(outfit)이 있으면 그 그림, 없으면 기본
  OutfitSprite(name) { return (this.outfit ? this.Sprite(name + "_" + this.outfit) : null) || this.Sprite(name); }

  SR(name, parent, order) { return new SpriteRenderer(name, parent, order); }
  T(name, parent) { return new Transform(name, parent); }

  // ───── 계층 만들기 (C# BuildRig2) ─────
  Build() {
    const r = this.root;
    r.children = [];
    this.hips = this.T("Hips", r);
    this.skirtT = this.T("Skirt", this.hips); this.skirtSR = this.SR("SkirtSprite", this.skirtT, 0);
    this.chest = this.T("Chest", this.hips); this.chestSR = this.SR("ChestSprite", this.chest, 10);
    this.collarSR = this.SR("Collar", this.chest, 120);           // 목 프릴: 팔 위에 덮어 어깨 관절을 가린다
    this.neck = this.T("Neck", this.chest);
    this.bounce = this.T("Bounce", this.neck);
    this.headSR = this.SR("HeadSprite", this.bounce, 40);
    this.faceSR = this.SR("Face", this.headSR.transform, 50);
    this.hairT = this.T("HairRoot", this.neck);
    this.hairSR = this.SR("Hair", this.hairT, -80);               // 뒷머리: 몸통·팔·다리보다 뒤
    // 머리카락 뼈 사슬 (hairBones): HairRoot(뼈 0) ─ HairB1 ─ HairB2 ─ … 그림은 목(neck) 공간에 한 장, 줄마다 가까운 뼈들을 섞어 휜다 (C#: SpriteSkin)
    this.hairBone = [this.hairT];
    for (let i = 1; i < 6; i++) this.hairBone[i] = this.T("HairB" + i, this.hairBone[i - 1]);
    this.hairSkinSR = this.SR("HairSkin", this.neck, 45);
    // 옆모습: 같은 머리카락 그림을 어깨 윗선에서 줄로 나눠 아래쪽은 몸통 뒤에 한 번 더 그린다 (가까운 어깨·몸통이 머리카락보다 앞)
    this.hairSkinLowSR = this.SR("HairSkinLow", this.neck, 5);
    this.capOverSR = this.SR("CapOver", this.headSR.transform, 47); // 갓만 오린 그림(<머리>_cap): 뒷모습에서 머리카락 위에 갓을 다시 덮는다
    this.sleeveT = []; this.sleeveSR = [];
    this.shoulder = []; this.upper = []; this.elbow = []; this.wrist = []; this.upperSR = []; this.foreSR = []; this.handSR = [];
    this.hip = []; this.thigh = []; this.legSR = []; this.foot = []; this.footRot = []; this.shoeSR = [];
    this.thighT = []; this.thighSR = []; this.shinT = []; this.shinSR = [];
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      this.shoulder[i] = this.T("Shoulder" + n, this.chest);     // 뒤집기만 (회전은 자식에) → 오른쪽 각도가 거울처럼
      this.upper[i] = this.T("Upper", this.shoulder[i]);
      this.upperSR[i] = this.SR("UpperSprite", this.upper[i], 80);
      this.elbow[i] = this.T("Elbow", this.upper[i]);
      this.foreSR[i] = this.SR("ForeSprite", this.elbow[i], 90);
      this.wrist[i] = this.T("Wrist", this.elbow[i]);
      this.handSR[i] = this.SR("HandSprite", this.wrist[i], 100);
      // 소매 (모습에 sleeve가 있을 때, 지금은 옆·뒷모습): 어깨 축을 덮어 위팔 이음을 가린다. 위팔 회전의 일부만 따라 돈다
      this.sleeveT[i] = this.T("Sleeve", this.shoulder[i]);
      this.sleeveSR[i] = this.SR("SleeveSprite", this.sleeveT[i], 85);
      this.hip[i] = this.T("Hip" + n, this.hips);
      this.thigh[i] = this.T("Leg", this.hip[i]);
      this.legSR[i] = this.SR("LegSprite", this.thigh[i], -30);         // (무릎 없는 예전 다리: thigh·shin 그림이 없을 때만)
      this.thighT[i] = this.T("Thigh" + n, r);                            // 허벅지·정강이는 root 공간 (IK 결과를 그대로 놓는다)
      this.thighSR[i] = this.SR("ThighSprite", this.thighT[i], -32);
      this.shinT[i] = this.T("Shin" + n, r);
      this.shinSR[i] = this.SR("ShinSprite", this.shinT[i], -31);
      this.foot[i] = this.T("Foot" + n, r);
      this.footRot[i] = this.T("FootRot", this.foot[i]);
      this.shoeSR[i] = this.SR("Shoe", this.footRot[i], -20);
    }
    // ★0·★1
    this.body = this.T("Body", r);
    this.stemSR = this.SR("Stem", this.body, 0);
    this.stemFace = this.SR("StemFace", this.stemSR.transform, 10);
    this.capT = this.T("Cap", this.body);
    this.capSR = this.SR("CapSprite", this.capT, -10);                // 갓은 대 뒤 (대 윗부분이 주름 속으로 들어감)
    this.built = true;
  }

  // ───── 모습 세우기 (C# ApplyRig2) ─────
  Apply(look, outfit = "") {
    if (!this.built) this.Build();
    this.look = look; this.outfit = outfit || "";
    const l = look, h = this.headed;
    for (const t of [this.hips]) t.SetActive(h);
    for (let i = 0; i < 2; i++) this.foot[i].SetActive(h ? !!l.shoe?.sprite : !!(i === 0 ? l.footL : l.footR));
    this.body.SetActive(!h);
    for (let i = 0; i < 2; i++) { this.thighT[i].SetActive(h); this.shinT[i].SetActive(h); }
    this.viewKey = "front"; this.lv = l;
    if (h) this.ApplyHeaded(l, true); else this.ApplyHeadless(l);
  }

  // ───── 보는 방향 (정면 front · 옆 side · 뒤 back) ─────
  // 옆·뒤 그림은 look.side / look.back 에 정면과 같은 이름의 칸만 덮어쓴다 (없으면 정면 값). 옆모습은 오른쪽을 보고, 왼쪽은 root 뒤집기.
  ViewData(key) { const l = this.look; return key === "front" || !l[key] ? l : Object.assign({}, l, l[key]); }
  HasView(key) { return key === "front" || !!(this.look && this.look[key]); }
  SetView(key) {
    if (!this.HasView(key)) key = "front";
    if (key === this.viewKey) return;
    this.viewKey = key; this.lv = this.ViewData(key);
    this.ApplyHeaded(this.lv, key !== "side");          // 옆모습은 두 팔다리가 같은 쪽을 본다 (뒤집지 않음)
  }

  ApplyHeaded(l, mirror = true) {
    this.mR = mirror ? -1 : 1;
    const W = V(l.waist), N = V(l.neck), SL = V(l.shoulderL), E = V(l.elbow), Wr = V(l.wrist), HL = V(l.hipL), A = V(l.ankle);
    this.hips.set(W.x, W.y + (this.lift || 0), 0);
    this.skirtT.set(0, 0, 0);
    this.skirtSR.sprite = this.OutfitSprite(l.skirt.sprite);
    this.skirtSR.transform.set(l.skirt.x - W.x, l.skirt.y - W.y, 0);
    this.chest.set(0, 0, 0);
    this.chestSR.sprite = this.OutfitSprite(l.chest.sprite);
    this.chestSR.transform.set(l.chest.x - W.x, l.chest.y - W.y, 0);
    this.collarSR.sprite = l.collar && l.collar.sprite ? this.OutfitSprite(l.collar.sprite) : null;
    this.collarSR.transform.set(l.collar.x - W.x, l.collar.y - W.y, 0);
    this.neck.set(N.x - W.x, N.y - W.y, 0);
    this.bounce.set(0, 0, 0);
    this.headSR.sprite = this.Sprite(l.head.sprite);
    this.headSR.transform.set(l.head.x - N.x, l.head.y - N.y, 0);
    this.capOverSR.sprite = l.head.sprite ? this.Sprite(l.head.sprite + "_cap") : null;   // 없으면 안 그림 (지금은 뒷모습만 있음)
    this.capOverSR.transform.set(0, 0, 0);
    this.faceSR.sprite = l.face ? this.Sprite(l.face + "normal") : null;
    this.faceSR.transform.set(0, 0, 0);
    const HR0 = V(l.hairRoot);
    this.hairT.set(HR0.x - N.x, HR0.y - N.y, 0);
    // 머리카락: hairBones(2개 이상)가 있으면 뼈 사슬 스킨, 없으면 예전처럼 한 장을 뿌리 진자로
    const hs = l.hair?.sprite, B = l.hairBones && l.hairBones.length >= 2 ? l.hairBones.slice(0, this.hairBone.length).map(V) : null;
    this.hairSkin = !!(B && hs && this.Sprite(hs));
    this.hairSR.sprite = this.hairSkin ? null : this.Sprite(hs);
    this.hairSR.transform.set((l.hair?.x ?? 0) - HR0.x, (l.hair?.y ?? 0) - HR0.y, 0);
    this.hairSkinSR.sprite = this.hairSkin ? this.Sprite(hs) : null;
    this.hairSkinSR.skin = null; this.hairSkinSR.skinRows = null;
    this.hairSkinLowSR.sprite = null; this.hairSkinLowSR.skin = null; this.hairSkinLowSR.skinRows = null;
    this.nBones = this.hairSkin ? B.length : 1;
    for (let i = 1; i < this.hairBone.length; i++) this.hairBone[i].SetActive(i < this.nBones);
    if (this.hairSkin) {
      this.hairT.set(B[0].x - N.x, B[0].y - N.y, 0);
      for (let i = 1; i < B.length; i++) this.hairBone[i].set(B[i].x - B[i - 1].x, B[i].y - B[i - 1].y, 0);
      this.hairSkinSR.transform.set(l.hair.x - N.x, l.hair.y - N.y, 0);
      this.hairSkinSR.skin = this.MakeHairSkin(B.map((b) => b.sub(N)), l.hair.y - N.y);
      const sp = this.hairSkinSR.sprite;
      if (this.viewKey === "side" && sp) {
        // 나누는 줄 = 어깨 윗선(shoulderL + hairSplitUp) 높이의 그림 줄. 위 = 머리 위(45), 아래 = 가슴 뒤(5)
        const top = (l.hair.y - N.y) + (1 - sp.py) * sp.h / sp.ppu, ySplit = SL.y + this.M.hairSplitUp - N.y;
        const row = Mathf.Clamp(Math.round((top - ySplit) * sp.ppu), 0, sp.h);
        this.hairSkinSR.skinRows = [0, row];
        this.hairSkinLowSR.sprite = sp; this.hairSkinLowSR.skin = this.hairSkinSR.skin; this.hairSkinLowSR.skinRows = [row, sp.h];
        this.hairSkinLowSR.transform.set(l.hair.x - N.x, l.hair.y - N.y, 0);
      }
    }
    // 길이 (IK)
    this.upperLen = E.sub(SL).magnitude; this.foreLen = Wr.sub(E).magnitude;
    // 무릎: 허벅지(엉덩이→무릎) · 정강이(무릎→발목).
    //  lift가 적혀 있으면: 몸(엉덩이 위)을 lift만큼 올려 그리고, 다리 그림·무릎·발목은 땅 좌표 그대로 (긴 다리 그림)
    //  없으면(예전 잘라 쓴 다리): legScale만큼 다리를 늘이고 몸을 그만큼 올린다
    const ls = l.legScale || 1, liftArt = l.lift != null;
    this.lift = liftArt ? l.lift : (HL.y - A.y) * (ls - 1);
    const HLb = liftArt ? HL.add(new Vector2(0, this.lift)) : HL;            // 다리 바인드의 엉덩이
    this.hipBind = HLb;
    this.legBind = A.sub(HLb); this.legLen = this.legBind.magnitude;
    const K = l.knee ? V(l.knee) : HLb.add(A).mul(0.5);
    this.legScaleV = ls; this.kneeOn = !!(l.thigh && l.thigh.sprite && this.Sprite(l.thigh.sprite));
    this.thighBind = K.sub(HLb); this.shinBind = A.sub(K);
    const lk = liftArt ? 1 : ls;
    this.thighLen = this.thighBind.magnitude * lk; this.shinLen = this.shinBind.magnitude * lk;
    this.L = Math.max(0.2, HL.y + (this.lift || 0) - (l.sole ?? 0));      // (lift를 더한 엉덩이 ~ 발바닥)   // 다리 길이 (엉덩이~발바닥): 걸음·점프 단위
    this.H = l.height;
    for (let i = 0; i < 2; i++) {
      const m = i === 0 ? 1 : this.mR;
      const S = V(i === 0 ? l.shoulderL : l.shoulderR), Hp = V(i === 0 ? l.hipL : l.hipR);
      this.shoulder[i].set(S.x - W.x, S.y - W.y, 0, m, 1);
      this.upper[i].set(0, 0, 0);
      this.upperSR[i].sprite = this.Sprite(l.upperArm.sprite);
      this.upperSR[i].transform.set(l.upperArm.x - SL.x, l.upperArm.y - SL.y, 0);
      this.elbow[i].set(E.x - SL.x, E.y - SL.y, 0);
      this.foreSR[i].sprite = this.Sprite(l.foreArm.sprite);
      this.foreSR[i].transform.set(l.foreArm.x - E.x, l.foreArm.y - E.y, 0);
      this.wrist[i].set(Wr.x - E.x, Wr.y - E.y, 0);
      this.handSR[i].sprite = this.Sprite(l.hand.sprite);
      this.handSR[i].transform.set(l.hand.x - Wr.x, l.hand.y - Wr.y, 0);
      this.sleeveT[i].set(0, 0, 0);
      this.sleeveSR[i].sprite = l.sleeve && l.sleeve.sprite ? this.OutfitSprite(l.sleeve.sprite) : null;
      if (l.sleeve) this.sleeveSR[i].transform.set(l.sleeve.x - SL.x, l.sleeve.y - SL.y, 0);
      this.hip[i].set(Hp.x - W.x, Hp.y - W.y, 0, m, 1);
      this.thigh[i].set(0, 0, 0);
      this.legSR[i].sprite = this.kneeOn ? null : this.Sprite(l.leg.sprite);
      this.legSR[i].transform.set(l.leg.x - HL.x, l.leg.y - HL.y, 0);
      this.thighSR[i].sprite = this.kneeOn ? this.Sprite(l.thigh.sprite) : null;
      this.thighSR[i].transform.set(l.thigh ? l.thigh.x - HLb.x : 0, l.thigh ? l.thigh.y - HLb.y : 0, 0);
      this.shinSR[i].sprite = this.kneeOn ? this.Sprite(l.shin.sprite) : null;
      this.shinSR[i].transform.set(l.shin ? l.shin.x - K.x : 0, l.shin ? l.shin.y - K.y : 0, 0);
      this.foot[i].set(0, 0, 0, m, 1);
      this.footRot[i].set(0, 0, 0);
      this.shoeSR[i].sprite = this.Sprite(l.shoe.sprite);
      this.shoeSR[i].transform.set(l.shoe.x - A.x, l.shoe.y - A.y, 0);
    }
    this.ankleRest = [A, new Vector2(V(l.hipR).x + this.mR * (A.x - HL.x), A.y)];
  }

  ApplyHeadless(l) {
    this.body.set(0, 0, 0);
    this.stemSR.sprite = this.Sprite(l.stem.sprite);
    this.stemSR.transform.set(l.stem.x, l.stem.y, 0);
    this.stemFace.sprite = this.Sprite(l.face + "normal");
    this.stemFace.transform.set(0, 0, 0);
    const C = V(l.capJoint);
    this.capT.set(C.x, C.y, 0);
    this.capSR.sprite = this.Sprite(l.cap.sprite);
    this.capSR.transform.set(l.cap.x - C.x, l.cap.y - C.y, 0);
    this.L = Math.max(0.2, C.y * 0.5); this.H = l.height;
    this.ankleRest = [V(l.ankle || [0, 0]), V(l.ankleR || [0, 0])];
    for (let i = 0; i < 2; i++) {
      const fp = i === 0 ? l.footL : l.footR;
      if (!fp) continue;
      this.foot[i].set(0, 0, 0, 1, 1);
      this.footRot[i].set(0, 0, 0);
      this.shoeSR[i].sprite = this.Sprite(fp.sprite);
      this.shoeSR[i].transform.set(fp.x - this.ankleRest[i].x, fp.y - this.ankleRest[i].y, 0);
    }
  }

  // ───── 동작 바꾸기: 지금 자세에서 섞어 넘어간다 ─────
  Play(name) {
    if (name === this.motion && MOTIONS[name]?.loop) return;
    this.prevPose = { ...this.pose }; this.blendT = 0;
    this.motion = name; this.motionT = 0; this.fidget = null;
    if (name === "held") { this.hang.a = 0; this.hang.v = 0; }
  }
  Poke() { this.pokeT = 0; }

  // ───── 매 프레임 (C# AnimateRig2) ─────
  Tick(dt) {
    if (!this.look) return;
    dt = Math.min(dt, 0.05);
    this.time += dt; this.motionT += dt; this.blendT += dt; this.pokeT += dt;
    const info = MOTIONS[this.motion];
    if (info && !info.loop && this.motionT >= info.len && !this.holdOneShot) this.Play("idle");
    const M = this.M;

    // 대기 중 가끔 짧은 행동 (손 흔들기·기지개·두리번·폴짝·하품)
    if (this.motion === "idle" && this.autoFidget) {
      if (!this.fidget) {
        this.fidgetWait -= dt;
        if (this.fidgetWait <= 0) {
          const list = ["wave", "stretch", "look", "hopOnce", "yawn"];
          this.fidget = list[this.rng.Range(0, list.length)]; this.fidgetT = 0;
        }
      } else {
        this.fidgetT += dt;
        if (this.fidgetT >= MOTIONS[this.fidget].len) { this.fidget = null; this.fidgetWait = this.rng.Range(M.fidgetMin, M.fidgetMax); }
      }
    }

    // ① 목표 자세
    let p = RestPose(M);
    this.Pose(this.motion, this.motionT, p, dt);
    if (this.motion === "idle" && this.fidget) {
      const q = { ...p };
      this.Pose(this.fidget, this.fidgetT, q, dt);
      const e = smooth(Math.min(this.fidgetT, MOTIONS[this.fidget].len - this.fidgetT) / 0.25);
      p = this.Mix(p, q, e); p.face = q.face;
    }
    // ② 섞기
    const bl = this.prevPose && Math.abs(this.prevPose.yaw - p.yaw) > 30 ? Math.max(M.blend, M.turnTime) : M.blend;   // 돌아설 땐 더 천천히
    if (this.prevPose && this.blendT < bl) p = this.Mix(this.prevPose, p, smooth(this.blendT / bl));
    this.pose = p;
    // ③④⑤
    if (this.headed) this.SolveHeaded(p, dt); else this.SolveHeadless(p, dt);
    this.UpdateFace(p, dt);
  }

  Mix(a, b, t) {
    const o = { ...b };
    for (const k of POSE_KEYS) o[k] = a[k] + (b[k] - a[k]) * t;
    o.face = t < 0.5 ? a.face : b.face;
    o.hang = b.hang; o.grounded = b.grounded; o.armScreen = t < 0.5 ? a.armScreen : b.armScreen;
    return o;
  }

  // ───── 동작 함수: 목표 자세 (C# Pose(...) switch) ─────
  Pose(name, t, p, dt) {
    const M = this.M, L = this.L, H = this.H;
    const info = MOTIONS[name];
    const u = info && info.len ? Mathf.Clamp01(t / info.len) : 0;
    switch (name) {
      case "idle": this.PoseIdle(t, p); break;
      case "walk": this.PoseWalk(t, p, dt, false); break;
      case "carry": this.PoseWalk(t, p, dt, true); break;
      case "work": this.PoseWork(t, p); break;
      case "climb": this.PoseClimbing(t, p, dt, 1, false); break;
      case "climbDown": this.PoseClimbing(t, p, dt, -1, false); break;
      case "ladder": this.PoseClimbing(t, p, dt, 1, true); break;
      case "ladderDown": this.PoseClimbing(t, p, dt, -1, true); break;
      case "hop": this.PoseHop(frac(t / M.hopPeriod), p, M.hopH); break;
      case "hopOnce": this.PoseIdle(t, p); this.PoseHop(u, p, M.hopH); break;
      case "jump": this.PoseJump(u, p); break;
      case "surprise": this.PoseSurprise(u, p); break;
      case "spin": this.PoseSpin(u, p); break;
      case "land": this.PoseLand(u, p); break;
      case "dance": this.PoseDance(t, p); break;
      case "heart": this.PoseHeart(t, p); break;
      case "sleep": this.PoseSleep(t, p); break;
      case "held": this.PoseHeld(t, p); break;
      case "fall": this.PoseFall(t, p); break;
      case "wave": this.PoseIdle(t, p); this.PoseWave(u, t, p); break;
      case "stretch": this.PoseIdle(t, p); this.PoseStretch(u, p); break;
      case "look": this.PoseIdle(t, p); this.PoseLook(u, p); break;
      case "yawn": this.PoseIdle(t, p); this.PoseYawn(u, p); break;
      case "pose": if (this.manual) Object.assign(p, this.manual); break;
    }
  }

  // 대기: 숨쉬기(가슴이 부풀며 어깨가 오름) + 천천히 무게 옮기기(콘트라포스토: 디딘 쪽으로 골반, 반대쪽 골반이 내려감, 상체는 반대로)
  PoseIdle(t, p) {
    const M = this.M, L = this.L;
    const br = Mathf.Sin(t * 2 * Mathf.PI / M.breathPeriod);
    p.breath = br;
    p.raiseL += br * 1.2; p.raiseR += br * 1.2;
    p.bendL += br * 2; p.bendR += br * 2;
    const sh = Math.tanh(2.5 * Mathf.Sin(t * 2 * Mathf.PI / M.shiftPeriod + 0.7)) / Math.tanh(2.5);   // 오래 머물고 부드럽게 넘어감
    p.hipX = sh * M.shiftX * L;
    p.hipRot = -sh * M.shiftTilt;                   // 디딘 쪽(+x)이면 반대쪽(-x) 골반이 내려감 → 반시계
    p.hipY = -Math.abs(sh) * 0.02 * L;
    p.chestRot = sh * M.shiftTilt * 0.7;            // 상체는 반대로 (균형)
    p.headRot = -sh * 1.5 + Mathf.Sin(t * 0.9 + 1.3) * M.idleHead;
    const free = sh > 0 ? "L" : "R";                // 쉬는 다리: 발을 살짝 안으로
    p["foot" + free + "X"] += (sh > 0 ? 1 : -1) * Math.abs(sh) * 0.04 * L;
    p.face = "normal";
  }

  // 걷기 (나르기 = 무거운 걷기). 발 디딤(stance) 동안 발목 목표는 이동 속도로 뒤로 미끄러져 "땅에 붙어" 보이고,
  // 발을 뗀 동안(swing) 들어서 앞으로. 골반 높이는 디딘 다리가 막대처럼 서는 역진자 → 한 걸음에 두 번 오르내림.
  PoseWalk(t, p, dt, carry) {
    const M = this.M, L = this.L, H = this.H;
    const speed = (carry ? M.carrySpeed : M.walkSpeed) * H;                           // 리그 단위/초
    const cadence = Mathf.Clamp(speed / (M.strideMax * L), M.cadenceMin, M.cadenceMax) * (carry ? 0.85 : 1);   // 걸음/초
    this.gait += dt * cadence / 2;                                                    // 한 주기 = 두 걸음
    const ph = frac(this.gait), beta = M.stance;
    const D = speed * beta * 2 / cadence;                                             // 디딘 동안 발이 뒤로 가는 거리
    const T = this.keepFront ? 0 : M.walkTurn;                                         // 몸을 가는 쪽으로 돌린 정도 (0 정면 ~ 1 옆). keepFront = 걸을 때 정면 유지
    this.walkInfo = { speed, cadence, D, stepLen: speed / cadence };
    const fwd = [0, 0];
    const Lg = this.kneeOn ? this.thighLen + this.shinLen : this.legLen;
    let hipH = Lg;                                      // 디딘 다리가 허락하는 엉덩이 높이 (역진자)
    for (let i = 0; i < 2; i++) {
      const q = frac(ph + (i === 0 ? 0 : 0.5));
      let d, lift = 0, rot = 0;
      if (q < beta) {
        // 디딤: 뒤꿈치 닿기(발끝 듦) → 무릎으로 받기 → 발바닥 → 뒤꿈치 들고 발끝으로 밀기. 발목은 땅에 붙어 뒤로
        const s = q / beta;
        d = D * (0.5 - s);
        rot = s < 0.15 ? M.toeUp * (1 - s / 0.15) : s > 0.6 ? -M.toeOff * smooth(seg(s, 0.6, 1)) : 0;
        lift = s > 0.6 ? M.heelLift * L * smooth(seg(s, 0.6, 1)) : 0;                  // 뒤꿈치가 들리면 발목이 올라감
        const reach = Math.sqrt(Math.max(0, Lg * Lg - d * d)) + lift;                   // 이 발로 설 수 있는 엉덩이 높이
        const dip = M.walkDip * L * (carry ? M.carryDip : 1) * Mathf.Sin(Mathf.Clamp01(s / 0.35) * Mathf.PI);   // 닿은 직후 무릎으로 받기
        hipH = Math.min(hipH, reach - dip);
      } else {
        // 뗌: 발끝 떼기 → 무릎을 굽혀 발을 뒤로 들어 올림(초반에 가장 높이) → 다리를 앞으로 뻗어 뒤꿈치부터
        const s = (q - beta) / (1 - beta);
        d = -D / 2 + D * smooth(s);
        const pk = M.liftPeak;
        lift = M.liftH * L * (carry ? 0.75 : 1) * (s < pk ? Mathf.Sin(s / pk * Mathf.PI / 2) : Mathf.Cos((s - pk) / (1 - pk) * Mathf.PI / 2));
        rot = s < 0.25 ? -M.toeOff * (1 - s / 0.25) : s > 0.7 ? M.toeUp * smooth(seg(s, 0.7, 1)) : 0;
      }
      fwd[i] = d / (D / 2);
      const n = i === 0 ? "L" : "R";
      p["front" + n] = d;
      p["foot" + n + "X"] = (i === 0 ? 1 : -1) * M.footIn * L;
      p["foot" + n + "Y"] = lift;
      p["foot" + n + "Z"] = d;                         // 앞으로 (화면 투영은 SolveHeaded가 yaw로)
      p["foot" + n + "Rot"] = rot;
    }
    // 방향: 걸을 땐 가는 쪽 옆모습 (옆 그림이 있으면), "걸을 때 정면 유지"면 정면에 가깝게
    const sideWalk = !this.keepFront && this.HasView("side");
    p.yaw = this.keepFront ? 0 : sideWalk ? 90 : M.walkYaw;
    const restH = this.kneeOn ? (V((this.lv || this.look).hipL).y + this.lift - this.ankleRest[0].y) : Lg;
    const dropMax = Math.max(0, restH - hipH) + M.kneeSoft * L;                         // 무릎을 살짝 굽힌 채 걷는다
    // 골반: 디딘 발 위로 무게 (옆으로 흔들림), 뗀 쪽 골반이 내려감, 상체는 반대로 + 가는 쪽으로 숙임
    const w = -Mathf.Cos(2 * Mathf.PI * (ph - beta / 2));                              // 왼발 디딤 한가운데 -1, 오른발 +1
    p.hipY = -dropMax;
    p.hipX = w * M.walkSway * L * (1 - 0.6 * T);
    p.hipRot = M.walkTilt * w;
    p.chestRot = -p.hipRot * M.walkCounter - (carry ? M.carryLean : M.walkLean) * T;
    if (sideWalk) {                                    // 옆에서 보면 옆 흔들림·골반 기울기는 안 보이고, 앞으로 숙임이 보인다
      p.hipX = 0; p.hipRot = 0; p.chestRot = -(carry ? M.carryLean : M.walkLean) * 1.2;
    }
    p.headRot = 0;
    p.headTurn = T;
    // 팔: 다리와 반대로 흔들기. 앞으로 간 팔은 팔꿈치를 굽히고 짧아 보인다 (원근)
    for (let i = 0; i < 2; i++) {
      const af = -fwd[i];                               // 왼팔은 왼발과 반대
      const n = i === 0 ? "L" : "R";
      const toward = i === 0 ? -1 : 1;                  // 앞(+x)으로 = 왼팔은 안쪽(-), 오른팔은 바깥(+)
      p["swing" + n] = carry ? 30 + af * 4 : af * M.armSwingSide;          // 옆모습: 앞뒤로 흔들기
      if (carry) {
        p["raise" + n] = M.carryRaise + af * 3; p["bend" + n] = M.carryBend;
      } else {
        p["raise" + n] = M.armRest + toward * af * M.armSwing * T + Math.abs(af) * 3;
        p["bend" + n] = M.elbowRest + Math.max(0, af) * M.armBendFwd;
        p["fore" + n] = 1 - Math.max(0, af) * M.armFore * (1 - T);
      }
    }
    p.face = carry ? "effort" : "normal";
    if (carry) p.headRot = -4;
  }

  // 일하기: 한 손씩 위로 뻗어(발끝으로 서며) 따서 아래 바구니로. 다른 손은 바구니를 든다
  PoseWork(t, p) {
    const M = this.M, L = this.L;
    const c = t / M.workPeriod, k = Math.floor(c), u = frac(c);
    const side = k % 2 === 0 ? "L" : "R", other = side === "L" ? "R" : "L", sg = side === "L" ? -1 : 1;
    const reach = smooth(seg(u, 0.05, 0.35)) * (1 - smooth(seg(u, 0.55, 0.85)));       // 뻗기 → 잡고 → 내리기
    const pull = bump(seg(u, 0.5, 0.9));
    p["raise" + side] = Mathf.Lerp(M.armRest, M.workReach, reach);
    p["bend" + side] = Mathf.Lerp(M.elbowRest, 18, reach) + pull * 80;
    p["wrist" + side] = -20 * reach;
    p["fore" + side] = 1 + 0.25 * reach;
    p["raise" + other] = 16; p["bend" + other] = 75;
    p.hipY = reach * M.workRise * L - pull * 0.05 * L;                                 // 발끝으로 서고, 내릴 때 살짝 앉음
    p.footLRot = p.footRRot = 0;
    p.hipRot = sg * -3 * reach; p.chestRot = sg * 6 * reach;                            // 뻗는 쪽으로 몸이 늘어남
    p.headRot = sg * 9 * reach;
    p.face = reach > 0.3 ? "effort" : "normal";
  }

  // 손발 엇갈려 오르기 공용 (사다리 = 뒷모습, 줄기·덩굴 = 옆모습). dir 1 오르기 · -1 내려오기 (같은 동작을 거꾸로).
  // 잡은 손·디딘 발은 세상(가로대)에 붙어 있어 몸이 오르는 만큼 리그 안에서 아래로 내려가고, 놓은 손발은 두 칸 위 가로대로 옮겨 간다.
  // 오른손+왼발이 같이, 다음에 왼손+오른발. 손이 당기는 동안 몸이 오르고 발이 다음 가로대를 딛는다.
  PoseClimbing(t, p, dt, dir, ladder) {
    const M = this.M, L = this.L;
    const G = ladder ? M.rungGap : M.trunkGap;
    this.climbS += dir * (ladder ? M.ladderSpeed : M.trunkSpeed) * dt;
    const S = this.climbS, beta = 0.62;
    const vk = ladder ? "back" : "side";
    const vd = this.ViewData(vk), lift = (vd.lift != null ? vd.lift : this.lift) || 0;
    const side = vk === "side";
    p.yaw = ladder ? 180 : 90; p.armScreen = true; p.grounded = true;
    const shY = V(vd.shoulderL).y + lift + 0.04 * L;
    const yTop = shY + (ladder ? M.ladderReachUp : 0.36);                  // 손이 잡는 가장 높은 가로대 (어깨 위)
    this.rungBase = yTop; this.rungGap = G; this.climbLadder = ladder;
    const ph = S / (2 * G);
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      // 손: 왼손 0 · 오른손 0.5 박자
      const q = frac(ph + (i === 0 ? 0 : 0.5));
      let y, out = 0;
      if (q < beta) y = yTop - 2 * G * q;                                 // 잡고 끌어내림 (= 몸이 오름)
      else { const u = (q - beta) / (1 - beta); y = yTop - 2 * G * beta + 2 * G * beta * smooth(u); out = bump(u); }   // 놓고 두 칸 위로 (살짝 바깥으로 돌려)
      const Sh = V(i === 0 ? vd.shoulderL : vd.shoulderR).add(new Vector2(0, lift));
      let tx, ty = y - Sh.y;
      if (side) tx = M.trunkReach - out * 0.08;                          // 옆모습: 앞(+x)의 줄기를 잡는다
      else tx = (i === 0 ? -1 : 1) * (M.ladderHalfW - Math.abs(Sh.x) - 0.06 + out * 0.07);   // 뒷모습: 난간 쪽 (긴 머리 밖으로 손이 보이게)
      const local = new Vector2(side || i === 0 ? tx : -tx, ty);           // 오른팔(뒤집힌 공간)은 x를 뒤집어 준다
      const fore = ladder ? M.ladderArmStretch : 1;                         // 짧은 팔: 사다리에선 늘여서 머리 옆 가로대까지 (늘이기)
      const ik = this.ArmIK(i, local.mul(1 / fore), side ? 1 : -1);       // 팔꿈치는 아래로 (옆: 아래·뒤, 뒤: 아래·바깥)
      p["raise" + n] = ik.raise; p["bend" + n] = ik.bend; p["wrist" + n] = side ? 25 : -25 + out * 20;
      p["fore" + n] = fore;
      // 발: 손과 엇갈려 (오른손 0.5 ↔ 왼발 0.5)
      const qf = frac(ph + (i === 0 ? 0.5 : 0));
      const rest = this.ankleRest[i];
      const sole = vd.sole ?? 0, ankleH = rest.y - sole;
      const hipY = V(vd.hipL).y + lift;
      const want = hipY - this.thighLen * 0.95 - this.shinLen * 0.55;    // 무릎을 굽혀 딛는 높이
      const fTop = yTop - G * Math.ceil((yTop - want) / G);               // 발도 같은 가로대 줄에
      let fy, lift2 = 0;
      if (qf < beta) fy = fTop - 2 * G * qf;
      else { const u = (qf - beta) / (1 - beta); fy = fTop - 2 * G * beta + 2 * G * beta * smooth(u); lift2 = bump(u); }
      p["foot" + n + "Y"] = fy + ankleH - rest.y;
      p["foot" + n + "X"] = side ? 0 : (i === 0 ? 1 : -1) * 0.03 * L;
      p["foot" + n + "Z"] = side ? M.trunkReach * 0.75 - lift2 * 0.05 : 0.04 + lift2 * 0.08;   // 무릎이 앞으로 나왔다 디딤
      p["foot" + n + "Rot"] = side ? 10 : 0;
    }
    const w = Mathf.Sin(2 * Mathf.PI * ph);
    p.hipX = side ? 0.04 * L : w * 0.045 * L; p.hipRot = side ? -3 : w * 3; p.chestRot = side ? -7 : -w * 2.5;
    p.hipY = Math.abs(Mathf.Cos(2 * Mathf.PI * ph)) * 0.02 * L;            // 당길 때 살짝 솟음
    p.headRot = side ? 4 : -w * 2;
    p.face = "effort";
  }

  // 폴짝 한 번 (u 0~1): 웅크림(예비 동작) → 차고 늘어남 → 공중(발 모음) → 착지 찌그러짐 → 회복
  PoseHop(u, p, h) {
    const M = this.M, L = this.L, H = this.H;
    const crouch = bump(seg(u, 0, 0.3)), land = bump(seg(u, 0.78, 1));
    const air = seg(u, 0.25, 0.8);
    p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * h * H : 0;
    p.hipY = -(crouch * 0.6 + land * 0.5) * M.jumpCrouch * L;
    p.sqY = 1 - crouch * 0.07 - land * 0.09 + (air > 0 && air < 0.4 ? bump(air / 0.4) * 0.06 : 0);
    p.sqX = 1 / Math.sqrt(Math.max(0.5, p.sqY));
    const tuck = bump(air);
    p.footLY = p.footRY = tuck * 0.18 * L; p.footLX = 0.05 * L * tuck; p.footRX = -0.05 * L * tuck;
    p.raiseL = p.raiseR = Mathf.Lerp(p.raiseL, 70, tuck) - crouch * 4;
    p.bendL = p.bendR = 20 + crouch * 25;
    p.face = "happy";
    p.grounded = p.air <= 0;
  }

  PoseJump(u, p) {
    const M = this.M, L = this.L, H = this.H;
    const crouch = bump(seg(u, 0, 0.26)), land = bump(seg(u, 0.8, 1));
    const air = seg(u, 0.22, 0.82);
    p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * M.jumpH * H : 0;
    p.hipY = -(crouch + land * 0.8) * M.jumpCrouch * L;
    p.sqY = 1 - crouch * 0.1 - land * M.landSquash * 0.7 + (air > 0 && air < 0.35 ? bump(air / 0.35) * 0.1 : 0);
    p.sqX = 1 / Math.sqrt(Math.max(0.5, p.sqY));
    const up = smooth(seg(u, 0.18, 0.32)) * (1 - smooth(seg(u, 0.72, 0.9)));
    p.raiseL = p.raiseR = Mathf.Lerp(Mathf.Lerp(7, -10, crouch), 155, up);             // 웅크리며 팔을 뒤로 → 차면서 위로 휘두름
    p.bendL = p.bendR = 15 + crouch * 30;
    const tuck = bump(air);
    p.footLY = p.footRY = tuck * 0.3 * L; p.footLX = 0.06 * L * tuck; p.footRX = -0.06 * L * tuck;
    p.footLRot = p.footRRot = tuck * 12;
    p.chestRot = 0; p.headRot = -crouch * 4;
    p.face = u < 0.18 ? "normal" : "happy";
    p.grounded = p.air <= 0;
  }

  PoseSurprise(u, p) {
    const M = this.M, L = this.L, H = this.H;
    const pop = seg(u, 0, 0.35);
    p.air = pop < 1 ? 4 * pop * (1 - pop) * M.surpriseH * H : 0;                      // 예비 동작 없이 움찔 뜀
    const fling = smooth(seg(u, 0, 0.12)) * (1 - smooth(seg(u, 0.55, 1)));
    p.raiseL = p.raiseR = Mathf.Lerp(7, 125, fling); p.bendL = p.bendR = Mathf.Lerp(14, 40, fling);
    p.wristL = p.wristR = -25 * fling;
    p.sqY = 1 + fling * 0.06; p.sqX = 1 - fling * 0.03;
    p.headRot = 0; p.chestRot = 0;
    p.hipY = -bump(seg(u, 0.35, 0.6)) * 0.12 * L;
    p.face = "surprise";
    p.grounded = p.air <= 0;
  }

  // 돌기: 제자리에서 한 바퀴 (그림이 앞모습뿐이라 가로로 줄였다 뒤집는다. 뒤를 볼 땐 뒷머리가 얼굴 앞)
  PoseSpin(u, p) {
    const M = this.M, H = this.H;
    p.spin = smooth(u);
    const a = seg(u, 0.05, 0.6);
    p.air = a > 0 && a < 1 ? 4 * a * (1 - a) * 0.1 * H : 0;
    p.raiseL = p.raiseR = 60 * bump(u); p.bendL = p.bendR = 25;
    p.face = "happy";
    p.grounded = p.air <= 0;
  }

  // 착지: 쿵 → 무릎으로 받고(골반이 내려감) → 찌그러졌다 → 넘쳤다가 돌아옴 (스프링이 해 준다). 팔은 관성으로 아래로 갔다 올라옴
  PoseLand(u, p) {
    const M = this.M, L = this.L;
    const hit = Mathf.Exp(-u * 7) * (u > 0 ? 1 : 0);
    p.hipY = -hit * M.landDip * L;
    p.sqY = 1 - hit * M.landSquash; p.sqX = 1 + hit * M.landSquash * 0.7;
    p.raiseL = p.raiseR = 7 + hit * 40; p.bendL = p.bendR = 14 + hit * 30;
    p.headRot = 0; p.footLX = 0.04 * L * hit; p.footRX = -0.04 * L * hit;
    p.face = u < 0.4 ? "surprise" : "happy";
  }

  // 춤: 박자마다 무릎으로 통통, 골반을 좌우로, 팔을 번갈아 위로, 발을 번갈아 든다
  PoseDance(t, p) {
    const M = this.M, L = this.L;
    const b = t / M.danceBeat, k = Math.floor(b), u = frac(b), sg = k % 2 === 0 ? 1 : -1;
    const down = bump(u) ** 2;
    p.hipY = -down * 0.12 * L;
    const sway = Mathf.Sin(Mathf.PI * b);                                             // 두 박자에 한 번 좌우
    p.hipX = sway * M.danceSway * L; p.hipRot = -sway * 6; p.chestRot = sway * 9;
    p.headRot = sway * 8;
    p.raiseL = sg > 0 ? 145 : 40; p.raiseR = sg > 0 ? 40 : 145;
    p.bendL = sg > 0 ? 40 : 70; p.bendR = sg > 0 ? 70 : 40;
    p.wristL = p.wristR = sway * 20;
    const lift = bump(seg(u, 0.1, 0.7)) * 0.16 * L;
    if (sg > 0) p.footRY = lift; else p.footLY = lift;
    p.face = "happy";
  }

  // 하트: 손을 가슴에 모으고 발끝으로 살랑살랑, 고개를 갸웃
  PoseHeart(t, p) {
    const M = this.M, L = this.L;
    const s = Mathf.Sin(t * 3.2);
    for (let i = 0; i < 2; i++) {
      const ik = this.ArmIK(i, new Vector2(0.38, -0.32), 0);
      const n = i === 0 ? "L" : "R";
      p["raise" + n] = ik.raise; p["bend" + n] = ik.bend; p["wrist" + n] = 25;
    }
    p.hipX = s * 0.04 * L; p.hipRot = -s * 2; p.chestRot = s * M.heartSway * 0.5; p.headRot = s * M.heartSway + 6;
    p.hipY = (0.04 + 0.03 * Math.abs(s)) * L;
    p.footLRot = p.footRRot = -6;
    p.face = "happy";
  }

  // 잠: 선 채로 꾸벅꾸벅. 깊은 숨, 무릎이 풀리고 팔이 늘어지고 고개가 떨어졌다 가끔 번쩍
  PoseSleep(t, p) {
    const M = this.M, L = this.L;
    const br = Mathf.Sin(t * 2 * Mathf.PI / 4.2);
    p.breath = br * 2.2;
    const nod = frac(t / 3.6), jerk = nod > 0.9 ? bump(seg(nod, 0.9, 1)) : 0;
    p.headRot = M.sleepDroop * (smooth(nod / 0.9) * 0.6 + 0.4) * (1 - jerk * 0.8);
    p.chestRot = 4 + br; p.hipY = -0.07 * L; p.hipRot = 2;
    p.raiseL = p.raiseR = 2; p.bendL = p.bendR = 6;
    p.face = "blink";
  }

  // 들림: 갓을 잡혀 매달림. 진자로 흔들리고(손 가속도), 다리를 동동, 팔을 버둥버둥, 몸을 비튼다
  PoseHeld(t, p) {
    const M = this.M, L = this.L;
    p.hang = true; p.grounded = false;
    const w = t * 15;
    p.raiseL = 70 + Mathf.Sin(w) * M.heldFlail; p.raiseR = 70 + Mathf.Sin(w + 2.4) * M.heldFlail;
    p.bendL = 35 + Mathf.Sin(w + 1.3) * 40; p.bendR = 35 + Mathf.Sin(w + 3.7) * 40;
    p.hipRot = Mathf.Sin(t * 9) * 7; p.chestRot = -Mathf.Sin(t * 9) * 4; p.headRot = Mathf.Sin(t * 11 + 1) * 5;
    const k1 = Mathf.Sin(t * 13), k2 = Mathf.Sin(t * 13 + 2.2);
    p.footLY = Math.max(0, k1) * 0.25 * L - 0.12 * L; p.footRY = Math.max(0, k2) * 0.25 * L - 0.12 * L;   // 매달려 다리가 늘어지고 동동
    p.footLX = -k1 * 0.08 * L; p.footRX = k2 * 0.08 * L;
    p.footLRot = k1 * 15; p.footRRot = -k2 * 15;
    p.sqY = 1.03; p.sqX = 0.98;
    p.face = Math.floor(t * 2.2) % 3 === 2 ? "effort" : "surprise";
  }

  PoseFall(t, p) {
    const M = this.M, L = this.L;
    p.grounded = false;
    p.raiseL = 160 + Mathf.Sin(t * 26) * 8; p.raiseR = 160 + Mathf.Sin(t * 26 + 1.5) * 8;
    p.bendL = p.bendR = 12;
    p.footLY = -0.08 * L + Math.max(0, Mathf.Sin(t * 18)) * 0.08 * L; p.footRY = -0.08 * L + Math.max(0, -Mathf.Sin(t * 18)) * 0.08 * L;
    p.footLX = -0.04 * L; p.footRX = 0.04 * L;
    p.sqY = 1.05; p.sqX = 0.97;
    p.face = "surprise";
  }

  // 손 흔들기: 예비 동작(살짝 내림) → 들어 올림(넘쳤다 섬) → 팔꿈치로 흔들고 손목이 따라옴 → 내림
  PoseWave(u, t, p) {
    const side = this.waveSide || "R", sg = side === "L" ? -1 : 1;
    const pre = bump(seg(u, 0, 0.12)), up = smooth(seg(u, 0.08, 0.3)) * (1 - smooth(seg(u, 0.82, 1)));
    p["raise" + side] = Mathf.Lerp(7 - pre * 5, 150, up);
    p["fore" + side] = 1 + 0.15 * up;
    p["bend" + side] = Mathf.Lerp(14, 32 + Mathf.Sin(t * 15) * 28, up);
    p.chestRot -= sg * 3 * up; p.hipRot += sg * 1.5 * up; p.headRot += sg * 6 * up;
    p.face = "happy";
  }

  PoseStretch(u, p) {
    const L = this.L;
    const e = smooth(seg(u, 0.05, 0.4)) * (1 - smooth(seg(u, 0.7, 1)));
    p.raiseL = p.raiseR = Mathf.Lerp(p.raiseL, 168, e); p.bendL = p.bendR = Mathf.Lerp(p.bendL, 8, e);
    p.foreL = p.foreR = 1 + 0.35 * e;                                                  // 쭉 뻗는 팔은 늘어난다 (늘이기·찌그러뜨리기)
    p.wristL = p.wristR = -15 * e;
    p.hipY = e * 0.1 * L; p.sqY = 1 + e * 0.04; p.sqX = 1 - e * 0.02; p.breath = 1.5 * e;
    p.footLRot = p.footRRot = 0;
    p.hipRot = 0; p.chestRot = 0; p.headRot = Mathf.Sin(u * Mathf.PI * 2) * 3;
    p.face = e > 0.4 ? "blink" : "normal";
    if (u > 0.85) p.hipY -= bump(seg(u, 0.85, 1)) * 0.04 * L;                        // 풀면서 살짝 처짐
  }

  PoseLook(u, p) {
    const a = smooth(seg(u, 0.05, 0.25)) - smooth(seg(u, 0.4, 0.6)) * 2 + smooth(seg(u, 0.75, 0.95));
    p.headTurn = a; p.headRot += -a * 6; p.chestRot += -a * 2;
    p.face = "normal";
  }

  PoseYawn(u, p) {
    const L = this.L;
    const e = smooth(seg(u, 0.1, 0.35)) * (1 - smooth(seg(u, 0.7, 0.95)));
    const ik = this.ArmIK(0, new Vector2(0.55, 0.95), 0);                               // 왼손을 입으로
    p.raiseL = Mathf.Lerp(p.raiseL, ik.raise, e); p.bendL = Mathf.Lerp(p.bendL, ik.bend, e);
    p.raiseR = Mathf.Lerp(p.raiseR, 20, e);
    p.headRot += -7 * e; p.breath = 1.8 * e; p.hipY += 0.03 * L * e;
    p.face = e > 0.3 ? "blink" : "normal";
  }

  // 팔 IK: 어깨 공간(왼팔 기준, 오른팔은 뒤집혀 같은 값)의 목표 점 → 어깨 각(raise)·팔꿈치 각(bend)
  // prefer 1 = 팔꿈치가 바깥(-x)으로 굽는 해 (정면 기본), -1 = 반대 (옆모습에서 팔꿈치가 아래·뒤로)
  ArmIK(i, target, prefer = 1) {
    const a = this.upperLen, b = this.foreLen, sg = prefer < 0 ? -1 : 1;
    const d = Mathf.Clamp(target.magnitude, Math.abs(a - b) + 1e-3, a + b - 1e-3);
    const cosB = Mathf.Clamp((a * a + b * b - d * d) / (2 * a * b), -1, 1);
    const bend = sg * (180 - Math.acos(cosB) * Mathf.Rad2Deg);                          // 0 = 쭉 폄
    const cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1);
    const toT = Math.atan2(target.x, -target.y) * Mathf.Rad2Deg;                        // 아래(0)에서 +x 쪽으로 (반시계 +)
    const ang = toT - sg * Math.acos(cosA) * Mathf.Rad2Deg;
    return { raise: -ang, bend };                                                        // raise + = 바깥(-x)으로 = 시계
  }

  // ───── ③④⑤ ★2·★3 풀기 (C# SolveHeaded) ─────
  SolveHeaded(p, dt) {
    const M = this.M, L = this.L;
    // 보는 방향: yaw 0 정면 · 90 옆 · 180 뒤. 경계(45·135)를 지날 때 그림을 바꾸고, 그 순간 가로로 살짝 좁혀(돌아서는 느낌) 팍 바뀌어 보이지 않게
    const yawDeg = p.yaw;
    this.SetView(yawDeg < 45 ? "front" : yawDeg < 135 ? "side" : "back");
    const vk = this.viewKey, l = this.lv;
    const baseYaw = vk === "front" ? 0 : vk === "side" ? 90 : 180;
    const resYaw = Mathf.Clamp(yawDeg - baseYaw, -M.maxResYaw, M.maxResYaw);       // 그 그림에서 더 돌아간 정도 (다리 투영·얼굴 옮김)
    const pinch = Math.max(0, 1 - Math.min(Math.abs(yawDeg - 45), Math.abs(yawDeg - 135)) / M.turnPinchW);
    this.turnPinch = pinch;
    // ③ 몸 스프링 (자세가 갑자기 바뀌어도 무게 있게)
    this.sHip[0].Step(p.hipX, M.bodyK, M.bodyZ, dt); this.sHip[1].Step(p.hipY, M.bodyK, M.bodyZ, dt); this.sHip[2].Step(p.hipRot, M.bodyK, M.bodyZ, dt);
    this.sChest.Step(p.chestRot, M.bodyK * 0.7, M.bodyZ, dt);
    this.sTurn.Step(p.headTurn, M.headK, 0.9, dt);
    this.sAir.Step(p.air, 900, 1, dt);
    this.sSqX.Step(p.sqX, M.squashK, M.squashZ, dt); this.sSqY.Step(p.sqY, M.squashK, M.squashZ, dt);
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      this.sRaise[i].Step(this.ArmTarget(p, i, vk), M.armK, M.armZ, dt);
      this.sBend[i].Step(p["bend" + n], M.elbowK, M.elbowZ, dt);                         // 팔꿈치는 조금 늦게 (겹침)
    }
    const spinC = Mathf.Cos(p.spin * 2 * Mathf.PI), back = spinC < 0;
    const air = p.air > 0 ? p.air : Math.max(0, this.sAir.x);

    // root: 공중 높이 · 찌그러짐(발바닥 축) · 돌기 · 들림
    let rootRot = 0, rootPos = new Vector2(0, air);
    if (p.hang) {
      // 갓 꼭대기에 매달린 진자: 손 가속도가 밀어 흔든다 (g/길이 = hangK)
      const capTop = V(l.capTop).add(new Vector2(0, this.lift));
      this.hang.Step(0, M.hangK, M.hangZ, -this.grabAccel.x * 4, dt);
      rootRot = Mathf.Clamp(this.hang.a, -50, 50);
      const r = rootRot * Mathf.Deg2Rad;
      const ct = new Vector2(capTop.x * Math.cos(r) - capTop.y * Math.sin(r), capTop.x * Math.sin(r) + capTop.y * Math.cos(r));
      rootPos = capTop.sub(ct);                          // 갓 꼭대기가 서 있을 때 자리에 머문다
    }
    this.root.set(rootPos.x, rootPos.y, rootRot, this.scale * this.flip * this.sSqX.x * (1 - M.turnPinch * pinch) * (Math.abs(spinC) < 0.06 ? 0.06 * Mathf.Sign(spinC) : spinC), this.scale * this.sSqY.x);

    // 골반 · 상체 · 숨
    const W = V(l.waist);
    this.hips.set(W.x + this.sHip[0].x, W.y + this.lift + this.sHip[1].x, this.sHip[2].x);
    this.chest.set(0, 0, this.sChest.x, 1, 1 + p.breath * M.breathAmt);
    // 머리: 몸이 기울어도 수평을 지키려 하고(headStab), 스프링으로 늦게 따라와 흔들림
    const chestWorld = this.sHip[2].x + this.sChest.x + rootRot;
    this.sHead.Step(p.headRot - chestWorld * M.headStab, M.headK, M.headZ, dt);
    this.neck.localRotation = this.sHead.x;
    // 갓 통통 (누르면)
    const jig = this.pokeT < 1.2 ? 0.16 * Mathf.Exp(-5 * this.pokeT) * Mathf.Sin(this.pokeT * 26) : 0;
    // 고개 돌림: 얼굴 층을 옆으로, 뒷머리는 반대로 조금 (그림 한 장으로 3/4 느낌)
    const turn = Mathf.Clamp(this.sTurn.x + (vk === "front" ? Mathf.Clamp(resYaw / 45, -1, 1) * 0.9 : 0), -1, 1);
    // 머리 전체가 돈다: Bounce(갓 통통) 노드 = 머리·얼굴의 공통 부모. 얼굴 층은 그 안에서 아주 조금만
    this.bounce.localScale.x = (1 + jig) * (1 - M.headTurnSquash * Math.abs(turn)); this.bounce.localScale.y = 1 - jig;
    this.bounce.localPosition.x = turn * M.headTurnShift;
    this.bounce.localRotation = -turn * M.headTurnTilt;
    this.faceSR.transform.localPosition.x = turn * M.faceShift;
    this.faceSR.enabled = !back && vk !== "back";
    // 뒷머리: 정면 = 맨 뒤 · 옆 = 몸통·먼 팔 앞, 머리·가까운 팔 뒤 · 뒤 = 맨 위 (등을 덮음)
    // 뒷모습: 머리카락은 팔·몸·머리 그림(갓 밑 짧은 머리 포함) 위 45, 그 위에 갓만 오린 그림 47 → 머리카락이 갓 테두리 밑에서 나와 맨 앞에 보인다
    this.hairSR.sortingOrder = vk === "back" ? 45 : vk === "side" ? 15 : back ? 130 : -80;
    // 뼈 사슬 머리카락(한 장, 앞머리 옆 머리부터 등까지): 얼굴·몸통 위 45, 갓 덮개(<머리>_cap) 47 아래 → 갓 밑에서 나와 얼굴 뒤를 감싸고 등으로 흐른다
    // 정면(hairBones 있을 때) = 몸 뒤(-80, 뒤돌면 130) · 옆 = 위 45 / 아래 5 · 뒤 = 45
    this.hairSkinSR.sortingOrder = vk === "front" ? (back ? 130 : -80) : 45;

    // 팔
    // 먼 쪽 팔다리는 몸 뒤로: 정면에서 가는 쪽으로 돌면 반대쪽, 옆모습은 오른쪽(i=1)이 먼 쪽, 뒷모습은 두 팔 다 몸 앞쪽(=화면에선 뒤)
    const farSide = vk === "side" ? 1 : turn > 0.25 ? 0 : turn < -0.25 ? 1 : -1;
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      this.upper[i].localRotation = -this.sRaise[i].x;
      this.upper[i].localScale.y = p["fore" + n];
      this.elbow[i].localRotation = this.sBend[i].x;
      const far = i === farSide;
      // 뒷모습 (뒤→앞): 사다리 · 다리 · 몸통/치마 · 팔 · 머리(뒤통수·갓) · 뒷머리 (머리카락에 가려지는 팔은 가려진다)
      if (vk === "back") { this.upperSR[i].sortingOrder = 20; this.foreSR[i].sortingOrder = 21; this.handSR[i].sortingOrder = 22; }
      else { this.upperSR[i].sortingOrder = far ? -70 : 80; this.foreSR[i].sortingOrder = far ? -60 : 90; this.handSR[i].sortingOrder = far ? -50 : 100; }
      // 소매: 위팔 바로 위 (아래팔·손보다는 아래), 위팔 회전의 sleeveFollow만큼 따라 돈다 (어깨에 붙은 천이 팔을 따라 들림)
      this.sleeveSR[i].sortingOrder = this.upperSR[i].sortingOrder + 5;
      this.sleeveT[i].localRotation = this.upper[i].localRotation * M.sleeveFollow;
    }

    // ⑤ 다리 IK (무릎 2뼈, 3D → 화면): 발목 목표 = 쉬는 발목 + (옆 X, 높이 Y, 앞 Z). 무릎은 앞(+Z)으로 굽는다.
    //    3D 점을 보는 방향(yaw: 0 정면 · 90 옆)으로 화면에 투영 → 정면에선 무릎 굽힘이 다리가 짧아지는 것으로, 옆에선 꺾임으로 보인다.
    this.legStretch = [1, 1];
    const yaw = resYaw * Mathf.Deg2Rad, cy = Math.cos(yaw), sy = vk === "side" ? 1 : Math.sin(yaw), dk = M.depthK;
    // 3D(옆 X, 위 Y, 앞 Z) → 화면. 정면: 앞이 카메라 쪽(조금 아래로) · 옆: 앞이 화면 +x · 뒤: 앞이 화면 안쪽(조금 위로)
    const proj = vk === "side" ? (x, y, z) => new Vector2(x + z, y)
      : vk === "back" ? (x, y, z) => new Vector2(x - z * Math.sin(yaw), y + z * dk)
      : (x, y, z) => new Vector2(x * cy + z * sy, y - (z * cy - x * sy) * dk);
    const depthOf = [0, 0];
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R", m = i === 0 ? 1 : -1;
      const rest = this.ankleRest[i];
      const Hs = this.RootLocal(this.hip[i]);                       // 엉덩이 (화면 = root 안쪽 공간)
      const hipRest = V(i === 0 ? l.hipL : l.hipR).add(new Vector2(0, this.lift));
      // 발목 목표 (엉덩이 기준 3D). 땅에 있으면 발목은 땅 좌표에 고정, 공중·들림이면 엉덩이를 따라간다
      const fx = p["foot" + n + "X"], fy = p["foot" + n + "Y"], fz = p["foot" + n + "Z"];
      let vx, vy, vz = fz;
      if (p.grounded) { vx = rest.x + fx - Hs.x; vy = rest.y + fy - Hs.y; }
      else { vx = rest.x - hipRest.x + fx; vy = rest.y - hipRest.y + fy; }
      const a = this.kneeOn ? this.thighLen : this.legLen * 0.5, b = this.kneeOn ? this.shinLen : this.legLen * 0.5;
      let d = Math.sqrt(vx * vx + vy * vy + vz * vz);
      const dMax = (a + b) * 0.999, dMin = Math.abs(a - b) + 1e-3;
      this.legStretch[i] = d / (a + b);
      if (d > dMax || d < dMin) { const k = Mathf.Clamp(d, dMin, dMax) / Math.max(d, 1e-6); vx *= k; vy *= k; vz *= k; d = Mathf.Clamp(d, dMin, dMax); }
      // 무릎 방향: 다리 축에 수직이면서 앞(+Z) 쪽 (조금 바깥으로: kneeOut)
      const ux = vx / d, uy = vy / d, uz = vz / d;
      let wx = vk === "side" ? 0 : -m * M.kneeOut, wy = 0, wz = 1;
      const dot = wx * ux + wy * uy + wz * uz;
      wx -= dot * ux; wy -= dot * uy; wz -= dot * uz;
      const wl = Math.hypot(wx, wy, wz) || 1; wx /= wl; wy /= wl; wz /= wl;
      const cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1), sinA = Math.sqrt(1 - cosA * cosA);
      const kx = a * (cosA * ux + sinA * wx), ky = a * (cosA * uy + sinA * wy), kz = a * (cosA * uz + sinA * wz);
      const Ks = Hs.add(proj(kx, ky, kz)), As = Hs.add(proj(vx, vy, vz));
      depthOf[i] = vk === "side" ? -i : vk === "back" ? -vz : vz * cy - vx * sy;
      if (this.kneeOn) {
        this.SetBone(this.thighT[i], Hs, Ks, this.thighBind, m);
        this.SetBone(this.shinT[i], Ks, As, this.shinBind, m);
      } else {
        // 무릎 그림이 없으면 예전처럼 한 장 (엉덩이 → 발목)
        const bind = new Vector2(this.legBind.x * m, this.legBind.y), dd = As.sub(Hs);
        const worldAng = (Math.atan2(dd.y, dd.x) - Math.atan2(bind.y, bind.x)) * Mathf.Rad2Deg;
        this.thigh[i].localRotation = m * (worldAng - this.sHip[2].x);
        this.thigh[i].localScale.y = dd.magnitude / this.legLen;
      }
      this.foot[i].set(As.x, As.y, 0, m, 1);
      // 발: 땅에선 평평 + 뒤꿈치 닿기(발끝 듦)·발끝 떼기, 공중에선 정강이를 따라 늘어짐. 발 굴림은 옆에서 잘 보인다
      const shinAng = Math.atan2(As.y - Ks.y, As.x - Ks.x) * Mathf.Rad2Deg + 90;
      this.footRot[i].localRotation = m * p["foot" + n + "Rot"] * (vk === "side" ? 1 : 0.35 + 0.65 * Math.abs(Math.sin(yaw))) + (p.grounded ? 0 : shinAng * 0.5);
    }
    // 앞(카메라 쪽)에 있는 다리가 앞에 그려진다
    for (let i = 0; i < 2; i++) {
      const front = depthOf[i] > depthOf[1 - i] + 1e-3, far = i === farSide;
      const base = far ? -48 : front ? -28 : -34;
      this.thighSR[i].sortingOrder = base; this.shinSR[i].sortingOrder = base + 1; this.legSR[i].sortingOrder = base;
      this.shoeSR[i].sortingOrder = base + 4;
    }

    // ④ 2차 움직임: 관절점의 실제 가속도로 흔들리는 진자 (root 공간 + 바깥 이동)
    const pts = { hair: this.RootSpace(this.hairT), waist: this.RootSpace(this.hips), w0: this.RootSpace(this.wrist[0]), w1: this.RootSpace(this.wrist[1]) };
    const acc = this.Accel(pts, dt);
    const g = this.hang.a;                              // 들린 몸이 기운 만큼 머리카락·치마는 아래로
    const headWorld = chestWorld + this.sHead.x;
    this.hairP.Step(-headWorld * M.hairGravity - this.vel.x * M.hairDrag * 2, M.hairK, M.hairZ, -acc.hair.x * M.hairInertia * 10, dt);
    // 뼈 사슬이면 뿌리는 조금만 돌고(정수리·목덜미가 머리에서 떨어지지 않게) 흔들림은 아래 뼈가 나눠 맡는다
    const rootMax = this.hairSkin ? M.hairSegRootMax : 35;
    this.hairT.localRotation = Mathf.Clamp(this.hairP.a, -rootMax, rootMax);
    this.hairT.localPosition.x = V(l.hairRoot).x - V(l.neck).x - turn * M.hairParallax;
    this.hairT.localScale.y = this.hairSkin ? 1 : 1 + Mathf.Clamp(-acc.hair.y * M.hairStretch, -0.08, 0.08);
    // 뼈 1… : 윗뼈의 세상 각도만큼 아래로 늘어지려 하고(중력), 그 관절의 가속도에 밀린다. 아래 뼈일수록 무르다(k × hairSegK^n)
    if (this.hairSkin) {
      const jp = {};
      for (let i = 1; i < this.nBones; i++) jp["hb" + i] = this.RootSpace(this.hairBone[i]);
      const bAcc = this.Accel(jp, dt);
      let parentWorld = headWorld + this.hairT.localRotation;
      for (let i = 1; i < this.nBones; i++) {
        const k = M.hairK * Math.pow(M.hairSegK, i), a = bAcc["hb" + i];
        this.hairSegP[i - 1].Step(-parentWorld * M.hairGravity - this.vel.x * M.hairDrag, k, M.hairSegZ, -a.x * M.hairSegInertia * 10, dt);
        const r = Mathf.Clamp(this.hairSegP[i - 1].a, -M.hairSegMax, M.hairSegMax);
        this.hairBone[i].localRotation = r;
        parentWorld += r;
      }
    }
    const hipsWorld = this.sHip[2].x + rootRot;
    this.skirtP.Step(-hipsWorld * M.skirtGravity, M.skirtK, M.skirtZ, -acc.waist.x * M.skirtInertia * 10, dt);
    this.skirtT.localRotation = Mathf.Clamp(this.skirtP.a, -20, 20);
    const spread = (this.foot[1].localPosition.x - this.foot[0].localPosition.x) - (this.ankleRest[1].x - this.ankleRest[0].x);
    this.skirtT.localScale.x = 1 + Mathf.Clamp(spread / L, -0.3, 0.5) * M.skirtFlare;
    this.skirtT.localScale.y = 1 + Mathf.Clamp(-acc.waist.y * 0.004, -0.05, 0.05);
    for (let i = 0; i < 2; i++) {
      // 손: 아래팔이 도는 만큼 반대로 늦게 (손목 진자)
      const foreRot = this.elbow[i].rotation;
      const v = (Mathf.DeltaAngle(this.prevForeRot[i], foreRot)) / Math.max(dt, 1e-4);
      const aa = (v - this.prevForeVel[i]) / Math.max(dt, 1e-4);
      this.prevForeRot[i] = foreRot; this.prevForeVel[i] = v;
      const n = i === 0 ? "L" : "R";
      this.handP[i].Step(p["wrist" + n], M.handK, M.handZ, -Mathf.Clamp(aa, -4e4, 4e4) * M.handInertia * (i === 0 ? 1 : -1) * this.flip, dt);
      this.wrist[i].localRotation = Mathf.Clamp(this.handP[i].a, -60, 60);
    }
  }

  // ★0·★1: 몸 통통(발바닥 축) + 갓이 대 위에서 늦게 따라 흔들림·찌그러짐, 발 따로
  SolveHeadless(p, dt) {
    const M = this.M, l = this.look, L = this.L;
    // 팔이 없으니 팔 동작(손 흔들기·버둥)은 몸 기울기로: 오른팔을 들면 몸이 그쪽으로 (부드럽게)
    const armLean = Mathf.Clamp((p.raiseR - p.raiseL) * 0.06, -8, 8) + (p.raiseL + p.raiseR > 200 ? Mathf.Sin(this.time * 14) * 3 : 0);
    this.sHip[0].Step(p.hipX, M.bodyK, M.bodyZ, dt); this.sHip[1].Step(p.hipY, M.bodyK, M.bodyZ, dt); this.sHip[2].Step(p.hipRot + p.chestRot * 0.5 - armLean, M.bodyK, M.bodyZ, dt);
    this.sAir.Step(p.air, 900, 1, dt);
    this.sSqX.Step(p.sqX, M.squashK, M.squashZ, dt); this.sSqY.Step(p.sqY, M.squashK, M.squashZ, dt);
    const spinC = Mathf.Cos(p.spin * 2 * Mathf.PI);
    const air = p.air > 0 ? p.air : Math.max(0, this.sAir.x);
    let rootRot = 0, rootPos = new Vector2(0, air);
    if (p.hang) {
      const capTop = V(l.capTop);
      this.hang.Step(0, M.hangK, M.hangZ, -this.grabAccel.x * 4, dt);
      rootRot = Mathf.Clamp(this.hang.a, -50, 50);
      const r = rootRot * Mathf.Deg2Rad;
      rootPos = capTop.sub(new Vector2(capTop.x * Math.cos(r) - capTop.y * Math.sin(r), capTop.x * Math.sin(r) + capTop.y * Math.cos(r)));
    }
    this.root.set(rootPos.x, rootPos.y, rootRot, this.scale * this.flip * (Math.abs(spinC) < 0.06 ? 0.06 * Mathf.Sign(spinC) : spinC), this.scale);
    // 몸: 앉았다 서면(hipY) 세로로 눌림, 기울기, 찌그러짐 → 발바닥 축
    const squat = Mathf.Clamp(this.sHip[1].x / (this.H * 0.5), -0.3, 0.3);
    const sy = this.sSqY.x * (1 + squat), sx = this.sSqX.x * (1 - squat * 0.6);
    const jig = this.pokeT < 1.2 ? 0.14 * Mathf.Exp(-5 * this.pokeT) * Mathf.Sin(this.pokeT * 26) : 0;
    this.body.set(this.sHip[0].x * 0.6, 0, this.sHip[2].x * 0.7, sx * (1 + jig * 0.4), sy * (1 - jig * 0.4));
    this.stemFace.enabled = spinC >= 0;
    // 갓: 갓 테두리 축 진자. 몸이 움직인 가속도로 늦게 따라 흔들리고 세로로 출렁 (무게)
    const capPos = this.RootSpace(this.capT);
    const acc = this.Accel({ cap: capPos }, dt);
    this.capP.Step(p.capRot - this.sHip[2].x * 0.4, M.capK, M.capZ, -acc.cap.x * M.capInertia * 10, dt);
    this.capT.localRotation = Mathf.Clamp(this.capP.a, -25, 25);
    this.capSq.Step(Mathf.Clamp(acc.cap.y * 0.002, -1, 1) * M.capSquash * 10, M.capK * 1.4, M.capZ, dt);
    const cs = Mathf.Clamp(this.capSq.x, -0.15, 0.15) + jig;
    this.capT.localScale.x = 1 + cs; this.capT.localScale.y = 1 - cs;
    // 발: 발목 목표 그대로 (땅에 붙음)
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      const rest = this.ankleRest[i];
      let tx = rest.x * sx + p["foot" + n + "X"] * 0.6, ty = rest.y + p["foot" + n + "Y"] * 0.6;
      if (!p.grounded) ty += 0;
      this.foot[i].set(tx, ty, 0, 1, 1);
      this.footRot[i].localRotation = p["foot" + n + "Rot"];
    }
  }

  // 팔 목표 각 (화면, 시계 + = 왼팔이 바깥으로). 정면·뒤는 raise 그대로, 옆모습은 앞뒤 흔들기(swing)가 화면 회전이 된다
  // (앞 = +x = 반시계). 들어 올린 팔(raise)도 옆에서는 앞-위로. armScreen이면 동작이 이미 화면 각으로 준 값 (IK)
  ArmTarget(p, i, vk) {
    const n = i === 0 ? "L" : "R", raise = p["raise" + n];
    if (vk !== "side" || p.armScreen) return raise;
    return -(p["swing" + n] + Math.max(0, raise - this.M.armRest));
  }

  // 뼈 하나를 화면 두 점 사이에 놓기 (root 공간): 바인드 방향(왼쪽 기준, 오른쪽은 뒤집음)에서 돌리고, 길이 비율만큼 늘인다
  SetBone(t, from, to, bind, m) {
    const dx = to.x - from.x, dy = to.y - from.y, len2 = dx * dx + dy * dy;
    // 세로로만 늘이므로 늘인 바인드 벡터 (m·bx, s·by)의 길이가 목표 길이가 되게 s를 고른다
    const s = Math.sqrt(Math.max(0.04, (len2 - bind.x * bind.x) / Math.max(1e-6, bind.y * bind.y)));
    const ang = (Math.atan2(dy, dx) - Math.atan2(s * bind.y, m * bind.x)) * Mathf.Rad2Deg;
    t.set(from.x, from.y, ang, m, s);
  }

  // root 공간 위치 (root 변환 적용 후, 즉 화면 쪽) → 진자 가속도 계산용
  RootSpace(t) {
    // root의 부모 공간 = 리그 바깥(발바닥 원점). root까지 포함
    return t.localToWorld.point(0, 0);
  }
  HipsSpacePoint(v) { return this.RootLocalMatrix(this.hips).point(v.x, v.y); }
  // root 안쪽 공간 (발이 사는 공간)
  RootLocalMatrix(t) { let m = t.localMatrix; for (let x = t.parent; x && x !== this.root; x = x.parent) m = x.localMatrix.mul(m); return m; }
  RootLocal(t) { return this.RootLocalMatrix(t).point(0, 0); }

  // 머리카락 스킨 (C#: SpriteSkin 뼈 가중치와 같은 규칙). rest = 뼈 쉬는 자리(목 공간), hairY = 그림 중심 y(목 공간).
  //  가중치는 줄(높이)마다: 정수리 띠(뼈 0 위 headBand)까지는 머리에 붙음(움직이지 않음),
  //  그 아래는 "뼈 k 구간의 가운데"끼리 부드럽게 섞는다 (뼈 k 구간 = 뼈 k ~ 뼈 k+1, 마지막 뼈는 아래로 tail).
  MakeHairSkin(rest, hairY) {
    const rig = this, n = rest.length;
    const sk = { space: this.neck, M: [], c: [] };
    sk.prepare = () => {
      const M = rig.M, inv = rig.neck.localToWorld.inverse();
      sk.M = [new M2()]; sk.c = [rest[0].y + M.hairSkinHeadBand];
      for (let k = 0; k < n; k++) {
        sk.M.push(inv.mul(rig.hairBone[k].localToWorld).mul(new M2(1, 0, 0, 1, -rest[k].x, -rest[k].y)));
        sk.c.push(k < n - 1 ? (rest[k].y + rest[k + 1].y) / 2 : rest[k].y - M.hairSkinTail);
      }
    };
    // 그림의 픽셀 줄 row → 목 공간 변형 (rest → 지금)
    sk.at = (row, s) => {
      const y = hairY + (1 - s.py) * s.h / s.ppu - row / s.ppu, c = sk.c, Ms = sk.M;
      if (y >= c[0]) return Ms[0];
      for (let i = 0; i < c.length - 1; i++) {
        if (y >= c[i + 1]) {
          let t = (c[i] - y) / (c[i] - c[i + 1]); t = t * t * (3 - 2 * t);
          const A = Ms[i], Bm = Ms[i + 1];
          return new M2(A.a + (Bm.a - A.a) * t, A.b + (Bm.b - A.b) * t, A.c + (Bm.c - A.c) * t, A.d + (Bm.d - A.d) * t, A.e + (Bm.e - A.e) * t, A.f + (Bm.f - A.f) * t);
        }
      }
      return Ms[Ms.length - 1];
    };
    return sk;
  }

  Accel(pts, dt) {
    const out = {};
    const prev = this.prevPts || {};
    const pv = this.prevVel || {};
    const vel = {};
    for (const k in pts) {
      const P = pts[k].add(new Vector2(this.vel.x * this.time, this.vel.y * this.time));
      const q = prev[k] || P;
      const v = q === P ? new Vector2() : P.sub(q).mul(1 / Math.max(dt, 1e-4));
      const v0 = pv[k] || v;
      const a = v.sub(v0).mul(1 / Math.max(dt, 1e-4));
      out[k] = new Vector2(Mathf.Clamp(a.x, -60, 60), Mathf.Clamp(a.y, -60, 60));
      vel[k] = v; prev[k] = P;
    }
    this.prevPts = prev; this.prevVel = { ...pv, ...vel };
    return out;
  }

  // 표정: 동작이 정한 얼굴 + 깜빡임(가끔 두 번) + 누른 직후 놀람→웃음
  UpdateFace(p, dt) {
    let face = p.face || "normal";
    this.blinkT -= dt;
    if (this.blinkT <= 0) { this.blink2 = !this.blink2 && this.rng.value < 0.25; this.blinkT = this.blink2 ? 0.28 : this.rng.Range(2.2, 4.8); }
    const blinking = this.blinkT < 0.13;
    if (this.pokeT < 0.25) face = "surprise"; else if (this.pokeT < 1.1) face = "happy";
    if (blinking && (face === "normal" || face === "effort")) face = "blink";
    if (this.faceOverride) face = this.faceOverride;
    const l = this.lv || this.look;
    const sr = this.headed ? this.faceSR : this.stemFace;
    sr.sprite = l.face ? this.Sprite(l.face + face) || this.Sprite(l.face + "normal") : null;   // 옆·뒤 그림은 얼굴이 머리 그림에 있다 (face "")
    this.lastFace = face;
  }

  // 그리기용 관절점 (root 바깥 공간): 웹 오버레이·드래그
  // 고친 뒤 다시 세우기 (지금 보는 방향 유지)
  Reapply() { const k = this.viewKey || "front"; this.Apply(this.look, this.outfit); if (this.headed) this.SetView(k); }

  Joints() {
    const l = this.lv || this.look; if (!l) return [];
    const J = [];
    const P = (t, x = 0, y = 0) => t.localToWorld.point(x, y);
    if (this.headed) {
      J.push({ key: "waist", at: P(this.hips), parent: null, space: this.root });
      J.push({ key: "neck", at: P(this.neck), parent: "waist" });
      J.push({ key: "hairRoot", at: P(this.hairT), parent: "neck" });
      if (this.hairSkin)
        for (let i = 1; i < this.nBones; i++) J.push({ key: "hairB" + i, at: P(this.hairBone[i]), parent: i === 1 ? "hairRoot" : "hairB" + (i - 1) });
      J.push({ key: "capTop", at: P(this.neck, V(l.capTop).x - V(l.neck).x, V(l.capTop).y - V(l.neck).y), parent: "neck" });
      for (let i = 0; i < 2; i++) {
        const n = i === 0 ? "L" : "R";
        J.push({ key: "shoulder" + n, at: P(this.shoulder[i]), parent: "neck" });
        J.push({ key: "elbow" + n, at: P(this.elbow[i]), parent: "shoulder" + n });
        J.push({ key: "wrist" + n, at: P(this.wrist[i]), parent: "elbow" + n });
        J.push({ key: "hip" + n, at: P(this.hip[i]), parent: "waist" });
        if (this.kneeOn) J.push({ key: "knee" + n, at: P(this.shinT[i]), parent: "hip" + n });
        J.push({ key: "ankle" + n, at: P(this.foot[i]), parent: this.kneeOn ? "knee" + n : "hip" + n });
      }
    } else {
      J.push({ key: "capJoint", at: P(this.capT), parent: null });
      J.push({ key: "capTop", at: P(this.body, V(l.capTop).x, V(l.capTop).y), parent: "capJoint" });
      for (let i = 0; i < 2; i++) if (this.foot[i].activeSelf) J.push({ key: "ankle" + (i === 0 ? "L" : "R"), at: P(this.foot[i]), parent: "capJoint" });
    }
    return J;
  }
}
