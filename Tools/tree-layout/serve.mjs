#!/usr/bin/env node
// 세계수 도면 편집기 서버 (내장 모듈만): node Tools/tree-layout/serve.mjs [포트=5179]
//   GET  /                  → Tools/tree-layout/index.html
//   GET  /<프로젝트 경로>    → 프로젝트 루트의 파일 (나무·장식 그림, tree_layout.json)
//   GET  /api/defs          → C# 정의에서 읽은 값 {width, zoom, grow, mushSlots, beautySlots, climbFrom, join, decos, zones} (tree_defs.py와 같은 정규식)
//   POST /api/save          → 본문 JSON 글자를 Assets/Resources/Rig/tree_layout.json에 저장 (전에 Temp/tree_layout.bak.<시각>.json 백업)
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..", "..");
const PORT = Number(process.argv[2] || process.env.PORT || 5179);
const LAYOUT = path.join(ROOT, "Assets/Resources/Rig/tree_layout.json");
const TYPES = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8", ".png": "image/png", ".txt": "text/plain; charset=utf-8" };

function defs() {
  const farm = fs.readFileSync(path.join(ROOT, "Assets/Scripts/Core/Defs.Farm.cs"), "utf8");
  const core = fs.readFileSync(path.join(ROOT, "Assets/Scripts/Core/Defs.cs"), "utf8");
  const nums = (name) => new RegExp(`\\b${name}\\s*=\\s*\\{([^}]*)\\}`).exec(farm)[1].split(",").map((x) => Number(x.trim().replace(/f$/, "")));
  const join = [...new RegExp("CRITTER_JOIN\\s*=\\s*\\{([^}]*)\\}").exec(core)[1].matchAll(/"(\w+)"/g)].map((m) => m[1]);
  const decos = [...farm.matchAll(/new Deco \{ id = "(\w+)", of = "(\w+)", n = "([^"]*)".*?hang = (true|false).*?art = "(\w+)"/g)]
    .map((m) => ({ id: m[1], of: m[2], n: m[3], hang: m[4] === "true", art: m[5] }));
  const zones = [...farm.matchAll(/new Zone \{ id = "(\w+)", n = "([^"]*)".*?open = (\d+), weight = (\d+)/g)]
    .map((m) => ({ id: m[1], n: m[2], open: Number(m[3]), weight: Number(m[4]) }));
  const climbFrom = Number(/climbFrom\s*=\s*(\d+)/.exec(farm)[1]);
  return { width: nums("width"), zoom: nums("zoom"), grow: nums("grow"), mushSlots: nums("mushSlots"), beautySlots: 2, climbFrom, join, decos, zones };
}

function stamp() {
  const d = new Date(), p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}_${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
}

function body(req) {
  return new Promise((res, rej) => { const c = []; req.on("data", (x) => c.push(x)); req.on("end", () => res(Buffer.concat(c))); req.on("error", rej); });
}

function send(res, code, data, type = "application/json; charset=utf-8") {
  res.writeHead(code, { "Content-Type": type, "Cache-Control": "no-store" });
  res.end(typeof data === "string" || Buffer.isBuffer(data) ? data : JSON.stringify(data));
}

http.createServer(async (req, res) => {
  try {
    const u = new URL(req.url, "http://x");
    if (u.pathname === "/favicon.ico") { res.writeHead(204); return res.end(); }
    if (u.pathname === "/api/defs") return send(res, 200, defs());
    if (u.pathname === "/api/save" && req.method === "POST") {
      const text = (await body(req)).toString("utf8");
      JSON.parse(text);                                                     // 깨진 JSON은 저장하지 않는다
      const bak = path.join(ROOT, "Temp", `tree_layout.bak.${stamp()}.json`);
      fs.mkdirSync(path.dirname(bak), { recursive: true });
      if (fs.existsSync(LAYOUT)) fs.copyFileSync(LAYOUT, bak);
      fs.writeFileSync(LAYOUT, text);
      return send(res, 200, { ok: true, backup: path.relative(ROOT, bak) });
    }
    const rel = u.pathname === "/" ? "Tools/tree-layout/index.html" : decodeURIComponent(u.pathname.slice(1));
    const file = path.resolve(ROOT, rel);
    if (!file.startsWith(ROOT + path.sep) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) return send(res, 404, { error: "not found", rel });
    send(res, 200, fs.readFileSync(file), TYPES[path.extname(file).toLowerCase()] || "application/octet-stream");
  } catch (e) {
    send(res, 500, { error: String(e && e.stack || e) });
  }
}).listen(PORT, "127.0.0.1", () => console.log(`tree-layout http://127.0.0.1:${PORT}/`));
