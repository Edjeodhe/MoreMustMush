"""Soften the world tree's grassy base into the farm ground (user 2026-10-16: the base looked pasted on).

Each tree_<n> stands on a bright oval of grass with a crisp rim. In the bottom BAND of the sprite the alpha fades out
over FADE (fraction of the sprite width) towards the drawing's edge, more strongly lower down, and the base colour is
pulled a little toward the farm ground (GROUND), so the background grass shows through the rim. On top, a light
vignette, a shade at the foot and a hazy rim (VIG, SHADE, EDGE_TINT) sit the watercolor tree in the misty scene.

Works from untouched copies in Art/Raw_Dream/tree_src (made from Assets/Resources/Art/Dream/Farm/Tree on the first run), so it
can be run again. After re-slicing mush_tree or rebuilding tree_7 (build_tree_final.py), delete tree_src first.

Usage: python Tools/art/blend_tree_base.py
"""
import os, shutil, time
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, "Assets/Resources/Art/Dream/Farm/Tree")
SRC = os.path.join(ROOT, "Art/Raw_Dream/tree_src")
BAND = 0.2             # bottom part of the sprite (fraction of its height) that is blended
FADE = 0.07            # edge fade width at the very bottom (fraction of the sprite width)
GROUND = np.array([0.62, 0.63, 0.42])   # farm meadow tone around the tree spot (mean of bg_ranch, graded, stage 500-1150 x 700-820)
TINT = 0.25            # how far the base colour moves toward GROUND at the bottom
# 2026-10-20 (user: blend the watercolor tree into the scene a little): a soft vignette, a shade at the foot, a hazy rim
VIG = 0.10             # radial vignette: darkening at the sprite's far corners (0 in the middle)
SHADE = 0.16           # shade over the bottom SHADE_BAND of the tree, strongest at the trunk's foot
SHADE_BAND = 0.32
HAZE = np.array([0.70, 0.75, 0.66])     # misty tone of the farm background's air
EDGE = 0.035           # hazy rim width (fraction of the sprite width) ...
EDGE_TINT = 0.14       # ... and how far its outermost pixels move toward HAZE


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


os.makedirs(SRC, exist_ok=True)
for i in range(8):
    name = f"tree_{i}.png"
    src, dst = os.path.join(SRC, name), os.path.join(DIR, name)
    if not os.path.exists(dst):
        continue
    if not os.path.exists(src):
        shutil.copy2(dst, src)
    a = np.asarray(Image.open(src).convert("RGBA")).astype(np.float32) / 255
    h, w = a.shape[:2]
    y0 = h * (1 - BAND)
    wy = smooth((np.arange(h) - y0) / (h - y0))[:, None]          # 0 above the band .. 1 at the bottom row
    inside = ndimage.distance_transform_edt(a[:, :, 3] > 0.5)      # px to the drawing's edge
    fade = np.maximum(FADE * w * wy, 1e-3)
    k = np.where(wy > 0, smooth(inside / fade), 1.0)
    out = a.copy()
    out[:, :, 3] = a[:, :, 3] * k
    t = (TINT * wy)[..., None] * (1 - k[..., None] * 0.5)         # the fading rim takes on more of the ground tone
    out[:, :, :3] = a[:, :, :3] * (1 - t) + GROUND * t
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot((xs - w / 2) / (w / 2), (ys - h * 0.45) / (h * 0.55))
    vig = 1 - VIG * smooth((r - 0.35) / 0.75)
    sy = smooth((ys / h - (1 - SHADE_BAND)) / SHADE_BAND)                # 0 above the band .. 1 at the bottom
    sx = 1 - 0.5 * smooth(np.abs(xs - w / 2) / (w * 0.45))                # strongest under the trunk
    shade = 1 - SHADE * sy * sx
    rim = EDGE_TINT * (1 - smooth(inside / (EDGE * w)))
    out[:, :, :3] = out[:, :, :3] * (vig * shade)[..., None]
    out[:, :, :3] = out[:, :, :3] * (1 - rim[..., None]) + HAZE * rim[..., None]
    img = Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")
    for attempt in range(10):                                       # the open Unity editor briefly locks files it reimports
        try:
            img.save(dst)
            break
        except OSError:
            time.sleep(1)
    else:
        raise OSError(f"could not write {dst}")
    print(name, w, h)
