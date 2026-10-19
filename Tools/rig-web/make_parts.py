"""정령 리그2 부품 만들기 (2026-10-18, Tools/rig-web).

기존 진화 그림(Characters/Evo)을 그림풍 그대로 관절 단위로 나눠 Characters/Rig2/r2_<정령>_<모습>_* 로 저장한다.
기존 그림은 건드리지 않는다. 같은 캔버스 크기로 자르므로 나눈 부품의 중심 = 원래 그림 중심 (배치 좌표 그대로).

  ★2·★3  몸통(body) → 가슴(chest) + 치마(skirt)    : 허리(가장 좁은 줄)에서 나눔, 치마가 따로 흔들린다
          아래팔(arm_lo) → 아래팔(forearm) + 손(hand) : 손목(팔 굵기가 갑자기 넓어지는 줄)에서 나눔, 손이 늦게 따라온다
          다리(leg) → 다리(leg) + 신발(shoe)           : 신발 윗선(갈색)에서 나눔, 발이 땅에 붙는다
  ★0·★1  몸(body_bare) → 갓(cap) + 대(stem) + 발 둘(foot) : 갓 테두리 아래에서 나누고, 발(갈색)은 떼어 대 밑을 메운다
  얼굴    face_normal → blink · happy · surprise · effort  : 눈·입을 찾아 그려 넣는다 (같은 캔버스)

python Tools/rig-web/make_parts.py            부품만 (spirit_rig2.json이 있으면 그대로 둔다)
python Tools/rig-web/make_parts.py --json     부품 + spirit_rig2.json 새로 쓰기 (웹에서 고친 값이 지워진다)
python Tools/rig-web/make_parts.py --preview  Logs/Screens/rig2_parts_<id>.png 로 나눈 결과 보기
"""
import json
import os
import sys
import uuid

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EVO = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/Evo")
OUT = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/Rig2")
OLD_RIG = os.path.join(ROOT, "Assets/Resources/Rig/evo_rig.json")
NEW_RIG = os.path.join(ROOT, "Assets/Resources/Rig/spirit_rig2.json")
SHOTS = os.path.join(ROOT, "Logs/Screens")
PPU = 100.0
OUTFITS = ("pick", "climb", "carry")


def load(name):
    p = os.path.join(EVO, name + ".png")
    return np.asarray(Image.open(p).convert("RGBA")).copy() if os.path.exists(p) else None


def exists(name):
    return os.path.exists(os.path.join(EVO, name + ".png"))


META_SRC = None


def save(arr, name):
    """Characters/Rig2/<name>.png + .meta (기존 진화 그림과 같은 가져오기 설정: Sprite · 가운데 피벗 · PPU 100). guid는 처음 한 번만 만든다."""
    global META_SRC
    os.makedirs(OUT, exist_ok=True)
    png = os.path.join(OUT, name + ".png")
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        if META_SRC is None:
            META_SRC = open(os.path.join(EVO, "k_fire_3_body.png.meta"), encoding="utf-8").read()
        lines = [("guid: " + uuid.uuid4().hex) if l.startswith("guid:") else l for l in META_SRC.split("\n")]
        open(meta, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    return "Rig2/" + name


def alpha(a):
    return a[:, :, 3] > 40


def widths(a):
    return alpha(a).sum(axis=1)


def fade_rows(arr, y0, y1, keep_above=True):
    """keep_above: y0 위는 그대로, y0~y1 사이는 서서히 투명, y1 아래는 지움. (아니면 반대)"""
    out = arr.copy()
    h = arr.shape[0]
    ys = np.arange(h, dtype=np.float32)
    f = np.clip((y1 - ys) / max(1, y1 - y0), 0, 1) if keep_above else np.clip((ys - y0) / max(1, y1 - y0), 0, 1)
    out[:, :, 3] = (out[:, :, 3] * f[:, None]).astype(np.uint8)
    return out


# ───────────────────────── ★2·★3 ─────────────────────────
def waist_row(body):
    w = widths(body)
    h = len(w)
    lo, hi = int(h * 0.22), int(h * 0.6)
    seg = np.where(w[lo:hi] > 0, w[lo:hi], 1 << 30)
    return lo + int(np.argmin(seg))


def split_body(body, seam):
    chest = fade_rows(body, seam, seam + 5, keep_above=True)          # 가슴: 허리 아래 5px에서 사라짐 (치마 위에 겹쳐 이음새를 숨김)
    skirt = fade_rows(body, seam - 7, seam - 6, keep_above=False)     # 치마: 허리 7px 위부터 (가슴 뒤로 숨는 여유)
    return chest, skirt


def wrist_row(arm_lo):
    """아래팔 그림에서 손목: 팔 굵기 중간값보다 18% 넓어지는 첫 줄 (손이 시작되는 곳)."""
    w = widths(arm_lo)
    rows = np.nonzero(w)[0]
    top, bot = rows[0], rows[-1]
    mid = w[top + (bot - top) // 4: top + (bot - top) // 2 + 1]
    base = float(np.median(mid)) if len(mid) else float(w[rows].mean())
    for y in range(top + (bot - top) // 3, bot):
        if w[y] > base * 1.18:
            return y
    return top + int((bot - top) * 0.72)


def ball_split(arr, y, r_scale=1.0):
    """y 줄에서 둘로 (공 관절): 위 조각은 y 아래로 원 안만, 아래 조각은 y 위로 원 안만 남긴다 (split_arm과 같은 방식)."""
    al = alpha(arr)
    cols = np.nonzero(al[y])[0]
    if len(cols) == 0:
        return arr.copy(), arr.copy(), (arr.shape[1] / 2, y)
    cx, r = (cols[0] + cols[-1]) / 2, (cols[-1] - cols[0]) / 2 * 1.02 * r_scale
    yy, xx = np.mgrid[0:arr.shape[0], 0:arr.shape[1]]
    disk = (xx - cx) ** 2 + (yy - y) ** 2 <= r * r
    up, lo = arr.copy(), arr.copy()
    up[(yy > y) & ~disk, 3] = 0
    lo[(yy < y) & ~disk, 3] = 0
    return up, lo, (cx, y)


def lum(arr):
    return arr[:, :, 0] * 0.299 + arr[:, :, 1] * 0.587 + arr[:, :, 2] * 0.114


def shoe_mask(leg):
    """신발 = 어두운 갈색 픽셀 (밝은 크림색 다리와 구분)."""
    al = alpha(leg)
    m = al & (lum(leg.astype(np.float32)) < 190)
    m = ndimage.binary_opening(m, iterations=1)
    lab, n = ndimage.label(m)
    if n == 0:
        return m
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    return lab == (1 + int(np.argmax(sizes)))


def split_leg(leg):
    sm = shoe_mask(leg)
    rows = np.nonzero(sm.any(axis=1))[0]
    top = int(rows[0]) if len(rows) else int(leg.shape[0] * 0.65)
    shoe = leg.copy()
    keep = ndimage.binary_dilation(sm, iterations=2) & (np.arange(leg.shape[0])[:, None] >= top - 1)
    shoe[~keep, 3] = 0
    legp = fade_rows(leg, top + 6, top + 10, keep_above=True)           # 다리는 신발 속으로 조금 들어가서 끝남
    cols = np.nonzero(alpha(leg)[min(top + 2, leg.shape[0] - 1)])[0]
    ax = (cols[0] + cols[-1]) / 2 if len(cols) else leg.shape[1] / 2
    return legp, shoe, (ax, top + 3)


# ───────────────────────── 얼굴 표정 ─────────────────────────
def face_parts(face):
    a = face.astype(np.float32)
    dark = (a[:, :, 3] > 90) & (lum(a) < 120)
    lab, n = ndimage.label(dark)
    comps = []
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(ys) < 6:
            continue
        comps.append({"i": i, "n": len(ys), "x0": xs.min(), "x1": xs.max(), "y0": ys.min(), "y1": ys.max(),
                      "cx": xs.mean(), "cy": ys.mean(), "col": a[ys, xs, :3].mean(0)})
    comps.sort(key=lambda c: -c["n"])
    eyes = None
    for i in range(len(comps)):
        for j in range(i + 1, len(comps)):
            c1, c2 = comps[i], comps[j]
            if abs(c1["cy"] - c2["cy"]) < 14 and abs(c1["cx"] - c2["cx"]) > 20 and abs(c1["n"] - c2["n"]) < max(c1["n"], c2["n"]) * 0.6:
                eyes = sorted([c1, c2], key=lambda c: c["cx"])
                break
        if eyes:
            break
    if not eyes:
        return None
    mid = (eyes[0]["cx"] + eyes[1]["cx"]) / 2
    mouth = [c for c in comps if c not in eyes and eyes[0]["cx"] < c["cx"] < eyes[1]["cx"] and c["cy"] > eyes[0]["cy"] - 2]
    mouth = min(mouth, key=lambda c: abs(c["cx"] - mid)) if mouth else None
    return {"eyes": eyes, "mouth": mouth, "lab": lab}


def erase(arr, c, lab, pad=2):
    m = lab == c["i"]
    m = ndimage.binary_dilation(m, iterations=pad)
    out = arr.copy()
    out[m, 3] = 0
    return out


def draw_layer(shape, fn, ss=4):
    """크게 그려 줄여서 부드러운 선 (그림풍과 어울리게 끝이 둥근 선)."""
    h, w = shape[:2]
    big = Image.new("RGBA", (w * ss, h * ss))
    d = ImageDraw.Draw(big)
    fn(d, ss)
    return np.asarray(big.resize((w, h), Image.LANCZOS)).astype(np.float32)


def over(dst, src):
    d = dst.astype(np.float32)
    sa = src[:, :, 3:4] / 255.0
    da = d[:, :, 3:4] / 255.0
    oa = sa + da * (1 - sa)
    rgb = (src[:, :, :3] * sa + d[:, :, :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
    return np.concatenate([rgb, oa * 255], axis=2)


def arc(d, ss, cx, cy, rw, rh, up, width, col):
    """눈 감은 선: up=True ∩ (웃는 눈), False ∪ (감은 눈)."""
    box = [(cx - rw) * ss, (cy - rh) * ss, (cx + rw) * ss, (cy + rh) * ss]
    d.arc(box, 200 if up else 20, 340 if up else 160, fill=col, width=max(1, int(width * ss)))
    # 둥근 끝
    import math
    for ang in ((200, 340) if up else (20, 160)):
        t = math.radians(ang)
        x, y = cx + rw * math.cos(t), cy + rh * math.sin(t)
        r = width / 2
        d.ellipse([(x - r) * ss, (y - r) * ss, (x + r) * ss, (y + r) * ss], fill=col)


def make_faces(face):
    fp = face_parts(face)
    if fp is None:
        return {}
    eyes, mouth, lab = fp["eyes"], fp["mouth"], fp["lab"]
    col = tuple(int(v) for v in (eyes[0]["col"] * 0.9)) + (255,)
    no_eyes = face.copy()
    for e in eyes:
        no_eyes = erase(no_eyes, e, lab)
    r = np.mean([(e["x1"] - e["x0"] + 1) / 2 for e in eyes])
    lw = max(1.6, r * 0.55)
    out = {}

    def eyes_layer(kind):
        def fn(d, ss):
            for k, e in enumerate(eyes):
                cx, cy = e["cx"], e["cy"]
                if kind == "blink":
                    arc(d, ss, cx, cy - r * 0.35, r * 1.15, r * 0.7, False, lw, col)
                elif kind == "happy":
                    arc(d, ss, cx, cy + r * 0.45, r * 1.1, r * 0.9, True, lw, col)
                elif kind == "effort":   # > <
                    s = 1 if k == 0 else -1
                    pts = [(cx - s * r * 0.9, cy - r * 0.8), (cx + s * r * 0.8, cy), (cx - s * r * 0.9, cy + r * 0.8)]
                    d.line([(x * ss, y * ss) for x, y in pts], fill=col, width=max(1, int(lw * ss)), joint="curve")
                    for x, y in (pts[0], pts[2]):
                        d.ellipse([(x - lw / 2) * ss, (y - lw / 2) * ss, (x + lw / 2) * ss, (y + lw / 2) * ss], fill=col)
        return draw_layer(face.shape, fn)

    for kind in ("blink", "happy", "effort"):
        out[kind] = over(no_eyes, eyes_layer(kind))
    # 놀람: 눈을 1.25배로 키우고 입을 동그랗게
    sur = no_eyes.copy()
    if mouth is not None:
        sur = erase(sur, mouth, lab)

    def fn(d, ss):
        for e in eyes:
            rr = r * 1.25
            d.ellipse([(e["cx"] - rr) * ss, (e["cy"] - rr * 1.12) * ss, (e["cx"] + rr) * ss, (e["cy"] + rr * 1.12) * ss], fill=col)
            hl = rr * 0.38
            d.ellipse([(e["cx"] - rr * 0.35 - hl) * ss, (e["cy"] - rr * 0.45 - hl) * ss, (e["cx"] - rr * 0.35 + hl) * ss, (e["cy"] - rr * 0.45 + hl) * ss], fill=(255, 250, 240, 230))
        mx = mouth["cx"] if mouth is not None else (eyes[0]["cx"] + eyes[1]["cx"]) / 2
        my = mouth["cy"] if mouth is not None else eyes[0]["cy"] + r * 2.2
        mr = r * 0.62
        d.ellipse([(mx - mr * 0.8) * ss, (my - mr) * ss, (mx + mr * 0.8) * ss, (my + mr) * ss], fill=(120, 60, 55, 255))
    out["surprise"] = over(sur, draw_layer(face.shape, fn))
    return out


# ───────────────────────── ★0·★1 ─────────────────────────
def split_cap(body):
    """★0·★1 갓/대 나누기. 대(stem)는 갓 주름 앞에 그려지므로, 크림색 대 영역을 아래에서 위로 채워 찾는다
    (대 윗부분·얼굴 자리는 주름 속까지 올라가 있다). 갓 = 나머지, 대가 빠진 주름 구멍은 좌우 색으로 메운다 (갓이 기울어도 틈이 안 보이게)."""
    a = body.astype(np.float32)
    al = alpha(body)
    w = widths(body)
    h, W = al.shape
    peak = int(np.argmax(w))
    rim = next((y for y in range(peak, len(w)) if w[y] < w[peak] * 0.62), int(len(w) * 0.6))
    cols = np.nonzero(al[min(rim + int(h * 0.12), h - 1)])[0]
    x0, x1 = (cols[0], cols[-1]) if len(cols) else (W // 3, W * 2 // 3)
    mx, mn = a[:, :, :3].max(2), a[:, :, :3].min(2)
    sat = (mx - mn) / np.maximum(mx, 1)
    cream = al & (lum(a) > 185) & (sat < 0.32)
    xs = np.arange(W)
    band = np.zeros_like(al)
    band[max(0, rim - int(h * 0.3)):, max(0, x0 - 4):x1 + 5] = True
    lab, n = ndimage.label(cream & band)
    seed = lab[min(rim + int(h * 0.08), h - 1), (x0 + x1) // 2]
    if seed == 0:
        seed = np.bincount(lab[rim:].ravel())[1:].argmax() + 1 if n else 0
    region = lab == seed
    region = ndimage.binary_fill_holes(ndimage.binary_closing(region, iterations=2))
    region |= al & (np.arange(h)[:, None] >= rim + 2) & band                       # 테두리 아래 대 폭 안은 모두 대 (발·팔 혹 포함)
    lab2, _ = ndimage.label(al & ~region)
    region |= al & np.isin(lab2, np.unique(lab2[rim + 2:][lab2[rim + 2:] > 0])) & (np.arange(h)[:, None] >= rim)   # 테두리 아래 나머지 덩어리(★1 팔 혹)
    region &= al
    # 갓 쪽에 이어진 물방울·주름 끝은 갓으로
    side = al & ~band
    lab3, _ = ndimage.label(side)
    hang = np.unique(lab3[:rim + 2][lab3[:rim + 2] > 0])
    region &= ~np.isin(lab3, hang)
    # 대 윗선을 매끈한 둥근 선으로 (크림색 경계가 털처럼 들쭉날쭉하던 것) + 위 끝 4px 부드럽게
    region = ndimage.binary_opening(region, structure=np.ones((5, 5)), iterations=2) | (region & (np.arange(h)[:, None] >= rim + 2))
    lab4, n4 = ndimage.label(region)
    if n4 > 1:
        keep = np.bincount(lab4.ravel())[1:].argmax() + 1
        region = (lab4 == keep) | (region & (np.arange(h)[:, None] >= rim + 2))
    soft = ndimage.gaussian_filter(region.astype(np.float32), 1.6)
    stem = body.copy()
    top_rows = np.arange(h)[:, None] < rim + 2
    a_stem = np.where(top_rows, np.clip((soft - 0.25) / 0.5, 0, 1), region.astype(np.float32))
    stem[:, :, 3] = (stem[:, :, 3] * a_stem).astype(np.uint8)
    cap = body.copy()
    cap[region & ~top_rows, 3] = 0
    hole = region & top_rows & al
    # 주름 구멍 메우기 (테두리 위만): 구멍 둘레 주름 색(크림색 대 제외)의 중간값으로 칠하고 살짝 세로 결을 준다.
    # 대 뒤라 평소엔 안 보이고, 갓이 기울 때만 틈으로 보인다
    ring = ndimage.binary_dilation(hole, iterations=6) & ~hole & al & (np.arange(h)[:, None] < rim + 2)
    rc = a[ring][:, :3]
    if len(rc):
        rl = rc @ np.array([0.299, 0.587, 0.114])
        rs = (rc.max(1) - rc.min(1)) / np.maximum(rc.max(1), 1)
        g = rc[~((rl > 185) & (rs < 0.32))] if (~((rl > 185) & (rs < 0.32))).sum() > 10 else rc
        col = np.median(g, axis=0)
        cf = cap.astype(np.float32)
        hy, hx = np.nonzero(hole)
        streak = 1 + 0.06 * np.sin(hx * 0.9) - 0.12 * (hy - hy.min()) / max(1, hy.max() - hy.min())
        cf[hy, hx, :3] = col[None, :] * streak[:, None]
        cf[hole, 3] = body[hole, 3]
        cap = cf.clip(0, 255).astype(np.uint8)
    for y in range(0, 0):
        holes = np.nonzero(region[y])[0]
        if len(holes) == 0:
            continue
        l, r = holes[0] - 1, holes[-1] + 1
        if l < 0 or r >= W or not al[y, l] or not al[y, r]:
            continue
        cl, cr = body[y, l].astype(np.float32), body[y, r].astype(np.float32)
        for x in range(l + 1, r):
            t = (x - l) / (r - l)
            cap[y, x] = cl * (1 - t) + cr * t
    return cap, stem, rim


def split_feet(stem):
    """발 = 대 아래쪽의 갈색 덩어리 둘. 발을 떼고, 그 자리 대 밑단은 바로 위 색으로 메운다."""
    a = stem.astype(np.float32)
    h = stem.shape[0]
    al = alpha(stem)
    m = al & (lum(a) < 165) & (np.arange(h)[:, None] > h * 0.7)
    m = ndimage.binary_opening(m, iterations=1)
    lab, n = ndimage.label(m)
    if n < 2:
        return stem, None, None, None
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    idx = list(np.argsort(-sizes)[:2] + 1)
    feet = []
    for i in idx:
        fm = ndimage.binary_dilation(lab == i, iterations=2) & al
        ys, xs = np.nonzero(fm)
        f = stem.copy()
        f[~fm, 3] = 0
        feet.append((xs.mean(), f, fm, ys.min()))
    feet.sort(key=lambda t: t[0])
    body = stem.copy()
    allf = feet[0][2] | feet[1][2]
    # 메우기: 발이 덮던 대 부분 (발 윗선 위쪽 몸 픽셀이 있는 열)만 위 색을 끌어내린다
    ys_all, xs_all = np.nonzero(allf)
    for x in np.unique(xs_all):
        col = np.nonzero(allf[:, x])[0]
        y0 = col.min()
        above = np.nonzero(al[:y0, x] & ~allf[:y0, x])[0]
        if len(above) == 0:
            body[col, x, 3] = 0
            continue
        src = body[above[-1], x].copy()
        # 대 몸통 아래 끝: 발 윗선에서 몸 폭 쪽으로 3px만 메워 둥근 밑단
        fill_to = min(col.max(), y0 + 3)
        for y in col:
            if y <= fill_to:
                body[y, x] = src
            else:
                body[y, x, 3] = 0
    return body, feet[0][1], feet[1][1], (feet[0][3], feet[1][3])


# ───────────────────────── 배치 ─────────────────────────
def r4(v):
    return round(float(v), 4)


def part(sprite, x, y):
    return {"sprite": sprite, "x": r4(x), "y": r4(y)}


def px2rig(center, shape, px, py):
    """그림 픽셀 (px, py) → 리그 좌표 (그림 중심이 center에 있을 때)."""
    h, w = shape[:2]
    return [r4(center[0] + (px - w / 2) / PPU), r4(center[1] + (h / 2 - py) / PPU)]


def build_headed(l, preview):
    kid, form = l["kind"], l["form"]
    pre = f"r2_{kid}_{form}_"
    body_name = l["body"]["sprite"]
    body = load(body_name)
    seam = waist_row(body)
    bc = (l["body"]["x"], l["body"]["y"])
    for o in ("",) + OUTFITS:
        b = load(body_name + ("_" + o if o else ""))
        if b is None:
            continue
        chest, skirt = split_body(b, seam)
        save(chest, pre + "chest" + ("_" + o if o else ""))
        save(skirt, pre + "skirt" + ("_" + o if o else ""))
    waist = px2rig(bc, body.shape, body.shape[1] / 2, seam)

    up = load(l["armUp"])
    lo = load(l["armLo"])
    wy = wrist_row(lo)
    fore, hand, (wx, wy) = ball_split(lo, wy, 1.05)
    save(fore, pre + "forearm")
    save(hand, pre + "hand")
    shL = l["shoulderL"]
    arm_c = (shL[0] + l["armCenter"][0], shL[1] + l["armCenter"][1])
    elbow = [r4(shL[0] + l["armElbow"][0]), r4(shL[1] + l["armElbow"][1])]
    wrist = px2rig(arm_c, lo.shape, wx, wy)

    leg = load(l["leg"])
    legp, shoe, (ax, ay) = split_leg(leg)
    save(legp, pre + "leg")
    save(shoe, pre + "shoe")
    hipL = l["hipL"]
    leg_c = (hipL[0] + l["legCenter"][0], hipL[1] + l["legCenter"][1])
    ankle = px2rig(leg_c, leg.shape, ax, ay)
    sole = r4(leg_c[1] - leg.shape[0] / 2 / PPU)
    # 무릎: 엉덩이 줄과 발목 줄 가운데에서 공 관절로 허벅지/정강이 나누기
    hip_row = leg.shape[0] / 2 - (hipL[1] - leg_c[1]) * PPU
    krow = int(round((hip_row + ay) / 2))
    thigh, shin, (kx, ky) = ball_split(legp, krow, 1.04)
    save(thigh, pre + "thigh")
    save(shin, pre + "shin")
    knee = px2rig(leg_c, leg.shape, kx, ky)

    face = load(l["face"] + "normal")
    faces = make_faces(face)
    save(face, pre + "face_normal")
    for k, v in faces.items():
        save(v, pre + "face_" + k)

    head = l["head"]
    hair = load(l["hair"]) if l.get("hair") else None
    hp = l.get("hairPos", [0, 0])
    hair_root = [r4(hp[0]), r4(hp[1] + hair.shape[0] / 2 / PPU * 0.72)] if hair is not None else [0, 0]
    hd = load(head["bare"] or head["sprite"])
    cap_top = [r4(head["x"]), r4(head["y"] + hd.shape[0] / 2 / PPU * 0.92)]

    look = {
        "id": l["id"], "kind": kid, "form": form, "height": l["height"], "k": l["k"],
        "chest": part("Rig2/" + pre + "chest", *bc),
        "skirt": part("Rig2/" + pre + "skirt", *bc),
        "collar": part("Evo/" + l["collar"], *l["collarPos"]),
        "head": part("Evo/" + (head["bare"] or head["sprite"]), head["x"], head["y"]),
        "face": "Rig2/" + pre + "face_",
        "hair": part("Evo/" + l["hair"], *hp) if hair is not None else part("", 0, 0),
        "upperArm": part("Evo/" + l["armUp"], *arm_c),
        "foreArm": part("Rig2/" + pre + "forearm", *arm_c),
        "hand": part("Rig2/" + pre + "hand", *arm_c),
        "leg": part("Rig2/" + pre + "leg", *leg_c),
        "thigh": part("Rig2/" + pre + "thigh", *leg_c),
        "shin": part("Rig2/" + pre + "shin", *leg_c),
        "shoe": part("Rig2/" + pre + "shoe", *leg_c),
        "waist": waist, "neck": l["neck"], "hairRoot": hair_root, "capTop": cap_top,
        "shoulderL": l["shoulderL"], "shoulderR": l["shoulderR"], "elbow": elbow, "wrist": wrist,
        "hipL": l["hipL"], "hipR": l["hipR"], "knee": knee, "ankle": ankle, "sole": sole,
        "legScale": 1.6 if l["id"] == "fire_3" else 1.0,     # 다리 늘이기 (치마 밑으로 무릎이 보이게, 몸이 그만큼 올라감)
    }
    if preview:
        sheet([body, load(body_name.replace("body", "body")), up, fore, hand, legp, shoe, face] + list(faces.values()),
              [None, None, None, None, None, None, None, None], f"rig2_parts_{l['id']}.png",
              marks={0: [seam], 3: [wy], 5: [ay]})
    return look


def clean_strays(img):
    """발을 떼며 남은 1~2px 실선 지우기: 열기(opening)로 사라지는 가는 조각 중 큰 몸통에 안 붙은 것."""
    al = alpha(img)
    op = ndimage.binary_opening(al, iterations=2)
    lab, n = ndimage.label(op)
    if n == 0:
        return img
    main = lab == (np.bincount(lab.ravel())[1:].argmax() + 1)
    keep = ndimage.binary_dilation(main, iterations=3) & al
    out = img.copy()
    out[~keep, 3] = 0
    return out


def split_nubs(stem):
    """★1 몸 옆의 동그란 손 둘을 떼어 낸다. 손의 바깥 반원은 몸 윤곽선의 일부라서, 아래쪽 옆에서
    '바깥 반원이 윤곽선에 가장 잘 겹치는 원'을 찾는다 (반지름·중심을 훑어 찾기). 그 원 = 손 전체 그림.
    몸통에서는 손 자리의 옆선을 손 위·아래 옆선을 이어 다시 긋고, 손이 덮던 몸 안쪽은 주변 몸 색으로 메운다."""
    al = alpha(stem)
    h, w = al.shape
    ys, xs = np.nonzero(al)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    bw, bh = x1 - x0, y1 - y0
    boundary = al & ~ndimage.binary_erosion(al, iterations=1)
    dist = ndimage.distance_transform_edt(~boundary)
    nubs = []
    for side in (-1, 1):
        best = None
        ang = np.linspace(np.pi / 2, 3 * np.pi / 2, 25) if side < 0 else np.linspace(-np.pi / 2, np.pi / 2, 25)
        ang = ang[3:-3]                                               # 몸에 붙는 위·아래 끝은 빼고 바깥 호만
        for r in np.arange(bw * 0.07, bw * 0.15, 1.5):
            xa = np.arange(x0 + r * 0.6, x0 + bw * 0.25, 2) if side < 0 else np.arange(x1 - bw * 0.25, x1 - r * 0.6, 2)
            for cx in xa:
                for cy in np.arange(y0 + bh * 0.4, y1 - r * 0.8, 2):
                    px = (cx + r * np.cos(ang)).astype(int); py = (cy + r * np.sin(ang)).astype(int)
                    if px.min() < 0 or px.max() >= w or py.min() < 0 or py.max() >= h:
                        continue
                    sc = np.mean(dist[py, px] < 1.6) - 0.02 * abs(r - bw * 0.1)
                    if best is None or sc > best[0]:
                        best = (sc, cx, cy, r)
        if best is None or best[0] < 0.45:
            return None
        nubs.append((best[1], best[2], best[3], side))
    out_img = stem.copy().astype(np.float32)
    res = {"nub": []}
    X, Y = np.arange(w)[None, :], np.arange(h)[:, None]
    hole_all = np.zeros_like(al)
    for cx, cy, rr, side in nubs:
        circ = (X - cx) ** 2 + (Y - cy) ** 2 <= (rr * 1.12) ** 2
        img = stem.copy()
        img[~(circ & al), 3] = 0
        res["nub"].append({"img": img, "pivot": (cx - side * rr * 0.5, cy - rr * 0.15)})   # 몸에 붙는 점
        # 손 위·아래 줄의 옆선을 이어 새 옆선 (2차: 몸이 둥글게 조금 불룩)
        ya, yb = int(cy - rr * 1.2), int(cy + rr * 1.2)
        ya, yb = max(ya, y0), min(yb, y1)
        def edge(y):
            c = np.nonzero(al[y])[0]
            return (c[0] if side < 0 else c[-1]) if len(c) else None
        ea, eb = edge(ya), edge(yb)
        if ea is None or eb is None:
            continue
        for y in range(ya, yb + 1):
            t = (y - ya) / max(1, yb - ya)
            ex = ea + (eb - ea) * t + side * 4 * t * (1 - t) * rr * 0.25
            cols = np.nonzero(al[y])[0]
            for x in cols:
                d = (ex - x) if side < 0 else (x - ex)           # + = 새 옆선 밖
                if d > 0:
                    out_img[y, x, 3] = out_img[y, x, 3] * float(np.clip(1 - d, 0, 1))
            inside = circ[y] & al[y]
            xs_in = np.nonzero(inside)[0]
            for x in xs_in:
                d = (ex - x) if side < 0 else (x - ex)
                if d <= 0:
                    hole_all[y, x] = True
    # 손이 덮던 몸 안쪽 = 주변 몸 색으로 (번짐)
    known = (out_img[:, :, 3] > 40) & ~hole_all
    for _ in range(60):
        blur = np.stack([ndimage.uniform_filter(out_img[:, :, c] * known, 5) for c in range(3)], 2)
        wsum = ndimage.uniform_filter(known.astype(np.float32), 5)
        fill = hole_all & ~known & (wsum > 0.05)
        out_img[fill, :3] = blur[fill] / wsum[fill][:, None]
        known = known | fill
    res["stem"] = out_img.clip(0, 255).astype(np.uint8)
    return res


def build_headless(l, preview):
    kid, form = l["kind"], l["form"]
    pre = f"r2_{kid}_{form}_"
    body = load(l["body"]["bare"]) if l["body"].get("bare") and exists(l["body"]["bare"]) else load(l["body"]["sprite"])
    cap, stem, rim = split_cap(body)
    stem_b, fl, fr, ftops = split_feet(stem)
    stem_b = clean_strays(stem_b)
    nubs = split_nubs(stem_b) if form == 1 else None
    if nubs is not None:
        stem_b = nubs["stem"]
    save(cap, pre + "cap")
    save(stem_b, pre + "stem")
    bc = (l["body"]["x"], l["body"]["y"])
    look = {"id": l["id"], "kind": kid, "form": form, "height": l["height"], "k": l["k"],
            "cap": part("Rig2/" + pre + "cap", *bc), "stem": part("Rig2/" + pre + "stem", *bc),
            "face": "Rig2/" + pre + "face_"}
    if fl is not None:
        save(fl, pre + "footL")
        save(fr, pre + "footR")
        look["footL"] = part("Rig2/" + pre + "footL", *bc)
        look["footR"] = part("Rig2/" + pre + "footR", *bc)
        al = alpha(fl)
        ys, xs = np.nonzero(al)
        look["ankle"] = px2rig(bc, body.shape, xs.mean(), ftops[0] + 2)        # 왼발 위 끝 (발이 매달린 곳)
        ar = alpha(fr)
        ys2, xs2 = np.nonzero(ar)
        look["ankleR"] = px2rig(bc, body.shape, xs2.mean(), ftops[1] + 2)
    face = load(l["face"] + "normal")
    save(face, pre + "face_normal")
    for k, v in make_faces(face).items():
        save(v, pre + "face_" + k)
    if nubs is not None:
        # ★1 짧은 팔(몸 옆 동그란 손): 왼쪽 그림 하나 + 붙는 점. 오른쪽은 그 그림을 뒤집지 않고 따로 (그림이 조금씩 다름)
        for side, key in ((0, "L"), (1, "R")):
            nb = nubs["nub"][side]
            save(nb["img"], pre + "nub" + key)
            look["nub" + key] = part("Rig2/" + pre + "nub" + key, *bc)
            look["nubPivot" + key] = px2rig(bc, body.shape, *nb["pivot"])
    look["capJoint"] = px2rig(bc, body.shape, body.shape[1] / 2, rim)
    al = alpha(body)
    look["capTop"] = px2rig(bc, body.shape, body.shape[1] / 2, int(np.nonzero(al.any(axis=1))[0][0]) + 4)
    look["sole"] = r4(bc[1] - body.shape[0] / 2 / PPU)
    if preview:
        sheet([body, cap, stem_b] + ([fl, fr] if fl is not None else []) + ([n["img"] for n in nubs["nub"]] if nubs else []) + [face], [], f"rig2_parts_{l['id']}.png", marks={0: [rim]})
    return look


def sheet(arrs, _labels, name, marks=None):
    arrs = [a for a in arrs if a is not None]
    W = sum(a.shape[1] + 12 for a in arrs)
    H = max(a.shape[0] for a in arrs)
    c = Image.new("RGBA", (W, H), (200, 200, 210, 255))
    x = 0
    d = ImageDraw.Draw(c)
    for i, a in enumerate(arrs):
        c.alpha_composite(Image.fromarray(a.astype(np.uint8)), (x, 0))
        for y in (marks or {}).get(i, []):
            d.line([(x, y), (x + a.shape[1], y)], fill=(255, 0, 80, 255))
        x += a.shape[1] + 12
    os.makedirs(SHOTS, exist_ok=True)
    c.save(os.path.join(SHOTS, name))


def main():
    args = sys.argv[1:]
    want = [a for a in args if not a.startswith("--")]
    preview = "--preview" in args
    old = json.load(open(OLD_RIG, encoding="utf-8"))["looks"]
    looks = []
    for l in old:
        if want and l["id"] not in want and l["kind"] not in want:
            continue
        look = build_headed(l, preview) if l["form"] >= 2 else build_headless(l, preview)
        looks.append(look)
        print(l["id"], "ok")
    if not want and ("--json" in args or not os.path.exists(NEW_RIG)):
        data = {"looks": looks}
        if os.path.exists(NEW_RIG):
            prev = json.load(open(NEW_RIG, encoding="utf-8"))
            if "motion" in prev:
                data["motion"] = prev["motion"]
        json.dump(data, open(NEW_RIG, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print("wrote", NEW_RIG)


if __name__ == "__main__":
    main()
