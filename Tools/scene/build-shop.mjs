#!/usr/bin/env node
// Mushroom shop modal (sell · village quests · spore shop) under Canvas/Modal, built through MCP for Unity.
// Only creates Canvas/Modal/ShopPanel (deleting a previous one); the rest of the scene is untouched.
// References are wired afterwards by Tools/scene/wire.mjs.
import { Builder, call, ART, FONT, col, R, RT, STRETCH, img, txt, hlay, vlay, le, outline } from "./mcp.mjs";
import { makeKit, WOOD, WOOD_D, INK, PAPER, BTN, ic } from "./ui-kit.mjs";

const b = new Builder();
const k = makeKit(b);
const M = "Canvas/Modal", P = `${M}/ShopPanel`;
const created = [];
const go = (path, comps = {}, o = {}) => b.go(path, comps, o);
b.go = ((orig) => function (path, comps = {}, o = {}) { created.push([path, comps.RectTransform?.anchoredPosition]); return orig.call(this, path, comps, o); })(b.go);
const box = (path, o = {}) => k.image(path, PAPER, { type: "Sliced", color: col(o.bg ?? "#fffaf0"), outline: o.border ?? "#e2cfa8", ow: o.ow ?? 2, le: o.le, rect: o.rect, extra: o.extra });
const lay = (o) => ({ ...le(o), ...(o.flexH != null ? { flexibleHeight: o.flexH } : {}) });
const small = (path, label, act, arg, w = 46, o = {}) => k.button(path, label, act, { arg, rect: R(0, 0, w, 30), size: 15, le: { w, h: 30 }, style: o.style, padL: 2, padR: 2 });

await call("manage_gameobject", { action: "delete", target: P, search_method: "by_path" });

// ---------- panel ----------
k.panel(P, 1760, { h: 960, gap: 8, pad: { left: 36, right: 36, top: 26, bottom: 24 }, comps: { ShopPanel: {} } });
k.h2(`${P}/Title`, `${ic("md")} 버섯 상점`);
go(`${P}/Modes`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 10, cw: false, ch: false, align: "MiddleLeft" }), LayoutElement: le({ h: 52 }) });
for (const [arg, n] of [["sell", "판매"], ["quest", "마을 의뢰"], ["spore", "포자 상점"]]) {
  k.button(`${P}/Modes/${arg}`, n, "shopmode", { arg, rect: R(0, 0, 200, 48), size: 22 });
  if (arg === "quest") {
    k.image(`${P}/Modes/quest/Dot`, ART("FX/circle"), { rect: R(-6, -6, 30, 30, { anchor: [1, 1], pivot: [0.5, 0.5] }), color: col("#e8452c"), le: { ignore: true } });
    k.text(`${P}/Modes/quest/Dot/Text`, "0", 16, "#ffffff", { rect: STRETCH() });
  }
}
k.body(`${P}/Sub`, "", 17, "#6a5040", { le: {} });

// resources, top-right
go(`${P}/Res`, { RectTransform: R(-80, 22, 760, 48, { anchor: [1, 1], pivot: [1, 1] }), HorizontalLayoutGroup: hlay({ gap: 16, cw: false, ch: false, align: "MiddleRight" }), LayoutElement: le({ ignore: true }) });
k.button(`${P}/Res/QuestAll`, "일괄 완료", "questall", { style: "go", rect: R(0, 0, 190, 44), size: 19 });
for (const n of ["Spore", "Debt", "Gold"]) {
  go(`${P}/Res/${n}`, { RectTransform: R(0, 0, 150, 40), HorizontalLayoutGroup: hlay({ cw: true, ch: true }), ContentSizeFitter: { horizontalFit: "PreferredSize", verticalFit: "Unconstrained" } });
  k.text(`${P}/Res/${n}/Text`, "0", n === "Debt" ? 19 : 24, n === "Debt" ? "#d23a2a" : INK, { le: {} });
}

// ---------- sell ----------
const S = `${P}/Sell`;
go(S, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 8 }), LayoutElement: lay({ flexH: 1 }) });
go(`${S}/Tabs`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 10, cw: false, ch: false, align: "MiddleLeft" }), LayoutElement: le({ h: 50 }) });
for (const arg of ["all", "ed", "md", "ps"]) k.button(`${S}/Tabs/${arg}`, arg, "shoptab", { arg, rect: R(0, 0, 160, 46), size: 20 });
go(`${S}/Scroll`, { RectTransform: {}, ScrollRect: { horizontal: false, vertical: true, movementType: "Clamped", scrollSensitivity: 40 }, LayoutElement: le({ h: 590 }) });
go(`${S}/Scroll/Viewport`, { RectTransform: STRETCH(), Image: { color: col("#ffffff", 0.01), raycastTarget: true }, RectMask2D: {} });
go(`${S}/Scroll/Viewport/Content`, { RectTransform: { anchorMin: [0, 1], anchorMax: [1, 1], pivot: [0.5, 1], anchoredPosition: [0, 0], sizeDelta: [0, 600] },
  GridLayoutGroup: { cellSize: [836, 84], spacing: [8, 8], padding: { left: 4, right: 4, top: 4, bottom: 4 }, constraint: "FixedColumnCount", constraintCount: 2 },
  ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" } });

// row template (hidden; ShopPanel clones it)
const RW = `${S}/Scroll/Viewport/Content/RowTemplate`;
box(RW, { rect: R(0, 0, 836, 84), extra: { HorizontalLayoutGroup: hlay({ gap: 8, pad: { left: 10, right: 10, top: 6, bottom: 6 }, cw: false, ch: false, align: "MiddleLeft" }), ShopRow: {} } });
k.image(`${RW}/Icon`, ART("Mushrooms/Single/ed0"), { rect: R(0, 0, 52, 52), aspect: true });
go(`${RW}/NameCol`, { RectTransform: R(0, 0, 170, 64), VerticalLayoutGroup: vlay({ gap: 2, align: "MiddleLeft" }) });
k.text(`${RW}/NameCol/Title`, "버섯", 20, INK, { align: "Left", le: {} });
k.text(`${RW}/NameCol/Tier`, "일반", 13, INK, { align: "Left", le: {} });
k.text(`${RW}/Have`, "보유 0개", 16, INK, { rect: R(0, 0, 120, 64), align: "Left", lineSpacing: -10 });
go(`${RW}/Qty`, { RectTransform: R(0, 0, 290, 66), VerticalLayoutGroup: vlay({ gap: 4, align: "MiddleCenter", fw: false }) });
go(`${RW}/Qty/Steps`, { RectTransform: R(0, 0, 290, 30), HorizontalLayoutGroup: hlay({ gap: 4, cw: false, ch: false }) });
small(`${RW}/Qty/Steps/M10`, "−10", "shopq", "", 44);
small(`${RW}/Qty/Steps/M1`, "−1", "shopq", "", 38);
box(`${RW}/Qty/Steps/Box`, { rect: R(0, 0, 80, 30), border: "#cdb68e" });
k.text(`${RW}/Qty/Steps/Box/Text`, "0", 18, INK, { rect: STRETCH() });
small(`${RW}/Qty/Steps/P1`, "+1", "shopq", "", 38);
small(`${RW}/Qty/Steps/P10`, "+10", "shopq", "", 44);
go(`${RW}/Qty/Sets`, { RectTransform: R(0, 0, 290, 28), HorizontalLayoutGroup: hlay({ gap: 4, cw: false, ch: false }) });
small(`${RW}/Qty/Sets/Zero`, "0", "shopset", "", 34, { style: "ghost" });
small(`${RW}/Qty/Sets/Half`, "절반", "shopset", "", 50, { style: "ghost" });
small(`${RW}/Qty/Sets/All`, "전부", "shopset", "", 50, { style: "ghost" });
go(`${RW}/Qty/Sets/Slider`, { RectTransform: R(0, 0, 140, 24), Slider: { minValue: 0, maxValue: 1, wholeNumbers: true, direction: "LeftToRight" } });
k.image(`${RW}/Qty/Sets/Slider/Background`, ART("FX/square"), { rect: STRETCH(0, 9, 0, 9), color: col("#e2cfa8") });
go(`${RW}/Qty/Sets/Slider/FillArea`, { RectTransform: STRETCH(0, 9, 10, 9) });
k.image(`${RW}/Qty/Sets/Slider/FillArea/Fill`, ART("FX/square"), { rect: STRETCH(), color: col("#e8a900") });
go(`${RW}/Qty/Sets/Slider/HandleArea`, { RectTransform: STRETCH(10, 0, 10, 0) });
k.image(`${RW}/Qty/Sets/Slider/HandleArea/Handle`, ART("FX/circle"), { rect: R(0, 0, 22, 22, { pivot: [0.5, 0.5] }), color: col("#e8a900"), outline: WOOD_D, ow: 2, ray: true });
k.button(`${RW}/Sell`, "판매", "shopsell", { style: "go", rect: R(0, 0, 104, 68), size: 18 });

k.text(`${S}/Empty`, "팔 버섯이 없어요. 수확하러 가 볼까요?", 24, "#9a8a70", { le: { h: 80 } });
go(`${S}/Foot`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 16, cw: false, ch: false, align: "MiddleRight" }), LayoutElement: le({ h: 60 }) });
k.text(`${S}/Foot/Value`, "", 20, INK, { rect: R(0, 0, 420, 40), align: "Right" });
k.button(`${S}/Foot/SellAll`, "이 탭 버섯 전부 팔기", "shopsellall", { style: "go", rect: R(0, 0, 300, 54), size: 22 });
await b.send("shop: panel + sell");

// ---------- quests ----------
const Q = `${P}/Quest`;
go(Q, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 8 }), LayoutElement: lay({ flexH: 1 }) });
go(`${Q}/Scroll`, { RectTransform: {}, ScrollRect: { horizontal: false, vertical: true, movementType: "Clamped", scrollSensitivity: 40 }, LayoutElement: le({ h: 700 }) });
go(`${Q}/Scroll/Viewport`, { RectTransform: STRETCH(), Image: { color: col("#ffffff", 0.01), raycastTarget: true }, RectMask2D: {} });
go(`${Q}/Scroll/Viewport/Content`, { RectTransform: { anchorMin: [0, 1], anchorMax: [1, 1], pivot: [0.5, 1], anchoredPosition: [0, 0], sizeDelta: [0, 700] },
  VerticalLayoutGroup: vlay({ gap: 14, pad: { left: 4, right: 4, top: 4, bottom: 4 } }), ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" } });
const QT = `${Q}/Scroll/Viewport/Content/QuestTemplate`;
box(QT, { border: "#e2cfa8", ow: 3, le: { h: 200 }, extra: { HorizontalLayoutGroup: hlay({ gap: 20, pad: { left: 22, right: 22, top: 14, bottom: 14 }, cw: false, ch: false }), QuestRow: {} } });
go(`${QT}/Npc`, { RectTransform: R(0, 0, 150, 172), VerticalLayoutGroup: vlay({ gap: 2, align: "MiddleCenter", fw: false, cw: false, ch: false }) });
k.image(`${QT}/Npc/FaceBg`, ART("FX/circle"), { rect: R(0, 0, 120, 120), color: col("#ffe4d6"), outline: WOOD_D, ow: 4, extra: { Mask: { showMaskGraphic: true } } });
k.image(`${QT}/Npc/FaceBg/Face`, null, { rect: STRETCH(), aspect: true });
k.text(`${QT}/Npc/Name`, "주민", 22, INK, { rect: R(0, 0, 150, 28) });
k.image(`${QT}/Npc/Job`, BTN.wood, { type: "Sliced", rect: R(0, 0, 96, 20), color: col(WOOD) });
k.text(`${QT}/Npc/Job/Text`, "직업", 13, "#ffffff", { rect: STRETCH(), font: FONT.noto });
k.image(`${QT}/Talk`, PAPER, { type: "Sliced", rect: R(0, 0, 840, 150), color: col("#ffffff"), outline: "#d9c6a2", ow: 3 });
k.body(`${QT}/Talk/Text`, "", 19, "#4a3426", { rect: STRETCH(22, 16, 22, 16), align: "MidlineLeft" });
k.image(`${QT}/Need`, PAPER, { type: "Sliced", rect: R(0, 0, 360, 166), color: col("#f6ecd8"), extra: { HorizontalLayoutGroup: hlay({ gap: 14, pad: { left: 14, right: 14, top: 12, bottom: 12 }, cw: false, ch: false }) } });
k.image(`${P}/Quest/Scroll/Viewport/Content/QuestTemplate/Need/MushFrame`, PAPER, { type: "Sliced", rect: R(0, 0, 96, 96), color: col("#ffffff"), outline: "#d9c6a2", ow: 3 });
k.image(`${QT}/Need/MushFrame/Mush`, ART("Mushrooms/Single/ed0"), { rect: STRETCH(6, 6, 6, 6), aspect: true });
go(`${QT}/Need/Info`, { RectTransform: R(0, 0, 210, 140), VerticalLayoutGroup: vlay({ gap: 3, align: "MiddleLeft" }) });
k.body(`${QT}/Need/Info/Label`, "필요한 버섯", 13, "#8a6a4a", { align: "Left", le: {} });
k.text(`${QT}/Need/Info/Name`, "버섯", 22, INK, { align: "Left", le: {} });
k.text(`${QT}/Need/Info/Tier`, "일반", 14, INK, { align: "Left", le: {} });
k.image(`${QT}/Need/Info/Bar`, ART("FX/square"), { color: col("#e2cfa8"), le: { h: 12 } });
k.image(`${QT}/Need/Info/Bar/Fill`, ART("FX/square"), { rect: STRETCH(), color: col("#e8a900"), fill: true });
k.text(`${QT}/Need/Info/Have`, "창고 0 / 0", 14, INK, { align: "Left", le: {} });
go(`${QT}/Reward`, { RectTransform: R(0, 0, 230, 166), VerticalLayoutGroup: vlay({ gap: 6, align: "MiddleCenter" }) });
k.body(`${QT}/Reward/Label`, "보상", 13, "#8a6a4a", { le: {} });
k.text(`${QT}/Reward/Amount`, "+0", 30, "#c99a00", { le: {} });
k.button(`${QT}/Reward/Give`, "전달하기", "quest", { style: "go", size: 22, le: { h: 56 } });
k.text(`${Q}/Empty`, "버섯을 수확하면 마을 사람들이 의뢰를 보내와요.", 24, "#9a8a70", { le: { h: 80 } });
await b.send("shop: quests");

// ---------- spore shop ----------
const SP = `${P}/Spore`;
go(SP, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 18, pad: { left: 40, right: 40, top: 20, bottom: 10 } }), LayoutElement: lay({ flexH: 1 }) });
box(`${SP}/Card`, { le: { h: 190 }, extra: { HorizontalLayoutGroup: hlay({ gap: 30, pad: { left: 30, right: 30, top: 20, bottom: 20 }, cw: false, ch: false, align: "MiddleLeft" }) } });
k.image(`${SP}/Card/Icon`, ART("Icons/spore"), { rect: R(0, 0, 140, 140), aspect: true });
go(`${SP}/Card/Info`, { RectTransform: R(0, 0, 900, 150), VerticalLayoutGroup: vlay({ gap: 4, align: "MiddleLeft" }) });
k.text(`${SP}/Card/Info/Name`, "버섯 포자", 34, INK, { align: "Left", le: {} });
k.text(`${SP}/Card/Info/Price`, "", 26, INK, { align: "Left", le: {} });
k.text(`${SP}/Card/Info/Own`, "", 22, "#2a7ad8", { align: "Left", le: {} });
k.body(`${SP}/Card/Info/Note`, "세금 사이클이 오를수록 값도 조금씩 올라요.", 15, "#8a6a4a", { align: "Left", le: {} });
go(`${SP}/Buy`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 16, cw: false, ch: false }), LayoutElement: le({ h: 96 }) });
for (const arg of ["1", "10", "100", "max"]) k.button(`${SP}/Buy/${arg === "max" ? "Max" : "N" + arg}`, arg, "buyspore", { arg, style: "go", rect: R(0, 0, arg === "max" ? 300 : 220, 90), size: 24 });
k.body(`${SP}/Uses`, "", 18, "#4a3426", { align: "Left", le: {}, lineSpacing: 20 });
k.close(P);
await b.send("shop: spore + close");

// ---------- fix world-scale-preserving creation: local scale 1, z 0 ----------
const fixes = created.map(([path, ap]) => ({ tool: "manage_components", params: { action: "set_property", target: path, search_method: "by_path", component_type: "RectTransform",
  properties: { localScale: [1, 1, 1], anchoredPosition3D: ap ? [ap[0], ap[1], 0] : [0, 0, 0] } } }));
for (let i = 0; i < fixes.length; i += 25) await call("batch_execute", { commands: fixes.slice(i, i + 25), fail_fast: false });
console.log(`fixed ${fixes.length} transforms`);
