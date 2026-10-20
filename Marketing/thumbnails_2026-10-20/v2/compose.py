"""새 수채 배경(*_raw.png) + 게임 실제 정령 그림(Characters/Evo) 오른쪽 배치 + 로고(왼쪽 위) → itch 커버 3종.

python Marketing/thumbnails_2026-10-20/v2/compose.py <bg> <out_name> <spirit:x:foot_y:height>... [--logo w,x,y] [--crop ax]
  spirit  = evo 파일 이름(evo_fire_1). x = 캐릭터 가운데 x(0~1), foot_y = 발바닥 y(0~1), height = 키(화면 높이 비율)
  앞에 적은 것이 뒤에 그려진다.
"""
import sys
from pathlib import Path
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw

HERE = Path(__file__).parent
ROOT = HERE.parents[2]
EVO = ROOT / "Assets/Resources/Art/Dream/Characters/Evo"
LOGO = ROOT / "Marketing/itch_dream/out/banner.png"
W, H = 1260, 1000


def crop_cover(im, ax=0.5):
    r = W / H
    w, h = im.size
    if w / h > r:
        nw = round(h * r)
        x = round((w - nw) * ax)
        im = im.crop((x, 0, x + nw, h))
    else:
        nh = round(w / r)
        y = round((h - nh) * 0.5)
        im = im.crop((0, y, w, y + nh))
    return im.resize((W, H), Image.LANCZOS)


def spirit(name, height_px):
    sp = Image.open(EVO / f"{name}.png").convert("RGBA")
    sp = sp.crop(sp.getbbox())
    s = height_px / sp.height
    sp = sp.resize((round(sp.width * s), round(sp.height * s)), Image.LANCZOS)
    # 키운 뒤 무른 붓 자국을 살짝 되살린다
    return sp.filter(ImageFilter.UnsharpMask(radius=2, percent=40, threshold=3))


def place(cover, name, cx, foot, hfrac):
    sp = spirit(name, round(H * hfrac))
    x, y = round(W * cx - sp.width / 2), round(H * foot - sp.height)
    # 발밑 그림자: 수채 번짐처럼 넓고 옅게
    sh = Image.new("RGBA", cover.size, (0, 0, 0, 0))
    ew, eh = sp.width * 0.42, sp.height * 0.045
    ImageDraw.Draw(sh).ellipse((W * cx - ew, H * foot - eh, W * cx + ew, H * foot + eh), fill=(60, 70, 40, 95))
    cover.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
    cover.alpha_composite(sp, (x, y))


def shadowed_logo(width):
    logo = Image.open(LOGO).convert("RGBA")
    logo = logo.resize((width, round(logo.height * width / logo.width)), Image.LANCZOS)
    pad = 40
    canvas = Image.new("RGBA", (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
    a = logo.split()[3]
    for blur, alpha, dy in ((20, 90, 5), (5, 120, 6)):
        sh = Image.new("RGBA", logo.size, (60, 40, 25, 0))
        sh.putalpha(a.point(lambda v: v * alpha // 255))
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(sh, (pad, pad + dy), sh)
        canvas = Image.alpha_composite(canvas, layer.filter(ImageFilter.GaussianBlur(blur)))
    canvas.alpha_composite(logo, (pad, pad))
    return canvas, pad


def main():
    args = sys.argv[1:]
    bg, out_name = args[0], args[1]
    logo_w, logo_x, logo_y, ax = 0.58, 0.03, 0.04, 0.5
    sprites = []
    i = 2
    while i < len(args):
        if args[i] == "--logo":
            logo_w, logo_x, logo_y = map(float, args[i + 1].split(",")); i += 2
        elif args[i] == "--crop":
            ax = float(args[i + 1]); i += 2
        else:
            n, cx, fy, hf = args[i].split(":"); sprites.append((n, float(cx), float(fy), float(hf))); i += 1
    cover = crop_cover(Image.open(HERE / f"{bg}_raw.png").convert("RGB"), ax).convert("RGBA")
    for s in sprites:
        place(cover, *s)
    logo, pad = shadowed_logo(round(W * logo_w))
    cover.alpha_composite(logo, (round(W * logo_x) - pad, round(H * logo_y) - pad))
    cover = cover.convert("RGB")
    out = HERE / "out"
    out.mkdir(exist_ok=True)
    cover.save(out / f"cover_{out_name}_1260x1000.png")
    sharp = ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2)
    cover.resize((630, 500), Image.LANCZOS).filter(sharp).save(out / f"cover_{out_name}_630x500.png")
    cover.resize((315, 250), Image.LANCZOS).filter(sharp).save(out / f"preview_{out_name}_315x250.png")
    print("saved", out_name)


main()
