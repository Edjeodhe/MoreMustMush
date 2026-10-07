#!/usr/bin/env node
// Skin shop modal under Canvas/Modal (10 skin cards with before ▶ after previews), built through MCP for Unity.
// Only creates Canvas/Modal/SkinPanel (deleting a previous one).
import { Builder, call, ART, col, R, STRETCH, hlay, vlay, le } from "./mcp.mjs";
import { makeKit, INK, PAPER, ic } from "./ui-kit.mjs";

const b = new Builder();
const k = makeKit(b);
const P = "Canvas/Modal/SkinPanel";
const created = [];
b.go = ((orig) => function (path, comps = {}, o = {}) { created.push([path, comps.RectTransform?.anchoredPosition]); return orig.call(this, path, comps, o); })(b.go);
const go = (path, comps = {}, o = {}) => b.go(path, comps, o);
const box = (path, o = {}) => k.image(path, PAPER, { type: "Sliced", color: col(o.bg ?? "#fffaf0"), outline: o.border ?? "#cdb68e", ow: o.ow ?? 3, le: o.le, rect: o.rect, extra: o.extra });
const C = (n) => ART("Characters/Critters/" + n);
// centered rect at (x, y) (y up) inside a center-anchored parent
const CR = (x, y, w, h, flip) => ({ anchorMin: [0.5, 0.5], anchorMax: [0.5, 0.5], pivot: [0.5, 0.5], anchoredPosition: [x, y], sizeDelta: [w, h], ...(flip ? { localScale: [-1, 1, 1] } : {}) });

// UI critter at rig proportions; u = UI px per rig unit
function critter(path, cx, cy, u, kind) {
  go(path, { RectTransform: CR(cx, cy, u, u), CritterIcon: { unitPx: u } });
  const part = (n, sprite, x, y, wu, hu, flip) => k.image(`${path}/${n}`, C(sprite), { rect: CR(x * u, y * u, wu * u, hu * u, flip), aspect: true });
  part("FootL", "foot", -0.133, -0.371, 0.243, 0.201);
  part("FootR", "foot", 0.133, -0.371, 0.243, 0.201, true);
  part("Body", "body", 0, -0.152, 0.507, 0.418);
  part("Blush", "blush", 0, -0.213, 0.397, 0.119);
  part("Eyes", "eyes_open", 0, -0.118, 0.33, 0.137);
  part("Mouth", "mouth_smile", 0, -0.243, 0.11, 0.058);
  part("Cap", "cap_" + kind, 0, 0.228, 0.616, 0.524);
  part("Acc", "Acc/bandana", 0, 0.1, 0.66, 0.4);
}

await call("manage_gameobject", { action: "delete", target: P, search_method: "by_path" });

k.panel(P, 1500, { h: 900, gap: 10, pad: { left: 36, right: 36, top: 26, bottom: 24 }, comps: { SkinPanel: {} } });
k.h2(`${P}/Title`, `${ic("star")} 스킨 상점`);
k.body(`${P}/Sub`, "균사석으로 꼬마와 수확기의 모습을 바꿔요. 산 스킨은 언제든 입고 벗을 수 있어요. 꼬마 스킨은 목장·밭·식당에, 수확기 스킨은 라운드의 핀볼에 보여요. (능력치는 그대로예요)", 17, "#6a5040", { le: {} });
go(`${P}/Res`, { RectTransform: R(-80, 22, 300, 48, { anchor: [1, 1], pivot: [1, 1] }), LayoutElement: le({ ignore: true }) });
k.text(`${P}/Res/Gem`, "0", 24, INK, { rect: STRETCH(), align: "Right" });
go(`${P}/Tabs`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 10, cw: false, ch: false, align: "MiddleLeft" }), LayoutElement: le({ h: 52 }) });
k.button(`${P}/Tabs/ch`, "꼬마 스킨", "skintab", { arg: "ch", rect: R(0, 0, 200, 48), size: 22 });
k.button(`${P}/Tabs/hv`, "수확기 스킨", "skintab", { arg: "hv", rect: R(0, 0, 200, 48), size: 22 });
go(`${P}/Grid`, { RectTransform: {}, GridLayoutGroup: { cellSize: [270, 320], spacing: [14, 14], constraint: "FixedColumnCount", constraintCount: 5, childAlignment: "UpperCenter" }, LayoutElement: le({ h: 660 }) });
const KINDS = ["fire", "spark", "dew", "coin", "star", "leaf", "moon", "wind", "rock", "chest"];
for (let i = 0; i < 10; i++) {
  const Cd = `${P}/Grid/Card${i}`;
  box(Cd, { rect: R(0, 0, 270, 320), extra: { SkinCard: {} } });
  go(`${Cd}/Pics`, { RectTransform: R(0, 10, 270, 150, { anchor: [0.5, 1], pivot: [0.5, 1] }) });
  critter(`${Cd}/Pics/ChBase`, -70, 0, 104, KINDS[i]);
  critter(`${Cd}/Pics/ChSkin`, 66, 4, 124, KINDS[i]);
  k.image(`${Cd}/Pics/HvBase`, ART("Harvesters/sam"), { rect: CR(-70, 0, 92, 92), aspect: true });
  k.image(`${Cd}/Pics/HvAura`, ART("FX/soft"), { rect: CR(66, 0, 150, 150) });
  k.image(`${Cd}/Pics/HvSkin`, ART("Harvesters/sam"), { rect: CR(66, 0, 108, 108), aspect: true });
  k.text(`${Cd}/Pics/Arrow`, "▶", 22, "#8a6a4a", { rect: CR(-6, 0, 30, 30) });
  k.text(`${Cd}/Title`, "스킨", 22, INK, { rect: R(0, 166, 270, 30, { anchor: [0.5, 1], pivot: [0.5, 1] }) });
  k.body(`${Cd}/Who`, "", 14, "#8a6a4a", { rect: R(0, 198, 260, 22, { anchor: [0.5, 1], pivot: [0.5, 1] }) });
  k.button(`${Cd}/Btn`, "구입", "buyskin", { style: "go", rect: R(0, 232, 220, 54, { anchor: [0.5, 1], pivot: [0.5, 1] }), size: 18 });
  if (i % 5 === 4) await b.send(`skins: cards to ${i}`);
}
k.close(P);
await b.send("skins: close");

const fixes = created.map(([path, ap]) => ({ tool: "manage_components", params: { action: "set_property", target: path, search_method: "by_path", component_type: "RectTransform",
  properties: { localScale: /FootR$/.test(path) ? [-1, 1, 1] : [1, 1, 1], anchoredPosition3D: ap ? [ap[0], ap[1], 0] : [0, 0, 0] } } }));
for (let i = 0; i < fixes.length; i += 25) await call("batch_execute", { commands: fixes.slice(i, i + 25), fail_fast: false });
console.log(`fixed ${fixes.length} transforms`);
