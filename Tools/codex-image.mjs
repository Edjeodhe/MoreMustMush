#!/usr/bin/env node
// Generate an image with the Codex CLI (ChatGPT login, built-in image_generation tool)
// and copy the result into the Unity project.
//
// Usage:
//   node Tools/codex-image.mjs --prompt "<what to draw>" --out Assets/Art/Generated/<name>.png
//        [--transparent] [--ref <image>]... [--style <file>] [--search] [--timeout <sec>]
//
// Prints one JSON line on success: {"out": "...", "source": "...", "rgba": true}

import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const codexHome = process.env.CODEX_HOME || path.join(os.homedir(), ".codex");
const imagesRoot = path.join(codexHome, "generated_images");

function parseArgs(argv) {
  const opts = { refs: [], transparent: false, timeout: 300 };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => {
      if (i + 1 >= argv.length) fail(`${a} needs a value`);
      return argv[++i];
    };
    if (a === "--prompt") opts.prompt = next();
    else if (a === "--out") opts.out = next();
    else if (a === "--ref") opts.refs.push(next());
    else if (a === "--style") opts.style = next();
    else if (a === "--timeout") opts.timeout = Number(next());
    else if (a === "--transparent") opts.transparent = true;
    else if (a === "--search") opts.search = true;
    else fail(`unknown argument: ${a}`);
  }
  if (!opts.prompt || !opts.out) fail("--prompt and --out are required");
  return opts;
}

function fail(msg) {
  console.error(`codex-image: ${msg}`);
  process.exit(1);
}

// The Codex desktop app ships codex.exe under a hashed folder that changes on update,
// so pick the newest one unless CODEX_CLI_PATH or PATH provides it.
function resolveCodex() {
  if (process.env.CODEX_CLI_PATH && fs.existsSync(process.env.CODEX_CLI_PATH)) return process.env.CODEX_CLI_PATH;
  const binRoot = path.join(process.env.LOCALAPPDATA || "", "OpenAI", "Codex", "bin");
  if (fs.existsSync(binRoot)) {
    const found = fs.readdirSync(binRoot)
      .map((d) => path.join(binRoot, d, "codex.exe"))
      .filter((p) => fs.existsSync(p))
      .sort((a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs);
    if (found.length) return found[0];
  }
  return "codex";
}

function buildPrompt(opts) {
  const lines = [
    "Generate exactly one image with your image generation tool.",
    "Do not write code, edit files, or run shell commands.",
    "",
    `Image request: ${opts.prompt}`,
  ];
  if (opts.style) lines.push("", "Art style guide:", fs.readFileSync(opts.style, "utf8").trim());
  if (opts.transparent) {
    lines.push("", "Output requirements: transparent background (PNG with alpha channel), a single isolated subject, centered with some padding, no ground shadow, no text, no frame.");
  }
  if (opts.refs.length) lines.push("", "Use the attached image(s) as visual reference for style and consistency.");
  if (opts.search) lines.push("", "You may web-search real references first (e.g. photos of the named species) so shapes and colors are accurate, then draw in the art style above.");
  lines.push("", "When the image is generated, reply with just: done");
  return lines.join("\n");
}

function listImages(dir) {
  if (!fs.existsSync(dir)) return [];
  const out = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) out.push(...listImages(p));
    else if (/\.(png|webp|jpe?g)$/i.test(e.name)) out.push(p);
  }
  return out;
}

// PNG color type 4 or 6 means the image carries an alpha channel.
function pngHasAlpha(file) {
  const buf = Buffer.alloc(26);
  const fd = fs.openSync(file, "r");
  fs.readSync(fd, buf, 0, 26, 0);
  fs.closeSync(fd);
  const isPng = buf.readUInt32BE(0) === 0x89504e47;
  return isPng && (buf[25] === 4 || buf[25] === 6);
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const codex = resolveCodex();
  const startedAt = Date.now();

  const args = ["exec", "--skip-git-repo-check", "--json", "-s", "read-only", "-C", repoRoot];
  // Live web search lets Codex look up real references (e.g. actual mushroom species) before drawing.
  if (opts.search) args.push("-c", 'web_search="live"');
  for (const r of opts.refs) args.push("-i", path.resolve(r));
  args.push("-");

  const child = spawn(codex, args, { stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
  child.stdin.end(buildPrompt(opts));

  let threadId = null;
  let lastError = null;
  let buf = "";
  child.stdout.on("data", (d) => {
    buf += d;
    let i;
    while ((i = buf.indexOf("\n")) >= 0) {
      const line = buf.slice(0, i).trim();
      buf = buf.slice(i + 1);
      if (!line) continue;
      try {
        const ev = JSON.parse(line);
        if (ev.thread_id && !threadId) threadId = ev.thread_id;
        if (ev.type === "error" || ev.type === "turn.failed") lastError = JSON.stringify(ev);
      } catch { /* non-JSON log line */ }
    }
  });
  let stderr = "";
  child.stderr.on("data", (d) => { stderr += d; });

  const timer = setTimeout(() => child.kill(), opts.timeout * 1000);
  const code = await new Promise((resolve) => child.on("close", resolve));
  clearTimeout(timer);

  // Only this run's thread folder counts: other Codex runs (parallel sheets) write to the same root.
  // Without a thread id, fall back to anything written since we started.
  const own = threadId ? listImages(path.join(imagesRoot, threadId)) : [];
  const candidates = (own.length || threadId ? own : listImages(imagesRoot).filter((p) => fs.statSync(p).mtimeMs >= startedAt - 1000))
    .sort((a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs);

  if (!candidates.length) {
    fail(`no image produced (exit ${code}). ${lastError ?? ""}\n${stderr.trim().split("\n").slice(-10).join("\n")}`);
  }

  const source = candidates[0];
  const out = path.resolve(repoRoot, opts.out);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.copyFileSync(source, out);

  const rgba = pngHasAlpha(out);
  if (opts.transparent && !rgba) console.error("codex-image: warning: requested transparency but the PNG has no alpha channel");
  console.log(JSON.stringify({ out: path.relative(repoRoot, out).replaceAll("\\", "/"), source, rgba }));
}

main();
