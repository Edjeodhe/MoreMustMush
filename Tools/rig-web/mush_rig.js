// 버섯 리그 (★0·★1 전용, 2026-10-18): 사람형 리그2(SpiritRig2)와 따로. C# MushRig.cs로 1:1 옮길 수 있게 같은 모양.
// 몸 = 갓 + 대(얼굴) + 작은 발 둘 (+ ★1 몸 옆 동그란 손 둘). 무릎·사람 걷기·옆/뒷모습은 쓰지 않는다 (정면 + 뒤집기).
// 맛: 통통 튀며 이동(폴짝 걸음) + 뒤뚱(좌우 기울기), 찌그러짐/늘어남이 핵심, 갓은 몸보다 늦게 따라와 출렁(관성), 착지 때 갓이 눌렸다 튐.
//
// 계층: root(발바닥 가운데: 뒤집기·공중·들림 회전) ─ body(발바닥 축: 기울기·찌그러짐) ─ bodySkin(통 몸) · face · nubPivot[i] ─ nub[i] · capT(갓 뼈, 갓이 대에 붙는 곳) ─ prop(짐)
//       foot[i] (root 공간: 땅에 붙는다)
// 통 몸 (2026-10-20, 모습에 body가 있을 때): 갓과 대를 나누지 않은 그림 한 장을 줄마다 "몸 뼈(body) ↔ 갓 뼈(capT)"로 섞어 휜다 (C#: SpriteSkin 2뼈).
//   capJoint 아래 skinBandLo ~ 위 skinBandHi 사이가 부드럽게 휘는 목 → 갓이 늦게 따라와도 대에서 떨어져 보이지 않는다. 얼굴 층도 같은 스킨.
//   body가 없는 예전 모습은 갓·대 두 장 (갓 -10 · 대 0).
// 그리는 순서: 발 -5 · 몸 0 · 얼굴 10 · 손 20 · 짐 30
import { Mathf, Rng, Vector2, M2, Transform, SpriteRenderer } from "./unity.js";
import { Spring, Pendulum, MOTIONS } from "./rig2.js";

export const MUSH_DEFAULTS = {
  blend: 0.18,
  // 몸 젤리 (찌그러짐 스프링: 감쇠가 낮을수록 출렁) · 기울기 · 공중
  squashK: 260, squashZ: 0.28, leanK: 180, leanZ: 0.55, volume: 0.8,
  // 갓: 진자(옆 흔들림) · 세로 출렁(눌림) — 몸보다 늦게
  capK: 70, capZ: 0.24, capInertia: 2, capFollow: 0.92, capMaxRot: 4, capBobK: 220, capBobZ: 0.24, capBobInertia: 0.01, capSquash: 0.8,   // 갓은 몸에 거의 붙어 돈다 (capMaxRot: 몸에 대한 갓 자기 회전 최대 각. 크면 걸을 때 몸이 갓 밑에서 미끄러져 머리가 떨어져 보임)
  // 통 몸 스킨: 갓이 휘는 정도(capMaxRot 대신 skinMaxRot) · 갓이 옆으로 밀리는 정도 · 휘는 목 띠(capJoint 아래/위, 리그 단위)
  skinMaxRot: 9, capShear: 0.35, skinBandLo: 0.45, skinBandHi: 0.25,
  // 손 (★1): 늦게 따라옴
  nubK: 260, nubZ: 0.45, nubReach: 0.55,   // nubReach: 동작이 정한 손 이동량에 곱함 (짧은 동글 손은 몸 옆을 미끄러져 오르기보다 제자리에서 파닥이는 편이 자연스러움)
  // 대기
  breathPeriod: 2.6, breathAmt: 0.025, idleSway: 2, fidgetMin: 3, fidgetMax: 7,
  // 걷기 (폴짝 + 뒤뚱)
  walkSpeed: 0.9, stepRate: 4.2, stepHop: 0.045, waddle: 7, walkLean: 5, landSquash: 0.12, airStretch: 0.07, footStep: 0.18,
  carryRate: 3.2, carryHop: 0.025,
  // 뛰기·착지·폴짝
  jumpH: 0.42, crouch: 0.2, hopH: 0.16, hopPeriod: 0.5, landDip: 0.3,
  // 오르기 (폴짝폴짝 튀어 오르기)
  rungGap: 0.26, ladderHalfW: 0.95, trunkReach: 0.55, trunkGap: 0.3, climbHop: 0.55, climbLean: 12,
  // 기타
  danceBeat: 0.45, sleepLean: 8, heldKick: 0.12,
};

export const MUSH_DESC = {
  idle: "숨쉬듯 늘었다 줄고, 갓이 살짝 출렁", walk: "통통 튀며 뒤뚱뒤뚱, 갓이 늦게 따라 흔들림", carry: "머리(갓) 위에 바구니를 이고 무겁게 통통",
  work: "꾸벅 숙였다 펴며 줍기 (★1은 손을 번갈아 뻗음)", climb: "줄기 쪽으로 기울여 폴짝폴짝 튀어 오르기", climbDown: "줄기 오르기를 거꾸로",
  ladder: "사다리 앞에서 폴짝폴짝 한 칸씩 튀어 오르기", ladderDown: "사다리 오르기를 거꾸로", hop: "웅크렸다 팡 튀어 오르고 철퍽 착지",
  dance: "박자마다 통통, 좌우로 기울며 손을 번갈아", heart: "살랑살랑 흔들며 손을 모음", sleep: "깊게 숨쉬며 꾸벅, 갓이 처짐",
  held: "갓을 잡혀 대롱대롱, 몸이 늘어지고 발을 동동", fall: "늘어난 채 떨어짐", jump: "웅크림 → 쭉 늘어나며 뛰기 → 철퍽 찌그러짐",
  surprise: "움찔 늘어나며 튐", spin: "제자리에서 한 바퀴", land: "철퍽 찌그러졌다가 젤리처럼 출렁",
  wave: "★1 손 흔들기 · ★0 몸을 좌우로 흔들기", stretch: "쭉 늘어나며 기지개", look: "좌우로 기울여 두리번", hopOnce: "작게 한 번 폴짝", yawn: "뒤로 젖히며 하품",
};

export const MUSH_POSE_KEYS = ["air", "sqY", "lean", "shiftX", "capRot", "capY", "nubLX", "nubLY", "nubLRot", "nubRX", "nubRY", "nubRRot",
  "footLX", "footLY", "footLRot", "footRX", "footRY", "footRRot", "spin", "prop"];
export function MushRestPose() {
  const p = {};
  for (const k of MUSH_POSE_KEYS) p[k] = 0;
  p.sqY = 1; p.face = "normal"; p.hang = false; p.grounded = true;
  return p;
}

const V = (a) => Vector2.of(a);
const smooth = (t) => { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); };
const bump = (t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
const frac = (t) => t - Math.floor(t);
const seg = (u, a, b) => Mathf.Clamp01((u - a) / (b - a));

export class MushRig {
  static DEFAULTS = MUSH_DEFAULTS;
  static JSON_KEY = "mushMotion";
  static POSE_KEYS = MUSH_POSE_KEYS;

  constructor(sprites, seed = 11) {
    this.sprites = sprites;
    this.M = { ...MUSH_DEFAULTS };
    this.rng = new Rng(seed);
    this.look = null; this.outfit = ""; this.flip = 1; this.scale = 1;
    this.motion = "idle"; this.motionT = 0; this.time = 0;
    this.vel = new Vector2(); this.grabAccel = new Vector2();
    this.manual = null; this.faceOverride = null; this.autoFidget = true; this.keepFront = false;
    this.root = new Transform("Mush");
    this.headed = false; this.viewKey = "front";
    this.built = false;
    this.Reset();
  }

  Reset() {
    this.time = 0; this.motionT = 0; this.climbS = 0; this.stepPh = 0;
    this.prevPose = null; this.blendT = 99; this.pose = MushRestPose();
    this.sSq = new Spring(); this.sSq.Snap(1); this.sLean = new Spring(); this.sShift = new Spring(); this.sAir = new Spring();
    this.capP = new Pendulum(); this.capBob = new Spring(); this.hang = new Pendulum();
    this.sNub = [[new Spring(), new Spring(), new Spring()], [new Spring(), new Spring(), new Spring()]];
    this.prevCap = null; this.prevCapV = null;
    this.blinkT = 2.2; this.blink2 = false; this.pokeT = 99; this.fidget = null; this.fidgetT = 0; this.fidgetWait = 2.5;
    this.lastFace = "normal";
  }

  Sprite(name) { return name ? this.sprites(name) : null; }
  get hasNubs() { return !!(this.look && this.look.nubL && this.look.nubL.sprite); }

  Build() {
    const r = this.root; r.children = [];
    this.body = new Transform("Body", r);
    this.bodySR = new SpriteRenderer("BodySkin", this.body, 0);
    this.stemSR = new SpriteRenderer("Stem", this.body, 0);
    this.stemFace = new SpriteRenderer("Face", this.body, 10);
    this.capT = new Transform("Cap", this.body);
    this.capSR = new SpriteRenderer("CapSprite", this.capT, -10);
    this.propSR = new SpriteRenderer("Prop", this.capT, 30);
    this.nubPivot = []; this.nubT = []; this.nubSR = []; this.foot = []; this.footRot = []; this.shoeSR = [];
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      this.nubPivot[i] = new Transform("NubPivot" + n, this.body);
      this.nubT[i] = new Transform("Nub" + n, this.nubPivot[i]);
      this.nubSR[i] = new SpriteRenderer("NubSprite", this.nubT[i], 20);
      this.foot[i] = new Transform("Foot" + n, r);
      this.footRot[i] = new Transform("FootRot", this.foot[i]);
      this.shoeSR[i] = new SpriteRenderer("FootSprite", this.footRot[i], -5);
    }
    this.built = true;
  }

  Apply(look, outfit = "") {
    if (!this.built) this.Build();
    this.look = look; this.lv = look; this.outfit = outfit || "";
    const l = look;
    this.body.set(0, 0, 0);
    const C = V(l.capJoint), T = V(l.capTop);
    this.capJ = C;
    this.capT.set(C.x, C.y, 0);
    this.skinned = !!(l.body && this.Sprite(l.body.sprite));
    this.bodySR.sprite = this.skinned ? this.Sprite(l.body.sprite) : null;
    this.stemSR.sprite = this.skinned ? null : this.Sprite(l.stem && l.stem.sprite);
    this.capSR.sprite = this.skinned ? null : this.Sprite(l.cap && l.cap.sprite);
    const fp0 = this.skinned ? l.body : l.stem;                            // 얼굴 층 = 몸(또는 대) 그림과 같은 캔버스·자리
    this.stemFace.sprite = this.Sprite(l.face + "normal");
    this.stemFace.transform.set(fp0.x, fp0.y, 0);
    this.bodySR.skin = this.stemFace.skin = null;
    if (this.skinned) {
      this.bodySR.transform.set(l.body.x, l.body.y, 0);
      this.bodySR.skin = this.MakeBodySkin(l.body.y);
      this.stemFace.skin = this.MakeBodySkin(fp0.y);
    } else {
      this.stemSR.transform.set(l.stem.x, l.stem.y, 0);
      this.capSR.transform.set(l.cap.x - C.x, l.cap.y - C.y, 0);
    }
    const prop = this.Sprite("Rig2/prop_basket");
    this.propSR.sprite = prop;
    const pw = prop ? prop.w / prop.ppu : 1, ps = (l.height * 0.32) / pw;
    this.propSR.transform.set(T.x - C.x, T.y - C.y - 0.05, 0, ps, ps);
    this.propSR.enabled = false;
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      const np = l["nub" + n], pv = l["nubPivot" + n];
      this.nubPivot[i].SetActive(!!(np && np.sprite));
      if (np && np.sprite) {
        this.nubPivot[i].set(pv[0], pv[1], 0);
        this.nubT[i].set(0, 0, 0);
        this.nubSR[i].sprite = this.Sprite(np.sprite);
        this.nubSR[i].transform.set(np.x - pv[0], np.y - pv[1], 0);
      }
      const fp = i === 0 ? l.footL : l.footR, ank = V(i === 0 ? l.ankle : l.ankleR);
      this.foot[i].SetActive(!!fp);
      if (fp) {
        this.foot[i].set(ank.x, ank.y, 0);
        this.footRot[i].set(0, 0, 0);
        this.shoeSR[i].sprite = this.Sprite(fp.sprite);
        this.shoeSR[i].transform.set(fp.x - ank.x, fp.y - ank.y, 0);
      }
    }
    this.ankleRest = [V(l.ankle || [0, 0]), V(l.ankleR || [0, 0])];
    this.H = l.height; this.L = Math.max(0.2, C.y * 0.5);
  }
  Reapply() { this.Apply(this.look, this.outfit); }
  SetView() { }

  Play(name) {
    if (name === this.motion && MOTIONS[name]?.loop) return;
    this.prevPose = { ...this.pose }; this.blendT = 0;
    this.motion = name; this.motionT = 0; this.fidget = null;
    if (name === "held") { this.hang.a = 0; this.hang.v = 0; }
  }
  Poke() { this.pokeT = 0; }

  Tick(dt) {
    if (!this.look) return;
    dt = Math.min(dt, 0.05);
    this.time += dt; this.motionT += dt; this.blendT += dt; this.pokeT += dt;
    const M = this.M, info = MOTIONS[this.motion];
    if (info && !info.loop && this.motionT >= info.len) this.Play("idle");
    if (this.motion === "idle" && this.autoFidget) {
      if (!this.fidget) { this.fidgetWait -= dt; if (this.fidgetWait <= 0) { const list = ["wave", "stretch", "look", "hopOnce", "yawn"]; this.fidget = list[this.rng.Range(0, list.length)]; this.fidgetT = 0; } }
      else { this.fidgetT += dt; if (this.fidgetT >= MOTIONS[this.fidget].len) { this.fidget = null; this.fidgetWait = this.rng.Range(M.fidgetMin, M.fidgetMax); } }
    }
    let p = MushRestPose();
    this.Pose(this.motion, this.motionT, p, dt);
    if (this.motion === "idle" && this.fidget) {
      const q = { ...p }; this.Pose(this.fidget, this.fidgetT, q, dt);
      const e = smooth(Math.min(this.fidgetT, MOTIONS[this.fidget].len - this.fidgetT) / 0.2);
      p = this.Mix(p, q, e); p.face = q.face;
    }
    if (this.prevPose && this.blendT < M.blend) p = this.Mix(this.prevPose, p, smooth(this.blendT / M.blend));
    this.pose = p;
    this.Solve(p, dt);
    this.UpdateFace(p, dt);
  }

  Mix(a, b, t) {
    const o = { ...b };
    for (const k of MUSH_POSE_KEYS) o[k] = a[k] + (b[k] - a[k]) * t;
    o.face = t < 0.5 ? a.face : b.face; o.hang = b.hang; o.grounded = b.grounded;
    return o;
  }

  // ───── 동작 (MushRig.Pose) ─────
  Pose(name, t, p, dt) {
    const M = this.M, H = this.H, info = MOTIONS[name];
    const u = info && info.len ? Mathf.Clamp01(t / info.len) : 0;
    switch (name) {
      case "idle": this.PIdle(t, p); break;
      case "walk": this.PWalk(t, p, dt, false); break;
      case "carry": this.PWalk(t, p, dt, true); break;
      case "work": this.PWork(t, p); break;
      case "climb": this.PClimb(t, p, dt, 1, false); break;
      case "climbDown": this.PClimb(t, p, dt, -1, false); break;
      case "ladder": this.PClimb(t, p, dt, 1, true); break;
      case "ladderDown": this.PClimb(t, p, dt, -1, true); break;
      case "hop": this.PHop(frac(t / M.hopPeriod), p, M.hopH); break;
      case "hopOnce": this.PIdle(t, p); this.PHop(u, p, M.hopH * 0.7); break;
      case "jump": this.PHop(u, p, M.jumpH, true); break;
      case "surprise": this.PSurprise(u, p); break;
      case "spin": this.PSpin(u, p); break;
      case "land": this.PLand(u, p); break;
      case "dance": this.PDance(t, p); break;
      case "heart": this.PHeart(t, p); break;
      case "sleep": this.PSleep(t, p); break;
      case "held": this.PHeld(t, p); break;
      case "fall": this.PFall(t, p); break;
      case "wave": this.PIdle(t, p); this.PWave(u, t, p); break;
      case "stretch": this.PIdle(t, p); this.PStretch(u, p); break;
      case "look": this.PIdle(t, p); this.PLook(u, p); break;
      case "yawn": this.PIdle(t, p); this.PYawn(u, p); break;
      case "pose": if (this.manual) Object.assign(p, this.manual); break;
    }
  }

  // 숨: 늘었다 줄고 (부피 유지라 가로는 반대로), 아주 천천히 좌우로
  PIdle(t, p) {
    const M = this.M;
    p.sqY = 1 + Mathf.Sin(t * 2 * Mathf.PI / M.breathPeriod) * M.breathAmt;
    p.lean = Mathf.Sin(t * 0.7) * M.idleSway;
    p.nubLY = p.nubRY = Mathf.Sin(t * 2 * Mathf.PI / M.breathPeriod) * 0.01;
    p.face = "normal";
  }

  // 걷기: 한 걸음마다 폴짝 (뜰 때 늘어나고, 닿을 때 찌그러짐) + 디딘 쪽으로 뒤뚱 기울기 + 가는 쪽으로 살짝 숙임.
  // 발은 번갈아 들고, 땅에 있는 발은 제자리 (몸이 그 위로 튐)
  PWalk(t, p, dt, carry) {
    const M = this.M, H = this.H;
    const rate = carry ? M.carryRate : M.stepRate;
    this.stepPh += dt * rate;
    const k = Math.floor(this.stepPh), u = frac(this.stepPh), side = k % 2 === 0 ? 1 : -1;
    const hop = (carry ? M.carryHop : M.stepHop) * H;
    const air = bump(seg(u, 0.12, 0.88));
    p.air = air * hop;
    const land = u < 0.12 ? 1 - u / 0.12 : u > 0.88 ? (u - 0.88) / 0.12 : 0;   // 닿는 순간
    p.sqY = 1 - land * M.landSquash * (carry ? 1.4 : 1) + air * M.airStretch;
    p.lean = side * M.waddle * Mathf.Sin(u * Mathf.PI) + (this.keepFront ? 0 : M.walkLean);
    p.shiftX = side * 0.02 * H * Mathf.Sin(u * Mathf.PI);
    const n = side > 0 ? "R" : "L";                                                   // 오른쪽으로 기울 땐 왼발을 든다
    const lift = bump(seg(u, 0.05, 0.8)) * M.footStep * this.L;
    p["foot" + (side > 0 ? "L" : "R") + "Y"] = lift;
    p["foot" + (side > 0 ? "L" : "R") + "X"] = (this.keepFront ? 0 : 0.06) * H * (u - 0.5);
    p.footLRot = p.footRRot = 0;
    if (this.hasNubs) {                                                               // ★1: 손을 번갈아 파닥
      p.nubLRot = Mathf.Sin(this.stepPh * Mathf.PI) * 18; p.nubRRot = -Mathf.Sin(this.stepPh * Mathf.PI) * 18;
      p.nubLY = p.nubRY = air * 0.03 * H;
    }
    if (carry) {
      p.prop = 1; p.face = "effort";
      if (this.hasNubs) { p.nubLY = p.nubRY = 0.42 * H; p.nubLX = 0.06 * H; p.nubRX = -0.06 * H; p.nubLRot = -60; p.nubRRot = 60; }   // 손으로 짐을 받침
    } else p.face = "normal";
    this.walkInfo = { speed: M.walkSpeed * H, cadence: rate, D: 0, stepLen: M.walkSpeed * H / rate };
  }

  // 일하기: 꾸벅 숙였다 펴며 줍기. ★1은 손을 번갈아 위로 뻗음
  PWork(t, p) {
    const H = this.H, c = t / 1.0, k = Math.floor(c), u = frac(c);
    const dip = bump(seg(u, 0.1, 0.6));
    p.lean = 14 * dip; p.sqY = 1 - 0.1 * dip + 0.06 * bump(seg(u, 0.6, 0.9));
    p.air = bump(seg(u, 0.62, 0.9)) * 0.03 * H;
    if (this.hasNubs) {
      const n = k % 2 === 0 ? "L" : "R";
      p["nub" + n + "Y"] = 0.25 * H * bump(seg(u, 0.55, 1)); p["nub" + n + "Rot"] = (n === "L" ? -1 : 1) * 40 * bump(seg(u, 0.55, 1));
      p["nub" + n + "X"] = (n === "L" ? 1 : -1) * 0.05 * H * dip;
    }
    p.face = dip > 0.3 ? "effort" : "normal";
  }

  // 오르기: 폴짝폴짝 한 칸씩. 웅크림 → 위로 팡 (늘어남) → 다음 가로대에 착지 (찌그러짐). 사다리(뒤 그림 없음) = 정면, 줄기 = 줄기 쪽으로 기울임
  PClimb(t, p, dt, dir, ladder) {
    const M = this.M, H = this.H;
    const G = ladder ? M.rungGap : M.trunkGap;
    const period = 0.55;
    const ph = t / period, u = frac(ph);
    const crouch = bump(seg(u, 0, 0.3)), air = seg(u, 0.25, 0.8), land = bump(seg(u, 0.78, 1));
    const step = G * 2;                                                                // 한 번에 두 칸
    // 몸이 뜬 동안 사다리(세상)가 한 걸음만큼 내려간다 → 손발이 다음 칸에 닿는다
    this.climbS = dir * (Math.floor(ph) + smooth(air)) * step;
    p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * M.climbHop * step : 0;
    p.sqY = 1 - crouch * 0.16 - land * 0.14 + (air > 0 && air < 0.5 ? bump(air / 0.5) * 0.14 : 0);
    p.lean = ladder ? 0 : M.climbLean;
    p.footLY = p.footRY = bump(air) * 0.04 * H;
    this.rungGap = G; this.rungBase = 0; this.climbLadder = ladder;
    if (this.hasNubs) { p.nubLY = p.nubRY = (0.2 + 0.15 * bump(air)) * H; p.nubLRot = -50; p.nubRRot = 50; if (!ladder) { p.nubLX = p.nubRX = 0.08 * H; } }
    p.face = "effort";
  }

  // 폴짝/점프 (u 0~1): 웅크림(예비) → 늘어나며 뜀 → 공중(발 모음) → 착지 찌그러짐. 갓 출렁은 스프링이
  PHop(u, p, h, big) {
    const M = this.M, H = this.H;
    const crouch = bump(seg(u, 0, 0.28)), land = bump(seg(u, 0.8, 1)), air = seg(u, 0.24, 0.82);
    p.air = air > 0 && air < 1 ? 4 * air * (1 - air) * h * H : 0;
    p.sqY = 1 - crouch * M.crouch - land * M.crouch * (big ? 1.1 : 0.8) + (air > 0 && air < 0.45 ? bump(air / 0.45) * (big ? 0.22 : 0.12) : 0);
    p.footLY = p.footRY = bump(air) * 0.05 * H;
    if (this.hasNubs) { const up = smooth(seg(u, 0.2, 0.35)) * (1 - smooth(seg(u, 0.75, 0.9))); p.nubLY = p.nubRY = up * 0.22 * H; p.nubLRot = -60 * up; p.nubRRot = 60 * up; }
    p.face = u < 0.2 ? "normal" : "happy";
    p.grounded = p.air <= 0;
  }

  PSurprise(u, p) {
    const H = this.H, pop = seg(u, 0, 0.3);
    p.air = pop < 1 ? 4 * pop * (1 - pop) * 0.08 * H : 0;
    const e = smooth(seg(u, 0, 0.1)) * (1 - smooth(seg(u, 0.5, 1)));
    p.sqY = 1 + 0.16 * e - bump(seg(u, 0.3, 0.5)) * 0.12;
    if (this.hasNubs) { p.nubLY = p.nubRY = 0.3 * H * e; p.nubLRot = -80 * e; p.nubRRot = 80 * e; }
    p.face = "surprise";
  }

  PSpin(u, p) {
    const H = this.H, a = seg(u, 0.05, 0.6);
    p.spin = smooth(u); p.air = a > 0 && a < 1 ? 4 * a * (1 - a) * 0.1 * H : 0;
    p.sqY = 1 + 0.06 * bump(u); p.face = "happy";
  }

  PLand(u, p) {
    const M = this.M, hit = Mathf.Exp(-u * 6);
    p.sqY = 1 - hit * M.landDip; p.lean = 0; p.face = u < 0.35 ? "surprise" : "happy";
    if (this.hasNubs) { p.nubLY = p.nubRY = hit * 0.15 * this.H; }
  }

  PDance(t, p) {
    const M = this.M, H = this.H, b = t / M.danceBeat, k = Math.floor(b), u = frac(b), sg = k % 2 === 0 ? 1 : -1;
    p.sqY = 1 - bump(u) ** 2 * 0.12 + bump(seg(u, 0.4, 0.8)) * 0.06;
    p.air = bump(seg(u, 0.35, 0.9)) * 0.04 * H;
    p.lean = Mathf.Sin(Mathf.PI * b) * 10;
    if (this.hasNubs) { p["nub" + (sg > 0 ? "L" : "R") + "Y"] = 0.3 * H; p.nubLRot = sg > 0 ? -70 : 10; p.nubRRot = sg > 0 ? -10 : 70; }
    p["foot" + (sg > 0 ? "L" : "R") + "Y"] = bump(seg(u, 0.1, 0.6)) * 0.12 * this.L;
    p.face = "happy";
  }

  PHeart(t, p) {
    const H = this.H, s = Mathf.Sin(t * 3);
    p.lean = s * 6; p.sqY = 1 + Math.abs(Mathf.Sin(t * 6)) * 0.04;
    if (this.hasNubs) { p.nubLX = 0.12 * H; p.nubRX = -0.12 * H; p.nubLY = p.nubRY = 0.08 * H; }
    p.face = "happy";
  }

  PSleep(t, p) {
    const M = this.M, br = Mathf.Sin(t * 2 * Mathf.PI / 4);
    p.sqY = 1 + br * 0.04; p.lean = M.sleepLean + br; p.capRot = 6 + br * 2;
    p.face = "blink";
  }

  // 들림: 갓 꼭대기에 매달려 대롱대롱 (진자) · 몸이 무게로 늘어남 · 발 동동 · ★1 손 파닥
  PHeld(t, p) {
    const M = this.M, H = this.H;
    p.hang = true; p.grounded = false;
    p.sqY = 1.14 + Mathf.Sin(t * 9) * 0.03;
    const k1 = Mathf.Sin(t * 14), k2 = Mathf.Sin(t * 14 + 2.4);
    p.footLY = Math.max(0, k1) * M.heldKick * H - 0.03 * H; p.footRY = Math.max(0, k2) * M.heldKick * H - 0.03 * H;
    p.lean = Mathf.Sin(t * 7) * 6;
    if (this.hasNubs) { p.nubLRot = -40 + Mathf.Sin(t * 22) * 35; p.nubRRot = 40 - Mathf.Sin(t * 22 + 1) * 35; p.nubLY = p.nubRY = 0.12 * H; }
    p.face = Math.floor(t * 2.2) % 3 === 2 ? "effort" : "surprise";
  }

  PFall(t, p) {
    const H = this.H;
    p.grounded = false; p.sqY = 1.12;
    p.footLY = -0.02 * H + Math.max(0, Mathf.Sin(t * 16)) * 0.04 * H; p.footRY = -0.02 * H + Math.max(0, -Mathf.Sin(t * 16)) * 0.04 * H;
    if (this.hasNubs) { p.nubLY = p.nubRY = 0.3 * H; p.nubLRot = -90 + Mathf.Sin(t * 26) * 10; p.nubRRot = 90 - Mathf.Sin(t * 26) * 10; }
    p.face = "surprise";
  }

  // 손 흔들기: ★1 = 한 손을 들어 흔듦, ★0 = 몸을 좌우로 흔들며 살짝 폴짝
  PWave(u, t, p) {
    const H = this.H, e = smooth(seg(u, 0.05, 0.25)) * (1 - smooth(seg(u, 0.8, 1)));
    if (this.hasNubs) {
      const n = this.waveSide === "L" ? "L" : "R", sg = n === "L" ? -1 : 1;
      p["nub" + n + "Y"] = 0.28 * H * e; p["nub" + n + "X"] = sg * 0.02 * H * e; p["nub" + n + "Rot"] = sg * (60 + Mathf.Sin(t * 16) * 30) * e;
      p.lean += sg * 4 * e;
    } else {
      p.lean += Mathf.Sin(t * 10) * 9 * e; p.air += Math.abs(Mathf.Sin(t * 10)) * 0.02 * H * e;
    }
    p.face = "happy";
  }

  PStretch(u, p) {
    const H = this.H, e = smooth(seg(u, 0.05, 0.4)) * (1 - smooth(seg(u, 0.7, 1)));
    p.sqY = 1 + 0.16 * e; p.lean = 0;
    if (this.hasNubs) { p.nubLY = p.nubRY = 0.3 * H * e; p.nubLRot = -80 * e; p.nubRRot = 80 * e; }
    p.face = e > 0.4 ? "blink" : "normal";
  }

  PLook(u, p) {
    const a = smooth(seg(u, 0.05, 0.25)) - smooth(seg(u, 0.4, 0.6)) * 2 + smooth(seg(u, 0.75, 0.95));
    p.lean += a * 9; p.shiftX += a * 0.02 * this.H; p.face = "normal";
  }

  PYawn(u, p) {
    const H = this.H, e = smooth(seg(u, 0.1, 0.35)) * (1 - smooth(seg(u, 0.7, 0.95)));
    p.lean += -6 * e; p.sqY = 1 + 0.08 * e;
    if (this.hasNubs) { p.nubLY = 0.18 * H * e; p.nubLX = 0.08 * H * e; p.nubLRot = -50 * e; }
    p.face = e > 0.3 ? "blink" : "normal";
  }

  // ───── 풀기 (MushRig.Solve) ─────
  Solve(p, dt) {
    const M = this.M, l = this.look, H = this.H;
    this.sSq.Step(p.sqY, M.squashK, M.squashZ, dt);                          // 젤리: 넘쳤다 돌아옴
    this.sLean.Step(p.lean, M.leanK, M.leanZ, dt);
    this.sShift.Step(p.shiftX, M.leanK, M.leanZ, dt);
    this.sAir.Step(p.air, 900, 1, dt);
    const air = p.air > 0 ? p.air : Math.max(0, this.sAir.x);
    const spinC = Mathf.Cos(p.spin * 2 * Mathf.PI);
    let rootRot = 0, rootPos = new Vector2(0, air);
    if (p.hang) {
      const capTop = V(l.capTop);
      this.hang.Step(0, 26, 0.12, -this.grabAccel.x * 4, dt);
      rootRot = Mathf.Clamp(this.hang.a, -55, 55);
      const r = rootRot * Mathf.Deg2Rad;
      rootPos = capTop.sub(new Vector2(capTop.x * Math.cos(r) - capTop.y * Math.sin(r), capTop.x * Math.sin(r) + capTop.y * Math.cos(r)));
    }
    this.root.set(rootPos.x, rootPos.y, rootRot, this.scale * this.flip * (Math.abs(spinC) < 0.06 ? 0.06 * Mathf.Sign(spinC) : spinC), this.scale);
    // 몸: 발바닥 축으로 기울고(lean + = 오른쪽/가는 쪽) 찌그러짐 (부피 유지: 가로 = 1/√세로)
    const sy = Mathf.Clamp(this.sSq.x, 0.55, 1.6), sx = Math.pow(1 / sy, 0.5 * M.volume);   // volume 1 = 부피 그대로
    const jig = this.pokeT < 1.2 ? 0.14 * Mathf.Exp(-5 * this.pokeT) * Mathf.Sin(this.pokeT * 26) : 0;
    this.body.set(this.sShift.x, 0, -this.sLean.x, sx * (1 + jig * 0.4), sy * (1 - jig * 0.4));
    this.stemFace.enabled = spinC >= 0;
    // 갓: 진자 (몸 기울기를 늦게 따라감 + 갓 테두리 점의 가속도) + 세로 출렁 (가속도로 눌렸다 튐)
    const cp = this.capT.localToWorld.point(0, 0).add(new Vector2(this.vel.x * this.time, 0));
    const v = this.prevCap ? cp.sub(this.prevCap).mul(1 / Math.max(dt, 1e-4)) : new Vector2();
    const a = this.prevCapV ? v.sub(this.prevCapV).mul(1 / Math.max(dt, 1e-4)) : new Vector2();
    this.prevCap = cp; this.prevCapV = v;
    const ax = Mathf.Clamp(a.x, -80, 80), ay = Mathf.Clamp(a.y, -80, 80);
    // 갓의 기울기(세상 기준)는 몸 기울기를 늦게 따라간다 → 몸에 붙은 갓의 자기 회전 = (늦은 기울기 - 몸 기울기). capFollow = 따라가는 정도
    this.capP.Step(this.sLean.x * M.capFollow + p.capRot, M.capK, M.capZ, ax * M.capInertia * 10, dt);   // + = 오른쪽으로 (시계 방향)
    const maxRot = this.skinned ? M.skinMaxRot : M.capMaxRot;              // 통 몸은 목이 휘므로 더 크게 기울어도 떨어져 보이지 않는다
    const lag = Mathf.Clamp(this.capP.a - this.sLean.x, -maxRot, maxRot);  // + = 갓이 오른쪽(시계 방향)으로
    this.capT.localRotation = -lag;
    this.capBob.Step(0, M.capBobK, M.capBobZ, dt);
    this.capBob.v += -ay * M.capBobInertia * dt * 60;
    const bob = Mathf.Clamp(this.capBob.x, -0.25, 0.25);
    const shear = this.skinned ? Mathf.Sin(lag * Mathf.Deg2Rad) * this.capJ.y * M.capShear : 0;   // 갓이 기운 쪽으로 대가 휘며 밀림
    this.capT.localPosition.x = this.capJ.x + shear; this.capT.localPosition.y = this.capJ.y + bob * 0.4 * H * 0.1;
    const cs = Mathf.Clamp(-bob * M.capSquash, -0.14, 0.14) + jig * 0.6;
    this.capT.localScale.x = (1 + cs) / sx; this.capT.localScale.y = (1 - cs) / sy;     // 갓은 몸 찌그러짐을 그대로 받지 않고 자기 출렁만 (단단한 갓)
    this.propSR.enabled = p.prop > 0.5;
    // 손 (★1): 붙는 점 기준 위치·회전, 조금 늦게
    for (let i = 0; i < 2; i++) {
      if (!this.nubPivot[i].activeSelf) continue;
      const n = i === 0 ? "L" : "R", s = this.sNub[i];
      s[0].Step(p["nub" + n + "X"] * M.nubReach, M.nubK, M.nubZ, dt); s[1].Step(p["nub" + n + "Y"] * M.nubReach, M.nubK, M.nubZ, dt); s[2].Step(p["nub" + n + "Rot"], M.nubK, M.nubZ, dt);
      this.nubT[i].set(s[0].x / sx, s[1].x / sy, s[2].x, 1 / sx, 1 / sy);           // 손은 몸 찌그러짐을 덜 받는다
    }
    // 발: 땅에 붙는다 (몸이 찌그러지면 발 간격이 벌어짐). 공중이면 몸을 따라감
    for (let i = 0; i < 2; i++) {
      if (!this.foot[i].activeSelf) continue;
      const n = i === 0 ? "L" : "R", rest = this.ankleRest[i];
      const y = rest.y + p["foot" + n + "Y"];
      this.foot[i].set(rest.x * sx + p["foot" + n + "X"] + this.sShift.x * 0.5, p.grounded ? y : y, p["foot" + n + "Rot"]);
    }
    this.legStretch = [1, 1];
  }

  // 통 몸 스킨 (C#: SpriteSkin 2뼈 가중치와 같은 규칙). sprY = 렌더러 그림 중심 y (몸 공간).
  //  그림 줄 row의 높이 y가 capJoint - skinBandLo 아래면 몸 뼈(그대로), capJoint + skinBandHi 위면 갓 뼈, 그 사이는 부드럽게 섞는다.
  //  갓 뼈 변형 = capT 지금 행렬 · (쉬는 자리 capJoint로 되돌림)
  MakeBodySkin(sprY) {
    const rig = this, sk = { space: this.body, cap: new M2() };
    sk.prepare = () => { const J = rig.capJ; sk.cap = rig.capT.localMatrix.mul(new M2(1, 0, 0, 1, -J.x, -J.y)); };
    sk.at = (row, s) => {
      const M = rig.M, y = sprY + (1 - s.py) * s.h / s.ppu - row / s.ppu;
      const lo = rig.capJ.y - M.skinBandLo, hi = rig.capJ.y + M.skinBandHi;
      let t = Mathf.Clamp01((y - lo) / Math.max(1e-4, hi - lo)); t = t * t * (3 - 2 * t);
      const A = sk.cap;
      return new M2(1 + (A.a - 1) * t, A.b * t, A.c * t, 1 + (A.d - 1) * t, A.e * t, A.f * t);
    };
    return sk;
  }

  UpdateFace(p, dt) {
    let face = p.face || "normal";
    this.blinkT -= dt;
    if (this.blinkT <= 0) { this.blink2 = !this.blink2 && this.rng.value < 0.25; this.blinkT = this.blink2 ? 0.28 : this.rng.Range(2.2, 4.8); }
    if (this.pokeT < 0.25) face = "surprise"; else if (this.pokeT < 1.1) face = "happy";
    if (this.blinkT < 0.13 && (face === "normal" || face === "effort")) face = "blink";
    if (this.faceOverride) face = this.faceOverride;
    const l = this.look;
    this.stemFace.sprite = this.Sprite(l.face + face) || this.Sprite(l.face + "normal");
    this.lastFace = face;
  }

  Joints() {
    const l = this.look; if (!l) return [];
    const P = (t, x = 0, y = 0) => t.localToWorld.point(x, y);
    const J = [{ key: "capJoint", at: P(this.capT), parent: null }];
    J.push({ key: "capTop", at: P(this.body, V(l.capTop).x, V(l.capTop).y), parent: "capJoint" });
    for (let i = 0; i < 2; i++) {
      const n = i === 0 ? "L" : "R";
      if (this.nubPivot[i].activeSelf) J.push({ key: "nubPivot" + n, at: P(this.nubPivot[i]), parent: "capJoint" });
      if (this.foot[i].activeSelf) J.push({ key: "ankle" + n, at: P(this.foot[i]), parent: "capJoint" });
    }
    return J;
  }
}
