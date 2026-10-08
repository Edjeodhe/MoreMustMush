#!/usr/bin/env node
// Builds the prefabs (round entities, character rigs, tree node, UI cards) through MCP for Unity:
// each prefab is assembled in the scene under __PrefabWork, saved to Assets/Prefabs/..., then removed.
import { Builder, call, ART, MAT, FONT, TMAT, col, R, img, txt, hlay, le, outline } from "./mcp.mjs";

const b = new Builder();
const W = "__PrefabWork";
const SR = (sprite, order, color = "#ffffff", o = {}) => ({ ...(sprite ? { sprite } : {}), sharedMaterial: o.add ? MAT.add : MAT.sprite, sortingOrder: order, color: o.color ?? col(color, o.a), ...(o.sliced ? { drawMode: "Sliced" } : {}) });
const LR = (order, width = 0.04, color = "#ffffff", a = 1) => ({ sharedMaterial: MAT.sprite, useWorldSpace: false, positionCount: 0, sortingOrder: order, widthMultiplier: width, numCapVertices: 2, startColor: col(color, a), endColor: col(color, a) });

async function save(path, prefab) {
  await b.send(path);
  const r = await call("manage_prefabs", { action: "create_from_gameobject", target: path.split("/").pop(), prefab_path: prefab, allow_overwrite: true });
  console.log(`  → ${prefab}: ${r.success ? "saved" : JSON.stringify(r).slice(0, 300)}`);
}

await call("manage_gameobject", { action: "delete", target: W, search_method: "by_name" }).catch(() => {});
b.go(W); await b.send("work root");

// ===== round entities =====
b.go(`${W}/ShroomView`, { ShroomView: {} });
b.go(`${W}/ShroomView/Pivot`);
b.go(`${W}/ShroomView/Pivot/Body`, { SpriteRenderer: SR(ART("Mushrooms/Single/ed0"), 1000) });
b.go(`${W}/ShroomView/Shadow`, { SpriteRenderer: SR(ART("FX/soft"), 900, "#000000", { a: 0.16 }) });
b.go(`${W}/ShroomView/HpBack`, { SpriteRenderer: SR(ART("FX/square"), 2950, "#000000", { a: 0.5 }) });
b.go(`${W}/ShroomView/HpFill`, { SpriteRenderer: SR(ART("FX/square"), 2951) });
b.go(`${W}/ShroomView/Cracks`, { SpriteRenderer: SR(ART("Props/crack"), 2000) });
b.go(`${W}/ShroomView/Glow`, { SpriteRenderer: SR(ART("FX/soft"), 3950, "#fffac8", { add: true, a: 0.45 }) });
await save("ShroomView", "Assets/Prefabs/Round/ShroomView.prefab");

b.go(`${W}/BallView`, { BallView: {} });
b.go(`${W}/BallView/Trail`, { LineRenderer: LR(3480, 0.09, "#ffffff", 0.2) });
b.go(`${W}/BallView/Aura`, { SpriteRenderer: SR(ART("FX/soft"), 3485, "#6ed2ff", { a: 0.18 }) });
b.go(`${W}/BallView/Fire`, { SpriteRenderer: SR(ART("FX/soft"), 3486, "#ff9a3c", { add: true, a: 0.35 }) });
b.go(`${W}/BallView/Blade`, { SpriteRenderer: SR(ART("FX/ring_thin"), 3490, "#e6e6ff", { a: 0.2 }) });
b.go(`${W}/BallView/Body`, { SpriteRenderer: SR(ART("Harvesters/sam"), 3500) });
for (let i = 0; i < 4; i++) b.go(`${W}/BallView/Debuff${i}`, { SpriteRenderer: SR(ART("FX/circle"), 3510, "#aa6edc", { a: 0.9 }) }, { scale: [0.08, 0.08, 1] });
await save("BallView", "Assets/Prefabs/Round/BallView.prefab");

b.go(`${W}/DeviceView`, { DeviceView: {} });
for (const n of ["A", "B", "C"]) b.go(`${W}/DeviceView/${n}`, { SpriteRenderer: SR(ART("Props/stump"), 150) });
for (let i = 0; i < 3; i++) b.go(`${W}/DeviceView/Glows${i}`, { SpriteRenderer: SR(ART("FX/soft"), 140, "#ffe678", { add: true, a: 0.8 }) });
b.go(`${W}/DeviceView/Link`, { LineRenderer: LR(130, 0.03, "#5a3c1e", 0.3) });
await save("DeviceView", "Assets/Prefabs/Round/DeviceView.prefab");

b.go(`${W}/CloudView`, { CloudView: {} });
for (let i = 0; i < 6; i++) b.go(`${W}/CloudView/Puffs${i}`, { SpriteRenderer: SR(ART("FX/soft"), 3020, "#a078c8", { a: 0.35 }) });
await save("CloudView", "Assets/Prefabs/Round/CloudView.prefab");

b.go(`${W}/Part`, { SpriteRenderer: SR(ART("FX/circle"), 3800) });
await save("Part", "Assets/Prefabs/Round/Part.prefab");
b.go(`${W}/Flyer`, { SpriteRenderer: SR(ART("Mushrooms/Single/ed0"), 4200) });
await save("Flyer", "Assets/Prefabs/Round/Flyer.prefab");
b.go(`${W}/Line`, { LineRenderer: LR(3000, 0.04) });
await save("Line", "Assets/Prefabs/Round/Line.prefab");
b.go(`${W}/FieldText`, { RectTransform: R(0, 0, 900, 80, { pivot: [0.5, 0.5] }), TextMeshProUGUI: txt("+1", 20, "#ffffff", { mat: TMAT.dark }) });
await save("FieldText", "Assets/Prefabs/Round/FieldText.prefab");

// ===== mushroom critter rig =====
// Laid out in critter-sheet pixels (origin = critter center, y up), then scaled: the prototype critter
// (r = 50 px) is ~94 px tall, the assembled parts ~500 sheet px → 0.0019 units per sheet pixel.
{
  const K = 0.0019, s = K * 100;   // sprite scale for PPU 100
  const p = (x, y) => [x * K, y * K, 0];
  const C = `${W}/CritterRig`;
  b.go(C, { CritterRig: { baseRadius: 50 }, SortingGroup: { sortingOrder: 2200 } });
  b.go(`${C}/Root`);
  b.go(`${C}/Root/FootL`, { SpriteRenderer: SR(ART("Characters/Critters/foot"), 0) }, { pos: p(-70, -195), scale: [s, s, 1] });
  b.go(`${C}/Root/FootR`, { SpriteRenderer: SR(ART("Characters/Critters/foot"), 0) }, { pos: p(70, -195), scale: [-s, s, 1] });
  b.go(`${C}/Root/Body`, { SpriteRenderer: SR(ART("Characters/Critters/body"), 1) }, { pos: p(0, -80), scale: [s, s, 1] });
  b.go(`${C}/Root/Blush`, { SpriteRenderer: SR(ART("Characters/Critters/blush"), 2) }, { pos: p(0, -112), scale: [s * 0.8, s * 0.8, 1] });
  b.go(`${C}/Root/Eyes`, { SpriteRenderer: SR(ART("Characters/Critters/eyes_open"), 3) }, { pos: p(0, -62), scale: [s * 0.62, s * 0.62, 1] });
  b.go(`${C}/Root/Mouth`, { SpriteRenderer: SR(ART("Characters/Critters/mouth_smile"), 3) }, { pos: p(0, -128), scale: [s * 0.45, s * 0.45, 1] });
  b.go(`${C}/Root/Cap`, {}, { pos: p(0, 20) });
  b.go(`${C}/Root/Cap/CapSprite`, { SpriteRenderer: SR(ART("Characters/Critters/cap_fire"), 4) }, { pos: p(0, 100), scale: [s, s, 1] });
  await save("CritterRig", "Assets/Prefabs/Characters/CritterRig.prefab");
}

// ===== grandpa rig =====
// Built from the reference illustration instead: Tools/art/build_grandpa_rig.py cuts the parts, and the prefab
// (Assets/Prefabs/Characters/GrandpaRig.prefab) is laid out from its rig.json through MCP. Not rebuilt here.

// ===== mycelium tree node =====
{
  const N = `${W}/TreeNode`;
  const T3 = (text, size, color = "#fff6e0") => ({ text, fontSize: size, alignment: "Center", color: col(color), textWrappingMode: "NoWrap", fontSharedMaterial: TMAT.dark, sortingOrder: 5040 });
  b.go(N, { TreeNodeView: { nodeId: "ed_score" } });
  b.go(`${N}/Link`, { LineRenderer: LR(5000, 0.06, "#f2e6c8", 0.5) });
  b.go(`${N}/Glow`, { SpriteRenderer: SR(ART("FX/soft"), 5010, "#4fb4ff", { a: 0.4 }) });
  b.go(`${N}/Pulse`, { SpriteRenderer: SR(ART("FX/ring_thin"), 5011, "#4fb4ff", { a: 0.5 }) });
  b.go(`${N}/Body`, { SpriteRenderer: SR(ART("FX/circle"), 5020, "#2e2016") }, { scale: [0.48, 0.48, 1] });
  b.go(`${N}/Border`, { SpriteRenderer: SR(ART("FX/ring"), 5021, "#4fb4ff") }, { scale: [0.52, 0.52, 1] });
  for (let i = 0; i < 5; i++) b.go(`${N}/Level${i}`, { LineRenderer: LR(5022, 0.03, "#f2e6c8") });
  b.go(`${N}/Inner`, { SpriteRenderer: SR(ART("Icons/ed"), 5023) }, { scale: [0.1, 0.1, 1] });
  b.go(`${N}/Badge`, { SpriteRenderer: SR(ART("FX/circle"), 5030, "#4fb4ff") }, { pos: [0.19, 0.19, -0.01], scale: [0.18, 0.18, 1] });
  b.go(`${N}/Badge/Text`, { TextMeshPro: { ...T3("▲", 6, "#ffffff"), sortingOrder: 5031 } }, { pos: [0, 0, -0.01] });
  b.go(`${N}/Label`, { TextMeshPro: T3("노드", 1.8) }, { pos: [0, -0.38, 0] });
  b.go(`${N}/Line2`, { TextMeshPro: T3("Lv 0/5 · 16G", 1.3, "#9fd6ff") }, { pos: [0, -0.56, 0] });
  b.go(`${N}/QMark`, { TextMeshPro: T3("?", 2.4, "#dcd2be") });
  await save("TreeNode", "Assets/Prefabs/Tree/TreeNode.prefab");
}

// ===== UI cards =====
{
  const C = `${W}/NewCard`;
  b.go(C, { RectTransform: R(0, 0, 220, 70), Image: img(ART("UI/panel_paper"), { type: "Sliced" }), Outline: outline("#8ac06a", 3), HorizontalLayoutGroup: hlay({ gap: 6, pad: { left: 10, right: 14, top: 6, bottom: 6 }, cw: false, ch: false }), ContentSizeFitter: { horizontalFit: "PreferredSize", verticalFit: "Unconstrained" }, NewCard: {} });
  b.go(`${C}/Icon`, { RectTransform: R(0, 0, 56, 56), Image: img(ART("Mushrooms/Single/ed0"), { aspect: true }), LayoutElement: le({ w: 56, h: 56 }) });
  b.go(`${C}/Name`, { TextMeshProUGUI: txt("양송이", 20), LayoutElement: le({}), ContentSizeFitter: { horizontalFit: "PreferredSize", verticalFit: "Unconstrained" } });
  b.go(`${C}/Tag`, { RectTransform: R(0, 0, 60, 22, { anchor: [1, 1], pivot: [1, 0.5] }), Image: img(ART("UI/btn_danger"), { type: "Sliced", color: col("#e8452c") }), LayoutElement: le({ ignore: true }) });
  b.go(`${C}/Tag/Text`, { RectTransform: { anchorMin: [0, 0], anchorMax: [1, 1], sizeDelta: [0, 0] }, TextMeshProUGUI: txt("NEW", 13, "#ffffff") });
  await save("NewCard", "Assets/Prefabs/UI/NewCard.prefab");

  const K = `${W}/CodexCell`;
  b.go(K, { RectTransform: R(0, 0, 88, 98), Image: img(ART("UI/panel_paper"), { type: "Sliced", color: col("#f3ead8"), ray: true }), Outline: outline("#d9c6a2", 3), Button: {}, UIAction: { act: "codexsel" }, CodexCell: {} });
  b.raw("manage_components", { action: "add", target: K, search_method: "by_path", component_type: "Outline" });
  b.go(`${K}/Icon`, { RectTransform: R(14, 4, 60, 60), Image: img(ART("Mushrooms/Single/ed0"), { aspect: true }) });
  b.go(`${K}/Name`, { RectTransform: R(2, 64, 84, 20), TextMeshProUGUI: txt("양송이", 12, "#3b2414", { font: FONT.noto }) });
  b.go(`${K}/Stars`, { RectTransform: R(0, 82, 88, 14), TextMeshProUGUI: txt("★★★★★", 11, "#e8a900") });
  b.go(`${K}/ThemeMark`, { RectTransform: R(66, 78, 20, 18), TextMeshProUGUI: txt("", 12) });
  b.go(`${K}/GoldStar`, { RectTransform: R(64, 0, 22, 22), TextMeshProUGUI: txt("★", 20, "#e8a900") });
  b.go(`${K}/GiantMark`, { RectTransform: R(4, 4, 22, 18), Image: img(ART("FX/square"), { color: col("#7a4f2e") }) });
  b.go(`${K}/GiantMark/Text`, { RectTransform: { anchorMin: [0, 0], anchorMax: [1, 1], sizeDelta: [0, 0] }, TextMeshProUGUI: txt("巨", 14, "#ffffff", { font: FONT.noto }) });
  await save("CodexCell", "Assets/Prefabs/UI/CodexCell.prefab");
}

await call("manage_gameobject", { action: "delete", target: W, search_method: "by_name" });
console.log("prefabs done");
