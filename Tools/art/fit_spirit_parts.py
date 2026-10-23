"""Rig the spirit puppet parts (Characters/Spirits/p_<id>_*) by their joints and write Assets/Resources/Rig/spirit_rig.json.

The parts come from one sheet per spirit drawn at one scale, but not from the same drawing as the whole pictures
(Characters/Kids/...), so every joint is found as an exact point on the parts themselves and the parts are snapped
joint to joint (fitting the parts onto the whole picture was tried first and fails: each generation draws differently).

  neck      torso: the short stub at the top of the body (top run, down to where it widens into the shoulders) → its centre
            head : the stub under the chin, found by the torso stub's skin colour (long hair can hang below the chin)
            → the head's stub centre is put on the torso's stub centre; the head turns about that point
  shoulder  torso: the round shoulder nub = a circle fitted to the body outline around the left-most point of the upper body
            arm  : the round shoulder end = a circle fitted to the outline of the arm's top cap
            → ball joint: the arm is scaled so both circles have the same radius and put centre on centre
  elbow     the arm is cut in the middle (square to the shoulder→hand line) into upper arm + forearm; both pieces keep a
            disc of the arm's own pixels around the elbow so the joint stays closed when it bends
Outfit torsos (pick · climb · carry) are read on their bare-skin pixels only (the base torso's stub colour), so ladders,
ropes, bundles and baskets never pass for a stub or a nub; when a strap cuts the nub off, the base torso's nub is used
(same offset from the stub). The right arm mirrors the left about the neck line and is drawn
behind the torso (far arm).

Per-spirit settings in Tools/art/spirit_rig_fix.json: {"<id>": {"armRest": deg, "armScale": multiplier}},
"nubs": {"<id>": {"L": [x, y, r], "R": [x, y, r]}} (the bare torso's shoulder nubs read by eye, used instead of the
automatic ones; the other looks follow the neck stub), "eyes" (head eye hints, see build_spirit_faces.py), and per look
{"<id>_<look>": {"neck": [dx, dy], "shoulder": [dx, dy], "head": [dx, dy]}} (part pixels, y down, added after detection).

Coordinates: rig units = part pixels / 100, origin at the neck's x and the torso's bottom, y up; sprite positions are
sprite centres (sprites pivot at their centre). armLocal is in the arm's own unscaled sprite units from the shoulder ball
centre. height = head top - torso bottom (CritterRig scales every look by the base look's height).

usage: python Tools/art/fit_spirit_parts.py [id ...]          (default: every id that has parts)
       python Tools/art/fit_spirit_parts.py --preview [id ...] (Logs/Screens/spirit_fit_<id>.png + spirit_joints_<id>.png)
"""
import glob, json, math, os, sys
import cv2
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_spirit_faces as FACES      # eye finder (the head's neck stub is searched under the eyes)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "Assets/Resources/Characters")
OUT = os.path.join(ROOT, "Assets/Resources/Rig/spirit_rig.json")
FIX = os.path.join(ROOT, "Tools/art/spirit_rig_fix.json")
PPU = 100.0
LOOKS = ["", "_pick", "_climb", "_carry", "_skin"]
OUTFITS = ("_pick", "_climb", "_carry")
PREVIEW_FACE = {"_pick": "happy", "_climb": "effort", "_carry": "tired"}   # preview only: the face each job would show


# ---------- image helpers ----------
def part_path(name):
    return os.path.join(ART, "Spirits", name + ".png")


def rgba(name):
    return np.asarray(Image.open(part_path(name)).convert("RGBA")).astype(np.float32)


def bbox(a):
    ys, xs = np.nonzero(a)
    return xs.min(), ys.min(), xs.max(), ys.max()


def runs(row):
    d = np.diff(np.concatenate([[0], row.astype(np.int8), [0]]))
    return list(zip(np.nonzero(d == 1)[0], np.nonzero(d == -1)[0] - 1))


def biggest(a):
    lab, n = ndi.label(a)
    if n < 2:
        return a
    return lab == 1 + int(np.argmax(ndi.sum(a, lab, range(1, n + 1))))


def contour(a):
    cs, _ = cv2.findContours(a.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    return max(cs, key=len)[:, 0, :].astype(np.float64)


def fit_circle(pts):
    """Least-squares circle (Kasa) → (cx, cy, r)."""
    x, y = pts[:, 0], pts[:, 1]
    A = np.c_[2 * x, 2 * y, np.ones(len(x))]
    b = x * x + y * y
    (cx, cy, c), *_ = np.linalg.lstsq(A, b, rcond=None)
    return float(cx), float(cy), float(math.sqrt(max(c + cx * cx + cy * cy, 1)))


# ---------- joints ----------
def stub_down(body):
    """Neck stub at the top of a torso: (cx, cy, width, top, bottom)."""
    x0, y0, x1, y1 = bbox(body)
    H = y1 - y0 + 1
    s, e = max(runs(body[y0 + 2]), key=lambda r: r[1] - r[0])
    w = e - s + 1
    cx = (s + e) / 2
    y = y0 + 2
    while y < y0 + H * 0.15:
        rs = [r for r in runs(body[y]) if r[0] <= cx <= r[1]]
        if not rs or rs[0][1] - rs[0][0] + 1 > w * 1.35:
            break
        y += 1
    return float(cx), float((y0 + y) / 2), float(w), float(y0), float(y)


def stub_colour(img, body, stub):
    cx, _, w, top, bottom = stub
    patch = img[int(top) + 1:int(bottom) + 1, int(cx - w / 3):int(cx + w / 3) + 1, :3].reshape(-1, 3)
    return np.median(patch, 0)


def skin_body(img, col, w0):
    """Outfit torso: the bare-skin piece holding the neck stub = the first skin-coloured narrow run from the top that
    sits near the middle of the body colour and goes on down (ladders, ropes, bundles and baskets are other colours;
    mushroom stems that happen to be stone-coloured hang off to one side). None if not found."""
    near = (img[:, :, 3] > 100) & (np.sqrt(((img[:, :, :3] - col) ** 2).sum(2)) < 40)
    near = cv2.morphologyEx(near.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
    if not near.any():
        return None
    lab, n = ndi.label(near)
    mid = np.nonzero(near)[1].mean()
    ys, _ = np.nonzero(img[:, :, 3] > 100)
    xs = np.nonzero(img[:, :, 3] > 100)[1]
    width, top0, H = np.ptp(xs), ys.min(), np.ptp(ys)
    for y in range(top0, top0 + int(H * 0.3)):
        for a, b in runs(near[y]):
            if 0.5 * w0 <= b - a + 1 <= 1.8 * w0 and abs((a + b) / 2 - mid) < 0.3 * width and near[y + 4, a:b + 1].mean() > 0.8:
                return lab == lab[y, (a + b) // 2]
    return None


def shoulder_nub(body, stub, side=-1):
    """Shoulder nub on one side (side -1 = viewer's left, +1 = right) as a circle (cx, cy, r).
    The nub is the outermost bump at the body's upper corner: the outline point farthest out in the up-and-sideways
    direction (below the neck stub, in the upper part of the body) is the top of the nub, and a circle fitted to the
    outline around it gives the nub's centre and radius. Torsos are drawn in three-quarter view, so each side is found
    on its own."""
    cx, cyn, w, top, bottom = stub
    x0, y0, x1, y1 = bbox(body)
    H = y1 - y0 + 1
    # smooth away thin things that stick out (lightning spikes, leaves, flame-tail tips): a round nub survives
    k = max(3, int(w * 0.6)) | 1
    smooth = cv2.morphologyEx(body.astype(np.uint8), cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))) > 0
    smooth = biggest(smooth) if smooth.any() else body
    pts = contour(smooth)
    up = pts[(pts[:, 1] > top + 2) & (pts[:, 1] < bottom + H * 0.25) & ((pts[:, 0] - cx) * side > w * 0.55)
             & (np.abs(pts[:, 0] - cx) < w * 2.6)]
    if len(up) < 8:
        return None
    score = (up[:, 0] - cx) * side - 0.45 * (up[:, 1] - top)
    ex, ey = up[int(np.argmax(score))]
    r0 = max(w * 0.6, H * 0.09)          # a nub is about this big; smaller fits are the neck notch's curve
    r, c = r0, None
    for _ in range(5):
        near = pts[np.hypot(pts[:, 0] - ex, pts[:, 1] - ey) < r * 1.4]
        if len(near) < 8:
            break
        fx, fy, fr = fit_circle(near)
        if not (0.5 * r0 < fr < 2.2 * r0) or (fx - cx) * side < w * 0.2 or not smooth[int(np.clip(fy, 0, H * 4)), int(np.clip(fx, 0, smooth.shape[1] - 1))]:
            break
        c, r = (fx, fy), fr
    if c is None:
        return None
    return float(c[0]), float(c[1]), float(r)


def head_stub(a, img, col, w0, eyes=None):
    """Neck stub under the chin: the lowest narrow skin-coloured run below the eyes and between them → (cx, cy).
    (Long hair can hang below the chin with tips as light as the skin, so the search stays under the face.)
    Falls back to the bottom centre."""
    x0, y0, x1, y1 = bbox(a)
    H = y1 - y0 + 1
    skin = a & (np.sqrt(((img[:, :, :3] - col) ** 2).sum(2)) < 55)
    lo, xa, xb = y0 + int(H * 0.45), x0, x1
    if eyes is not None:
        (lx, ly, _), (rx, ry, _) = eyes
        d = rx - lx
        lo, xa, xb = int(max(ly, ry) + d * 0.3), lx - d * 0.35, rx + d * 0.35
    mid = (xa + xb) / 2
    best = None      # the stub sits right under the mouth: of the candidate runs, the one nearest below the eyes' middle
    for y in range(y1, lo, -1):
        for s, e in runs(skin[y]):
            if not xa <= (s + e) / 2 <= xb:
                continue
            if 0.5 * w0 <= e - s + 1 <= 2.0 * w0 and skin[y - 4, s:e + 1].mean() > 0.7:
                sc = abs((s + e) / 2 - mid) * 2 - (y - lo) * 0.3
                if best is None or sc < best[0]:
                    best = (sc, y, s, e)
    if best is not None:
        _, y, s, e = best
        w, cx, top = e - s + 1, (s + e) / 2, y
        while top > y - H * 0.15:          # up the stub to where it widens into the chin
            rs = [r for r in runs(skin[top - 1]) if r[0] <= cx <= r[1]]
            if not rs or rs[0][1] - rs[0][0] + 1 > w * 1.4:
                break
            top -= 1
        return float(cx), float((top + y) / 2)
    if eyes is not None:     # no stub found: straight below the eyes, at the bottom of the head there
        mx = (eyes[0][0] + eyes[1][0]) / 2
        col_ = np.nonzero(a[:, int(mx)])[0]
        return float(mx), float(col_.max() - H * 0.04)
    xs = np.nonzero(a[y1 - max(2, int(H * 0.04)):y1 + 1])[1]
    return float(xs.mean()), float(y1 - H * 0.04)


def arm_joints(a):
    """Shoulder ball (cx, cy, r) = circle fitted to the arm's top cap; hand = centroid of the bottom 30%;
    tilt = degrees (CCW) that turn the shoulder→hand line straight down."""
    x0, y0, x1, y1 = bbox(a)
    H = y1 - y0 + 1
    ty, tx = np.nonzero(a[y0:y0 + max(2, int(H * 0.25))])
    by, bx = np.nonzero(a[y1 - int(H * 0.3):y1 + 1])
    top = np.array([tx.mean(), y0 + ty.mean()])
    hand = np.array([bx.mean(), y1 - int(H * 0.3) + by.mean()])
    axis = (hand - top) / np.linalg.norm(hand - top)
    pts = contour(a)
    t = (pts - top) @ axis
    side = (pts - top) @ np.array([-axis[1], axis[0]])
    tmin = t.min()
    near = np.abs(t - (tmin + (t.max() - tmin) * 0.2)) < 4
    r0 = (side[near].max() - side[near].min()) / 2 if near.sum() > 2 else H * 0.1
    cap = pts[t < tmin + r0 * 1.2]
    cx, cy, r = fit_circle(cap) if len(cap) >= 8 else (top[0], top[1], r0)
    if not a[int(np.clip(cy, 0, a.shape[0] - 1)), int(np.clip(cx, 0, a.shape[1] - 1))] or not 0.5 * r0 < r < 1.8 * r0:
        cx, cy, r = float(top[0] - axis[0] * 0), float(tmin * 0 + y0 + r0), float(r0)   # cap centre fallback
        cx = float(np.nonzero(a[int(cy)])[0].mean())
    vx, vy = hand[0] - cx, -(hand[1] - cy)
    tilt = -math.degrees(math.atan2(vx, -vy))
    return (cx, cy, r), (float(hand[0]), float(hand[1])), round(tilt, 1)


def split_arm(name, ball, hand):
    """Cut the arm at the elbow into <name>_up / <name>_lo (same canvas). Returns the elbow (x, y) in arm pixels."""
    im = np.asarray(Image.open(part_path(name)).convert("RGBA"))
    a = biggest(im[:, :, 3] > 100)
    j, h = np.array(ball[:2]), np.array(hand)
    axis = (h - j) / np.linalg.norm(h - j)
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    t = (xx - j[0]) * axis[0] + (yy - j[1]) * axis[1]
    tmax = t[a].max()
    te = tmax * 0.5
    band = a & (np.abs(t - te) < max(3, tmax * 0.04))
    ey, ex = np.nonzero(band)
    elbow = np.array([ex.mean(), ey.mean()])
    side = (xx - elbow[0]) * -axis[1] + (yy - elbow[1]) * axis[0]
    r = (side[band].max() - side[band].min()) / 2
    disc = (xx - elbow[0]) ** 2 + (yy - elbow[1]) ** 2 <= r * r
    for suf, keep in (("_up", (t <= te) | disc), ("_lo", (t >= te) | disc)):
        out = im.copy()
        out[:, :, 3] = np.where(keep & a, im[:, :, 3], 0)
        Image.fromarray(out).save(part_path(name + suf))
    return float(elbow[0]), float(elbow[1]), float(r)


# ---------- assembly ----------
def names_for(kid, look):
    skin = look == "_skin"
    return {"head": f"p_{kid}_head" + ("_skin" if skin else ""),
            "torso": f"p_{kid}_torso" + look,
            "arm": f"p_{kid}_arm" + ("_skin" if skin else "")}


def assemble(kid, look, base, fix):
    """base = the bare torso's joints {stub, nub, col} (None while doing the bare look itself)."""
    names = names_for(kid, look)
    if not all(os.path.exists(part_path(n)) for n in names.values()):
        return None
    f = fix.get(kid + look, {})
    add = lambda p, key: (p[0] + f.get(key, [0, 0])[0], p[1] + f.get(key, [0, 0])[1]) + tuple(p[2:])

    T = rgba(names["torso"])
    ta = biggest(T[:, :, 3] > 100)
    body = ta
    if look in OUTFITS and base is not None:
        b = skin_body(T, base["col"], base["stub"][2])
        if b is not None:
            body = b
    stub = stub_down(body)
    col = stub_colour(T, body, stub)
    nubs = {}
    manual = fix.get("nubs", {}).get(kid)
    for side in (-1, 1):
        if manual and base is None:
            nubs[side] = tuple(float(v) for v in manual["L" if side < 0 else "R"])   # read off the bare torso by eye
            continue
        nub = None if manual else shoulder_nub(body, stub, side)
        if base is not None:
            # a strap or a basket can cut the nub off the bare skin: keep the base nub (same offset from the stub, scaled)
            bs, bn = base["stub"], base["nubs"][side]
            k = 1.0       # one sheet = one scale; the stub width varies too much (collars, towels) to scale by it
            guess = (stub[0] + (bn[0] - bs[0]) * k, stub[1] + (bn[1] - bs[1]) * k, bn[2] * k)
            if nub is None or abs(nub[2] / guess[2] - 1) > 0.4 or math.hypot(nub[0] - guess[0], nub[1] - guess[1]) > guess[2] * 0.8:
                nub = guess
        elif nub is None:
            nub = (stub[0] + side * stub[2] * 1.1, stub[4] + stub[2] * 0.4, stub[2] * 0.55)   # last resort
        nubs[side] = nub
    if "neckAt" in f:                                  # read by eye where the stub is hidden (props over the neck)
        nx, ny = f["neckAt"]
        stub = (float(nx), float(ny), stub[2], float(ny) - stub[2] * 0.3, float(ny) + stub[2] * 0.3)
        if base is not None:
            bs = base["stub"]
            for side in (-1, 1):
                bn = base["nubs"][side]
                nubs[side] = (stub[0] + bn[0] - bs[0], stub[1] + bn[1] - bs[1], bn[2])
    neck = add(stub[:2], "neck")
    nubs[-1] = add(nubs[-1], "shoulder")
    nubs[1] = add(nubs[1], "shoulderR")

    Hd = rgba(names["head"])
    ha = biggest(Hd[:, :, 3] > 100)
    hcol = col if look == "_skin" or base is None else base["col"]
    hint = fix.get("eyes", {}).get(names["head"])
    eyes = (tuple(sorted((float(x), float(y), abs(hint[1][0] - hint[0][0]) * 0.15) for x, y in hint)) if hint
            else FACES.find_eyes(Hd))
    if eyes is not None:
        hcol = FACES.skin_near_eyes(Hd, eyes)        # the head's own skin (a little off from the torso's)
    hn = add(head_stub(ha, Hd, hcol, stub[2], eyes), "head")

    Ar = rgba(names["arm"])
    aa = biggest(Ar[:, :, 3] > 100)
    ball, hand, tilt = arm_joints(aa)
    el = split_arm(names["arm"], ball, hand)
    kfix = fix.get(kid, {})
    # one arm size per spirit, from the bare look (outfit shoulders are often hidden under straps)
    if base is None:
        nub_r = (nubs[-1][2] + nubs[1][2]) / 2
        scale = float(np.clip(nub_r / ball[2], 0.6, 1.4)) * kfix.get("armScale", 1.0)
    else:
        scale = base["armScale"]

    tH, tW = T.shape[:2]
    bottom = bbox(ta)[3]
    rig = lambda px, py: [round((px - neck[0]) / PPU, 4), round((bottom - py) / PPU, 4)]
    res = {"id": kid + look}
    res["torso"] = dict(zip(("x", "y"), rig(tW / 2, tH / 2)), sprite=names["torso"])
    hH, hW = Hd.shape[:2]
    res["head"] = dict(zip(("x", "y"), rig(neck[0] + (hW / 2 - hn[0]), neck[1] + (hH / 2 - hn[1]))), sprite=names["head"])
    skin = "_skin" if look == "_skin" else ""
    if os.path.exists(part_path(names["head"] + "_bare")) and os.path.exists(part_path(f"p_{kid}_face{skin}_normal")):
        res["head"]["bare"] = names["head"] + "_bare"     # expressions: bare head + face layer on the head's own canvas
        res["face"] = f"p_{kid}_face{skin}_"
        fa = rgba(f"p_{kid}_face{skin}_normal")[:, :, 3] > 100
        fx0, fy0, fx1, fy1 = bbox(fa)
        # the eyes-and-mouth box from the head sprite's centre (rig units, y up): hats sit on it
        res["faceBox"] = [round(((fx0 + fx1) / 2 - hW / 2) / PPU, 4), round((hH / 2 - (fy0 + fy1) / 2) / PPU, 4),
                          round((fx1 - fx0) / PPU, 4), round((fy1 - fy0) / PPU, 4)]
    aH, aW = Ar.shape[:2]
    loc = lambda px, py: [round((px - ball[0]) / PPU, 4), round((ball[1] - py) / PPU, 4)]
    res["arm"] = {"sprite": names["arm"], "up": names["arm"] + "_up", "lo": names["arm"] + "_lo"}
    res["armLocal"] = {"center": loc(aW / 2, aH / 2), "elbow": loc(el[0], el[1])}
    res["neck"] = rig(*neck)
    res["shoulderL"] = rig(nubs[-1][0], nubs[-1][1])      # front arm (viewer's left)
    res["shoulderR"] = rig(nubs[1][0], nubs[1][1])        # far arm (viewer's right, mirrored sprite, behind the torso)
    res["armScale"] = round(scale, 3)
    res["armTilt"] = tilt                              # turns the arm about the shoulder so it hangs straight down
    res["armRest"] = kfix.get("armRest", 18)          # then this far outward (degrees)
    head_top = neck[1] - (hn[1] - bbox(ha)[1])
    res["height"] = round((bottom - min(head_top, bbox(ta)[1])) / PPU, 4)
    dbg = {"torso": (names["torso"], stub, nubs), "head": (names["head"], hn, eyes), "arm": (names["arm"], ball, el, hand)}
    joints = {"stub": stub, "nubs": nubs, "col": col, "armScale": scale}
    return res, joints, dbg


# ---------- previews ----------
def rot(v, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return v[0] * c - v[1] * s, v[0] * s + v[1] * c


def preview(kid, data, dbgs):
    os.makedirs(os.path.join(ROOT, "Logs/Screens"), exist_ok=True)
    tiles = []
    for look in LOOKS:
        d = data.get(kid + look)
        if d is None:
            continue
        whole_p = os.path.join(ART, "Kids", kid + look + ".png")
        S = 520
        canvas = Image.new("RGBA", (S * 3, S), (40, 50, 70, 255))
        if os.path.exists(whole_p):
            whole = Image.open(whole_p).convert("RGBA")
            k = min(S * 0.9 / whole.height, S * 0.95 / whole.width)
            w = whole.resize((int(whole.width * k), int(whole.height * k)), Image.LANCZOS)
            canvas.alpha_composite(w, ((S - w.width) // 2, S - w.height - 10))
        k = S * 0.9 / (d["height"] * PPU)

        def draw(ox, oy, sprite, x, y, deg=0.0, s=1.0, flip=False):
            im = Image.open(part_path(sprite)).convert("RGBA")
            im = im.resize((max(1, int(im.width * k * s)), max(1, int(im.height * k * s))), Image.LANCZOS)
            if deg:
                im = im.rotate(deg, resample=Image.BICUBIC, expand=True)
            if flip:
                im = im.transpose(Image.FLIP_LEFT_RIGHT)
            canvas.alpha_composite(im, (int(ox + x * PPU * k - im.width / 2), int(oy - y * PPU * k - im.height / 2)))

        def arm(ox, oy, flip, raise_, bend):
            sc, L = d["armScale"], d["armLocal"]
            sh = (-d["shoulderR"][0], d["shoulderR"][1]) if flip else d["shoulderL"]   # mirrored space for the far arm
            a1 = d["armTilt"] - d["armRest"] - raise_
            a2 = a1 + bend
            c1 = rot((L["center"][0] * sc, L["center"][1] * sc), a1)
            e = rot((L["elbow"][0] * sc, L["elbow"][1] * sc), a1)
            c2 = rot(((L["center"][0] - L["elbow"][0]) * sc, (L["center"][1] - L["elbow"][1]) * sc), a2)
            f = -1 if flip else 1
            draw(ox, oy, d["arm"]["up"], f * (sh[0] + c1[0]), sh[1] + c1[1], f * a1, sc, flip)
            draw(ox, oy, d["arm"]["lo"], f * (sh[0] + e[0] + c2[0]), sh[1] + e[1] + c2[1], f * a2, sc, flip)

        for panel, (raise_, bend, face) in enumerate([(0, 12, PREVIEW_FACE.get(look, "normal")), (55, 75, "effort")]):
            ox, oy = S * (panel + 1) + S / 2, S - 10
            arm(ox, oy, True, raise_, bend)
            draw(ox, oy, d["torso"]["sprite"], d["torso"]["x"], d["torso"]["y"])
            arm(ox, oy, False, raise_, bend)
            hd = d["head"]
            if "face" in d:
                draw(ox, oy, hd["bare"], hd["x"], hd["y"])
                draw(ox, oy, d["face"] + (face if os.path.exists(part_path(d["face"] + face)) else "normal"), hd["x"], hd["y"])
            else:
                draw(ox, oy, hd["sprite"], hd["x"], hd["y"])
        tiles.append(canvas)
    if tiles:
        sheet = Image.new("RGBA", (tiles[0].width, sum(t.height for t in tiles)), (40, 50, 70, 255)); y = 0
        for t in tiles:
            sheet.alpha_composite(t, (0, y)); y += t.height
        out = os.path.join(ROOT, f"Logs/Screens/spirit_fit_{kid}.png")
        sheet.save(out)
        print("preview", out)
    # joints drawn on the parts themselves: torso stub (box) + nub circle, head stub point, arm ball + elbow + hand
    rows = []
    for look in LOOKS:
        g = dbgs.get(kid + look)
        if g is None:
            continue
        cells = []
        for key in ("torso", "head", "arm"):
            name = g[key][0]
            im = Image.open(part_path(name)).convert("RGBA")
            bg = Image.new("RGBA", im.size, (40, 50, 70, 255)); bg.alpha_composite(im)
            dr = ImageDraw.Draw(bg)
            if key == "torso":
                (cx, cy, w, top, bot), nubs = g[key][1], g[key][2]
                dr.rectangle([cx - w / 2, top, cx + w / 2, bot], outline=(0, 255, 0, 255), width=2)
                dr.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(0, 255, 0, 255))
                for nx, ny, nr in nubs.values():
                    dr.ellipse([nx - nr, ny - nr, nx + nr, ny + nr], outline=(255, 0, 255, 255), width=2)
                    dr.ellipse([nx - 3, ny - 3, nx + 3, ny + 3], fill=(255, 0, 255, 255))
            elif key == "head":
                hx, hy = g[key][1][:2]
                dr.ellipse([hx - 5, hy - 5, hx + 5, hy + 5], fill=(0, 255, 0, 255))
                for ex, ey, es in (g[key][2] or ()):
                    dr.ellipse([ex - es, ey - es, ex + es, ey + es], outline=(255, 220, 0, 255), width=2)
            else:
                (bx, by, br), (ex, ey, er), (hx, hy) = g[key][1], g[key][2], g[key][3]
                dr.ellipse([bx - br, by - br, bx + br, by + br], outline=(255, 0, 255, 255), width=2)
                dr.ellipse([ex - er, ey - er, ex + er, ey + er], outline=(0, 200, 255, 255), width=2)
                dr.ellipse([hx - 4, hy - 4, hx + 4, hy + 4], fill=(255, 220, 0, 255))
            cells.append(bg)
        row = Image.new("RGBA", (sum(c.width for c in cells) + 20, max(c.height for c in cells)), (20, 20, 20, 255)); x = 0
        for c in cells:
            row.paste(c, (x, 0)); x += c.width + 10
        rows.append(row)
    if rows:
        sheet = Image.new("RGBA", (max(r.width for r in rows), sum(r.height for r in rows)), (20, 20, 20, 255)); y = 0
        for r in rows:
            sheet.paste(r, (0, y)); y += r.height
        out = os.path.join(ROOT, f"Logs/Screens/spirit_joints_{kid}.png")
        sheet.save(out)
        print("joints", out)


def main():
    args = sys.argv[1:]
    want_preview = "--preview" in args
    args = [a for a in args if a != "--preview"]
    kids = args or sorted({os.path.basename(p).split("_")[1] for p in glob.glob(os.path.join(ART, "Spirits", "p_*_head.png"))})
    fix = json.load(open(FIX, encoding="utf-8")) if os.path.exists(FIX) else {}
    data = {}
    if os.path.exists(OUT):
        data = {k["id"]: k for k in json.load(open(OUT, encoding="utf-8"))["looks"]}
    for kid in kids:
        base, dbgs = None, {}
        for look in LOOKS:
            r = assemble(kid, look, base, fix)
            if r is None:
                continue
            res, joints, dbg = r
            data[kid + look], dbgs[kid + look] = res, dbg
            if look == "":
                base = joints
            print(kid + look, "L", res["shoulderL"], "R", res["shoulderR"], "armScale", res["armScale"])
        if want_preview:
            preview(kid, data, dbgs)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump({"looks": [data[k] for k in sorted(data)]}, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
