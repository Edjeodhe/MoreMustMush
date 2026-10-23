#!/usr/bin/env python
"""Warm, livelier colour grade for the Dream backgrounds and framing scenery (user: the field looked drab and cool).

Composition is untouched; only colour moves: warmer white balance, a little more saturation, lifted midtones.
Always grades from the ungraded originals, so running it again never stacks:
  - backgrounds: Art/Raw_Dream/bg_*.png  ->  Assets/Resources/Backgrounds/bg_*.png
  - Env scenery: first run copies Assets/Resources/Env/**.png to Art/Raw_Dream/grade_src/Env/, grades from there
    (re-slicing env sheets writes ungraded sprites: delete grade_src/Env and run this again)

  python Tools/art/grade_warm.py [--preview out_dir]
"""
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "Art/Raw_Dream"
OUT = ROOT / "Assets/Resources"
SRC_ENV = RAW / "grade_src/Env"

WARM = np.array([1.05, 1.0, 0.89])   # white balance: a touch more red, less blue
SAT = 1.16                           # saturation around luminance
GAMMA = 0.93                         # < 1 lifts midtones
NIGHT = {"bg_night"}                 # night keeps its blue, only gets livelier


def grade(img: Image.Image, warm=True) -> Image.Image:
    a = np.asarray(img.convert("RGBA")).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3:]
    if warm:
        rgb = rgb * WARM
    lum = (rgb * [0.299, 0.587, 0.114]).sum(-1, keepdims=True)
    rgb = lum + (rgb - lum) * SAT
    rgb = np.clip(rgb, 0, 1) ** GAMMA
    out = np.concatenate([np.clip(rgb, 0, 1), al], -1)
    return Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA")


def main():
    preview = Path(sys.argv[sys.argv.index("--preview") + 1]) if "--preview" in sys.argv else None
    n = 0
    for src in sorted(RAW.glob("bg_*.png")):
        dst = OUT / "Backgrounds" / src.name
        if not dst.exists() or src.stem.endswith("_wide"):   # bg_ranch_wide was painted from the graded glade already
            continue
        g = grade(Image.open(src), warm=src.stem not in NIGHT)
        if preview:
            preview.mkdir(parents=True, exist_ok=True)
            w, h = g.size
            both = Image.new("RGBA", (w * 2, h))
            both.paste(Image.open(src).convert("RGBA"), (0, 0)); both.paste(g, (w, 0))
            both.resize((w, h // 2)).save(preview / src.name)
        else:
            g.convert("RGB").save(dst)
        n += 1
    if not preview:
        env = OUT / "Env"
        if not SRC_ENV.exists():
            for p in env.rglob("*.png"):
                q = SRC_ENV / p.relative_to(env)
                q.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(p, q)
        for q in SRC_ENV.rglob("*.png"):
            grade(Image.open(q)).save(env / q.relative_to(SRC_ENV))
            n += 1
    print(f"graded {n}")


if __name__ == "__main__":
    main()
