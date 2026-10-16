# itch.io 페이지 에셋 (몽환 수채화판): 배경·배너·커버·소개 띠·맺음 그림.
# 입력: Marketing/itch_dream/{bg,cover_art,intro_art,footer_art,banner_art}.png (사용자가 생성한 원화), Dream/UI/logo.png
# 출력: Marketing/itch_dream/out/
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'Marketing', 'itch_dream')
OUT = os.path.join(SRC, 'out')
FONT = os.path.join(ROOT, 'Assets', 'Fonts')
LOGO = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Dream', 'UI', 'logo.png')
os.makedirs(OUT, exist_ok=True)

def font(name, size): return ImageFont.truetype(os.path.join(FONT, name), size)
HEAD, BODY = 'GowunBatang-Bold.ttf', 'GowunDodum-Regular.ttf'

# 팔레트: 해질녘 숲 남색 배경 + 크림 종이 본문 + 갈색 글씨 (게임 패널과 같은 색)
PAGE_BG = (41, 60, 83)      # bg.png 아래 가장자리 색 = itch BG
PAPER = (250, 237, 214)     # 본문 종이 = itch BG2
INK = (84, 58, 40); INK2 = (122, 96, 74)
MOSS = (107, 134, 66); CORAL = (205, 104, 78); DUSK = (104, 108, 160)

def src(name): return Image.open(os.path.join(SRC, name)).convert('RGB')

def wrap(draw, text, f, width):
    lines, cur = [], ''
    for word in text.split(' '):
        t = (cur + ' ' + word).strip()
        if draw.textlength(t, font=f) <= width: cur = t
        else: lines.append(cur); cur = word
    lines.append(cur)
    return lines

def logo_img(w):
    logo = Image.open(LOGO).convert('RGBA')
    return logo.resize((w, round(logo.height * w / logo.width)), Image.LANCZOS)

def with_shadow(base, img, xy, color=(20, 28, 45, 120), blur=10, dy=8):
    sh = Image.new('RGBA', base.size, (0, 0, 0, 0))
    sh.paste(color, (xy[0], xy[1] + dy), img)
    base = Image.alpha_composite(base, sh.filter(ImageFilter.GaussianBlur(blur)))
    base.alpha_composite(img, xy)
    return base

# 배경: 2560 폭으로 키우고 아래를 PAGE_BG로 이어 붙여 긴 페이지에서도 이음매가 없게
def background():
    im = src('bg.png')
    W = 2560; H = round(im.height * W / im.width)
    im.resize((W, H), Image.LANCZOS).save(os.path.join(OUT, 'background.jpg'), quality=88)

def banner():
    logo = logo_img(720); pad = 24
    out = Image.new('RGBA', (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
    with_shadow(out, logo, (pad, pad), blur=12).save(os.path.join(OUT, 'banner.png'))

# 배너 띠: banner_art.png(가운데를 비워 둔 숲 풍경)를 본문 폭 1280×380으로 자르고 가운데에 로고.
# 로고 뒤에 크림빛 번짐을 깔아 하늘 위에서도 글자가 또렷하게
def banner_strip():
    W, H = 1280, 380
    im = src('banner_art.png')
    h = round(im.width * H / W); top = round(im.height * 0.47 - h / 2)   # 풍경의 가운데 띠
    strip = im.crop((0, top, im.width, top + h)).resize((W, H), Image.LANCZOS).convert('RGBA')
    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((W // 2 - 360, 40, W // 2 + 360, H - 40), fill=PAPER + (150,))
    strip = Image.alpha_composite(strip, glow.filter(ImageFilter.GaussianBlur(60)))
    logo = logo_img(560)
    strip = with_shadow(strip, logo, ((W - logo.width) // 2, (H - logo.height) // 2), blur=12)
    strip.convert('RGB').save(os.path.join(OUT, 'banner_strip.png'))

def cover():
    W, H = 630, 500
    cov = src('cover_art.png').resize((W, H), Image.LANCZOS).convert('RGBA')
    logo = logo_img(380)
    cov = with_shadow(cov, logo, ((W - logo.width) // 2, 18), blur=8, dy=5)
    cov.convert('RGB').save(os.path.join(OUT, 'cover.png'))

# 소개 띠: 2배 해상도(표시 872×280), 오른쪽 종이 칸에 이야기 글
S = 2
def intro():
    W, H = 872 * S, 280 * S
    im = src('intro_art.png')
    im = im.resize((W, round(im.height * W / im.width)), Image.LANCZOS)
    im = im.crop((0, (im.height - H) // 2, W, (im.height - H) // 2 + H))
    # 글 칸 뒤를 종이색으로 한 번 더 바래 그림과 글이 겹쳐 보이지 않게
    g0, g1 = 470 * S, 560 * S
    mask = Image.new('L', (W, H), 0); md = ImageDraw.Draw(mask)
    for x_ in range(g0, W): md.line([(x_, 0), (x_, H)], fill=int(225 * min(1, (x_ - g0) / (g1 - g0)) ** 1.3))
    im.paste(Image.new('RGB', (W, H), PAPER), (0, 0), mask.filter(ImageFilter.GaussianBlur(20 * S)))
    d = ImageDraw.Draw(im)
    x = 535 * S; tw = W - x - 26 * S; y = 40 * S
    d.text((x, y), 'THE STORY', font=font(HEAD, 14 * S), fill=MOSS); y += 26 * S
    f1 = font(HEAD, 22 * S)
    for ln in wrap(d, 'Giant mushrooms swallowed the village.', f1, tw):
        d.text((x, y), ln, font=f1, fill=INK); y += 31 * S
    y += 8 * S
    f2 = font(BODY, 14 * S)
    body = ('An old mushroom hunter picks up his scythe and heads into the forest. '
            'Little mushroom spirits tag along, and a tiny World Tree waits to grow.')
    for ln in wrap(d, body, f2, tw): d.text((x, y), ln, font=f2, fill=INK2); y += 21 * S
    y += 14 * S
    f3 = font(HEAD, 22 * S)
    for t, c in [('Bounce.', DUSK), ('Harvest.', CORAL), ('Grow.', MOSS)]:
        d.text((x, y), t, font=f3, fill=c); x += d.textlength(t + ' ', font=f3)
    im.save(os.path.join(OUT, 'intro.png'))

# 맺음 그림: 위쪽 빈 종이에 인사말
def footer():
    W = 872 * S
    im = src('footer_art.png')
    im = im.resize((W, round(im.height * W / im.width)), Image.LANCZOS)
    im = im.crop((0, 40 * S, W, im.height - 30 * S))
    d = ImageDraw.Draw(im)
    f = font(HEAD, 26 * S); t = 'Thanks for playing!'
    d.text(((W - d.textlength(t, font=f)) / 2, 22 * S), t, font=f, fill=INK)
    f2 = font(BODY, 14 * S); t2 = 'Tell us which little one is your favorite in the comments.'
    d.text(((W - d.textlength(t2, font=f2)) / 2, 62 * S), t2, font=f2, fill=INK2)
    im.save(os.path.join(OUT, 'footer.png'))

if __name__ == '__main__':
    import sys
    steps = sys.argv[1:] or ['background', 'banner', 'banner_strip', 'cover', 'intro', 'footer']
    for st in steps: globals()[st]()
    for n in sorted(x for x in os.listdir(OUT) if x.endswith(('.png', '.jpg', '.gif'))):
        p = os.path.join(OUT, n); print(n, Image.open(p).size, round(os.path.getsize(p) / 1e3), 'KB')
