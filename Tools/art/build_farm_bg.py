"""Farm background for the world-tree zoom (user 2026-10-16: the camera pulls back a lot as the tree grows).

Nested layers, each the previous scene seen from further back (Codex, the previous layer as reference):
  bg_ranch        the sharp original glade (Assets/.../bg_ranch.png, warm-graded) = the 1920x1080 stage
  bg_ranch_wide   Art/Raw_Dream/bg_ranch_wide.png   ~1.9x further back
  bg_ranch_wide2  Art/Raw_Dream/bg_ranch_wide2.png  ~1.8x further again
Each inner layer is registered inside the next one (scale + offset), gets its top and side edges faded, and is drawn
over it, so the close view stays sharp and the far view has room. Writes bg_ranch_core / bg_ranch_wide (faded) and
bg_ranch_wide2 (outermost, opaque) and prints each layer's rect in stage px for FarmView.BG_LAYERS.

Run after grade_warm.py (the core is cut from the graded glade).

Usage: python Tools/art/build_farm_bg.py
"""
import os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_critter_rig as R

ROOT = R.ROOT
OUT = os.path.join(ROOT, "Assets/Resources/Backgrounds")
SRC = [("bg_ranch_core", os.path.join(OUT, "bg_ranch.png")),
       ("bg_ranch_wide", os.path.join(ROOT, "Art/Raw_Dream/bg_ranch_wide.png")),
       ("bg_ranch_wide2", os.path.join(ROOT, "Art/Raw_Dream/bg_ranch_wide2.png"))]
FADE = 0.09            # faded border of an inner layer, fraction of its width (top and sides; bottom = screen edge)


def small(im, w):
    k = w / im.width
    return np.asarray(im.resize((w, round(im.height * k)), Image.LANCZOS).convert("RGBA")).astype(np.float32) / 255


def register(inner, outer):
    """(scale inner px -> outer px, x, y of inner's top-left in outer px)"""
    o6, i6 = small(outer, 600), small(inner, 600)
    r = R.find(o6, i6, np.linspace(0.4, 0.95, 23))
    r = R.find(o6, i6, np.linspace(r[1] * 0.97, r[1] * 1.03, 13))
    _, s, x, y = r
    k = outer.width / 600
    print("  match score %.4f" % r[0])
    return s * 600 / inner.width * k, x * k, y * k


def faded(im):
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
    h, w = a.shape[:2]
    fw = FADE * w
    xs, ys = np.arange(w), np.arange(h)
    m = np.outer(np.clip(ys / fw, 0, 1), np.clip(np.minimum(xs, w - 1 - xs) / fw, 0, 1))
    a[:, :, 3] = m * m * (3 - 2 * m)
    return Image.fromarray((a * 255).astype(np.uint8), "RGBA")


layers = [(n, Image.open(p).convert("RGB")) for n, p in SRC if os.path.exists(p)]
rect = (0.0, 0.0, 1920.0, 1080.0)
rects = [rect]
for (_, inner), (name, outer) in zip(layers, layers[1:]):
    s, X0, Y0 = register(inner, outer)
    f = rect[2] / (inner.width * s)          # outer px -> stage px
    rect = (rect[0] - X0 * f, rect[1] - Y0 * f, outer.width * f, outer.height * f)
    rects.append(rect)
for i, (name, im) in enumerate(layers):
    (faded(im) if i < len(layers) - 1 else im).save(os.path.join(OUT, name + ".png"))
    x, y, w, h = rects[i]
    zx = min(960 - x, x + w - 960) / 960
    zy = (1080 - y) / 1080
    print('            ("Backgrounds/%s", new Rect(%.1ff, %.1ff, %.1ff, %.1ff)),   // max zoom %.2f' % (name, x, y, w, h, min(zx, zy)))
