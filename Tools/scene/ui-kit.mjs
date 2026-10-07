// Shared uGUI building blocks for the build-ui-*.mjs scripts (prototype CSS → uGUI).
import { ART, FONT, TMAT, col, R, RC, STRETCH, img, txt, vlay, hlay, le, outline, sliceDensity } from "./mcp.mjs";

export const WOOD = "#7a4f2e", WOOD_D = "#3b2414", CREAM = "#f6ead2", INK = "#3b2414";
export const BTN = { wood: ART("UI/btn_wood"), go: ART("UI/btn_go"), danger: ART("UI/btn_danger") };
export const PANEL = ART("UI/panel_wood"), PAPER = ART("UI/panel_paper");
export const ic = (name) => `<sprite name="${name}">`;

export function makeKit(b) {
  const k = {};
  // plain text: o.rect (R/RC/STRETCH props) or layout (o.le)
  k.text = (path, text, size, color = INK, o = {}) => b.go(path, {
    ...(o.rect ? { RectTransform: o.rect } : {}),
    TextMeshProUGUI: txt(text, size, color, o),
    ...(o.le ? { LayoutElement: le(o.le) } : {}),
  });
  k.body = (path, text, size, color = "#6a5040", o = {}) => k.text(path, text, size, color, { font: FONT.noto, wrap: true, ...o });
  // image
  k.image = (path, sprite, o = {}) => b.go(path, {
    ...(o.rect ? { RectTransform: o.rect } : { RectTransform: {} }),
    Image: img(sprite, o),
    ...(o.outline ? { Outline: outline(o.outline, o.ow ?? 3, o.oa) } : {}),
    ...(o.le ? { LayoutElement: le(o.le) } : {}),
    ...(o.extra ?? {}),
  });
  // button with label child "Text"; style wood | go | danger | ghost
  k.button = (path, label, act, o = {}) => {
    const style = o.style ?? "wood";
    const sprite = style === "go" ? BTN.go : style === "danger" ? BTN.danger : BTN.wood;
    const color = style === "ghost" ? col("#ffffff", 0.75) : col("#ffffff");
    b.go(path, {
      ...(o.rect ? { RectTransform: o.rect } : { RectTransform: {} }),
      Image: { sprite, type: "Sliced", color, raycastTarget: true, pixelsPerUnitMultiplier: sliceDensity(sprite) },
      Button: {},
      UIAction: { act, ...(o.arg ? { arg: o.arg } : {}) },
      UIButtonFx: {},
      ...(o.dim ? { CanvasGroup: { alpha: 0.6 } } : {}),
      ...(o.le ? { LayoutElement: le(o.le) } : {}),
    });
    k.text(`${path}/Text`, label, o.size ?? 22, style === "danger" ? "#ffffff" : INK, { rect: STRETCH(o.padL ?? 14, 0, o.padR ?? 14, 4) });
    return path;
  };
  // close "×" at a panel's top-right
  k.close = (panel) => k.button(`${panel}/Close`, "×", "close", { rect: R(-14, 14, 52, 46, { anchor: [1, 1], pivot: [1, 1] }), size: 30, le: { ignore: true } });
  // paper row with Label (left) and Value (right)
  k.row = (path, label, value, o = {}) => {
    k.image(path, PAPER, { type: "Sliced", color: col("#fffaf0"), outline: "#e2cfa8", ow: 2, le: { h: o.h ?? 62 }, extra: { HorizontalLayoutGroup: hlay({ gap: 12, pad: { left: 16, right: 16, top: 6, bottom: 6 }, cw: true, ch: true, fh: true }) } });
    k.text(`${path}/Label`, label, o.size ?? 30, INK, { align: "MidlineLeft", le: { flex: 1 } });
    k.text(`${path}/Value`, value, o.vsize ?? 40, o.vcolor ?? INK, { align: "MidlineRight", le: {} });
    return path;
  };
  // big modal panel: wood frame + cream, vertical layout, size fitted to content
  k.panel = (path, width, o = {}) => {
    b.go(path, {
      RectTransform: { anchorMin: [0.5, 0.5], anchorMax: [0.5, 0.5], pivot: [0.5, 0.5], anchoredPosition: [0, 0], sizeDelta: [width, o.h ?? 400] },
      Image: { sprite: PANEL, type: "Sliced", color: col("#ffffff"), raycastTarget: true, pixelsPerUnitMultiplier: 4 },
      Outline: outline(WOOD, 8),
      VerticalLayoutGroup: vlay({ gap: o.gap ?? 10, pad: o.pad ?? { left: 36, right: 36, top: 30, bottom: 30 }, align: "UpperCenter", fw: true }),
      ContentSizeFitter: { horizontalFit: "Unconstrained", verticalFit: o.h ? "Unconstrained" : "PreferredSize" },
      ...(o.comps ?? {}),
    });
    return path;
  };
  k.h2 = (path, text, o = {}) => k.text(path, text, o.size ?? 42, INK, { align: o.align ?? "Center", le: { h: o.h ?? 54 } });
  return k;
}
