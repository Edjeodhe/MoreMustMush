// 개발자 패널 프리팹 (Assets/Resources/Dev/DevPanel.prefab): 씬에 잠깐 만들어 프리팹으로 저장하고 씬에서 지운다.
// App이 개발용(에디터·Development Build)일 때만 복제한다. ` 키 = Window 여닫기, Shift+숫자 = 치트 (DebugPanel.CHEATS)
//   node Tools/scene/build-devpanel.mjs
import { call, destroyPaths, Builder, col, R, txt } from "./mcp.mjs";

const ROOT = "DevPanel", PREFAB = "Assets/Resources/Dev/DevPanel.prefab";
const FONT = "Assets/Fonts/ONEMobilePOP SDF.asset";
const panel = (a) => ({ color: col("#000000", a), raycastTarget: false });
const text = (s, size, color, align = "TopLeft") => ({ ...txt(s, size, color, { align, wrap: true, font: FONT }), raycastTarget: false });
const ONE = { localScale: [1, 1, 1] };

await destroyPaths([ROOT]);
const b = new Builder();
b.go(ROOT, {
  Canvas: { renderMode: "ScreenSpaceOverlay", sortingOrder: 30000 },
  CanvasScaler: { uiScaleMode: "ScaleWithScreenSize", referenceResolution: [1920, 1080], screenMatchMode: "MatchWidthOrHeight", matchWidthOrHeight: 0.5 },
  "MoreMush.DevOnly": {},
  "MoreMush.DebugPanel": {},
});
// 화면에 늘 떠 있는 안내는 두지 않는다 (다른 사람이 보지 않게, 2026-10-20 사용자). ` 키로 여닫는 창 (왼쪽 위): 상태 + 치트 키 표
b.go(`${ROOT}/Window`, { RectTransform: { ...R(10, 10, 660, 840), ...ONE }, Image: panel(0.72) });
b.go(`${ROOT}/Window/Info`, { RectTransform: { ...R(18, 14, 624, 330), ...ONE }, TextMeshProUGUI: text("FPS", 18, "#ffffff") });
b.go(`${ROOT}/Window/Keys`, { RectTransform: { ...R(18, 356, 624, 470), ...ONE }, TextMeshProUGUI: text("치트 키", 21, "#ffe9a8") });
await b.send("devpanel");

// 씬에 만든 직후 id로 DebugPanel 참조를 잇는다
const ids = {};
for (const p of ["Window", "Window/Info", "Window/Keys"]) {
  const r = await call("find_gameobjects", { search_term: `${ROOT}/${p}`, search_method: "by_path", include_inactive: true });
  ids[p] = r?.data?.instanceIDs?.[0] ?? r?.data?.items?.[0]?.instanceID ?? r?.data?.results?.[0]?.instanceID;
  if (!ids[p]) console.error("not found", p, JSON.stringify(r).slice(0, 300));
}
for (const [prop, p] of [["window", "Window"], ["info", "Window/Info"], ["keys", "Window/Keys"]]) {
  const r = await call("manage_components", { action: "set_property", target: ROOT, search_method: "by_path", component_type: "DebugPanel", property: prop, value: ids[p] });
  if (!r?.success) console.error("wire", prop, JSON.stringify(r).slice(0, 300));
}
await call("manage_gameobject", { action: "modify", target: `${ROOT}/Window`, search_method: "by_path", set_active: false });

const r = await call("manage_prefabs", { action: "create_from_gameobject", target: ROOT, prefab_path: PREFAB, allow_overwrite: true });
console.log("prefab", r?.success, r?.message);
await destroyPaths([ROOT]);
