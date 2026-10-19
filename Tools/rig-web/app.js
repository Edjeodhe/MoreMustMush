// 정령 리그2 작업장 화면: 모습 고르기 · 재생/스크럽 · 관절·부품 드래그 편집 · 동작 상수 · 수동 포즈 · 저장 · C# 조각
import { SpiritRig2, MOTIONS, MOTION_DEFAULTS, POSE_KEYS, RestPose } from "./rig2.js";
import { M2, collectRenderers, drawSprite, Mathf, Vector2 } from "./unity.js";
import * as RJ from "./rigjson.js";
import { MushRig, MUSH_DEFAULTS, MUSH_DESC, MUSH_POSE_KEYS, MushRestPose } from "./mush_rig.js";

const RIG_URL = "/Assets/Resources/Rig/spirit_rig2.json";
const FIXED = 1 / 60;
const LOAD_T = Date.now();
const $ = (id) => document.getElementById(id);
const canvas = $("stage"), ctx = canvas.getContext("2d");

const S = {
  doc: null, orig: null, origText: "", nl: "\r\n", spriteMap: {}, cache: {},
  look: null, outfit: "", motion: "idle", playing: true, speed: 1, simTime: 0, acc: 0,
  zoom: 90, pan: new Vector2(0, 0), charX: 0, grabY: 0, travel: 0,
  drag: null, hover: null, grid: null, dirty: false,
};

// ───── 그림 ─────
function sprite(name) {
  const key = "Characters/" + name;
  if (S.cache[key] !== undefined) return S.cache[key];
  const info = S.spriteMap[key];
  if (!info) { S.cache[key] = null; return null; }
  const img = new Image();
  img.onload = () => requestDraw();
  img.src = info.url.startsWith("data:") ? info.url : info.url + (info.url.includes("?") ? "&" : "?") + "t=" + LOAD_T;   // 새로고침마다 새 그림 (브라우저 캐시 무시)
  return (S.cache[key] = { img, w: info.w, h: info.h, px: info.px, py: info.py, ppu: info.ppu, key });
}

// ★2·★3 = 사람형 리그2 (SpiritRig2), ★0·★1 = 버섯 리그 (MushRig). 모습을 바꾸면 리그도 바뀐다
const rigHuman = new SpiritRig2(sprite), rigMush = new MushRig(sprite);
let rig = rigHuman;
Object.defineProperty(window, "rig", { get: () => rig });
const isMush = (r = rig) => r instanceof MushRig;
const makeRig = (look) => (look.form >= 2 ? new SpiritRig2(sprite) : new MushRig(sprite));
const constsOf = (r = rig) => (isMush(r) ? { key: "mushMotion", defaults: MUSH_DEFAULTS, cls: "MushMotion" } : { key: "motion", defaults: MOTION_DEFAULTS, cls: "Rig2Motion" });
const descOf = (k, r = rig) => (isMush(r) ? MUSH_DESC[k] : null) || MOTIONS[k]?.desc || "";

function motionConsts(r = rig) { const c = constsOf(r); return { ...c.defaults, ...(S.doc[c.key] || {}) }; }

// ───── 불러오기 ─────
async function load() {
  const [map, text] = await Promise.all([
    fetch("/api/sprites?dirs=Characters/Evo,Characters/Rig2").then((r) => r.json()),
    fetch(RIG_URL, { cache: "no-store" }).then((r) => r.text()),
  ]);
  S.spriteMap = map; S.origText = text; S.nl = RJ.newlineOf(text);
  // 그림을 전부 미리 불러 둔다 (표정·복장을 바꿀 때 한 프레임 비지 않게)
  await Promise.all(Object.keys(map).map((k) => new Promise((res) => { const s = sprite(k.slice("Characters/".length)); if (!s) return res(); if (s.img.complete) res(); else { s.img.onload = res; s.img.onerror = res; } })));
  S.doc = RJ.parse(text); S.orig = RJ.clone(S.doc);
  rig.M = motionConsts();
  const kinds = [...new Set(S.doc.looks.map((l) => l.kind))];
  $("selKind").innerHTML = kinds.map((k) => `<option>${k}</option>`).join("");
  $("selKind").value = kinds.includes("fire") ? "fire" : kinds[0];
  fillForms(3);
  buildMotionButtons(); buildMotionTab(); buildPoseTab(); refreshCs();
  status(`불러옴: ${S.doc.looks.length}개 모습 · 그림 ${Object.keys(map).length}장`);
}

function fillForms(pref) {
  const k = $("selKind").value;
  const forms = S.doc.looks.filter((l) => l.kind === k).map((l) => l.form).sort();
  $("selForm").innerHTML = forms.map((f) => `<option value="${f}">★${f}</option>`).join("");
  $("selForm").value = forms.includes(pref) ? pref : forms[forms.length - 1];
  setLook();
}

function setLook() {
  const id = $("selKind").value + "_" + $("selForm").value;
  S.look = S.doc.looks.find((l) => l.id === id);
  S.outfit = $("selOutfit").value;
  const next = S.look.form >= 2 ? rigHuman : rigMush;
  if (next !== rig) {
    for (const k of ["flip", "autoFidget", "faceOverride", "waveSide", "keepFront"]) next[k] = rig[k];
    rig = next;
    buildMotionTab(); refreshCs(); buildViewerButtons();
  }
  rig.Apply(S.look, S.outfit);
  restart();
  buildLookTab(); refreshDiff();
  // 이 모습의 그림을 미리 불러 둔다
  for (const r of collectAll(rig.root)) if (r.sprite) void r.sprite;
  fitZoom();
}

function collectAll(root) { const out = []; (function w(t) { if (t.renderer) out.push(t.renderer); t.children.forEach(w); })(root); return out; }

function fitZoom() {
  const h = canvas.clientHeight || 600;
  S.zoom = Math.round(h * 0.62 / (S.look.height * 1.05));
  $("rngZoom").value = S.zoom; $("txtZoom").textContent = S.zoom;
}

// ───── 재생 (고정 1/60초 단계 → 스크럽해도 같은 결과) ─────
function restart() {
  rig.Reset(); rig.M = motionConsts();
  rig.faceOverride = $("selFace").value || null;
  rig.autoFidget = $("chkFidget").checked;
  rig.waveSide = $("selWave").value;
  rig.flip = $("chkFlip").checked ? -1 : 1;
  rig.motion = S.motion; rig.motionT = 0; rig.prevPose = null;
  rig.vel = new Vector2(); rig.grabAccel = new Vector2();
  S.simTime = 0; S.travel = 0; S.grabHist = [];
  rig.Tick(1e-4); rig.time = 0; rig.motionT = 0;      // 0초에도 발·얼굴이 제자리에 (계산 한 번)
  if (S.grid) for (const g of S.grid) { g.rig.Reset(); g.rig.M = motionConsts(g.rig); g.rig.motion = S.motion; g.rig.flip = rig.flip; g.rig.autoFidget = rig.autoFidget; }
}

function stepSim(r = rig, isMain = true) {
  const want = S.motion;
  r.vel.x = (want === "walk" || want === "carry") && r.walkInfo ? r.walkInfo.speed * r.flip : 0;
  if (S.grabFn && want === "held") r.grabAccel.x = S.grabFn(S.simTime);      // 확인용: 정해진 손 흔들기
  r.Tick(FIXED);
  if ($("chkLoopOne").checked && !MOTIONS[want].loop && r.motion === "idle") r.Play(want);
  if (isMain) {
    S.simTime += FIXED;
    if (r.walkInfo && (want === "walk" || want === "carry")) S.travel += r.walkInfo.speed * FIXED;
    else if (want.startsWith("climb") || want.startsWith("ladder")) S.travel = r.climbS;
    else if (want === "hop") S.travel += 0.9 * r.H * FIXED * 0.6;
  }
}

function seek(t) {
  restart();
  const n = Math.round(t / FIXED);
  for (let i = 0; i < n; i++) { stepSim(); if (S.grid) S.grid.forEach((g) => stepSim(g.rig, false)); }
}

function play(m) {
  S.motion = m;
  if (m === "pose" && !rig.manual) rig.manual = { ...rig.pose };
  rig.Play(m);
  if (S.grid) S.grid.forEach((g) => g.rig.Play(m));
  document.querySelectorAll("#motions button, #viewerMotions button").forEach((b) => b.classList.toggle("on", b.dataset.m === m));
  updateCaption();
  const info = MOTIONS[m];
  $("rngTime").max = info.len ? Math.max(2, info.len * 2) : 6;
}

let last = performance.now();
function frame(now) {
  const dt = Math.min(0.1, (now - last) / 1000); last = now;
  if (S.playing) {
    S.acc += dt * S.speed;
    let n = 0;
    while (S.acc >= FIXED && n < 8) { S.acc -= FIXED; stepSim(); if (S.grid) S.grid.forEach((g) => stepSim(g.rig, false)); n++; }
    if (n === 8) S.acc = 0;
    if (S.demo) tickDemo(dt * S.speed);
    const max = Number($("rngTime").max);
    $("rngTime").value = S.simTime % max;
  }
  // 들고 흔들기: 손(정령 위치) 가속도 → 리그
  if (S.motion === "held") {
    const h = S.grabHist; h.push({ t: now / 1000, x: S.charX });
    while (h.length > 4) h.shift();
    if (h.length >= 3) {
      const [a, b, c] = h.slice(-3);
      const v1 = (b.x - a.x) / Math.max(1e-3, b.t - a.t), v2 = (c.x - b.x) / Math.max(1e-3, c.t - b.t);
      rig.grabAccel.x = Mathf.Clamp((v2 - v1) / Math.max(1e-3, c.t - b.t), -400, 400) * rig.flip;
    }
  } else rig.grabAccel.x = 0;
  $("txtTime").textContent = S.simTime.toFixed(2) + "s";
  const wi = rig.walkInfo;
  $("walkInfo").textContent = (S.motion === "walk" || S.motion === "carry") && wi
    ? `속도 ${wi.speed.toFixed(2)}/s · ${wi.cadence.toFixed(2)}걸음/s · 보폭 ${wi.stepLen.toFixed(2)} (다리 ${rig.L.toFixed(2)})${rig.legStretch && Math.max(...rig.legStretch) >= 1.079 ? " · ⚠ 다리가 닿지 않아 발이 미끄러짐" : ""}`
    : `L ${rig.L?.toFixed(2)} · H ${rig.H?.toFixed(2)}`;
  draw();
  requestAnimationFrame(frame);
}
let drawPending = false;
function requestDraw() { if (!S.playing && !drawPending) { drawPending = true; requestAnimationFrame(() => { drawPending = false; }); } }

// ───── 그리기 ─────
function viewMatrix(w, h, zoom = S.zoom, ox = 0) {
  return new M2(zoom, 0, 0, -zoom, w / 2 + S.pan.x + ox * zoom, h * (S.viewer ? 0.92 : 0.8) + S.pan.y);
}

function draw() {
  const dpr = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) { canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr); }
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  drawBg(ctx, w, h);
  if (S.grid) return drawGrid(w, h, dpr);
  const charX = $("chkTravel").checked && S.motion !== "held" ? wrapTravel(w) : S.charX;
  const base = new M2(dpr, 0, 0, dpr, 0, 0).mul(viewMatrix(w, h, S.zoom, charX));
  if ($("chkGround").checked) drawGround(ctx, new M2(dpr, 0, 0, dpr, 0, 0).mul(viewMatrix(w, h)), w, rig);
  renderRig(ctx, rig, base);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  if (S.motion === "held") drawFinger(viewMatrix(w, h, S.zoom, charX));
  if (!S.viewer && ($("chkBones").checked || $("chkParts").checked)) drawHandles(viewMatrix(w, h, S.zoom, charX));
}

function wrapTravel(w) {
  const span = w / S.zoom * 0.8;
  return ((S.travel * rig.flip + span / 2) % span + span) % span - span / 2;
}

function drawBg(c, w, h) {
  const cs = getComputedStyle(document.documentElement);
  if ($("chkChecker").checked) {
    const a = cs.getPropertyValue("--check1"), b = cs.getPropertyValue("--check2"), q = 16;
    c.fillStyle = b; c.fillRect(0, 0, w, h); c.fillStyle = a;
    for (let y = 0; y < h; y += q) for (let x = ((y / q) % 2) * q; x < w; x += q * 2) c.fillRect(x, y, q, q);
  } else { c.fillStyle = cs.getPropertyValue("--bg"); c.fillRect(0, 0, w, h); }
}

function renderRig(c, r, m) {
  for (const sr of collectRenderers(r.root)) drawSprite(c, m, sr);
  c.setTransform(1, 0, 0, 1, 0, 0);
}

function drawGround(c, m, w, r) {
  const p0 = m.point(-100, 0);
  c.setTransform(1, 0, 0, 1, 0, 0);
  c.strokeStyle = "rgba(120,100,80,.55)"; c.lineWidth = 1.5;
  c.beginPath(); c.moveTo(0, p0.y); c.lineTo(c.canvas.width, p0.y); c.stroke();
  const travel = $("chkTravel").checked ? 0 : S.travel * rig.flip;
  if (S.motion === "ladder" || S.motion === "ladderDown") {
    // 사다리 (뒷모습 오르기): 가로대는 세상에 고정 → 몸이 오른 만큼(climbS) 내려간다. 손발이 이 줄에 맞는지 본다
    const G = r.rungGap || r.M.rungGap, base = r.rungBase || 0, S0 = r.climbS || 0;
    const sx = r.M.ladderHalfW;
    const left = m.point(-sx, 0).x, right = m.point(sx, 0).x;
    c.strokeStyle = "rgba(140,100,60,.75)"; c.lineWidth = Math.max(2, m.a * 0.05);
    c.beginPath(); c.moveTo(left, 0); c.lineTo(left, c.canvas.height); c.moveTo(right, 0); c.lineTo(right, c.canvas.height); c.stroke();
    const k0 = Math.floor((S0 - base) / G) - 2;
    for (let k = k0; k < k0 + 40; k++) { const y = m.point(0, base + k * G - S0).y; if (y < -20 || y > c.canvas.height + 20) continue; c.beginPath(); c.moveTo(left, y); c.lineTo(right, y); c.stroke(); }
    return;
  }
  if (S.motion === "climb" || S.motion === "climbDown") {
    // 줄기 (옆모습 오르기): 앞(+x)에 세로 줄기, 껍질 무늬가 몸이 오르는 만큼 내려간다
    const G = r.M.trunkGap, S0 = r.climbS || 0;
    const x0 = m.point(V2(r.lv.shoulderL).x + r.M.trunkReach + 0.02, 0).x, x1 = m.point(V2(r.lv.shoulderL).x + r.M.trunkReach + 0.9, 0).x;
    c.fillStyle = "rgba(150,110,75,.55)"; c.fillRect(Math.min(x0, x1), 0, Math.abs(x1 - x0), c.canvas.height);
    c.strokeStyle = "rgba(100,70,45,.6)"; c.lineWidth = 2;
    const k0 = Math.floor(S0 / G) - 2;
    for (let k = k0; k < k0 + 40; k++) { const y = m.point(0, k * G - S0).y; if (y < -20 || y > c.canvas.height + 20) continue; c.beginPath(); c.moveTo(x0, y); c.lineTo(x0 + (x1 - x0) * 0.35, y - 6); c.stroke(); }
    return;
  }
  c.fillStyle = "rgba(120,100,80,.6)";
  const step = 0.5;
  const off = ((travel % step) + step) % step;
  for (let x = -20; x <= 20; x += step) {
    const p = m.point(x - off, 0);
    c.fillRect(p.x - 1, p.y, 2, 6 * (m.d < 0 ? 1 : 1));
  }
}

function drawFinger(v) {
  const l = S.look, ct = l.capTop;
  const p = v.point(ct[0], ct[1]);
  const dpr = window.devicePixelRatio || 1;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.fillStyle = "rgba(230,190,160,.9)"; ctx.strokeStyle = "rgba(120,80,60,.8)";
  ctx.beginPath(); ctx.ellipse(p.x, p.y - 14, 9, 16, 0, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
}

// ───── 관절·부품 손잡이 ─────
function handles(v) {
  const out = [];
  if ($("chkBones").checked) {
    const J = rig.Joints();
    for (const j of J) out.push({ kind: "joint", key: j.key, at: v.point(j.at.x, j.at.y), parent: j.parent, world: j.at });
  }
  if ($("chkParts").checked) {
    const P = (name, sr) => { if (!sr || !sr.sprite || !sr.transform.activeInHierarchy) return; const q = sr.transform.localToWorld.point(0, 0); out.push({ kind: "part", key: name, at: v.point(q.x, q.y), sr }); };
    if (rig.headed) {
      P("chest", rig.chestSR); P("skirt", rig.skirtSR); P("collar", rig.collarSR); P("head", rig.headSR); P("hair", rig.hairSkin ? rig.hairSkinSR : rig.hairSR);
      P("arm", rig.upperSR[0]); P("sleeve", rig.sleeveSR[0]); P("leg", rig.legSR[0]);
    } else { P("cap", rig.capSR); P("stem", rig.stemSR); P("footL", rig.shoeSR[0]); P("footR", rig.shoeSR[1]); if (rig.nubSR) { P("nubL", rig.nubSR[0]); P("nubR", rig.nubSR[1]); } }
  }
  return out;
}

function drawHandles(v) {
  const H = handles(v);
  const byKey = Object.fromEntries(H.filter((h) => h.kind === "joint").map((h) => [h.key, h]));
  ctx.lineWidth = 2;
  for (const h of H) {
    if (h.kind !== "joint" || !h.parent || !byKey[h.parent]) continue;
    const p = byKey[h.parent].at;
    ctx.strokeStyle = "rgba(60,120,200,.55)";
    ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(h.at.x, h.at.y); ctx.stroke();
  }
  for (const h of H) {
    const on = S.hover === h.key || (S.drag && S.drag.key === h.key);
    if (h.kind === "joint") {
      ctx.fillStyle = on ? "#ff6a3d" : h.key.endsWith("R") ? "rgba(90,170,90,.9)" : "rgba(40,110,220,.9)";
      ctx.beginPath(); ctx.arc(h.at.x, h.at.y, on ? 6 : 4.5, 0, Math.PI * 2); ctx.fill();
      ctx.strokeStyle = "#fff"; ctx.lineWidth = 1.2; ctx.stroke();
    } else {
      ctx.fillStyle = on ? "#ff6a3d" : "rgba(200,120,40,.85)";
      ctx.fillRect(h.at.x - 5, h.at.y - 5, 10, 10);
    }
    if (on) { ctx.fillStyle = "#222"; ctx.font = "12px system-ui"; ctx.fillText(h.key, h.at.x + 8, h.at.y - 8); }
  }
}

function pick(x, y) {
  const w = canvas.clientWidth, h = canvas.clientHeight;
  const charX = $("chkTravel").checked && S.motion !== "held" ? wrapTravel(w) : S.charX;
  const H = handles(viewMatrix(w, h, S.zoom, charX));
  let best = null, bd = 10;
  for (const hd of H) { const d = Math.hypot(hd.at.x - x, hd.at.y - y); if (d < bd) { bd = d; best = hd; } }
  return best;
}

// 화면 이동량 → 그 노드의 부모 공간 이동량
function screenToLocal(dx, dy, t) {
  const v = viewMatrix(1, 1);
  const lin = (m) => new M2(m.a, m.b, m.c, m.d, 0, 0);
  const world = lin(v).inverse().point(dx, dy);
  const par = t.parent ? lin(t.parent.localToWorld) : new M2();
  return par.inverse().point(world.x, world.y);
}

const R4 = (v) => Math.round(v * 1e4) / 1e4;
const V2 = (a) => new Vector2(a ? a[0] : 0, a ? a[1] : 0);
// 고칠 대상: 정면이면 모습 자체, 옆·뒤면 look.side/back (없는 칸은 정면 값을 복사해 와서 고친다)
function editTarget() {
  const k = rig.viewKey || "front";
  if (k === "front" || !S.look[k]) return S.look;
  const sub = S.look[k];
  return new Proxy(sub, { get(t, key) { if (!(key in t) && key in S.look) t[key] = RJ.clone(S.look[key]); return t[key]; } });
}
function addArr(a, dx, dy) { if (!a) return; a[0] = R4(a[0] + dx); a[1] = R4(a[1] + dy); }
function addPart(p, dx, dy) { if (!p) return; p.x = R4(p.x + dx); p.y = R4(p.y + dy); }
function addBones(l, from, d) { if (l.hairBones) for (let i = from; i < l.hairBones.length; i++) addArr(l.hairBones[i], d.x, d.y); }

// 손잡이 → 고칠 값. d = 부모 공간 이동량 (팔·다리 오른쪽은 뒤집힌 공간이라 왼쪽 값과 같은 뜻)
function applyDrag(h, d, shift) {
  const l = editTarget(), mir = $("chkMirror").checked && rig.viewKey !== "side";
  const k = h.key;
  if (h.kind === "part") {
    const parts = { chest: ["chest"], skirt: ["skirt"], collar: ["collar"], head: ["head"], hair: ["hair"], arm: ["upperArm", "foreArm", "hand"], sleeve: ["sleeve"], leg: ["leg", "shoe"],
      cap: ["cap"], stem: ["stem"], footL: ["footL"], footR: ["footR"], nubL: ["nubL"], nubR: ["nubR"] }[k];
    for (const p of parts) addPart(l[p], d.x, d.y);
    if (k === "hair") { addArr(l.hairRoot, d.x, d.y); addBones(l, 0, d); }   // 머리카락 뼈는 그림과 같이
    if (k === "arm" && shift) { addArr(l.elbow, d.x, d.y); addArr(l.wrist, d.x, d.y); }
    if (k === "leg" && shift) addArr(l.ankle, d.x, d.y);
    return;
  }
  const side = k.endsWith("R") && rig.viewKey !== "side" ? -1 : 1;
  switch (k) {
    case "waist": addArr(l.waist, d.x, d.y); break;
    case "neck":
      addArr(l.neck, d.x, d.y);
      if (shift) { addPart(l.head, d.x, d.y); addPart(l.hair, d.x, d.y); addArr(l.hairRoot, d.x, d.y); addBones(l, 0, d); addArr(l.capTop, d.x, d.y); }
      break;
    // 머리카락 뿌리 = 뼈 0. 뼈 사슬이 있으면 hairBones[0]도 같이 (Shift = 그림·아래 뼈까지)
    case "hairRoot": addArr(l.hairRoot, d.x, d.y); if (l.hairBones) addArr(l.hairBones[0], d.x, d.y); if (shift) { addPart(l.hair, d.x, d.y); addBones(l, 1, d); } break;
    case "hairB1": case "hairB2": case "hairB3": case "hairB4": case "hairB5": {   // 머리카락 뼈 (그림은 그대로, Shift = 아래 뼈까지)
      const i = Number(k.slice(5)); if (l.hairBones) addArr(l.hairBones[i], d.x, d.y); if (shift) addBones(l, i + 1, d); break;
    }
    case "capTop": addArr(l.capTop, d.x, d.y); break;
    case "capJoint": addArr(l.capJoint, d.x, d.y); if (shift) addPart(l.cap, d.x, d.y); break;
    case "shoulderL": case "shoulderR": {
      addArr(l[k], d.x, d.y);
      if (mir) addArr(l[k === "shoulderL" ? "shoulderR" : "shoulderL"], -d.x, d.y);
      if (shift) { const ax = d.x * side; ["upperArm", "foreArm", "hand"].forEach((p) => addPart(l[p], ax, d.y)); addArr(l.elbow, ax, d.y); addArr(l.wrist, ax, d.y); }
      break;
    }
    case "elbowL": case "elbowR": addArr(l.elbow, d.x, d.y); if (shift) { addPart(l.foreArm, d.x, d.y); addPart(l.hand, d.x, d.y); addArr(l.wrist, d.x, d.y); } break;
    case "wristL": case "wristR": addArr(l.wrist, d.x, d.y); if (shift) addPart(l.hand, d.x, d.y); break;
    case "hipL": case "hipR": {
      addArr(l[k], d.x, d.y);
      if (mir) addArr(l[k === "hipL" ? "hipR" : "hipL"], -d.x, d.y);
      if (shift) { const ax = d.x * side; addPart(l.leg, ax, d.y); addPart(l.shoe, ax, d.y); addArr(l.ankle, ax, d.y); }
      break;
    }
    case "kneeL": case "kneeR": addArr(l.knee, d.x * side, d.y); break;
    case "nubPivotL": case "nubPivotR": addArr(l[k], d.x, d.y); if (shift) addPart(l[k === "nubPivotL" ? "nubL" : "nubR"], d.x, d.y); break;
    case "ankleL": case "ankleR":
      if (rig.headed) { addArr(l.ankle, d.x * side, d.y); if (shift) addPart(l.shoe, d.x * side, d.y); }
      else { addArr(l[k === "ankleL" ? "ankle" : "ankleR"], d.x, d.y); if (shift) addPart(l[k === "ankleL" ? "footL" : "footR"], d.x, d.y); }
      break;
  }
}

// 손잡이의 부모 공간 노드
function dragNode(h) {
  if (h.kind === "part") return h.sr.transform;
  const k = h.key;
  const map = rig.headed ? {
    waist: rig.hips, neck: rig.neck, hairRoot: rig.hairT, hairB1: rig.hairBone[1], hairB2: rig.hairBone[2], hairB3: rig.hairBone[3], hairB4: rig.hairBone[4], hairB5: rig.hairBone[5], capTop: rig.bounce, shoulderL: rig.shoulder[0], shoulderR: rig.shoulder[1],
    elbowL: rig.elbow[0], elbowR: rig.elbow[1], wristL: rig.wrist[0], wristR: rig.wrist[1], hipL: rig.hip[0], hipR: rig.hip[1],
    ankleL: rig.root.children[0], ankleR: rig.root.children[0], kneeL: rig.root.children[0], kneeR: rig.root.children[0],
  } : { capJoint: rig.capT, capTop: rig.capT, ankleL: rig.root.children[0], ankleR: rig.root.children[0],
    nubPivotL: rig.nubPivot && rig.nubPivot[0], nubPivotR: rig.nubPivot && rig.nubPivot[1] };
  return map[k];
}

canvas.addEventListener("pointerdown", (e) => {
  canvas.setPointerCapture(e.pointerId);
  const r = canvas.getBoundingClientRect(), x = e.clientX - r.left, y = e.clientY - r.top;
  const h = e.button === 0 ? pick(x, y) : null;
  S.drag = h ? { ...h, x, y, node: dragNode(h) } : { pan: true, x, y, held: S.motion === "held" && e.button === 0 };
});
canvas.addEventListener("pointermove", (e) => {
  const r = canvas.getBoundingClientRect(), x = e.clientX - r.left, y = e.clientY - r.top;
  if (!S.drag) { const h = pick(x, y); S.hover = h ? h.key : null; $("hover").textContent = h ? `${h.key} (${h.world ? h.world.x.toFixed(3) + ", " + h.world.y.toFixed(3) : ""})` : ""; return; }
  const dx = x - S.drag.x, dy = y - S.drag.y;
  S.drag.x = x; S.drag.y = y;
  if (S.drag.pan) {
    if (S.drag.held) S.charX += dx / S.zoom; else { S.pan.x += dx; S.pan.y += dy; }
    return;
  }
  const node = S.drag.node;
  // 관절 손잡이: 그 관절 노드의 부모 공간 · 부품: 그림 노드의 부모 공간 · 발목: root 안쪽 공간
  let d;
  if (S.drag.key.startsWith("ankle") || S.drag.key.startsWith("knee")) {
    const v = viewMatrix(1, 1); const lin = (m) => new M2(m.a, m.b, m.c, m.d, 0, 0);
    const w = lin(v).inverse().point(dx, dy); d = lin(rig.root.localToWorld).inverse().point(w.x, w.y);
  } else d = screenToLocal(dx, dy, node);
  applyDrag(S.drag, d, e.shiftKey);
  rig.Reapply();
  markDirty();
});
canvas.addEventListener("pointerup", () => { if (S.drag && !S.drag.pan) buildLookTab(); S.drag = null; });
canvas.addEventListener("wheel", (e) => {
  e.preventDefault();
  S.zoom = Mathf.Clamp(S.zoom * (e.deltaY < 0 ? 1.1 : 1 / 1.1), 10, 600);
  $("rngZoom").value = S.zoom; $("txtZoom").textContent = Math.round(S.zoom);
}, { passive: false });

// ───── 모두 보기 ─────
function buildGrid(on) {
  if (!on) { S.grid = null; return; }
  S.grid = S.doc.looks.map((l) => { const r = makeRig(l); r.M = motionConsts(r); r.Apply(l, S.outfit); r.motion = S.motion; r.flip = rig.flip; r.scale = l.gradeScale || 1; return { look: l, rig: r }; });   // 모두 보기: 같은 등급 키 맞춤(gradeScale)으로
}
function drawGrid(w, h, dpr) {
  const n = S.grid.length, cols = Math.ceil(Math.sqrt(n * w / h)), rows = Math.ceil(n / cols);
  const cw = w / cols, ch = h / rows;
  S.grid.forEach((g, i) => {
    const cx = (i % cols) * cw, cy = Math.floor(i / cols) * ch;
    const z = ch * 0.78 / 6.2;
    const m = new M2(dpr, 0, 0, dpr, 0, 0).mul(new M2(z, 0, 0, -z, cx + cw / 2, cy + ch * 0.9));
    renderRig(ctx, g.rig, m);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = "rgba(0,0,0,.45)"; ctx.font = "11px system-ui"; ctx.fillText(g.look.id, cx + 4, cy + 12);
  });
}

// ───── 오른쪽 탭: 부위·관절 숫자 ─────
function changed(path) { return RJ.diff(getPath(S.orig, path), getPath(S.doc, path)).length > 0; }
function getPath(o, path) { return path.reduce((x, k) => (x == null ? x : x[k]), o); }
function lookIndex() { return S.doc.looks.indexOf(S.look); }

function buildLookTab() {
  const vk = rig.viewKey || "front", li = lookIndex(), el = $("tab-look");
  const l = vk === "front" ? S.look : S.look[vk] || S.look;
  const basePath = vk === "front" ? ["looks", li] : ["looks", li, vk];
  const rows = [];
  const num = (path, v) => `<input type="number" step="0.005" data-path='${JSON.stringify(path)}' value="${v}">`;
  for (const k of Object.keys(l)) {
    const v = l[k], path = [...basePath, k];
    const cls = changed(path) ? "changed" : "";
    if (Array.isArray(v) && v.length === 2 && typeof v[0] === "number") rows.push(`<tr class="${cls}"><td class="k">${k}</td><td>${num([...path, 0], v[0])}</td><td>${num([...path, 1], v[1])}</td></tr>`);
    else if (v && typeof v === "object" && "sprite" in v) rows.push(`<tr class="${cls}"><td class="k">${k}</td><td>${num([...path, "x"], v.x)}</td><td>${num([...path, "y"], v.y)}</td></tr><tr><td></td><td colspan="2"><input type="text" data-path='${JSON.stringify([...path, "sprite"])}' value="${v.sprite}"></td></tr>`);
    else if (typeof v === "number" && k !== "form") rows.push(`<tr class="${cls}"><td class="k">${k}</td><td colspan="2">${num(path, v)}</td></tr>`);
    else if (typeof v === "string" && k === "face") rows.push(`<tr><td class="k">${k}</td><td colspan="2"><input type="text" data-path='${JSON.stringify(path)}' value="${v}"></td></tr>`);
  }
  el.innerHTML = `<div class="small"><b>${S.look.id} · ${{ front: "정면", side: "옆모습", back: "뒷모습" }[vk]}</b> 칸 (옆·뒤는 정면과 다른 칸만, 손잡이로 고치면 생긴다) · 리그 단위 (그림 px/100, 원점 = 발바닥 가운데). 부품 x,y = 서 있을 때 그림 중심, 팔·다리는 왼쪽 기준.</div><table>${rows.join("")}</table>`;
  el.querySelectorAll("input").forEach((inp) => inp.addEventListener("change", () => {
    const path = JSON.parse(inp.dataset.path), parent = getPath(S.doc, path.slice(0, -1)), key = path[path.length - 1];
    parent[key] = inp.type === "number" ? Number(inp.value) : inp.value;
    rig.Reapply(); markDirty(); buildLookTab();
  }));
}

// ───── 동작 상수 탭 ─────
const MOTION_GROUPS = [
  ["돌아서기·방향", ["turnTime", "turnPinch", "turnPinchW", "maxResYaw", "armSwingSide"]],
  ["오르기 (사다리 뒤 · 줄기 옆)", ["rungGap", "ladderSpeed", "ladderReachUp", "ladderArmStretch", "ladderHalfW", "trunkGap", "trunkSpeed", "trunkReach"]],
  ["섞기·스프링", ["blend", "armK", "armZ", "elbowK", "elbowZ", "bodyK", "bodyZ", "headK", "headZ", "squashK", "squashZ", "headStab", "sleeveFollow"]],
  ["2차 움직임 (진자)", ["hairK", "hairZ", "hairInertia", "hairGravity", "hairDrag", "hairStretch", "hairSegK", "hairSegZ", "hairSegInertia", "hairSegMax", "hairSegRootMax", "hairSkinHeadBand", "hairSkinTail", "hairSplitUp", "skirtK", "skirtZ", "skirtInertia", "skirtGravity", "skirtFlare", "handK", "handZ", "handInertia", "capK", "capZ", "capInertia", "capSquash"]],
  ["고개 돌림", ["headTurnShift", "headTurnSquash", "headTurnTilt", "faceShift", "hairParallax"]],
  ["대기", ["armRest", "elbowRest", "breathPeriod", "breathAmt", "shiftPeriod", "shiftX", "shiftTilt", "idleHead", "fidgetMin", "fidgetMax"]],
  ["걷기·나르기", ["walkSpeed", "strideMax", "cadenceMin", "cadenceMax", "stance", "liftH", "walkDip", "walkSway", "walkTilt", "walkCounter", "walkLean", "walkTurn", "walkYaw", "depthK", "footIn", "toeOff", "toeUp", "heelLift", "kneeSoft", "liftPeak", "kneeOut", "armSwing", "armBendFwd", "armFore", "carrySpeed", "carryLean", "carryDip", "carryRaise", "carryBend"]],
  ["일하기", ["workPeriod", "workReach", "workRise"]],
  ["뛰기·착지", ["jumpH", "jumpCrouch", "surpriseH", "hopH", "hopPeriod", "landDip", "landSquash"]],
  ["춤·하트·잠·들림·오르기", ["danceBeat", "danceSway", "heartSway", "sleepDroop", "heldKick", "heldFlail", "hangK", "hangZ", "climbSpeed", "climbReach", "climbStep"]],
];
function motionGroups() {
  if (isMush()) return [["버섯 리그 (★0·★1) · JSON mushMotion", Object.keys(MUSH_DEFAULTS)]];
  const listed = new Set(MOTION_GROUPS.flatMap((g) => g[1]));
  const extra = Object.keys(MOTION_DEFAULTS).filter((k) => !listed.has(k));
  return extra.length ? [...MOTION_GROUPS, ["기타", extra]] : MOTION_GROUPS;
}
function buildMotionTab() {
  const M = motionConsts(), el = $("tab-motion"), DEF = constsOf().defaults;
  const groups = motionGroups();
  let h = `<div class="small">값을 바꾸면 바로 재생에 반영. 저장하면 JSON "motion"에 들어가 Unity가 읽는다.</div><button id="btnMotionReset">기본값으로</button>`;
  for (const [name, keys] of groups) {
    h += `<div class="grp">${name}</div><table>`;
    for (const k of keys) {
      const d = DEF[k], v = M[k], max = d === 0 ? 1 : Math.abs(d) * 3, step = Math.abs(d) >= 20 ? 1 : Math.abs(d) >= 2 ? 0.05 : 0.005;
      h += `<tr class="${v !== d ? "changed" : ""}"><td class="k">${k}</td><td><input type="range" data-k="${k}" min="${d < 0 ? -max : 0}" max="${max}" step="${step}" value="${v}"></td><td><input type="number" data-k="${k}" step="${step}" value="${v}"></td></tr>`;
    }
    h += `</table>`;
  }
  el.innerHTML = h;
  el.querySelectorAll("input").forEach((inp) => inp.addEventListener("input", () => {
    setMotion(inp.dataset.k, Number(inp.value));
    el.querySelectorAll(`input[data-k="${inp.dataset.k}"]`).forEach((o) => { if (o !== inp) o.value = inp.value; });
  }));
  el.querySelectorAll("input").forEach((inp) => inp.addEventListener("change", () => buildMotionTab()));
  $("btnMotionReset").onclick = () => { const key = constsOf().key; if (S.orig[key]) S.doc[key] = RJ.clone(S.orig[key]); else delete S.doc[key]; rig.M = motionConsts(); buildMotionTab(); markDirty(); };
}
function setMotion(k, v) {
  const c = constsOf();
  if (!S.doc[c.key]) { S.doc[c.key] = {}; for (const kk of Object.keys(c.defaults)) S.doc[c.key][kk] = c.defaults[kk]; }
  S.doc[c.key][k] = v;
  rig.M = motionConsts();
  if (S.grid) S.grid.forEach((g) => (g.rig.M = motionConsts(g.rig)));
  markDirty(); refreshCs();
}

// ───── 수동 포즈 탭 ─────
const POSE_RANGE = { air: [0, 2], sqX: [0.6, 1.4], sqY: [0.6, 1.4], hipX: [-0.5, 0.5], hipY: [-0.5, 0.5], hipRot: [-30, 30], chestRot: [-30, 30], breath: [-2, 2], headRot: [-40, 40], headTurn: [-1, 1],
  raiseL: [-40, 180], bendL: [-30, 150], wristL: [-60, 60], raiseR: [-40, 180], bendR: [-30, 150], wristR: [-60, 60], foreL: [0.5, 1.2], foreR: [0.5, 1.2],
  footLX: [-0.6, 0.6], footLY: [-0.3, 0.6], footLRot: [-40, 40], footRX: [-0.6, 0.6], footRY: [-0.3, 0.6], footRRot: [-40, 40], spin: [0, 1], capRot: [-30, 30],
  footLZ: [-0.6, 0.6], footRZ: [-0.6, 0.6], yaw: [0, 180], swingL: [-60, 60], swingR: [-60, 60], frontL: [-1, 1], frontR: [-1, 1],
  sqY: [0.5, 1.6], lean: [-30, 30], shiftX: [-0.4, 0.4], capY: [-0.3, 0.3], nubLX: [-0.6, 0.6], nubLY: [-0.3, 1.5], nubLRot: [-120, 120], nubRX: [-0.6, 0.6], nubRY: [-0.3, 1.5], nubRRot: [-120, 120], prop: [0, 1] };
const poseKeys = () => (isMush() ? MUSH_POSE_KEYS : POSE_KEYS);
const restPose = () => (isMush() ? MushRestPose() : RestPose(rig.M));
function buildPoseTab() {
  const el = $("tab-pose");
  const p = rig.manual || restPose();
  let h = `<div class="small">슬라이더를 움직이면 "(수동 포즈)" 동작으로 바뀐다. 스프링·2차 움직임은 그대로 돈다 (흔들림 확인).</div><div class="row"><button id="btnPoseRest">쉬는 자세</button><button id="btnPoseGrab">지금 자세 가져오기</button></div><table>`;
  for (const k of poseKeys()) {
    const [a, b] = POSE_RANGE[k] || [-1, 1];
    h += `<tr><td class="k">${k}</td><td><input type="range" data-k="${k}" min="${a}" max="${b}" step="${(b - a) / 400}" value="${p[k]}"></td><td><input type="number" data-k="${k}" step="${(b - a) / 400}" value="${(+p[k]).toFixed(3)}"></td></tr>`;
  }
  h += `</table>`;
  el.innerHTML = h;
  el.querySelectorAll("input").forEach((inp) => inp.addEventListener("input", () => {
    if (!rig.manual) rig.manual = { ...restPose() };
    rig.manual[inp.dataset.k] = Number(inp.value);
    el.querySelectorAll(`input[data-k="${inp.dataset.k}"]`).forEach((o) => { if (o !== inp) o.value = inp.value; });
    if (S.motion !== "pose") play("pose");
  }));
  $("btnPoseRest").onclick = () => { rig.manual = restPose(); buildPoseTab(); play("pose"); };
  $("btnPoseGrab").onclick = () => { rig.manual = { ...rig.pose }; buildPoseTab(); play("pose"); };
}

// ───── 바뀐 것 · C# ─────
function refreshDiff() {
  const d = RJ.diff(S.orig, S.doc);
  const f = (v) => (v === undefined ? "∅" : typeof v === "object" ? JSON.stringify(v).slice(0, 60) : JSON.stringify(v));
  $("tab-diff").innerHTML = `<div class="small">처음 불러온 파일과 다른 곳 ${d.length}개</div><button id="btnRevertAll">전부 되돌리기</button><div class="diff">${d.map((x) => `<div>${x.path}: <span class="from">${f(x.from)}</span> → <span class="to">${f(x.to)}</span></div>`).join("")}</div>`;
  $("btnRevertAll").onclick = () => { S.doc = RJ.clone(S.orig); rig.M = motionConsts(); setLookById(S.look.id); buildMotionTab(); markDirty(); };
  return d.length;
}
function csCode(onlyChanged) {
  const M = motionConsts(), DEF = constsOf().defaults, cls = constsOf().cls;
  const f = (v) => (Number.isInteger(v) ? String(v) : String(Math.round(v * 1e4) / 1e4) + "f");
  const lines = [];
  for (const [name, keys] of motionGroups()) {
    const ks = keys.filter((k) => !onlyChanged || M[k] !== DEF[k]);
    if (!ks.length) continue;
    lines.push(`        // ${name}`);
    lines.push("        public float " + ks.map((k) => `${k} = ${f(M[k])}`).join(", ") + ";");
  }
  if (onlyChanged) return lines.join("\n");
  return `    // ${cls === "MushMotion" ? "버섯 리그(★0·★1)" : "리그2(★2·★3)"} 동작 상수 (Tools/rig-web에서 맞춘 값). spirit_rig2.json "${constsOf().key}"가 있으면 그 값을 쓴다
    [System.Serializable] public class ${cls}
    {
${lines.join("\n")}
    }`;
}
function refreshCs() { $("csOut").textContent = csCode(false); }

function markDirty() { S.dirty = true; const n = refreshDiff(); status(n ? `바뀐 곳 ${n}개 (저장 전)` : "바뀐 것 없음"); }
function status(t) { $("status").textContent = t; }
function setLookById(id) { const [k, f] = id.split("_"); $("selKind").value = k; fillForms(Number(f)); }

// ───── 저장 · 다운로드 · 스크린샷 ─────
function docText() { return RJ.stringify(S.doc, S.nl); }
async function save() {
  const text = docText();
  const r = await fetch("/api/save?file=spirit_rig2", { method: "POST", body: text }).then((x) => x.json());
  if (r.error) return status("저장 실패: " + r.error);
  status(r.same ? "저장: 파일과 같음 (바뀐 것 없음)" : `저장함 → ${r.file}  (백업 ${r.backup || "없음"})`);
  S.origText = text; S.orig = RJ.clone(S.doc); refreshDiff(); buildLookTab(); buildMotionTab();
  return r;
}
function download() {
  const a = document.createElement("a");
  a.href = URL.createObjectURL(new Blob([docText()], { type: "application/json" }));
  a.download = "spirit_rig2.json"; a.click();
}
async function postShot(name, cv = canvas) {
  return fetch("/api/shot?name=" + encodeURIComponent(name), { method: "POST", body: cv.toDataURL("image/png") }).then((r) => r.json());
}

// 관절·뼈 오버레이를 다른 캔버스에 (프레임 띠 확인용)
function drawJointsOn(c, m) {
  const J = rig.Joints(), at = Object.fromEntries(J.map((j) => [j.key, m.point(j.at.x, j.at.y)]));
  c.lineWidth = 2; c.strokeStyle = "rgba(40,110,220,.75)";
  for (const j of J) if (j.parent && at[j.parent]) { c.beginPath(); c.moveTo(at[j.parent].x, at[j.parent].y); c.lineTo(at[j.key].x, at[j.key].y); c.stroke(); }
  for (const j of J) { const p = at[j.key]; c.fillStyle = j.key.endsWith("R") ? "#3a9a3a" : j.key.endsWith("L") ? "#2a6ee0" : "#e05a2a"; c.beginPath(); c.arc(p.x, p.y, 3.5, 0, Math.PI * 2); c.fill(); }
}

// 프레임 띠: 동작을 처음부터 돌려 dt마다 한 칸씩 (확인용 증거)
async function strip(name, motion, frames = 8, dt = 0.1, opts = {}) {
  const cellH = opts.cellH || 300, cellW = opts.cellW || 220, z = cellH * 0.72 / ((S.look.height || 5.5) * 1.05);
  const cv = document.createElement("canvas"); cv.width = cellW * frames; cv.height = cellH + 18;
  const c = cv.getContext("2d");
  c.fillStyle = "#f4efe6"; c.fillRect(0, 0, cv.width, cv.height);
  const wasPlaying = S.playing; S.playing = false;
  S.grabFn = opts.grab || null;
  play(motion);
  const t0 = opts.t0 || 0;
  for (let k = 0; k < frames; k++) {
    seek(t0 + k * dt);
    const m = new M2(z, 0, 0, -z, k * cellW + cellW / 2, cellH * 0.92);
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.strokeStyle = "rgba(120,100,80,.5)"; c.beginPath(); c.moveTo(k * cellW + 6, cellH * 0.92); c.lineTo((k + 1) * cellW - 6, cellH * 0.92); c.stroke();
    if (motion === "walk" || motion === "carry") {   // 땅 눈금: 발이 붙어 있는지
      const step = 0.5, off = ((S.travel % step) + step) % step;
      c.fillStyle = "rgba(120,100,80,.7)";
      for (let x = -3; x <= 3; x += step) { const p = m.point(x - off, 0); if (p.x > k * cellW + 4 && p.x < (k + 1) * cellW - 4) c.fillRect(p.x - 1, p.y, 2, 6); }
    }
    if (/^(ladder|climb)/.test(motion)) { c.save(); c.beginPath(); c.rect(k * cellW, 0, cellW, cellH); c.clip(); drawGround(c, m, cellW, rig); c.restore(); }
    renderRig(c, rig, m);
    c.setTransform(1, 0, 0, 1, 0, 0);
    if (opts.bones) drawJointsOn(c, m);
    if (motion === "held") { const ct = S.look.capTop, p = m.point(ct[0], ct[1]); c.fillStyle = "rgba(230,190,160,.95)"; c.beginPath(); c.ellipse(p.x, p.y - 10, 7, 12, 0, 0, Math.PI * 2); c.fill(); }
    c.fillStyle = "#555"; c.font = "12px system-ui"; c.fillText(`${motion} ${(t0 + k * dt).toFixed(2)}s`, k * cellW + 6, cellH + 13);
  }
  await new Promise((r) => setTimeout(r, 50));
  const res = await postShot(name, cv);
  S.playing = wasPlaying; S.grabFn = null;
  return res;
}

// ───── 모션 보기 (편집 패널을 숨기고 캐릭터와 동작 단추만 크게) ─────
const DEMO = [["idle", 3], ["wave", 1.7], ["walk", 3.5], ["idle", 1.5], ["ladder", 4], ["ladderDown", 3], ["idle", 1.2], ["climb", 3.5], ["climbDown", 2.5],
  ["idle", 1], ["carry", 3], ["work", 3.5], ["jump", 0.9], ["surprise", 0.9], ["dance", 3.5], ["heart", 2.8], ["held", 3.5], ["fall", 0.8], ["land", 0.7],
  ["stretch", 1.9], ["yawn", 2.3], ["sleep", 4], ["look", 2.2], ["spin", 0.8], ["idle", 2]];
let viewerSaved = null;
function setViewer(on) {
  S.viewer = on;
  document.body.classList.toggle("viewer", on);
  $("btnViewer").textContent = on ? "✎ 편집 화면" : "▶ 모션 보기";
  if (on) {
    viewerSaved = { bones: $("chkBones").checked, parts: $("chkParts").checked, checker: $("chkChecker").checked };
    $("chkChecker").checked = false;
    S.pan = new Vector2(0, 0);
  } else {
    stopDemo();
    if (viewerSaved) { $("chkBones").checked = viewerSaved.bones; $("chkParts").checked = viewerSaved.parts; $("chkChecker").checked = viewerSaved.checker; }
    S.pan = new Vector2(0, 0);
  }
  S.playing = true; $("btnPlay").textContent = "일시정지";
  setTimeout(() => { const h = canvas.clientHeight || 600; S.zoom = Math.round(h * (on ? 0.66 : 0.62) / (S.look.height * 1.05)); $("rngZoom").value = S.zoom; draw(); }, 30);
  updateCaption();
}
function buildViewerButtons() {
  $("viewerMotions").innerHTML = Object.entries(MOTIONS).filter(([, v]) => !v.hidden).map(([k, v]) => `<button data-m="${k}"><b>${v.ko}</b><span>${descOf(k)}</span></button>`).join("");
  $("viewerMotions").querySelectorAll("button").forEach((b) => (b.onclick = () => { stopDemo(); play(b.dataset.m); if (!S.playing) { S.playing = true; } }));
  $("viewerMotions").querySelectorAll("button").forEach((b) => b.classList.toggle("on", b.dataset.m === S.motion));
}
function updateCaption() {
  const m = MOTIONS[S.motion]; if (!m || !$("viewerCap")) return;
  $("viewerCap").innerHTML = `<b>${m.ko}${isMush() ? " · 버섯 리그" : ""}</b><span>${descOf(S.motion)}${S.demo ? `  ·  데모 ${S.demo.i + 1}/${DEMO.length}` : ""}</span>`;
}
function startDemo() {
  S.demo = { i: 0, t: 0 };
  S.grabFn = (t) => 260 * Math.sin(t * 4.5) * (Mathf.Repeat(t, 2.4) < 0.9 ? 1 : 0);   // 들림: 들고 흔드는 손
  play(DEMO[0][0]); S.playing = true; $("btnDemo").textContent = "■ 데모 멈춤";
}
function stopDemo() { if (!S.demo) return; S.demo = null; S.grabFn = null; $("btnDemo").textContent = "▶ 데모 (동작 차례로 보기)"; updateCaption(); }
function tickDemo(dt) {
  S.demo.t += dt;
  if (S.demo.t >= DEMO[S.demo.i][1]) {
    S.demo.i = (S.demo.i + 1) % DEMO.length; S.demo.t = 0;
    play(DEMO[S.demo.i][0]);
  }
}

// 동작이 바뀌는 순서 띠: seq = [[시각, 동작], ...] (돌아서기 전환 확인용)
async function seqStrip(name, seq, frames, dt, t0 = 0, opts = {}) {
  const cellH = opts.cellH || 300, cellW = opts.cellW || 200, z = cellH * 0.72 / ((S.look.height || 5.5) * 1.05);
  const cv = document.createElement("canvas"); cv.width = cellW * frames; cv.height = cellH + 18;
  const c = cv.getContext("2d");
  c.fillStyle = "#f4efe6"; c.fillRect(0, 0, cv.width, cv.height);
  const wasPlaying = S.playing; S.playing = false;
  S.motion = seq[0][1]; restart(); play(seq[0][1]);
  let t = 0, si = 1;
  for (let k = 0; k < frames; k++) {
    const until = t0 + k * dt;
    while (t < until - 1e-9) { if (si < seq.length && t >= seq[si][0]) { play(seq[si][1]); si++; } stepSim(); t += FIXED; }
    const m = new M2(z, 0, 0, -z, k * cellW + cellW / 2, cellH * 0.92);
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.strokeStyle = "rgba(120,100,80,.5)"; c.beginPath(); c.moveTo(k * cellW + 6, cellH * 0.92); c.lineTo((k + 1) * cellW - 6, cellH * 0.92); c.stroke();
    renderRig(c, rig, m);
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.fillStyle = "#555"; c.font = "11px system-ui"; c.fillText(`${S.motion} ${until.toFixed(2)}s ${rig.viewKey}`, k * cellW + 4, cellH + 13);
  }
  const res = await postShot(name, cv);
  S.playing = wasPlaying;
  return res;
}

window.rigWeb = {
  S, get rig() { return rig; }, play, seek, save, strip, seqStrip, draw, shot: (n) => { draw(); return postShot(n); }, setLook: setLookById, docText,
  setOutfit: (o) => { $("selOutfit").value = o; setLook(); },
  grid: (on) => { $("chkAll").checked = on; buildGrid(on); },
  viewer: setViewer, demo: (on) => (on ? startDemo() : stopDemo()), tickDemo,
  errors: [],
};
window.addEventListener("error", (e) => window.rigWeb.errors.push(String(e.message)));

// ───── UI 묶기 ─────
function buildMotionButtons() {
  $("motions").innerHTML = Object.entries(MOTIONS).map(([k, v]) => `<button data-m="${k}" title="${v.desc || k}">${v.ko}</button>`).join("");
  buildViewerButtons();
  $("motions").querySelectorAll("button").forEach((b) => (b.onclick = () => { play(b.dataset.m); if (!S.playing) seek(0); }));
  play(S.motion);
}
$("selKind").onchange = () => fillForms(Number($("selForm").value));
$("selForm").onchange = setLook;
$("selOutfit").onchange = setLook;
$("selFace").onchange = () => (rig.faceOverride = $("selFace").value || null);
$("chkFlip").onchange = () => { rig.flip = $("chkFlip").checked ? -1 : 1; if (S.grid) S.grid.forEach((g) => (g.rig.flip = rig.flip)); };
$("chkFidget").onchange = () => { rig.autoFidget = $("chkFidget").checked; };
$("selWave").onchange = () => (rig.waveSide = $("selWave").value);
$("btnPoke").onclick = () => { rig.Poke(); if (S.grid) S.grid.forEach((g) => g.rig.Poke()); };
$("btnPlay").onclick = () => { S.playing = !S.playing; $("btnPlay").textContent = S.playing ? "일시정지" : "재생"; };
$("btnStep").onclick = () => { S.playing = false; $("btnPlay").textContent = "재생"; stepSim(); };
$("btnRestart").onclick = () => seek(0);
$("rngSpeed").oninput = () => { S.speed = Number($("rngSpeed").value); $("txtSpeed").textContent = S.speed.toFixed(2) + "×"; };
$("rngTime").oninput = () => { S.playing = false; $("btnPlay").textContent = "재생"; seek(Number($("rngTime").value)); };
$("rngZoom").oninput = () => { S.zoom = Number($("rngZoom").value); $("txtZoom").textContent = S.zoom; };
$("chkAll").onchange = () => buildGrid($("chkAll").checked);
$("btnSave").onclick = save;
$("btnViewer").onclick = () => setViewer(!S.viewer);
$("btnDemo").onclick = () => (S.demo ? stopDemo() : startDemo());
$("chkFaceWalk").onchange = () => { rig.keepFront = $("chkFaceWalk").checked; };
$("rngSpeed2").oninput = () => { S.speed = Number($("rngSpeed2").value); $("rngSpeed").value = S.speed; $("txtSpeed").textContent = S.speed.toFixed(2) + "×"; };
$("btnDownload").onclick = download;
$("btnRevertLook").onclick = () => {
  const i = lookIndex(); S.doc.looks[i] = RJ.clone(S.orig.looks[i]); S.look = S.doc.looks[i]; rig.Apply(S.look, S.outfit); buildLookTab(); markDirty();
};
$("btnShot").onclick = async () => { const r = await postShot("rig2_" + S.look.id + "_" + S.motion); status("스크린샷 → " + r.file); };
$("btnCopyCs").onclick = () => navigator.clipboard.writeText(csCode(false));
$("btnCopyCsChanged").onclick = () => navigator.clipboard.writeText(csCode(true));
document.querySelectorAll("#tabs button").forEach((b) => (b.onclick = () => {
  document.querySelectorAll("#tabs button").forEach((x) => x.classList.toggle("on", x === b));
  document.querySelectorAll(".tab").forEach((t) => t.classList.toggle("on", t.id === "tab-" + b.dataset.tab));
  if (b.dataset.tab === "pose") buildPoseTab();
}));

load().then(() => requestAnimationFrame(frame)).catch((e) => { status("불러오기 실패: " + e.message); console.error(e); });
