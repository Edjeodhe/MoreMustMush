# itch.io 페이지 에셋 만들기: 배경·배너·커버·인트로·스크린샷·메카닉 GIF 카드(2×2 세로 카드).
# 입력: Temp/itchcap/<clip>/f00001.png… (Unity 플레이 모드 녹화, 25fps 1920×1080), Art/ 원화
# 출력: Marketing/itch/   (본문 글·제목·목록은 이미지가 아니라 description.html의 진짜 텍스트)
import os, subprocess, shutil, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CAP = os.path.join(ROOT, 'Temp', 'itchcap')
OUT = os.path.join(ROOT, 'Marketing', 'itch')
WORK = os.path.join(CAP, '_work')
FONT = os.path.join(ROOT, 'Assets', 'Fonts')
os.makedirs(OUT, exist_ok=True); os.makedirs(WORK, exist_ok=True)

def font(name, size): return ImageFont.truetype(os.path.join(FONT, name), size)
BLACK, SEMI = 'Nunito-Black.ttf', 'Nunito-SemiBold.ttf'

# 팔레트: 흰 본문 + 숲 초록 글씨, 단계 색은 로고 색(하늘·초록·분홍·노랑)
WHITE = (255, 255, 255); EDGE = (214, 233, 196)
INK = (38, 58, 44); INK2 = (94, 111, 96)
SKY = (46, 155, 230); LEAF = (59, 165, 92); PINK = (236, 76, 138); SUN = (242, 165, 26)
PAGE_BG = (93, 158, 58)   # 배경 이미지 아래쪽 풀색 = itch 페이지 배경색

def rounded_mask(size, r, scale=4):
    w, h = size
    m = Image.new('L', (w * scale, h * scale), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, w * scale - 1, h * scale - 1], r * scale, fill=255)
    return m.resize(size, Image.LANCZOS)

def wrap(draw, text, f, width):
    lines = []
    for para in text.split('\n'):
        cur = ''
        for word in para.split(' '):
            t = (cur + ' ' + word).strip()
            if draw.textlength(t, font=f) <= width: cur = t
            else: lines.append(cur); cur = word
        lines.append(cur)
    return lines

def card_base(w, h, r=18, border=2, fill=WHITE, edge=EDGE):
    s = 4
    im = Image.new('RGBA', (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], r * s, fill=edge)
    d.rounded_rectangle([border * s, border * s, w * s - 1 - border * s, h * s - 1 - border * s], (r - border) * s, fill=fill)
    return im.resize((w, h), Image.LANCZOS)

# ---------------------------------------------------------------- 배경
def build_background():
    src = Image.open(os.path.join(ROOT, 'Art', 'Raw_Soft', 'bg_title.png')).convert('RGB')
    W = 1920; H = round(src.height * W / src.width)
    bg = src.resize((W, H), Image.LANCZOS)
    out = Image.new('RGB', (W, H + 260), PAGE_BG)
    out.paste(bg, (0, 0))
    # 아래 300px를 풀색으로 부드럽게 녹인다 (이음매 없이 단색 배경으로 이어짐)
    fade = 300
    grad = Image.new('L', (1, fade))
    for y in range(fade): grad.putpixel((0, y), int(255 * (y / (fade - 1)) ** 1.6))
    grad = grad.resize((W, fade))
    region = out.crop((0, H - fade, W, H))
    out.paste(Image.composite(Image.new('RGB', (W, fade), PAGE_BG), region, grad), (0, H - fade))
    out.save(os.path.join(OUT, 'background.jpg'), quality=90)

# ---------------------------------------------------------------- 배너·커버
def logo_img(w):
    logo = Image.open(os.path.join(ROOT, 'Art', 'StyleRef', 'logo.png')).convert('RGBA')
    return logo.resize((w, round(logo.height * w / logo.width)), Image.LANCZOS)

def build_banner():
    logo = logo_img(760); pad = 20
    out = Image.new('RGBA', (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
    sh = Image.new('RGBA', out.size, (0, 0, 0, 0)); sh.paste((40, 60, 90, 90), (pad, pad + 8), logo)
    out = Image.alpha_composite(out, sh.filter(ImageFilter.GaussianBlur(10)))
    out.alpha_composite(logo, (pad, pad))
    out.save(os.path.join(OUT, 'banner.png'))

def build_cover():
    W, H = 630, 500
    src = Image.open(os.path.join(ROOT, 'Art', 'Raw_Soft', 'bg_title.png')).convert('RGB')
    s = H * 1.18 / src.height
    big = src.resize((round(src.width * s), round(src.height * s)), Image.LANCZOS)
    x0 = big.width - W - 10; y0 = big.height - H
    cov = big.crop((x0, y0, x0 + W, y0 + H)).convert('RGBA')
    gp = Image.open(os.path.join(ROOT, 'Art', 'Raw_Soft', 'grandpa_full.png')).convert('RGBA').resize((300, 300), Image.LANCZOS)
    cov.alpha_composite(gp, (-30, H - 290))
    logo = logo_img(470); lw = logo.width
    sh = Image.new('RGBA', cov.size, (0, 0, 0, 0)); sh.paste((30, 50, 80, 110), ((W - lw) // 2, 28), logo)
    cov = Image.alpha_composite(cov, sh.filter(ImageFilter.GaussianBlur(8)))
    cov.alpha_composite(logo, ((W - lw) // 2, 22))
    cov.convert('RGB').save(os.path.join(OUT, 'cover.png'))

# ---------------------------------------------------------------- 인트로 (2배 해상도, 표시 폭 872)
S2 = 2
def intro():
    W, H = 872 * S2, 280 * S2
    im = card_base(W, H, 20 * S2, 2 * S2)
    # 왼쪽에 하늘색 둥근 무대를 깔고 할아버지를 세운다
    stage = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).rounded_rectangle([12 * S2, 12 * S2, 290 * S2, H - 12 * S2], 14 * S2, fill=255)
    sky = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(sky)
    for y in range(H): gd.line([(0, y), (W, y)], fill=(int(150 + 80 * y / H), int(212 + 30 * y / H), int(246 - 40 * y / H), 255))
    stage.paste(sky, (0, 0), m)
    im = Image.alpha_composite(im, stage)
    gp = Image.open(os.path.join(ROOT, 'Art', 'Raw_Soft', 'grandpa_full.png')).convert('RGBA')
    gs = 286 * S2
    im.alpha_composite(gp.resize((gs, gs), Image.LANCZOS), (8 * S2, H - gs + 4 * S2))
    # 둥근 무대 밖으로 나간 할아버지 아래쪽은 카드 테두리 안으로 자른다
    im.putalpha(rounded_mask((W, H), 20 * S2))
    d = ImageDraw.Draw(im)
    x = 320 * S2; y = 40 * S2; tw = W - x - 36 * S2
    d.text((x, y), 'THE STORY', font=font(BLACK, 15 * S2), fill=LEAF); y += 28 * S2
    d.text((x, y), 'Giant mushrooms swallowed the village.', font=font(BLACK, 25 * S2), fill=INK); y += 44 * S2
    f2 = font(SEMI, 17 * S2)
    body = ('An old mushroom hunter dusts off his trusty scythe and heads into the forest. '
            'Launch your harvester, bounce it through the caps and turn every last mushroom into gold.')
    for ln in wrap(d, body, f2, tw): d.text((x, y), ln, font=f2, fill=INK2); y += 27 * S2
    y += 18 * S2
    tag = [('Bounce.', SKY), ('Harvest.', PINK), ('Grow.', LEAF)]
    f3 = font(BLACK, 30 * S2)
    for t, c in tag:
        d.text((x, y), t, font=f3, fill=c); x += d.textlength(t + ' ', font=f3)
    im.save(os.path.join(OUT, 'intro.png'))

# ---------------------------------------------------------------- 스크린샷 (itch 오른쪽 열)
SHOTS = [('mid', 120, 'shot1_harvest.jpg'), ('late', 90, 'shot2_fever.jpg'),
         ('tree', 240, 'shot3_tree.jpg'), ('farm', 60, 'shot4_worldtree.jpg')]
def screenshots():
    for clip, n, name in SHOTS:
        fp = os.path.join(CAP, clip, f'f{n:05d}.png')
        if os.path.exists(fp): Image.open(fp).convert('RGB').save(os.path.join(OUT, name), quality=90)

# ---------------------------------------------------------------- 메카닉 GIF 카드 (세로 424, 두 장씩 나란히)
CW, CH = 424, 372
CLIP_W, CLIP_H = 400, 225
CLIP_X, CLIP_Y = 12, 12

def card_layers(num, title, body, color):
    bg = card_base(CW, CH)
    d = ImageDraw.Draw(bg)
    x = 18; y = CLIP_Y + CLIP_H + 18
    tag = f'STEP {num}'
    ft = font(BLACK, 12)
    tw = d.textlength(tag, font=ft)
    d.rounded_rectangle([x, y, x + tw + 18, y + 22], 11, fill=color)
    d.text((x + 9, y + 11), tag, font=ft, fill='white', anchor='lm')
    d.text((x + tw + 30, y + 10), title, font=font(BLACK, 24), fill=INK, anchor='lm')
    y += 36
    f = font(SEMI, 15)
    for ln in wrap(d, body, f, CW - x * 2): d.text((x, y), ln, font=f, fill=INK2); y += 22
    fg = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    hole = rounded_mask((CLIP_W, CLIP_H), 12)
    corner = Image.new('RGBA', (CLIP_W, CLIP_H), WHITE + (255,))
    corner.putalpha(Image.eval(hole, lambda a: 255 - a))
    fg.alpha_composite(corner, (CLIP_X, CLIP_Y))
    return bg, fg

CURSOR = None
def cursor_img():
    global CURSOR
    if CURSOR: return CURSOR
    s = 4; w, h = 26, 36
    im = Image.new('RGBA', (w * s, h * s), (0, 0, 0, 0))
    pts = [(2, 2), (2, 28), (9, 22), (14, 33), (19, 31), (14, 20), (23, 20)]
    ImageDraw.Draw(im).polygon([(x * s, y * s) for x, y in pts], fill='white', outline=(30, 30, 30), width=2 * s)
    CURSOR = im.resize((w, h), Image.LANCZOS)
    return CURSOR

def make_clip(clip, start, count, crop, step=2, cursor_log=None, click_frames=()):
    """녹화 프레임에서 잘라 CLIP 크기로 줄인 PNG 시퀀스를 만든다. step=2 → 12.5fps."""
    src = os.path.join(CAP, clip)
    dst = os.path.join(WORK, clip); shutil.rmtree(dst, ignore_errors=True); os.makedirs(dst)
    curs = {}
    if cursor_log:
        for ln in open(cursor_log):
            p = ln.split()
            if p and p[0] == 'c': curs[int(p[1]) + 1] = (float(p[2]), float(p[3]))
    x0, y0, x1, y1 = crop
    k = 0
    for i in range(start, start + count, step):
        fp = os.path.join(src, f'f{i:05d}.png')
        if not os.path.exists(fp): break
        im = Image.open(fp).convert('RGBA')
        if i in curs:
            cx, cy = curs[i]
            if any(0 <= i - c < 6 for c in click_frames):
                rr = Image.new('RGBA', im.size, (0, 0, 0, 0))
                ImageDraw.Draw(rr).ellipse([cx - 26, cy - 26, cx + 26, cy + 26], outline=(255, 230, 120, 230), width=5)
                im = Image.alpha_composite(im, rr)
            im.alpha_composite(cursor_img().resize((39, 54), Image.LANCZOS), (int(cx) - 3, int(cy) - 3))
        im = im.crop((x0, y0, x1, y1)).resize((CLIP_W, CLIP_H), Image.LANCZOS)
        k += 1; im.convert('RGB').save(os.path.join(dst, f'c{k:04d}.png'))
    return dst, k

def make_card_gif(name, num, title, body, color, clip_dir, fps, colors=200):
    bg, fg = card_layers(num, title, body, color)
    frames = sorted(f for f in os.listdir(clip_dir) if f.startswith('c'))
    dst = os.path.join(WORK, name + '_card'); shutil.rmtree(dst, ignore_errors=True); os.makedirs(dst)
    for i, f in enumerate(frames, 1):
        im = bg.copy()
        im.alpha_composite(Image.open(os.path.join(clip_dir, f)).convert('RGBA'), (CLIP_X, CLIP_Y))
        im.alpha_composite(fg)
        # 카드 바깥 모서리는 본문색(흰 반투명 위)과 섞이도록 흰색으로 채운다
        flat = Image.new('RGB', im.size, WHITE); flat.paste(im, (0, 0), im)
        flat.save(os.path.join(dst, f'k{i:04d}.png'))
    out = os.path.join(OUT, name + '.gif')
    flt = 'split[s0][s1];[s0]palettegen=max_colors=%d:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle' % colors
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-framerate', str(fps), '-i', os.path.join(dst, 'k%04d.png'),
                    '-filter_complex', flt, '-loop', '0', out], check=True)
    print(name, round(os.path.getsize(out) / 1e6, 2), 'MB')

STEPS = {
    'step1_launch': dict(num=1, title='Launch', color=SKY,
        body='Pull back the harvester and let go. Slide the bar with your mouse to keep it bouncing through the mushrooms.'),
    'step2_upgrade': dict(num=2, title='Upgrade', color=LEAF,
        body='Spend your gold on the Mycelium Tree. Each node makes your harvesters stronger, faster and more numerous.'),
    'step3_fever': dict(num=3, title='Fever!', color=PINK,
        body='Chain hits into huge combos to set off FEVER, and watch the whole forest pop at once.'),
    'step4_worldtree': dict(num=4, title='World Tree', color=SUN,
        body='Grow the World Tree with your harvest. Little critters move in and keep gathering while you are away.'),
}
PLAN = {
    'step1_launch': dict(clip='mid', start=1, count=150, crop=(400, 300, 1680, 1020)),
    'step2_upgrade': dict(clip='tree', start=1, count=250, crop=(320, 160, 1600, 880), cursor=True),
    'step3_fever': dict(clip='late', start=40, count=110, crop=(400, 300, 1680, 1020), colors=128),
    'step4_worldtree': dict(clip='farm', start=1, count=150, crop=(180, 260, 1180, 822)),
}

def build_gifs(only=None):
    for name, p in PLAN.items():
        if only and name not in only: continue
        if not os.path.isdir(os.path.join(CAP, p['clip'])): print('skip', name); continue
        clog = os.path.join(CAP, 'tree_cursor.txt') if p.get('cursor') else None
        clicks = []
        if clog:
            for ln in open(clog):
                q = ln.split()
                if len(q) == 3 and q[1] == 'buy': clicks.append(int(q[0]) + 1)
        d, k = make_clip(p['clip'], p['start'], p['count'], p['crop'], cursor_log=clog, click_frames=clicks)
        s = STEPS[name]
        make_card_gif(name, s['num'], s['title'], s['body'], s['color'], d, 12.5, p.get('colors', 200))

if __name__ == '__main__':
    only = sys.argv[1:]
    if not only or 'static' in only:
        build_background(); build_banner(); build_cover(); intro(); screenshots()
    if not only or 'gif' in only or any(o.startswith('step') for o in only):
        build_gifs([o for o in only if o.startswith('step')] or None)
