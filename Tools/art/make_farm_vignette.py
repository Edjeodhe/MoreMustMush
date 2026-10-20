"""Farm screen vignette (user 2026-10-20: blend the world tree into the background with a vignette and shadow).
A full-screen UI image under the farm UI: transparent centre around the tree, a soft cool blue-teal veil toward the
edges, with low-frequency watercolor unevenness so it reads as painted rather than a digital gradient.

Usage: python Tools/art/make_farm_vignette.py   -> Assets/Resources/Art/Dream/FX/vignette.png
"""
import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets/Resources/Art/Dream/FX/vignette.png")
W, H = 768, 432
COL = (30, 52, 62)          # deep blue-teal, the background's shadow colour
MAX_A = 0.62                # opacity at the far corners
CX, CY = 0.5, 0.45          # focus (fraction of the screen), a little above centre: the tree's body

yy, xx = np.mgrid[0:H, 0:W]
d = np.sqrt(((xx / W - CX) / 0.62) ** 2 + ((yy / H - CY) / 0.70) ** 2)
a = np.clip((d - 0.42) / 0.62, 0, 1) ** 1.4
rng = np.random.default_rng(7)
noise = Image.fromarray((rng.random((H // 16, W // 16)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
noise = np.asarray(noise.filter(ImageFilter.GaussianBlur(10))).astype(np.float32) / 255
a = np.clip(a * (0.85 + 0.3 * noise), 0, 1) * MAX_A
img = np.zeros((H, W, 4), np.uint8)
img[..., :3] = COL
img[..., 3] = (a * 255).astype(np.uint8)
Image.fromarray(img, "RGBA").save(OUT)
print(OUT)
