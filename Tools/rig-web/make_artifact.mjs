// rig-web → 여러 파일 아티팩트 (그림은 따로 올림)
import fs from "node:fs";
import path from "node:path";

const ROOT = process.argv[2];            // 꺼낸 저장소 루트
const OUT = process.argv[3];             // 출력 폴더
const HERE = path.join(ROOT, "Tools/rig-web");
const RIG = "Assets/Resources/Rig/spirit_rig2.json";
const ART = path.join(ROOT, "Assets/Resources");
const MODULES = ["unity.js", "rigjson.js", "rig2.js", "mush_rig.js", "app.js"];

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
  } catch {}
  return out;
}

fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(path.join(OUT, "sprites"), { recursive: true });
const rigText = fs.readFileSync(path.join(ROOT, RIG), "utf8");
const sprites = {};
const files = {};
for (const d of ["Characters/Evo", "Characters/Rig2"]) {
  const abs = path.join(ART, d);
  for (const f of fs.readdirSync(abs)) {
    if (!f.toLowerCase().endsWith(".png")) continue;
    const key = d + "/" + f.slice(0, -4);
    if (key.startsWith("Characters/Evo/") && !rigText.includes('"' + key.slice("Characters/".length) + '"')) continue;
    const src = path.join(abs, f);
    const buf = fs.readFileSync(src);
    const pub = "sprites/" + key.slice("Characters/".length).replace("/", "_") + ".png";
    fs.copyFileSync(src, path.join(OUT, pub));
    files[pub] = pub;
    sprites[key] = { url: pub, w: buf.readUInt32BE(16), h: buf.readUInt32BE(20), ...readMeta(src) };
  }
}
for (const m of MODULES) { fs.copyFileSync(path.join(HERE, m), path.join(OUT, m)); files[m] = m; }
fs.writeFileSync(path.join(OUT, "spirit_rig2.json"), rigText); files["spirit_rig2.json"] = "spirit_rig2.json";

const css = fs.readFileSync(path.join(HERE, "style.css"), "utf8");
let html = fs.readFileSync(path.join(HERE, "index.html"), "utf8");
const body = /<body>([\s\S]*)<\/body>/.exec(html)[1].replace(/<script type="module" src="app.js"><\/script>/, "");
const boot = `<script>
(() => {
  const SPRITES = ${JSON.stringify(sprites)};
  const json = (o) => new Response(JSON.stringify(o), { headers: { "Content-Type": "application/json" } });
  const realFetch = window.fetch.bind(window);
  window.fetch = async (input, init = {}) => {
    const url = typeof input === "string" ? input : input.url;
    const p = new URL(url, "http://x").pathname;
    if (p === "/api/sprites") return json(SPRITES);
    if (p === "/${RIG}") return realFetch("spirit_rig2.json");
    if (p === "/api/save") return json({ file: "웹 보기 전용이라 프로젝트 저장은 안 됩니다 (node Tools/rig-web/serve.mjs 로 띄워야 저장됨)", backup: null });
    if (p === "/api/shot") return json({ file: "웹 보기에서는 스크린샷 저장이 안 됩니다" });
    return realFetch(input, init);
  };
})();
</script>
<script type="module" src="app.js"></script>`;

const page = `<title>정령 리그2 작업장</title>
<meta name="claude:viewport-fit-sides" content="cover">
<style>
${css}
</style>
${body}
${boot}
`;
fs.writeFileSync(path.join(OUT, "index.html"), page);
fs.writeFileSync(path.join(OUT, "files.json"), JSON.stringify(files));
console.log("sprites", Object.keys(sprites).length, "files", Object.keys(files).length, "page KB", (page.length / 1024).toFixed(0));
