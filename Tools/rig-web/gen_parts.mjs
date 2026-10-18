#!/usr/bin/env node
// 리그2 부위별 파츠를 코덱스로 생성 (불 정령 ★3 시범): node Tools/rig-web/gen_parts.mjs [이름...] [--force] [--jobs 3]
//   원본(약 1254px)은 Art/Raw_Dream/rig2/gen_<이름>.png (게임 밖), 게임용 크기·자리는 fit_gen_parts.py가 Characters/Rig2/gen_fire_3_* 로.
//   이미 있는 원본은 건너뛴다 (--force로 다시). 쓴 프롬프트는 Tools/rig-web/parts_prompts.md 에 같이 적는다.
//   부위마다 한 장씩 (시트에 몰면 칸이 빠졌던 기록이 있어서). 그림 한 장 1~2분, 코덱스 한도를 쓴다.
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..", "..");
const RAW = "Art/Raw_Dream/rig2";
const EVO = "Assets/Resources/Art/Dream/Characters/Evo";
const REF = {
  whole: `${EVO}/evo_fire_3.png`,                 // 불 정령 ★3 전신 (확정 그림)
  head: `${EVO}/k_fire_3_head.png`, body: `${EVO}/k_fire_3_body.png`, hair: `${EVO}/k_fire_3_hair.png`, leg: `${EVO}/k_fire_3_leg.png`, arm: `${EVO}/k_fire_3_arm_up.png`,
  style: "Art/Raw_Dream/spirits_base.png",
};
const STYLE = "PAINTING STYLE (most important): copy the attached fire spirit painting exactly: light transparent watercolor washes on paper, visible paper grain, soft faceted colour patches inside each area, a thin slightly darker pigment edge where a wash dried, the same gentle pastel colours (cream hair and dress, red fly-agaric cap with cream warts, warm peach skin, soft brown shoes), flat soft lighting. NOT felt, NOT plush, NOT 3D rendered, no airbrush gradients, no hard black outlines, no glow, no shadow.";
const PART = "ONE single cut-out body part of the attached chibi mushroom-girl fire spirit, for 2D puppet (cut-out) animation: alone, centered, on a FULLY TRANSPARENT background, nothing else in the picture, no ground, no text, no frame.";

export const PARTS = [
  // 정면: 무릎이 보이는 다리 (허벅지·무릎·정강이·발목·신발, 한 장 → 무릎·발목에서 자른다)
  { name: "leg_front", refs: ["whole", "leg"], view: "정면",
    prompt: `${PART} FRONT VIEW of ONE whole LEG of the spirit (her left leg), hanging straight down: a slim cream-skinned chibi leg from the top of the thigh (cut cleanly with a rounded top where it goes under the skirt), a soft rounded KNEE in the middle, the shin, the ankle, and her little soft BROWN SHOE with the toe pointing slightly outward. The leg is about 2.5 times as long as the shoe is tall. Even width, simple, toy-like.` },
  // 옆모습 (오른쪽을 봄)
  { name: "head_side", refs: ["whole", "head"], view: "옆",
    prompt: `${PART} SIDE VIEW (profile, facing RIGHT) of the spirit's HEAD: her big red fly-agaric mushroom cap with cream warts seen from the side (worn like a hat, tilted slightly forward), under it her face in profile facing right: one small dark-brown dot eye, a soft blush, a tiny smile, the small nose bump, the cream front bangs; cut cleanly at the neck with a short neck stub under the chin. NO long back hair (it is a separate part), NO body.` },
  { name: "hair_side", refs: ["whole", "hair"], view: "옆",
    prompt: `${PART} SIDE VIEW (profile, the face would be on the RIGHT) of ONLY the spirit's long wavy cream BACK HAIR as it hangs behind her head and shoulders down to the waist, seen from the side: a soft tapered mass of wavy locks, the top part rounded where it meets the back of the head. NO face, NO cap, NO body.` },
  { name: "body_side", refs: ["whole", "body"], view: "옆",
    prompt: `${PART} SIDE VIEW (profile, facing RIGHT) of the spirit's TORSO wearing her cream ruffled mushroom-gill dress: a short neck stub at the top, the petal collar, the small chest, the waist, and the layered ruffled gill skirt flaring out (the skirt a little longer at the back), seen exactly from the side. NO head, NO arms, NO legs.` },
  { name: "leg_side", refs: ["whole", "leg"], view: "옆",
    prompt: `${PART} SIDE VIEW (profile, toes pointing RIGHT) of ONE whole LEG of the spirit hanging straight down: slim cream thigh with a rounded top, a soft rounded KNEE, the shin, the ankle and her little soft BROWN SHOE seen exactly from the side (round toe to the right, heel to the left). Simple, toy-like, the same proportions as the attached leg but longer.` },
  // 뒷모습 (사다리 오르기)
  { name: "head_back", refs: ["whole", "head"], view: "뒤",
    prompt: `${PART} BACK VIEW (seen from directly behind) of the spirit's HEAD: her big red fly-agaric mushroom cap with cream warts seen from behind, and under the cap the back of her head covered by cream hair (the short top layer of the hair only), cut cleanly at the neck with a short neck stub. NO face (we see her from behind), NO long hair below the cap, NO body.` },
  { name: "body_back", refs: ["whole", "body"], view: "뒤",
    prompt: `${PART} BACK VIEW (seen from directly behind) of the spirit's TORSO wearing her cream ruffled mushroom-gill dress: a short neck stub at the top, the petal collar seen from behind, the back of the dress with a small ribbon bow at the waist, and the layered ruffled gill skirt. NO head, NO arms, NO legs, NO hair.` },
  { name: "leg_back", refs: ["whole", "leg"], view: "뒤",
    prompt: `${PART} BACK VIEW (seen from directly behind) of ONE whole LEG of the spirit hanging straight down: slim cream thigh with a rounded top, the back of the knee, the calf, the ankle and the HEEL of her little soft BROWN SHOE seen from behind. Simple, toy-like.` },
  { name: "arm_back", refs: ["whole", "arm"], view: "뒤",
    prompt: `${PART} BACK VIEW of ONE whole ARM of the spirit (as seen from behind while she climbs a ladder), hanging straight down: rounded shoulder ball at the top, slim cream upper arm, elbow, forearm, and a small HAND curled as if gripping a rung (back of the hand visible). The same size and colours as the attached arm. Simple, toy-like.` },
];

function run(p) {
  const out = `${RAW}/gen_${p.name}.png`;
  const args = [path.join(ROOT, "Tools/codex-image.mjs"), "--prompt", `${p.prompt}\n\n${STYLE}`, "--out", out, "--transparent", "--timeout", "420"];
  for (const r of [...p.refs, "style"]) args.push("--ref", REF[r]);
  return new Promise((res) => {
    const t0 = Date.now();
    const c = spawn(process.execPath, args, { cwd: ROOT, stdio: ["ignore", "pipe", "pipe"] });
    let o = "", e = "";
    c.stdout.on("data", (d) => (o += d)); c.stderr.on("data", (d) => (e += d));
    c.on("close", (code) => { console.log(`[${p.name}] ${code === 0 ? "ok" : "FAIL"} ${((Date.now() - t0) / 1000).toFixed(0)}s ${o.trim()} ${e.trim().slice(-300)}`); res(code === 0); });
  });
}

function writePromptsMd() {
  const lines = ["# 리그2 코덱스 파츠 프롬프트 (불 정령 ★3 시범)", "", "`node Tools/rig-web/gen_parts.mjs`가 쓴다. 공통 그림풍:", "", "> " + STYLE, "", "공통 머리말:", "", "> " + PART, ""];
  for (const p of PARTS) {
    lines.push(`## gen_${p.name} (${p.view})`, "", `- 참고 그림: ${[...p.refs, "style"].map((r) => "`" + REF[r] + "`").join(", ")}`, `- 원본: \`${RAW}/gen_${p.name}.png\``, "", "> " + p.prompt.replace(PART + " ", ""), "");
  }
  fs.writeFileSync(path.join(HERE, "parts_prompts.md"), lines.join("\n"), "utf8");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const force = args.includes("--force");
  const ji = args.indexOf("--jobs"), jobs = ji >= 0 ? Number(args[ji + 1]) : 3;
  const names = args.filter((a, i) => !a.startsWith("--") && args[i - 1] !== "--jobs");
  writePromptsMd();
  const todo = PARTS.filter((p) => (!names.length || names.includes(p.name)) && (force || !fs.existsSync(path.join(ROOT, RAW, `gen_${p.name}.png`))));
  console.log("생성할 파츠:", todo.map((p) => p.name).join(", ") || "(없음)");
  const queue = [...todo];
  await Promise.all(Array.from({ length: Math.min(jobs, queue.length) }, async () => { while (queue.length) await run(queue.shift()); }));
}
