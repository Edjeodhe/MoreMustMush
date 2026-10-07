#!/usr/bin/env node
// Mushroom farm world under World/Farm: one ranch screen with the mushroom tree inside (13 mushroom slots), the
// buildable-area grid and placement ghost (24 footprint tiles),
// and hidden template slots for critters / buildings / particles / floating text. Built through MCP for Unity.
// Only creates World/Farm (deleting a previous one). Create positions / scales are local to the parent.
import { Builder, call, destroyPaths, ART, MAT, FONT, TMAT, col } from "./mcp.mjs";

const b = new Builder();
// children default to the parent's origin (an omitted position would land at the world origin)
b.go = ((orig) => function (path, comps = {}, o = {}) { return orig.call(this, path, comps, { pos: [0, 0, 0], ...o }); })(b.go);
const F = "World/Farm";
const P = (x, y, z = 0) => [x / 100, -y / 100, z];
const SR = (sprite, order, color = "#ffffff", o = {}) => ({ ...(sprite ? { sprite } : {}), sharedMaterial: o.add ? MAT.add : MAT.sprite, sortingOrder: order, color: col(color, o.a), ...(o.sliced ? { drawMode: "Sliced" } : {}) });
const TMP = (text, size, color, order, o = {}) => ({ text, fontSize: size / 10, alignment: o.align ?? "Center", color: col(color), textWrappingMode: "NoWrap", richText: true,
  font: o.font ?? FONT.jua, ...(o.mat ? { fontSharedMaterial: o.mat } : {}), sortingOrder: order });
// sprite pixel widths (PPU 100) → scale for a target width in stage pixels
const PX = { gem: 216, star: 227, surprise: 227, req: 202, tool: 321, hammer: 407, tree: 369, fruit: 563 };
const fit = (key, w) => { const s = w / PX[key]; return [s, s, 1]; };
const BG = 19.2 / 16.72;   // backgrounds are 1672 px wide
const TREE = { x: 570, y: 790 };
const GRID = { x0: 120, y0: 260, cell: 40, cols: 42, rows: 16 };

await destroyPaths([F]);

b.go(F, { FarmView: {} });
b.go(`${F}/Background`, { SpriteRenderer: SR(ART("Backgrounds/bg_ranch"), 0) }, { pos: P(960, 540, 1), scale: [BG, BG, 1] });
b.raw("manage_gameobject", { action: "create", name: "Grandpa", parent: F, prefab_path: "Assets/Prefabs/Characters/GrandpaRig.prefab", position: P(150, 960), scale: [0.55, 0.55, 1] });
// the mushroom tree (sits at the trunk base): stage picture, 13 mushroom slots with ripe glows, level sign
const T = `${F}/Tree`;
b.go(T, { FarmTreeView: {} }, { pos: P(TREE.x, TREE.y) });
b.go(`${T}/Shadow`, { SpriteRenderer: SR(ART("FX/soft"), 1840, "#1e3c14", { a: 0.3 }) }, { scale: [3.6, 0.6, 1] });
b.go(`${T}/TreeSprite`, { SpriteRenderer: SR(ART("Farm/Tree/tree_2"), 1850) }, { pos: [0, 2.1, 0], scale: fit("tree", 330) });
for (let i = 0; i < 13; i++) {
  b.go(`${T}/Glows${i}`, { SpriteRenderer: SR(ART("FX/soft"), 1851, "#fff2a0", { add: true, a: 0.55 }) }, { pos: [0, 2 + i * 0.1, 0], scale: [0.6, 0.6, 1] });
  b.go(`${T}/Fruits${i}`, { SpriteRenderer: SR(ART("Farm/Tree/fruit_ed"), 1852) }, { pos: [0, 2 + i * 0.1, 0], scale: fit("fruit", 44) });
}
b.go(`${T}/Sign`, {}, { pos: P(120, -20) });
b.go(`${T}/Sign/SignBack`, { SpriteRenderer: SR(ART("UI/panel_wood"), 1860, "#c89060", { sliced: true }) }, { scale: [1 / 7, 1 / 7, 1] });
b.go(`${T}/Sign/SignText`, { TextMeshPro: TMP("Lv.1", 22, "#fff6e0", 1861, { mat: TMAT.dark }) });
await b.send("farm: scene + tree");

// layers (clones go here), buildable-area grid and the placement ghost (24 footprint tiles = largest building 6×4)
b.go(`${F}/BuildLayer`);
b.go(`${F}/CritterLayer`);
b.go(`${F}/BuildGrid`, { SpriteRenderer: SR(ART("FX/build_grid"), 80, "#c8ffb0", { a: 0.8 }) }, { pos: P(GRID.x0 + GRID.cols * GRID.cell / 2, GRID.y0 + GRID.rows * GRID.cell / 2), scale: [GRID.cols * GRID.cell / 100, GRID.cols * GRID.cell / 100, 1] });   // FX sprites import 1 unit wide
b.go(`${F}/Ghost`, {}, { pos: P(1180, 560) });
b.go(`${F}/Ghost/GhostSprite`, { SpriteRenderer: SR(ART("Farm/Props/dc_table"), 4000, "#ffffff", { a: 0.75 }) }, { scale: [0.65, 0.65, 1] });
for (let i = 0; i < 24; i++) b.go(`${F}/Ghost/GhostTiles${i}`, { SpriteRenderer: SR(ART("FX/square"), 3990, "#59ff73", { a: 0.5 }) }, { pos: [(i % 6) * 0.4, (i / 6 | 0) * 0.4, 0], scale: [0.36, 0.36, 1] });
b.go(`${F}/Fx`);
await b.send("farm: layers, grid, ghost");

// ===== template slots (hidden, cloned by FarmView) =====
const TP = `${F}/Templates`;
b.go(TP);
b.go(`${F}/Fx/PartTemplate`, { SpriteRenderer: SR(ART("Icons/gem"), 5500) });
b.go(`${F}/Fx/FloatTemplate`, { FarmFloat: {} });
b.go(`${F}/Fx/FloatTemplate/Text`, { TextMeshPro: TMP("+1", 34, "#a8f5d4", 5601, { mat: TMAT.dark }) });
b.go(`${F}/Fx/FloatTemplate/Gem`, { SpriteRenderer: SR(ART("Icons/gem"), 5600) }, { pos: P(-40, 0), scale: fit("gem", 32) });
// building: the object sits at the bottom-center of its footprint
const BT = `${TP}/BuildingTemplate`;
b.go(BT, { BuildingView: {} });
b.go(`${BT}/Shadow`, { SpriteRenderer: SR(ART("FX/soft"), 899, "#1e3c14", { a: 0.3 }) }, { scale: [2.4, 0.3, 1] });
b.go(`${BT}/Sprite`, { SpriteRenderer: SR(ART("Farm/Props/dc_table"), 1000) }, { scale: [0.65, 0.65, 1] });
b.go(`${BT}/Site`, {}, { pos: P(0, -190) });
b.go(`${BT}/Site/TimerBack`, { SpriteRenderer: SR(ART("UI/panel_paper"), 5040, "#28190f", { a: 0.85, sliced: true }) }, { scale: [1 / 6, 1 / 6, 1] });
b.go(`${BT}/Site/Timer`, { TextMeshPro: TMP("0:00", 22, "#fff6e0", 5043) }, { pos: P(14, -2) });
b.go(`${BT}/Site/Hammer`, { SpriteRenderer: SR(ART("Icons/hammer"), 5044) }, { pos: P(-48, -2), scale: fit("hammer", 30) });
b.go(`${BT}/Site/BarBack`, { SpriteRenderer: SR(ART("FX/square"), 5041, "#000000", { a: 0.5 }) }, { pos: P(0, 26), scale: [1.2, 0.08, 1] });
b.go(`${BT}/Site/BarFill`, { SpriteRenderer: SR(ART("FX/square"), 5042, "#ffd84a") }, { pos: P(0, 26), scale: [0.6, 0.08, 1] });
// critter: the object sits at the feet
const C = `${TP}/CritterTemplate`;
b.go(C, { FarmCritter: {} });
b.go(`${C}/Shadow`, { SpriteRenderer: SR(ART("FX/circle"), 900, "#1e3c14", { a: 0.28 }) }, { scale: [0.66, 0.2, 1] });
b.go(`${C}/Glow`, { SpriteRenderer: SR(ART("FX/soft"), 901, "#a0ffd2", { a: 0.45 }) }, { scale: [1.1, 0.4, 1] });
b.go(`${C}/Body`);
b.raw("manage_gameobject", { action: "create", name: "Rig", parent: `${C}/Body`, prefab_path: "Assets/Prefabs/Characters/CritterRig.prefab", position: [0, 0.475, 0] });
for (let i = 0; i < 3; i++) b.go(`${C}/Stars${i}`, { SpriteRenderer: SR(ART("Icons/star"), 1001) }, { scale: fit("star", 20) });
b.go(`${C}/Surprise`, { SpriteRenderer: SR(ART("Farm/Icons/fx_surprise"), 5050) }, { pos: P(34, -90), scale: fit("surprise", 50) });
b.go(`${C}/Tool`, { SpriteRenderer: SR(ART("Farm/Field/tool_0"), 1002) }, { pos: P(33, -24), scale: fit("tool", 57) });
b.go(`${C}/Label`, {}, { pos: P(0, 26) });
b.go(`${C}/Label/LabelBack`, { SpriteRenderer: SR(ART("UI/panel_paper"), 5080, "#28190f", { a: 0.8, sliced: true }) }, { scale: [1 / 6, 1 / 6, 1] });
b.go(`${C}/Label/LabelText`, { TextMeshPro: TMP("꼬마 ♥♥♡♡♡", 22, "#fff6e0", 5081) });
b.go(`${C}/Say`, {}, { pos: P(0, -191) });
b.go(`${C}/Say/SayTail`, { SpriteRenderer: SR(ART("FX/square"), 5099, "#fffdf6") }, { pos: P(0, 33), rot: [0, 0, 45], scale: [0.16, 0.16, 1] });
b.go(`${C}/Say/SayBack`, { SpriteRenderer: SR(ART("UI/panel_paper"), 5100, "#fffdf6", { sliced: true }) }, { scale: [1 / 6, 1 / 6, 1] });
b.go(`${C}/Say/SayText`, { TextMeshPro: TMP("안녕!", 22, "#3b2414", 5101, { font: FONT.noto }) });
b.go(`${C}/Req`, {}, { pos: P(0, -193) });
b.go(`${C}/Req/ReqTail`, { SpriteRenderer: SR(ART("FX/square"), 5059, "#ffffff") }, { pos: P(0, 31), rot: [0, 0, 45], scale: [0.15, 0.15, 1] });
b.go(`${C}/Req/ReqBack`, { SpriteRenderer: SR(ART("UI/panel_paper"), 5060, "#ffffff", { sliced: true }) }, { scale: [1 / 6, 1 / 6, 1] });
b.go(`${C}/Req/ReqIcon`, { SpriteRenderer: SR(ART("Farm/Icons/req_water"), 5061) }, { scale: fit("req", 44) });
b.go(`${C}/Req/ReqDot`, { SpriteRenderer: SR(ART("FX/circle"), 5062, "#e8453c") }, { pos: P(32, -27), scale: [0.24, 0.24, 1] });
b.go(`${C}/Req/ReqMark`, { TextMeshPro: TMP("!", 20, "#ffffff", 5063) }, { pos: P(32, -27) });
await b.send("farm: templates");

// sliced sizes (in the scaled-down space), then hide templates and the ghost
const sliced = [
  [`${T}/Sign/SignBack`, 84, 36, 7], [`${BT}/Site/TimerBack`, 140, 36, 6],
  [`${C}/Label/LabelBack`, 160, 30, 6], [`${C}/Say/SayBack`, 200, 50, 6], [`${C}/Req/ReqBack`, 72, 62, 6],
].map(([path, w, h, k]) => ({ tool: "manage_components", params: { action: "set_property", target: path, search_method: "by_path", component_type: "SpriteRenderer", property: "size", value: [w / 100 * k, h / 100 * k] } }));
const r = await call("batch_execute", { commands: sliced, fail_fast: false });
console.log("[farm: sliced sizes]", (r?.data?.results ?? []).map((x) => (x.result?.success ? "ok" : x.result?.error ?? "?")).join(" "));
for (const t of [TP, `${F}/Fx/PartTemplate`, `${F}/Fx/FloatTemplate`, `${F}/Ghost`, `${F}/BuildGrid`, F])
  await call("manage_gameobject", { action: "modify", target: t, search_method: "by_path", set_active: false });
console.log("farm world built");
