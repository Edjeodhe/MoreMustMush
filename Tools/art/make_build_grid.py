"""Build-placement grid overlay for the farm: one rounded cell per tile that can hold a building
(cell center inside the fence ellipse and outside the mushroom-tree trunk zone). Same numbers as Defs.GRID / Defs.TREE —
rerun after changing them:  python Tools/art/make_build_grid.py"""
from PIL import Image, ImageDraw

CELL, X0, Y0, COLS, ROWS = 40, 120, 260, 42, 16
ECX, ECY, ERX, ERY = 960, 565, 840, 285
ZX0, ZX1, ZY0, ZY1 = 740, 1180, 260, 540   # TREE.zone

img = Image.new("RGBA", (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
for gy in range(ROWS):
    for gx in range(COLS):
        cx, cy = X0 + (gx + 0.5) * CELL, Y0 + (gy + 0.5) * CELL
        if ((cx - ECX) / ERX) ** 2 + ((cy - ECY) / ERY) ** 2 > 1: continue
        if ZX0 < cx < ZX1 and ZY0 < cy < ZY1: continue
        x, y = gx * CELL, gy * CELL
        d.rounded_rectangle([x + 3, y + 3, x + CELL - 4, y + CELL - 4], radius=6, fill=(255, 255, 255, 46), outline=(255, 255, 255, 110), width=2)
img.save("Assets/Art/Generated/FX/build_grid.png")
print("build_grid", img.size)
