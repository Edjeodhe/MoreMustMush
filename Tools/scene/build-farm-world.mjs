#!/usr/bin/env node
// Mushroom farm world under World/Farm: one ranch screen with the field inside, 64 plot slots, the placement ghost,
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
const PX = { scarecrow: 279, tile: 324, crop: 332, gem: 216, star: 227, surprise: 227, req: 202, tool: 321, hammer: 407 };
const fit = (key, w) => { const s = w / PX[key]; return [s, s, 1]; };
const BG = 19.2 / 16.72;   // backgrounds are 1672 px wide
const FIELD = { cx: 600, cy: 600 };

await destroyPaths([F]);

b.go(F, { FarmView: {} });
b.go(`${F}/Background`, { SpriteRenderer: SR(ART("Backgrounds/bg_ranch"), 0) }, { pos: P(960, 540, 1), scale: [BG, BG, 1] });
b.raw("manage_gameobject", { action: "create", name: "Grandpa", parent: F, prefab_path: "Assets/Prefabs/Characters/GrandpaRig.prefab", position: P(150, 960), scale: [0.55, 0.55, 1] });
b.go(`${F}/Scarecrow`, {}, { pos: P(330, 905) });
b.go(`${F}/Scarecrow/Sprite`, { SpriteRenderer: SR(ART("Farm/Props/scarecrow"), 1899) }, { pos: P(0, -95), scale: fit("scarecrow", 160) });
// field frames
b.go(`${F}/FieldNext`);
for (let i = 0; i < 4; i++) b.go(`${F}/FieldNext/NextEdges${i}`, { SpriteRenderer: SR(ART("FX/square"), 89, "#ffffff", { a: 0.7 }) }, { scale: i < 2 ? [4.7, 0.03, 1] : [0.03, 4.7, 1] });
b.go(`${F}/FieldFrame`, { SpriteRenderer: SR(ART("UI/panel_wood"), 90, "#ffffff", { sliced: true }) }, { pos: P(FIELD.cx, FIELD.cy), scale: [1 / 7, 1 / 7, 1] });
await b.send("farm: scene");

b.go(`${F}/Tiles`);
for (let r = 0; r < 8; r++) {
  for (let c = 0; c < 8; c++) {
    const i = r * 8 + c, T = `${F}/Tiles/Tiles${i}`;
    const cx = FIELD.cx + (c - 4) * 55 + 27.5, cy = FIELD.cy + (r - 4) * 55 + 27.5, k = 55 / 112;
    b.go(T, { FieldTile: {} }, { pos: P(cx, cy), scale: [k, k, 1] });
    // children are authored for a 112 px plot (the slot is scaled to the plot size)
    b.go(`${T}/Ground`, { SpriteRenderer: SR(ART("Farm/Field/tile_grass"), 100) }, { scale: fit("tile", 108) });
    b.go(`${T}/Glow`, { SpriteRenderer: SR(ART("FX/soft"), 101, "#a0ffd2", { a: 0.7 }) }, { scale: [1.3, 1.3, 1] });
    b.go(`${T}/Crop`, { SpriteRenderer: SR(ART("Farm/Field/crop_ed"), 103) }, { pos: P(0, -4), scale: fit("crop", 86) });
    b.go(`${T}/BarBack`, { SpriteRenderer: SR(ART("FX/square"), 104, "#000000", { a: 0.35 }) }, { pos: P(0, 46), scale: [0.92, 0.06, 1] });
    b.go(`${T}/BarFill`, { SpriteRenderer: SR(ART("FX/square"), 105, "#f28c28") }, { pos: P(0, 46), scale: [0.5, 0.06, 1] });
    b.go(`${T}/Gem`, { SpriteRenderer: SR(ART("Icons/gem"), 106) }, { pos: P(41, -41), scale: fit("gem", 26) });
    b.go(`${T}/Busy`, { SpriteRenderer: SR(ART("FX/square"), 107, "#ffe36e", { a: 0.22 }) }, { scale: [1.04, 1.04, 1] });
    b.go(`${T}/BusyIcon`, { SpriteRenderer: SR(ART("Farm/Field/tool_0"), 108) }, { pos: P(-36, -36), scale: fit("tool", 30) });
    b.go(`${T}/Hover`, { SpriteRenderer: SR(ART("FX/square"), 109, "#fff6b0", { a: 0.28 }) }, { scale: [1.04, 1.04, 1] });
  }
  await b.send(`farm: tiles row ${r}`);
}
b.go(`${F}/TileHint`, {}, { pos: P(FIELD.cx, 300) });
b.go(`${F}/TileHint/TileHintBack`, { SpriteRenderer: SR(ART("UI/panel_paper"), 5200, "#28190f", { a: 0.85, sliced: true }) }, { scale: [1 / 6, 1 / 6, 1] });
b.go(`${F}/TileHint/TileHintText`, { TextMeshPro: TMP("갈기", 22, "#fff6e0", 5201) });

// layers (clones go here) and the placement ghost
b.go(`${F}/BuildLayer`);
b.go(`${F}/CritterLayer`);
b.go(`${F}/Ghost`, {}, { pos: P(1180, 560) });
b.go(`${F}/Ghost/GhostFoot`, { SpriteRenderer: SR(ART("FX/square"), 3990, "#73ff80", { a: 0.38 }) }, { pos: [0, 0.6, 0], scale: [2.4, 1.2, 1] });
b.go(`${F}/Ghost/GhostSprite`, { SpriteRenderer: SR(ART("Farm/Props/dc_table"), 4000, "#ffffff", { a: 0.7 }) }, { scale: [0.65, 0.65, 1] });
b.go(`${F}/Fx`);
await b.send("farm: hint, layers, ghost");

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
  [`${F}/TileHint/TileHintBack`, 120, 32, 6], [`${BT}/Site/TimerBack`, 140, 36, 6],
  [`${C}/Label/LabelBack`, 160, 30, 6], [`${C}/Say/SayBack`, 200, 50, 6], [`${C}/Req/ReqBack`, 72, 62, 6],
  [`${F}/FieldFrame`, 468, 468, 7],
].map(([path, w, h, k]) => ({ tool: "manage_components", params: { action: "set_property", target: path, search_method: "by_path", component_type: "SpriteRenderer", property: "size", value: [w / 100 * k, h / 100 * k] } }));
const r = await call("batch_execute", { commands: sliced, fail_fast: false });
console.log("[farm: sliced sizes]", (r?.data?.results ?? []).map((x) => (x.result?.success ? "ok" : x.result?.error ?? "?")).join(" "));
for (const t of [TP, `${F}/Fx/PartTemplate`, `${F}/Fx/FloatTemplate`, `${F}/Ghost`, F])
  await call("manage_gameobject", { action: "modify", target: t, search_method: "by_path", set_active: false });
console.log("farm world built");
