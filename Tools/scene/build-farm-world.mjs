#!/usr/bin/env node
// Mushroom farm world under World/Farm (field | ranch | kitchen scenes, 64 plot slots, decorations, kitchen props,
// hidden template slots for critters / particles / floating text), built through MCP for Unity.
// Only creates World/Farm (deleting a previous one). Create positions / scales are local to the parent.
// Scene roots are built at x = 0 (so their children use stage coordinates) and moved apart at the end.
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
const PX = { counter: 366, dc_bed: 360, dc_fire: 285, dc_house: 401, dc_lamp: 309, dc_statue: 258, dc_swing: 430, dc_table: 369, dc_well: 299, dining: 382, scarecrow: 279, stove: 357,
  tile: 324, crop: 332, gem: 216, star: 227, surprise: 227, req: 202, tool: 321, spore: 185 };
const PH = { counter: 315, dc_bed: 258, dc_fire: 323, dc_house: 401 * 365 / 401, dc_lamp: 336, dc_statue: 375, dc_swing: 326, dc_table: 278, dc_well: 331, dining: 310, scarecrow: 332, stove: 346 };
const fit = (key, w) => { const s = w / PX[key]; return [s, s, 1]; };
const BG = 19.2 / 16.72;   // backgrounds are 1672 px wide

// a prop standing on its feet at (x, y): parent at the feet, sprite lifted by half its height
function prop(path, name, x, y, w, order, o = {}) {
  b.go(path, {}, { pos: P(x, y) });
  const s = w / PX[name], h = PH[name] * s;
  b.go(`${path}/Sprite`, { SpriteRenderer: SR(ART("Farm/Props/" + name), order) }, { pos: P(0, -h / 2 + (o.sink ?? 6)), scale: [s, s, 1] });
}

await destroyPaths([F]);

b.go(F, { FarmView: {} });
b.go(`${F}/Content`);
for (const n of ["Field", "Pets", "Kitchen"]) b.go(`${F}/Content/${n}`);
b.go(`${F}/PanShade`, { SpriteRenderer: SR(ART("FX/square"), 5800, "#14200a", { a: 0 }) }, { pos: P(960, 540), scale: [19.6, 11.2, 1] });
b.go(`${F}/Fx`);
await b.send("farm: roots");

// ===== field =====
const FD = `${F}/Content/Field`;
b.go(`${FD}/Background`, { SpriteRenderer: SR(ART("Backgrounds/bg_field"), 0) }, { pos: P(960, 540, 1), scale: [BG, BG, 1] });
b.raw("manage_gameobject", { action: "create", name: "FieldGrandpa", parent: FD, prefab_path: "Assets/Prefabs/Characters/GrandpaRig.prefab", position: P(330, 900), scale: [0.5, 0.5, 1] });
prop(`${FD}/Scarecrow`, "scarecrow", 1121, 930, 170, 1925);
// frames: sliced wood, scaled down so the 9-slice border reads ~14 px
b.go(`${FD}/FieldNext`);
for (let i = 0; i < 4; i++) b.go(`${FD}/FieldNext/NextEdges${i}`, { SpriteRenderer: SR(ART("FX/square"), 89, "#ffffff", { a: 0.7 }) }, { scale: i < 2 ? [5.9, 0.03, 1] : [0.03, 5.9, 1] });
b.go(`${FD}/FieldFrame`, { SpriteRenderer: SR(ART("UI/panel_wood"), 90, "#ffffff", { sliced: true }) }, { pos: P(690, 586), scale: [1 / 7, 1 / 7, 1] });
await b.send("farm: field scene");
b.go(`${FD}/Tiles`);
for (let r = 0; r < 8; r++) {
  for (let c = 0; c < 8; c++) {
    const i = r * 8 + c, T = `${FD}/Tiles/Tiles${i}`;
    const cx = 690 + (c - 4) * 80 + 40, cy = 586 + (r - 4) * 80 + 40, k = 80 / 112;
    b.go(T, { FieldTile: {} }, { pos: P(cx, cy), scale: [k, k, 1] });
    const L = P;   // children are authored for a 112 px plot (the slot is scaled to the plot size)
    b.go(`${T}/Ground`, { SpriteRenderer: SR(ART("Farm/Field/tile_grass"), 100) }, { pos: L(0, 0), scale: fit("tile", 108) });
    b.go(`${T}/Glow`, { SpriteRenderer: SR(ART("FX/soft"), 101, "#a0ffd2", { a: 0.7 }) }, { pos: L(0, 0), scale: [1.3, 1.3, 1] });
    b.go(`${T}/Crop`, { SpriteRenderer: SR(ART("Farm/Field/crop_ed"), 103) }, { pos: L(0, -4), scale: fit("crop", 86) });
    b.go(`${T}/BarBack`, { SpriteRenderer: SR(ART("FX/square"), 104, "#000000", { a: 0.35 }) }, { pos: L(0, 46), scale: [0.92, 0.06, 1] });
    b.go(`${T}/BarFill`, { SpriteRenderer: SR(ART("FX/square"), 105, "#f28c28") }, { pos: L(0, 46), scale: [0.5, 0.06, 1] });
    b.go(`${T}/Gem`, { SpriteRenderer: SR(ART("Icons/gem"), 106) }, { pos: L(41, -41), scale: fit("gem", 26) });
    b.go(`${T}/Busy`, { SpriteRenderer: SR(ART("FX/square"), 107, "#ffe36e", { a: 0.22 }) }, { pos: L(0, 0), scale: [1.04, 1.04, 1] });
    b.go(`${T}/BusyIcon`, { SpriteRenderer: SR(ART("Farm/Field/tool_0"), 108) }, { pos: L(-36, -36), scale: fit("tool", 30) });
    b.go(`${T}/Hover`, { SpriteRenderer: SR(ART("FX/square"), 109, "#fff6b0", { a: 0.28 }) }, { pos: L(0, 0), scale: [1.04, 1.04, 1] });
  }
  await b.send(`farm: tiles row ${r}`);
}
b.go(`${FD}/TileHint`, {}, { pos: P(690, 200) });
b.go(`${FD}/TileHint/TileHintBack`, { SpriteRenderer: SR(ART("UI/panel_paper"), 5200, "#28190f", { a: 0.85, sliced: true }) }, { scale: [1 / 6, 1 / 6, 1] });
b.go(`${FD}/TileHint/TileHintText`, { TextMeshPro: TMP("갈기", 22, "#fff6e0", 5201) });
await b.send("farm: tile hint");

// ===== ranch =====
const PT = `${F}/Content/Pets`;
b.go(`${PT}/Background`, { SpriteRenderer: SR(ART("Backgrounds/bg_ranch"), 0) }, { pos: P(960, 540, 1), scale: [BG, BG, 1] });
b.raw("manage_gameobject", { action: "create", name: "PetsGrandpa", parent: PT, prefab_path: "Assets/Prefabs/Characters/GrandpaRig.prefab", position: P(150, 960), scale: [0.55, 0.55, 1] });
const DECOR = [["dc_table", 620, 440, 300], ["dc_fire", 1290, 450, 150], ["dc_lamp", 960, 400, 280], ["dc_bed", 400, 430, 200], ["dc_swing", 470, 790, 280], ["dc_well", 1450, 790, 190], ["dc_house", 1530, 420, 240], ["dc_statue", 960, 600, 130]];
DECOR.forEach(([id, x, y, w], i) => prop(`${PT}/Decor${i}`, id, x, y, w, 1000 + y - 4));
await b.send("farm: ranch scene");

// ===== kitchen =====
const KT = `${F}/Content/Kitchen`;
b.go(`${KT}/Background`, { SpriteRenderer: SR(ART("Backgrounds/bg_kitchen"), 0) }, { pos: P(960, 540, 1), scale: [BG, BG, 1] });
prop(`${KT}/Stove`, "stove", 420, 800, 340, 1790);
b.go(`${KT}/StoveGlow`, { SpriteRenderer: SR(ART("FX/soft"), 1791, "#ff9a3c", { add: true, a: 0.35 }) }, { pos: P(430, 700), scale: [2.4, 1.6, 1] });
prop(`${KT}/Counter`, "counter", 775, 800, 260, 1790);
prop(`${KT}/Dining`, "dining", 980, 945, 330, 1935);
b.go(`${KT}/Dishes`);
for (let i = 0; i < 10; i++) b.go(`${KT}/Dishes/Dishes${i}`, { SpriteRenderer: SR(ART("Farm/Icons/dish_soup"), 1940 + i) }, { pos: P(870 + (i % 5) * 54, 820 + Math.floor(i / 5) * 34), scale: [0.2, 0.2, 1] });
b.go(`${KT}/Steam`);
for (let i = 0; i < 6; i++) b.go(`${KT}/Steam/Steam${i}`, { SpriteRenderer: SR(ART("FX/circle"), 1800, "#ffffff", { a: 0.4 }) }, { pos: P(380 + i * 16, 470), scale: [0.3, 0.3, 1] });
await b.send("farm: kitchen scene");

// ===== template slots (hidden, cloned by FarmView) =====
const TP = `${F}/Templates`;
b.go(TP);
b.go(`${F}/Fx/PartTemplate`, { SpriteRenderer: SR(ART("Icons/gem"), 5500) });
b.go(`${F}/Fx/FloatTemplate`, { FarmFloat: {} });
b.go(`${F}/Fx/FloatTemplate/Text`, { TextMeshPro: TMP("+1", 34, "#a8f5d4", 5601, { mat: TMAT.dark }) });
b.go(`${F}/Fx/FloatTemplate/Gem`, { SpriteRenderer: SR(ART("Icons/gem"), 5600) }, { pos: P(-40, 0), scale: fit("gem", 32) });
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

// sliced sizes, critter template off, scene roots apart
const cmds = [
  ["TileHint/TileHintBack", `${FD}/TileHint/TileHintBack`, [120, 32]],
  ["LabelBack", `${C}/Label/LabelBack`, [160, 30]], ["SayBack", `${C}/Say/SayBack`, [200, 50]], ["ReqBack", `${C}/Req/ReqBack`, [72, 62]],
  ["FieldFrame", `${FD}/FieldFrame`, [668, 668]],
].map(([, path, [w, h]]) => {
  const k = path.endsWith("Frame") ? 7 : 6;
  return { tool: "manage_components", params: { action: "set_property", target: path, search_method: "by_path", component_type: "SpriteRenderer", property: "size", value: [w / 100 * k, h / 100 * k] } };
});
cmds.push({ tool: "manage_components", params: { action: "set_property", target: `${F}/Content/Field`, search_method: "by_path", component_type: "Transform", property: "localPosition", value: [-19.2, 0, 0] } });
cmds.push({ tool: "manage_components", params: { action: "set_property", target: `${F}/Content/Kitchen`, search_method: "by_path", component_type: "Transform", property: "localPosition", value: [19.2, 0, 0] } });
const r = await call("batch_execute", { commands: cmds, fail_fast: false });
console.log("[farm: sizes + layout]", (r?.data?.results ?? []).map((x) => (x.result?.success ? "ok" : x.result?.error ?? "?")).join(" "));
await call("manage_gameobject", { action: "modify", target: TP, search_method: "by_path", set_active: false });
for (const t of [`${F}/Fx/PartTemplate`, `${F}/Fx/FloatTemplate`]) await call("manage_gameobject", { action: "modify", target: t, search_method: "by_path", set_active: false });
await call("manage_gameobject", { action: "modify", target: F, search_method: "by_path", set_active: false });
console.log("farm world built");
