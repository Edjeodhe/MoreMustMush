"""The world tree at every stage the way the farm shows it, without running Unity -> Logs/Screens/tree_stages_ingame.png

Same framing as FarmView: the camera view is 1920x1080 x WT.zoom with the ground (y 1080) at the screen bottom and
centre x 960; backgrounds bg_ranch_wide2 < bg_ranch_wide < bg_ranch_core at their FarmView rects; the tree is WT.width
wide with its foot line (tree_layout foot) on WT.y (FarmTreeView.Layout). The red line is WT.y. No spirits, shadow or UI.
usage: python Tools/tree-layout/stage_preview.py
"""
import os
from PIL import Image, ImageDraw, ImageFont
import tree_defs as T

B = os.path.join(T.ROOT, "Assets/Resources/Backgrounds")
NAMES = ["새싹", "묘목", "어린 나무", "버섯 나무", "이끼 고목", "룬 신목", "고대수", "아르카나 세계수"]
OW, OH = 640, 360


def main():
    core = Image.open(os.path.join(B, "bg_ranch_core.png")).convert("RGBA")
    ch = 1920 * core.height / core.width
    layers = [(Image.open(os.path.join(B, "bg_ranch_wide2.png")).convert("RGBA"), (-2669.7, -3044.4, 7317.2, 4118.1)),
              (Image.open(os.path.join(B, "bg_ranch_wide.png")).convert("RGBA"), (-877.0, -1020.0, 3731.8, 2100.2)),
              (core, (0, 540 - ch / 2, 1920, ch))]
    feet = {p["pic"]: p.get("foot", 1) for p in T.load()["pics"]}
    font = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 24)
    sheet = Image.new("RGB", (OW * 4 + 30, OH * 2 + 10), (20, 24, 32))
    for pic in range(T.STAGES):
        z = T.ZOOM[pic]; vw, vh = 1920 * z, 1080 * z; x0, y0 = 960 - vw / 2, 1080 - vh; k = OW / vw
        fr = Image.new("RGBA", (OW, OH), (0, 0, 0, 255))
        for im, (rx, ry, rw, rh) in layers:
            t = im.resize((max(1, round(rw * k)), max(1, round(rh * k))))
            fr.paste(t, (round((rx - x0) * k), round((ry - y0) * k)), t)
        tree = Image.open(os.path.join(T.TREE, f"tree_{pic}.png")).convert("RGBA")
        w = T.WIDTH[pic]; h = w * tree.height / tree.width; foot = feet.get(pic, 1)
        t = tree.resize((max(1, round(w * k)), max(1, round(h * k))), Image.LANCZOS)
        fr.alpha_composite(t, (round((T.WT_X - w / 2 - x0) * k), round((T.WT_Y + 6 + (1 - foot) * h - h - y0) * k)))
        d = ImageDraw.Draw(fr)
        gy = (T.WT_Y - y0) * k
        d.line([(0, gy), (OW, gy)], fill=(255, 80, 80, 110), width=1)
        d.rectangle([0, 0, OW, 34], fill=(0, 0, 0, 140))
        d.text((8, 2), f"{pic + 1}단계 {NAMES[pic]} · 화각 ×{z} · foot {foot}", fill=(255, 255, 255), font=font)
        sheet.paste(fr.convert("RGB"), ((pic % 4) * (OW + 10), (pic // 4) * (OH + 10)))
    out = os.path.join(T.ROOT, "Logs/Screens/tree_stages_ingame.png")
    sheet.save(out)
    print(out)


if __name__ == "__main__":
    main()
