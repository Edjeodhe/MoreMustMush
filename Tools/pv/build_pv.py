"""MoreMustMush 30초 PV 편집 (콘티: Marketing/PV/PV_콘티.md).

python Tools/pv/build_pv.py [--preview] [--from 0 --to 30] [--stills t1,t2,...]
  --preview  : 960x540 빠른 렌더
  --stills   : 해당 시각 프레임만 PNG로 (Marketing/PV/out/still_*.png)

녹화 프레임: Temp/pvcap/<이름>/00000.jpg … (30fps, 1920x1080)
결과: Marketing/PV/out/MoreMustMush_PV_30s.mp4
"""
import math, os, sys, subprocess, random, argparse, glob, functools
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
P = lambda *a: os.path.join(ROOT, *a)
DREAM = P('Assets', 'Resources', 'Art', 'Dream')
CAP = P('Temp', 'pvcap')
ART = P('Marketing', 'PV', 'art')
OUT = P('Marketing', 'PV', 'out')
FONT = P('Assets', 'Fonts', 'Jua-Regular.ttf')

W, H, FPS, DUR = 1920, 1080, 30, 30.0
BEAT = 0.5

CREAM = (251, 240, 217)
BROWN = (78, 46, 31)
CORAL = (233, 124, 96)
SAGE = (150, 178, 132)

# ---------- 공용 ----------
@functools.lru_cache(None)
def img(path):
    return Image.open(path).convert('RGBA')

def dream(rel):
    return img(os.path.join(DREAM, rel))

def ease_out_back(x, k=1.9):
    x = max(0.0, min(1.0, x)); x -= 1
    return 1 + (k + 1) * x ** 3 + k * x ** 2

def ease_out(x):
    x = max(0.0, min(1.0, x)); return 1 - (1 - x) ** 3

def ease_in_out(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

def cover(im, w=W, h=H, zoom=1.0, cx=0.5, cy=0.5):
    """im을 w×h에 꽉 차게 (zoom ≥ 1이면 더 확대), 중심 cx,cy (0..1)"""
    s = max(w / im.width, h / im.height) * zoom
    nw, nh = max(w, int(im.width * s + 0.5)), max(h, int(im.height * s + 0.5))
    r = im.resize((nw, nh), Image.BICUBIC)
    x = int(min(max(cx * nw - w / 2, 0), nw - w)); y = int(min(max(cy * nh - h / 2, 0), nh - h))
    return r.crop((x, y, x + w, y + h))

def paste(dst, sprite, cx, cy, scale=1.0, rot=0.0, alpha=1.0, sx=1.0, sy=1.0):
    """스프라이트를 중심 (cx, cy)에 붙인다. sx·sy: 스쿼시"""
    if scale <= 0.01 or alpha <= 0.01: return
    w, h = max(1, int(sprite.width * scale * sx)), max(1, int(sprite.height * scale * sy))
    s = sprite.resize((w, h), Image.BICUBIC)
    if rot: s = s.rotate(rot, Image.BICUBIC, expand=True)
    if alpha < 1:
        a = s.getchannel('A').point(lambda v: int(v * alpha)); s.putalpha(a)
    dst.alpha_composite(s, (int(cx - s.width / 2), int(cy - s.height / 2))) if (0 <= int(cx - s.width / 2) and 0 <= int(cy - s.height / 2) and int(cx - s.width / 2) + s.width <= dst.width and int(cy - s.height / 2) + s.height <= dst.height) else _paste_clip(dst, s, int(cx - s.width / 2), int(cy - s.height / 2))

def _paste_clip(dst, s, x, y):
    l, t = max(0, -x), max(0, -y)
    r, b = min(s.width, dst.width - x), min(s.height, dst.height - y)
    if r <= l or b <= t: return
    dst.alpha_composite(s.crop((l, t, r, b)), (x + l, y + t))

# ---------- 글자 ----------
@functools.lru_cache(None)
def word(text, size=220, fill=CREAM, stroke=BROWN, sw=None, accent=None):
    """로고 같은 크림 글자 + 갈색 테두리. accent: 강조색으로 칠할 글자 위치 집합"""
    f = ImageFont.truetype(FONT, size)
    sw = sw if sw is not None else max(4, size // 13)
    pad = sw * 3
    bb = f.getbbox(text, stroke_width=sw)
    im = Image.new('RGBA', (bb[2] - bb[0] + pad * 2, bb[3] - bb[1] + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    ox, oy = pad - bb[0], pad - bb[1]
    # 두꺼운 바깥 테두리(갈색) → 안쪽 글자
    d.text((ox, oy + sw * 0.5), text, font=f, fill=stroke, stroke_width=int(sw * 1.25), stroke_fill=stroke)
    if accent:
        x = ox
        for i, ch in enumerate(text):
            d.text((x, oy), ch, font=f, fill=CORAL if i in accent else fill)
            x += f.getlength(ch)
    else:
        d.text((ox, oy), text, font=f, fill=fill)
    # 수채 종이 결: 글자 안쪽 밝기 살짝 흔들기
    rnd = np.random.default_rng(len(text) * 7 + size)
    n = Image.fromarray((rnd.normal(0, 1, (im.height // 6 + 1, im.width // 6 + 1)) * 9 + 128).clip(0, 255).astype('uint8')).resize(im.size, Image.BICUBIC)
    arr = np.asarray(im).astype(np.float32)
    nn = (np.asarray(n).astype(np.float32) - 128) / 255
    arr[..., :3] = np.clip(arr[..., :3] * (1 + nn[..., None] * 0.5), 0, 255)
    return Image.fromarray(arr.astype('uint8'))

def pop_word(dst, text, t, t0, cx, cy, size=220, rot=0.0, dur=0.28, accent=None, wobble=True, out_t=None, fill=CREAM):
    """t0에 튀어나오는 글자 (스쿼시 → 오버슈트). out_t가 있으면 그때 작아지며 사라진다"""
    if t < t0: return
    k = ease_out_back((t - t0) / dur)
    s = 0.25 + 0.75 * k
    sq = 1 + 0.12 * math.sin(min(1, (t - t0) / dur) * math.pi)
    r = rot + (math.sin((t - t0) * 5) * 1.5 if wobble else 0)
    a = 1.0
    if out_t is not None and t >= out_t:
        q = (t - out_t) / 0.15
        if q >= 1: return
        s *= 1 - 0.5 * q; a = 1 - q
    paste(dst, word(text, size, accent=frozenset(accent) if accent else None, fill=fill), cx, cy, s, r, a, sx=sq, sy=1 / sq)

def band(dst, text, t, t0, size=74):
    """하단 고정 띠 자막 (NKK 몽타주 방식): 크림 종이 띠가 왼쪽에서 밀려 들어온다"""
    if t < t0: return
    k = ease_out((t - t0) / 0.3)
    wimg = word(text, size, fill=BROWN, stroke=CREAM, sw=max(4, size // 14))
    bw, bh = wimg.width + 120, wimg.height + 10
    strip = Image.new('RGBA', (bw, bh), (0, 0, 0, 0))
    ImageDraw.Draw(strip).rounded_rectangle((0, 8, bw - 1, bh - 8), radius=bh // 2 - 8, fill=CREAM + (235,), outline=BROWN + (255,), width=6)
    strip.alpha_composite(wimg, ((bw - wimg.width) // 2, (bh - wimg.height) // 2))
    x = int(-bw + (W / 2 + bw / 2) * k)
    _paste_clip(dst, strip, x, H - bh - 46)

# ---------- 배경 카드 ----------
def _proc_sunburst():
    """Codex card_wash가 없을 때: 수채 종이 방사형을 직접 만든다"""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    ang = np.arctan2(yy - H / 2, xx - W / 2)
    rad = np.hypot(xx - W / 2, yy - H / 2) / math.hypot(W / 2, H / 2)
    rnd = np.random.default_rng(3)
    noise = np.asarray(Image.fromarray((rnd.normal(0, 1, (H // 16, W // 16)) * 40 + 128).clip(0, 255).astype('uint8')).resize((W, H), Image.BICUBIC)).astype(np.float32) / 255 - 0.5
    stripe = (np.sin(ang * 8 + noise * 0.6) > 0).astype(np.float32)
    v = 0.86 + 0.05 * stripe - 0.16 * rad ** 1.6 + noise * 0.05
    return Image.fromarray((np.clip(v, 0, 1) * 255).astype('uint8'), 'L')

@functools.lru_cache(None)
def wash_lum():
    p = os.path.join(ART, 'card_wash.png')
    if os.path.exists(p):
        return cover(Image.open(p).convert('L'), int(W * 1.25), int(H * 1.25))
    return _proc_sunburst().resize((int(W * 1.25), int(H * 1.25)), Image.BICUBIC)

@functools.lru_cache(None)
def card_bg(tone):
    """같은 수채 방사형을 색만 바꿔 장을 나눈다 (밝은 쪽 색, 어두운 쪽 색)"""
    tones = {
        'sage': ((236, 240, 214), (128, 160, 120)),
        'apricot': ((253, 236, 208), (226, 150, 112)),
        'moss': ((232, 238, 200), (110, 140, 92)),
        'lavender': ((238, 232, 246), (140, 128, 186)),
        'night': ((150, 160, 196), (40, 52, 84)),
    }[tone]
    L = np.asarray(wash_lum()).astype(np.float32) / 255
    lo, hi = np.array(tones[1], np.float32), np.array(tones[0], np.float32)
    # 대비를 키워 줄무늬가 보이게
    L = np.clip((L - L.mean()) * 1.8 + 0.72, 0, 1)
    rgb = lo + (hi - lo) * L[..., None]
    return Image.fromarray(rgb.astype('uint8')).convert('RGBA')

def card(tone, t, t0):
    """천천히 도는 방사형 카드"""
    bg = card_bg(tone)
    a = (t - t0) * 6  # 도/초
    r = bg.rotate(a, Image.BICUBIC)
    x, y = (r.width - W) // 2, (r.height - H) // 2
    return r.crop((x, y, x + W, y + H))

# ---------- 인게임 클립 ----------
@functools.lru_cache(None)
def clip_files(name):
    fs = sorted(glob.glob(os.path.join(CAP, name, '*.jpg')) + glob.glob(os.path.join(CAP, name, '*.png')))
    return fs

PLACEHOLDER = {
    'round': P('Marketing', 'itch_dream', 'out', 'shot1_harvest.jpg'),
    'combo': P('Marketing', 'itch_dream', 'out', 'shot2_combo.jpg'),
    'tree': P('Marketing', 'itch_dream', 'out', 'shot3_tree.jpg'),
    'farm': P('Marketing', 'itch_dream', 'out', 'shot4_worldtree.jpg'),
}

def clip(name, t, t0, start=0.0, speed=1.0, zoom0=1.0, zoom1=1.04, cx=0.5, cy=0.5, seg=None):
    """녹화 프레임 (start초부터). seg = (전체 길이) 클립 안 진행률로 줌을 건다"""
    fs = clip_files(name)
    if fs:
        i = int((start + (t - t0) * speed) * FPS)
        im = Image.open(fs[max(0, min(len(fs) - 1, i))]).convert('RGBA')
    else:
        key = next((k for k in PLACEHOLDER if name.startswith(k)), 'round')
        im = img(PLACEHOLDER[key])
    q = 0 if not seg else (t - t0) / seg
    return cover(im, zoom=zoom0 + (zoom1 - zoom0) * ease_in_out(q), cx=cx, cy=cy)

# ---------- 장면 ----------
# 세계수 꼬마 정령 8종 (Defs.Farm KIDS 순서). 게임 속 모습인 버섯 정령 진화 그림(★0/★1 통짜)을 쓴다
KIDS = ['leaf', 'dew', 'wind', 'fire', 'rock', 'spark', 'moon', 'star']
KID_ART = {'leaf': 'e_leaf_0_body', 'dew': 'e_dew_1_body', 'wind': 'e_wind_1_body', 'fire': 'e_fire_1_body',
           'rock': 'e_rock_1_body', 'spark': 'e_spark_1_body', 'moon': 'e_moon_1_body', 'star': 'e_star_1_body'}
@functools.lru_cache(None)
def kid(k):
    im = dream(f'Characters/Evo/{KID_ART[k]}.png')
    s = 360 / math.sqrt(im.width * im.height)   # 그림마다 크기가 달라 넓이로 맞춘다
    return im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)

def hop(t, t0, period=0.5, h=40):
    """박마다 퐁 튀는 높이 + 착지 스쿼시"""
    if t < t0: return 0, 1
    ph = ((t - t0) % period) / period
    y = -h * math.sin(ph * math.pi)
    sq = 1 + (0.14 * (1 - ph * 6) if ph < 0.16 else 0)
    return y, sq

def s1_hook(t):
    """0.0–2.5 숲 배너 + 제목 의성어"""
    bg = cover(img(P('Marketing', 'itch_dream', 'banner_art.png')), zoom=1.0 + 0.06 * t / 2.5, cy=0.55)
    for i, (k, x) in enumerate([('leaf', 360), ('fire', 1560), ('moon', 1210)]):
        t0 = 0.2 + i * 0.25
        if t < t0: continue
        rise = ease_out_back((t - t0) / 0.35)
        y, sq = hop(t, t0 + 0.35, 0.5, 26)
        base = 1080 + 40 - 330 * rise
        paste(bg, kid(k), x, base + y, 0.62, 0, 1, sx=sq, sy=1 / sq)
    pop_word(bg, 'More!', t, 0.5, 600, 250, 190, -6)
    pop_word(bg, 'Must!!', t, 1.0, 960, 470, 210, 3)
    pop_word(bg, 'Mush!!!', t, 1.5, 1330, 700, 240, -4, accent={0, 1, 2, 3})
    return bg

def s2_bounce(t, t0=2.5):
    """2.5–4.0 BOUNCE! 카드 + 바 튕기기 설명"""
    f = card('sage', t, t0); lt = t - t0
    mech = os.path.join(ART, 'mech_bounce.png')
    if os.path.exists(mech):
        m = img(mech)
        k = ease_out_back(lt / 0.35)
        paste(f, m, 960, 690 + (1 - k) * 200, 0.62 * (0.6 + 0.4 * k))
    else:
        # 그림이 없으면 인게임 스프라이트로: 바 + 포물선으로 튀는 낫 + 버섯 무리
        bar_y = 930
        bx = 760 + 220 * math.sin(lt * 2.0)
        ImageDraw.Draw(f).rounded_rectangle((bx - 210, bar_y - 18, bx + 210, bar_y + 18), 18, fill=(150, 104, 66, 255), outline=BROWN + (255,), width=5)
        q = (lt % 0.75) / 0.75
        hx = bx + (1440 - bx) * q; hy = bar_y - 60 - 520 * math.sin(q * math.pi * 0.62) * 1.0
        paste(f, dream('Harvesters/sam.png'), hx, hy, 0.55, -q * 720)
    pop_word(f, 'BOUNCE!', t, t0 + 0.05, 960, 230, 230, -3)
    return f

def overlay_sfx(f, t, words):
    """인게임 위 의성어: 박마다 모서리를 바꿔 가며"""
    for (txt, t0, x, y, r) in words:
        pop_word(f, txt, t, t0, x, y, 150, r, dur=0.22, out_t=t0 + 0.42)

def s3_round(t, t0=4.0):
    f = clip('round', t, t0, start=R1_START, seg=3.0, zoom0=1.0, zoom1=1.06)
    overlay_sfx(f, t, [('Pop!', 4.5, 330, 230, -8), ('Bonk!', 5.5, 1600, 260, 6), ('Pop!!', 6.5, 380, 820, -5)])
    return f

def s4_combo(t, t0=7.0):
    f = card('apricot', t, t0); lt = t - t0
    pop_word(f, 'COMBO!', t, t0 + 0.02, 960, 300, 230, 3)
    n = 5 if lt < 0.33 else 12 if lt < 0.66 else 23
    t1 = t0 + (0 if lt < 0.33 else 0.33 if lt < 0.66 else 0.66)
    pop_word(f, f'x{n}', t, t1, 960, 680, 200 + n * 6, -4, dur=0.2, accent=set(range(len(str(n)) + 1)))
    paste(f, dream('Harvesters/sam.png'), 520, 700 + hop(t, t0, 0.33, 30)[0], 0.8, -15 + lt * 200)
    paste(f, dream('Harvesters/hoe.png'), 1400, 700 + hop(t, t0 + 0.16, 0.33, 30)[0], 0.5, 15 - lt * 200)
    return f

def s5_skills(t, t0=8.0):
    f = clip('round', t, t0, start=R1_SKILL, seg=3.0, zoom0=1.02, zoom1=1.08)
    band(f, '15 SKILLS TO CHAIN!', t, t0 + 0.25)
    return f

def s6_grow(t, t0=11.0):
    f = card('moss', t, t0); lt = t - t0
    g = dream('Characters/Grandpa/grandpa_ref.png')
    k = ease_out_back(lt / 0.4)
    paste(f, g, 960, 1080 + 260 - 640 * k, 0.62)
    for i, x in enumerate([560, 1360, 420, 1500]):
        ts = t0 + 0.15 + i * 0.12
        if t > ts: paste(f, dream(f'Mushrooms/Single/ed{[0, 3, 7, 12][i]}.png'), x, 930 - 30 * ease_out((t - ts) / 0.3), 0.55 * ease_out_back((t - ts) / 0.3))
    pop_word(f, 'GROW!', t, t0 + 0.05, 960, 220, 240, -3)
    return f

def s7_tree(t, t0=12.0):
    f = clip('tree', t, t0, start=0.0, seg=2.5, zoom0=1.0, zoom1=1.05)
    pop_word(f, 'LEVEL UP!', t, 13.0, 1420, 200, 150, 5, out_t=14.2)
    return f

def s8_friends(t, t0=14.5):
    f = card('lavender', t, t0)
    grp = os.path.join(ART, 'friends_group.png')
    if os.path.exists(grp):
        # 1·2·3단계 정령이 함께 선 Codex 단체 그림: 아래에서 솟아오르고 박마다 살짝 통통
        g = img(grp)
        k = ease_out_back((t - t0) / 0.35)
        y, sq = hop(t, t0 + 0.35, 0.5, 10)
        sc = min(1760 / g.width, 720 / g.height)
        paste(f, g, 960, 1080 + 420 - 760 * k + y, sc, 0, 1, sx=sq, sy=1 / sq)
        pop_word(f, 'FRIENDS!', t, t0 + 0.1, 960, 200, 220, 3)
        return f
    for i, k in enumerate(KIDS):
        ts = t0 + i * 0.07
        if t < ts: continue
        x = 190 + i * 220
        y, sq = hop(t, ts + 0.3, 0.5, 18)
        k2 = ease_out_back((t - ts) / 0.3)
        sp = kid(k)
        sc = 230 / max(sp.width, sp.height)
        paste(f, sp, x, 1180 - 480 * k2 + y, sc * 1.25, 0, 1, sx=sq, sy=1 / sq)
    pop_word(f, 'FRIENDS!', t, t0 + 0.1, 960, 300, 230, 3)
    return f

def s9_farm(t, t0=15.5):
    f = clip('farm', t, t0, start=0.0, seg=3.0, zoom0=1.08, zoom1=1.0)
    band(f, 'RAISE YOUR WORLD TREE', t, t0 + 0.25)
    return f

def s10_boss(t, t0=18.5):
    lt = t - t0
    p = os.path.join(ART, 'boss_scene.png')
    shake = (8 * math.sin(lt * 70), 6 * math.cos(lt * 55)) if 1.0 < lt < 1.6 else (0, 0)
    if os.path.exists(p):
        f = cover(img(p), zoom=1.12 - 0.08 * ease_out(lt / 3.0), cx=0.5 + shake[0] / W, cy=0.45 + shake[1] / H)
    else:
        # 그림이 없으면: 밤 숲 배경 + 보스가 아래에서 솟아오름 + 놀란 꼬마들
        f = cover(dream('Backgrounds/bg_title.png'), zoom=1.1 - 0.06 * lt / 3, cy=0.5)
        ov = Image.new('RGBA', f.size, (40, 40, 80, 90)); f.alpha_composite(ov)
        k = ease_out(max(0, lt - 0.3) / 0.9)
        paste(f, dream('Bosses/king.png'), 960 + shake[0], 1080 + 300 - 780 * k + shake[1], 1.5)
        for i, (kd, x) in enumerate([('leaf', 420), ('fire', 1500), ('moon', 1250)]):
            jump = -60 * ease_out_back(max(0, lt - 1.0) / 0.25) if lt > 1.0 else 0
            paste(f, kid(kd), x, 960 + jump, 0.5)
    pop_word(f, 'Uh-oh...', t, t0 + 0.5, 520, 200, 140, -4, out_t=t0 + 1.45)
    pop_word(f, 'BOSS!!', t, t0 + 1.5, 960, 240, 280, 3, accent={0, 1, 2, 3})
    if 1.0 < lt < 1.1:  # 번쩍
        f.alpha_composite(Image.new('RGBA', f.size, CREAM + (int(200 * (1 - (lt - 1.0) / 0.1)),)))
    return f

BOSS_CLIPS = ['boss_king', 'boss_cleaner', 'boss_phantom']
def s11_bosses(t, t0=21.5):
    seg = 4.0 / len(BOSS_CLIPS)
    i = min(len(BOSS_CLIPS) - 1, int((t - t0) / seg))
    f = clip(BOSS_CLIPS[i], t, t0 + i * seg, start=BOSS_START, seg=seg, zoom0=1.04, zoom1=1.0)
    band(f, 'A BOSS EVERY 10 STAGES!', t, t0 + 0.1)
    return f

def s12_fast(t, t0=25.5):
    lt = t - t0
    if lt < 0.5: f = clip('round', t, t0, start=R1_FAST, seg=0.5, zoom0=1.1, zoom1=1.16); w = 'HARVEST.'
    elif lt < 1.0: f = clip('tree', t, t0 + 0.5, start=2.0, seg=0.5, zoom0=1.1, zoom1=1.16); w = 'UPGRADE.'
    else: f = clip('farm', t, t0 + 1.0, start=2.0, seg=0.5, zoom0=1.1, zoom1=1.16); w = 'REPEAT!'
    tw = t0 + (0 if lt < 0.5 else 0.5 if lt < 1.0 else 1.0)
    pop_word(f, w, t, tw, 960, 540, 230, -3 if lt < 1 else 3, dur=0.18, accent=set(range(len(w))) if w == 'REPEAT!' else None)
    return f

def s13_end(t, t0=27.0):
    lt = t - t0
    f = cover(img(P('Marketing', 'itch_dream', 'banner_art.png')), zoom=1.06 - 0.04 * ease_out(lt / 3), cy=0.6)
    logo = img(P('Marketing', 'itch_dream', 'out', 'banner.png'))
    k = ease_out_back(lt / 0.45)
    paste(f, logo, 960, 400, 1.25 * (0.3 + 0.7 * k), -2 * (1 - k))
    for i, (kd, x) in enumerate([('leaf', 300), ('fire', 520), ('dew', 1400), ('star', 1620)]):
        ts = t0 + 0.3 + i * 0.1
        if t < ts: continue
        y, sq = hop(t, ts + 0.3, 0.5, 22)
        paste(f, kid(kd), x, 1180 - 330 * ease_out_back((t - ts) / 0.3) + y, 0.5, 0, 1, sx=sq, sy=1 / sq)
    pop_word(f, 'PLAY FREE ON itch.io', t, t0 + 0.8, 960, 790, 96, 0, wobble=False)
    # 아이리스 아웃: 로고 버섯 쪽으로 원이 좁혀진다
    if lt > 2.2:
        q = ease_in_out((lt - 2.2) / 0.7)
        r = (1 - q) * 1200
        m = Image.new('L', f.size, 0)
        cx, cy = 960 - 300, 400 - 120
        ImageDraw.Draw(m).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
        black = Image.new('RGBA', f.size, (14, 12, 16, 255))
        black.paste(f, (0, 0), m)
        f = black
    return f

# 녹화 안에서 쓸 구간 (초) — 녹화 후 고른다
R1_START, R1_SKILL, R1_FAST, BOSS_START = 2.0, 9.0, 14.0, 1.0

SCENES = [
    (0.0, 2.5, s1_hook), (2.5, 4.0, s2_bounce), (4.0, 7.0, s3_round), (7.0, 8.0, s4_combo),
    (8.0, 11.0, s5_skills), (11.0, 12.0, s6_grow), (12.0, 14.5, s7_tree), (14.5, 15.5, s8_friends),
    (15.5, 18.5, s9_farm), (18.5, 21.5, s10_boss), (21.5, 25.5, s11_bosses), (25.5, 27.0, s12_fast),
    (27.0, 30.0, s13_end),
]

def frame(t):
    for a, b, fn in SCENES:
        if a <= t < b:
            return fn(t) if fn.__code__.co_argcount == 1 else fn(t, a)
    return SCENES[-1][2](t, SCENES[-1][0])

# ---------- 소리 (120BPM 오르골·피치카토 + 팝) ----------
SR = 44100
def synth_audio(path):
    n = int(SR * DUR)
    out = np.zeros(n, np.float32)
    tt = np.arange(n) / SR

    def note(t0, f, d, v, kind='pluck'):
        i0 = int(t0 * SR); m = int(d * SR)
        if i0 >= n: return
        m = min(m, n - i0); x = np.arange(m) / SR
        if kind == 'pluck':   # 오르골/칼림바: 사인 + 배음, 빠른 감쇠
            s = np.sin(2 * np.pi * f * x) + 0.35 * np.sin(2 * np.pi * f * 2 * x) * np.exp(-x * 9) + 0.12 * np.sin(2 * np.pi * f * 3.01 * x) * np.exp(-x * 14)
            env = np.exp(-x * 4.5) * np.minimum(1, x * 400)
        elif kind == 'pad':
            s = np.sin(2 * np.pi * f * x) + 0.5 * np.sin(2 * np.pi * f * 1.005 * x) + 0.25 * np.sin(2 * np.pi * f * 2 * x)
            env = np.minimum(1, x / 0.25) * np.minimum(1, (d - x) / 0.3).clip(0, 1)
        elif kind == 'bass':
            s = np.sin(2 * np.pi * f * x) + 0.2 * np.sin(2 * np.pi * f * 2 * x)
            env = np.exp(-x * 3.5) * np.minimum(1, x * 200)
        elif kind == 'pop':   # 게임 Snd 같은 짧은 팝 (위로 미끄러지는 사인)
            ff = f * (1 + 1.2 * x / d)
            s = np.sin(2 * np.pi * np.cumsum(ff) / SR)
            env = np.exp(-x * 30)
        elif kind == 'thud':
            ff = f * np.exp(-x * 12)
            s = np.sin(2 * np.pi * np.cumsum(ff) / SR)
            env = np.exp(-x * 9)
        elif kind == 'shaker':
            s = np.random.default_rng(int(t0 * 1000)).normal(0, 1, m)
            s = np.diff(np.concatenate([[0], s]))
            env = np.exp(-x * 40)
        out[i0:i0 + m] += (s * env * v).astype(np.float32)

    # 코드 진행 (F - C - Dm - Bb), 한 마디 = 4박 = 2초
    hz = lambda midi: 440 * 2 ** ((midi - 69) / 12)
    prog = [(53, [65, 69, 72]), (48, [64, 67, 72]), (50, [65, 69, 74]), (46, [65, 70, 74])]
    mel = [77, 76, 72, 74, 76, 72, 69, 72, 74, 72, 69, 67, 65, 67, 69, 72]
    for bar in range(15):
        t0 = bar * 2.0
        root, ch = prog[bar % 4]
        quiet = 18.5 <= t0 < 21.5      # 보스 등장: 음악을 비우고 긴장
        if not quiet:
            note(t0, hz(root - 12), 1.9, 0.30, 'bass'); note(t0 + 1.0, hz(root - 5), 0.9, 0.18, 'bass')
            note(t0, hz(ch[0]), 2.0, 0.045, 'pad'); note(t0, hz(ch[1]), 2.0, 0.04, 'pad'); note(t0, hz(ch[2]), 2.0, 0.035, 'pad')
            for s8 in range(8):      # 8분음 아르페지오
                note(t0 + s8 * 0.25, hz(ch[s8 % 3] + (12 if s8 % 4 == 3 else 0)), 0.5, 0.10, 'pluck')
            for b in range(4):
                if bar >= 1: note(t0 + b * 0.5 + 0.25, 0, 0.08, 0.05, 'shaker')
                if bar >= 1 and b % 2 == 0: note(t0 + b * 0.5, 120, 0.25, 0.30, 'thud')
            if bar >= 2:             # 멜로디 (4분음)
                for b in range(4):
                    m = mel[(bar * 4 + b) % len(mel)]
                    note(t0 + b * 0.5, hz(m), 0.6, 0.13, 'pluck')
        else:
            note(t0, hz(root - 24), 2.0, 0.35, 'bass')
            note(t0, hz(ch[0] - 12), 2.0, 0.06, 'pad'); note(t0, hz(ch[0] - 11), 2.0, 0.05, 'pad')
    # 보스 쿵 (19.5)·글자 팝
    note(19.5, 70, 0.8, 0.9, 'thud'); note(20.0, 60, 0.8, 0.7, 'thud')
    pops = [0.5, 1.0, 1.5, 2.55, 4.5, 5.5, 6.5, 7.0, 7.33, 7.66, 11.05, 13.0, 14.6, 15.0, 20.0, 25.5, 26.0, 26.5, 27.0, 27.8]
    for i, p in enumerate(pops):
        note(p, 520 + (i % 4) * 130, 0.12, 0.35, 'pop')
    # 인게임 구간엔 잔잔한 수확 틱
    rnd = random.Random(5)
    for a, b in [(4.0, 7.0), (8.0, 11.0), (21.5, 25.5)]:
        x = a
        while x < b:
            note(x, rnd.choice([880, 990, 1175, 1320]), 0.06, 0.06, 'pop'); x += rnd.uniform(0.09, 0.22)
    # 끝: 페이드아웃
    fade = np.clip((DUR - tt) / 0.8, 0, 1)
    out *= fade
    out /= max(1e-6, np.abs(out).max()) / 0.8
    st = np.stack([out, out], 1)
    # 아주 짧은 스테레오 지연으로 넓게
    d = int(0.012 * SR); st[d:, 1] = st[:-d, 1] * 0.9 + st[d:, 1] * 0.1
    import wave
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((st * 32767).astype('<i2').tobytes())

# ---------- 출력 ----------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--preview', action='store_true')
    ap.add_argument('--stills', default='')
    ap.add_argument('--from', dest='t0', type=float, default=0)
    ap.add_argument('--to', dest='t1', type=float, default=DUR)
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    if a.stills:
        for s in a.stills.split(','):
            frame(float(s)).convert('RGB').save(os.path.join(OUT, f'still_{s}.jpg'), quality=88)
        return
    wav = os.path.join(OUT, 'pv_audio.wav'); synth_audio(wav)
    ow, oh = (960, 540) if a.preview else (W, H)
    name = 'MoreMustMush_PV_30s_preview.mp4' if a.preview else 'MoreMustMush_PV_30s.mp4'
    cmd = ['ffmpeg', '-y', '-v', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{ow}x{oh}', '-r', str(FPS), '-i', '-',
           '-ss', str(a.t0), '-i', wav, '-map', '0:v', '-map', '1:a', '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p',
           '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', os.path.join(OUT, name)]
    pr = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n0, n1 = int(a.t0 * FPS), int(a.t1 * FPS)
    for i in range(n0, n1):
        f = frame(i / FPS).convert('RGB')
        if a.preview: f = f.resize((ow, oh), Image.BILINEAR)
        pr.stdin.write(f.tobytes())
        if i % 60 == 0: print(f'{i / FPS:.1f}s', flush=True)
    pr.stdin.close(); pr.wait()
    print('->', os.path.join(OUT, name))

if __name__ == '__main__':
    main()
