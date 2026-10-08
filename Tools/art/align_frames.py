"""Line up the two builder-critter frames (build_<id>_0 hammer raised, build_<id>_1 hammer down) so the critter's
body stays put when the game flips between them.

Frame 1 is matched onto frame 0 by the best overlap of their silhouettes (scale + offset search: the body and cap
are most of the picture, the hammer is the only part that moves). Both frames are then written onto one shared
canvas whose bottom edge is (about) the feet line and whose horizontal center is the feet center of frame 0, so the game
can draw them with a bottom-center pivot at the same size.

Run after slicing the critter_build_a / critter_build_b sheets:
    python Tools/art/align_frames.py
"""
import json
import os

import numpy as np
from PIL import Image
from scipy.signal import fftconvolve

DIR = "Assets/Art/Generated/Characters/Critters/Build"
IDS = ["fire", "spark", "dew", "coin", "star", "leaf", "moon", "wind", "rock", "chest"]
ALPHA = 128
WORK = 0.5          # search on half-size masks
PAD = 8


def mask(img, k=1.0):
    if k != 1.0:
        img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
    return (np.asarray(img)[:, :, 3] >= ALPHA).astype(np.float32)


def best_offset(a, b):
    """Offset (dx, dy) of b's top-left inside a's frame that maximizes overlap, and the IoU there."""
    corr = fftconvolve(a, b[::-1, ::-1], mode="full")
    y, x = np.unravel_index(np.argmax(corr), corr.shape)
    dy, dx = y - (b.shape[0] - 1), x - (b.shape[1] - 1)
    inter = corr[y, x]
    return dx, dy, inter / (a.sum() + b.sum() - inter)


def feet_center(m):
    rows = np.nonzero(m.any(axis=1))[0]
    bottom = rows[-1]
    band = m[max(0, bottom - int(m.shape[0] * 0.08)):bottom + 1]
    xs = np.nonzero(band.any(axis=0))[0]
    return (xs[0] + xs[-1]) / 2, bottom


def main():
    report = {}
    for cid in IDS:
        p0, p1 = os.path.join(DIR, f"build_{cid}_0.png"), os.path.join(DIR, f"build_{cid}_1.png")
        if not (os.path.exists(p0) and os.path.exists(p1)):
            report[cid] = "missing"; continue
        f0, f1 = Image.open(p0).convert("RGBA"), Image.open(p1).convert("RGBA")
        a = mask(f0, WORK)
        best = None
        for s in np.arange(0.80, 1.21, 0.02):
            b = mask(f1, WORK * s)
            dx, dy, iou = best_offset(a, b)
            if best is None or iou > best[3]: best = (s, dx / WORK, dy / WORK, iou)
        s, dx, dy, iou = best
        f1 = f1.resize((round(f1.width * s), round(f1.height * s)), Image.LANCZOS)

        # frame 1's top-left sits at (dx, dy) in frame 0's pixels; build a canvas holding both
        fx, fy = feet_center(mask(f0))
        x0, y0 = min(0, dx), min(0, dy)
        x1, y1 = max(f0.width, dx + f1.width), max(fy + 1, dy + f1.height)
        half = max(fx - x0, x1 - fx) + PAD
        top = y0 - PAD
        W, H = int(np.ceil(half * 2)), int(np.ceil(y1 - top))   # a hammer head below the feet lifts both frames a little
        ox, oy = half - fx, -top          # frame-0 pixel (x, y) → canvas (x + ox, y + oy)
        for img, (px, py), path in ((f0, (0, 0), p0), (f1, (dx, dy), p1)):
            c = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            c.alpha_composite(img, (int(round(px + ox)), int(round(py + oy))))
            c.save(path)
        report[cid] = {"scale": round(float(s), 2), "dx": round(float(dx)), "dy": round(float(dy)), "iou": round(float(iou), 3), "size": [W, H]}
    print(json.dumps(report, indent=1))


if __name__ == "__main__":
    main()
