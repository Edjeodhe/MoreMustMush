"""Give the decoration props (Farm/Props/<art>.png) a tone of their own so they read as objects on the world tree.

2026-10-20 user: "decorations a slightly different tone from the world tree, so it shows they are objects".
The props were painted in the tree's own style (moss, leaves, blue flowers), so on the tree they melted into the foliage.
From the untouched paintings (Art/Raw_Dream/props_tree, copied there on the first run) each prop gets:
  - tone: greens pulled toward warm sage and a little less saturated (the tree's foliage is saturated green/blue),
    the whole thing a touch warmer and brighter, like a painted toy set on the tree
  - a cream rim (a soft sticker edge) around the silhouette, and a faint warm glow outside it
Re-running always starts from props_tree, so the result does not drift.

Usage: python Tools/art/deco_tone.py [--preview out.png]
"""
import os, shutil, sys
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DST = os.path.join(ROOT, "Assets/Resources/Farm/Props")
SRC = os.path.join(ROOT, "Art/Raw_Dream/props_tree")
SKIP = {"auto_reward"}            # not a decoration

RIM = (255, 244, 220)             # cream sticker edge
GLOW = (255, 214, 150)            # warm halo
RIM_PX, GLOW_PX = 3.0, 7.0        # at a 190 px prop; scaled with the image
PAD = 10


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rr = m & (mx == r); gg = m & (mx == g) & ~rr; bb = m & ~rr & ~gg
    h[rr] = ((g - b)[rr] / d[rr]) % 6
    h[gg] = (b - r)[gg] / d[gg] + 2
    h[bb] = (r - g)[bb] / d[bb] + 4
    h /= 6
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    i = np.floor(h * 6).astype(int) % 6
    f = h * 6 - np.floor(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    out = np.zeros(h.shape + (3,))
    for k, (a, b, c) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == k
        out[sel, 0], out[sel, 1], out[sel, 2] = a[sel], b[sel], c[sel]
    return out


def tone(rgb):
    h, s, v = rgb_to_hsv(rgb)
    # greens / teal (hue 0.2~0.5): less saturated, nudged toward yellow-sage, so leaves stop matching the tree
    green = np.clip(1 - np.abs(h - 0.33) / 0.15, 0, 1)
    h = h - green * 0.035
    s = s * (1 - 0.38 * green)
    # blue flower accents (tree has a lot of blue): a little softer too
    blue = np.clip(1 - np.abs(h - 0.6) / 0.08, 0, 1)
    s = s * (1 - 0.2 * blue)
    v = np.clip(v * 1.06 + 0.02, 0, 1)
    out = hsv_to_rgb(h % 1, np.clip(s, 0, 1), v)
    # warm wash
    out = out * np.array([1.03, 1.0, 0.94]) + np.array([0.015, 0.008, 0])
    return np.clip(out, 0, 1)


def blur_alpha(alpha, r):
    im = Image.fromarray((alpha * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255


def grow(alpha, r):
    im = Image.fromarray((alpha * 255).astype(np.uint8))
    n = max(1, int(round(r)))
    im = im.filter(ImageFilter.MaxFilter(n * 2 + 1))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(0.8)), dtype=np.float32) / 255


def process(src):
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    k = max(w, h) / 190
    canvas = Image.new("RGBA", (w + PAD * 2, h + PAD * 2), (0, 0, 0, 0))
    canvas.paste(im, (PAD, PAD))
    a = np.asarray(canvas, dtype=np.float32) / 255
    rgb, alpha = a[..., :3], a[..., 3]
    body = tone(rgb)
    rim = grow((alpha > 0.35).astype(np.float32), RIM_PX * k)
    glow = blur_alpha(rim, GLOW_PX * k) * 0.45
    # back to front: glow, rim, painted body
    out_a = glow
    out_rgb = np.broadcast_to(np.array(GLOW) / 255, rgb.shape).copy()
    ra = rim * 0.95
    out_rgb = (np.array(RIM) / 255 * ra[..., None] + out_rgb * out_a[..., None] * (1 - ra[..., None]))
    out_a = ra + out_a * (1 - ra)
    out_rgb = np.where(out_a[..., None] > 1e-6, out_rgb / np.maximum(out_a[..., None], 1e-6), 0)
    ba = alpha
    out_rgb = body * ba[..., None] + out_rgb * out_a[..., None] * (1 - ba[..., None])
    out_a = ba + out_a * (1 - ba)
    out_rgb = np.where(out_a[..., None] > 1e-6, out_rgb / np.maximum(out_a[..., None], 1e-6), 0)
    res = np.dstack([out_rgb, out_a])
    return Image.fromarray((np.clip(res, 0, 1) * 255).astype(np.uint8), "RGBA")


def main():
    if not os.path.isdir(SRC):
        os.makedirs(SRC)
        for f in os.listdir(DST):
            if f.endswith(".png") and f[:-4] not in SKIP:
                shutil.copy2(os.path.join(DST, f), os.path.join(SRC, f))
        print("kept the untouched props in", SRC)
    names = sorted(f for f in os.listdir(SRC) if f.endswith(".png"))
    done = []
    for f in names:
        out = process(os.path.join(SRC, f))
        out.save(os.path.join(DST, f))
        done.append(out)
    print(f"toned {len(done)} props -> {DST}")
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        tree = Image.open(os.path.join(ROOT, "Assets/Resources/Farm/Tree/tree_6.png")).convert("RGBA") \
            if os.path.exists(os.path.join(ROOT, "Assets/Resources/Farm/Tree/tree_6.png")) else None
        S, W = 220, 6
        rows = (len(names) + W - 1) // W
        sheet = Image.new("RGBA", (W * S * 2, rows * S), (110, 140, 95, 255))
        if tree is not None:
            bg = tree.resize((W * S * 2, rows * S))
            sheet.alpha_composite(bg)
        for i, f in enumerate(names):
            for j, img in enumerate([Image.open(os.path.join(SRC, f)).convert("RGBA"), done[i]]):
                t = img.copy(); t.thumbnail((S - 16, S - 16))
                sheet.alpha_composite(t, ((i % W) * S * 2 + j * S + 8, (i // W) * S + 8))
        sheet.save(path)
        print("preview (before | after on the tree):", path)


if __name__ == "__main__":
    main()
