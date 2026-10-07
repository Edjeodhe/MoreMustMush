#!/usr/bin/env node
// Workshop modal under Canvas/Modal (harvester cards + weather table), built through MCP for Unity.
// Only creates Canvas/Modal/WorkshopPanel (deleting a previous one). Fixed counts → every card/row is placed here.
import { Builder, call, ART, FONT, col, R, STRETCH, hlay, vlay, le } from "./mcp.mjs";
import { makeKit, WOOD_D, INK, PAPER, ic } from "./ui-kit.mjs";

const b = new Builder();
const k = makeKit(b);
const P = "Canvas/Modal/WorkshopPanel";
const created = [];
b.go = ((orig) => function (path, comps = {}, o = {}) { created.push([path, comps.RectTransform?.anchoredPosition]); return orig.call(this, path, comps, o); })(b.go);
const go = (path, comps = {}, o = {}) => b.go(path, comps, o);
const box = (path, o = {}) => k.image(path, PAPER, { type: "Sliced", color: col(o.bg ?? "#fffaf0"), outline: o.border ?? "#e2cfa8", ow: o.ow ?? 2, le: o.le, rect: o.rect, extra: o.extra });
const HVS = ["sam", "saw", "mill", "spore", "bell", "coin", "gold"];
const WS = ["clear", "rain", "moon", "autumn", "thunder", "rainbow", "fog", "wind", "storm", "cold", "drought"];

await call("manage_gameobject", { action: "delete", target: P, search_method: "by_path" });

k.panel(P, 1760, { h: 960, gap: 8, pad: { left: 36, right: 36, top: 26, bottom: 24 }, comps: { WorkshopPanel: {} } });
k.h2(`${P}/Title`, `${ic("s_sharpen")} 공방`);
k.body(`${P}/Sub`, "", 17, "#6a5040", { le: {} });
go(`${P}/Res`, { RectTransform: R(-80, 22, 400, 48, { anchor: [1, 1], pivot: [1, 1] }), HorizontalLayoutGroup: hlay({ gap: 14, cw: false, ch: false, align: "MiddleRight" }), LayoutElement: le({ ignore: true }) });
k.text(`${P}/Res/Gold`, "0", 24, INK, { rect: R(0, 0, 300, 40), align: "Right" });

go(`${P}/Body`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 26, cw: false, ch: false, align: "UpperLeft" }), LayoutElement: { preferredHeight: 790, flexibleHeight: 1 } });
// harvesters
go(`${P}/Body/Hv`, { RectTransform: R(0, 0, 960, 790), VerticalLayoutGroup: vlay({ gap: 8, align: "UpperLeft" }) });
k.text(`${P}/Body/Hv/Head`, "수확기", 26, INK, { align: "Left", le: { h: 34 } });
go(`${P}/Body/Hv/Grid`, { RectTransform: {}, GridLayoutGroup: { cellSize: [310, 360], spacing: [12, 12], constraint: "FixedColumnCount", constraintCount: 3 }, LayoutElement: le({ h: 744 }) });
for (const id of HVS) {
  const C = `${P}/Body/Hv/Grid/${id}`;
  box(C, { border: "#cdb68e", ow: 3, rect: R(0, 0, 310, 360), extra: { VerticalLayoutGroup: vlay({ gap: 3, pad: { left: 10, right: 10, top: 10, bottom: 10 }, align: "UpperCenter" }), HvCard: { id } } });
  k.image(`${C}/Icon`, ART("Harvesters/" + id), { aspect: true, le: { h: 72 } });
  k.text(`${C}/Title`, id, 20, INK, { le: { h: 26 } });
  k.text(`${C}/Stars`, "", 18, INK, { le: { h: 22 } });
  k.body(`${C}/Type`, "", 13, "#8a6a4a", { le: { h: 18 } });
  k.body(`${C}/Trait`, "", 13, "#6a3a9a", { le: { h: 70 } });
  k.body(`${C}/Next`, "", 12, "#8a7a6a", { le: { h: 34 } });
  k.text(`${C}/Req`, "", 14, "#8a6a4a", { wrap: true, le: { h: 40 } });
  go(`${C}/Btns`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 6, cw: false, ch: false }), LayoutElement: le({ h: 44 }) });
  k.button(`${C}/Btns/Unlock`, "해금", "hvunlock", { arg: id, rect: R(0, 0, 230, 42), size: 17 });
  k.button(`${C}/Btns/Toggle`, "✔ 활성", "hvtoggle", { arg: id, rect: R(0, 0, 104, 42), size: 16 });
  k.button(`${C}/Btns/Level`, "★", "hvlevel", { arg: id, rect: R(0, 0, 160, 42), size: 16 });
}
await b.send("workshop: harvesters");
// weathers
go(`${P}/Body/W`, { RectTransform: R(0, 0, 690, 790), VerticalLayoutGroup: vlay({ gap: 5, align: "UpperLeft" }) });
k.text(`${P}/Body/W/Head`, "날씨 <size=60%><color=#8a6a4a>(라운드마다 무작위 · 파랑 좋은 날씨 / 빨강 나쁜 날씨)</color></size>", 26, INK, { align: "Left", le: { h: 34 } });
for (const id of WS) {
  const Wr = `${P}/Body/W/${id}`;
  box(Wr, { le: { h: 62 }, extra: { CanvasGroup: {}, WeatherRow: { id } } });
  k.image(`${Wr}/Icon`, ART("Icons/w_" + id), { rect: R(10, 8, 46, 46), aspect: true });
  k.text(`${Wr}/Title`, id, 19, INK, { rect: R(66, 4, 150, 28), align: "Left" });
  k.body(`${Wr}/Desc`, "", 13, "#6a5040", { rect: R(216, 6, 380, 26), align: "Left" });
  k.body(`${Wr}/Fav`, "", 13, "#6a5040", { rect: R(66, 34, 600, 24), align: "Left" });
  k.text(`${Wr}/Chance`, "", 16, INK, { rect: R(-12, 6, 110, 26, { anchor: [1, 1], pivot: [1, 1] }), align: "Right" });
}
k.close(P);
await b.send("workshop: weathers");

const fixes = created.map(([path, ap]) => ({ tool: "manage_components", params: { action: "set_property", target: path, search_method: "by_path", component_type: "RectTransform",
  properties: { localScale: [1, 1, 1], anchoredPosition3D: ap ? [ap[0], ap[1], 0] : [0, 0, 0] } } }));
for (let i = 0; i < fixes.length; i += 25) await call("batch_execute", { commands: fixes.slice(i, i + 25), fail_fast: false });
console.log(`fixed ${fixes.length} transforms`);
