#!/usr/bin/env node
// UI part 1: Canvas, round UI (field texts, HUD, combo/fever), title screen, tree screen.
import { Builder, call, ART, FONT, TMAT, col, R, RC, RT, STRETCH, img, txt, hlay, vlay, le, outline } from "./mcp.mjs";
import { makeKit, WOOD, WOOD_D, CREAM, INK, PANEL, PAPER, BTN, ic } from "./ui-kit.mjs";

const b = new Builder();
const k = makeKit(b);
const DARK = (a) => col("#28190f", a);

await call("manage_gameobject", { action: "delete", target: "Canvas", search_method: "by_name" });

// ===== canvas: Screen Space - Camera on the letterboxed main camera, 1920×1080 reference =====
const cams = await call("find_gameobjects", { search_term: "Main Camera", search_method: "by_name" });
const camId = cams?.data?.instanceIDs?.[0] ?? cams?.data?.items?.[0]?.instanceID ?? cams?.data?.[0]?.instanceID;
b.go("Canvas", {
  Canvas: { renderMode: "ScreenSpaceCamera", planeDistance: 5, sortingOrder: 10000 },
  CanvasScaler: { uiScaleMode: "ScaleWithScreenSize", referenceResolution: [1920, 1080], screenMatchMode: "MatchWidthOrHeight", matchWidthOrHeight: 0.5 },
  GraphicRaycaster: {},
});
b.raw("manage_components", { action: "set_property", target: "Canvas", search_method: "by_name", component_type: "Canvas", property: "worldCamera", value: camId });
await b.send("canvas");

// ===== screens (children of Canvas, back to front) =====
const full = (p, comps = {}) => b.go(p, { RectTransform: STRETCH(), ...comps });

// ---------- title ----------
full("Canvas/TitleScreen", { TitleScreen: {} });
b.go("Canvas/TitleScreen/Box", {
  RectTransform: RC(960, 540, 1180, 640),
  Image: { sprite: PANEL, type: "Sliced", color: col("#ffffff", 0.96), raycastTarget: true },
  Outline: outline(WOOD, 10),
});
b.go("Canvas/TitleScreen/Box/Icons", { RectTransform: RT(0, 40, 600, 100) });
for (let i = 0; i < 6; i++) k.image(`Canvas/TitleScreen/Box/Icons/Icon${i}`, ART("Mushrooms/Single/ed0"), { rect: RC(51 + i * 98, 50, 90, 90), aspect: true });
k.text("Canvas/TitleScreen/Box/Logo", "MoreMush", 130, "#e8452c", { rect: RT(0, 140, 1000, 150), mat: TMAT.wood });
k.text("Canvas/TitleScreen/Box/Subtitle", "거대 버섯에 뒤덮인 마을, 노련한 버섯 사냥꾼의 핀볼 수확기", 26, "#6a4a2a", { rect: RT(0, 300, 1100, 40) });
b.go("Canvas/TitleScreen/Box/Buttons", { RectTransform: RT(0, 370, 1000, 90), HorizontalLayoutGroup: hlay({ gap: 20, cw: false, ch: false }) });
k.button("Canvas/TitleScreen/Box/Buttons/Continue", "이어하기", "continue", { rect: R(0, 0, 260, 78), size: 34 });
k.button("Canvas/TitleScreen/Box/Buttons/NewGame", "시작하기", "newgame", { rect: R(0, 0, 260, 78), size: 34 });
k.button("Canvas/TitleScreen/Box/Buttons/Reset", "초기화", "reset", { rect: R(0, 0, 150, 56), style: "ghost" });
k.body("Canvas/TitleScreen/Box/Howto", "조작", 16, "#6a5040", { rect: RT(0, 480, 1040, 70) });
k.text("Canvas/TitleScreen/Box/Proto", "Unity 이식판 · 수치는 임시값 (밸런스 기획서에서 수정 예정)", 15, "#9a8a70", { rect: RT(0, 575, 1000, 30) });
await b.send("title");

// ---------- tree screen ----------
const TS = "Canvas/TreeScreen";
full(TS, { TreeScreen: {} });
// top bar
b.go(`${TS}/Topbar`, {
  RectTransform: RT(0, 14, 1200, 64),
  Image: { sprite: PANEL, type: "Sliced", color: col(WOOD), raycastTarget: true },
  Outline: outline(WOOD_D, 4),
  HorizontalLayoutGroup: hlay({ gap: 10, pad: { left: 14, right: 14, top: 8, bottom: 8 }, cw: true, ch: true, fh: true }),
  ContentSizeFitter: { horizontalFit: "PreferredSize", verticalFit: "PreferredSize" },
});
const resBox = (name, icon, o = {}) => {
  const p = `${TS}/Topbar/${name}`;
  k.image(p, PAPER, { type: "Sliced", color: col(o.bg ?? CREAM), extra: { HorizontalLayoutGroup: hlay({ gap: 6, pad: { left: 8, right: 14, top: 4, bottom: 4 }, cw: true, ch: true }), ...(o.outline ? { Outline: outline("#e8452c", 5, 0) } : {}) }, le: { minW: o.minW ?? 110, h: 48 } });
  k.image(`${p}/Icon`, ART("Icons/" + icon), { aspect: true, le: { w: o.isz ?? 34, h: o.isz ?? 34 } });
  return p;
};
let p = resBox("Stage", "t_forest", { minW: 0 });
b.go(`${p}/Texts`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 0, align: "MiddleLeft", fw: false }) });
k.text(`${p}/Texts/Title`, "스테이지 1", 20, INK, { align: "Left" });
k.body(`${p}/Texts/Sub`, "숲", 13, "#7a5a3a", { align: "Left", wrap: false });
p = resBox("Gold", "gold"); k.text(`${p}/Value`, "0", 26, INK, { align: "MidlineLeft" });
p = resBox("Gem", "gem", { bg: "#dff7ec", minW: 80, isz: 28 }); k.text(`${p}/Value`, "0", 22, INK, { align: "MidlineLeft" });
p = resBox("Spore", "spore", { minW: 80, isz: 28 }); k.text(`${p}/Value`, "0", 22, INK, { align: "MidlineLeft" });
k.image(`${TS}/Topbar/Sep`, ART("FX/square"), { color: col(WOOD_D), le: { w: 3, h: 34 } });
p = resBox("Tax", "tax", { minW: 0, outline: true });
b.go(`${p}/Texts`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 0, align: "MiddleLeft", fw: false }) });
k.text(`${p}/Texts/Title`, "사이클 1 세금 100", 22, INK, { align: "Left" });
k.body(`${p}/Texts/Sub`, "3라운드 뒤 청구", 13, "#7a5a3a", { align: "Left", wrap: false });
await b.send("tree topbar");

// bottom buttons
b.go(`${TS}/Buttons`, { RectTransform: { anchorMin: [0, 0], anchorMax: [1, 0], pivot: [0.5, 0], anchoredPosition: [0, 20], sizeDelta: [-48, 80] } });
b.go(`${TS}/Buttons/Left`, { RectTransform: { anchorMin: [0, 0], anchorMax: [0, 1], pivot: [0, 0], anchoredPosition: [0, 0], sizeDelta: [400, 0] }, HorizontalLayoutGroup: hlay({ gap: 14, align: "LowerLeft", cw: false, ch: false }) });
k.button(`${TS}/Buttons/Left/Title`, "타이틀", "title", { rect: R(0, 0, 120, 48), style: "ghost", size: 18 });
k.button(`${TS}/Buttons/Left/Records`, `${ic("trophy")} 기록`, "records", { rect: R(0, 0, 120, 48), size: 18 });
b.go(`${TS}/Buttons/Right`, { RectTransform: { anchorMin: [1, 0], anchorMax: [1, 1], pivot: [1, 0], anchoredPosition: [0, 0], sizeDelta: [1400, 0] }, HorizontalLayoutGroup: hlay({ gap: 14, align: "LowerRight", cw: false, ch: false }) });
k.button(`${TS}/Buttons/Right/Shop`, `${ic("md")} 버섯 상점`, "shop", { rect: R(0, 0, 180, 56), dim: true });
k.button(`${TS}/Buttons/Right/Farm`, "버섯 농장", "farm", { rect: R(0, 0, 160, 56), dim: true });
k.button(`${TS}/Buttons/Right/Skin`, "스킨", "skinshop", { rect: R(0, 0, 110, 56), dim: true });
k.button(`${TS}/Buttons/Right/Codex`, `${ic("book")} 도감 (0/75)`, "codex", { rect: R(0, 0, 200, 56) });
k.button(`${TS}/Buttons/Right/Workshop`, "공방 (도감 10종)", "workshop", { rect: R(0, 0, 190, 56), dim: true });
k.button(`${TS}/Buttons/Right/Bulk`, `${ic("s_bolt")} 일괄 강화`, "bulk", { rect: R(0, 0, 170, 56) });
k.button(`${TS}/Buttons/Right/Go`, "수확하러 가기 ▶", "go", { rect: R(0, 0, 300, 74), style: "go", size: 32 });
await b.send("tree buttons");

// legend (top-left)
const LG = `${TS}/Legend`;
k.image(LG, ART("UI/btn_wood"), { type: "Sliced", rect: R(22, 22, 190, 150), color: DARK(0.7) });
k.body(`${LG}/Head`, "강화 상태", 15, "#cbbfa8", { rect: R(16, 8, 160, 22), align: "Left", wrap: false });
[["#4fb4ff", "▲", "강화 가능"], ["#ff5a4a", "!", "재화 부족"], ["#ffd23a", "★", "최대 레벨"]].forEach(([c, s, l], i) => {
  k.image(`${LG}/Dot${i}`, ART("FX/ring"), { rect: RC(26, 44 + i * 30, 22, 22), color: col(c) });
  k.text(`${LG}/Sym${i}`, s, 11, c, { rect: RC(26, 44 + i * 30, 22, 22) });
  k.text(`${LG}/Label${i}`, l, 18, "#fff6e0", { rect: R(46, 32 + i * 30, 140, 24), align: "MidlineLeft" });
});
k.image(`${LG}/GemIcon`, ART("Icons/gem"), { rect: RC(26, 134, 24, 24), aspect: true });
k.text(`${LG}/GemLabel`, "균사석 노드", 16, "#a8f5d4", { rect: R(46, 122, 140, 24), align: "MidlineLeft" });

// tooltip
const TT = `${TS}/Tooltip`;
b.go(TT, {
  RectTransform: R(400, 300, 330, 200),
  Image: { sprite: PAPER, type: "Sliced", color: col("#faf0dc", 0.97), raycastTarget: false },
  Outline: outline(WOOD_D, 3),
  VerticalLayoutGroup: vlay({ gap: 4, pad: { left: 14, right: 14, top: 12, bottom: 12 }, align: "UpperLeft" }),
  ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" },
  TreeTooltip: {},
});
k.text(`${TT}/Title`, "노드", 26, "#f28c28", { align: "Left", wrap: true });
k.body(`${TT}/Level`, "LVL 0 / 5", 15, "#7a5a3a", { align: "Left" });
k.body(`${TT}/Desc`, "설명", 13, "#8a8378", { align: "Left" });
k.body(`${TT}/Effect`, "효과", 16, INK, { align: "Left" });
k.text(`${TT}/Cost`, "비용", 19, INK, { align: "Left", wrap: true });
k.body(`${TT}/Note`, "메모", 13, "#6a5a4a", { align: "Left" });
full(`${TS}/Sparks`);
await b.send("tree legend + tooltip");

// ---------- round UI ----------
const RU = "Canvas/RoundUI";
full(RU);
b.go(`${RU}/FieldText`, { RectTransform: R(0, 0, 1920, 1080) });
const H = `${RU}/RoundHUD`;
full(H, { RoundHUD: {} });
const wood = (path, x, y, w, h) => k.image(path, PANEL, { type: "Sliced", rect: R(x, y, w, h) });
wood(`${H}/ScorePanel`, 16, 14, 300, 124);
k.text(`${H}/ScorePanel/Label`, "SCORE", 24, "#5a3a20", { rect: RC(150, 28, 260, 30) });
k.text(`${H}/ScorePanel/Score`, "0", 50, INK, { rect: RC(150, 78, 280, 60) });
[["ed", 186], ["md", 244], ["ps", 302]].forEach(([c, y]) => {
  wood(`${H}/Slot_${c}`, 20, y - 26, 220, 52);
  k.image(`${H}/Slot_${c}/Icon`, ART("Icons/" + c), { rect: RC(46, 26, 36, 36), aspect: true });
  k.text(`${H}/Slot_${c}/Count`, "× 0", 28, INK, { rect: R(80, 4, 136, 44), align: "MidlineLeft" });
});
wood(`${H}/ValuePanel`, 20, 334, 260, 56);
k.image(`${H}/ValuePanel/Icon`, ART("Icons/gold"), { rect: RC(28, 28, 32, 32), aspect: true });
k.text(`${H}/ValuePanel/Value`, "0", 28, "#8a6a00", { rect: R(56, 6, 150, 44), align: "MidlineLeft" });
k.body(`${H}/ValuePanel/Label`, "버섯 가치", 15, "#8a6a4a", { rect: R(150, 6, 96, 44), align: "MidlineRight", wrap: false });
// time bar
k.image(`${H}/TimeBar`, BTN.wood, { type: "Sliced", rect: R(634, 12, 652, 38), color: DARK(0.75) });
k.image(`${H}/TimeBar/Track`, BTN.wood, { type: "Sliced", rect: R(6, 6, 640, 26), color: col("#3a2a1a") });
k.image(`${H}/TimeBar/Track/Fill`, ART("FX/square"), { rect: STRETCH(), color: col("#7fd65a"), fill: true });
k.text(`${H}/TimeBar/Text`, "20.0초", 22, "#ffffff", { rect: STRETCH(0, 0, 0, 2), mat: TMAT.dark });
k.text(`${H}/FestText`, "풍년!", 26, "#ffe070", { rect: RC(960, 68, 900, 40), mat: TMAT.dark });
// stat bar
b.go(`${H}/StatBar`, {
  RectTransform: RC(960, 66, 700, 34),
  Image: { sprite: BTN.wood, type: "Sliced", color: DARK(0.55), raycastTarget: false },
  HorizontalLayoutGroup: hlay({ gap: 10, pad: { left: 14, right: 14, top: 2, bottom: 2 }, cw: true, ch: true }),
  ContentSizeFitter: { horizontalFit: "PreferredSize", verticalFit: "Unconstrained" },
});
k.text(`${H}/StatBar/Stats`, "공격 1", 20, "#ffffff", { le: {} });
b.go(`${H}/StatBar/Skills`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 6, cw: false, ch: false }), LayoutElement: le({ h: 30 }) });
k.image(`${H}/StatBar/Skills/SkillIcon`, ART("FX/circle"), { rect: R(0, 0, 28, 28), color: col("#ffffff", 0.15) });
k.image(`${H}/StatBar/Skills/SkillIcon/Icon`, ART("Icons/s_burst"), { rect: STRETCH(3, 3, 3, 3), aspect: true });
// weather banner
b.go(`${H}/WeatherBanner`, { RectTransform: R(580, 118, 760, 92), Image: { sprite: BTN.wood, type: "Sliced", color: col("#140c06", 0.82), raycastTarget: false }, Outline: outline("#5fb8ff", 4), CanvasGroup: {} });
k.image(`${H}/WeatherBanner/Icon`, ART("Icons/w_clear"), { rect: RC(56, 46, 62, 62), aspect: true });
k.text(`${H}/WeatherBanner/Title`, "오늘의 날씨: 맑음", 34, "#5fb8ff", { rect: R(104, 10, 640, 44), align: "MidlineLeft", mat: TMAT.dark });
k.text(`${H}/WeatherBanner/Desc`, "좋은 날씨 · 변화 없음", 19, "#cfe8ff", { rect: R(104, 54, 640, 28), align: "MidlineLeft" });
// corner: weather · region · speed
k.image(`${H}/Corner`, BTN.wood, { type: "Sliced", rect: R(1500, 14, 404, 70), color: DARK(0.6) });
k.image(`${H}/Corner/WeatherIcon`, ART("Icons/w_clear"), { rect: RC(32, 22, 28, 28), aspect: true });
k.text(`${H}/Corner/Weather`, "맑음", 22, "#5fb8ff", { rect: R(50, 6, 200, 32), align: "MidlineLeft", mat: TMAT.dark });
k.text(`${H}/Corner/Region`, "숲 · 스테이지 1", 18, "#e8dcc4", { rect: R(18, 38, 260, 26), align: "MidlineLeft" });
k.text(`${H}/Corner/Speed`, "▶ ×1", 22, "#bbbbbb", { rect: R(230, 6, 154, 32), align: "MidlineRight" });
k.text(`${H}/Corner/Hint`, "누르기·F·우클릭 배속", 15, "#cbbfa8", { rect: R(184, 38, 200, 26), align: "MidlineRight" });
k.button(`${H}/PauseButton`, `${ic("pause")} 일시정지`, "pause", { rect: R(1528, 978, 176, 64), size: 24 });
k.button(`${H}/CodexButton`, `${ic("book")} 도감 0/75`, "codex", { rect: R(1716, 978, 176, 64), size: 24 });
b.go(`${H}/AimHint`, { RectTransform: STRETCH() });
k.text(`${H}/AimHint/Hint1`, "핀볼을 잡고 뒤로 끌었다 놓으면 발사!", 34, "#ffffff", { rect: RC(960, 830, 1400, 50), mat: TMAT.dark });
k.text(`${H}/AimHint/Hint2`, "발사 후에는 마우스를 좌우로 움직여 바로 받아치세요 (안 움직여도 괜찮아요)", 20, "#f0f0e0", { rect: RC(960, 872, 1600, 34), mat: TMAT.dark });
k.image(`${H}/EndDim`, ART("FX/square"), { rect: STRETCH(), color: col("#000000", 0.35) });
k.text(`${H}/EndDim/Title`, "수확 끝!", 110, "#fff3b0", { rect: RC(960, 480, 900, 140), mat: TMAT.brown });
k.image(`${H}/PausedDim`, ART("FX/square"), { rect: STRETCH(), color: col("#000000", 0.35) });
await b.send("round HUD");

// combo · fever · flash (above the HUD)
const C = `${RU}/ComboHUD`;
full(C, { ComboHUD: {} });
full(`${C}/Flames`);
b.go(`${C}/Combo`, { RectTransform: RC(1590, 300, 10, 10) });
k.text(`${C}/Combo/Number`, "0", 60, "#ffffff", { rect: RC(0, 0, 500, 120), mat: TMAT.brown });
k.text(`${C}/Combo/Label`, "COMBO", 26, "#ffffff", { rect: RC(0, 40, 400, 50), mat: TMAT.brown });
k.text(`${C}/FeverMul`, "점수 ×1.5", 24, "#ffe36e", { rect: RC(1590, 400, 400, 40), mat: TMAT.brown });
b.go(`${C}/FeverMsg`, { RectTransform: RC(960, 470, 10, 10) });
k.text(`${C}/FeverMsg/Title`, "FEVER!", 80, "#ffe9a0", { rect: RC(0, 0, 1400, 120), mat: TMAT.brown });
k.text(`${C}/FeverMsg/Sub`, "", 36, "#ffe9a0", { rect: RC(0, 74, 900, 50), mat: TMAT.brown });
k.image(`${C}/Flash`, ART("FX/square"), { rect: STRETCH(), color: col("#fffff0", 0) });
await b.send("combo HUD");
console.log("ui part 1 done");
