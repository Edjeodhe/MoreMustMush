// Tiny DSL that turns a hierarchy description into MCP for Unity commands (manage_gameobject create with
// components and properties), sent in batch_execute chunks. Used by the build-*.mjs scene scripts.
const URL_ = process.env.UNITY_MCP_URL || "http://127.0.0.1:8080/mcp";
let sessionId = null, nextId = 1;

async function rpc(method, params, notify = false) {
  const body = { jsonrpc: "2.0", method, ...(params ? { params } : {}) };
  if (!notify) body.id = nextId++;
  const headers = { "Content-Type": "application/json", Accept: "application/json, text/event-stream" };
  if (sessionId) headers["Mcp-Session-Id"] = sessionId;
  const res = await fetch(URL_, { method: "POST", headers, body: JSON.stringify(body) });
  sessionId = res.headers.get("mcp-session-id") ?? sessionId;
  if (notify) return null;
  const text = await res.text();
  const payloads = (res.headers.get("content-type") || "").includes("text/event-stream")
    ? text.split(/\r?\n/).filter((l) => l.startsWith("data:")).map((l) => JSON.parse(l.slice(5)))
    : [JSON.parse(text)];
  const msg = payloads.find((p) => p.id === body.id) ?? payloads.at(-1);
  if (msg.error) throw new Error(JSON.stringify(msg.error));
  return msg.result;
}

let connected = false;
export async function call(tool, args) {
  if (!connected) {
    await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "moremush-scene-builder", version: "1" } });
    await rpc("notifications/initialized", undefined, true);
    connected = true;
  }
  const r = await rpc("tools/call", { name: tool, arguments: args });
  const text = (r.content || []).filter((c) => c.type === "text").map((c) => c.text).join("\n");
  try { return JSON.parse(text); } catch { return { success: !r.isError, message: text }; }
}

// Delete every object at these scene paths, including inactive ones (manage_gameobject's path lookup skips
// objects under an inactive parent, so a rebuild would otherwise stack a second copy).
export async function destroyPaths(paths) {
  const list = paths.map((p) => JSON.stringify(p)).join(", ");
  const code = `int n = 0; var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
foreach (var path in new string[] { ${list} }) {
  var parts = path.Split(new[] { '/' }, 2);
  foreach (var root in scene.GetRootGameObjects()) {
    if (root.name != parts[0]) continue;
    var hits = new System.Collections.Generic.List<GameObject>();
    if (parts.Length == 1) hits.Add(root);
    else foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (AnimationUtility.CalculateTransformPath(t, root.transform) == parts[1]) hits.Add(t.gameObject);
    foreach (var g in hits) if (g != null) { Undo.DestroyObjectImmediate(g); n++; }
  }
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return n;`;
  const r = await call("execute_code", { action: "execute", code });
  console.log(`deleted ${r?.data?.result ?? "?"} object(s)`, r?.success === false ? JSON.stringify(r).slice(0, 300) : "");
}

// ===== value helpers =====
export function col(hex, a) {
  hex = hex.replace("#", "");
  if (hex.length === 3) hex = hex.split("").map((c) => c + c).join("");
  const n = (i) => parseInt(hex.slice(i, i + 2), 16) / 255;
  return { r: n(0), g: n(2), b: n(4), a: a ?? (hex.length === 8 ? n(6) : 1) };
}
export const ART = (p) => `Assets/Art/Generated/${p}.png`;
export const MAT = { sprite: "Assets/Materials/Sprite.mat", add: "Assets/Materials/SpriteAdditive.mat", weather: "Assets/Materials/WeatherMask.mat" };
export const FONT = { jua: "Assets/Fonts/Jua SDF.asset", noto: "Assets/Fonts/NotoSansKR SDF.asset" };
export const TMAT = { dark: "Assets/Fonts/Jua Outline Dark.mat", wood: "Assets/Fonts/Jua Outline Wood.mat", brown: "Assets/Fonts/Jua Outline Brown.mat" };

// RectTransform anchored to the parent's top-left; (x, y) is the rect's top-left in prototype pixels.
export const R = (x, y, w, h, o = {}) => ({ anchorMin: o.anchor ?? [0, 1], anchorMax: o.anchor ?? [0, 1], pivot: o.pivot ?? [0, 1], anchoredPosition: [x, -y], sizeDelta: [w, h] });
// RectTransform centered on (x, y) (top-left anchored)
export const RC = (x, y, w, h) => R(x, y, w, h, { pivot: [0.5, 0.5] });
// Stretch to the parent with insets
export const STRETCH = (l = 0, t = 0, r = 0, b = 0) => ({ anchorMin: [0, 0], anchorMax: [1, 1], pivot: [0.5, 0.5], anchoredPosition: [(l - r) / 2, (b - t) / 2], sizeDelta: [-(l + r), -(t + b)] });
// Anchored to the parent's top-center
export const RT = (x, y, w, h) => ({ anchorMin: [0.5, 1], anchorMax: [0.5, 1], pivot: [0.5, 1], anchoredPosition: [x, -y], sizeDelta: [w, h] });

export const img = (sprite, o = {}) => ({
  ...(sprite ? { sprite } : {}),
  type: o.type ?? (sprite && /UI\/(panel|btn)/.test(sprite) ? "Sliced" : "Simple"),
  color: o.color ?? col("#ffffff"),
  raycastTarget: o.ray ?? false,
  preserveAspect: o.aspect ?? false,
  ...(o.fill ? { type: "Filled", fillMethod: "Horizontal", fillOrigin: 0, fillAmount: 1 } : {}),
  ...(o.mat ? { material: o.mat } : {}),
  ...(sprite ? { pixelsPerUnitMultiplier: sliceDensity(sprite) } : {}),
});
// 9-slice borders of the generated UI kit are drawn big; this thins them to the prototype's 4–12 px frames.
export const sliceDensity = (sprite) => /panel_paper/.test(sprite) ? 6 : /panel_wood/.test(sprite) ? 4 : /btn_/.test(sprite) ? 2.2 : 1;

export const txt = (text, size, color = "#3b2414", o = {}) => ({
  text, fontSize: size, color: col(color), alignment: o.align ?? "Center", raycastTarget: false,
  textWrappingMode: o.wrap ? "Normal" : "NoWrap", overflowMode: o.overflow ?? "Overflow", richText: true,
  ...(o.font ? { font: o.font } : {}), ...(o.mat ? { fontSharedMaterial: o.mat } : {}),
  ...(o.lineSpacing != null ? { lineSpacing: o.lineSpacing } : {}),
});

export const vlay = (o = {}) => ({ spacing: o.gap ?? 0, padding: o.pad ?? { left: 0, right: 0, top: 0, bottom: 0 }, childAlignment: o.align ?? "UpperCenter", childControlWidth: o.cw ?? true, childControlHeight: o.ch ?? true, childForceExpandWidth: o.fw ?? true, childForceExpandHeight: false });
export const hlay = (o = {}) => ({ spacing: o.gap ?? 0, padding: o.pad ?? { left: 0, right: 0, top: 0, bottom: 0 }, childAlignment: o.align ?? "MiddleCenter", childControlWidth: o.cw ?? true, childControlHeight: o.ch ?? true, childForceExpandWidth: false, childForceExpandHeight: o.fh ?? false });
export const fit = (h = "PreferredSize", v = "PreferredSize") => ({ horizontalFit: h, verticalFit: v });
export const le = (o) => ({ ...(o.w != null ? { preferredWidth: o.w } : {}), ...(o.h != null ? { preferredHeight: o.h } : {}), ...(o.minH != null ? { minHeight: o.minH } : {}), ...(o.minW != null ? { minWidth: o.minW } : {}), ...(o.flex != null ? { flexibleWidth: o.flex } : {}), ...(o.ignore ? { ignoreLayout: true } : {}) });
export const outline = (hex, d = 3, a) => ({ effectColor: col(hex, a), effectDistance: [d, -d] });

// ===== command list =====
export class Builder {
  constructor() { this.cmds = []; }
  // path "A/B/C": creates C under A/B. comps: { Type: props | {} }. o: { pos, rot, scale, active }
  go(path, comps = {}, o = {}) {
    const i = path.lastIndexOf("/");
    const params = { action: "create", name: path.slice(i + 1) };
    if (i >= 0) params.parent = path.slice(0, i);
    const types = Object.keys(comps);
    if (types.length) params.components_to_add = types;
    const props = Object.fromEntries(Object.entries(comps).filter(([, v]) => v && Object.keys(v).length));
    if (Object.keys(props).length) params.component_properties = props;
    if (o.pos) params.position = o.pos;
    if (o.rot) params.rotation = o.rot;
    if (o.scale) params.scale = o.scale;
    this.cmds.push({ tool: "manage_gameobject", params });
    if (o.active === false) this.cmds.push({ tool: "manage_gameobject", params: { action: "modify", target: path, search_method: "by_path", set_active: false } });
    return path;
  }
  raw(tool, params) { this.cmds.push({ tool, params }); }

  async send(label = "", size = 25) {
    let ok = 0, fail = 0;
    for (let i = 0; i < this.cmds.length; i += size) {
      const chunk = this.cmds.slice(i, i + size);
      const r = await call("batch_execute", { commands: chunk, fail_fast: false });
      const results = r?.data?.results ?? [];
      results.forEach((res, k) => {
        if (res.result?.success) ok++;
        else { fail++; console.error(`  ✗ ${JSON.stringify(chunk[k].params).slice(0, 160)}\n    → ${res.result?.error || res.error} ${JSON.stringify(res.result?.data?.errors ?? "")}`); }
      });
      if (!results.length) { fail += chunk.length; console.error("batch error:", JSON.stringify(r).slice(0, 400)); }
    }
    console.log(`[${label}] ${ok} ok, ${fail} failed`);
    this.cmds = [];
    return fail === 0;
  }
}
