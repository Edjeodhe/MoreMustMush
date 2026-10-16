#!/usr/bin/env node
// Generate (once) and slice every sprite sheet in sheets.mjs.
//
//   node Tools/art/build-art.mjs              # all sheets; reuses Art/Raw/<id>.png when it exists
//   node Tools/art/build-art.mjs <id> ...     # only these sheets
//   node Tools/art/build-art.mjs --force <id> # regenerate these sheets with Codex
//   node Tools/art/build-art.mjs --slice-only # never call Codex, just re-slice existing raw sheets
//   node Tools/art/build-art.mjs --set soft   # the soft watercolor set (sheets-soft.mjs → Art/Raw_Soft, Assets/Resources/Art/Soft)
//
// Raw sheets and their prompts live in Art/Raw/ (committed), so a build never pays for an image twice.

import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const argv = process.argv.slice(2);
const setIdx = argv.indexOf("--set");
const SET = setIdx >= 0 ? argv.splice(setIdx, 2)[1] : "default";
const SETS = {
  default: { module: "./sheets.mjs", raw: "Art/Raw", gen: "Assets/Resources/Art/Generated", style: "Tools/art-style.md" },
  soft: { module: "./sheets-soft.mjs", raw: "Art/Raw_Soft", gen: "Assets/Resources/Art/Soft", style: "Tools/art-style-soft.md" },
  dream: { module: "./sheets-dream.mjs", raw: "Art/Raw_Dream", gen: "Assets/Resources/Art/Dream", style: "Tools/art-style-dream.md" },
  doodle: { module: "./sheets-doodle.mjs", raw: "Art/Raw_Doodle", gen: "Assets/Resources/Art/Doodle", style: "Tools/art-style-doodle.md", imageStyle: "Tools/art-style-story.md" },
};
if (!SETS[SET]) { console.error(`unknown set: ${SET}`); process.exit(1); }
const { SHEETS } = await import(SETS[SET].module);
const rawDir = path.join(repoRoot, SETS[SET].raw);
const genRoot = path.join(repoRoot, SETS[SET].gen);
const CONCURRENCY = Number(process.env.ART_CONCURRENCY || 2);

const force = argv.includes("--force");
const sliceOnly = argv.includes("--slice-only");
const wanted = argv.filter((a) => !a.startsWith("--"));
const sheets = wanted.length ? SHEETS.filter((s) => wanted.includes(s.id)) : SHEETS;
const unknown = wanted.filter((w) => !SHEETS.some((s) => s.id === w));
if (unknown.length) { console.error(`unknown sheet(s): ${unknown.join(", ")}`); process.exit(1); }

function run(cmd, args, env) {
  return new Promise((resolve) => {
    const p = spawn(cmd, args, { cwd: repoRoot, windowsHide: true, env: env ? { ...process.env, ...env } : process.env });
    let out = "", err = "";
    p.stdout.on("data", (d) => (out += d));
    p.stderr.on("data", (d) => (err += d));
    p.on("close", (code) => resolve({ code, out, err }));
  });
}

function sheetPrompt(s) {
  const n = s.items.length;
  const lines = [
    `Sprite sheet: a ${s.rows}x${s.cols} grid holding ${n} separate game assets on a fully transparent background.`,
    "Each asset sits centered in its own invisible grid cell with clear empty space around it. Assets never touch or overlap.",
    s.lettering // lettering sheets: the words ARE the assets
      ? "No grid lines and no extra words: only the exact lettering listed for each cell, spelled exactly as written. All assets share one consistent lettering style."
      : "No grid lines, no labels, no numbers, no text anywhere. All assets share one consistent art style, line weight and lighting.",
    s.subject,
    "",
    "Assets in reading order (left to right, top to bottom):",
    ...s.items.map(([, desc], i) => `${i + 1}. ${desc}`),
  ];
  if (n < s.rows * s.cols) lines.push(`Leave the remaining ${s.rows * s.cols - n} cell(s) empty.`);
  return lines.join("\n");
}

async function buildSheet(s) {
  const raw = path.join(rawDir, `${s.id}.png`);
  const single = s.kind === "image";   // one full image (e.g. a background): copied as is, never sliced
  if (s.copyOf && !fs.existsSync(raw)) fs.copyFileSync(path.join(repoRoot, s.copyOf), raw);
  if (!sliceOnly && (force || !fs.existsSync(raw))) {
    const prompt = single ? s.prompt : sheetPrompt(s);
    fs.mkdirSync(rawDir, { recursive: true });
    fs.writeFileSync(path.join(rawDir, `${s.id}.prompt.txt`), prompt);
    const args = ["Tools/codex-image.mjs", "--prompt", prompt, "--out", path.relative(repoRoot, raw), "--style", (single && SETS[SET].imageStyle) || SETS[SET].style, "--timeout", "600"];
    if (!single) args.push("--transparent");
    if (s.search) args.push("--search");
    for (const r of s.refs || []) args.push("--ref", r);
    console.log(`[${s.id}] generating...`);
    const g = await run("node", args);
    if (g.code !== 0) { console.error(`[${s.id}] generation failed:\n${g.err.trim()}`); return false; }
    if (g.err.trim()) console.error(`[${s.id}] ${g.err.trim()}`);
  }
  if (!fs.existsSync(raw)) { console.error(`[${s.id}] no raw sheet`); return false; }
  if (single) {
    const out = path.join(genRoot, s.out);
    fs.mkdirSync(path.dirname(out), { recursive: true });
    fs.copyFileSync(raw, out);
    console.log(`[${s.id}] copied to ${path.relative(repoRoot, out)}`);
    return true;
  }
  const names = s.items.map(([name]) => name).join(",");
  // fx sheets keep soft alpha and fill a fixed canvas (tintable effect textures), see slice_fx.py
  // rig: true = group blobs per row by their gaps (uneven spacing, detached bits like stars or swirls), see slice_rig.py;
  // with fx: true the FX slicer cuts each item by that grouping instead of by grid cell
  const slicer = s.fx ? "Tools/art/slice_fx.py" : s.rig ? "Tools/art/slice_rig.py" : "Tools/art/slice_sheet.py";
  const sl = await run("python", [slicer, raw, String(s.rows), String(s.cols), path.join(genRoot, s.out), names], s.fx && s.rig ? { FX_GROUP: "1" } : null);
  if (sl.code !== 0) { console.error(`[${s.id}] slicing failed:\n${sl.err.trim()}`); return false; }
  const res = JSON.parse(sl.out);
  console.log(`[${s.id}] sliced ${Object.keys(res.items).length}/${s.items.length}${res.warnings.length ? " — " + res.warnings.join("; ") : ""}`);
  return res.warnings.length === 0;
}

// A sheet that uses another sheet of this run as a reference (e.g. character parts drawn from the full
// character) starts only after that sheet is done.
let ok = true;
const rawOf = (s) => path.relative(repoRoot, path.join(rawDir, `${s.id}.png`)).replaceAll("\\", "/");
// Sheets that others wait on go first so long reference chains (full → parts → skins) start early.
const isRef = (s) => sheets.some((o) => (o.refs || []).includes(rawOf(s)));
const pending = new Map([...sheets].sort((x, y) => isRef(y) - isRef(x)).map((s) => [rawOf(s), s]));
const done = new Set();
const waitsOn = (s) => (s.refs || []).filter((r) => pending.has(r) && !done.has(r) && r !== rawOf(s));
const running = new Set();
await new Promise((resolve) => {
  const pump = () => {
    for (const [key, s] of pending) {
      if (running.size >= CONCURRENCY) break;
      if (running.has(key) || done.has(key) || waitsOn(s).length) continue;
      running.add(key);
      buildSheet(s).then((r) => { ok = r && ok; running.delete(key); done.add(key); pump(); });
    }
    if (done.size === pending.size) resolve();
  };
  pump();
});
// Critter caps and bodies are post-processed by build_critter_rig.py (opening cut out of each cap, closed-eye bodies
// re-made from the open ones). Slicing those sheets again writes the raw parts back over the processed ones, so the
// rig builder runs again here, from the fresh slices (its unprocessed copies in rig_src are replaced first).
const RIG_SHEET = /^critter_(cap|body|rig)_/;
const rigSheets = sheets.filter((s) => RIG_SHEET.test(s.id));
if (SET === "dream" && rigSheets.length) {
  const src = path.join(repoRoot, "Art/Raw_Dream/rig_src");
  for (const s of rigSheets)
    for (const [name] of s.items) {
      const f = path.join(src, `${name}.png`);
      if (fs.existsSync(f)) fs.rmSync(f);
    }
  const r = await run("python", ["Tools/art/build_critter_rig.py"]);
  console.log(r.code === 0 ? "[critter rig] rebuilt (caps cut, closed bodies, layout)" : `[critter rig] failed:
${r.err.trim()}`);
  if (r.code !== 0) ok = false;
}
process.exitCode = ok ? 0 : 1;
