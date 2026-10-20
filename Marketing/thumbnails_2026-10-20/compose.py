"""생성 그림(*_raw.png)을 1.26:1로 자르고 로고를 합성해 itch 커버 3종 크기로 내보낸다.

python Marketing/thumbnails_2026-10-20/compose.py A_hero [logo_w=0.70] [logo_y=0.03] [logo_x=center|left|right] [crop_x=0.5] [extend_top=0] [vignette=0]
"""
import sys
from pathlib import Path
from PIL import Image, ImageFilter, ImageEnhance

HERE = Path(__file__).parent
LOGO = HERE.parent / "itch_dream/out/banner.png"
W, H = 1260, 1000


def extend_top(im, frac):
    """위 가장자리 띠(단색에 가까운 하늘)를 늘려 로고 자리를 만든다."""
    if frac <= 0:
        return im
    w, h = im.size
    add = round(h * frac)
    band = im.crop((0, 0, w, round(h * 0.06))).resize((w, add + round(h * 0.06)), Image.BICUBIC)
    band = band.filter(ImageFilter.GaussianBlur(3))
    out = Image.new("RGB", (w, h + add))
    out.paste(band, (0, 0))
    # 이어 붙인 경계를 부드럽게
    mask = Image.linear_gradient("L").resize((w, round(h * 0.06)))
    out.paste(im, (0, add))
    seam = band.crop((0, add, w, add + round(h * 0.06)))
    out.paste(seam, (0, add), Image.eval(mask, lambda v: 255 - v))
    return out


def crop_cover(im, ax=0.5):
    """가운데 1.26:1. 세로가 남으면 위를 살린다(로고 자리)."""
    r = W / H
    w, h = im.size
    if w / h > r:
        nw = round(h * r)
        x = round((w - nw) * ax)
        im = im.crop((x, 0, x + nw, h))
    else:
        nh = round(w / r)
        im = im.crop((0, 0, w, nh))
    return im.resize((W, H), Image.LANCZOS)


def vignette(im, strength):
    """가장자리를 짙게 눌러 가운데 주인공으로 시선을 모은다."""
    if strength <= 0:
        return im
    m = Image.radial_gradient("L").resize(im.size, Image.BICUBIC)  # 가운데 0 → 바깥 255
    m = m.point(lambda v: int(max(0, v - 90) * 255 / 165 * strength))
    dark = Image.new("RGB", im.size, (10, 34, 38))
    return Image.composite(dark, im, m)


def shadowed_logo(width):
    logo = Image.open(LOGO).convert("RGBA")
    logo = logo.resize((width, round(logo.height * width / logo.width)), Image.LANCZOS)
    pad = 40
    canvas = Image.new("RGBA", (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
    # 진한 그림자 + 넓은 어두운 후광: 밝은 배경에서도 글자가 떠 보이게
    a = logo.split()[3]
    for blur, alpha, dy in ((22, 120, 6), (6, 170, 8)):
        sh = Image.new("RGBA", logo.size, (38, 24, 16, 0))
        sh.putalpha(a.point(lambda v: v * alpha // 255))
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(sh, (pad, pad + dy), sh)
        canvas = Image.alpha_composite(canvas, layer.filter(ImageFilter.GaussianBlur(blur)))
    canvas.alpha_composite(logo, (pad, pad))
    return canvas, pad


def main():
    name = sys.argv[1]
    lw = float(sys.argv[2]) if len(sys.argv) > 2 else 0.70
    ly = float(sys.argv[3]) if len(sys.argv) > 3 else 0.03
    lx = sys.argv[4] if len(sys.argv) > 4 else "center"
    ax = float(sys.argv[5]) if len(sys.argv) > 5 else 0.5
    ext = float(sys.argv[6]) if len(sys.argv) > 6 else 0.0
    art = crop_cover(extend_top(Image.open(HERE / f"{name}_raw.png").convert("RGB"), ext), ax)
    art = ImageEnhance.Contrast(art).enhance(1.06)
    art = vignette(art, float(sys.argv[7]) if len(sys.argv) > 7 else 0.0)
    cover = art.convert("RGBA")
    logo, pad = shadowed_logo(round(W * lw))
    x = {"center": (W - logo.width) // 2, "left": 18 - pad // 2, "right": W - logo.width - 18 + pad // 2}[lx]
    cover.alpha_composite(logo, (x, round(H * ly) - pad))
    cover = cover.convert("RGB")
    out = HERE / "out"
    out.mkdir(exist_ok=True)
    cover.save(out / f"cover_{name}_1260x1000.png")
    # 줄이면 윤곽이 물러지므로 작은 크기는 살짝 선명하게
    sharp = ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2)
    cover.resize((630, 500), Image.LANCZOS).filter(sharp).save(out / f"cover_{name}_630x500.png")
    cover.resize((315, 250), Image.LANCZOS).filter(sharp).save(out / f"preview_{name}_315x250.png")
    print("saved", name)


main()
