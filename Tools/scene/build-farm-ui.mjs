#!/usr/bin/env node
// Farm HUD (Canvas/FarmScreen, tree panel, placement hint), build shop (Canvas/Modal/BuildPanel), idle harvest reward
// (Canvas/Modal/AutoPanel), speed-up (Canvas/Modal/AccelPanel),
// critter card (Canvas/Modal/PetCardPanel) and the tree screen's farm badge / idle-reward button / diamond counter,
// built through MCP for Unity. Fixed counts → every chip, crop card and building card is placed here.
// Deletes and recreates only those objects (and removes the old decoration shop and pre-round dish rows).
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
const BUILDS = ["dc_table", "dc_lamp", "dc_bed", "dc_fire", "dc_swing", "dc_well", "dc_house", "dc_statue"];
const FS = "Canvas/FarmScreen";
const TB = "Canvas/TreeScreen";

await destroyPaths([FS, "Canvas/Modal/DecoPanel", "Canvas/Modal/BuildPanel", "Canvas/Modal/AutoPanel", "Canvas/Modal/AccelPanel", "Canvas/Modal/PetCardPanel",
  `${TB}/Buttons/Right/Farm/FarmBadge`, `${TB}/Buttons/Left/AutoHarvest`, `${TB}/Topbar/Dia`,
  "Canvas/Modal/PreRoundPanel/DishesHead", "Canvas/Modal/PreRoundPanel/Dishes"]);

// ===== HUD =====
go(FS, { RectTransform: STRETCH(), FarmScreen: {} });
k.image(`${FS}/TitleBox`, PANEL, { type: "Sliced", color: { r: 0.62, g: 0.42, b: 0.27, a: 1 }, rect: R(24, 18, 520, 60) });
k.text(`${FS}/TitleBox/Title`, "버섯 농장", 32, "#fff6e0", { rect: STRETCH(22, 0, 12, 4), align: "MidlineLeft" });
k.image(`${FS}/StatusBox`, PAPER, { type: "Sliced", color: col("#3b2414", 0.78), rect: R(24, 84, 420, 40) });
k.text(`${FS}/StatusBox/Status`, "", 18, "#fff6e0", { rect: STRETCH(14, 0, 10, 2), align: "MidlineLeft" });
button(`${FS}/Skin`, `${ic("star")} 스킨`, "skinshop", { rect: R(24, 132, 120, 46), size: 18 });
button(`${FS}/Build`, `${ic("build")} 건축`, "buildshop", { rect: R(152, 132, 130, 46), size: 18 });
button(`${FS}/TreeToggle`, `${ic("fruit_ed")} 버섯 나무`, "treepanel", { rect: R(290, 132, 230, 46), size: 18, textName: "TreeToggleText" });
[["Gold", "gold"], ["Gem", "gem"], ["Dia", "dia"], ["Spore", "spore"]].forEach(([n, icon], i) => {
  box(`${FS}/${n}Box`, { bg: "#f6ead2", border: WOOD_D, ow: 3, rect: R(1192 + i * 178, 18, 170, 52) });
  k.text(`${FS}/${n}Box/${n}Text`, `${ic(icon)}0`, 24, INK, { rect: STRETCH(10, 0, 10, 2) });
});
go(`${FS}/CenterMsg`, { RectTransform: R(960, 546, 1400, 120, { pivot: [0.5, 0.5] }) });
k.text(`${FS}/CenterMsg/CenterTitle`, "아직 농장에 아무도 없어요", 40, "#fff6e0", { rect: R(0, 0, 1400, 56), mat: TMAT.dark });
k.text(`${FS}/CenterMsg/CenterSub`, "", 26, "#fff6e0", { rect: R(0, 62, 1400, 40), mat: TMAT.dark });
await b.send("farm hud: top");

// bottom (hidden while placing a building)
go(`${FS}/Bottom`, { RectTransform: STRETCH() });
button(`${FS}/Bottom/Back`, "◀ 균사 트리", "totree2", { rect: R(24, 1000, 200, 64), size: 26, style: "ghost" });
k.image(`${FS}/Bottom/Chips`, PAPER, { type: "Sliced", color: col("#3b2414", 0.82), rect: R(240, 990, 1390, 76) });
for (let i = 0; i < 10; i++) {
  const Cp = `${FS}/Bottom/Chips/ChipList${i}`;
  go(Cp, { RectTransform: R(8 + i * 138, 8, 132, 60), Button: {}, UIAction: { act: "farmcard", arg: KINDS[i] }, UIButtonFx: {}, FarmChip: {} });
  go(`${Cp}/Group`, { RectTransform: STRETCH(), CanvasGroup: {} });
  box(`${Cp}/Group/Frame`, { bg: "#f6ead2", border: "#cdb68e", ow: 2, ray: true });
  critter(`${Cp}/Group/Icon`, -40, 2, 52, KINDS[i]);
  k.text(`${Cp}/Group/Title`, "꼬마", 15, INK, { rect: R(52, 6, 80, 24), align: "MidlineLeft" });
  k.text(`${Cp}/Group/Sub`, "♥♡♡♡♡ 0:00", 11, "#6a5040", { rect: R(52, 32, 80, 22), align: "MidlineLeft", font: FONT.noto });
  k.image(`${Cp}/Group/EvoBadge`, PAPER, { type: "Sliced", color: col("#e8453c"), rect: R(-6, -12, 52, 22, { anchor: [1, 1], pivot: [1, 0.5] }) });
  k.text(`${Cp}/Group/EvoBadge/Text`, "진화!", 13, "#ffffff", { rect: STRETCH() });
  k.text(`${Cp}/Group/EvoStars`, "진화 1", 12, "#e8a900", { rect: R(-2, -10, 60, 20, { anchor: [1, 1], pivot: [1, 0.5] }), align: "Right" });
  if (i % 3 === 2) await b.send(`farm hud: chips to ${i}`);
}
button(`${FS}/Bottom/CareAll`, "모두 돌보기", "farmall", { style: "go", rect: R(1646, 996, 250, 68), size: 24, textName: "CareAllText" });
await b.send("farm hud: bottom");

// mushroom tree panel (opens with "버섯 나무" or by clicking the trunk)
const TPn = `${FS}/TreePanel`;
k.image(TPn, PAPER, { type: "Sliced", color: col("#f6ead2", 0.96), outline: WOOD_D, ow: 5, rect: R(1246, 84, 650, 420) });
k.image(`${TPn}/TreeIcon`, ART("Farm/Tree/tree_2"), { rect: R(18, 18, 120, 130), aspect: true });
k.text(`${TPn}/TreeHead`, "버섯 나무 Lv.1", 28, INK, { rect: R(150, 16, 480, 40), align: "MidlineLeft" });
k.text(`${TPn}/TreeStats`, "", 17, INK, { rect: R(150, 60, 480, 90), align: "TopLeft", wrap: true, lineSpacing: 10 });
box(`${TPn}/UpRow`, { rect: R(18, 160, 614, 110) });
k.body(`${TPn}/UpRow/TreeNext`, "", 14, "#4a3424", { rect: R(14, 8, 586, 40), align: "MidlineLeft" });
button(`${TPn}/UpRow/TreeUp`, "레벨 업", "treeup", { style: "go", rect: R(14, 50, 300, 52), size: 20, textName: "TreeUpText" });
k.text(`${TPn}/UpRow/TreeMax`, "최고 레벨 ★", 20, "#b8860b", { rect: R(14, 50, 300, 52), align: "MidlineLeft" });
button(`${TPn}/PickAll`, "모두 따기", "pickall", { style: "go", rect: R(18, 290, 300, 60), size: 21, textName: "PickAllText" });
button(`${TPn}/GrowFast`, "빨리 자라게", "treeaccel", { rect: R(332, 290, 300, 60), size: 20, textName: "GrowFastText" });
k.body(`${TPn}/Hint`, "버섯을 누르면 꼬마가 따러 가요. 딴 자리에는 새 버섯이 바로 자라요.", 13, "#8a6a4a", { rect: R(18, 362, 614, 40), align: "MidlineLeft" });
await b.send("farm hud: tree panel");

// placement hint bar (top center while placing; left click on green tiles builds)
const PB = `${FS}/PlaceBar`;
k.image(PB, PAPER, { type: "Sliced", color: col("#fffaf0", 0.97), outline: WOOD_D, ow: 4, rect: R(960, 228, 760, 96, { pivot: [0.5, 0.5] }) });
k.text(`${PB}/PlaceTitle`, "건물", 24, INK, { rect: R(20, 10, 580, 32), align: "MidlineLeft" });
k.text(`${PB}/PlaceSub`, "", 15, "#6a5040", { rect: R(20, 50, 580, 30), align: "MidlineLeft", font: FONT.noto });
button(`${PB}/PlaceCancel`, "취소", "placecancel", { rect: R(612, 22, 130, 52), size: 22 });
await b.send("farm hud: place bar");

// ===== build shop =====
const BP = "Canvas/Modal/BuildPanel";
k.panel(BP, 1520, { h: 940, gap: 10, pad: { left: 36, right: 36, top: 26, bottom: 24 }, comps: { BuildPanel: {} } });
k.h2(`${BP}/Title`, `${ic("build")} 건축`);
k.body(`${BP}/Sub`, "다이아몬드로 건물을 지어요. 쉬고 있는 꼬마 한 마리가 가서 짓고, 다 지을 때까지는 밭일을 못 해요. 건물마다 크기와 최대 개수가 정해져 있고, 지으면 작은 능력치가 영구로 붙어요.", 17, "#6a5040", { le: {} });
go(`${BP}/Res`, { RectTransform: R(-80, 22, 520, 48, { anchor: [1, 1], pivot: [1, 1] }), LayoutElement: le({ ignore: true }) });
k.text(`${BP}/Res/Workers`, "", 18, "#6a5040", { rect: R(0, 0, 300, 48), align: "MidlineRight" });
k.text(`${BP}/Res/DiaText`, "0", 24, INK, { rect: TR(0, 0, 200, 48), align: "MidlineRight" });
go(`${BP}/Grid`, { RectTransform: {}, GridLayoutGroup: { cellSize: [350, 380], spacing: [14, 14], constraint: "FixedColumnCount", constraintCount: 4, childAlignment: "UpperCenter" }, LayoutElement: le({ h: 774 }) });
for (let i = 0; i < 8; i++) {
  const Cd = `${BP}/Grid/Cards${i}`;
  go(Cd, { RectTransform: R(0, 0, 350, 380), BuildCard: {} });
  box(`${Cd}/Frame`, { border: "#cdb68e", ow: 3 });
  k.image(`${Cd}/PicBg`, ART("FX/soft"), { color: col("#b8e090"), rect: R(55, 10, 240, 170) });
  k.image(`${Cd}/Pic`, ART("Farm/Props/" + BUILDS[i]), { rect: R(65, 14, 220, 160), aspect: true });
  k.text(`${Cd}/Title`, BUILDS[i], 22, INK, { rect: R(0, 182, 350, 30) });
  k.body(`${Cd}/Effect`, "", 14, "#3a6a2a", { rect: R(14, 214, 322, 42), align: "Top" });
  k.text(`${Cd}/Info`, "", 14, "#8a6a4a", { rect: R(10, 258, 330, 24) });
  button(`${Cd}/Button`, "건설", "build", { arg: BUILDS[i], style: "go", rect: R(55, 296, 240, 56), size: 19, textName: "ButtonText" });
  if (i % 4 === 3) await b.send(`build: cards to ${i}`);
}
k.close(BP);
await b.send("build: close");

// ===== idle harvest reward =====
const AP = "Canvas/Modal/AutoPanel";
k.panel(AP, 900, { h: 800, gap: 10, pad: { left: 40, right: 40, top: 26, bottom: 28 }, comps: { AutoPanel: {} } });
k.h2(`${AP}/Title`, `${ic("auto_harvest")} 자동 수확 보상`);
k.text(`${AP}/Stage`, "현재 스테이지", 22, "#6a5040", { le: { h: 30 } });
go(`${AP}/PicRow`, { RectTransform: {}, LayoutElement: le({ h: 280 }) });
k.image(`${AP}/PicRow/Glow`, ART("FX/soft"), { color: col("#ffe9a0", 0.9), rect: R(210, -20, 400, 320) });
k.image(`${AP}/PicRow/Pic`, ART("Farm/Props/auto_reward"), { rect: R(250, 0, 320, 280), aspect: true });
k.text(`${AP}/TimeText`, "0시간 0분", 26, INK, { le: { h: 34 } });
go(`${AP}/TimeBar`, { RectTransform: {}, LayoutElement: le({ h: 22 }) });
k.image(`${AP}/TimeBar/Back`, PAPER, { type: "Sliced", color: col("#6e4426"), rect: R(70, 0, 680, 22) });
k.image(`${AP}/TimeBar/TimeFill`, ART("FX/square"), { color: col("#8fd062"), rect: R(74, 4, 672, 14), fill: true });
go(`${AP}/Rewards`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 18, cw: false, ch: false, align: "MiddleCenter" }), LayoutElement: le({ h: 120 }) });
[["Gold", "gold"], ["Gem", "gem"], ["Dia", "dia"]].forEach(([n, icon]) => {
  box(`${AP}/Rewards/${n}`, { border: "#cdb68e", ow: 3, rect: R(0, 0, 200, 116) });
  k.image(`${AP}/Rewards/${n}/Icon`, ART("Icons/" + icon), { rect: R(66, 8, 68, 64), aspect: true });
  k.text(`${AP}/Rewards/${n}/${n}Text`, "0", 26, INK, { rect: R(0, 76, 200, 34) });
});
k.body(`${AP}/Rates`, "", 15, "#6a5040", { align: "Center", le: { h: 44 } });
go(`${AP}/ClaimRow`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ cw: false, ch: false, align: "MiddleCenter" }), LayoutElement: le({ h: 76 }) });
button(`${AP}/ClaimRow/Claim`, "보상 받기", "claimauto", { style: "go", rect: R(0, 0, 360, 72), size: 28, textName: "ClaimText" });
k.close(AP);
await b.send("auto reward");

// ===== speed-up (균사석 가속) =====
const AC = "Canvas/Modal/AccelPanel";
k.panel(AC, 760, { h: 460, gap: 10, pad: { left: 40, right: 40, top: 30, bottom: 28 }, comps: { AccelPanel: {} } });
k.h2(`${AC}/Title`, "가속");
go(`${AC}/IconRow`, { RectTransform: {}, LayoutElement: le({ h: 140 }) });
k.image(`${AC}/IconRow/Icon`, ART("Farm/Tree/tree_2"), { rect: R(270, 0, 140, 140), aspect: true });
k.body(`${AC}/Desc`, "", 16, "#6a5040", { align: "Center", le: { h: 50 } });
k.text(`${AC}/TimeText`, "", 24, INK, { le: { h: 34 } });
go(`${AC}/BtnRow`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ cw: false, ch: false, align: "MiddleCenter" }), LayoutElement: le({ h: 72 }) });
button(`${AC}/BtnRow/Accel`, "가속", "accelok", { style: "go", rect: R(0, 0, 380, 68), size: 26, textName: "CostText" });
k.close(AC);
await b.send("accel");

// ===== critter card =====
const PC = "Canvas/Modal/PetCardPanel";
k.panel(PC, 1040, { h: 640, gap: 0, pad: { left: 40, right: 40, top: 36, bottom: 30 }, comps: { PetCardPanel: {} } });
go(`${PC}/Body`, { RectTransform: {}, LayoutElement: le({ h: 570 }) });
go(`${PC}/Body/PicBox`, { RectTransform: R(0, 90, 300, 300) });
k.image(`${PC}/Body/PicBox/Ring`, ART("FX/soft"), { color: col("#8ff0c8", 0), rect: R(-50, -50, 400, 400) });
k.image(`${PC}/Body/PicBox/Circle`, ART("FX/circle"), { color: col("#e4f4d4"), rect: R(10, 10, 280, 280), outline: WOOD_D, ow: 4 });
critter(`${PC}/Body/PicBox/Pic`, 0, 6, 230, "fire");
k.image(`${PC}/Body/PicBox/EvoPill`, PAPER, { type: "Sliced", color: col(WOOD), rect: R(80, 276, 140, 34) });
k.text(`${PC}/Body/PicBox/EvoPill/EvoStars`, "진화 전", 20, "#ffd23a", { rect: STRETCH() });
const IN = `${PC}/Body/Info`;
go(IN, { RectTransform: R(330, 0, 630, 570) });
k.text(`${IN}/Title`, "불씨 꼬마", 40, INK, { rect: R(0, 0, 630, 52), align: "MidlineLeft" });
k.body(`${IN}/Perk`, "", 15, "#6a3a9a", { rect: R(0, 54, 630, 40), align: "MidlineLeft" });
box(`${IN}/GradeBox`, { bg: "#fffaf0", border: "#e2cfa8", rect: R(0, 98, 630, 96) });
k.text(`${IN}/GradeBox/GradeLabel`, "별 등급", 20, INK, { rect: R(14, 8, 90, 40), align: "MidlineLeft" });
k.text(`${IN}/GradeBox/GradeStars`, "★★★★★", 30, "#e8a900", { rect: R(100, 6, 190, 44), align: "MidlineLeft" });
button(`${IN}/GradeBox/StarUp`, "★1 강화", "starup", { style: "go", rect: R(296, 6, 322, 48), size: 18, textName: "StarUpText" });
k.text(`${IN}/GradeBox/StarMax`, "최고 등급 ★", 20, "#b8860b", { rect: R(296, 6, 322, 48), align: "MidlineRight" });
k.body(`${IN}/GradeBox/GradeInfo`, "", 14, "#4a3424", { rect: R(14, 58, 604, 28), align: "MidlineLeft" });
k.text(`${IN}/HeartLabel`, "호감도", 22, INK, { rect: R(0, 204, 80, 44), align: "MidlineLeft" });
for (let i = 0; i < 5; i++) {
  k.text(`${IN}/Hearts${i}`, "♡", 34, "#e8b8c8", { rect: R(84 + i * 42, 202, 40, 46) });
  k.image(`${IN}/Hearts${i}/HeartFills${i}`, ART("FX/square"), { color: col("#ff5a8a"), rect: R(4, 44, 32, 4), fill: true });
}
k.body(`${IN}/Note`, "", 14, "#8a6a4a", { rect: R(0, 252, 630, 24), align: "MidlineLeft" });
k.body(`${IN}/List`, "", 15, "#4a3424", { rect: R(0, 280, 630, 96), align: "TopLeft", lineSpacing: 20 });
button(`${IN}/Evolve`, "진화하기", "evolve", { style: "go", rect: R(0, 384, 460, 60), size: 24, textName: "EvolveText" });
k.text(`${IN}/EvoMax`, "★ 최종 진화 완료 ★", 22, "#b8860b", { rect: R(0, 384, 460, 60), align: "MidlineLeft" });
button(`${IN}/Skin`, "스킨", "skinshop", { rect: R(0, 454, 460, 52), size: 19, textName: "SkinText" });
k.close(PC);
await b.send("pet card");

// ===== tree screen: farm badge, idle reward button, diamonds =====
k.image(`${TB}/Buttons/Right/Farm/FarmBadge`, ART("FX/circle"), { color: col("#e8453c"), rect: R(-12, -8, 32, 32, { anchor: [1, 1], pivot: [0.5, 0.5] }), outline: "#ffffff", ow: 2 });
k.text(`${TB}/Buttons/Right/Farm/FarmBadge/FarmBadgeText`, "1", 17, "#ffffff", { rect: STRETCH(0, 0, 0, 2) });
button(`${TB}/Buttons/Left/AutoHarvest`, `${ic("auto_harvest")} 자동 수확`, "autoharvest", { rect: R(0, -24, 254, 48), size: 18, textName: "AutoText", le: { ignore: true } });   // above 타이틀 · 기록 (outside the row layout)
k.image(`${TB}/Buttons/Left/AutoHarvest/AutoBadge`, ART("FX/circle"), { color: col("#e8453c"), rect: R(-10, -6, 22, 22, { anchor: [1, 1], pivot: [0.5, 0.5] }), outline: "#ffffff", ow: 2 });
await b.send("tree additions");
await call("execute_code", { action: "execute", code: `var top = GameObject.Find("Canvas").transform.Find("TreeScreen/Topbar");
var gem = top.Find("Gem"); var dia = UnityEngine.Object.Instantiate(gem.gameObject, top); dia.name = "Dia"; dia.transform.SetSiblingIndex(gem.GetSiblingIndex() + 1);
dia.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Icons/dia.png");
Undo.RegisterCreatedObjectUndo(dia, "dia box");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return "dia box";` });

const fixes = created.map(([path, ap]) => ({ tool: "manage_components", params: { action: "set_property", target: renames[path] ?? path, search_method: "by_path", component_type: "RectTransform",
  properties: { localScale: /FootR$/.test(path) ? [-1, 1, 1] : [1, 1, 1], anchoredPosition3D: ap ? [ap[0], ap[1], 0] : [0, 0, 0] } } }));
for (let i = 0; i < fixes.length; i += 25) await call("batch_execute", { commands: fixes.slice(i, i + 25), fail_fast: false });
console.log(`fixed ${fixes.length} transforms`);

// labels under inactive parents can't be renamed by path; panels start hidden (ModalHost shows one at a time)
const late = Object.entries(renames).map(([a, z]) => `[${JSON.stringify(a.replace("Canvas/", ""))}] = ${JSON.stringify(z.split("/").pop())}`).join(", ");
const res = await call("execute_code", { action: "execute", code: `var c = GameObject.Find("Canvas").transform; int n = 0;
var renames = new System.Collections.Generic.Dictionary<string, string> { ${late} };
foreach (var kv in renames) { var t = c.Find(kv.Key); if (t != null) { t.name = kv.Value; n++; } }
foreach (var p in new[] { "Modal/BuildPanel", "Modal/AutoPanel", "Modal/AccelPanel", "Modal/PetCardPanel" }) c.Find(p).gameObject.SetActive(false);
c.Find("FarmScreen").gameObject.SetActive(false);
c.Find("FarmScreen").SetSiblingIndex(c.Find("Modal").GetSiblingIndex());   // HUD stays under the modal layer
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return "late renames " + n;` });
console.log(res?.data?.result ?? JSON.stringify(res).slice(0, 300));
