"""World tree stage 8 (tree_7) = the approved final concept Art/Raw_Dream/concept/tree_final_v2.png (user 2026-10-16).

The generator leaves a faint halo of half-transparent colour around the drawing: drop pixels under ALPHA_CUT, then
crop to the drawing and save it where the mush_tree sheet puts the other stages.

Usage: python Tools/art/build_tree_final.py
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Art/Raw_Dream/concept/tree_final_v2.png")
DST = os.path.join(ROOT, "Assets/Resources/Farm/Tree/tree_7.png")
ALPHA_CUT = 170

a = np.array(Image.open(SRC).convert("RGBA"))
a[a[:, :, 3] < ALPHA_CUT] = 0
im = Image.fromarray(a)
im = im.crop(im.getbbox())
im.save(DST)
print("tree_7", im.size)
