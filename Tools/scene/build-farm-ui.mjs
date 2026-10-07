#!/usr/bin/env node
// Farm HUD (Canvas/FarmScreen), decoration shop (Canvas/Modal/DecoPanel), critter card (Canvas/Modal/PetCardPanel)
// and the farm alert badge on the tree screen, built through MCP for Unity. Fixed counts → every chip, crop card,
// recipe row and deco card is placed here. Deletes and recreates only those objects.
import { Builder, call, destroyPaths, ART, FONT, TMAT, col, R, STRETCH, hlay, vlay, le } from "./mcp.mjs";
import { makeKit, WOOD, WOOD_D, INK, PAPER, PANEL, ic } from "./ui-kit.mjs";

const b = new Builder();
const k = makeKit(b);
const created = [];
b.go = ((orig) => function (path, comps = {}, o = {}) { created.push([path, comps.RectTransform?.anchoredPosition]); return orig.call(this, path, comps, o); })(b.go);
const go = (path, comps = {}, o = {}) => b.go(path, comps, o);
const box = (path, o = {}) => k.image(path, PAPER, { type: "Sliced", color: col(o.bg ?? "#fffaf0", o.a), outline: o.border ?? "#e2cfa8", ow: o.ow ?? 2, le: o.le, rect: o.rect ?? STRETCH(), extra: o.extra, ray: o.ray });
const TR = (x, y, w, h) => R(x, y, w, h, { anchor: [1, 1], pivot: [1, 1] });   // anchored to the parent's top-right
const CR = (x, y, w, h, flip) => ({ anchorMin: [0.5, 0.5], anchorMax: [0.5, 0.5], pivot: [0.5, 0.5], anchoredPosition: [x, y], sizeDelta: [w, h], ...(flip ? { localScale: [-1, 1, 1] } : {}) });
// button whose label child is named (so AutoWire finds it)
const renames = {};
function button(path, label, act, o = {}) {
  k.button(path, label, act, o);
  if (o.textName) { b.raw("manage_gameobject", { action: "modify", target: `${path}/Text`, search_method: "by_path", name: o.textName }); renames[`${path}/Text`] = `${path}/${o.textName}`; }
  return path;
}
const C = (n) => ART("Characters/Critters/" + n);
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
const KINDS = ["fire", "spark", "dew", "coin", "star", "leaf", "moon", "wind", "rock", "chest"];
const FS = "Canvas/FarmScreen";

await destroyPaths([FS, "Canvas/Modal/DecoPanel", "Canvas/Modal/PetCardPanel", "Canvas/TreeScreen/Buttons/Right/Farm/FarmBadge"]);

// ===== HUD =====
go(FS, { RectTransform: STRETCH(), FarmScreen: {} });
// top-left: title + shops
k.image(`${FS}/TitleBox`, PANEL, { type: "Sliced", color: { r: 0.62, g: 0.42, b: 0.27, a: 1 }, rect: R(24, 18, 470, 60) });
k.text(`${FS}/TitleBox/Title`, "버섯 농장", 32, "#fff6e0", { rect: STRETCH(22, 0, 12, 4), align: "MidlineLeft" });
button(`${FS}/Skin`, `${ic("star")} 스킨`, "skinshop", { rect: R(24, 86, 120, 42), size: 18 });
button(`${FS}/Deco`, `${ic("fx_heart")} 꾸미기`, "decoshop", { rect: R(152, 86, 140, 42), size: 18 });
// tabs
const TABS = [["field", `${ic("crop_ed")} 버섯 밭`], ["pets", `${ic("ed")} 꼬마 목장`], ["kitchen", `${ic("dish_soup")} 꼬마 식당`]];
TABS.forEach(([v, label], i) => {
  const T = `${FS}/Tabs${i}`;
  button(T, label, "farmgo", { arg: v, rect: R(626 + i * 226, 18, 216, 58), size: 26, textName: `TabTexts${i}` });
  k.image(`${T}/Badges${i}`, ART("FX/circle"), { color: col("#e8453c"), rect: R(-14, -10, 34, 34, { anchor: [1, 1], pivot: [0.5, 0.5] }), outline: "#ffffff", ow: 2 });
  k.text(`${T}/Badges${i}/BadgeTexts${i}`, "1", 18, "#ffffff", { rect: STRETCH(0, 0, 0, 2) });
});
// resources
[["Gold", "gold"], ["Gem", "gem"], ["Spore", "spore"]].forEach(([n, icon], i) => {
  box(`${FS}/${n}Box`, { bg: "#f6ead2", border: WOOD_D, ow: 3, rect: R(1376 + i * 178, 18, 170, 52) });
  k.text(`${FS}/${n}Box/${n}Text`, `${ic(icon)}0`, 24, INK, { rect: STRETCH(10, 0, 10, 2) });
});
await b.send("farm hud: top");

// scene navigation (fade while panning)
go(`${FS}/Nav`, { RectTransform: STRETCH(), CanvasGroup: {} });
button(`${FS}/Nav/NavL`, "◀ 농사하러 가기", "farmgo", { arg: "field", rect: R(24, 615, 380, 76), size: 28, textName: "NavLText" });
button(`${FS}/Nav/NavR`, "요리하러 가기 ▶", "farmgo", { arg: "kitchen", rect: R(1516, 615, 380, 76), size: 28, textName: "NavRText" });
// empty scene message
go(`${FS}/CenterMsg`, { RectTransform: R(960, 546, 1300, 120, { pivot: [0.5, 0.5] }) });
k.text(`${FS}/CenterMsg/CenterTitle`, "아직 농장에 아무도 없어요", 40, "#fff6e0", { rect: R(0, 0, 1300, 56), mat: TMAT.dark });
k.text(`${FS}/CenterMsg/CenterSub`, "", 26, "#fff6e0", { rect: R(0, 62, 1300, 40), mat: TMAT.dark });
// bottom
button(`${FS}/Back`, "◀ 균사 트리", "totree2", { rect: R(24, 1000, 200, 64), size: 26, style: "ghost" });
k.image(`${FS}/Chips`, PAPER, { type: "Sliced", color: col("#3b2414", 0.82), rect: R(240, 990, 1390, 76), extra: { CanvasGroup: {} } });
for (let i = 0; i < 10; i++) {
  const Cp = `${FS}/Chips/ChipList${i}`;
  go(Cp, { RectTransform: R(8 + i * 138, 8, 132, 60), Button: {}, UIAction: { act: "farmcard", arg: KINDS[i] }, UIButtonFx: {}, FarmChip: {} });
  go(`${Cp}/Group`, { RectTransform: STRETCH(), CanvasGroup: {} });
  box(`${Cp}/Group/Frame`, { bg: "#f6ead2", border: "#cdb68e", ow: 2, ray: true });
  critter(`${Cp}/Group/Icon`, -40, 2, 52, KINDS[i]);
  k.text(`${Cp}/Group/Title`, "꼬마", 15, INK, { rect: R(52, 6, 80, 24), align: "MidlineLeft" });
  k.text(`${Cp}/Group/Sub`, "♥♡♡♡♡ 0:00", 11, "#6a5040", { rect: R(52, 32, 80, 22), align: "MidlineLeft", font: FONT.noto });
  k.image(`${Cp}/Group/EvoBadge`, PAPER, { type: "Sliced", color: col("#e8453c"), rect: R(-6, -12, 52, 22, { anchor: [1, 1], pivot: [1, 0.5] }) });
  k.text(`${Cp}/Group/EvoBadge/Text`, "진화!", 13, "#ffffff", { rect: STRETCH() });
  k.text(`${Cp}/Group/EvoStars`, "★", 13, "#e8a900", { rect: R(-2, -10, 50, 20, { anchor: [1, 1], pivot: [1, 0.5] }), align: "Right" });
  if (i % 3 === 2) await b.send(`farm hud: chips to ${i}`);
}
button(`${FS}/CareAll`, "모두 돌보기", "farmall", { style: "go", rect: R(1646, 996, 250, 68), size: 24, textName: "CareAllText" });
await b.send("farm hud: bottom");

// ===== field panel =====
const FP = `${FS}/FieldPanel`;
k.image(FP, PAPER, { type: "Sliced", color: col("#f6ead2", 0.96), outline: WOOD_D, ow: 5, rect: R(1246, 112, 650, 520),
  extra: { VerticalLayoutGroup: vlay({ gap: 12, pad: { left: 18, right: 18, top: 16, bottom: 16 }, align: "UpperLeft" }), ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" }, CanvasGroup: {} } });
k.text(`${FP}/FieldHead`, "심을 작물", 24, INK, { align: "MidlineLeft", wrap: true, le: { h: 34 } });
go(`${FP}/CropRow`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 8, cw: false, ch: false, align: "MiddleLeft" }), LayoutElement: le({ h: 96 }) });
["ed", "md", "ps"].forEach((c, i) => {
  const Cc = `${FP}/CropRow/Crops${i}`;
  go(Cc, { RectTransform: R(0, 0, 199, 96), Button: {}, UIAction: { act: "fieldcrop", arg: c }, UIButtonFx: {}, CropCard: {} });
  box(`${Cc}/Frame`, { border: "#d9c6a2", ow: 3, ray: true });
  k.image(`${Cc}/Icon`, ART("Icons/" + c), { rect: R(8, 24, 44, 44), aspect: true });
  k.text(`${Cc}/Title`, c, 19, INK, { rect: R(58, 6, 136, 26), align: "MidlineLeft" });
  k.text(`${Cc}/Need`, "", 15, INK, { rect: R(58, 34, 136, 24), align: "MidlineLeft" });
  k.text(`${Cc}/Time`, "", 12, "#6a5040", { rect: R(58, 62, 136, 24), align: "MidlineLeft", font: FONT.noto });
  k.text(`${Cc}/Lack`, "재료 부족", 11, "#d23a2a", { rect: TR(-6, 4, 70, 16), align: "Right", font: FONT.noto });
});
go(`${FP}/StockRow`, { RectTransform: {}, LayoutElement: le({ h: 40 }) });
k.text(`${FP}/StockRow/Stock`, "창고", 18, INK, { rect: R(0, 0, 440, 40), align: "MidlineLeft" });
button(`${FP}/StockRow/SporeShop`, `${ic("spore")} 포자 상점`, "sporeshop", { rect: TR(0, 0, 160, 40), size: 16 });
go(`${FP}/BtnRow`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 8, cw: false, ch: false, align: "MiddleLeft" }), LayoutElement: le({ h: 52 }) });
[["till", "모두 갈기"], ["plant", "모두 심기"], ["harvest", "모두 수확"]].forEach(([m, label], i) =>
  button(`${FP}/BtnRow/FieldButtons${i}`, label, "fieldall", { arg: m, rect: R(0, 0, 199, 50), size: 18, style: i === 2 ? "go" : "wood", textName: `FieldButtonTexts${i}` }));
// tool row
box(`${FP}/ToolRow`, { le: { h: 88 }, rect: {} });
k.image(`${FP}/ToolRow/ToolIcon`, ART("Farm/Field/tool_0"), { rect: R(12, 12, 64, 64), aspect: true });
k.text(`${FP}/ToolRow/ToolTitle`, "낡은 호미", 22, INK, { rect: R(88, 12, 300, 30), align: "MidlineLeft" });
k.text(`${FP}/ToolRow/ToolSub`, "", 13, "#6a5040", { rect: R(88, 48, 300, 24), align: "MidlineLeft", font: FONT.noto });
button(`${FP}/ToolRow/ToolUp`, "", "toolup", { style: "go", rect: TR(-12, 12, 210, 64), size: 16, textName: "ToolUpText" });
k.image(`${FP}/ToolRow/ToolUp/ToolUpIcon`, ART("Farm/Field/tool_1"), { rect: R(10, 15, 34, 34), aspect: true });
k.text(`${FP}/ToolRow/ToolMax`, "최고 등급 ★", 18, "#b8860b", { rect: TR(-16, 12, 200, 64), align: "MidlineRight" });
// size row
box(`${FP}/SizeRow`, { le: { h: 88 }, rect: {} });
k.image(`${FP}/SizeRow/SizeBox`, PAPER, { type: "Sliced", color: col("#6e4426"), outline: WOOD_D, ow: 3, rect: R(12, 12, 64, 64) });
k.text(`${FP}/SizeRow/SizeBox/SizeText`, "4×4", 22, "#fff6e0", { rect: STRETCH() });
k.text(`${FP}/SizeRow/ExpandTitle`, "밭 넓히기", 22, INK, { rect: R(88, 12, 300, 30), align: "MidlineLeft" });
k.text(`${FP}/SizeRow/ExpandSub`, "", 13, "#6a5040", { rect: R(88, 48, 300, 24), align: "MidlineLeft", font: FONT.noto });
button(`${FP}/SizeRow/Expand`, "", "fieldexpand", { style: "go", rect: TR(-12, 12, 210, 64), size: 16, textName: "ExpandText" });
k.text(`${FP}/SizeRow/ExpandMax`, "최대 ★", 18, "#b8860b", { rect: TR(-16, 12, 200, 64), align: "MidlineRight" });
await b.send("farm hud: field panel");

// ===== kitchen panel =====
const KP = `${FS}/KitchenPanel`;
k.image(KP, PAPER, { type: "Sliced", color: col("#f6ead2", 0.96), outline: WOOD_D, ow: 5, rect: R(1246, 112, 650, 840),
  extra: { VerticalLayoutGroup: vlay({ gap: 8, pad: { left: 18, right: 18, top: 16, bottom: 16 }, align: "UpperLeft" }), CanvasGroup: {} } });
go(`${KP}/HeadRow`, { RectTransform: {}, LayoutElement: le({ h: 56 }) });
k.text(`${KP}/HeadRow/KitchenHead`, "오늘의 메뉴", 24, INK, { rect: R(0, 0, 440, 56), align: "MidlineLeft", wrap: true });
button(`${KP}/HeadRow/CookAll`, `${ic("dish_soup")} 모두 요리`, "cookall", { style: "go", rect: TR(0, 6, 160, 44), size: 17 });
go(`${KP}/List`, { RectTransform: {}, Image: { color: col("#ffffff", 0), raycastTarget: true }, ScrollRect: { horizontal: false, vertical: true, movementType: "Clamped", scrollSensitivity: 30 }, LayoutElement: le({ h: 680 }) });
go(`${KP}/List/Viewport`, { RectTransform: STRETCH(), RectMask2D: {} });
go(`${KP}/List/Viewport/Content`, { RectTransform: { anchorMin: [0, 1], anchorMax: [1, 1], pivot: [0.5, 1], anchoredPosition: [0, 0], sizeDelta: [-8, 0] },
  VerticalLayoutGroup: vlay({ gap: 6, align: "UpperLeft" }), ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" } });
const RCAT = { ed: ["식용 요리", "회복·버프", "#f28c28"], md: ["약용 요리", "치유·상태이상 해제", "#3fae4a"], ps: ["독 요리", "공격·디버프 부여", "#9b4fd1"], mix: ["상위 요리", "여러 버섯 + 균사석", "#2a9a74"] };
const RECIPES = [["soup", "ed"], ["songi", "ed"], ["tea", "md"], ["reishi", "md"], ["cure", "md"], ["venom", "ps"], ["fog", "ps"], ["hotpot", "mix"], ["jap", "mix"], ["truffle", "mix"]];
let lastCat = null;
RECIPES.forEach(([id, cat], i) => {
  const L = `${KP}/List/Viewport/Content`;
  if (cat !== lastCat) {
    lastCat = cat;
    const [n, d, c] = RCAT[cat];
    k.text(`${L}/Cat_${cat}`, `${n} <size=60%><color=#8a6a4a>${d}</color></size>`, 20, c, { align: "MidlineLeft", le: { h: 30 } });
  }
  const Rw = `${L}/Recipes${i}`;
  go(Rw, { RectTransform: {}, RecipeRow: {}, LayoutElement: le({ h: 78 }) });
  box(`${Rw}/Frame`, {});
  k.image(`${Rw}/Icon`, ART("Farm/Icons/dish_" + id), { rect: R(8, 12, 54, 54), aspect: true });
  k.text(`${Rw}/Title`, id, 19, INK, { rect: R(70, 4, 360, 26), align: "MidlineLeft" });
  k.text(`${Rw}/Effect`, "", 12, "#6a5040", { rect: R(70, 30, 360, 18), align: "MidlineLeft", font: FONT.noto });
  k.text(`${Rw}/Need`, "", 14, INK, { rect: R(70, 50, 360, 24), align: "MidlineLeft" });
  button(`${Rw}/Button`, "요리하기", "cook", { arg: id, style: "go", rect: TR(-8, 13, 124, 52), size: 16, textName: "ButtonText" });
});
await b.send("farm hud: kitchen rows");
k.text(`${KP}/ReadyText`, "상에 차린 요리:", 18, INK, { align: "MidlineLeft", le: { h: 40 } });
await b.send("farm hud: kitchen panel");

// ===== decoration shop =====
const DP = "Canvas/Modal/DecoPanel";
k.panel(DP, 1340, { h: 900, gap: 10, pad: { left: 36, right: 36, top: 26, bottom: 24 }, comps: { DecoPanel: {} } });
k.h2(`${DP}/Title`, `${ic("fx_heart")} 농장 꾸미기 상점`);
k.body(`${DP}/Sub`, "균사석으로 꼬마 목장을 꾸며요. 산 장식은 언제든 놓거나 치울 수 있고, 꼬마들이 장식 근처에서 놀기를 좋아해요. (능력치는 없는 꾸미기 아이템이에요)", 17, "#6a5040", { le: {} });
go(`${DP}/Res`, { RectTransform: R(-80, 22, 300, 48, { anchor: [1, 1], pivot: [1, 1] }), LayoutElement: le({ ignore: true }) });
k.text(`${DP}/Res/GemText`, "0", 24, INK, { rect: STRETCH(), align: "Right" });
go(`${DP}/Grid`, { RectTransform: {}, GridLayoutGroup: { cellSize: [300, 360], spacing: [14, 14], constraint: "FixedColumnCount", constraintCount: 4, childAlignment: "UpperCenter" }, LayoutElement: le({ h: 740 }) });
const DECOS = ["dc_table", "dc_fire", "dc_lamp", "dc_bed", "dc_swing", "dc_well", "dc_house", "dc_statue"];
for (let i = 0; i < 8; i++) {
  const Cd = `${DP}/Grid/Cards${i}`;
  go(Cd, { RectTransform: R(0, 0, 300, 360), DecoCard: {} });
  box(`${Cd}/Frame`, { border: "#cdb68e", ow: 3 });
  k.image(`${Cd}/PicBg`, ART("FX/soft"), { color: col("#b8e090"), rect: R(30, 10, 240, 190, { anchor: [0, 1] }) });
  k.image(`${Cd}/Pic`, ART("Farm/Props/" + DECOS[i]), { rect: R(40, 18, 220, 176), aspect: true });
  k.text(`${Cd}/Title`, DECOS[i], 22, INK, { rect: R(0, 206, 300, 30) });
  k.body(`${Cd}/Desc`, "", 13, "#8a6a4a", { rect: R(14, 236, 272, 50), align: "Top" });
  button(`${Cd}/Button`, "구입", "buydeco", { arg: DECOS[i], style: "go", rect: R(40, 292, 220, 54), size: 18, textName: "ButtonText" });
  if (i % 4 === 3) await b.send(`deco: cards to ${i}`);
}
k.close(DP);
await b.send("deco: close");

// ===== critter card =====
const PC = "Canvas/Modal/PetCardPanel";
k.panel(PC, 1000, { h: 560, gap: 0, pad: { left: 40, right: 40, top: 36, bottom: 30 }, comps: { PetCardPanel: {} } });
go(`${PC}/Body`, { RectTransform: {}, LayoutElement: le({ h: 490 }) });
go(`${PC}/Body/PicBox`, { RectTransform: R(0, 60, 300, 300) });
k.image(`${PC}/Body/PicBox/Ring`, ART("FX/soft"), { color: col("#8ff0c8", 0), rect: R(-50, -50, 400, 400) });
k.image(`${PC}/Body/PicBox/Circle`, ART("FX/circle"), { color: col("#e4f4d4"), rect: R(10, 10, 280, 280), outline: WOOD_D, ow: 4 });
critter(`${PC}/Body/PicBox/Pic`, 0, 6, 230, "fire");
k.image(`${PC}/Body/PicBox/EvoPill`, PAPER, { type: "Sliced", color: col(WOOD), rect: R(90, 276, 120, 34) });
k.text(`${PC}/Body/PicBox/EvoPill/EvoStars`, "☆☆", 22, "#ffd23a", { rect: STRETCH() });
const IN = `${PC}/Body/Info`;
go(IN, { RectTransform: R(330, 0, 590, 490) });
k.text(`${IN}/Title`, "불씨 꼬마", 40, INK, { rect: R(0, 0, 590, 52), align: "MidlineLeft" });
k.body(`${IN}/Perk`, "", 15, "#6a3a9a", { rect: R(0, 56, 590, 26), align: "MidlineLeft" });
k.text(`${IN}/HeartLabel`, "호감도", 22, INK, { rect: R(0, 92, 80, 44), align: "MidlineLeft" });
for (let i = 0; i < 5; i++) {
  k.text(`${IN}/Hearts${i}`, "♡", 34, "#e8b8c8", { rect: R(84 + i * 42, 90, 40, 46) });
  k.image(`${IN}/Hearts${i}/HeartFills${i}`, ART("FX/square"), { color: col("#ff5a8a"), rect: R(4, 44, 32, 4), fill: true });
}
k.body(`${IN}/Note`, "", 14, "#8a6a4a", { rect: R(0, 142, 590, 24), align: "MidlineLeft" });
k.body(`${IN}/List`, "", 15, "#4a3424", { rect: R(0, 172, 590, 110), align: "TopLeft", lineSpacing: 20 });
button(`${IN}/Evolve`, "진화하기", "evolve", { style: "go", rect: R(0, 296, 440, 64), size: 24, textName: "EvolveText" });
k.text(`${IN}/EvoMax`, "★ 최종 진화 완료 ★", 22, "#b8860b", { rect: R(0, 296, 440, 64), align: "MidlineLeft" });
button(`${IN}/Skin`, "스킨", "skinshop", { rect: R(0, 372, 440, 54), size: 19, textName: "SkinText" });
k.close(PC);
await b.send("pet card");

// ===== tree screen badge =====
const TB = "Canvas/TreeScreen/Buttons/Right/Farm/FarmBadge";
k.image(TB, ART("FX/circle"), { color: col("#e8453c"), rect: R(-12, -8, 32, 32, { anchor: [1, 1], pivot: [0.5, 0.5] }), outline: "#ffffff", ow: 2 });
k.text(`${TB}/FarmBadgeText`, "1", 17, "#ffffff", { rect: STRETCH(0, 0, 0, 2) });
await b.send("tree badge");

const fixes = created.map(([path, ap]) => ({ tool: "manage_components", params: { action: "set_property", target: renames[path] ?? path, search_method: "by_path", component_type: "RectTransform",
  properties: { localScale: /FootR$/.test(path) ? [-1, 1, 1] : [1, 1, 1], anchoredPosition3D: ap ? [ap[0], ap[1], 0] : [0, 0, 0] } } }));
for (let i = 0; i < fixes.length; i += 25) await call("batch_execute", { commands: fixes.slice(i, i + 25), fail_fast: false });
console.log(`fixed ${fixes.length} transforms`);

// modal panels start hidden (ModalHost shows one at a time; an active sibling would show behind every modal)
await call("execute_code", { action: "execute", code: `var m = GameObject.Find("Canvas").transform.Find("Modal");
foreach (var n in new[] { "DecoPanel", "PetCardPanel" }) m.Find(n).gameObject.SetActive(false);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return "hidden";` });
console.log("modal panels hidden");
