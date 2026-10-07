#!/usr/bin/env node
// Builds the world-space part of the Game scene through MCP for Unity:
// camera + systems, title backdrop, round field, mycelium tree (88 node prefab instances).
import fs from "node:fs";
import { Builder, call, ART, MAT, TMAT, col } from "./mcp.mjs";

const b = new Builder();
const SR = (sprite, order, color = "#ffffff", o = {}) => ({ ...(sprite ? { sprite } : {}), sharedMaterial: o.mat ?? MAT.sprite, sortingOrder: order, color: col(color, o.a), ...(o.sliced ? { drawMode: "Sliced" } : {}) });
const LR = (order, width = 0.04, color = "#ffffff", a = 1) => ({ sharedMaterial: MAT.sprite, useWorldSpace: false, positionCount: 0, sortingOrder: order, widthMultiplier: width, numCapVertices: 4, startColor: col(color, a), endColor: col(color, a) });
const P = (x, y, z = 0) => [x / 100, -y / 100, z];

for (const n of ["World", "Systems", "EventSystem"]) await call("manage_gameobject", { action: "delete", target: n, search_method: "by_name" });

// ===== camera: orthographic 1920×1080 px view (1 unit = 100 px), letterboxed to 16:9 =====
b.raw("manage_gameobject", { action: "modify", target: "Main Camera", search_method: "by_name", position: [9.6, -5.4, -10], components_to_add: ["AspectLetterbox"] });
b.raw("manage_components", { action: "set_property", target: "Main Camera", search_method: "by_name", component_type: "Camera", properties: { orthographic: true, orthographicSize: 5.4, clearFlags: "SolidColor", backgroundColor: col("#1a120c"), nearClipPlane: 0.1, farClipPlane: 100 } });
b.go("Systems", { GameFlow: {}, Snd: {} });
b.go("EventSystem", { EventSystem: {}, InputSystemUIInputModule: {} });
await b.send("camera + systems");

// ===== title backdrop (town, grandpa, bouncing harvester) =====
b.go("World");
b.go("World/TitleBackdrop", { TitleBackdrop: {} });
b.go("World/TitleBackdrop/Background", { SpriteRenderer: SR(ART("Backgrounds/bg_title"), -100) }, { pos: P(960, 540, 1) });
b.raw("manage_gameobject", { action: "create", name: "Grandpa", parent: "World/TitleBackdrop", prefab_path: "Assets/Prefabs/Characters/GrandpaRig.prefab", position: P(250, 1020), scale: [2.2, 2.2, 1] });
b.go("World/TitleBackdrop/Harvester", { SpriteRenderer: SR(ART("Harvesters/sam"), 60) }, { pos: P(960, 540), scale: [0.17, 0.17, 1] });
await b.send("title backdrop");

// ===== round field =====
const F = "World/Round/Field";
b.go("World/Round", { RoundController: {}, RoundView: {} });
b.go(F);
b.go(`${F}/Background`, { SpriteRenderer: SR(ART("Backgrounds/bg_forest"), 0) }, { pos: P(960, 540, 1) });
for (const n of ["DeviceRoot", "ShroomRoot", "FxRoot", "BallRoot", "FlyerRoot"]) b.go(`${F}/${n}`);
b.go(`${F}/Bar`, { SpriteRenderer: SR(ART("UI/bar_log"), 3600, "#ffffff", { sliced: true }) }, { pos: P(960, 1000) });
b.go(`${F}/GiantWarn`, { SpriteRenderer: SR(ART("Props/warn_shadow"), 950) });
b.go(`${F}/FestTint`, { SpriteRenderer: SR(ART("FX/square"), 4100, "#ffbe3c", { a: 0.05 }) });
b.go(`${F}/AcornFrame`, { LineRenderer: LR(4100, 0.12, "#ffdc64", 0.5) });
b.go(`${F}/Weather`, { WeatherOverlay: {} });
b.go(`${F}/Weather/Mask`, { SpriteRenderer: SR(ART("FX/square"), 3900, "#ffffff", { mat: MAT.weather }) });
b.go(`${F}/Weather/Tint`, { SpriteRenderer: SR(ART("FX/square"), 3890, "#141e3c", { a: 0.2 }) });
b.go(`${F}/Weather/MoonIcon`, { SpriteRenderer: SR(ART("Icons/w_moon"), 3910) });
b.go(`${F}/SpecialShadow`, { SpriteRenderer: SR(ART("FX/soft"), 2150, "#000000", { a: 0.2 }) });
b.raw("manage_gameobject", { action: "create", name: "Special", parent: F, prefab_path: "Assets/Prefabs/Characters/CritterRig.prefab", position: P(960, 500) });
b.go(`${F}/SpecialRingBack`, { LineRenderer: LR(2190, 0.07, "#000000", 0.35) });
b.go(`${F}/SpecialRing`, { LineRenderer: LR(2191, 0.05, "#ff7a2a") });
b.go(`${F}/SpecialHpBack`, { SpriteRenderer: SR(ART("FX/square"), 2300, "#000000", { a: 0.5 }) });
b.go(`${F}/SpecialHpFill`, { SpriteRenderer: SR(ART("FX/square"), 2301) });
await b.send("round field");

// ===== mycelium tree =====
const T = "World/TreeWorld";
b.go(T, { TreeView: { camZ: 0.95 } });
b.go(`${T}/Soil`, { SpriteRenderer: SR(null, 4900) });
b.go(`${T}/Content`);
for (let i = 0; i < 3; i++) b.go(`${T}/Content/Trunks${i}`, { LineRenderer: LR(4995, 0.12, "#f2e6c8", 0.45) });
b.go(`${T}/Content/CenterGlow`, { SpriteRenderer: SR(ART("FX/soft"), 4996, "#fff0be", { a: 0.55 }) }, { scale: [2.6, 2.6, 1] });
b.raw("manage_gameobject", { action: "create", name: "Grandpa", parent: `${T}/Content`, prefab_path: "Assets/Prefabs/Characters/GrandpaRig.prefab", position: P(-14, 82), scale: [0.5, 0.5, 1] });
for (let i = 0; i < 3; i++) b.go(`${T}/Content/HubLabels${i}`, { TextMeshPro: { text: "균사", fontSize: 2.2, alignment: "Center", color: col("#f2e6c8"), textWrappingMode: "NoWrap", fontSharedMaterial: TMAT.dark, sortingOrder: 5050 } });
b.go(`${T}/Content/Nodes`);
await b.send("tree base");
b.raw("manage_components", { action: "set_property", target: `${T}/Content/Grandpa`, search_method: "by_path", component_type: "SortingGroup", property: "sortingOrder", value: 5005 });
const layout = JSON.parse(fs.readFileSync(new URL("./tree-layout.json", import.meta.url)));
for (const n of layout) {
  b.raw("manage_gameobject", { action: "create", name: n.id, parent: `${T}/Content/Nodes`, prefab_path: "Assets/Prefabs/Tree/TreeNode.prefab", position: P(n.x, n.y) });
  b.raw("manage_components", { action: "set_property", target: `${T}/Content/Nodes/${n.id}`, search_method: "by_path", component_type: "TreeNodeView", property: "nodeId", value: n.id });
}
await b.send("tree nodes");
console.log("world done");
