"""Depth haze on the farm backgrounds so the world tree reads as the focus (user 2026-10-20: lower the background's
contrast the farther back it is, keep the original painting).

Works in STAGE coordinates (1920x1080, y down, the ground line at the bottom), so the three nested layers
(bg_ranch_core / bg_ranch_wide / bg_ranch_wide2, rects from FarmView.BG_LAYERS) get the same haze at the same spot and
no seam shows where an inner layer fades into the next one. Higher up = farther = lower contrast, less saturation,
more pale fog; the bottom band (near bushes) gets a little darker and cooler so it frames the view.

Works from untouched copies in Art/Raw_Dream/bg_src (made from Assets/Resources/Backgrounds on the first run),
so it can be run again. After rebuilding the layers (build_farm_bg.py), delete bg_src first.

Usage: python Tools/art/haze_farm_bg.py [strength=0.6]
"""
import os, shutil, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, "Assets/Resources/Backgrounds")
SRC = os.path.join(ROOT, "Art/Raw_Dream/bg_src")
K = float(sys.argv[1]) if len(sys.argv) > 1 else 0.6     # overall strength
STAGE_H = 1080.0
LAYERS = {                                                 # name: (x, y, w, h) in stage px
    "bg_ranch_core": (0, 0, 1920, 1080),
    "bg_ranch_wide": (-877.0, -1020.0, 3731.8, 2100.2),
    "bg_ranch_wide2": (-2669.7, -3044.4, 7317.2, 4118.1),
}
FOG = np.array([0.80, 0.80, 0.86])                         # pale misty lavender-blue
NEAR_TINT = np.array([0.05, 0.08, 0.12])
HORIZON = 0.78                                             # below this (fraction of stage height) nothing is "far"

os.makedirs(SRC, exist_ok=True)
mean = None
for name, (rx, ry, rw, rh) in LAYERS.items():
    src, dst = os.path.join(SRC, name + ".png"), os.path.join(DIR, name + ".png")
    if not os.path.exists(src):
        shutil.copy2(dst, src)
    im = Image.open(src)
    has_a = im.mode == "RGBA"
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3:]
    h = rgb.shape[0]
    yn = ((ry + (np.arange(h) + 0.5) * rh / h) / STAGE_H)[:, None, None]
    far = K * np.clip((HORIZON - yn) / HORIZON, 0, 1) ** 0.8
    near = np.clip((yn - HORIZON) / (1 - HORIZON), 0, 1)
    if mean is None:                                       # one pivot for every layer (the core's mean colour)
        mean = (rgb * al).reshape(-1, 3).sum(0) / max(al.sum(), 1)
    lum = (rgb * [0.299, 0.587, 0.114]).sum(-1, keepdims=True)
    rgb = lum + (rgb - lum) * (1 - 0.55 * far)
    rgb = mean + (rgb - mean) * (1 - 0.12 * K) * (1 - 0.55 * far)
    rgb = rgb * (1 - 0.5 * far ** 1.3) + FOG * (0.5 * far ** 1.3)
    rgb = rgb * (1 - 0.08 * near) + NEAR_TINT * 0.08 * near
    out = np.concatenate([np.clip(rgb, 0, 1), al], -1)
    Image.fromarray((out * 255).astype(np.uint8), "RGBA" if has_a else None).convert("RGBA" if has_a else "RGB").save(dst)
    print(name, im.size)
