// 세계수 도면 편집기 (Assets/Resources/Rig/tree_layout.json). 좌표는 게임 나무 그림 Farm/Tree/tree_<pic>.png의 비율 (u 오른쪽, v 아래).
// 크기 규칙은 게임과 같다: 장식 긴 변 = (걸이형 95 · 설치형 115) × 화각 × 그림 배율(WT.grow) × s (무대 px), 나무버섯 = (34 + 20·pic/7) × 화각, 나무 너비 = WT.width.
const $ = (id) => document.getElementById(id);
const cv = $("cv"), ctx = cv.getContext("2d");
const TREE = "/Assets/Resources/Art/Dream/Farm/Tree/tree_";
const PROPS = "/Assets/Resources/Art/Dream/Farm/Props/";
const EDGE = { walk: "#50e678", climb: "#ffc828", hop: "#ff5ac8" };
const ZCOL = { root: "#c08a4a", trunk: "#3cc85a", branch: "#28aaff", crown: "#c878ff" };

let D, data, pic = 7, tool = "move", sel = null, edgeFrom = null, dirty = false;
const imgs = {}, undo = [];
const view = { cx: 0.5, cy: 0.55, k: 1 };   // k = 화면 px / 그림 높이 1

function img(url) {
  if (!imgs[url]) { const i = new Image(); i.onload = draw; i.src = url; imgs[url] = i; }
  return imgs[url];
}
const P = () => data.pics.find((p) => p.pic === pic);
const treeImg = () => img(TREE + pic + ".png");
const decosFor = (pc) => D.decos.filter((d) => D.join.slice(0, pc + 1).includes(d.of));
const decoPx = (d) => (d.hang ? 95 : 115) * D.zoom[pic] * (D.grow?.[pic] ?? 1);
const fruitPx = () => (34 + 20 * pic / (D.width.length - 1)) * D.zoom[pic];
// 서 있는 정령 (무대 px): 1배 화각 110 × 155, 화각^0.75로 커진다 (tree_defs.spirit_px)
const spiritPx = () => [110, 155].map((s) => s * Math.pow(D.zoom[pic], 0.75));
function spotClash(p) {   // 서로 겹치는 머무는 곳 id (|dx| < 0.9 폭 · |dy| < 0.8 키)
  const [w, h] = size(), [sw, sh] = spiritPx(), W = D.width[pic], H = W * h / w, out = new Set();
  const s = (p.nodes || []).filter((n) => n.spot);
  for (let i = 0; i < s.length; i++) for (let j = i + 1; j < s.length; j++)
    if (Math.abs(s[i].u - s[j].u) * W < 0.9 * sw && Math.abs(s[i].v - s[j].v) * H < 0.8 * sh) { out.add(s[i].id); out.add(s[j].id); }
  return out;
}

function zoneSlots(pc) {
  const stage = pc + 1, total = D.mushSlots[pc] + D.beautySlots;
  const open = D.zones.filter((z) => stage >= z.open), wsum = open.reduce((a, z) => a + z.weight, 0);
  const o = {}; open.forEach((z) => (o[z.id] = Math.floor(total * z.weight / wsum)));
  let left = total - Object.values(o).reduce((a, b) => a + b, 0);
  [...open].sort((a, b) => (total * b.weight % wsum) - (total * a.weight % wsum) || D.zones.indexOf(a) - D.zones.indexOf(b))
    .forEach((z) => { if (left > 0) { o[z.id]++; left--; } });
  D.zones.forEach((z) => (o[z.id] ??= 0));
  return o;
}

// ---- 좌표: 그림 비율 ↔ 화면
function size() { const im = treeImg(); return im.naturalWidth ? [im.naturalWidth, im.naturalHeight] : [1, 1]; }
function toS(u, v) { const [w, h] = size(); const a = w / h; return [cv.width / 2 + (u - view.cx) * a * view.k, cv.height / 2 + (v - view.cy) * view.k]; }
function toUV(x, y) { const [w, h] = size(); const a = w / h; return [(x - cv.width / 2) / (a * view.k) + view.cx, (y - cv.height / 2) / view.k + view.cy]; }
const pxPerStage = () => { const [w, h] = size(); return (w / h) * view.k / D.width[pic]; };   // 화면 px / 무대 px
const r3 = (x) => Math.round(x * 1000) / 1000;

function fit() {
  const [w, h] = size(), a = w / h;
  view.k = Math.min(cv.height * 0.8, cv.width * 0.6 / a); view.cx = 0.5; view.cy = 0.55;
  draw();
}

// ---- 데이터 바꾸기 (되돌리기용 스냅숏)
function touch() { undo.push(JSON.stringify(data)); if (undo.length > 100) undo.shift(); dirty = true; $("dirty").textContent = "● 저장 안 됨"; }
function doUndo() { if (!undo.length) return; data = JSON.parse(undo.pop()); sel = null; edgeFrom = null; dirty = true; refresh(); }

function dumps(obj) {   // tree_defs.dumps와 같은 모양: indent 1, 짧은 배열은 한 줄
  return JSON.stringify(obj, null, 1).replace(/\[\s*([^\[\]{}]*?)\s*\]/g, (m, g) => "[" + g.replace(/\s*\n\s*/g, " ") + "]") + "\n";
}
async function save() {
  const r = await fetch("/api/save", { method: "POST", body: dumps(data) }).then((r) => r.json());
  if (r.ok) { dirty = false; $("dirty").textContent = "저장됨"; setTimeout(() => !dirty && ($("dirty").textContent = ""), 1500); }
  else alert("저장 실패: " + JSON.stringify(r));
}

// ---- 그리기
function draw() {
  if (!data) return;
  const p = P(), im = treeImg();
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.fillStyle = "#2a3242"; ctx.fillRect(0, 0, cv.width, cv.height);
  const [gx0, gy] = toS(-5, p.foot ?? 1), [gx1] = toS(5, p.foot ?? 1);   // 땅 = 그림의 foot 선 (밑동 WT.y)
  ctx.fillStyle = "#33402e"; ctx.fillRect(0, gy, cv.width, cv.height - gy);
  if (im.naturalWidth) {
    const [x0, y0] = toS(0, 0), [x1, y1] = toS(1, 1);
    ctx.globalAlpha = Number($("alpha").value); ctx.drawImage(im, x0, y0, x1 - x0, y1 - y0); ctx.globalAlpha = 1;
    ctx.strokeStyle = "rgba(255,255,255,.18)"; ctx.setLineDash([6, 6]); ctx.strokeRect(x0, y0, x1 - x0, y1 - y0); ctx.setLineDash([]);
  }
  const k = pxPerStage();
  ctx.font = "12px Malgun Gothic";
  if ($("vMush").checked) {
    const r = fruitPx() * k / 2;
    for (const [z, pts] of Object.entries(p.mush || {})) pts.forEach(([u, v], i) => {
      const [x, y] = toS(u, v), on = sel && sel.type === "mush" && sel.zone === z && sel.i === i;
      ctx.lineWidth = on ? 4 : 2; ctx.strokeStyle = on ? "#fff" : ZCOL[z];
      ctx.beginPath(); ctx.ellipse(x, y, r, r * 0.7, 0, 0, 7); ctx.stroke();
      ctx.fillStyle = ZCOL[z]; ctx.fillText(String(i + 1), x - 4, y + 4);
    });
  }
  if ($("vDeco").checked) {
    for (const [id, at] of Object.entries(p.deco || {})) {
      const d = D.decos.find((q) => q.id === id); if (!d) continue;
      const b = decoBox(d, at), on = sel && sel.type === "deco" && sel.id === id;
      const a = img(PROPS + d.art + ".png");
      if ($("vArt").checked && a.naturalWidth) ctx.drawImage(a, b.x0, b.y0, b.x1 - b.x0, b.y1 - b.y0);
      ctx.strokeStyle = on ? "#fff" : d.hang ? "#ff5050" : "#ffa020"; ctx.lineWidth = on ? 3 : 1.5; ctx.setLineDash([6, 4]);
      ctx.strokeRect(b.x0, b.y0, b.x1 - b.x0, b.y1 - b.y0); ctx.setLineDash([]);
      const [x, y] = toS(at[0], at[1]);
      ctx.fillStyle = d.hang ? "#ff5050" : "#ffa020"; ctx.beginPath(); ctx.arc(x, y, 6, 0, 7); ctx.fill();
      ctx.fillStyle = "#fff"; ctx.fillText(d.n, b.x0, d.hang ? b.y1 + 13 : b.y0 - 4);
    }
  }
  if ($("vNav").checked) {
    const pos = Object.fromEntries((p.nodes || []).map((n) => [n.id, toS(n.u, n.v)]));
    // 머무는 곳마다 서 있는 정령 크기 (발이 점). 다른 머무는 곳과 겹치면 빨강 — fit_spots.py와 같은 기준
    const [sw, sh] = spiritPx().map((s) => s * k), clash = spotClash(p);
    (p.nodes || []).forEach((n) => {
      if (!n.spot) return;
      const [x, y] = pos[n.id];
      ctx.strokeStyle = clash.has(n.id) ? "#ff4040" : "rgba(90,200,255,.9)"; ctx.lineWidth = 2; ctx.setLineDash([5, 4]);
      ctx.strokeRect(x - sw / 2, y - sh, sw, sh); ctx.setLineDash([]);
    });
    (p.edges || []).forEach((e, i) => {
      const a = pos[e[0]], c = pos[e[1]]; if (!a || !c) return;
      const on = sel && sel.type === "edge" && sel.i === i;
      if (e[3] === "plank" || e[3] === "bridge") { ctx.strokeStyle = "#8c5528"; ctx.lineWidth = 12; line(a, c); }
      if (e[3] === "ladder") { ctx.strokeStyle = "#a06432"; ctx.lineWidth = 3; ladder(a, c, 16); }
      if (e[3] === "bridge") { ctx.setLineDash([3, 5]); }
      ctx.strokeStyle = on ? "#fff" : EDGE[e[2]] || "#fff"; ctx.lineWidth = on ? 6 : 4; line(a, c); ctx.setLineDash([]);
    });
    (p.nodes || []).forEach((n, i) => {
      const [x, y] = pos[n.id], on = sel && sel.type === "node" && sel.i === i;
      ctx.fillStyle = n.spot ? "#fff" : n.ground ? "#3cc8ff" : "#bbb";
      ctx.strokeStyle = on || edgeFrom === i ? "#ffcf5a" : "#000"; ctx.lineWidth = on || edgeFrom === i ? 4 : 1.5;
      ctx.beginPath(); ctx.arc(x, y, n.spot ? 8 : 6, 0, 7); ctx.fill(); ctx.stroke();
      ctx.fillStyle = "#fff"; ctx.fillText(n.id, x + 9, y - 8);
    });
  }
  const nSpot = (p.nodes || []).filter((n) => n.spot).length, [spw, sph] = spiritPx();
  $("status").textContent = `${pic + 1}단계 · tree_${pic}.png · 나무 너비 ${D.width[pic]}px · 화각 ×${D.zoom[pic]}`
    + ` · 정령 ${spw.toFixed(0)}×${sph.toFixed(0)}px`
    + (pic + 1 < D.climbFrom ? ` · ${D.climbFrom}단계 전: 나무 오르기 없음(길 안 씀)` : ` · 머무는 곳 ${nSpot} / 정령 ${pic + 1}`) + (spotClash(p).size ? " · 겹치는 곳 있음(빨강)" : "") + (edgeFrom != null ? " · 선 잇기: 두 번째 마디를 누르세요" : "");
}
function line(a, b) { ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.lineTo(b[0], b[1]); ctx.stroke(); }
function ladder(a, b, w) {
  const dx = b[0] - a[0], dy = b[1] - a[1], L = Math.hypot(dx, dy) || 1, nx = -dy / L * w / 2, ny = dx / L * w / 2;
  line([a[0] + nx, a[1] + ny], [b[0] + nx, b[1] + ny]); line([a[0] - nx, a[1] - ny], [b[0] - nx, b[1] - ny]);
  for (let t = w * 0.6; t < L; t += w * 0.6) { const c = [a[0] + dx * t / L, a[1] + dy * t / L]; line([c[0] + nx, c[1] + ny], [c[0] - nx, c[1] - ny]); }
}
function decoBox(d, at) {
  const long = decoPx(d) * (at[2] ?? 1) * pxPerStage(), a = img(PROPS + d.art + ".png");
  const [w, h] = a.naturalWidth ? [a.naturalWidth, a.naturalHeight] : [0.8, 1], m = Math.max(w, h);
  const bw = long * w / m, bh = long * h / m, [x, y] = toS(at[0], at[1]);
  return d.hang ? { x0: x - bw / 2, y0: y, x1: x + bw / 2, y1: y + bh } : { x0: x - bw / 2, y0: y - bh, x1: x + bw / 2, y1: y };
}

// ---- 짚기
function hit(x, y) {
  const p = P();
  if ($("vNav").checked) {
    for (let i = (p.nodes || []).length - 1; i >= 0; i--) { const [nx, ny] = toS(p.nodes[i].u, p.nodes[i].v); if (Math.hypot(nx - x, ny - y) < 10) return { type: "node", i }; }
  }
  if ($("vDeco").checked) {
    for (const [id, at] of Object.entries(p.deco || {})) { const [dx, dy] = toS(at[0], at[1]); if (Math.hypot(dx - x, dy - y) < 9) return { type: "deco", id }; }
  }
  if ($("vMush").checked) {
    const r = Math.max(8, fruitPx() * pxPerStage() / 2);
    for (const [z, pts] of Object.entries(p.mush || {})) for (let i = 0; i < pts.length; i++) { const [mx, my] = toS(pts[i][0], pts[i][1]); if (Math.hypot(mx - x, my - y) < r) return { type: "mush", zone: z, i }; }
  }
  if ($("vNav").checked) {
    const pos = Object.fromEntries((p.nodes || []).map((n) => [n.id, toS(n.u, n.v)]));
    for (let i = 0; i < (p.edges || []).length; i++) {
      const a = pos[p.edges[i][0]], b = pos[p.edges[i][1]]; if (!a || !b) continue;
      if (segDist(x, y, a, b) < 6) return { type: "edge", i };
    }
  }
  if ($("vDeco").checked) {
    for (const [id, at] of Object.entries(p.deco || {})) {
      const d = D.decos.find((q) => q.id === id); if (!d) continue;
      const b = decoBox(d, at); if (x > b.x0 && x < b.x1 && y > b.y0 && y < b.y1) return { type: "deco", id };
    }
  }
  return null;
}
function segDist(x, y, a, b) {
  const dx = b[0] - a[0], dy = b[1] - a[1], L = dx * dx + dy * dy || 1;
  const t = Math.max(0, Math.min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / L));
  return Math.hypot(x - a[0] - dx * t, y - a[1] - dy * t);
}
function getUV(s) {
  const p = P();
  if (s.type === "node") return [p.nodes[s.i].u, p.nodes[s.i].v];
  if (s.type === "deco") return p.deco[s.id];
  if (s.type === "mush") return p.mush[s.zone][s.i];
}
function setUV(s, u, v) {
  const p = P();
  if (s.type === "node") { p.nodes[s.i].u = r3(u); p.nodes[s.i].v = r3(v); }
  if (s.type === "deco") { p.deco[s.id][0] = r3(u); p.deco[s.id][1] = r3(v); }
  if (s.type === "mush") p.mush[s.zone][s.i] = [r3(u), r3(v)];
}

let drag = null;
cv.addEventListener("contextmenu", (e) => e.preventDefault());
cv.addEventListener("mousedown", (e) => {
  const x = e.offsetX * devicePixelRatio, y = e.offsetY * devicePixelRatio;
  if (e.button !== 0) { drag = { pan: true, x, y, cx: view.cx, cy: view.cy }; return; }
  const p = P(), h = hit(x, y), [u, v] = toUV(x, y);
  if (tool === "edge") {
    if (!h || h.type !== "node") { edgeFrom = null; draw(); return; }
    if (edgeFrom == null) { edgeFrom = h.i; draw(); return; }
    if (edgeFrom !== h.i) {
      touch();
      const a = p.nodes[edgeFrom].id, b = p.nodes[h.i].id;
      const j = p.edges.findIndex((q) => (q[0] === a && q[1] === b) || (q[0] === b && q[1] === a));
      if (j >= 0) p.edges.splice(j, 1);
      else { const e2 = [a, b, $("eKind").value]; if ($("eArt").value) e2.push($("eArt").value); p.edges.push(e2); }
    }
    edgeFrom = null; refresh(); return;
  }
  if (tool === "node" && !h) {
    touch();
    let n = 1; while (p.nodes.some((q) => q.id === "n" + n)) n++;
    p.nodes.push({ id: "n" + n, u: r3(u), v: r3(v) });
    sel = { type: "node", i: p.nodes.length - 1 }; refresh(); return;
  }
  if (tool === "mush" && !h) {
    const z = $("mZone").value; touch();
    (p.mush[z] ??= []).push([r3(u), r3(v)]);
    sel = { type: "mush", zone: z, i: p.mush[z].length - 1 }; refresh(); return;
  }
  sel = h; refresh();
  if (h && h.type !== "edge") { const [hu, hv] = getUV(h); drag = { sel: h, du: hu - u, dv: hv - v, moved: false }; }
});
window.addEventListener("mousemove", (e) => {
  if (!drag) return;
  const r = cv.getBoundingClientRect(), x = (e.clientX - r.left) * devicePixelRatio, y = (e.clientY - r.top) * devicePixelRatio;
  if (drag.pan) { const [w, h] = size(); view.cx = drag.cx - (x - drag.x) / ((w / h) * view.k); view.cy = drag.cy - (y - drag.y) / view.k; draw(); return; }
  if (!drag.moved) { touch(); drag.moved = true; }
  const [u, v] = toUV(x, y); setUV(drag.sel, u + drag.du, v + drag.dv); draw(); panel();
});
window.addEventListener("mouseup", () => (drag = null));
cv.addEventListener("wheel", (e) => {
  e.preventDefault();
  const x = e.offsetX * devicePixelRatio, y = e.offsetY * devicePixelRatio, [u, v] = toUV(x, y);
  view.k *= Math.exp(-e.deltaY * 0.0015);
  const [u2, v2] = toUV(x, y); view.cx += u - u2; view.cy += v - v2; draw();
}, { passive: false });

window.addEventListener("keydown", (e) => {
  if (e.ctrlKey && e.key.toLowerCase() === "s") { e.preventDefault(); save(); return; }
  if (e.ctrlKey && e.key.toLowerCase() === "z") { e.preventDefault(); doUndo(); return; }
  if (e.target.tagName === "INPUT" || e.target.tagName === "SELECT") return;
  if ((e.key === "Delete" || e.key === "Backspace") && sel) { e.preventDefault(); removeSel(); }
  if (e.key === "Escape") { sel = null; edgeFrom = null; refresh(); }
});
function removeSel() {
  const p = P(); touch();
  if (sel.type === "node") { const id = p.nodes[sel.i].id; p.nodes.splice(sel.i, 1); p.edges = p.edges.filter((e) => e[0] !== id && e[1] !== id); }
  if (sel.type === "edge") p.edges.splice(sel.i, 1);
  if (sel.type === "deco") delete p.deco[sel.id];
  if (sel.type === "mush") p.mush[sel.zone].splice(sel.i, 1);
  sel = null; refresh();
}

// ---- 왼쪽 판
function panel() {
  const p = P(), el = $("sel");
  el.className = sel ? "" : "dim"; el.innerHTML = sel ? "" : "없음";
  if (sel?.type === "node") {
    const n = p.nodes[sel.i];
    el.innerHTML = `<div class="row">마디 <input type="text" id="sId" value="${n.id}"> <span class="dim">${n.u}, ${n.v}</span></div>
      <div class="row"><label><input type="checkbox" id="sSpot" ${n.spot ? "checked" : ""}>머무는 곳(spot)</label>
      <label><input type="checkbox" id="sGround" ${n.ground ? "checked" : ""}>땅 입구(ground)</label></div>
      <div class="row"><button id="sDel">지우기</button></div>`;
    $("sId").onchange = (e) => {
      const v = e.target.value.trim(); if (!v || p.nodes.some((q) => q.id === v)) return panel();
      touch(); p.edges.forEach((q) => { if (q[0] === n.id) q[0] = v; if (q[1] === n.id) q[1] = v; }); n.id = v; refresh();
    };
    $("sSpot").onchange = (e) => { touch(); e.target.checked ? (n.spot = true) : delete n.spot; draw(); };
    $("sGround").onchange = (e) => { touch(); e.target.checked ? (n.ground = true) : delete n.ground; draw(); };
  } else if (sel?.type === "edge") {
    const e = p.edges[sel.i];
    el.innerHTML = `<div class="row">선 ${e[0]} — ${e[1]}</div>
      <div class="row">움직임 <select id="sKind"><option>walk</option><option>climb</option><option>hop</option></select>
      그림 <select id="sArt"><option value="">없음</option><option>plank</option><option>ladder</option><option>bridge</option></select></div>
      <div class="row"><button id="sDel">지우기</button></div>`;
    $("sKind").value = e[2]; $("sArt").value = e[3] || "";
    $("sKind").onchange = (ev) => { touch(); e[2] = ev.target.value; draw(); };
    $("sArt").onchange = (ev) => { touch(); ev.target.value ? (e[3] = ev.target.value) : e.splice(3); draw(); };
  } else if (sel?.type === "deco") {
    const d = D.decos.find((q) => q.id === sel.id), at = p.deco[sel.id];
    el.innerHTML = `<div class="row"><b>${d.n}</b> <span class="dim">${d.id} · ${d.hang ? "걸이형(고리가 점)" : "설치형(바닥이 점)"}</span></div>
      <div class="row">${at[0]}, ${at[1]} · 크기 <input type="number" id="sS" min="0.2" max="3" step="0.05" value="${at[2] ?? 1}"></div>
      <div class="row"><button id="sDel">자리 지우기</button></div>`;
    $("sS").onchange = (ev) => { touch(); const s = Number(ev.target.value); if (Math.abs(s - 1) < 1e-6) at.splice(2); else at[2] = r3(s); draw(); };
  } else if (sel?.type === "mush") {
    const pts = p.mush[sel.zone];
    el.innerHTML = `<div class="row">나무버섯 ${D.zones.find((z) => z.id === sel.zone).n} ${sel.i + 1}번 <span class="dim">${pts[sel.i]}</span></div>
      <div class="row"><button id="sUp">앞으로</button><button id="sDn">뒤로</button><button id="sDel">지우기</button></div>`;
    const mv = (k) => { const j = sel.i + k; if (j < 0 || j >= pts.length) return; touch(); [pts[sel.i], pts[j]] = [pts[j], pts[sel.i]]; sel.i = j; refresh(); };
    $("sUp").onclick = () => mv(-1); $("sDn").onclick = () => mv(1);
  }
  if ($("sDel")) $("sDel").onclick = removeSel;

  // 장식 목록: 이 단계에 열리는 장식 전부가 자리를 가져야 한다
  const ds = decosFor(pic), have = ds.filter((d) => p.deco[d.id]).length, extra = Object.keys(p.deco).filter((id) => !ds.some((d) => d.id === id));
  $("decoCount").textContent = `${have} / ${ds.length}` + (extra.length ? ` · 이 단계엔 안 열리는 장식 ${extra.length}` : "");
  $("decos").innerHTML = ds.map((d) => `<div class="item ${sel?.type === "deco" && sel.id === d.id ? "sel" : ""}" data-id="${d.id}">
      <img src="${PROPS + d.art}.png"> <span>${d.n}</span> <span class="dim">${d.hang ? "걸이" : "설치"}</span>
      ${p.deco[d.id] ? "" : '<span class="miss">자리 없음 — 누르면 만듦</span>'}</div>`).join("");
  $("decos").querySelectorAll(".item").forEach((it) => (it.onclick = () => {
    const id = it.dataset.id;
    if (!p.deco[id]) { touch(); p.deco[id] = [r3(view.cx), r3(Math.min(0.95, view.cy))]; }
    sel = { type: "deco", id }; refresh();
  }));

  const need = zoneSlots(pic);
  $("mushInfo").innerHTML = D.zones.map((z) => {
    const n = (p.mush[z.id] || []).length;
    return `<div class="row"><span style="color:${ZCOL[z.id]}">●</span> ${z.n} ${n} / ${need[z.id]}${n < need[z.id] ? ' <span class="miss" style="color:#ff7b7b">모자람</span>' : ""}</div>`;
  }).join("");
}

function refresh() { draw(); panel(); }

function setPic(pc) {
  pic = pc; sel = null; edgeFrom = null;
  let p = P();
  if (!p) { p = { pic, aspect: 1, nodes: [], edges: [], mush: {}, deco: {} }; data.pics.push(p); data.pics.sort((a, b) => a.pic - b.pic); }
  p.nodes ??= []; p.edges ??= []; p.mush ??= {}; p.deco ??= {};
  document.querySelectorAll("#stages button").forEach((b) => b.classList.toggle("on", Number(b.dataset.pic) === pc));
  const im = treeImg();
  const ready = () => {
    const a = Math.round(im.naturalWidth / im.naturalHeight * 10000) / 10000;
    if (p.aspect !== a) p.aspect = a;   // 가이드 그림 틀 (그림이 바뀌면 따라간다)
    fit(); panel();
  };
  if (im.complete && im.naturalWidth) ready(); else im.addEventListener("load", ready, { once: true });
  refresh();
}

function resize() { const r = cv.getBoundingClientRect(); cv.width = r.width * devicePixelRatio; cv.height = r.height * devicePixelRatio; draw(); }

async function main() {
  D = await fetch("/api/defs").then((r) => r.json());
  data = await fetch("/Assets/Resources/Rig/tree_layout.json", { cache: "no-store" }).then((r) => r.json());
  $("stages").innerHTML = D.width.map((_, i) => `<button data-pic="${i}">${i + 1}</button>`).join("");
  $("stages").querySelectorAll("button").forEach((b) => (b.onclick = () => setPic(Number(b.dataset.pic))));
  $("mZone").innerHTML = D.zones.map((z) => `<option value="${z.id}">${z.n}</option>`).join("");
  $("tools").querySelectorAll("button").forEach((b) => (b.onclick = () => {
    tool = b.dataset.tool; edgeFrom = null;
    $("tools").querySelectorAll("button").forEach((q) => q.classList.toggle("on", q === b)); draw();
  }));
  ["vNav", "vMush", "vDeco", "vArt", "alpha"].forEach((id) => ($(id).oninput = draw));
  $("fit").onclick = fit; $("save").onclick = save; $("undo").onclick = doUndo;
  window.addEventListener("beforeunload", (e) => { if (dirty) { e.preventDefault(); e.returnValue = ""; } });
  window.addEventListener("resize", resize);
  resize();
  setPic(7);
}
main();
