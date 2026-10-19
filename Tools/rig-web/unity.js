// Unity 흉내 (리그2 웹 작업장): Mathf · Transform(T·R·S, 2D) · SpriteRenderer · Random.
// rig2.js가 C#과 같은 모양으로 쓰도록 이름을 Unity와 맞췄다. 각도는 도(Z 회전, 반시계 +), y는 위가 +.

export const Mathf = {
  PI: Math.PI,
  Deg2Rad: Math.PI / 180,
  Rad2Deg: 180 / Math.PI,
  Sin: Math.sin, Cos: Math.cos, Abs: Math.abs, Sqrt: Math.sqrt, Exp: Math.exp, Atan2: Math.atan2, Sign: (v) => (v >= 0 ? 1 : -1),
  Max: Math.max, Min: Math.min, Pow: Math.pow, Floor: Math.floor, Round: Math.round,
  Clamp: (v, a, b) => (v < a ? a : v > b ? b : v),
  Clamp01: (v) => (v < 0 ? 0 : v > 1 ? 1 : v),
  Lerp: (a, b, t) => a + (b - a) * (t < 0 ? 0 : t > 1 ? 1 : t),
  LerpUnclamped: (a, b, t) => a + (b - a) * t,
  InverseLerp: (a, b, v) => (a === b ? 0 : Mathf.Clamp01((v - a) / (b - a))),
  SmoothStep: (a, b, t) => { t = Mathf.Clamp01(t); t = t * t * (3 - 2 * t); return a + (b - a) * t; },
  Repeat: (t, len) => t - Math.floor(t / len) * len,
  PingPong: (t, len) => { t = Mathf.Repeat(t, len * 2); return len - Math.abs(t - len); },
  DeltaAngle: (a, b) => { let d = Mathf.Repeat(b - a, 360); if (d > 180) d -= 360; return d; },
};

// 결정적 난수 (스크럽 때 같은 결과): Unity Random.Range / value 와 같은 뜻
export class Rng {
  constructor(seed = 1) { this.s = seed >>> 0 || 1; }
  get value() { let x = this.s; x ^= x << 13; x ^= x >>> 17; x ^= x << 5; this.s = x >>> 0; return this.s / 4294967296; }
  Range(a, b) { return Number.isInteger(a) && Number.isInteger(b) ? a + Math.floor(this.value * (b - a)) : a + this.value * (b - a); }
}

export class Vector2 {
  constructor(x = 0, y = 0) { this.x = x; this.y = y; }
  static of(a) { return a && a.length >= 2 ? new Vector2(a[0], a[1]) : new Vector2(); }
  add(o) { return new Vector2(this.x + o.x, this.y + o.y); }
  sub(o) { return new Vector2(this.x - o.x, this.y - o.y); }
  mul(k) { return new Vector2(this.x * k, this.y * k); }
  get magnitude() { return Math.hypot(this.x, this.y); }
}

// 2D 아핀 행렬 [a c e; b d f]
export class M2 {
  constructor(a = 1, b = 0, c = 0, d = 1, e = 0, f = 0) { this.a = a; this.b = b; this.c = c; this.d = d; this.e = e; this.f = f; }
  mul(m) {
    return new M2(this.a * m.a + this.c * m.b, this.b * m.a + this.d * m.b, this.a * m.c + this.c * m.d, this.b * m.c + this.d * m.d,
      this.a * m.e + this.c * m.f + this.e, this.b * m.e + this.d * m.f + this.f);
  }
  point(x, y) { return new Vector2(this.a * x + this.c * y + this.e, this.b * x + this.d * y + this.f); }
  inverse() {
    const det = this.a * this.d - this.b * this.c || 1e-9;
    const a = this.d / det, b = -this.b / det, c = -this.c / det, d = this.a / det;
    return new M2(a, b, c, d, -(a * this.e + c * this.f), -(b * this.e + d * this.f));
  }
  static TRS(px, py, deg, sx, sy) {
    const r = deg * Mathf.Deg2Rad, cs = Math.cos(r), sn = Math.sin(r);
    // T · R · S (Unity와 같은 순서: 크기 → 회전 → 이동)
    return new M2(cs * sx, sn * sx, -sn * sy, cs * sy, px, py);
  }
  get rotation() { return Math.atan2(this.b, this.a) * Mathf.Rad2Deg; }
  get det() { return this.a * this.d - this.b * this.c; }
}

let creation = 0;
export class Transform {
  constructor(name, parent = null) {
    this.name = name; this.parent = null; this.children = [];
    this.localPosition = new Vector2(); this.localRotation = 0; this.localScale = new Vector2(1, 1);
    this.activeSelf = true; this.renderer = null; this.id = creation++;
    if (parent) this.SetParent(parent);
  }
  SetParent(p) { this.parent = p; p.children.push(this); }
  SetActive(on) { this.activeSelf = on; }
  get activeInHierarchy() { return this.activeSelf && (!this.parent || this.parent.activeInHierarchy); }
  get localMatrix() { return M2.TRS(this.localPosition.x, this.localPosition.y, this.localRotation, this.localScale.x, this.localScale.y); }
  get localToWorld() { return this.parent ? this.parent.localToWorld.mul(this.localMatrix) : this.localMatrix; }
  get position() { const m = this.localToWorld; return new Vector2(m.e, m.f); }
  TransformPoint(p) { return this.localToWorld.point(p.x, p.y); }
  InverseTransformPoint(p) { return this.localToWorld.inverse().point(p.x, p.y); }
  get rotation() { return this.localToWorld.rotation; }
  set(px, py, rot, sx = 1, sy = 1) { this.localPosition.x = px; this.localPosition.y = py; this.localRotation = rot; this.localScale.x = sx; this.localScale.y = sy; }
}

export class SpriteRenderer {
  constructor(name, parent, order) {
    this.transform = new Transform(name, parent); this.transform.renderer = this;
    this.sprite = null; this.sortingOrder = order; this.enabled = true; this.color = 1;
  }
}

// 그리기: 켜진 렌더러를 sortingOrder → 만든 순서로 (Unity는 같은 순서끼리 정해지지 않음)
export function collectRenderers(root) {
  const out = [];
  (function walk(t) {
    if (!t.activeSelf) return;
    if (t.renderer && t.renderer.enabled && t.renderer.sprite) out.push(t.renderer);
    for (const c of t.children) walk(c);
  })(root);
  out.sort((a, b) => a.sortingOrder - b.sortingOrder || a.transform.id - b.transform.id);
  return out;
}

// sprite = {img, w, h, px, py, ppu}: 이미지 픽셀 → 스프라이트 단위 (피벗 원점, y 위)
// 스킨 (뼈로 휘는 그림, Unity SpriteSkin 흉내): sr.skin = { space: Transform, prepare(), at(row, sprite) → space 안 변형 M2 }
//   렌더러는 space의 바로 아래 자식. 그림을 가로 띠(SKIN_STRIP px)로 나눠 띠마다 그 줄의 변형으로 그린다 (1px 겹쳐 틈 없게).
const SKIN_STRIP = 3, SKIN_PAD = 2;
function drawSkinned(ctx, view, sr, s) {
  const sk = sr.skin;
  sk.prepare();
  const pre = view.mul(sk.space.localToWorld), loc = sr.transform.localMatrix;
  const pix = new M2(1 / s.ppu, 0, 0, -1 / s.ppu, -s.px * s.w / s.ppu, (1 - s.py) * s.h / s.ppu);
  ctx.globalAlpha = sr.color;
  // 띠는 위아래로 SKIN_PAD px씩 겹쳐 그린다: 띠가 서로 조금 다르게 돌아도 사이에 가는 틈 선이 안 보이게
  const [lo, hi] = sr.skinRows || [0, s.h];          // 이 줄 범위만 그린다 (같은 그림을 층 나눠 그릴 때)
  for (let r = lo - (lo % SKIN_STRIP); r < hi; r += SKIN_STRIP) {
    const r0 = Math.max(lo, r - SKIN_PAD), r1 = Math.min(hi, r + SKIN_STRIP + SKIN_PAD);
    if (r1 <= r0) continue;
    const m = pre.mul(sk.at(r + SKIN_STRIP / 2, s)).mul(loc).mul(pix);
    ctx.setTransform(m.a, m.b, m.c, m.d, m.e, m.f);
    ctx.drawImage(s.img, 0, r0, s.w, r1 - r0, 0, r0, s.w, r1 - r0);
  }
  ctx.globalAlpha = 1;
}

export function drawSprite(ctx, view, sr) {
  const s = sr.sprite;
  if (!s || !s.img || !s.img.complete) return;
  if (sr.skin) { drawSkinned(ctx, view, sr, s); return; }
  const m = view.mul(sr.transform.localToWorld).mul(new M2(1 / s.ppu, 0, 0, -1 / s.ppu, -s.px * s.w / s.ppu, (1 - s.py) * s.h / s.ppu));
  ctx.setTransform(m.a, m.b, m.c, m.d, m.e, m.f);
  ctx.globalAlpha = sr.color;
  ctx.drawImage(s.img, 0, 0);
  ctx.globalAlpha = 1;
}
