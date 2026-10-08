"""Cut the grandpa reference illustration (grandpa_ref.png) into rig parts.

Parts (Assets/Art/Generated/Characters/Grandpa/Rig/):
  body.png         whole character minus the scythe; the hole is filled with the nearest colours
  scythe.png       fist + handle + blade (sways around the wrist); the handle hidden behind the head
                   is continued so no gap shows while it moves
  head_cover.png   original head pixels where the handle passes behind the beard / hat brim,
                   drawn over the scythe so the handle stays tucked behind the head
  eyes_closed.png  blink patch over both eyes
  rig.json         each part's top-left in reference pixels + joint positions, used to lay out the prefab

Usage: python Tools/art/build_grandpa_rig.py [--debug <dir>]
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Assets/Art/Generated/Characters/Grandpa/grandpa_ref.png")
OUT = os.path.join(ROOT, "Assets/Art/Generated/Characters/Grandpa/Rig")

FEET = (517, 1205)          # rig root (bottom centre between the boots)
WRIST = (215, 505)          # scythe joint
DARK = 0.30                 # max(rgb) below this = ink outline

# Seeds inside each colour region of the scythe (separated from the body by ink lines).
SCYTHE_SEEDS = [
    (40, 600), (60, 590), (110, 565), (150, 545), (175, 525),          # handle end cap + shaft left of the fist
    (160, 480), (200, 470), (240, 462), (270, 478), (285, 500),        # fist
    (310, 445), (335, 430),                                            # shaft between fist and beard
]
# The scythe head floats on the background: everything opaque inside this polygon belongs to it.
SCYTHE_HEAD_POLY = [(688, 190), (760, 135), (845, 25), (1070, 25), (1070, 660), (995, 660),
                    (872, 330), (800, 300), (762, 262), (688, 258)]
# Where the handle disappears behind the beard / hat brim (cover zones, reference px boxes).
COVERS = [(345, 375, 440, 475), (650, 180, 775, 275)]
HIDDEN = ((330, 446), (700, 212))   # handle centre line behind the head
EYES = [  # (cx, cy, rx, ry, lid curve sag) per eye
    (529, 288, 31, 19, 7),
    (428, 321, 25, 15, 6),
]


def load():
    im = np.asarray(Image.open(SRC).convert("RGBA")).astype(np.float32) / 255
    return im


def region_mask(im, seeds):
    a = im[..., 3] > 0.5
    v = im[..., :3].max(axis=2)
    open_ = a & (v > DARK)
    lab, _ = ndimage.label(open_)
    ids = {lab[y, x] for x, y in seeds if lab[y, x] > 0}
    m = np.isin(lab, list(ids))
    # take in the ink outline around the picked regions
    ink = a & ~open_
    grow = ndimage.binary_dilation(m, iterations=7)
    m = m | (grow & ink)
    return ndimage.binary_fill_holes(m)


def poly_mask(shape, poly):
    img = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(img).polygon(poly, fill=255)
    return np.asarray(img) > 0


def nearest_fill(rgba, hole):
    """Fill hole pixels with the nearest non-hole pixel (colour and alpha, hard edge)."""
    _, (iy, ix) = ndimage.distance_transform_edt(hole, return_indices=True)
    out = rgba.copy()
    out[hole] = rgba[iy[hole], ix[hole]]
    out[hole & (out[..., 3] < 0.5), 3] = 0          # next to the background: stay see-through
    out[hole & (out[..., 3] >= 0.5), 3] = 1
    return out


def continue_handle(im, scythe):
    """Paint the handle behind the head by sliding a visible plain-wood chunk along the hidden centre line."""
    h, w = scythe.shape
    (x0, y0), (x1, y1) = HIDDEN
    chunk = scythe.copy(); chunk[:, :690] = False; chunk[:, 755:] = False; chunk[:180] = False; chunk[240:] = False
    cy, cx = np.nonzero(chunk)
    L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x0 - x1) / L, (y0 - y1) / L
    out = np.zeros_like(im); filled = np.zeros_like(scythe)
    step = 40.0
    for k in range(1, int(L // step) + 2):
        dx, dy = ux * step * k, uy * step * k
        ty, tx = np.round(cy + dy).astype(int), np.round(cx + dx).astype(int)
        ok = (tx >= 0) & (tx < w) & (ty >= 0) & (ty < h)
        ty, tx, sy, sx = ty[ok], tx[ok], cy[ok], cx[ok]
        free = ~filled[ty, tx]
        out[ty[free], tx[free]] = im[sy[free], sx[free]]
        filled[ty[free], tx[free]] = True
    return out, filled


def eye_patches(im):
    h, w = im.shape[:2]
    closed = np.zeros_like(im)
    yy, xx = np.mgrid[0:h, 0:w]
    lid = Image.new("RGBA", (w, h), (0, 0, 0, 0)); ld = ImageDraw.Draw(lid)
    for cx, cy, rx, ry, sag in EYES:
        e = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
        inner = e <= 1.0
        ring = (e > 1.6) & (e < 3.2)
        ring &= (im[..., 3] > 0.9) & (im[..., :3].max(axis=2) > 0.75) & (im[..., 0] > im[..., 2] + 0.15)   # skin only
        skin = np.median(im[ring][:, :3], axis=0)
        soft = np.clip((1.25 - e) / 0.25, 0, 1)
        closed[..., :3] = np.where(soft[..., None] > 0, skin, closed[..., :3])
        closed[..., 3] = np.maximum(closed[..., 3], soft)
        # closed lid: a thick ink curve sagging downwards, lashes at the outer end
        pts = [(cx - rx + 2 + i * (2 * rx - 4) / 20, cy + 2 + sag * math.sin(math.pi * i / 20)) for i in range(21)]
        ld.line(pts, fill=(58, 32, 22, 255), width=max(4, rx // 6), joint="curve")
    closed_img = Image.fromarray((closed * 255).astype(np.uint8), "RGBA")
    closed_img.alpha_composite(lid)
    return closed_img


def to_img(arr_or_img):
    return arr_or_img if isinstance(arr_or_img, Image.Image) else Image.fromarray((np.clip(arr_or_img, 0, 1) * 255).astype(np.uint8), "RGBA")


def save_part(arr_or_img, name, meta, box=None):
    img = to_img(arr_or_img)
    if box is None:
        box = img.getbbox()
        pad = 2
        box = (max(0, box[0] - pad), max(0, box[1] - pad), min(img.width, box[2] + pad), min(img.height, box[3] + pad))
    img.crop(box).save(os.path.join(OUT, name + ".png"))
    meta[name] = {"x": box[0], "y": box[1], "w": box[2] - box[0], "h": box[3] - box[1]}


def main():
    debug = sys.argv[sys.argv.index("--debug") + 1] if "--debug" in sys.argv else None
    os.makedirs(OUT, exist_ok=True)
    im = load()
    a = im[..., 3] > 0.02
    scythe = region_mask(im, SCYTHE_SEEDS) | (poly_mask(a.shape, SCYTHE_HEAD_POLY) & a)
    hid, hid_mask = continue_handle(im, scythe)
    cover_zone = np.zeros_like(a); hid_zone = np.zeros_like(a)
    for x0, y0, x1, y1 in COVERS:
        cover_zone[y0:y1, x0:x1] = True
        hid_zone[y0 + 10:y1 - 10, x0 + 10:x1 - 10] = True   # margin: the sway never pushes it out from under the cover
    hid_mask &= hid_zone & ~scythe & a        # only where the head cover hides it

    sc = np.zeros_like(im)
    sc[hid_mask] = hid[hid_mask]
    sc[scythe] = im[scythe]

    # the hole also takes the ink lines the body shared with the scythe, so no stray outline is left behind
    ink = a & (im[..., :3].max(axis=2) <= DARK)
    hole = ndimage.binary_dilation(scythe, iterations=1) | (ink & ndimage.binary_dilation(scythe, iterations=5))
    body = nearest_fill(im, hole)

    cover = np.zeros_like(im)
    cm = cover_zone & a & ~scythe
    cover[cm] = im[cm]

    closed = eye_patches(im)

    meta = {"ref": {"w": im.shape[1], "h": im.shape[0]}, "feet": FEET, "wrist": WRIST}
    save_part(body, "body", meta)
    save_part(sc, "scythe", meta)
    save_part(cover, "head_cover", meta)
    save_part(closed, "eyes_closed", meta)
    with open(os.path.join(OUT, "rig.json"), "w") as f: json.dump(meta, f, indent=1)

    if debug:
        base = Image.open(SRC).convert("RGBA")
        bg = Image.new("RGBA", base.size, (170, 200, 170, 255)); bg.alpha_composite(base)
        ov = np.zeros((*a.shape, 4), np.uint8); ov[scythe] = (255, 0, 255, 110); ov[hid_mask & ~scythe] = (0, 120, 255, 110); ov[cm] = (255, 200, 0, 90)
        bg.alpha_composite(Image.fromarray(ov, "RGBA")); bg.save(os.path.join(debug, "rig_masks.png"))
        b2 = Image.new("RGBA", base.size, (170, 200, 170, 255)); b2.alpha_composite(Image.fromarray((np.clip(body, 0, 1) * 255).astype(np.uint8), "RGBA")); b2.save(os.path.join(debug, "rig_body.png"))
    print(json.dumps(meta))


if __name__ == "__main__":
    main()
