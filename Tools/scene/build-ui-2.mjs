#!/usr/bin/env node
// UI part 2: settlement, tax bill, modal panels (pause, reset, records, bulk, pre-round, codex), toast, debug.
import { Builder, call, ART, FONT, TMAT, col, R, RC, RT, STRETCH, img, txt, hlay, vlay, le, outline } from "./mcp.mjs";
import { makeKit, WOOD, WOOD_D, CREAM, INK, PANEL, PAPER, BTN, ic } from "./ui-kit.mjs";

const b = new Builder();
const k = makeKit(b);
const full = (p, comps = {}) => b.go(p, { RectTransform: STRETCH(), ...comps });
const CAT = { ed: ["식용", "#f28c28"], md: ["약용", "#3fae4a"], ps: ["독", "#9b4fd1"] };
const box = (path, o = {}) => k.image(path, PAPER, { type: "Sliced", color: col(o.bg ?? "#fffaf0"), outline: o.border ?? "#e2cfa8", ow: o.ow ?? 3, le: o.le, rect: o.rect, extra: o.extra });

for (const n of ["SettleScreen", "TaxScreen", "Modal", "Toast", "DebugPanel"]) await call("manage_gameobject", { action: "delete", target: `Canvas/${n}`, search_method: "by_path" });

// ---------- settlement ----------
const S = "Canvas/SettleScreen";
full(S, { SettleScreen: {} });
k.panel(`${S}/Card`, 900, { gap: 8, pad: { left: 50, right: 50, top: 28, bottom: 30 } });
k.text(`${S}/Card/Title`, "수확 완료!", 72, "#e8892a", { mat: TMAT.wood, le: { h: 84 } });
b.go(`${S}/Card/BasketRow`, { RectTransform: {}, LayoutElement: le({ h: 110 }) });
k.image(`${S}/Card/BasketRow/Basket`, BTN.wood, { type: "Sliced", rect: RT(0, 0, 380, 110), color: col("#a8703c"), extra: { RectMask2D: {} } });
k.image(`${S}/Card/BasketRow/Basket/Shroom`, ART("Mushrooms/Single/ed0"), { rect: R(0, 0, 46, 46, { pivot: [0.5, 0.5] }), aspect: true });
b.go(`${S}/Card/Gains`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 14, cw: true, ch: true, fh: true }), LayoutElement: le({ h: 64 }) });
for (const [c, [n, colr]] of Object.entries(CAT)) {
  box(`${S}/Card/Gains/${c}`, { border: colr, extra: { HorizontalLayoutGroup: hlay({ gap: 8, pad: { left: 8, right: 8, top: 4, bottom: 4 }, cw: true, ch: true }) }, le: { flex: 1 } });
  k.image(`${S}/Card/Gains/${c}/Icon`, ART("Icons/" + c), { aspect: true, le: { w: 40, h: 40 } });
  k.text(`${S}/Card/Gains/${c}/Name`, n, 22, INK, { le: {} });
  k.text(`${S}/Card/Gains/${c}/Amount`, "+0개", 30, colr, { le: {} });
}
k.row(`${S}/Card/SaleRow`, "창고로 들어간 버섯 가치 <size=60%><color=#8a6a4a>(상점에서 팔면 골드)</color></size>", "0");
k.row(`${S}/Card/ScoreRow`, "얻은 점수 → 골드", "0");
k.row(`${S}/Card/BonusRow`, "보너스 골드", "+0");
k.row(`${S}/Card/DebtRow`, "체납금 자동 차감", "−0", { vcolor: "#d23a2a" });
k.row(`${S}/Card/GoldRow`, "받은 골드", "0", { vcolor: "#c99a00" });
k.body(`${S}/Card/Meta`, "", 18, "#8a6a4a", { le: {} });
box(`${S}/Card/NewTheme`, { bg: "#e8f4ff", border: "#5fb8ff", extra: { VerticalLayoutGroup: vlay({ pad: { left: 10, right: 10, top: 10, bottom: 10 } }) } });
k.text(`${S}/Card/NewTheme/Text`, "새 지역", 26, INK, { wrap: true });
for (const [name, head] of [["Unlocks", `${ic("md")} 새로 해금된 버섯 (다음 라운드부터 등장)`], ["News", "도감 소식"]]) {
  b.go(`${S}/Card/${name}`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 6 }) });
  k.text(`${S}/Card/${name}/Head`, head, 22, INK, { le: {} });
  b.go(`${S}/Card/${name}/Cards`, { RectTransform: {}, GridLayoutGroup: { cellSize: [250, 70], spacing: [10, 10], constraint: "FixedColumnCount", constraintCount: 3, childAlignment: "UpperCenter" }, ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" } });
}
b.go(`${S}/Card/Records`, { RectTransform: {}, VerticalLayoutGroup: vlay({}) });
k.text(`${S}/Card/Records/Text`, "", 22, "#d2691e", { wrap: true });
b.go(`${S}/Card/Buttons`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 14, cw: false, ch: false }), LayoutElement: le({ h: 84 }) });
k.button(`${S}/Card/Buttons/Shop`, `${ic("md")} 버섯 상점`, "shop", { rect: R(0, 0, 250, 72), size: 30, dim: true });
k.button(`${S}/Card/Buttons/ToTree`, "균사 트리로 ▶", "totree", { rect: R(0, 0, 330, 72), size: 30, style: "go" });
await b.send("settlement");

// ---------- tax bill ----------
const T = "Canvas/TaxScreen";
full(T, { TaxScreen: {} });
k.panel(`${T}/Card`, 980, { gap: 10, pad: { left: 50, right: 50, top: 28, bottom: 30 } });
k.text(`${T}/Card/Title`, `${ic("tax")} 세금 고지서`, 72, "#e8892a", { mat: TMAT.wood, le: { h: 84 } });
k.body(`${T}/Card/Sub`, "사이클 1", 20, "#6a5040", { le: {} });
b.go(`${T}/Card/Bill`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 8 }) });
k.row(`${T}/Card/Bill/BillRow`, "납부할 세금", "0");
k.row(`${T}/Card/Bill/HaveRow`, "보유 골드", "0");
k.row(`${T}/Card/Bill/InvRow`, "창고 버섯", "", { vsize: 18 });
b.go(`${T}/Card/Bill/Options`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 18, cw: true, ch: true, fh: true }), LayoutElement: le({ h: 220 }) });
for (const [n, label, act, style] of [["Pay", "납부하기", "taxpay", "go"], ["Skip", "미납하기", "taxskip", "danger"]]) {
  box(`${T}/Card/Bill/Options/${n}`, { extra: { VerticalLayoutGroup: vlay({ gap: 10, pad: { left: 16, right: 16, top: 16, bottom: 16 }, align: "UpperCenter", cw: true, fw: true }) }, le: { flex: 1 } });
  k.button(`${T}/Card/Bill/Options/${n}/Button`, label, act, { style, size: 32, le: { h: 72 } });
  k.body(`${T}/Card/Bill/Options/${n}/Note`, "", 16, "#6a5040", { le: {} });
}
b.go(`${T}/Card/Result`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 10 }) });
k.text(`${T}/Card/Result/Text`, "", 34, INK, { wrap: true, le: {} });
b.go(`${T}/Card/Result/Unlocks`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 6 }) });
k.text(`${T}/Card/Result/Unlocks/Head`, `${ic("tax")} 세금 납부로 해금된 버섯`, 22, INK, { le: {} });
b.go(`${T}/Card/Result/Unlocks/Cards`, { RectTransform: {}, GridLayoutGroup: { cellSize: [250, 70], spacing: [10, 10], constraint: "FixedColumnCount", constraintCount: 3, childAlignment: "UpperCenter" }, ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" } });
k.body(`${T}/Card/Result/Meta`, "", 18, "#8a6a4a", { le: {} });
b.go(`${T}/Card/Result/ButtonRow`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ cw: false, ch: false }), LayoutElement: le({ h: 80 }) });
k.button(`${T}/Card/Result/ButtonRow/AfterTax`, "균사 트리로 ▶", "aftertax", { rect: R(0, 0, 330, 72), size: 30, style: "go" });
await b.send("tax");

// ---------- modal layer ----------
const M = "Canvas/Modal";
full(M, { Image: { sprite: ART("FX/square"), color: col("#140c06", 0.55), raycastTarget: true }, ModalHost: {} });

// pause
k.panel(`${M}/PausePanel`, 560, { gap: 16 });
k.h2(`${M}/PausePanel/Title`, "일시정지");
k.button(`${M}/PausePanel/Resume`, "계속하기", "resume", { size: 34, le: { h: 72 } });
k.button(`${M}/PausePanel/EndNow`, "라운드 끝내고 정산하기", "endnow", { style: "ghost", le: { h: 56 } });
k.button(`${M}/PausePanel/Mute`, "소리 켜기/끄기 (M)", "mute", { style: "ghost", size: 20, le: { h: 50 } });
k.close(`${M}/PausePanel`);

// reset
k.panel(`${M}/ResetPanel`, 560, { gap: 16 });
k.h2(`${M}/ResetPanel/Title`, "초기화");
k.body(`${M}/ResetPanel/Sub`, "저장된 진행을 모두 지울까요? 되돌릴 수 없어요.", 17, "#6a5040", { le: {} });
k.button(`${M}/ResetPanel/Yes`, "모두 지우기", "reset-yes", { style: "danger", size: 30, le: { h: 70 } });
k.button(`${M}/ResetPanel/Cancel`, "취소", "close", { style: "ghost", le: { h: 54 } });

// records
k.panel(`${M}/RecordsPanel`, 640, { gap: 8, comps: { RecordsPanel: {} } });
k.h2(`${M}/RecordsPanel/Title`, `${ic("trophy")} 기록`);
b.go(`${M}/RecordsPanel/Rows`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 8 }) });
for (let i = 0; i < 9; i++) k.row(`${M}/RecordsPanel/Rows/Row${i}`, "기록", "0", { size: 22, vsize: 22, h: 48 });
k.close(`${M}/RecordsPanel`);
await b.send("pause/reset/records");

// bulk
k.panel(`${M}/BulkPanel`, 760, { gap: 12, comps: { BulkPanel: {} } });
k.h2(`${M}/BulkPanel/Title`, `${ic("s_bolt")} 일괄 강화`);
k.body(`${M}/BulkPanel/Sub`, "가장 싼 강화부터 골드가 되는 만큼 한 번에 사요. 열린 노드만 대상이에요(핵심 코어 포함, 균사석 노드 제외).\n노드 하나만 끝까지 올리려면 균사 트리에서 그 노드를 <b>Shift+클릭</b> 또는 <b>우클릭</b>하세요.", 17, "#6a5040", { le: {} });
b.go(`${M}/BulkPanel/Tabs`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 10, cw: false, ch: false }), LayoutElement: le({ h: 56 }) });
for (const [arg, n] of [["all", "전체"], ["ed", "식용 균사"], ["md", "약용 균사"], ["ps", "독 균사"]]) k.button(`${M}/BulkPanel/Tabs/${arg}`, n, "bulktab", { arg, rect: R(0, 0, 150, 50) });
b.go(`${M}/BulkPanel/Keep`, { RectTransform: {}, Toggle: { isOn: true }, HorizontalLayoutGroup: hlay({ gap: 10, cw: false, ch: false }), LayoutElement: le({ h: 40 }) });
k.image(`${M}/BulkPanel/Keep/Box`, PAPER, { type: "Sliced", rect: R(0, 0, 30, 30), outline: WOOD_D, ow: 2, ray: true });
k.image(`${M}/BulkPanel/Keep/Box/Check`, ART("FX/circle"), { rect: STRETCH(7, 7, 7, 7), color: col("#3a8a2a") });
k.text(`${M}/BulkPanel/Keep/Label`, "다음 세금 예상액만큼 남기기", 20, INK, { rect: R(0, 0, 560, 34), align: "MidlineLeft" });
k.text(`${M}/BulkPanel/Plan`, "", 22, INK, { wrap: true, le: {} });
k.button(`${M}/BulkPanel/Go`, "한 번에 강화하기", "bulkgo", { style: "go", size: 32, le: { h: 72 } });
k.close(`${M}/BulkPanel`);
await b.send("bulk");

// pre-round
k.panel(`${M}/PreRoundPanel`, 1560, { gap: 10, comps: { PreRoundPanel: {} } });
b.go(`${M}/PreRoundPanel/Header`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 16, cw: true, ch: true }), LayoutElement: le({ h: 54 }) });
k.text(`${M}/PreRoundPanel/Header/Title`, "수확 준비", 42, INK, { le: {} });
k.text(`${M}/PreRoundPanel/Header/Stage`, "스테이지 1", 30, "#8a6a4a", { le: {} });
k.text(`${M}/PreRoundPanel/ThemesHead`, "지역 <size=65%><color=#8a6a4a>(10스테이지마다 새 지역이 열려요 · 지역 전용 버섯은 그 지역에서만 나와요)</color></size>", 26, INK, { align: "MidlineLeft", le: {} });
b.go(`${M}/PreRoundPanel/Themes`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 12, cw: true, ch: true, fh: true }), LayoutElement: le({ h: 200 }) });
for (const id of ["forest", "night", "field", "sea", "ruins"]) {
  const p = `${M}/PreRoundPanel/Themes/${id}`;
  b.go(p, { RectTransform: {}, Image: { sprite: PAPER, type: "Sliced", color: col("#fffaf0"), raycastTarget: true }, Outline: outline("#cdb68e", 3), CanvasGroup: {}, Button: {}, UIAction: { act: "pretheme", arg: id }, UIButtonFx: {}, LayoutElement: le({ flex: 1 }), VerticalLayoutGroup: vlay({ gap: 4, pad: { left: 10, right: 10, top: 10, bottom: 10 } }) });
  k.image(`${p}/Icon`, ART("Icons/t_" + id), { aspect: true, le: { h: 56 } });
  k.text(`${p}/Name`, id, 20, INK, { le: {} });
  k.body(`${p}/Desc`, "", 13, "#6a5040", { le: { h: 40 } });
  k.text(`${p}/Count`, "", 15, INK, { le: {} });
}
k.text(`${M}/PreRoundPanel/HvHead`, "활성 수확기 <size=65%><color=#8a6a4a>(핀볼마다 무작위 · 공방에서 켜고 끄기)</color></size>", 26, INK, { align: "MidlineLeft", le: {} });
k.text(`${M}/PreRoundPanel/Harvesters`, "", 20, INK, { align: "MidlineLeft", le: {} });
k.text(`${M}/PreRoundPanel/DishesHead`, "준비된 요리 <size=65%><color=#8a6a4a>(이번 라운드에 적용)</color></size>", 26, INK, { align: "MidlineLeft", le: {} });
k.text(`${M}/PreRoundPanel/Dishes`, "", 20, INK, { align: "MidlineLeft", le: {} });
k.body(`${M}/PreRoundPanel/WeatherNote`, "날씨는 출발할 때 무작위로 정해져요.", 17, "#6a5040", { align: "Left", le: {} });
b.go(`${M}/PreRoundPanel/GoRow`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ cw: false, ch: false }), LayoutElement: le({ h: 84 }) });
k.button(`${M}/PreRoundPanel/GoRow/Go`, "출발! ▶", "startround", { rect: R(0, 0, 300, 76), style: "go", size: 34 });
k.close(`${M}/PreRoundPanel`);
await b.send("pre-round");

// codex
const CX = `${M}/CodexPanel`;
k.panel(CX, 1780, { gap: 8, pad: { left: 30, right: 30, top: 22, bottom: 22 }, comps: { CodexPanel: {} } });
b.go(`${CX}/Header`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 14, cw: true, ch: true }), LayoutElement: le({ h: 50 }) });
k.text(`${CX}/Header/Title`, `${ic("book")} 버섯 도감`, 40, INK, { le: {} });
k.text(`${CX}/Header/Count`, "0/75", 30, "#8a6a4a", { le: {} });
k.body(`${CX}/Sub`, "버섯은 자동으로 해금돼요: 앞 버섯을 정해진 수만큼 수확하거나, 세금을 정해진 횟수만큼 내거나, 새 지역이 열리면(그 지역 전용) 열립니다. 같은 버섯을 많이 딸수록 별(최대 5성)이 붙고, 별마다 그 버섯의 고유 능력치가 영구로 올라요. 상점에서 팔아도 도감·별은 그대로예요.", 15, "#6a5040", { le: {} });
b.go(`${CX}/Columns`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 16, cw: true, ch: true, fh: true }), LayoutElement: le({ h: 590 }) });
for (const [c, [n, colr]] of Object.entries(CAT)) {
  const col_ = `${CX}/Columns/${c}`;
  box(col_, { extra: { VerticalLayoutGroup: vlay({ gap: 8, pad: { left: 10, right: 10, top: 8, bottom: 10 } }) }, le: { flex: 1 } });
  b.go(`${col_}/Head`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ cw: true, ch: true }), LayoutElement: le({ h: 32 }) });
  k.text(`${col_}/Head/Name`, `${n} 버섯`, 24, colr, { align: "MidlineLeft", le: { flex: 1 } });
  k.text(`${col_}/Head/Count`, "0/25", 24, "#8a6a4a", { align: "MidlineRight", le: {} });
  b.go(`${col_}/Grid`, { RectTransform: {}, GridLayoutGroup: { cellSize: [100, 98], spacing: [8, 8], constraint: "FixedColumnCount", constraintCount: 5, childAlignment: "UpperCenter" }, LayoutElement: le({ h: 530 }) });
  for (let i = 0; i < 25; i++) {
    b.raw("manage_gameobject", { action: "create", name: `${c}${i}`, parent: `${col_}/Grid`, prefab_path: "Assets/Prefabs/UI/CodexCell.prefab" });
    b.raw("manage_components", { action: "set_property", target: `${col_}/Grid/${c}${i}`, search_method: "by_path", component_type: "CodexCell", property: "id", value: `${c}${i}` });
  }
}
await b.send("codex grids");
b.go(`${CX}/Bottom`, { RectTransform: {}, HorizontalLayoutGroup: hlay({ gap: 16, cw: true, ch: true, fh: true }), LayoutElement: le({ h: 330 }) });
b.go(`${CX}/Bottom/Left`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 10 }), LayoutElement: le({ flex: 1.3 }) });
box(`${CX}/Bottom/Left/Specials`, { extra: { VerticalLayoutGroup: vlay({ gap: 4, pad: { left: 12, right: 12, top: 6, bottom: 8 } }) }, le: { h: 140 } });
k.text(`${CX}/Bottom/Left/Specials/Head`, "특수 버섯", 22, INK, { align: "MidlineLeft", le: {} });
b.go(`${CX}/Bottom/Left/Specials/Grid`, { RectTransform: {}, GridLayoutGroup: { cellSize: [88, 96], spacing: [6, 6], constraint: "FixedColumnCount", constraintCount: 10, childAlignment: "UpperLeft" }, LayoutElement: le({ h: 96 }) });
for (const id of ["fire", "spark", "dew", "coin", "star", "leaf", "moon", "wind", "rock", "chest"]) {
  b.raw("manage_gameobject", { action: "create", name: `sp_${id}`, parent: `${CX}/Bottom/Left/Specials/Grid`, prefab_path: "Assets/Prefabs/UI/CodexCell.prefab" });
  b.raw("manage_components", { action: "set_property", target: `${CX}/Bottom/Left/Specials/Grid/sp_${id}`, search_method: "by_path", component_type: "CodexCell", property: "id", value: `sp:${id}` });
}
box(`${CX}/Bottom/Left/DetailEmpty`, { le: { h: 180 } });
k.text(`${CX}/Bottom/Left/DetailEmpty/Text`, "버섯을 눌러 자세히 보기", 24, "#9a8a70", { rect: STRETCH() });
box(`${CX}/Bottom/Left/Detail`, { extra: { HorizontalLayoutGroup: hlay({ gap: 18, pad: { left: 14, right: 14, top: 10, bottom: 10 }, align: "UpperLeft", cw: true, ch: true }) }, le: { h: 180 } });
k.image(`${CX}/Bottom/Left/Detail/Icon`, ART("Mushrooms/Single/ed0"), { aspect: true, le: { w: 160, h: 160 } });
b.go(`${CX}/Bottom/Left/Detail/Info`, { RectTransform: {}, VerticalLayoutGroup: vlay({ gap: 2, align: "UpperLeft" }), LayoutElement: le({ flex: 1 }) });
k.text(`${CX}/Bottom/Left/Detail/Info/Name`, "이름", 30, INK, { align: "Left", le: {} });
k.body(`${CX}/Bottom/Left/Detail/Info/Desc`, "", 17, INK, { align: "Left", le: {} });
k.text(`${CX}/Bottom/Left/Detail/Info/Stars`, "", 16, "#8a6a00", { align: "Left", le: {} });
k.body(`${CX}/Bottom/Left/Detail/Info/Ability`, "", 14, "#2a6a9a", { align: "Left", le: {} });
k.body(`${CX}/Bottom/Left/Detail/Info/Trait`, "", 14, "#6a3a9a", { align: "Left", le: {} });
k.body(`${CX}/Bottom/Left/Detail/Info/Warn`, "⚠ 실제로는 먹거나 만지지 마세요", 14, "#c0392b", { align: "Left", le: {} });
k.body(`${CX}/Bottom/Left/Detail/Info/Record`, "", 12, "#7a6a5a", { align: "Left", le: {} });
box(`${CX}/Bottom/Sets`, { le: { flex: 1 } });
b.go(`${CX}/Bottom/Sets/Scroll`, { RectTransform: STRETCH(12, 10, 12, 10), ScrollRect: { horizontal: false, vertical: true, movementType: "Clamped", scrollSensitivity: 30 } });
b.go(`${CX}/Bottom/Sets/Scroll/Viewport`, { RectTransform: STRETCH(), Image: { color: col("#ffffff", 0.01), raycastTarget: true }, RectMask2D: {} });
b.go(`${CX}/Bottom/Sets/Scroll/Viewport/Content`, { RectTransform: { anchorMin: [0, 1], anchorMax: [1, 1], pivot: [0.5, 1], anchoredPosition: [0, 0], sizeDelta: [0, 600] }, VerticalLayoutGroup: vlay({ gap: 6 }), ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" } });
k.text(`${CX}/Bottom/Sets/Scroll/Viewport/Content/AbHead`, "★ 별 능력 합계", 22, INK, { align: "Left", le: {} });
k.body(`${CX}/Bottom/Sets/Scroll/Viewport/Content/AbList`, "", 14, INK, { align: "Left", le: {} });
k.body(`${CX}/Bottom/Sets/Scroll/Viewport/Content/SetList`, "", 15, INK, { align: "Left", font: FONT.jua, le: {} });
k.close(CX);
await b.send("codex bottom");

// ---------- toast & debug ----------
b.go("Canvas/Toast", {
  RectTransform: RT(0, 110, 400, 50),
  Image: { sprite: BTN.wood, type: "Sliced", color: col("#28190f", 0.9), raycastTarget: false },
  CanvasGroup: { blocksRaycasts: false, interactable: false },
  HorizontalLayoutGroup: hlay({ pad: { left: 24, right: 24, top: 10, bottom: 10 }, cw: true, ch: true }),
  ContentSizeFitter: { horizontalFit: "PreferredSize", verticalFit: "PreferredSize" },
  Toast: {},
});
k.text("Canvas/Toast/Text", "", 22, "#fff6e0", { le: {} });
b.go("Canvas/DebugPanel", {
  RectTransform: R(1674, 110, 230, 600),
  Image: { sprite: BTN.wood, type: "Sliced", color: col("#14141e", 0.9), raycastTarget: true },
  VerticalLayoutGroup: vlay({ gap: 6, pad: { left: 10, right: 10, top: 10, bottom: 10 } }),
  ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: "PreferredSize" },
  DebugPanel: {},
});
k.body("Canvas/DebugPanel/Head", "<b>디버그</b>", 14, "#eeeeee", { align: "Left", le: {} });
for (const [act, n, arg] of [["dbg-res", "골드 ×1000"], ["dbg-spd", "배속 1", "1"], ["dbg-spd", "배속 2", "2"], ["dbg-spd", "배속 4", "4"], ["dbg-seeds", "모든 버섯 해금"], ["dbg-inf", "제한시간 무한"], ["dbg-special", "특수 버섯 소환 (라운드 중)"], ["dbg-tax", "다음 정산 때 세금 청구"], ["dbg-stage", "스테이지 +10 (다음 지역)"], ["dbg-specials", "특수 버섯 전부 포획"], ["dbg-gem", "균사석 +100 · 포자 +100"], ["dbg-preset", "밸런스: 조정안", "tuned"], ["dbg-preset", "밸런스: 명세 원본", "spec"]])
  k.button(`Canvas/DebugPanel/${act}${arg ? "_" + arg : ""}`, n, act, { arg, size: 14, le: { h: 30 } });
k.body("Canvas/DebugPanel/Info", "", 12, "#aaaaaa", { align: "Left", le: {} });
await b.send("toast + debug");

// ---------- initial visibility ----------
// children first: a hidden parent's children can no longer be found by path
for (const p of ["Canvas/TreeScreen/Tooltip", `${M}/PausePanel`, `${M}/ResetPanel`, `${M}/RecordsPanel`, `${M}/BulkPanel`, `${M}/PreRoundPanel`, `${M}/CodexPanel`,
  "Canvas/TreeScreen", "Canvas/RoundUI", "Canvas/SettleScreen", "Canvas/TaxScreen", "Canvas/Modal", "Canvas/DebugPanel", "World/Round", "World/TreeWorld"])
  b.raw("manage_gameobject", { action: "modify", target: p, search_method: "by_path", set_active: false });
await b.send("visibility");
console.log("ui part 2 done");
