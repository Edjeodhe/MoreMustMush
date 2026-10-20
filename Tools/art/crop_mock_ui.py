"""Cut the illustrations out of the user's window mockups (Art/Raw_Dream/concept/ui_mock_*.png, 1536x1024) and save
them as UI sprites in Resources/Art/Dream/UI/Window/mock_*.png. 2026-10-20 user: "내가 보낸 사진들 다 그대로 적용해".

  card   = rounded-rect picture (soft alpha corners)
  circle = round badge picture (soft alpha edge)
  wash   = blurred picture, used as the soft watercolor backdrop behind decoration art (recipe/slot pictures)
  plain  = rectangle as is

Usage: python Tools/art/crop_mock_ui.py [--preview out.png]
"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Art/Raw_Dream/concept")
DST = os.path.join(ROOT, "Assets/Resources/Art/Dream/UI/Window")

CUTS = [
    # name, mockup, box (l, t, r, b), kind
    ("mock_kid_sprout", "ui_mock_kids", (178, 182, 520, 592), "card"),
    ("mock_kid_dew", "ui_mock_kids", (568, 182, 910, 592), "card"),
    ("mock_kid_request", "ui_mock_kids", (962, 236, 1146, 466), "card"),
    ("mock_kid_evolve", "ui_mock_kids", (966, 650, 1240, 822), "card"),
    ("mock_grow_ring", "ui_mock_grow", (714, 220, 868, 374), "circle"),
    ("mock_grow_tribute", "ui_mock_grow", (700, 485, 869, 635), "circle"),
    ("mock_grow_bless", "ui_mock_grow", (700, 704, 887, 891), "circle"),
    ("mock_grow_evolve", "ui_mock_grow", (194, 772, 312, 885), "circle"),
    ("mock_ws_lock", "ui_mock_workshop", (976, 742, 1126, 891), "card"),
    ("mock_ws_wash", "ui_mock_workshop", (530, 336, 612, 556), "wash"),
]


def mask_round_rect(size, r, feather=2):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((feather, feather, size[0] - 1 - feather, size[1] - 1 - feather), r, fill=255)
    return m.filter(ImageFilter.GaussianBlur(feather))


def mask_circle(size, feather=3):
    m = Image.new("L", size, 0)
    d = min(size) - 2 * feather
    x0, y0 = (size[0] - d) // 2, (size[1] - d) // 2
    ImageDraw.Draw(m).ellipse((x0, y0, x0 + d, y0 + d), fill=255)
    return m.filter(ImageFilter.GaussianBlur(feather))


def main():
    os.makedirs(DST, exist_ok=True)
    out = []
    for name, mock, box, kind in CUTS:
        im = Image.open(os.path.join(SRC, mock + ".png")).convert("RGBA").crop(box)
        if kind == "wash":
            im = im.resize((330, 230), Image.BICUBIC).filter(ImageFilter.GaussianBlur(16))
            im.putalpha(mask_round_rect(im.size, 22, 3))
        elif kind == "card":
            im.putalpha(mask_round_rect(im.size, 22))
        elif kind == "circle":
            im.putalpha(mask_circle(im.size))
        # bigger copy so it stays sharp when the UI scales it up a little
        im = im.resize((im.width * 2, im.height * 2), Image.LANCZOS)
        im.save(os.path.join(DST, name + ".png"))
        out.append(im)
        print(name, im.size)
    if "--preview" in sys.argv:
        p = sys.argv[sys.argv.index("--preview") + 1]
        W = 5; S = 300
        sheet = Image.new("RGBA", (W * S, ((len(out) + W - 1) // W) * S), (70, 80, 70, 255))
        for i, im in enumerate(out):
            t = im.copy(); t.thumbnail((S - 10, S - 10)); sheet.alpha_composite(t, ((i % W) * S + 5, (i // W) * S + 5))
        sheet.save(p)


if __name__ == "__main__":
    main()
