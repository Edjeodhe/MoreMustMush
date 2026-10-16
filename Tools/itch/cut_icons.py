# icon_sheet.png(4×3 칸, 흰 배경)를 칸마다 오려 투명 PNG로 → Marketing/itch_dream/out/ic_*.png
# 칸 안에서 잉크(흰색이 아닌 곳) 테두리를 찾아 자르고, 흰 배경은 밝기로 알파를 만들어 부드럽게 걷어낸다.
import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Marketing", "itch_dream", "icon_sheet.png")
OUT = os.path.join(ROOT, "Marketing", "itch_dream", "out")
NAMES = ["book", "tree_nodes", "weather", "boss",
         "sapling", "palette", "launch", "mouse",
         "key", "pause", "coin", "hourglass"]
SIZE = 96   # 설명 글 줄머리용

def main():
    im = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    H, W, _ = im.shape
    for i, name in enumerate(NAMES):
        r, c = divmod(i, 4)
        cell = im[r * H // 3:(r + 1) * H // 3, c * W // 4:(c + 1) * W // 4]
        ink = (255 - cell.min(axis=2)) > 18
        ys, xs = np.where(ink)
        y0, y1, x0, x1 = max(ys.min() - 6, 0), ys.max() + 7, max(xs.min() - 6, 0), xs.max() + 7
        part = cell[y0:y1, x0:x1]
        a = np.clip((255 - part.min(axis=2) - 8) * 6, 0, 255)    # 흰색에 가까울수록 투명
        rgb = np.clip((part - 255 * (1 - a[..., None] / 255)) / np.maximum(a[..., None] / 255, 1e-3), 0, 255)   # 흰색 번짐 걷기
        out = Image.fromarray(np.dstack([rgb, a]).astype(np.uint8), "RGBA")
        side = max(out.size)
        sq = Image.new("RGBA", (side, side)); sq.paste(out, ((side - out.width) // 2, (side - out.height) // 2))
        sq.resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(OUT, f"ic_{name}.png"))
        print(name, out.size)

if __name__ == "__main__":
    main()
