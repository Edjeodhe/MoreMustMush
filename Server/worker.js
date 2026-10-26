// MMM 로그 수집 Worker (Cloudflare Workers + D1). HANDOFF.md 2·3·6절 기준.
//   POST /?k=<MMM_LOG_KEY>   본문: 로그 객체 1개 또는 배열(최대 50개), 전체 20KB 이하
//   응답: 저장 성공 "ok" · 키 불일치 403 "forbidden" · 크기 초과 413 · JSON·형식 오류 400
// 바인딩: D1 → env.MMM_DB, 비밀 키 → env.MMM_LOG_KEY (Settings > Variables and Secrets, 또는 wrangler secret put MMM_LOG_KEY)
// 받을 때는 DB를 읽지 않는 검사만 한다(형식·필수 항목·음수·말이 안 되게 큰 값). 재화 정합성은 분석 때 본다.
// 같은 eid는 INSERT OR IGNORE로 무시한다(클라 재전송 중복).

const MAX_BYTES = 20 * 1024;
const MAX_ITEMS = 50;
const DATA_MAX = 4000;
const EVENTS = new Set(["session_start", "stage_end", "tree_up", "tree_build", "tree_act", "quest_clear", "ftue", "crash", "error", "game_clear"]);
const RESULTS = new Set(["clear", "fail", "early_exit"]);
const UID_RE = /^[0-9a-f]{32}$/;
const BIG = 1e40;          // 재화가 이보다 크면 비정상 (후반 골드는 1e20 이상까지 간다)
const CORS = {
  "Access-Control-Allow-Origin": "*",            // itch.io 웹 빌드는 다른 도메인(iframe)에서 보낸다
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
  "Access-Control-Max-Age": "86400",
};

const reply = (body, status = 200) => new Response(body, { status, headers: { ...CORS, "Content-Type": "text/plain; charset=utf-8" } });

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: CORS });
    if (request.method !== "POST") return reply("method not allowed", 405);

    const url = new URL(request.url);
    if (!env.MMM_LOG_KEY || url.searchParams.get("k") !== env.MMM_LOG_KEY) return reply("forbidden", 403);

    // IP당 요청 제한 (Rate Limiting 바인딩 MMM_LIMIT: 60초에 60번). DB를 쓰지 않고 Cloudflare 위치별 메모리로 센다.
    // 바인딩이 없으면 건너뛴다. 막히면 429 → 클라는 큐에 남겨 두고 나중에 다시 보낸다
    if (env.MMM_LIMIT) {
      const ip = request.headers.get("cf-connecting-ip") || "?";
      const { success } = await env.MMM_LIMIT.limit({ key: ip });
      if (!success) return reply("too many", 429);
    }

    const len = Number(request.headers.get("content-length") || 0);
    if (len > MAX_BYTES) return reply("too large", 413);
    const text = await request.text();
    if (new TextEncoder().encode(text).length > MAX_BYTES) return reply("too large", 413);

    let body;
    try { body = JSON.parse(text); } catch { return reply("bad json", 400); }
    const items = Array.isArray(body) ? body : [body];
    if (items.length === 0 || items.length > MAX_ITEMS) return reply("bad count", 400);

    const now = Date.now();
    const stmts = [];
    let odd = null;   // 이상치: 저장하지 않고 suspect에 uid·사유만 남긴다 (클라에는 ok를 줘서 다시 보내지 않게)
    const uid0 = items[0] && items[0].uid;
    for (const it of items) {
      // 모르는 이벤트(새 클라가 서버보다 먼저 나간 경우)는 그 건만 건너뛴다. 묶음 전체를 400으로 내면 클라가 한 건씩 다시 보내 요청이 50배가 된다
      if (it && typeof it === "object" && typeof it.event === "string" && !EVENTS.has(it.event)) continue;
      const err = check(it);
      if (err) return reply("bad item: " + err, 400);
      const why = it.uid !== uid0 ? "mixed_uid" : weird(it);
      if (why) { odd = odd || { uid: it.uid, why: it.event + ":" + why }; continue; }
      stmts.push(toStatement(env, it, now));
    }
    if (odd) stmts.push(env.MMM_DB.prepare("INSERT OR REPLACE INTO suspect (uid, reason, ts) VALUES (?, ?, ?)").bind(odd.uid, odd.why.slice(0, 64), now));
    if (stmts.length) await env.MMM_DB.batch(stmts);
    return reply("ok");
  },
};

// ===== 입력 검사 (DB를 읽지 않는다) =====
function str(v, max) { return typeof v === "string" && v.length > 0 && v.length <= max; }
function num(v) { return typeof v === "number" && Number.isFinite(v); }
function sane(v) { return v === undefined || (num(v) && v >= 0 && v < BIG); }

function check(it) {
  if (!it || typeof it !== "object" || Array.isArray(it)) return "not object";
  if (!str(it.game, 16)) return "game";
  if (!str(it.event, 32) || !EVENTS.has(it.event)) return "event";
  if (!str(it.uid, 32) || !UID_RE.test(it.uid)) return "uid";
  if (!str(it.eid, 64)) return "eid";
  if (it.version !== undefined && !str(it.version, 32)) return "version";
  if (it.platform !== undefined && !str(it.platform, 16)) return "platform";
  if (it.session !== undefined && !str(it.session, 64)) return "session";
  if (it.t !== undefined && !(num(it.t) && it.t >= 0 && it.t < 1e7)) return "t";
  const d = it.data;
  if (d !== undefined && (d === null || typeof d !== "object" || Array.isArray(d))) return "data";
  if (d) {
    for (const k of ["in", "out", "bal", "score", "out_gem", "gem"]) if (!sane(d[k])) return "data." + k;
    if (d.etc !== undefined && !(num(d.etc) && Math.abs(d.etc) < BIG)) return "data.etc";
  }
  if (it.event === "stage_end") {
    if (!d) return "stage_end data";
    if (!(Number.isInteger(d.stage) && d.stage >= 1 && d.stage < 1e6)) return "data.stage";
    if (!RESULTS.has(d.result)) return "data.result";
    if (typeof d.boss !== "boolean") return "data.boss";
    if (d.boss_hp !== undefined && !(num(d.boss_hp) && d.boss_hp >= 0 && d.boss_hp <= 100)) return "data.boss_hp";
    if (d.sec !== undefined && !(num(d.sec) && d.sec >= 0 && d.sec < 3600)) return "data.sec";
    if (d.up !== undefined && !Array.isArray(d.up)) return "data.up";
    if (d.codex !== undefined && !Array.isArray(d.codex)) return "data.codex";
  }
  return null;
}

// ===== 이상치 (DB를 읽지 않는다) =====
// 형식은 맞지만 정상 클라가 보낼 수 없는 값. 키가 클라에 들어 있어 누구나 보낼 수 있으므로, 지표가 오염되지 않게 저장하지 않는다.
//   한 묶음은 한 기기(uid)에서만 온다 · eid는 "세션-번호" · 묶음 줄(c)과 단계·레벨은 게임 규칙 범위 안
const BOSS_EVERY = 10;
function int(v, lo, hi) { return Number.isInteger(v) && v >= lo && v <= hi; }

function weird(it) {
  if (it.session !== undefined && !it.eid.startsWith(it.session + "-")) return "eid";
  const d = it.data || {};
  if (d.c !== undefined && !int(d.c, 1, d.post ? 100000 : 10000)) return "c";
  if (it.event === "stage_end") {
    if (d.stage0 !== undefined && !d.post && !(int(d.stage0, 1, d.stage) && d.stage - d.stage0 < BOSS_EVERY)) return "stage0";
    if (d.codex !== undefined && d.codex.length > 200) return "codex";
    if (d.up !== undefined && d.up.length > 500) return "up";
  }
  if (it.event === "ftue" && !int(d.step, 1, 5)) return "step";
  if (it.event === "tree_up" && !int(d.lv, 0, 1000000)) return "lv";
  return null;
}

// ===== 저장 =====
function cts(it, now) {
  // 기기 시각은 참고용. 미래이거나 30일보다 오래되면 버린다
  return num(it.ts) && it.ts <= now + 600000 && it.ts >= now - 30 * 86400000 ? Math.round(it.ts) : null;
}

function clip(s) { return s.length > DATA_MAX ? s.slice(0, DATA_MAX) : s; }

function toStatement(env, it, now) {
  const base = [it.eid, now, cts(it, now), it.uid, it.game, it.version ?? null, it.platform ?? null, it.session ?? null];
  if (it.event === "stage_end") {
    const d = { ...it.data };
    const take = (k) => { const v = d[k]; delete d[k]; return v; };
    const stage = take("stage"), result = take("result"), boss = take("boss") ? 1 : 0;
    const score = take("score") ?? null, gain = take("in") ?? null, spent = take("out") ?? null, bal = take("bal") ?? null, etc = take("etc") ?? null;
    const bossHp = take("boss_hp") ?? null, sec = take("sec") ?? null;
    const up = d.up !== undefined ? clip(JSON.stringify(take("up"))) : null;
    const codex = d.codex !== undefined ? clip(JSON.stringify(take("codex"))) : null;
    const rest = Object.keys(d).length ? clip(JSON.stringify(d)) : null;
    return env.MMM_DB.prepare(
      "INSERT OR IGNORE INTO stage_end (eid, ts, cts, uid, game, version, platform, session, t, stage, result, boss, score, gain, spent, bal, etc, boss_hp, sec, up, codex, data) " +
      "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
    ).bind(...base, it.t ?? null, stage, result, boss, score, gain, spent, bal, etc, bossHp, sec, up, codex, rest);
  }
  const data = it.data !== undefined ? clip(JSON.stringify(it.data)) : null;
  return env.MMM_DB.prepare(
    "INSERT OR IGNORE INTO logs (eid, ts, cts, uid, game, version, platform, session, event, t, data) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
  ).bind(...base, it.event, it.t ?? null, data);
}
