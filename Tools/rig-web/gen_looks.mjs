#!/usr/bin/env node
// 리그2 옆·뒷모습 파츠를 정령 모습마다 코덱스로 생성 (불 ★3 방식을 모든 ★2·★3에):
//   node Tools/rig-web/gen_looks.mjs --looks dew_2,dew_3,... [--parts head_side,...] [--phase 1|2] [--jobs 3] [--force]
//   원본은 Art/Raw_Dream/rig2/<모습>/gen_<부위>.png (게임 밖). 크기·자리 맞추기는 fit_gen_parts.py --look <모습>.
//   phase 1: 머리·몸통 옆/뒤 + 소매 (맞춘 뒤 배치 그림 ref_side/back_layout.png가 생긴다)
//   phase 2: 머리카락 한 장 옆/뒤 (배치 그림을 참고로 넣어 크기·자리를 맞춘다)
//   다리·팔은 모든 정령이 같은 살색·갈색 신발이라 불 ★3 원본(Art/Raw_Dream/rig2/gen_leg_*·gen_arm_full)을 같이 쓴다.
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..", "..");
const RAW = "Art/Raw_Dream/rig2";
const EVO = "Assets/Resources/Art/Dream/Characters/Evo";

// 정령별 모습 설명 (참고 그림을 돕는 짧은 말). 색·모양은 참고 그림이 우선
export const SPIRIT = {
  dew: { cap: "a wide flat BLUE mushroom cap with soft darker blue rings and little water drops dripping from the rim", hair: "very pale icy blue-white", dress: "pale blue and white ruffled gill dress" },
  fire: { cap: "a big RED fly-agaric mushroom cap with cream warts", hair: "cream blonde", dress: "cream ruffled mushroom-gill dress" },
  leaf: { cap: "a rounded mossy GREEN mushroom cap with darker moss patches and a tiny two-leaf sprout on top", hair: "cream blonde", dress: "cream ruffled gill dress with green leaf trim" },
  moon: { cap: "a pointed deep INDIGO-BLUE mushroom cap with a golden crescent moon and tiny star dots", hair: "soft lavender", dress: "lavender and pale blue ruffled gill dress" },
  rock: { cap: "a smooth round BROWN bolete mushroom cap", hair: "warm cream blonde", dress: "cream and soft brown ruffled gill dress" },
  spark: { cap: "a wide wavy-edged YELLOW chanterelle mushroom cap", hair: "pale golden blonde", dress: "pale yellow ruffled gill dress" },
  star: { cap: "a golden-yellow STAR-FLOWER mushroom cap with pointed petal flaps around a round cream dome and a little gold star on top", hair: "cream blonde", dress: "pale yellow ruffled gill dress" },
  wind: { cap: "a pale silver-white upturned FUNNEL mushroom cap with fine grey gills showing underneath", hair: "silvery white", dress: "silver-white ruffled gill dress" },
};
const HAIRLEN = { 2: "a short soft chin-length bob", 3: "long soft wavy hair that flows down to her waist" };

const STYLE = "PAINTING STYLE (most important): copy the attached spirit painting exactly: light transparent watercolor washes on paper, visible paper grain, soft faceted colour patches inside each area, a thin slightly darker pigment edge where a wash dried, exactly the same gentle pastel colours as this spirit in the attached pictures, flat soft lighting. NOT felt, NOT plush, NOT 3D rendered, no airbrush gradients, no hard black outlines, no glow, no shadow.";
const PART = "ONE single cut-out body part of the attached chibi mushroom-girl spirit (the FIRST attached picture of the whole character shows how she looks), for 2D puppet (cut-out) animation: alone, centered, on a FULLY TRANSPARENT background, nothing else in the picture, no ground, no text, no frame.";

function partsFor(id, form) {
  const s = SPIRIT[id], hl = HAIRLEN[form];
  return [
    { name: "head_side", phase: 1, refs: ["whole", "head"],
      prompt: `${PART} SIDE VIEW (profile, facing RIGHT) of the spirit's HEAD: her mushroom cap (${s.cap}) seen exactly from the side, worn like a hat and tilted slightly forward; under it her face in profile facing right: one small dark-brown dot eye, a soft blush, a tiny smile, the small nose bump, her short ${s.hair} front bangs on the forehead only; the jaw line curving naturally and roundly into a short smooth neck stub under the chin (the neck the same width as her neck in the front view, its bottom cut straight; NO flat chin, NO step between jaw and neck). NO hair behind the ear and NO hair on the back of the head (all the side and back hair is a separate part, so behind the cheek there is only plain skin of the head under the cap), NO body.` },
    { name: "head_back", phase: 1, refs: ["whole", "head"],
      prompt: `${PART} BACK VIEW (seen from directly behind) of the spirit's HEAD: her mushroom cap (${s.cap}) seen from behind, and under the cap the back of her head covered by ${s.hair} hair (the short top layer only), cut cleanly at the neck with a short neck stub. NO face (we see her from behind), NO long hair below the cap, NO body.` },
    { name: "body_side", phase: 1, refs: ["body", "whole"],
      prompt: `${PART} SIDE VIEW (profile, facing RIGHT) of the spirit's TORSO wearing her CREAM ruffled mushroom-gill dress (EXACTLY the colours, petal shapes and painting of the FIRST attached picture, a plain cream dress for every spirit — ignore any dress colour in the other pictures): a short neck stub at the top, the petal collar, the small chest, the waist, and the layered ruffled skirt flaring out (a little longer at the back), seen exactly from the side. NO head, NO arms, NO legs, NO hair.` },
    { name: "body_back", phase: 1, refs: ["body", "whole"],
      prompt: `${PART} BACK VIEW (seen from directly behind) of the spirit's TORSO wearing her CREAM ruffled mushroom-gill dress (EXACTLY the colours, petal shapes and painting of the FIRST attached picture, a plain cream dress for every spirit — ignore any dress colour in the other pictures): a short neck stub at the top, the petal collar seen from behind, the back of the dress with a small ribbon bow at the waist, and the layered ruffled skirt. NO head, NO arms, NO legs, NO hair.` },
    { name: "sleeve", phase: 1, refs: ["body", "whole"],
      prompt: `${PART} ONE short puffy cap SLEEVE of her CREAM ruffled mushroom-gill dress (EXACTLY the colours, petal shapes and painting of the FIRST attached picture, a plain cream dress for every spirit — ignore any dress colour in the other pictures), seen from the SIDE, as a separate cloth piece: a soft rounded puff with two or three layered scalloped petal ruffles at the bottom hem, like a small upside-down flower bell, the opening at the bottom is empty (the arm will come out of it), the top is rounded where it joins the shoulder. NO arm, NO body.` },
    { name: "hair_side_full", phase: 2, refs: ["layout", "head", "whole"],
      prompt: `${PART} SIDE VIEW (profile, the face would be on the RIGHT) of ONLY the spirit's WHOLE HAIR (${s.hair}, ${hl}, the same style as her hair in the other attached pictures) drawn as ONE continuous hairstyle, fitting the head in the FIRST attached picture (the side-view layout of this character, drawn without the long hair). IMPORTANT: the head in the FIRST attached picture already has her bangs and the short hair under the cap; this hair must CONTINUE that hair seamlessly, with exactly the same lock shapes, stroke size, colours and watercolour painting, so that the head's hair and this hair look like ONE hairstyle painted at the same time (not two different drawings). The hair starts at the top of her head under the mushroom cap (the top edge is a simple smooth rounded crown that will be hidden under the cap), then the soft layered locks that fall behind her ear and cheek and down the back of her head${form === 3 ? ", and below them the long wavy locks that flow down her back to her waist" : ""}. Every lock flows continuously from the crown down to the tips in one unbroken direction, like one real head of hair: NO seam, NO separate layer, NO horizontal break. On the right side leave an empty curved notch where her face and neck are. NO face, NO cap, NO front bangs on the forehead, NO body, NO dress.` },
    { name: "hair_front_full", phase: 2, refs: ["layoutFront", "head", "whole"],
      prompt: `${PART} FRONT VIEW of ONLY the spirit's long BACK HAIR (${s.hair}, ${hl}) as it is seen from the front hanging BEHIND her head, neck and shoulders, fitting the character in the FIRST attached picture (the front-view layout of this character, drawn without the back hair) at exactly the same size and place. IMPORTANT: the head in the FIRST attached picture already has her bangs and the short hair under the cap; this hair must CONTINUE that hair seamlessly, with exactly the same lock shapes, stroke size, colours and watercolour painting, so that the head's hair and this hair look like ONE hairstyle painted at the same time (not two different drawings). The top is a simple rounded mass that will be hidden behind her head and cap, then the hair falls down on both sides behind her shoulders${form === 3 ? " into long wavy locks that reach her waist and curl softly outward at the tips" : " and ends around her chin and neck"}. In the middle where her face, neck and body are, the hair is simply hidden behind them (draw it plain there). NO face, NO cap, NO bangs, NO body, NO dress.` },
    { name: "hair_back_full", phase: 2, refs: ["layoutBack", "head", "whole"],
      prompt: `${PART} BACK VIEW (seen from directly behind) of ONLY the spirit's WHOLE HAIR (${s.hair}, ${hl}, the same style as her hair in the other attached pictures) drawn as ONE continuous hairstyle, fitting the head in the FIRST attached picture (the back-view layout of this character, drawn without the long hair). IMPORTANT: the head in the FIRST attached picture already has her bangs and the short hair under the cap; this hair must CONTINUE that hair seamlessly, with exactly the same lock shapes, stroke size, colours and watercolour painting, so that the head's hair and this hair look like ONE hairstyle painted at the same time (not two different drawings). The hair starts at the top of the back of her head (the top edge is a simple smooth rounded crown that will be hidden under the mushroom cap), covers the whole back of her head as soft layered locks parted gently down the middle${form === 3 ? ", then flows down her back into long wavy locks that end at her waist and curl softly outward at the tips" : " and ends around her chin and neck"}. Every lock flows continuously from the crown down to the tips, like one real head of hair seen from behind: NO seam, NO separate layer, NO horizontal break, NO flat smooth patch. Symmetric overall but with natural variation. NO cap, NO face, NO body, NO dress.` },
  ];
}

// ★0·★1 버섯 모습: 예전엔 통짜 그림을 잘라 써서 몸통에 손 자국·얼굴 조각이 남고 갓 안쪽이 메워져 있었다 → 부위마다 깨끗하게 새로 그린다
function mushPartsFor(id, form) {
  const s = SPIRIT[id];
  const MP = "ONE single cut-out body part of the attached little chibi mushroom creature (the FIRST attached picture shows the whole creature), for 2D puppet (cut-out) animation: alone, centered, on a FULLY TRANSPARENT background, nothing else in the picture, no ground, no text, no frame.";
  const out = [
    { name: "m_cap", phase: 1, refs: ["whole"],
      prompt: `${MP} ONLY its mushroom CAP (${s.cap}), seen from the front and very slightly from below, exactly the same shape, colours and pattern as in the attached picture, with the whole underside gill band visible as a clean curved band under the rim, the middle of the underside is plain gills (nothing attached). NO stem, NO body, NO face, NO feet.` },
    { name: "m_stem", phase: 1, refs: ["whole"],
      prompt: `${MP} ONLY its soft chubby round cream STEM BODY (the little rounded body under the cap), front view, the same size, colour and soft fuzzy texture as in the attached picture, with two soft pink blush cheeks, a smooth rounded top edge (it goes under the cap). It is completely PLAIN: NO eyes, NO mouth, NO face at all, NO arms, NO hands, NO feet, NO cap.` },
    { name: "m_foot", phase: 1, refs: ["whole"],
      prompt: `${MP} ONLY ONE of its little stubby rounded BROWN FEET (a small soft oval nub foot), seen from the front, the same colour and size proportion as in the attached picture. Just one foot.` },
  ];
  if (form === 1)
    out.push({ name: "m_nub", phase: 1, refs: ["whole"],
      prompt: `${MP} ONLY ONE of its tiny round cream stubby ARMS (a small soft rounded nub hand like a little mitten, slightly longer than wide), the same colour and texture as its body in the attached picture. Just one arm, pointing down.` });
  return out;
}

function refPath(look, r) {
  const [id, f] = look.split("_");
  return {
    whole: `${EVO}/evo_${look}.png`, head: `${EVO}/k_${look}_head.png`, body: `${EVO}/k_${look}_body.png`, hair: `${EVO}/k_${look}_hair.png`,
    style: "Art/Raw_Dream/spirits_base.png",
    layout: `${RAW}/${look}/ref_side_layout.png`, layoutBack: `${RAW}/${look}/ref_back_layout.png`, layoutFront: `${RAW}/${look}/ref_front_layout.png`,
  }[r];
}

function run(look, p) {
  const out = `${RAW}/${look}/gen_${p.name}.png`;
  const args = [path.join(ROOT, "Tools/codex-image.mjs"), "--prompt", `${p.prompt}\n\n${STYLE}`, "--out", out, "--transparent", "--timeout", "420"];
  for (const r of [...p.refs, "style"]) args.push("--ref", refPath(look, r));
  return new Promise((res) => {
    const t0 = Date.now();
    const c = spawn(process.execPath, args, { cwd: ROOT, stdio: ["ignore", "pipe", "pipe"] });
    let o = "", e = "";
    c.stdout.on("data", (d) => (o += d)); c.stderr.on("data", (d) => (e += d));
    c.on("close", (code) => {
      const line = `[${look} ${p.name}] ${code === 0 ? "ok" : "FAIL"} ${((Date.now() - t0) / 1000).toFixed(0)}s ${o.trim()} ${e.trim().slice(-300)}`;
      console.log(line);
      fs.appendFileSync(path.join(ROOT, RAW, "gen_looks_log.txt"), line + "\n");
      res(code === 0);
    });
  });
}

function writePromptsMd(looks) {
  const lines = ["# 리그2 옆·뒷모습 파츠 프롬프트 (정령 모습마다)", "", "`node Tools/rig-web/gen_looks.mjs`가 쓴다. 공통 그림풍:", "", "> " + STYLE, "", "공통 머리말:", "", "> " + PART, ""];
  for (const look of looks) {
    const [id, f] = look.split("_");
    lines.push(`## ${look}`, "");
    for (const p of (Number(f) >= 2 ? partsFor : mushPartsFor)(id, Number(f)))
      lines.push(`- **${p.name}** (phase ${p.phase}, 참고 ${p.refs.join("·")}): ${p.prompt.replace(PART + " ", "")}`);
    lines.push("");
  }
  fs.writeFileSync(path.join(HERE, "looks_prompts.md"), lines.join("\n"), "utf8");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const a = process.argv.slice(2);
  const opt = (k, d) => { const i = a.indexOf(k); return i >= 0 ? a[i + 1] : d; };
  const looks = opt("--looks", "").split(",").filter(Boolean);
  const only = opt("--parts", "").split(",").filter(Boolean);
  const phase = Number(opt("--phase", "0"));
  const jobs = Number(opt("--jobs", "3")), force = a.includes("--force");
  writePromptsMd(looks);
  const todo = [];
  for (const look of looks) {
    const [id, f] = look.split("_");
    fs.mkdirSync(path.join(ROOT, RAW, look), { recursive: true });
    for (const p of (Number(f) >= 2 ? partsFor : mushPartsFor)(id, Number(f))) {
      if (only.length && !only.includes(p.name)) continue;
      if (phase && p.phase !== phase) continue;
      if (!force && fs.existsSync(path.join(ROOT, RAW, look, `gen_${p.name}.png`))) continue;
      todo.push([look, p]);
    }
  }
  console.log("생성할 파츠:", todo.length, "장");
  const q = [...todo];
  await Promise.all(Array.from({ length: Math.min(jobs, q.length) }, async () => { while (q.length) { const [l, p] = q.shift(); await run(l, p); } }));
}
