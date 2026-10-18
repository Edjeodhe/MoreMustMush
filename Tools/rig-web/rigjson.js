// JSON 읽기/쓰기: 파이썬 json.dump(indent=1, ensure_ascii=False)와 같은 모양으로 쓴다 (프로젝트 도구들과 같은 형식).
// 숫자는 읽은 글자를 기억해 두었다가 값이 그대로면 그 글자 그대로 쓴다 → 고치지 않고 저장하면 diff 0.
// 새로 바뀐 숫자는 소수 4자리로 반올림 (원래 정수였으면 정수로, 아니면 파이썬처럼 1.0).
const RAW = Symbol("raw");

export function parse(text) {
  let i = 0;
  const ws = () => { while (i < text.length && " \t\r\n".includes(text[i])) i++; };
  function value() {
    ws();
    const c = text[i];
    if (c === "{") {
      i++; const o = {}; Object.defineProperty(o, RAW, { value: {}, enumerable: false, writable: true });
      ws(); if (text[i] === "}") { i++; return o; }
      for (;;) {
        ws(); const k = str(); ws(); i++; // :
        const st = i; ws(); const s0 = i; const v = value();
        if (typeof v === "number") o[RAW][k] = text.slice(s0, i).trim();
        o[k] = v; ws();
        if (text[i] === ",") { i++; continue; }
        i++; return o;
      }
    }
    if (c === "[") {
      i++; const a = []; Object.defineProperty(a, RAW, { value: [], enumerable: false, writable: true });
      ws(); if (text[i] === "]") { i++; return a; }
      for (;;) {
        ws(); const s0 = i; const v = value();
        if (typeof v === "number") a[RAW][a.length] = text.slice(s0, i).trim();
        a.push(v); ws();
        if (text[i] === ",") { i++; continue; }
        i++; return a;
      }
    }
    if (c === '"') return str();
    if (text.startsWith("true", i)) { i += 4; return true; }
    if (text.startsWith("false", i)) { i += 5; return false; }
    if (text.startsWith("null", i)) { i += 4; return null; }
    const m = /^-?\d+(\.\d+)?([eE][+-]?\d+)?/.exec(text.slice(i, i + 40));
    if (!m) throw new Error("JSON 오류 @" + i);
    i += m[0].length;
    return Number(m[0]);
  }
  function str() {
    const s0 = i; i++;
    while (text[i] !== '"') { if (text[i] === "\\") i++; i++; }
    i++;
    return JSON.parse(text.slice(s0, i));
  }
  const v = value();
  return v;
}

export function fmtNum(v, raw) {
  if (raw !== undefined && Number(raw) === v) return raw;
  let r = Math.round(v * 1e4) / 1e4;
  if (Object.is(r, -0)) r = 0;
  const intStyle = raw !== undefined && !/[.eE]/.test(raw);
  if (Number.isInteger(r)) return intStyle ? String(r) : r.toFixed(1);
  return String(r);
}

function pyStr(s) {
  // ensure_ascii=False: 제어 문자만 이스케이프 (JSON.stringify와 같음, 단 U+2028/2029 그대로)
  return JSON.stringify(s);
}

export function stringify(v, nl = "\n", ind = 1) {
  const pad = (d) => " ".repeat(d * ind);
  function go(x, d, raw) {
    if (x === null) return "null";
    if (typeof x === "number") return fmtNum(x, raw);
    if (typeof x === "boolean") return x ? "true" : "false";
    if (typeof x === "string") return pyStr(x);
    if (Array.isArray(x)) {
      if (!x.length) return "[]";
      const r = x[RAW] || [];
      return "[" + nl + x.map((e, k) => pad(d + 1) + go(e, d + 1, r[k])).join("," + nl) + nl + pad(d) + "]";
    }
    const keys = Object.keys(x);
    if (!keys.length) return "{}";
    const r = x[RAW] || {};
    return "{" + nl + keys.map((k) => pad(d + 1) + pyStr(k) + ": " + go(x[k], d + 1, r[k])).join("," + nl) + nl + pad(d) + "}";
  }
  return go(v, 0, undefined);
}

export function newlineOf(text) { return text.includes("\r\n") ? "\r\n" : "\n"; }

// 깊은 복사 (숫자 원문 기억도 같이)
export function clone(x) {
  if (Array.isArray(x)) { const a = x.map(clone); if (x[RAW]) Object.defineProperty(a, RAW, { value: [...x[RAW]], enumerable: false, writable: true }); return a; }
  if (x && typeof x === "object") { const o = {}; for (const k of Object.keys(x)) o[k] = clone(x[k]); if (x[RAW]) Object.defineProperty(o, RAW, { value: { ...x[RAW] }, enumerable: false, writable: true }); return o; }
  return x;
}

// 바뀐 곳 목록 [{path, from, to}]
export function diff(a, b, path = "") {
  const out = [];
  if (typeof a !== typeof b || Array.isArray(a) !== Array.isArray(b) || a === null || b === null || typeof a !== "object") {
    if (a !== b && !(typeof a === "number" && typeof b === "number" && Math.abs(a - b) < 5e-5)) out.push({ path, from: a, to: b });
    return out;
  }
  const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
  for (const k of keys) {
    const p = Array.isArray(a) ? `${path}[${k}]` : path ? `${path}.${k}` : k;
    if (!(k in a)) out.push({ path: p, from: undefined, to: b[k] });
    else if (!(k in b)) out.push({ path: p, from: a[k], to: undefined });
    else out.push(...diff(a[k], b[k], p));
  }
  return out;
}
