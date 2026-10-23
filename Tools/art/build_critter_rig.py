"""Lay out each critter's separately drawn parts by its body structure and write the rig layout.

Inputs (Assets/Resources/Characters/Critters), all cut from one parts sheet row so they share one pixel scale:
  cap_<id>, body_<id>, blink_<id>, arm_<id>, foot_<id>.png   (critter_rig_a|b sheets)
  body_<id>_closed.png   optional second body with the eyes closed (critter_closed_a|b), aligned here onto body_<id>
  <id>_ref.png           the design drawing, only for the debug overlay
Output: Assets/Resources/Rig/critter_rig.json read by CritterRig (rig units: the critter is 1.0 tall, feet line at
y = -0.5, x = 0 at the body centre; part x/y = sprite centre, s = localScale for a 100 px-per-unit sprite).

Structure (user direction 2026-10-15): the body is a round cylinder that goes into the cap like a stem. Each cap has
the place where its stem would go (a pointed, rounded mound drawn on the gills) cut out along a hand-traced outline
(OPENING), so the body shows through it and its top edge hides under the shell; the gills at the sides and the front
lip stay. Flat caps (wind, coin) just cover the top of the body.
Caps are kept uncut in Art/Raw_Dream/rig_src and cut again on every run. The closed-eye body is the open body with
only the face swapped in, so blinking never changes the outline. Arms hang in front of the body sides, feet peek out
under it.

Usage: python Tools/art/build_critter_rig.py [--debug <dir>] [ids...]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.signal import fftconvolve

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, "Assets/Resources/Characters/Critters")   # the art set that wins in SpriteDB
OUT = os.path.join(ROOT, "Assets/Resources/Rig/critter_rig.json")
IDS = ["fire", "spark", "dew", "coin", "star", "leaf", "moon", "wind", "rock", "chest"]
PPU = 100

# structure ratios (fractions of the body's opaque box unless noted)
SEAT = 0.03             # the body's top edge hides this far (x body height) behind the cap's shell (hat caps)
FLAT_COVER = 0.12        # a flat cap covers this much of the top of the body
SHOULDER_Y = 0.58        # shoulder height down the body
ARM_TUCK = 0.25          # this much of the arm's width overlaps the body edge (arms draw in front of the body)
ARM_SIZE = 0.8           # arms drawn on the parts sheet came out a little big for the cylinder body
FOOT_X = 0.26            # feet centres at +- this x torso width
FOOT_TUCK = 0.5          # this much of the foot's height is tucked under the body


# Where the body goes into each cap, traced by hand on the stemless cap_<id>.png (px, critter_cap sheets): up the
# drawn mound's left edge, along the shell's inner rim, down its right edge, then along the bottom (a front lip below
# stays). Automatic edge finding kept drifting into the shell or the gills, so these are fixed. Re-trace them if the
# critter_cap sheets are regenerated. The skin caps use the same outline mapped by their silhouette box.
OPENING = {
    "star":  [(123, 330), (136, 294), (150, 259), (180, 253), (218, 252), (250, 254), (278, 258), (295, 294), (311, 330), (218, 330)],
    "moon":  [(119, 250), (146, 214), (173, 181), (211, 181), (254, 189), (278, 220), (301, 250), (211, 251)],
    "chest": [(129, 285), (160, 245), (189, 209), (236, 205), (280, 206), (297, 210), (326, 248), (354, 285), (240, 287)],
    "dew":   [(118, 310), (132, 262), (145, 236), (177, 240), (207, 244), (230, 233), (257, 220), (283, 226), (313, 246), (327, 270), (340, 310)],
    "leaf":  [(165, 290), (174, 250), (183, 238), (207, 213), (236, 197), (266, 212), (290, 224), (308, 232), (318, 258), (330, 300)],
    "fire":  [(108, 310), (120, 270), (132, 251), (152, 244), (175, 239), (202, 240), (225, 245), (252, 257), (278, 267), (305, 274), (328, 277), (338, 310)],
    "rock":  [(134, 300), (142, 261), (152, 245), (174, 230), (204, 220), (241, 215), (281, 215), (314, 222), (341, 233), (357, 248), (369, 268), (372, 300)],
    "spark": [(149, 330), (150, 294), (170, 265), (190, 239), (237, 234), (285, 239), (306, 267), (327, 296), (328, 330), (237, 330)],   # through the thin front lip (its edge line crossed the face)
}


def catmull(pts, n=12):
    """closed Catmull-Rom curve through the points"""
    P = np.array(pts, float); out = []
    for i in range(len(P)):
        p0, p1, p2, p3 = P[i - 1], P[i], P[(i + 1) % len(P)], P[(i + 2) % len(P)]
        for t in np.linspace(0, 1, n, endpoint=False):
            out.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    return np.array(out)


def silhouette_box(a):
    ys, xs = np.nonzero(a[:, :, 3] > 0.5)
    return xs.min(), ys.min(), xs.max(), ys.max()


def opening(cid, cap, target):
    """the traced opening of cap_<id> mapped onto `target` (the cap itself or its skin), as points"""
    c, t = silhouette_box(cap), silhouette_box(target)
    kx, ky = (t[2] - t[0]) / (c[2] - c[0]), (t[3] - t[1]) / (c[3] - c[1])
    return [((x - c[0]) * kx + t[0], (y - c[1]) * ky + t[1]) for x, y in OPENING[cid]]


def cut_opening(src, pts):
    import cv2
    m = np.zeros(src.shape[:2], np.uint8)
    cv2.fillPoly(m, [np.round(catmull(pts) * 4).astype(np.int32)], 255, lineType=cv2.LINE_AA, shift=2)
    out = src.copy()
    out[:, :, 3] = src[:, :, 3] * (1 - m.astype(np.float32) / 255)
    return out


def load(name, cid):
    p = os.path.join(DIR, f"{name}_{cid}.png")
    return np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) / 255 if os.path.exists(p) else None


def box(a, t=0.5):
    ys, xs = np.nonzero(a[:, :, 3] > t)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def find(ref, part, scales):
    """Best (score, scale, x, y) top-left placement of `part` inside `ref` by masked colour SSD."""
    R = ref[:, :, :3] * ref[:, :, 3:4]
    best = None
    for s in scales:
        im = Image.fromarray((part * 255).astype(np.uint8), "RGBA")
        im = im.resize((max(2, round(im.width * s)), max(2, round(im.height * s))), Image.LANCZOS)
        p = np.asarray(im).astype(np.float32) / 255
        ph, pw = p.shape[:2]
        if ph > ref.shape[0] or pw > ref.shape[1]:
            continue
        m = (p[:, :, 3] > 0.5).astype(np.float32)
        P = p[:, :, :3] * p[:, :, 3:4]
        mf = m[::-1, ::-1]
        ssd = np.zeros((ref.shape[0] - ph + 1, ref.shape[1] - pw + 1), np.float32)
        for c in range(3):
            ssd += fftconvolve(R[:, :, c] ** 2, mf, mode="valid") - 2 * fftconvolve(R[:, :, c], (m * P[:, :, c])[::-1, ::-1], mode="valid") + (m * P[:, :, c] ** 2).sum()
        outside = m.sum() - fftconvolve(ref[:, :, 3], mf, mode="valid")
        score = (ssd + 1.5 * outside) / m.sum()
        iy, ix = np.unravel_index(np.argmin(score), score.shape)
        if best is None or score[iy, ix] < best[0]:
            best = (float(score[iy, ix]), s, int(ix), int(iy))
    return best


def orient_arm(cid):
    """The rig wants the viewer-left arm: shoulder at its top right, hand hanging out to the left. Flip the png if
    it was drawn the other way (idempotent: a correct png is left alone)."""
    path = os.path.join(DIR, f"arm_{cid}.png")
    a = np.asarray(Image.open(path).convert("RGBA"))[:, :, 3] > 128
    ys, xs = np.nonzero(a)
    top, bot = ys < np.percentile(ys, 25), ys > np.percentile(ys, 75)
    if xs[top].mean() < xs[bot].mean():
        Image.open(path).transpose(Image.FLIP_LEFT_RIGHT).save(path)


def align_closed(cid, body):
    """body_<id>_closed = body_<id> with only the face taken from the closed-eye drawing, so blinking never changes
    the outline or size. The closed drawing is aligned onto the open one (silhouette match), the pixels where the two
    faces differ (eyes, mouth) are swapped in with a soft edge, and their skin tone is matched to the open body."""
    if not os.path.exists(os.path.join(DIR, f"body_{cid}_closed.png")) and not os.path.exists(os.path.join(SRC, f"body_{cid}_closed.png")):
        return False
    closed = source("body", f"{cid}_closed")
    pad = 40
    ref = np.zeros((body.shape[0] + 2 * pad, body.shape[1] + 2 * pad, 4), np.float32); ref[pad:-pad, pad:-pad] = body
    sil = closed.copy(); sil[:, :, :3] = 0.5
    ref_sil = ref.copy(); ref_sil[:, :, :3] = 0.5 * ref_sil[:, :, 3:4]
    kw = (box(body)[2] - box(body)[0]) / (box(closed)[2] - box(closed)[0])
    r = find(ref_sil, sil, np.linspace(kw * 0.97, kw * 1.03, 13))
    if r is None:
        return False
    _, sc, x, y = r
    im = Image.fromarray((closed * 255).astype(np.uint8), "RGBA")
    im = im.resize((round(im.width * sc), round(im.height * sc)), Image.LANCZOS)
    big = Image.new("RGBA", (body.shape[1] + 2 * pad, body.shape[0] + 2 * pad), (0, 0, 0, 0))
    big.paste(im, (x, y), im)
    cl = np.asarray(big).astype(np.float32)[pad:-pad, pad:-pad] / 255

    both = (body[:, :, 3] > 0.5) & (cl[:, :, 3] > 0.5)
    inside = ndimage.binary_erosion(both, iterations=8)
    diff = (np.abs(body[:, :, :3] - cl[:, :, :3]).sum(2) > 0.3) & inside
    diff = ndimage.binary_opening(diff, iterations=1)
    face = ndimage.binary_dilation(diff, iterations=6) & ndimage.binary_erosion(both, iterations=4)
    ring = ndimage.binary_dilation(face, iterations=8) & ~ndimage.binary_dilation(face, iterations=2) & inside
    off = body[ring][:, :3].mean(0) - cl[ring][:, :3].mean(0) if ring.any() else np.zeros(3)
    m = ndimage.gaussian_filter(face.astype(np.float32), 1.6)[..., None]
    out = body.copy()
    out[:, :, :3] = body[:, :, :3] * (1 - m) + np.clip(cl[:, :, :3] + off, 0, 1) * m
    save(out, "body", f"{cid}_closed")
    return True


def torso_width(a):
    """Median opaque width over 55-85 % of the body height: the body itself, not a scarf tail or cape flare."""
    x0, y0, x1, y1 = box(a)
    ws = []
    for y in range(int(y0 + 0.55 * (y1 - y0)), int(y0 + 0.85 * (y1 - y0))):
        xs = np.nonzero(a[y, :, 3] > 0.5)[0]
        if len(xs):
            ws.append((xs.max() - xs.min(), (xs.max() + xs.min()) / 2))
    ws.sort()
    return ws[len(ws) // 2]


def scaled(a, k):
    im = Image.fromarray((a * 255).astype(np.uint8), "RGBA")
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255


SRC = os.path.join(ROOT, "Art/Raw_Dream/rig_src")   # unseated caps (the sliced originals)


def source(name, cid):
    """The unseated cap / skin png (copied from the sliced one the first time)."""
    os.makedirs(SRC, exist_ok=True)
    src, cur = os.path.join(SRC, f"{name}_{cid}.png"), os.path.join(DIR, f"{name}_{cid}.png")
    if not os.path.exists(src):
        Image.open(cur).save(src)
    return np.asarray(Image.open(src).convert("RGBA")).astype(np.float32) / 255


def save(a, name, cid):
    Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(DIR, f"{name}_{cid}.png"))


def layout(cid):
    """Stack the parts on the cylinder body (body png pixel space), then normalise to rig units."""
    orient_arm(cid)
    body, arm, foot, old = (load(n, cid) for n in ("body", "arm", "foot", "rigbody"))
    cap = source("cap", cid)
    has_closed = align_closed(cid, body)
    closed = load("body", f"{cid}_closed") if has_closed else None
    bx0, by0, bx1, by1 = box(body)
    bh = by1 - by0
    tw, tcx = torso_width(body)
    # cap, arm and foot were drawn on the parts sheet next to `rigbody`; carry them over at the same relative size
    k = tw / torso_width(old)[0] if old is not None else 1.0
    # the stemless caps (critter_cap sheets) were drawn at their own scale: match the parts sheet's cap width
    ref_cap = load("rigcap", cid)
    kc = k * (box(ref_cap)[2] - box(ref_cap)[0]) / (box(cap)[2] - box(cap)[0]) if ref_cap is not None else k
    capk, armk, footk = scaled(cap, kc), scaled(arm, k * ARM_SIZE), scaled(foot, k)
    C = {"body": (body.shape[1] / 2, body.shape[0] / 2)}

    # eyes: where the open and closed bodies differ (else the upper third)
    eye_top = by0 + 0.35 * bh
    if closed is not None:
        # only well inside both bodies: a pixel of misalignment along the outline is not an eye
        inside = ndimage.binary_erosion((body[:, :, 3] > 0.5) & (closed[:, :, 3] > 0.5), iterations=10)
        d = (np.abs(body[:, :, :3] - closed[:, :, :3]).sum(2) > 0.35) & inside
        d = ndimage.binary_opening(d, iterations=2)
        d[int(by0 + 0.75 * bh):] = False
        ys = np.nonzero(d.any(1))[0]
        if len(ys):
            eye_top = ys.min()
    # the body goes into the cap through the traced opening: the cap sits so the opening's top (just under the shell)
    # is a little above the body's top edge, which therefore hides under the shell. Caps without an opening (wind,
    # coin: flat undersides) just cover the top of the body.
    capk = scaled(cap, kc)
    cx0, cy0, cx1, cy1 = box(capk)
    ccx = (cx0 + cx1) / 2
    if cid in OPENING:
        pts = np.array(OPENING[cid], float)
        ccx = (pts[:, 0].min() + pts[:, 0].max()) / 2 * kc      # centre the opening on the body
        anchor, joint = pts[:, 1].min() * kc, by0 + SEAT * bh
    else:
        col = capk[:, int(ccx - 0.05 * (cx1 - cx0)):int(ccx + 0.05 * (cx1 - cx0)), 3] > 0.5
        anchor, joint = np.nonzero(col.any(1))[0].max(), by0 + FLAT_COVER * bh
    C["cap"] = (tcx - (ccx - capk.shape[1] / 2), joint - anchor + capk.shape[0] / 2)
    for name in ("cap", "skin"):                          # the skin cap is drawn at the cap's size and uses its transform
        src = source(name, cid)
        if cid in OPENING:
            src = cut_opening(src, opening(cid, cap, src))
        save(src, name, cid)
        if name == "cap":
            capk = scaled(src, kc)

    # arm: its shoulder (centroid of the top fifth) goes onto the body's left edge at shoulder height
    m = armk[:, :, 3] > 0.5
    ys, xs = np.nonzero(m)
    top = ys < ys.min() + (ys.max() - ys.min()) * 0.2
    sx_l, sy_l = xs[top].mean(), ys[top].mean()
    sy = by0 + SHOULDER_Y * bh
    row = np.nonzero(body[int(sy), :, 3] > 0.5)[0]
    left = row.min() if len(row) else bx0
    shoulder = (left + ARM_TUCK * (xs.max() - xs.min()), sy)
    C["arm"] = (shoulder[0] - sx_l + armk.shape[1] / 2, shoulder[1] - sy_l + armk.shape[0] / 2)

    fx0, fy0, fx1, fy1 = box(footk)
    fh = fy1 - fy0
    fcx, fcy = tcx - FOOT_X * tw, by1 - FOOT_TUCK * fh + fh / 2
    C["foot"] = (fcx - ((fx0 + fx1) / 2 - footk.shape[1] / 2), fcy - ((fy0 + fy1) / 2 - footk.shape[0] / 2))

    # normalise: 1.0 tall from the cap top to the feet, feet line at -0.5, torso centre x = 0
    top_y = min(C["cap"][1] - capk.shape[0] / 2 + cy0, by0)
    bot_y = fcy + fh / 2
    H = bot_y - top_y
    U = lambda x, y: (round((x - tcx) / H, 4), round((bot_y - y) / H - 0.5, 4))
    entry = {"id": cid}
    for n in ("cap", "body", "arm", "foot"):
        x, y = U(*C[n])
        entry[n] = {"x": x, "y": y, "s": round(PPU / H * {"body": 1, "cap": kc, "arm": k * ARM_SIZE, "foot": k}[n], 4)}
    entry["blink"] = dict(entry["body"])          # unused when the closed body exists
    entry["shoulderX"], entry["shoulderY"] = U(*shoulder)
    entry["capJointY"] = U(0, joint)[1]           # the cap wobbles about where it sits on the body
    entry["closed"] = has_closed
    return entry, (capk, body, closed, armk, footk), C, (H, top_y)


ICON_PX = 300      # icon pixels per rig unit
ICON_UNITS = 1.4   # icon canvas spans -0.7..0.7 units around the rig origin (CritterIcon draws it at 1.4 * unitPx); room for skin accessories


def compose(e, cid, capkind):
    """One-piece UI icon of the critter in its rest pose: icon_<id>.png (own cap) / icon_<id>_skin.png (skin cap)."""
    size = int(ICON_PX * ICON_UNITS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    def put(name, p, x, mirror=False):
        path = os.path.join(DIR, f"{name}_{cid}.png")
        if not os.path.exists(path):
            return
        im = Image.open(path).convert("RGBA")
        f = p["s"] * ICON_PX / PPU
        im = im.resize((max(1, int(im.width * f)), max(1, int(im.height * f))), Image.LANCZOS)
        if mirror:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        canvas.alpha_composite(im, (int(size / 2 + x * ICON_PX - im.width / 2), int(size / 2 - p["y"] * ICON_PX - im.height / 2)))

    f, b, a, c = e["foot"], e["body"], e["arm"], e["cap"]
    put("foot", f, f["x"]); put("foot", f, -f["x"], True)
    put("body", b, b["x"])
    put("arm", a, a["x"]); put("arm", a, -a["x"], True)
    put(capkind, c, c["x"])
    canvas.save(os.path.join(DIR, f"icon_{cid}{'_skin' if capkind == 'skin' else ''}.png"))


def debug_image(cid, parts, C, H, dbg):
    """Rest pose (rig draw order) with eyes open and closed, next to the design drawing at the same height."""
    cap, body, closed, arm, foot = parts
    H, top_y = H
    size = int(H * 1.25)
    def pose(bod):
        cv = Image.new("RGBA", (size, size), (205, 228, 196, 255))
        ox, oy = size / 2 - C["body"][0], size * 0.1 - top_y
        def put(a, c, mirror=False):
            im = Image.fromarray((a * 255).astype(np.uint8), "RGBA")
            if mirror:
                im = im.transpose(Image.FLIP_LEFT_RIGHT)
            cv.alpha_composite(im, (int(ox + c[0] - im.width / 2), int(oy + c[1] - im.height / 2)))
        bcx = C["body"][0]
        put(foot, C["foot"]); put(foot, (2 * bcx - C["foot"][0], C["foot"][1]), True)
        put(bod, C["body"])
        put(arm, C["arm"]); put(arm, (2 * bcx - C["arm"][0], C["arm"][1]), True)
        put(cap, C["cap"])
        return cv
    out = Image.new("RGBA", (size * 3, size), (205, 228, 196, 255))
    out.alpha_composite(pose(body), (0, 0))
    if closed is not None:
        out.alpha_composite(pose(closed), (size, 0))
    ref_p = os.path.join(DIR, f"{cid}_ref.png")
    if os.path.exists(ref_p):
        r = Image.open(ref_p).convert("RGBA")
        k = size * 0.8 / r.height
        r = r.resize((int(r.width * k), int(r.height * k)), Image.LANCZOS)
        out.alpha_composite(r, (size * 2 + (size - r.width) // 2, int(size * 0.1)))
    out.save(os.path.join(dbg, f"rig_{cid}.png"))


def main():
    args = sys.argv[1:]
    dbg = None
    if "--debug" in args:
        i = args.index("--debug"); dbg = args[i + 1]; del args[i:i + 2]
        os.makedirs(dbg, exist_ok=True)
    ids = args or IDS
    old = {}
    if os.path.exists(OUT):
        for k in json.load(open(OUT, encoding="utf-8"))["kinds"]:
            old[k["id"]] = k
    for cid in ids:
        entry, parts, C, H = layout(cid)
        old[cid] = entry
        print(cid, "closed body" if entry["closed"] else "no closed body", json.dumps({k: entry[k] for k in ("cap", "body", "arm", "foot")}))
        if dbg:
            debug_image(cid, parts, C, H, dbg)
    for cid in ids:
        compose(old[cid], cid, "cap")
        compose(old[cid], cid, "skin")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump({"kinds": [old[i] for i in IDS if i in old]}, open(OUT, "w", encoding="utf-8"), indent=1)
    print("wrote", OUT, len(old))
    # skins with the accessory painted on: the pass above rewrote skin_<id> from rig_src, so lay them on again
    import build_skin_acc
    build_skin_acc.main(ids)


if __name__ == "__main__":
    main()
