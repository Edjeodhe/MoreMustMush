"""Remove the soft glow halo Codex sometimes paints around transparent sprites.

Alpha below LO becomes 0, above HI becomes 255, linear in between (keeps a 1-2 px antialiased edge).
Usage: python Tools/art/alpha_clean.py <png or dir> [...]
"""
import os, sys
import numpy as np
from PIL import Image

LO, HI = 110, 245


def clean(path):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).copy()
    al = a[:, :, 3].astype(np.float32)
    a[:, :, 3] = np.clip((al - LO) * 255 / (HI - LO), 0, 255).astype(np.uint8)
    Image.fromarray(a).save(path)


for arg in sys.argv[1:]:
    files = [os.path.join(r, f) for r, _, fs in os.walk(arg) for f in fs if f.endswith(".png")] if os.path.isdir(arg) else [arg]
    for f in files:
        clean(f)
    print(f"{arg}: {len(files)} cleaned")
