#!/usr/bin/env node
// 정령 리그2 웹 작업장 서버 (내장 모듈만): node Tools/rig-web/serve.mjs [포트=5178]
//   GET  /                         → Tools/rig-web/index.html
//   GET  /<프로젝트 경로>           → 프로젝트 루트의 파일 (그림·JSON)
//   GET  /api/sprites?dirs=Characters/Evo,Characters/Rig2
//                                  → {키: {url, w, h, px, py, ppu}}  (SpriteDB와 같은 키·같은 우선순위, 피벗·PPU는 .meta)
//   POST /api/save?file=spirit_rig2 → 본문 JSON 글자를 Assets/Resources/Rig/<file>.json에 저장 (전에 Temp/<file>.bak.<시각>.json 백업)
//   POST /api/shot?name=rig2_walk  → 본문 PNG(dataURL)를 Logs/Screens/<name>.png로 (확인용 스크린샷)
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..", "..");
const PORT = Number(process.argv[2] || process.env.PORT || 5178);
// MoreMushSetup.ArtRoots: 뒤가 앞을 덮는다
const ART_ROOTS = ["Assets/ArtArchive/Generated", "Assets/ArtArchive/Soft", "Assets/ArtArchive/Doodle", "Assets/Resources"];
const SAVE_OK = new Set(["spirit_rig2"]);
const TYPES = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".mjs": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8", ".png": "image/png", ".jpg": "image/jpeg", ".md": "text/plain; charset=utf-8", ".txt": "text/plain; charset=utf-8" };

function pngSize(file) {
  const fd = fs.openSync(file, "r");
  const b = Buffer.alloc(24); fs.readSync(fd, b, 0, 24, 0); fs.closeSync(fd);
  return [b.readUInt32BE(16), b.readUInt32BE(20)];
}

function readMeta(file) {
  const out = { px: 0.5, py: 0.5, ppu: 100 };
  try {
    const t = fs.readFileSync(file + ".meta", "utf8");
    const al = /^\s*alignment:\s*(\d+)/m.exec(t);
    const pv = /^\s*spritePivot:\s*\{x:\s*([-\d.]+),\s*y:\s*([-\d.]+)\}/m.exec(t);
    const pp = /^\s*spritePixelsToUnits:\s*([\d.]+)/m.exec(t);
    if (pp) out.ppu = Number(pp[1]);
    // alignment: 0 가운데 1 왼위 2 위 3 오른위 4 왼 5 오른 6 왼아래 7 아래 8 오른아래 9 사용자(spritePivot)
    const A = [[0.5, 0.5], [0, 1], [0.5, 1], [1, 1], [0, 0.5], [1, 0.5], [0, 0], [0.5, 0], [1, 0]];
    const a = al ? Number(al[1]) : 0;
    if (a === 9 && pv) { out.px = Number(pv[1]); out.py = Number(pv[2]); } else if (A[a]) [out.px, out.py] = A[a];
  } catch { /* .meta가 없으면 Unity 기본값 */ }
  return out;
}

function sprites(dirs) {
  const map = {};
  for (const r of ART_ROOTS) {
    for (const d of dirs) {
      const abs = path.join(ROOT, r, d);
      if (!fs.existsSync(abs)) continue;
      for (const f of fs.readdirSync(abs)) {
        if (!f.toLowerCase().endsWith(".png")) continue;
        const file = path.join(abs, f);
        const [w, h] = pngSize(file);
        const key = d + "/" + f.slice(0, -4);
        map[key] = { url: "/" + [r, d, f].join("/"), w, h, ...readMeta(file) };
      }
    }
  }
  return map;
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
    if (u.pathname === "/api/sprites") return send(res, 200, sprites((u.searchParams.get("dirs") || "Characters/Evo,Characters/Rig2").split(",")));
    if (u.pathname === "/api/save" && req.method === "POST") {
      const name = u.searchParams.get("file") || "spirit_rig2";
      if (!SAVE_OK.has(name)) return send(res, 400, { error: "저장할 수 없는 파일: " + name });
      const text = (await body(req)).toString("utf8");
      const data = JSON.parse(text);                                   // 깨진 JSON은 쓰지 않는다
      if (!Array.isArray(data.looks)) return send(res, 400, { error: "looks 배열이 없음" });
      const file = path.join(ROOT, "Assets/Resources/Rig", name + ".json");
      let backup = null;
      if (fs.existsSync(file)) {
        const old = fs.readFileSync(file, "utf8");
        if (old === text) return send(res, 200, { ok: true, same: true, file });
        fs.mkdirSync(path.join(ROOT, "Temp"), { recursive: true });
        backup = path.join(ROOT, "Temp", `${name}.bak.${stamp()}.json`);
        fs.writeFileSync(backup, old, "utf8");
      }
      fs.writeFileSync(file, text, "utf8");
      return send(res, 200, { ok: true, file, backup });
    }
    if (u.pathname === "/api/shot" && req.method === "POST") {
      const name = (u.searchParams.get("name") || "rig2_shot").replace(/[^\w.-]/g, "_");
      const b64 = (await body(req)).toString("utf8").replace(/^data:image\/png;base64,/, "");
      const dir = path.join(ROOT, "Logs/Screens");
      fs.mkdirSync(dir, { recursive: true });
      const file = path.join(dir, name + ".png");
      fs.writeFileSync(file, Buffer.from(b64, "base64"));
      return send(res, 200, { ok: true, file });
    }
    // 정적 파일
    let rel = decodeURIComponent(u.pathname);
    if (rel === "/" || rel === "") { res.writeHead(302, { Location: "/Tools/rig-web/index.html" }); return res.end(); }
    const file = path.resolve(ROOT, "." + rel);
    if (!file.startsWith(ROOT + path.sep)) return send(res, 403, { error: "forbidden" });
    if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) { console.log("404", rel); return send(res, 404, { error: "not found: " + rel }); }
    send(res, 200, fs.readFileSync(file), TYPES[path.extname(file).toLowerCase()] || "application/octet-stream");
  } catch (e) {
    send(res, 500, { error: String(e && e.message || e) });
  }
}).listen(PORT, "127.0.0.1", () => console.log(`rig-web: http://127.0.0.1:${PORT}/  (root ${ROOT})`));
