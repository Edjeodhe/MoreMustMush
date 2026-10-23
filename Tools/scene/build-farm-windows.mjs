// 세계수 창 3종(공방·꼬마·성장) + 목록 팝업 + 꼬마 정보 카드 프리팹을 만든다 (2026-10-20 시안 Art/Raw_Dream/concept/ui_mock_*.png).
// Unity MCP execute_code로 C#을 보내 프리팹(Assets/Resources/Prefabs/UI/<이름>.prefab)을 만들고 저장한다. 코드(GrowWindow·KidWindow·WorkshopWindow·
// FarmListPopup·KidInfoCard)는 여기서 붙인 이름으로 찾는다. 그림 = Resources/UI/Window (sheets-dream ui_window·ui_window_icons).
//   node Tools/scene/build-farm-windows.mjs            # 전부
//   node Tools/scene/build-farm-windows.mjs GrowWindow # 하나만
// 다시 돌리면 그 프리팹을 새로 만든다 (손으로 고친 것은 덮인다).
import { call } from "./mcp.mjs";

// C# 6 (CodeDom): 지역 함수·튜플·out var 없이 델리게이트로
const PRELUDE = String.raw`
TMPro.TMP_FontAsset FONT = null;
foreach (var g in UnityEditor.AssetDatabase.FindAssets("ONEMobilePOP SDF t:TMP_FontAsset")) { var fa = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)); if (fa != null && fa.name == "ONEMobilePOP SDF") FONT = fa; }
var INK = new UnityEngine.Color(0.33f, 0.22f, 0.14f);
var SOFT = new UnityEngine.Color(0.55f, 0.42f, 0.3f);
System.Func<string, UnityEngine.Sprite> SP = n => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Resources/" + n + ".png");
System.Func<string, UnityEngine.Transform, UnityEngine.RectTransform> N = (name, parent) => { var g = new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform)); if (parent != null) g.transform.SetParent(parent, false); return (UnityEngine.RectTransform)g.transform; };
// anchor = pivot = (ax, ay); (x, y) = offset from that anchor, +y up
System.Action<UnityEngine.RectTransform, float, float, float, float, float, float> P = (rt, ax, ay, x, y, w, h) => { rt.anchorMin = rt.anchorMax = rt.pivot = new UnityEngine.Vector2(ax, ay); rt.anchoredPosition = new UnityEngine.Vector2(x, y); rt.sizeDelta = new UnityEngine.Vector2(w, h); };
System.Action<UnityEngine.RectTransform, float, float, float, float> S = (rt, l, t, r, b) => { rt.anchorMin = UnityEngine.Vector2.zero; rt.anchorMax = UnityEngine.Vector2.one; rt.pivot = new UnityEngine.Vector2(0.5f, 0.5f); rt.offsetMin = new UnityEngine.Vector2(l, b); rt.offsetMax = new UnityEngine.Vector2(-r, -t); };
// mul = pixelsPerUnitMultiplier for sliced sprites (bigger = thinner borders); 0 = simple + preserve aspect
System.Func<UnityEngine.RectTransform, string, bool, float, UnityEngine.UI.Image> I = (rt, sprite, ray, mul) => {
  var im = rt.gameObject.AddComponent<UnityEngine.UI.Image>(); var s = sprite != null ? SP(sprite) : null; im.sprite = s; im.raycastTarget = ray;
  if (s != null && mul > 0 && s.border.sqrMagnitude > 0) { im.type = UnityEngine.UI.Image.Type.Sliced; im.pixelsPerUnitMultiplier = mul; } else im.preserveAspect = s != null;
  return im; };
System.Func<UnityEngine.RectTransform, string, float, UnityEngine.Color, TMPro.TextAlignmentOptions, TMPro.TextMeshProUGUI> T = (rt, text, size, col, al) => {
  var t = rt.gameObject.AddComponent<TMPro.TextMeshProUGUI>(); t.font = FONT; t.text = text; t.fontSize = size; t.color = col; t.alignment = al; t.raycastTarget = false; t.richText = true;
  t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = UnityEngine.Mathf.Max(12, size * 0.5f); t.textWrappingMode = TMPro.TextWrappingModes.Normal; t.overflowMode = TMPro.TextOverflowModes.Ellipsis; return t; };
System.Func<UnityEngine.RectTransform, UnityEngine.UI.Button> B = rt => { var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = rt.GetComponent<UnityEngine.UI.Image>(); var c = b.colors; c.disabledColor = new UnityEngine.Color(0.85f, 0.83f, 0.8f, 0.8f); b.colors = c; return b; };
// button with a label: sprite btn_leaf (green, white text) or btn_cream
System.Func<UnityEngine.RectTransform, string, string, float, UnityEngine.RectTransform> BTN = (rt, sprite, label, size) => {
  var s = SP("UI/Window/" + sprite); I(rt, "UI/Window/" + sprite, true, s != null ? s.rect.height / rt.sizeDelta.y : 1); B(rt);
  var l = N("Label", rt); S(l, 24, 8, 24, 12); var t = T(l, label, size, sprite == "btn_leaf" ? UnityEngine.Color.white : INK, TMPro.TextAlignmentOptions.Center); t.fontStyle = TMPro.FontStyles.Bold; t.textWrappingMode = TMPro.TextWrappingModes.NoWrap; return rt; };
System.Func<UnityEngine.RectTransform, string, UnityEngine.RectTransform> HEADER = (parent, text) => {
  var h = N("Header", parent); P(h, 0.5f, 1, 0, 38, 440, 80); var s = SP("UI/Window/header_plaque"); I(h, "UI/Window/header_plaque", false, s != null ? s.rect.height / 80f : 1);
  var tx = N("Text", h); S(tx, 80, 12, 80, 18); T(tx, text, 34, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold; return h; };
System.Func<UnityEngine.RectTransform, float, UnityEngine.RectTransform> BAR = (bar, h) => {
  I(bar, "UI/progress_back", false, 2.2f); var f = N("Fill", bar); f.anchorMin = UnityEngine.Vector2.zero; f.anchorMax = new UnityEngine.Vector2(0.6f, 1); f.pivot = new UnityEngine.Vector2(0, 0.5f); f.offsetMin = new UnityEngine.Vector2(4, 4); f.offsetMax = new UnityEngine.Vector2(-4, -4); I(f, "UI/progress_fill", false, 2.2f);
  var l = N("Label", bar); S(l, 8, 0, 8, 2); T(l, "", h * 0.62f, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold; return bar; };
System.Func<UnityEngine.RectTransform, float, float, UnityEngine.RectTransform> HEARTS = (hs, w, h) => {
  var hl = hs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); hl.spacing = 6; hl.childAlignment = UnityEngine.TextAnchor.MiddleCenter; hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
  for (int i = 0; i < 5; i++) { var x = N("Heart" + i, hs); x.sizeDelta = new UnityEngine.Vector2(w, h); I(x, "UI/Window/ic_heart_empty", false, 0); } return hs; };
// picture mat with an image inside (inner name), inset px
System.Func<UnityEngine.RectTransform, string, float, UnityEngine.UI.Image> PIC = (pic, inner, inset) => {
  var s = SP("UI/Window/pic_frame"); I(pic, "UI/Window/pic_frame", false, s != null ? UnityEngine.Mathf.Max(0.6f, s.rect.height / pic.sizeDelta.y) : 1);
  var i = N(inner, pic); S(i, inset, inset, inset, inset); return I(i, null, false, 0); };
// window: root (stretch) + Dim + Box(win_frame) + Title plaque (Icon + Text) + Exit
System.Func<string, float, float, float, float, string, string, UnityEngine.RectTransform> WIN = (name, w, h, x, y, icon, title) => {
  var root = N(name, null); S(root, 0, 0, 0, 0);
  var dim = N("Dim", root); S(dim, 0, 0, 0, 0); var di = I(dim, null, true, 0); di.color = new UnityEngine.Color(0.05f, 0.07f, 0.05f, 0.45f);
  var box = N("Box", root); P(box, 0.5f, 0.5f, x, y, w, h); I(box, "UI/Window/win_frame", true, 0.8f);
  var tp = N("Title", box); P(tp, 0.5f, 1, 0, 52, 660, 124); var ts = SP("UI/Window/title_plaque"); I(tp, "UI/Window/title_plaque", false, ts != null ? ts.rect.height / 124f : 1);
  var ic = N("Icon", tp); P(ic, 0, 0.5f, -40, 6, 136, 136); I(ic, "UI/Window/" + icon, false, 0);
  var tt = N("Text", tp); S(tt, 110, 16, 110, 24); T(tt, title, 58, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
  var ex = N("Exit", box); P(ex, 1, 1, 30, 30, 96, 96); I(ex, "UI/btn_close", true, 0); B(ex);
  return box; };
System.Func<UnityEngine.RectTransform, bool, UnityEngine.RectTransform> SCROLL = (sc, horizontal) => {
  var si = sc.gameObject.AddComponent<UnityEngine.UI.Image>(); si.color = new UnityEngine.Color(1, 1, 1, 0.003f); sc.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
  var sr = sc.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); sr.horizontal = horizontal; sr.vertical = !horizontal; sr.scrollSensitivity = 40; sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
  var c = N("Content", sc);
  if (horizontal) { c.anchorMin = new UnityEngine.Vector2(0, 0); c.anchorMax = new UnityEngine.Vector2(0, 1); c.pivot = new UnityEngine.Vector2(0, 0.5f); c.offsetMin = c.offsetMax = UnityEngine.Vector2.zero;
    var hl = c.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); hl.padding = new UnityEngine.RectOffset(8, 8, 6, 6); hl.spacing = 22; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
    c.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; }
  else { c.anchorMin = new UnityEngine.Vector2(0, 1); c.anchorMax = new UnityEngine.Vector2(1, 1); c.pivot = new UnityEngine.Vector2(0.5f, 1); c.offsetMin = c.offsetMax = UnityEngine.Vector2.zero;
    var vl = c.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); vl.padding = new UnityEngine.RectOffset(6, 6, 6, 10); vl.spacing = 14; vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false; vl.childAlignment = UnityEngine.TextAnchor.UpperCenter;
    c.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; }
  sr.content = c; sr.viewport = sc; return c; };
System.Func<UnityEngine.RectTransform, float, float, UnityEngine.UI.LayoutElement> LE = (rt, w, h) => { var le = rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); if (w > 0) le.preferredWidth = w; if (h > 0) le.preferredHeight = h; return le; };
System.Func<UnityEngine.RectTransform, string, string> SAVE = (root, name) => {
  System.IO.Directory.CreateDirectory("Assets/Resources/Prefabs/UI"); bool ok; UnityEditor.PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Resources/Prefabs/UI/" + name + ".prefab", out ok);
  UnityEngine.Object.DestroyImmediate(root.gameObject); return name + " saved=" + ok + " font=" + (FONT != null); };
`;

const BUILD = {
  // ===== 세계수 성장 =====
  GrowWindow: String.raw`
var box = WIN("GrowWindow", 1440, 860, -130, -34, "ic_grow", "세계수 성장");
// 왼쪽: 나무 카드 · 현재 효과 · 진화
var card = N("TreeCard", box); P(card, 0, 1, 40, -96, 470, 420); I(card, "UI/Window/card_panel", false, 1);
var pic = N("Pic", card); P(pic, 0.5f, 1, 0, -16, 438, 340); PIC(pic, "Tree", 18);
var lv = N("Lv", card); P(lv, 0.5f, 0, 0, -30, 390, 156); I(lv, "UI/ribbon", false, 0);
var lvt = N("Text", lv); S(lvt, 74, 50, 74, 64); T(lvt, "3단계", 34, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var ef = N("Effects", box); P(ef, 0, 1, 40, -528, 470, 150); I(ef, "UI/Window/card_panel", false, 1.4f);
var eft = N("Title", ef); P(eft, 0.5f, 1, 0, -10, 430, 34); T(eft, "현재 효과", 28, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var efx = N("Text", ef); S(efx, 26, 46, 26, 12); T(efx, "", 21, INK, TMPro.TextAlignmentOptions.TopLeft);
var ev = N("Evolve", box); P(ev, 0, 1, 40, -690, 470, 142); I(ev, "UI/Window/row_panel", false, 1.6f);
var evi = N("Icon", ev); P(evi, 0, 0.5f, 12, 0, 110, 110); I(evi, "UI/Window/mock_grow_evolve", false, 0);
var evtt = N("Title", ev); P(evtt, 0, 1, 128, -12, 320, 34); T(evtt, "세계수 진화", 26, INK, TMPro.TextAlignmentOptions.MidlineLeft).fontStyle = TMPro.FontStyles.Bold;
var evb = N("Bar", ev); P(evb, 0, 1, 128, -50, 318, 24); BAR(evb, 24);
var evx = N("Text", ev); P(evx, 0, 1, 128, -78, 190, 56); T(evx, "", 15, SOFT, TMPro.TextAlignmentOptions.TopLeft);
var evbt = N("Button", ev); P(evbt, 1, 0, -16, 12, 134, 56); BTN(evbt, "btn_cream", "진화", 26);
// 오른쪽: 나이테 · 공물 · 가호
string[] rows = { "Row_Ring", "Row_Tribute", "Row_Bless" }; string[] icons = { "mock_grow_ring", "mock_grow_tribute", "mock_grow_bless" }; string[] titles = { "나이테", "공물", "세계수의 가호" };
for (int i = 0; i < 3; i++) {
  var r = N(rows[i], box); P(r, 1, 1, -40, -96 - i * 236, 856, 220); I(r, "UI/Window/row_panel", false, 1.1f);
  var ic = N("Icon", r); P(ic, 0, 0.5f, 22, 0, 178, 178); var ii = N("Img", ic); S(ii, 0, 0, 0, 0); I(ii, "UI/Window/" + icons[i], false, 0);
  var tt = N("Title", r); P(tt, 0, 1, 220, -22, 380, 50); T(tt, titles[i], 40, INK, TMPro.TextAlignmentOptions.MidlineLeft).fontStyle = TMPro.FontStyles.Bold;
  var sub = N("Sub", r); P(sub, 0, 1, 220, -76, 380, 34); T(sub, "Lv. 1 / 10", 28, INK, TMPro.TextAlignmentOptions.MidlineLeft);
  var bar = N("Bar", r); P(bar, 0, 1, 220, -116, 360, 30); BAR(bar, 30);
  var tx = N("Text", r); P(tx, 0, 1, 220, i == 0 ? -152 : -80, 380, i == 0 ? 50 : 116); T(tx, "", 24, INK, TMPro.TextAlignmentOptions.TopLeft);
  var bt = N("Button", r); P(bt, 1, 0.5f, -30, 0, 230, i == 0 ? 118 : 84); BTN(bt, i == 0 ? "btn_leaf" : "btn_cream", i == 0 ? "성장" : i == 1 ? "바치기" : "확인", 34);
}
return SAVE((UnityEngine.RectTransform)box.parent, "GrowWindow");`,

  // ===== 꼬마의 하루 =====
  KidWindow: String.raw`
var box = WIN("KidWindow", 1440, 860, -130, -34, "ic_join", "꼬마의 하루");
// 왼쪽: 고정 카드 두 장 (시안 그림 그대로, 이름 글씨 없음). 새싹 = 호감도, 이슬 = 등급(별)
string[] cn = { "Card_Aff", "Card_Grade" }; string[] art = { "mock_kid_sprout", "mock_kid_dew" };
for (int k = 0; k < 2; k++) {
  var kc = N(cn[k], box); P(kc, 0, 1, 34 + k * 496, -88, 470, 740); I(kc, "UI/Window/card_panel", true, 1);
  var pic = N("Pic", kc); P(pic, 0.5f, 1, 0, -16, 436, 522); var pi = I(pic, "UI/Window/" + art[k], false, 0); pi.preserveAspect = true;
  var info = N("Info", kc); P(info, 0.5f, 1, 0, -548, 430, 46); T(info, k == 0 ? "꼬마 3명  |  호감도 12" : "별 4/9", 28, INK, TMPro.TextAlignmentOptions.Center);
  var hb = N("HeartsBack", kc); P(hb, 0.5f, 1, 0, -598, 380, 62); var hbi = I(hb, "UI/Window/name_pill", false, 2); hbi.color = new UnityEngine.Color(0.94f, 0.9f, 0.84f, 1);
  if (k == 0) { var hs = N("Hearts", kc); P(hs, 0.5f, 1, 0, -600, 340, 58); HEARTS(hs, 54, 46); }
  else { var st = N("Stars", kc); P(st, 0.5f, 1, 0, -600, 340, 58); T(st, "★★★", 44, new UnityEngine.Color(0.91f, 0.66f, 0f), TMPro.TextAlignmentOptions.Center); }
  var bt = N("Button", kc); P(bt, 0.5f, 0, 0, 18, 410, 66); BTN(bt, k == 0 ? "btn_leaf" : "btn_cream", k == 0 ? "호감도" : "등급", 38);
}
// 오른쪽: 꼬마의 부탁 · 꼬마 진화
var rq = N("ReqBox", box); P(rq, 1, 1, -34, -88, 380, 360); I(rq, "UI/Window/card_panel", false, 1);
var rqt = N("Title", rq); P(rqt, 0.5f, 1, 0, -16, 340, 52); T(rqt, "꼬마의 부탁", 38, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var rqp = N("Pic", rq); P(rqp, 0, 0, 10, 96, 176, 210); I(rqp, "UI/Window/mock_kid_request", false, 0);
var bub = N("Bubble", rq); P(bub, 1, 0, -12, 140, 196, 140); I(bub, "UI/tooltip", false, 1.4f);
var bt2 = N("Text", bub); S(bt2, 16, 14, 16, 18); T(bt2, "매일 세 번, 함께하는 작은 일을 부탁할게요!", 22, INK, TMPro.TextAlignmentOptions.Center);
var rqb = N("Button", rq); P(rqb, 0.5f, 0, 0, 16, 340, 76); BTN(rqb, "btn_cream", "부탁 보기  >", 32);
var eb = N("EvoBox", box); P(eb, 1, 1, -34, -468, 380, 360); I(eb, "UI/Window/card_panel", false, 1);
var ebt = N("Title", eb); P(ebt, 0.5f, 1, 0, -16, 340, 52); T(ebt, "꼬마 진화", 38, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var ebi = N("Icon", eb); P(ebi, 0, 0, 12, 100, 230, 170); I(ebi, "UI/Window/mock_kid_evolve", false, 0);
var ebx = N("Text", eb); P(ebx, 1, 0, -10, 120, 124, 140); T(ebx, "한 단계 더 자라요!", 26, INK, TMPro.TextAlignmentOptions.Center);
var ebb = N("Button", eb); P(ebb, 0.5f, 0, 0, 16, 340, 76); BTN(ebb, "btn_cream", "진화  >", 32);
return SAVE((UnityEngine.RectTransform)box.parent, "KidWindow");`,

  // ===== 작은 제작소 =====
  WorkshopWindow: String.raw`
var box = WIN("WorkshopWindow", 1440, 860, -130, -34, "ic_mallet", "작은 제작소");
var info = N("Info", box); P(info, 0, 0, 40, 10, 860, 34); T(info, "", 22, SOFT, TMPro.TextAlignmentOptions.MidlineLeft);
var lp = N("Recipes", box); P(lp, 0, 1, 34, -96, 900, 716); I(lp, "UI/Window/card_panel", false, 0.9f);
HEADER(lp, "제작 목록");
var ls = N("Scroll", lp); S(ls, 20, 56, 20, 16); var lc = SCROLL(ls, true);
var rc = N("Recipe", lc); I(rc, "UI/Window/card_panel", false, 1.1f); LE(rc, 410, 0);
var rn = N("Name", rc); P(rn, 0.5f, 1, 0, -14, 370, 52); T(rn, "새싹의 집", 36, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var rp = N("Pic", rc); P(rp, 0.5f, 1, 0, -70, 360, 238); PIC(rp, "Thumb", 22); var rw = N("Wash", rp); S(rw, 10, 10, 10, 10); rw.SetAsFirstSibling(); I(rw, "UI/Window/mock_ws_wash", false, 0).preserveAspect = false;
var rd = N("Desc", rc); P(rd, 0.5f, 1, 0, -316, 370, 50); var rdi = I(rd, "UI/Window/name_pill", false, 2.4f); rdi.color = new UnityEngine.Color(0.95f, 0.92f, 0.86f, 1);
var rdt = N("Text", rd); S(rdt, 20, 4, 20, 6); T(rdt, "", 20, SOFT, TMPro.TextAlignmentOptions.Center);
var mt = N("MatTitle", rc); P(mt, 0.5f, 1, 0, -374, 370, 34); T(mt, "필요 재료", 24, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var mats = N("Mats", rc); P(mats, 0.5f, 1, 0, -412, 372, 120);
for (int i = 0; i < 3; i++) { var m = N("Mat" + i, mats); P(m, 0, 1, i * 128, 0, 116, 120); I(m, "UI/Window/mat_slot", false, 1.6f);
  var mi = N("Icon", m); P(mi, 0.5f, 1, 0, -10, 64, 64); I(mi, null, false, 0);
  var mc = N("Count", m); P(mc, 0.5f, 0, 0, 8, 110, 34); var mct = T(mc, "0/0", 22, INK, TMPro.TextAlignmentOptions.Center); mct.fontStyle = TMPro.FontStyles.Bold; mct.textWrappingMode = TMPro.TextWrappingModes.NoWrap; }
var cb = N("Button_Craft", rc); P(cb, 0.5f, 0, 0, 18, 368, 96); BTN(cb, "btn_leaf", "제작", 44);
var rp2 = N("Slots", box); P(rp2, 1, 1, -34, -96, 470, 716); I(rp2, "UI/Window/card_panel", false, 0.9f);
HEADER(rp2, "제작 슬롯");
var ss = N("Scroll", rp2); S(ss, 16, 56, 16, 14); var sc = SCROLL(ss, false);
var sl = N("Slot", sc); I(sl, "UI/Window/row_panel", false, 1.5f); LE(sl, 0, 196);
var sp = N("Pic", sl); P(sp, 0, 0.5f, 18, -4, 150, 150); PIC(sp, "Thumb", 14); var sw = N("Wash", sp); S(sw, 8, 8, 8, 8); sw.SetAsFirstSibling(); I(sw, "UI/Window/mock_ws_wash", false, 0).preserveAspect = false;
var lk = N("Lock", sp); S(lk, 0, 0, 0, 0); I(lk, "UI/Window/mock_ws_lock", false, 0);
var num = N("Num", sl); P(num, 0, 1, -4, 4, 62, 62); I(num, "UI/Window/num_badge", false, 0);
var numt = N("Text", num); S(numt, 6, 6, 6, 8); T(numt, "1", 30, UnityEngine.Color.white, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var sn = N("Name", sl); P(sn, 0, 1, 184, -18, 250, 44); T(sn, "이슬 풍경", 30, INK, TMPro.TextAlignmentOptions.MidlineLeft).fontStyle = TMPro.FontStyles.Bold;
var sb = N("Bar", sl); P(sb, 0, 1, 184, -70, 170, 26); BAR(sb, 26);
var st = N("Time", sl); P(st, 0, 1, 360, -66, 86, 34); T(st, "3:20", 24, INK, TMPro.TextAlignmentOptions.MidlineLeft);
var ss2 = N("Status", sl); P(ss2, 0, 1, 184, -98, 254, 28); T(ss2, "제작 중…", 20, SOFT, TMPro.TextAlignmentOptions.MidlineLeft);
var dn = N("Done", sl); P(dn, 0, 0, 22, 8, 142, 48); var dns = SP("UI/Window/done_badge"); I(dn, "UI/Window/done_badge", false, dns != null ? dns.rect.height / 48f : 1);
var dnt = N("Text", dn); S(dnt, 44, 6, 10, 9); T(dnt, "완료!", 24, UnityEngine.Color.white, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var br = N("Button_Rush", sl); P(br, 1, 0, -14, 14, 150, 58); BTN(br, "btn_cream", "가속", 26);
var bc = N("Button_Claim", sl); P(bc, 0, 0, 184, 12, 254, 58); BTN(bc, "btn_leaf", "받기", 34);
var bu = N("Button_Unlock", sl); P(bu, 1, 0, -14, 14, 180, 58); BTN(bu, "btn_cream", "해금", 26);
return SAVE((UnityEngine.RectTransform)box.parent, "WorkshopWindow");`,

  // ===== 목록 팝업 (호감도·합류·부탁·진화·가호) =====
  FarmListPopup: String.raw`
var box = WIN("FarmListPopup", 1120, 820, -110, -40, "ic_affection", "꼬마 호감도");
var sc = N("Scroll", box); S(sc, 40, 96, 40, 34); var c = SCROLL(sc, false);
var sec = N("Section", c); LE(sec, 0, 74); var sh = N("Plaque", sec); P(sh, 0, 0.5f, 6, 0, 420, 70); var shs = SP("UI/Window/header_plaque"); I(sh, "UI/Window/header_plaque", false, shs != null ? shs.rect.height / 70f : 1);
var sht = N("Text", sh); S(sht, 70, 10, 70, 16); T(sht, "오늘의 부탁", 30, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var row = N("Row", c); I(row, "UI/Window/row_panel", false, 1.4f); LE(row, 0, 178);
var pic = N("Pic", row); P(pic, 0, 0.5f, 18, 0, 146, 146); PIC(pic, "Img", 12);
var ar = N("Arrow", row); P(ar, 0, 0.5f, 168, 0, 36, 60); T(ar, ">", 44, SOFT, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var p2 = N("Pic2", row); P(p2, 0, 0.5f, 206, 0, 146, 146); PIC(p2, "Img", 12);
var body = N("Body", row); P(body, 0, 1, 180, -16, 560, 150);
var nm = N("Name", body); P(nm, 0, 1, 0, 0, 560, 42); T(nm, "새싹", 32, INK, TMPro.TextAlignmentOptions.MidlineLeft).fontStyle = TMPro.FontStyles.Bold;
var l1 = N("Line1", body); P(l1, 0, 1, 0, -44, 560, 34); T(l1, "", 23, INK, TMPro.TextAlignmentOptions.MidlineLeft);
var l2 = N("Line2", body); P(l2, 0, 1, 0, -78, 560, 30); T(l2, "", 20, SOFT, TMPro.TextAlignmentOptions.MidlineLeft);
var hs = N("Hearts", body); P(hs, 0, 1, 0, -110, 250, 40); HEARTS(hs, 42, 36); hs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment = UnityEngine.TextAnchor.MiddleLeft;
var bar = N("Bar", body); P(bar, 0, 1, 262, -114, 290, 30); BAR(bar, 30);
var ba = N("Button_A", row); P(ba, 1, 0.5f, -22, 38, 230, 66); BTN(ba, "btn_leaf", "선물", 30);
var bb = N("Button_B", row); P(bb, 1, 0.5f, -22, -38, 230, 62); BTN(bb, "btn_cream", "Lv업", 26);
var em = N("Empty", box); S(em, 80, 200, 80, 200); T(em, "", 34, SOFT, TMPro.TextAlignmentOptions.Center);
return SAVE((UnityEngine.RectTransform)box.parent, "FarmListPopup");`,

  // ===== 꼬마 정보 카드 (꼬마를 고르면 화면 오른쪽) =====
  KidInfoCard: String.raw`
var root = N("KidInfoCard", null); S(root, 0, 0, 0, 0);
var card = N("Card", root); P(card, 1, 0.5f, -330, -20, 470, 560); I(card, "UI/Window/card_panel", true, 1);
var np = N("NamePill", card); P(np, 0.5f, 1, 0, 34, 330, 84); var ns = SP("UI/Window/name_pill"); I(np, "UI/Window/name_pill", false, ns != null ? ns.rect.height / 84f : 1);
var nt = N("Text", np); S(nt, 34, 12, 34, 16); T(nt, "새싹", 42, INK, TMPro.TextAlignmentOptions.Center).fontStyle = TMPro.FontStyles.Bold;
var ex = N("Exit", card); P(ex, 1, 1, 22, 22, 74, 74); I(ex, "UI/btn_close", true, 0); B(ex);
var stars = N("Stars", card); P(stars, 0.5f, 1, 0, -60, 420, 54); T(stars, "★★★", 46, new UnityEngine.Color(0.91f, 0.66f, 0f), TMPro.TextAlignmentOptions.Center);
var lv = N("Lv", card); P(lv, 0.5f, 1, 0, -116, 420, 40); T(lv, "Lv. 3 / 10   호감도 2", 28, INK, TMPro.TextAlignmentOptions.Center);
var hb = N("HeartsBack", card); P(hb, 0.5f, 1, 0, -164, 350, 66); var hbi = I(hb, "UI/Window/name_pill", false, 2); hbi.color = new UnityEngine.Color(0.94f, 0.9f, 0.84f, 1);
var hs = N("Hearts", card); P(hs, 0.5f, 1, 0, -168, 330, 58); HEARTS(hs, 54, 46);
var bar = N("Bar", card); P(bar, 0.5f, 1, 0, -246, 400, 34); BAR(bar, 34);
var zn = N("Zone", card); P(zn, 0.5f, 1, 0, -296, 420, 40); T(zn, "", 26, INK, TMPro.TextAlignmentOptions.Center);
var tr = N("Trait", card); P(tr, 0.5f, 1, 0, -340, 420, 92); T(tr, "", 21, SOFT, TMPro.TextAlignmentOptions.Top);
var bz = N("Button_Zone", card); P(bz, 0, 0, 24, 24, 204, 76); BTN(bz, "btn_cream", "구역", 28);
var bg = N("Button_Gift", card); P(bg, 1, 0, -24, 24, 204, 76); BTN(bg, "btn_leaf", "호감도", 32);
return SAVE(root, "KidInfoCard");`,
};

const only = process.argv.slice(2);
for (const [name, body] of Object.entries(BUILD)) {
  if (only.length && !only.includes(name)) continue;
  // 본문은 람다 안에: 도구 람다 안의 지역 이름(box·pic …)과 겹쳐도 형제 범위라 괜찮다 (C# 6)
  const r = await call("execute_code", { action: "execute", code: PRELUDE + "System.Func<string> __body = () => {" + body + "\n};\nreturn __body();" });
  console.log(name, r?.data?.result ?? JSON.stringify(r).slice(0, 600));
}
