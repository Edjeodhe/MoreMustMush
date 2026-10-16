"""Square and centre the node slot pictures (Assets/Art/Dream/UI/Slots) so every slot has the same circle size.

Each slot is cropped to its round silhouette, centred on a square canvas with a small margin and resized to SIZE,
so a slot drawn a few pixels bigger or off-centre on the sheet still lines up with the others in the tree.
Usage: python Tools/art/norm_slots.py
"""
import glob, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, "Assets/Art/Dream/UI/Slots")
SIZE, MARGIN = 256, 4

for p in sorted(glob.glob(os.path.join(DIR, "slot_*.png"))):
    im = Image.open(p).convert("RGBA")
    a = np.asarray(im)[:, :, 3]
    ys, xs = np.nonzero(a > 128)
    # the circle: fit by the solid area (robust to a small ornament sticking out of the rim)
    cy, cx = ys.mean(), xs.mean()
    r = np.sqrt((a > 128).sum() / np.pi)
    half = r + MARGIN
    box = (int(round(cx - half)), int(round(cy - half)), int(round(cx + half)), int(round(cy + half)))
    out = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (0, 0, 0, 0))
    out.paste(im.crop((max(box[0], 0), max(box[1], 0), min(box[2], im.width), min(box[3], im.height))), (max(-box[0], 0), max(-box[1], 0)))
    out.resize((SIZE, SIZE), Image.LANCZOS).save(p)
    print(os.path.basename(p), "r", round(r, 1))
