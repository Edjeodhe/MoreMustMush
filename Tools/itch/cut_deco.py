# deco_sheet.png(흰 배경)를 조각별 투명 PNG로 오린다 → Marketing/itch_dream/out/deco_*.png
# 바깥 흰 배경만 투명으로, 가장자리 2px은 흰색을 걷어내 부드럽게. 빈 원판에는 1~4 숫자를 합성한다.
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Marketing", "itch_dream", "deco_sheet.png")
OUT = os.path.join(ROOT, "Marketing", "itch_dream", "out")

# 시트 위에서부터 줄 순서, 줄 안에서는 왼→오 순서의 이름
NAMES = [
    ["divider_vine", "divider_clover"],
    ["ribbon_red", "ribbon_green", "label"],
    ["grandpa", "spirit_fire", "spirit_sprout", "spirit_book"],
    ["spirit_water", "spirit_moon", "spirit_star", "worldtree"],
    ["icon_mushroom", "icon_leaf", "icon_sparkle", "icon_acorn", "icon_drop",
     "badge_1", "badge_2", "badge_3", "badge_4"],
]


BOXES = [  # (y0, y1, x0, x1) — 1254px 시트 기준
    [(80, 185, 15, 620), (80, 190, 630, 1235)],
    [(210, 360, 25, 510), (220, 360, 515, 980), (225, 365, 982, 1235)],
    [(380, 745, 15, 382), (430, 705, 380, 630), (465, 725, 638, 925), (400, 730, 935, 1240)],
    [(760, 1065, 40, 300), (770, 1050, 340, 580), (770, 1045, 600, 855), (760, 1065, 870, 1235)],
    [(1090, 1205, 20, 135), (1095, 1205, 145, 230), (1105, 1190, 250, 355), (1090, 1205, 380, 475),
     (1085, 1195, 505, 600), (1070, 1215, 630, 780), (1070, 1215, 780, 930), (1070, 1215, 930, 1080),
     (1070, 1215, 1080, 1230)],
]


def find_parts(rgb):
    return BOXES


def find_parts_auto(rgb):
    ink = (255 - rgb.min(axis=2)) > 22
    merged = ndi.binary_dilation(ink, iterations=14)
    lab, n = ndi.label(merged)
    boxes = []
    for i, sl in enumerate(ndi.find_objects(lab), 1):
        ys, xs = sl
        if (ys.stop - ys.start) * (xs.stop - xs.start) < 1500:
            continue
        boxes.append((ys.start, ys.stop, xs.start, xs.stop))
    # 줄 묶기: 세로 중심이 가까운 것끼리
    boxes.sort(key=lambda b: (b[0] + b[1]) / 2)
    rows, cur = [], []
    for b in boxes:
        cy = (b[0] + b[1]) / 2
        if cur and cy - (cur[-1][0] + cur[-1][1]) / 2 > 90:
            rows.append(cur); cur = []
        cur.append(b)
    rows.append(cur)
    return [sorted(r, key=lambda b: b[2]) for r in rows]


def cut(rgb, box, pad=0):
    y0, y1, x0, x1 = box
    h, w = rgb.shape[:2]
    y0, x0 = max(0, y0 - pad), max(0, x0 - pad)
    y1, x1 = min(h, y1 + pad), min(w, x1 + pad)
    c = rgb[y0:y1, x0:x1].astype(np.float32)
    white = (255 - c.min(axis=2)) <= 18
    lab, _ = ndi.label(white)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(edge))
    # 다른 조각이 칸 안에 걸쳐 들어온 것 지우기: 가장 큰 비배경 덩어리만 남김
    fg_lab, n = ndi.label(ndi.binary_dilation(~bg, iterations=10))
    if n > 1:
        sizes = ndi.sum(~bg, fg_lab, range(1, n + 1))
        border = set(np.unique(np.concatenate([fg_lab[0], fg_lab[-1], fg_lab[:, 0], fg_lab[:, -1]]))) - {0}
        ids = [i for i in range(1, n + 1) if i not in border or i == np.argmax(sizes) + 1]
        bg |= ~np.isin(fg_lab, ids)
    alpha = np.where(bg, 0.0, 1.0)
    # 가장자리 띠: 흰색과 섞인 정도만큼 알파
    band = ndi.binary_dilation(bg, iterations=2) & ~bg
    unmix = ((255 - c) / 255.0).max(axis=2)
    alpha = np.where(band, np.clip(unmix * 1.6, 0, 1), alpha)
    a3 = np.maximum(alpha, 1e-3)[..., None]
    col = np.where(band[..., None], (c - 255 * (1 - a3)) / a3, c)
    out = np.dstack([np.clip(col, 0, 255), alpha * 255]).astype(np.uint8)
    img = Image.fromarray(out, "RGBA")
    return img.crop(img.getbbox())


def number_badge(img, n):
    font_path = os.path.join(ROOT, "Assets", "Fonts", "GowunBatang-Bold.ttf")
    d = ImageDraw.Draw(img)
    size = int(img.height * 0.55)
    font = ImageFont.truetype(font_path, size) if os.path.exists(font_path) else ImageFont.load_default()
    d.text((img.width / 2, img.height / 2), str(n), font=font, fill=(84, 58, 40, 255), anchor="mm")
    return img


def main():
    rgb = np.array(Image.open(SRC).convert("RGB"))
    rows = find_parts(rgb)
    counts = [len(r) for r in rows]
    want = [len(r) for r in NAMES]
    if counts != want:
        raise SystemExit(f"조각 수가 다름: 찾음 {counts}, 기대 {want}")
    os.makedirs(OUT, exist_ok=True)
    for row, names in zip(rows, NAMES):
        for box, name in zip(row, names):
            img = cut(rgb, box)
            if name.startswith("badge_"):
                img = number_badge(img, int(name[-1]))
            img.save(os.path.join(OUT, f"deco_{name}.png"))
            print(name, img.size)
    little_ones()


def little_ones(h=230, gap=24):
    """꼬마 정령 6명을 한 줄로 → little_ones.png, 구분선 → divider.png"""
    names = ["spirit_sprout", "spirit_fire", "spirit_water", "spirit_moon", "spirit_star", "spirit_book"]
    ims = []
    for n in names:
        im = Image.open(os.path.join(OUT, f"deco_{n}.png"))
        ims.append(im.resize((round(im.width * h / im.height), h), Image.LANCZOS))
    W = sum(i.width for i in ims) + gap * (len(ims) - 1)
    row = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    x = 0
    for im in ims:
        row.alpha_composite(im, (x, 0)); x += im.width + gap
    row.save(os.path.join(OUT, "little_ones.png"))
    Image.open(os.path.join(OUT, "deco_divider_vine.png")).save(os.path.join(OUT, "divider.png"))
    print("little_ones", row.size)


if __name__ == "__main__":
    main()
