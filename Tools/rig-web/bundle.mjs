#!/usr/bin/env node
// 정령 리그2 작업장을 HTML 한 장으로 묶는다 (서버 없이 더블클릭으로 열림): node Tools/rig-web/bundle.mjs [출력=Tools/rig-web/dist/spirit_rig2.html]
//   - style.css · app.js · rig2.js · unity.js · rigjson.js 를 넣고, 모듈 import는 실행할 때 Blob URL로 이어 붙인다
//   - 그림은 data URI로 넣는다: Characters/Rig2 전부 + spirit_rig2.json이 가리키는 Characters/Evo 그림
//   - fetch를 가로챈다: /api/sprites·spirit_rig2.json은 넣어 둔 데이터, "프로젝트에 저장"은 JSON 다운로드, 스크린샷은 PNG 다운로드
// 리그·화면 코드를 고친 뒤 다시 돌리면 된다.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..", "..");
const OUT = path.resolve(process.argv[2] || path.join(HERE, "dist", "spirit_rig2.html"));
const RIG = "Assets/Resources/Rig/spirit_rig2.json";
// serve.mjs와 같은 순서 (뒤가 앞을 덮는다)
const ART_ROOTS = ["Assets/ArtArchive/Generated", "Assets/ArtArchive/Soft", "Assets/ArtArchive/Doodle", "Assets/Resources/Art/Dream"];
const MODULES = ["unity.js", "rigjson.js", "rig2.js", "mush_rig.js", "app.js"];   // 의존 순서

const read = (f) => fs.readFileSync(path.join(HERE, f), "utf8");

function pngSize(buf) { return [buf.readUInt32BE(16), buf.readUInt32BE(20)]; }

// serve.mjs readMeta와 같다
function readMeta(file) {
  const out = { px: 0.5, py: 0.5, ppu: 100 };
  try {
    const t = fs.readFileSync(file + ".meta", "utf8");
    const al = /^\s*alignment:\s*(\d+)/m.exec(t);
    const pv = /^\s*spritePivot:\s*\{x:\s*([-\d.]+),\s*y:\s*([-\d.]+)\}/m.exec(t);
    const pp = /^\s*spritePixelsToUnits:\s*([\d.]+)/m.exec(t);
    if (pp) out.ppu = Number(pp[1]);
    const A = [[0.5, 0.5], [0, 1], [0.5, 1], [1, 1], [0, 0.5], [1, 0.5], [0, 0], [0.5, 0], [1, 0]];
    const a = al ? Number(al[1]) : 0;
    if (a === 9 && pv) { out.px = Number(pv[1]); out.py = Number(pv[2]); } else if (A[a]) [out.px, out.py] = A[a];
  } catch { /* .meta가 없으면 Unity 기본값 */ }
  return out;
}

// 키 → 파일 (SpriteDB 우선순위)
function spriteFiles(dirs) {
  const map = {};
  for (const r of ART_ROOTS)
    for (const d of dirs) {
      const abs = path.join(ROOT, r, d);
      if (!fs.existsSync(abs)) continue;
      for (const f of fs.readdirSync(abs))
        if (f.toLowerCase().endsWith(".png")) map[d + "/" + f.slice(0, -4)] = path.join(abs, f);
    }
  return map;
}

const rigText = fs.readFileSync(path.join(ROOT, RIG), "utf8");
const files = spriteFiles(["Characters/Evo", "Characters/Rig2"]);
const sprites = {};
let bytes = 0;
for (const [key, file] of Object.entries(files)) {
  // Evo는 리그 데이터가 가리키는 것만 (Rig2는 표정·복장 이름을 코드가 만들어서 전부)
  if (key.startsWith("Characters/Evo/") && !rigText.includes('"' + key.slice("Characters/".length) + '"')) continue;
  const buf = fs.readFileSync(file);
  const [w, h] = pngSize(buf);
  bytes += buf.length;
  sprites[key] = { url: "data:image/png;base64," + buf.toString("base64"), w, h, ...readMeta(file) };
}

const mods = Object.fromEntries(MODULES.map((m) => [m, read(m)]));
const css = read("style.css");
let html = read("index.html");
const esc = (s) => s.replace(/<\/script/gi, "<\\/script");

// 서버 없이 도는 부트: fetch 가로채기 → 모듈을 Blob URL로 이어 붙여 app.js 실행
const boot = `
(() => {
  const DATA = window.__RIG2__;
  const dl = (name, blob) => { const a = document.createElement("a"); a.href = URL.createObjectURL(blob); a.download = name; a.click(); };
  const json = (o) => new Response(JSON.stringify(o), { headers: { "Content-Type": "application/json" } });
  const realFetch = window.fetch.bind(window);
  window.fetch = async (input, init = {}) => {
    const url = typeof input === "string" ? input : input.url;
    const p = new URL(url, "http://x").pathname;
    if (p === "/api/sprites") return json(DATA.sprites);
    if (p === "/${RIG}") return new Response(DATA.rig, { headers: { "Content-Type": "application/json" } });
    if (p === "/api/save") { dl("spirit_rig2.json", new Blob([init.body], { type: "application/json" })); return json({ file: "다운로드한 spirit_rig2.json (Assets/Resources/Rig/에 덮어쓰기)", backup: null }); }
    if (p === "/api/shot") {
      const name = new URL(url, "http://x").searchParams.get("name") || "shot";
      const blob = await realFetch(init.body).then((r) => r.blob());
      dl(name + ".png", blob); return json({ file: name + ".png" });
    }
    return realFetch(input, init);
  };
  const urls = {};
  const link = (src) => src.replace(/from\\s*["']\\.\\/([\\w.]+)["']/g, (m, f) => 'from "' + urls[f] + '"');
  for (const name of ${JSON.stringify(MODULES)})
    urls[name] = URL.createObjectURL(new Blob([link(DATA.mods[name])], { type: "text/javascript" }));
  import(urls["app.js"]).catch((e) => { console.error(e); document.getElementById("status").textContent = "불러오기 실패: " + e; });
})();`;

const data = `window.__RIG2__ = ${JSON.stringify({ sprites, rig: rigText, mods })};`;

html = html
  .replace(/<link rel="stylesheet" href="style.css">/, () => `<style>\n${css}\n</style>`)
  .replace(/<script type="module" src="app.js"><\/script>/, () => `<script>${esc(data)}</script>\n<script>${esc(boot)}</script>`);
if (!html.includes("window.__RIG2__")) throw new Error("index.html의 app.js <script> 줄을 찾지 못했습니다");

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, html);
console.log(`${path.relative(ROOT, OUT)}  그림 ${Object.keys(sprites).length}장 (원본 ${(bytes / 1048576).toFixed(1)}MB) → ${(fs.statSync(OUT).size / 1048576).toFixed(1)}MB`);
