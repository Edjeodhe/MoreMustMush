# itch 데브로그 커버 (16:9, 1280×720): 몽환 숲 배경 + 왼쪽 로고·제목 + 오른쪽 실제 게임 스크린샷 4장(폴라로이드)
# 입력: Marketing/itch_dream/bg.png, Marketing/itch_dream/out/shots_v2/*.jpg, 게임 로고
# 출력: Marketing/itch_dream/devlog_2026-10-26/cover.png
# 사용: python Tools/itch/build_devlog_cover.py
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
sys.path.insert(0, os.path.dirname(__file__))
import build_itch_dream as dream

SHOTS = os.path.join(dream.OUT, 'shots_v2')
DST = os.path.join(dream.SRC, 'devlog_2026-10-26')
W, H = 1280, 720
CREAM = (255, 246, 228)
# (파일, 자를 곳(1920×1080 기준, 16:9), 이름표)
CARDS = [
    ('shot1_decorate.jpg', (380, 150, 1580, 825), 'Decorate the World Tree'),
    ('shot2_growth.jpg', (120, 110, 1560, 920), 'Grow it to Tier 8'),
    ('shot3_boss.jpg', (380, 20, 1620, 718), 'Fight Boss Mushrooms'),
    ('shot4_idle_reward.jpg', (300, 70, 1620, 812), 'Collect Idle Rewards'),
]

def card(path, crop, label, iw):
    shot = Image.open(os.path.join(SHOTS, path)).convert('RGB').crop(crop)
    ih = round(iw * 9 / 16)
    shot = shot.resize((iw, ih), Image.LANCZOS)
    pad, lab = 10, 44
    c = Image.new('RGBA', (iw + pad * 2, ih + pad + lab), CREAM + (255,))
    c.paste(shot, (pad, pad))
    d = ImageDraw.Draw(c)
    d.text((c.width // 2, ih + pad + lab // 2), label, font=dream.font(dream.HEAD, 25), fill=dream.INK, anchor='mm')
    return c

def build():
    os.makedirs(DST, exist_ok=True)
    bg = dream.src('bg.png')
    s = max(W / bg.width, H / bg.height)
    bg = bg.resize((round(bg.width * s), round(bg.height * s)), Image.LANCZOS)
    bg = bg.crop(((bg.width - W) // 2, (bg.height - H) // 2, (bg.width - W) // 2 + W, (bg.height - H) // 2 + H))
    bg = ImageEnhance.Brightness(bg.filter(ImageFilter.GaussianBlur(3))).enhance(0.72)
    im = bg.convert('RGBA')

    # 왼쪽: 로고 + 제목 + 한 줄 소개
    logo = dream.logo_img(400)
    im = dream.with_shadow(im, logo, (36, 40), blur=10)
    d = ImageDraw.Draw(im)
    y = 40 + logo.height + 26
    f = dream.font(dream.HEAD, 62)
    for ln in ['Beyond', 'the Harvest']:
        d.text((44, y + 3), ln, font=f, fill=(20, 28, 45))
        d.text((42, y), ln, font=f, fill=CREAM); y += 72
    y += 14
    fb = dream.font(dream.BODY, 27)
    for ln in ['Decorate, grow, battle', 'and relax in the', 'cozy mushroom forest']:
        d.text((44, y), ln, font=fb, fill=(236, 222, 196)); y += 36

    # 오른쪽: 2×2 폴라로이드 (살짝 기울여서)
    iw = 344; tilts = [-2.2, 1.8, 1.6, -1.8]
    pos = [(486, 58), (872, 74), (500, 382), (884, 372)]
    for (p, crop, label), t, xy in zip(CARDS, tilts, pos):
        c = card(p, crop, label, iw).rotate(t, resample=Image.BICUBIC, expand=True)
        im = dream.with_shadow(im, c, xy, blur=9, dy=7)
    out = os.path.join(DST, 'cover.png')
    im.convert('RGB').save(out)
    print(out, im.size)

if __name__ == '__main__':
    build()
