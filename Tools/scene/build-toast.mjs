#!/usr/bin/env node
// 알림 띠 Canvas/Toast (NoticeToast가 App.Notify 문구를 위 가운데에 잠깐 띄운다), built through MCP for Unity.
// 네 씬(MainMenu·MainGame·OutGame·Stage)을 차례로 열어 Canvas/Toast를 지우고 다시 만든 뒤 저장한다.
// 글자는 ONEMobilePOP(한·영 전용 UI 폰트) — LocText 없이 두면 TextLangFont가 다른 언어에서 그 언어 제목 폰트로 바꾼다.
import { Builder, call, col, img, txt, RT, STRETCH } from "./mcp.mjs";

const SCENES = ["MainMenu", "MainGame", "OutGame", "Stage"];
const P = "Canvas/Toast";
const PAPER = "Assets/Resources/UI/panel_paper.png";
const POP = "Assets/Fonts/ONEMobilePOP SDF.asset";

for (const s of SCENES) {
  const r = await call("manage_scene", { action: "load", path: `Assets/Scenes/${s}.unity` });
  if (!r?.success) { console.error(`load ${s} failed`, JSON.stringify(r).slice(0, 300)); process.exit(1); }
  await call("manage_gameobject", { action: "delete", target: P, search_method: "by_path" });

  const b = new Builder();
  b.go(P, {
    RectTransform: {},
    Image: img(PAPER, { type: "Sliced" }),
    CanvasGroup: { alpha: 0, interactable: false, blocksRaycasts: false },
    NoticeToast: {},
  });
  // 캔버스 스케일(0.01)을 이어받지 않게 만든 뒤 로컬 값을 다시 넣는다
  b.raw("manage_components", { action: "set_property", target: P, search_method: "by_path", component_type: "RectTransform",
    properties: { localScale: [1, 1, 1], anchoredPosition3D: [0, -110, 0], ...RT(0, 110, 960, 110) } });
  b.go(`${P}/Text`, { RectTransform: {}, TextMeshProUGUI: txt("", 32, "#4a3426", { wrap: true, font: POP }) });
  b.raw("manage_components", { action: "set_property", target: `${P}/Text`, search_method: "by_path", component_type: "RectTransform",
    properties: { localScale: [1, 1, 1], anchoredPosition3D: [0, 0, 0], ...STRETCH(28, 12, 28, 12) } });
  await b.send(`toast: ${s}`);

  const saved = await call("manage_scene", { action: "save" });
  console.log(`  save ${s}:`, saved?.success ? "ok" : JSON.stringify(saved).slice(0, 300));
}
