"""itch 커버 v3 '수확 한 방' — 생성 배경 + 게임 실제 그림만 합성.

python Marketing/thumbnails_2026-10-20/v3/compose.py <bg_name> <out_name> [옵션 key=value ...]
  배경  v3/<bg_name>_raw.png (캐릭터·도구·코인·숫자 없이 생성한 수채 장면)
  정령  Rig2/gen_fire_1_* 부품을 게임 순서(갓→신발→몸→얼굴→팔)로 조립, 얼굴 surprise, 팔 벌림
  수확기 Harvesters/sam.png · 코인 Harvesters/coin.png · 숫자 Fonts/DmgDigitsAtlas.png(게임 데미지 숫자 그림 글자)
  로고  itch_dream/out/banner.png
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image, ImageFilter, ImageDraw, ImageEnhance

HERE = Path(__file__).parent
ROOT = HERE.parents[2]
ART = ROOT / "Assets/Resources/Art/Dream"
RIG = ART / "Characters/Rig2/gen_fire_1_"
LOGO = ROOT / "Marketing/itch_dream/out/banner.png"
DIGITS = ROOT / "Assets/Fonts/DmgDigitsAtlas.png"
W, H = 1260, 1000

# 옵션 기본값 (key=value 로 덮어씀)
O = dict(
    crop=0.5,
    sx=0.80, sy=0.93, sh=0.40, lean=-5, hop=0.015, arm=95,      # 정령: 가운데 x, 발 y, 키(화면비), 기울기°, 뜀, 팔 각도
    hx=0.38, hy=0.66, hs=0.19, hrot=-25,                        # 수확기: 가운데, 지름(화면 폭비), 회전
    lw=0.54, lx=0.03, ly=0.035,                                 # 로고
    nx=0.50, ny=0.42,                                         # 큰 숫자 가운데
    ca0=-70, ca1=-5,                                            # 코인 퍼지는 각도 범위
    seed=7,
)

GLYPHS = [",", "0", "1", "2", "3", "4",
          "5", "6", "7", "8", "9", ".",
          "!", "a", "c", "i", "o", "p",
          "x", "+", "B", "D", "G", "K",
          "M", "N", "O", "Q", "S", "T"]


def load(p):
    return Image.open(p).convert("RGBA")


def fit_h(im, h):
    s = h / im.height
    return im.resize((max(1, round(im.width * s)), max(1, round(h))), Image.LANCZOS)


def glyph_table():
    atlas = load(DIGITS)
    cw, ch = atlas.width // 6, atlas.height // 5
    t = {}
    for i, g in enumerate(GLYPHS):
        c = atlas.crop(((i % 6) * cw, (i // 6) * ch, (i % 6 + 1) * cw, (i // 6 + 1) * ch))
        if c.getbbox():
            t[g] = c.crop(c.getbbox())
    return t


def number(text, height, table):
    """게임 숫자 그림 글자로 문자열을 만든다. 글자 높이 = 숫자 '0' 기준."""
    s = height / table["0"].height
    parts = [table[ch].resize((round(table[ch].width * s), round(table[ch].height * s)), Image.LANCZOS) for ch in text]
    gap = round(-0.06 * height)
    w = sum(p.width for p in parts) + gap * (len(parts) - 1)
    base = round(height)
    out = Image.new("RGBA", (w, base + round(height * 0.2)), (0, 0, 0, 0))
    x = 0
    for ch, p in zip(text, parts):
        y = base - p.height if ch not in ",." else base - p.height
        if ch == "+":
            y = (base - p.height) // 2
        out.alpha_composite(p, (x, max(0, y)))
        x += p.width + gap
    return out


def spirit(height, arm):
    def L(n):
        return load(f"{RIG}{n}.png")
    c = Image.new("RGBA", (432, 384), (0, 0, 0, 0))
    c.alpha_composite(L("cap"))
    c.alpha_composite(L("footL")); c.alpha_composite(L("footR"))
    c.alpha_composite(L("stem")); c.alpha_composite(L("face_surprise"))
    # 팔 피벗 (spirit_rig2.json nubPivotL/R → 100px/unit, 캔버스 가운데 = (0.02, 1.82))
    c.alpha_composite(L("nubL").rotate(-arm, Image.BICUBIC, center=(142, 288)))
    c.alpha_composite(L("nubR").rotate(arm, Image.BICUBIC, center=(301, 286)))
    big = Image.new("RGBA", (560, 520), (0, 0, 0, 0))
    big.alpha_composite(c, (64, 100))
    big = big.crop(big.getbbox())
    return fit_h(big, height).filter(ImageFilter.UnsharpMask(radius=2, percent=35, threshold=3))


def soft_shadow(cover, cx, cy, rw, rh, alpha=90, blur=14):
    sh = Image.new("RGBA", cover.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - rw, cy - rh, cx + rw, cy + rh), fill=(40, 55, 35, alpha))
    cover.alpha_composite(sh.filter(ImageFilter.GaussianBlur(blur)))


def paste_c(cover, im, cx, cy):
    cover.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))


def logo_img(width):
    logo = load(LOGO)
    logo = logo.resize((width, round(logo.height * width / logo.width)), Image.LANCZOS)
    pad = 40
    canvas = Image.new("RGBA", (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
    a = logo.split()[3]
    for blur, alpha, dy in ((20, 110, 5), (5, 130, 6)):
        sh = Image.new("RGBA", logo.size, (50, 32, 20, 0))
        sh.putalpha(a.point(lambda v: v * alpha // 255))
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(sh, (pad, pad + dy), sh)
        canvas = Image.alpha_composite(canvas, layer.filter(ImageFilter.GaussianBlur(blur)))
    canvas.alpha_composite(logo, (pad, pad))
    return canvas, pad


def crop_cover(im, ax):
    r = W / H
    w, h = im.size
    if w / h > r:
        nw = round(h * r); x = round((w - nw) * ax); im = im.crop((x, 0, x + nw, h))
    else:
        nh = round(w / r); y = round((h - nh) * 0.5); im = im.crop((0, y, w, y + nh))
    return im.resize((W, H), Image.LANCZOS)


def main():
    bg, name = sys.argv[1], sys.argv[2]
    for kv in sys.argv[3:]:
        k, v = kv.split("="); O[k] = type(O[k])(float(v)) if isinstance(O[k], float) else int(float(v))
    rnd = random.Random(O["seed"])
    cover = crop_cover(Image.open(HERE / f"{bg}_raw.png").convert("RGB"), O["crop"]).convert("RGBA")
    table = glyph_table()

    # 수확기 — 지나온 자리에 옅은 잔상 2개 + 본체
    hd = round(W * O["hs"])
    harv = load(ART / "Harvesters/sam.png")
    harv = harv.resize((hd, round(harv.height * hd / harv.width)), Image.LANCZOS)
    hx, hy = W * O["hx"], H * O["hy"]
    for i, (dx, dy, a) in enumerate(((-0.16, 0.10, 60), (-0.08, 0.05, 105))):
        g = harv.rotate(O["hrot"] + 40 * (2 - i), Image.BICUBIC).filter(ImageFilter.GaussianBlur(6 - i * 2))
        g.putalpha(g.split()[3].point(lambda v: v * a // 255))
        paste_c(cover, g, hx + dx * W, hy + dy * H)
    soft_shadow(cover, hx + hd * 0.08, hy + hd * 0.55, hd * 0.42, hd * 0.08, 70, 10)
    paste_c(cover, harv.rotate(O["hrot"], Image.BICUBIC, expand=True), hx, hy)

    # 코인 — 수확기에서 오른쪽 위로 솟는 부채꼴
    coin = load(ART / "Harvesters/coin.png")
    for i in range(14):
        t = i / 10
        ang = math.radians(O["ca0"] + (O["ca1"] - O["ca0"]) * t + rnd.uniform(-6, 6))
        dist = W * (0.09 + 0.22 * rnd.random())
        cx, cy = hx + math.cos(ang) * dist, hy + math.sin(ang) * dist * 0.9
        if cx < W * (O["lx"] + O["lw"] + 0.02) and cy < H * 0.36:  # 로고 자리는 비운다
            continue
        sz = round(W * rnd.uniform(0.035, 0.065))
        c = coin.resize((sz, sz), Image.LANCZOS).rotate(rnd.uniform(0, 360), Image.BICUBIC, expand=True)
        if rnd.random() < 0.3:  # 납작하게 뒤집힌 코인
            c = c.resize((max(4, round(c.width * rnd.uniform(0.35, 0.7))), c.height), Image.LANCZOS)
        paste_c(cover, c, cx, cy)

    # 숫자 팝업 (게임 데미지 숫자 그림 글자)
    for text, x, y, hgt in (("+590K", O["nx"], O["ny"], 0.11), ("+24.6K", 0.17, 0.55, 0.058), ("+21.3K", 0.33, 0.70, 0.05)):
        n = number(text, H * hgt, table)
        paste_c(cover, n, W * x, H * y)

    # 정령 — 놀라서 살짝 뛰어오름, 몸은 수확기 반대쪽으로 젖힘
    sp = spirit(round(H * O["sh"]), O["arm"])
    sp = sp.rotate(O["lean"], Image.BICUBIC, expand=True)
    sx, foot = W * O["sx"], H * O["sy"]
    soft_shadow(cover, sx, foot, sp.width * 0.36, sp.width * 0.05, 85, 12)
    paste_c(cover, sp, sx, foot - H * O["hop"] - sp.height / 2)
    ex = number("!", H * 0.11, table).rotate(-10, Image.BICUBIC, expand=True)
    paste_c(cover, ex, sx + sp.width * 0.18, foot - H * O["hop"] - sp.height + H * 0.01)

    if O["lw"] > 0:
        logo, pad = logo_img(round(W * O["lw"]))
        cover.alpha_composite(logo, (round(W * O["lx"]) - pad, round(H * O["ly"]) - pad))

    cover = cover.convert("RGB")
    out = HERE / "out"; out.mkdir(exist_ok=True)
    cover.save(out / f"cover_{name}_1260x1000.png")
    sharp = ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2)
    cover.resize((630, 500), Image.LANCZOS).filter(sharp).save(out / f"cover_{name}_630x500.png")
    cover.resize((315, 250), Image.LANCZOS).filter(sharp).save(out / f"preview_{name}_315x250.png")
    print("saved", name)


main()
