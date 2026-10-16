"""Skin parts with the accessory painted on (user 2026-10-16): the skin swaps the rig part itself instead of stacking
a separate accessory sprite on it.

Input: Art/Raw_Dream/skinacc/<id>.png (sheet critter_skin_acc: each cell redraws the skin cap / body of the reference
sheet skin_acc_ref.png wearing its accessory). Each redraw is registered (scale + offset, masked colour match) back
onto the part it was drawn from, so the existing rig layout (critter_rig.json) still fits:
  caps   rig_src/skin_<id>.png (uncut)  ->  Assets/.../skin_<id>.png, socket cut again with the traced OPENING
  bodies body_<id>.png                  ->  Assets/.../body_<id>_skin.png + body_<id>_skin_closed.png
The canvas only grows evenly around the original (accessory overhang), so the part's centre never moves.
build_critter_rig.py calls this after its own pass (it rewrites skin_<id> from rig_src every run).

Usage: python Tools/art/build_skin_acc.py [ids...]
"""
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_critter_rig as R

GEN = os.path.join(R.ROOT, "Art/Raw_Dream/skinacc")
CAPS = ["fire", "spark", "coin", "star", "leaf", "wind", "rock", "chest"]
BODIES = {"dew": "dew_body", "moon": "moon_body"}
NO_BLINK = {"moon"}        # sunglasses hide the eyes: the closed body is the open one


def rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255


def register(orig, gen):
    """Place `gen` in `orig`'s pixel space: returns (gen resized, x, y) = its top-left in orig coords."""
    pad = max(gen.shape[:2])
    big = np.zeros((gen.shape[0] + 2 * pad, gen.shape[1] + 2 * pad, 4), np.float32)
    big[pad:-pad, pad:-pad] = gen
    ob, gb = R.box(orig), R.box(gen)
    k0 = (gb[2] - gb[0]) / (ob[2] - ob[0])                # orig -> gen scale guess (the accessory may widen gen)
    best = None
    for lo, hi in ((0.55, 1.15), (None, None)):
        scales = np.linspace(k0 * lo, k0 * hi, 25) if lo else np.linspace(best[1] * 0.97, best[1] * 1.03, 13)
        r = R.find(big, orig, scales)
        if r and (best is None or r[0] <= best[0]):
            best = r
    _, s, x, y = best                                     # orig scaled by s sits at (x, y) in big
    k = 1 / s
    im = Image.fromarray((gen * 255).astype(np.uint8), "RGBA")
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255, (pad - x) * k, (pad - y) * k


def on_canvas(orig, g, gx, gy):
    """Paste registered `g` on orig's canvas grown evenly to hold it; returns (canvas, pad x, pad y)."""
    h, w = orig.shape[:2]
    gb = R.box(g, 0.05)
    x0, y0, x1, y1 = gx + gb[0], gy + gb[1], gx + gb[2], gy + gb[3]
    px = int(np.ceil(max(0, -x0, x1 - w))) + 2
    py = int(np.ceil(max(0, -y0, y1 - h))) + 2
    cv = Image.new("RGBA", (w + 2 * px, h + 2 * py), (0, 0, 0, 0))
    gi = Image.fromarray((g * 255).astype(np.uint8), "RGBA")
    cv.alpha_composite(gi, (int(round(gx + px)), int(round(gy + py))))
    return np.asarray(cv).astype(np.float32) / 255, px, py


def clean(a):
    """Drop the faint speckle the generator leaves around shapes."""
    a = a.copy()
    a[a[:, :, 3] < 0.06] = 0
    return a


def cap(cid):
    orig = R.source("skin", cid)                          # uncut skin cap
    g, gx, gy = register(orig, clean(rgba(os.path.join(GEN, f"{cid}.png"))))
    out, px, py = on_canvas(orig, g, gx, gy)
    if cid in R.OPENING:
        # the same socket as the plain skin: traced outline mapped onto the uncut skin, moved by the padding
        pts = [(x + px, y + py) for x, y in R.opening(cid, R.source("cap", cid), orig)]
        out = R.cut_opening(out, pts)
    R.save(out, "skin", cid)


def body(cid):
    base = R.load("body", cid)
    g, gx, gy = register(base, clean(rgba(os.path.join(GEN, f"{BODIES[cid]}.png"))))
    out, px, py = on_canvas(base, g, gx, gy)
    R.save(out, "body", f"{cid}_skin")
    closed = R.load("body", f"{cid}_closed")
    if closed is None or cid in NO_BLINK:
        R.save(out, "body", f"{cid}_skin_closed")
        return
    # blink: where the plain open / closed bodies differ (eyes, mouth), take the closed face, on the padded canvas
    both = (base[:, :, 3] > 0.5) & (closed[:, :, 3] > 0.5)
    face = (np.abs(base[:, :, :3] - closed[:, :, :3]).sum(2) > 0.3) & ndimage.binary_erosion(both, iterations=8)
    face = ndimage.binary_dilation(ndimage.binary_opening(face, iterations=1), iterations=6)
    m = np.zeros(out.shape[:2], np.float32)
    m[py:py + base.shape[0], px:px + base.shape[1]] = ndimage.gaussian_filter(face.astype(np.float32), 1.6)
    cl = np.zeros_like(out); cl[py:py + base.shape[0], px:px + base.shape[1]] = closed
    res = out.copy()
    res[:, :, :3] = out[:, :, :3] * (1 - m[..., None]) + cl[:, :, :3] * m[..., None]
    R.save(res, "body", f"{cid}_skin_closed")


def compose_skin_icon(entry, cid):
    """icon_<id>_skin with the skin body when there is one (build_critter_rig.compose uses the plain body)."""
    skin_body = os.path.join(R.DIR, f"body_{cid}_skin.png")
    if not os.path.exists(skin_body):
        return
    plain = os.path.join(R.DIR, f"body_{cid}.png")
    tmp = plain + ".plain"
    os.replace(plain, tmp)
    try:
        Image.open(skin_body).save(plain)
        R.compose(entry, cid, "skin")
    finally:
        os.replace(tmp, plain)


def main(ids=None):
    import json
    ids = ids or R.IDS
    layout = {k["id"]: k for k in json.load(open(R.OUT, encoding="utf-8"))["kinds"]}
    for cid in ids:
        if cid in CAPS and os.path.exists(os.path.join(GEN, f"{cid}.png")):
            cap(cid); print(cid, "skin cap + accessory")
        if cid in BODIES and os.path.exists(os.path.join(GEN, f"{BODIES[cid]}.png")):
            body(cid); print(cid, "skin body + accessory")
        if cid in layout:
            R.compose(layout[cid], cid, "skin")
            compose_skin_icon(layout[cid], cid)


if __name__ == "__main__":
    main(sys.argv[1:])
