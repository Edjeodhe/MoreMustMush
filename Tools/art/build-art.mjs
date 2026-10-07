#!/usr/bin/env node
// Generate (once) and slice every sprite sheet in sheets.mjs.
//
//   node Tools/art/build-art.mjs              # all sheets; reuses Art/Raw/<id>.png when it exists
//   node Tools/art/build-art.mjs <id> ...     # only these sheets
//   node Tools/art/build-art.mjs --force <id> # regenerate these sheets with Codex
//   node Tools/art/build-art.mjs --slice-only # never call Codex, just re-slice existing raw sheets
//
// Raw sheets and their prompts live in Art/Raw/ (committed), so a build never pays for an image twice.

import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SHEETS } from "./sheets.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const rawDir = path.join(repoRoot, "Art", "Raw");
const genRoot = path.join(repoRoot, "Assets", "Art", "Generated");
const CONCURRENCY = 2;

const argv = process.argv.slice(2);
const force = argv.includes("--force");
const sliceOnly = argv.includes("--slice-only");
const wanted = argv.filter((a) => !a.startsWith("--"));
const sheets = wanted.length ? SHEETS.filter((s) => wanted.includes(s.id)) : SHEETS;
const unknown = wanted.filter((w) => !SHEETS.some((s) => s.id === w));
if (unknown.length) { console.error(`unknown sheet(s): ${unknown.join(", ")}`); process.exit(1); }

function run(cmd, args) {
  return new Promise((resolve) => {
    const p = spawn(cmd, args, { cwd: repoRoot, windowsHide: true });
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
    "No grid lines, no labels, no numbers, no text anywhere. All assets share one consistent art style, line weight and lighting.",
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
  if (!sliceOnly && (force || !fs.existsSync(raw))) {
    const prompt = single ? s.prompt : sheetPrompt(s);
    fs.mkdirSync(rawDir, { recursive: true });
    fs.writeFileSync(path.join(rawDir, `${s.id}.prompt.txt`), prompt);
    const args = ["Tools/codex-image.mjs", "--prompt", prompt, "--out", path.relative(repoRoot, raw), "--style", "Tools/art-style.md", "--timeout", "600"];
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
  const sl = await run("python", ["Tools/art/slice_sheet.py", raw, String(s.rows), String(s.cols), path.join(genRoot, s.out), names]);
  if (sl.code !== 0) { console.error(`[${s.id}] slicing failed:\n${sl.err.trim()}`); return false; }
  const res = JSON.parse(sl.out);
  console.log(`[${s.id}] sliced ${Object.keys(res.items).length}/${s.items.length}${res.warnings.length ? " — " + res.warnings.join("; ") : ""}`);
  return res.warnings.length === 0;
}

// Sheets that use another sheet as a reference (e.g. character parts drawn from the full character) run last.
let ok = true;
for (const batch of [sheets.filter((s) => !s.refs), sheets.filter((s) => s.refs)]) {
  const queue = [...batch];
  await Promise.all(Array.from({ length: Math.min(CONCURRENCY, queue.length) }, async () => {
    while (queue.length) ok = (await buildSheet(queue.shift())) && ok;
  }));
}
process.exitCode = ok ? 0 : 1;
