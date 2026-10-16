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

# ---------- 손 흔드는 정령 (게임 리그 Resources/Rig/evo_rig.json ★2를 그대로 조립) ----------
PPU = 100   # 부품 그림 Pixels Per Unit (리그 좌표 1 = 100px)
@functools.lru_cache(None)
def evo_look(kind, form=2):
    import json
    d = json.load(open(P('Assets', 'Resources', 'Rig', 'evo_rig.json'), encoding='utf-8'))
    return next(l for l in d['looks'] if l['kind'] == kind and l['form'] == form)

def evo_part(name):
    p = os.path.join(DREAM, 'Characters', 'Evo', name + '.png')
    return img(p) if name and os.path.exists(p) else None

def _put(layer, sp, x, y, rot=0.0, flip=False):
    """부품을 리그 좌표 (x, y) 중심에 (layer = 큰 캔버스, 원점은 발바닥 가운데)"""
    if sp is None: return
    if flip: sp = sp.transpose(Image.FLIP_LEFT_RIGHT)
    if rot: sp = sp.rotate(rot, Image.BICUBIC, expand=True)
    ox, oy = layer.width / 2, layer.height * 0.85
    _paste_clip(layer, sp, int(ox + x * PPU - sp.width / 2), int(oy - y * PPU - sp.height / 2))

def _rot(v, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return (v[0] * c - v[1] * s, v[0] * s + v[1] * c)

def _arm(layer, l, side, raise_deg, elbow_deg, k=1.0, out=0.0):
    """side 0 = 보는 쪽 왼팔, 1 = 오른팔(왼팔을 뒤집음). raise: 바깥으로 드는 각도, elbow: 팔꿈치 접는 각도
    k: 팔 부품 확대 (흔드는 팔이 머리카락 밖으로 나오게), out: 어깨를 바깥으로 더 뺄 거리(리그 단위)"""
    sx = 1 if side == 0 else -1
    sh = l['shoulderL']; sh = ((sh[0] - out) * sx, sh[1])
    up, lo = evo_part(l['armUp']), evo_part(l['armLo'])
    if k != 1.0:
        up = up.resize((int(up.width * k), int(up.height * k)), Image.BICUBIC)
        lo = lo.resize((int(lo.width * k), int(lo.height * k)), Image.BICUBIC)
    cen, elb = (l['armCenter'][0] * k, l['armCenter'][1] * k), (l['armElbow'][0] * k, l['armElbow'][1] * k)
    r1 = -raise_deg * sx           # 게임과 같은 회전 (왼팔은 시계 방향으로 들면 바깥)
    c1 = _rot((cen[0] * sx, cen[1]), r1)
    _put(layer, up, sh[0] + c1[0], sh[1] + c1[1], r1, flip=side == 1)
    e = _rot((elb[0] * sx, elb[1]), r1)
    r2 = -(raise_deg + elbow_deg) * sx   # 팔꿈치도 같은 쪽으로 더 들어 아래팔이 위를 향하게
    c2 = _rot(((cen[0] - elb[0]) * sx, cen[1] - elb[1]), r2)
    _put(layer, lo, sh[0] + e[0] + c2[0], sh[1] + e[1] + c2[1], r2, flip=side == 1)

@functools.lru_cache(maxsize=256)
def kid_wave_frame(kind, q):
    """q = 손 흔들기 위상 (0..1 을 16칸으로 반올림해 캐시). 오른팔을 들어 흔들고 머리는 살짝 갸웃"""
    l = evo_look(kind)
    layer = Image.new('RGBA', (900, 900), (0, 0, 0, 0))
    sw = math.sin(q * 2 * math.pi)
    tilt = 4 * sw
    neck = l['neck']
    _put(layer, evo_part(l['hair']), l['hairPos'][0], l['hairPos'][1], tilt)
    for side in (0, 1):
        _put(layer, evo_part(l['leg']), l['hipL'][0] * (1 if side == 0 else -1) + l['legCenter'][0], l['hipL'][1] + l['legCenter'][1], 0, flip=side == 1)
    _put(layer, evo_part(l['body']['sprite']), l['body']['x'], l['body']['y'])
    hx, hy = _rot((l['head']['x'] - neck[0], l['head']['y'] - neck[1]), tilt)
    head = evo_part(l['head']['sprite'])
    _put(layer, head, neck[0] + hx, neck[1] + hy, tilt)
    _put(layer, evo_part(l['face'] + 'normal'), neck[0] + hx, neck[1] + hy, tilt)
    _arm(layer, l, 0, 12, 0, 1.2)                     # 왼팔은 내린 채
    # 오른팔: 리그 팔이 짧아 머리카락에 묻히므로 1.4배로 키우고 어깨를 조금 빼서, 위팔은 옆으로 뻗고 아래팔을 세워 좌우로 흔든다
    _arm(layer, l, 1, 96 + 4 * sw, 52 + 20 * sw, 1.4, 0.1)
    _put(layer, evo_part(l['collar']), l['collarPos'][0], l['collarPos'][1])
    return layer.crop(layer.getbbox())

def paste_kid_wave(dst, kind, cx, foot_y, t, scale=0.5, period=0.5, flip=False):
    """발바닥 가운데가 (cx, foot_y). 박(0.5초)마다 한 번 흔든다. flip: 좌우 뒤집어 반대 손으로"""
    q = round(((t / period) % 1) * 16) / 16
    fr = kid_wave_frame(kind, q)
    if flip: fr = fr.transpose(Image.FLIP_LEFT_RIGHT)
    s = scale
    w, h = int(fr.width * s), int(fr.height * s)
    paste(dst, fr, cx, foot_y - h / 2, s)

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
    """27.0–30.0 엔드카드: 낫이 날아와 로고를 쳐서 박고(27.3), 정령들이 손 흔들며 인사 → 아이리스 아웃"""
    lt = t - t0
    hit = 0.3                                   # 27.3초에 쾅
    ht = lt - hit
    # 흔들림 (쾅 뒤 0.3초, 빠르게 줄어듦)
    dx = dy = 0.0
    if 0 <= ht < 0.3:
        a = 22 * (1 - ht / 0.3) ** 2
        dx, dy = a * math.sin(ht * 95), a * 0.7 * math.cos(ht * 77)
    f = cover(img(P('Marketing', 'itch_dream', 'banner_art.png')), zoom=1.06 - 0.04 * ease_out(lt / 3),
              cx=0.5 - dx / W, cy=0.6 - dy / H)
    logo = img(P('Marketing', 'itch_dream', 'out', 'banner.png'))
    LX, LY, LS = 960, 400, 1.25
    HX, HY = 560, 470                           # 로고 왼쪽 아래 모서리 근처를 친다
    if ht >= 0:
        # 맞은 순간 오른쪽으로 납작하게 밀렸다가 용수철처럼 튀며 제자리에 박힌다
        d = math.exp(-ht * 7.5)
        sp = d * math.cos(ht * 34)
        sq = 1 + 0.32 * sp                      # 가로로 늘고 세로로 눌림 → 반대로 튐
        push = 70 * sp
        paste(f, logo, LX + push * 0.8 + dx, LY - push * 0.25 + dy, LS * (1 + 0.06 * d), -5 * sp, sx=sq, sy=1 / sq)
        # 충격선: 친 자리에서 사방으로 뻗는 갈색·크림 획
        if ht < 0.28:
            q = ht / 0.28
            dr = ImageDraw.Draw(f)
            rnd = random.Random(13)
            for j in range(14):
                ang = math.radians(100 + j * (220 / 13) + rnd.uniform(-6, 6))
                r0 = 60 + 260 * ease_out(q); r1 = r0 + 140 * (1 - q) + 30
                x0, y0 = HX + dx + math.cos(ang) * r0, HY + dy - math.sin(ang) * r0
                x1, y1 = HX + dx + math.cos(ang) * r1, HY + dy - math.sin(ang) * r1
                wdt = int(16 * (1 - q)) + 3
                dr.line((x0, y0, x1, y1), fill=BROWN + (255,), width=wdt + 6)
                dr.line((x0, y0, x1, y1), fill=CREAM + (255,), width=wdt)
    # 낫: 왼쪽 아래 화면 밖에서 돌며 날아와 쾅 → 튕겨 왼쪽 위로 날아간다
    sam = dream('Harvesters/sam.png')
    if lt < hit:
        q = lt / hit
        x = -220 + (HX - 90 + 220) * q
        y = 1250 + (HY + 20 - 1250) * q - 260 * math.sin(q * math.pi)
        paste(f, sam, x, y, 1.15, -q * 900)
        for j in range(1, 4):                   # 잔상
            qq = max(0, q - j * 0.07)
            paste(f, sam, -220 + (HX - 90 + 220) * qq, 1250 + (HY + 20 - 1250) * qq - 260 * math.sin(qq * math.pi), 1.15, -qq * 900, alpha=0.28 / j)
    elif ht < 0.7:
        q = ht / 0.7
        x = HX - 90 - 900 * q
        y = HY + 20 - 700 * q + 900 * q * q
        paste(f, sam, x + dx, y + dy, 1.15 * (1 - 0.3 * q), -900 + q * 1500)
    # 번쩍임
    if 0 <= ht < 0.12:
        f.alpha_composite(Image.new('RGBA', f.size, CREAM + (int(150 * (1 - ht / 0.12)),)))
    # 정령들: 아래에서 솟아올라 박마다 손 흔들기 (위상을 조금씩 엇갈림)
    for i, (kd, x) in enumerate([('leaf', 170), ('fire', 400), ('dew', 1520), ('star', 1760)]):
        ts = t0 + 0.45 + i * 0.08
        if t < ts: continue
        rise = ease_out_back((t - ts) / 0.32)
        paste_kid_wave(f, kd, x + dx, 1080 + 345 - 360 * rise + dy, t + i * 0.11, scale=0.5, flip=i < 2)   # 왼쪽 둘은 뒤집어 바깥 손을 흔든다
    pop_word(f, 'PLAY FREE ON itch.io', t, t0 + 0.8, 960 + dx, 745 + dy, 96, 0, wobble=False)
    # 아이리스 아웃: 로고 버섯 쪽으로 원이 좁혀진다
    if lt > 2.2:
        q = ease_in_out((lt - 2.2) / 0.7)
        r = (1 - q) * 1200
        m = Image.new('L', f.size, 0)
        cx, cy = 960 - 255, 400 - 135   # 로고 버섯 아이콘
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
MUSIC = P('Marketing', 'PV', 'art', 'The_Bramblewood_Picnic.mp3')   # 사용자가 Gemini로 만든 곡
MUSIC_START = 6.0    # 곡 6초부터: 곡이 비는 24.5~26.2초 → 영상 18.5초(Uh-oh), 다시 터지는 26초 → 영상 20초(BOSS!!)
MUSIC_VOL, SFX_VOL = 0.8, 0.75   # 곡이 0dB까지 꽉 차 있어 조금 낮추고, 효과음(정규화 후 최대 1)은 그보다 작게 얹는다

def synth_audio(path, sfx_only=False):
    """sfx_only: 합성 음악은 빼고 효과음(글자 팝·휙·bonk·수확 틱)만"""
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
        elif kind == 'whoosh':  # 낫이 돌며 날아오는 휙: 노이즈를 회전 박자로 떨며 점점 크게
            s = np.random.default_rng(int(t0 * 1000) + 1).normal(0, 1, m)
            s = np.convolve(s, np.ones(6) / 6, 'same')            # 거친 고음을 깎는다
            env = (x / d) ** 1.5 * (0.6 + 0.4 * np.sin(2 * np.pi * (8 + 14 * x / d) * x)) * np.minimum(1, (d - x) / 0.01).clip(0, 1)
        out[i0:i0 + m] += (s * env * v).astype(np.float32)

    # 코드 진행 (F - C - Dm - Bb), 한 마디 = 4박 = 2초
    hz = lambda midi: 440 * 2 ** ((midi - 69) / 12)
    prog = [(53, [65, 69, 72]), (48, [64, 67, 72]), (50, [65, 69, 74]), (46, [65, 70, 74])]
    mel = [77, 76, 72, 74, 76, 72, 69, 72, 74, 72, 69, 67, 65, 67, 69, 72]
    for bar in range(0 if sfx_only else 15):
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
    if not sfx_only: note(19.5, 70, 0.8, 0.9, 'thud'); note(20.0, 60, 0.8, 0.7, 'thud')
    pops = [0.5, 1.0, 1.5, 2.55, 4.5, 5.5, 6.5, 7.0, 7.33, 7.66, 11.05, 13.0, 14.6, 15.0, 20.0, 25.5, 26.0, 26.5, 27.8]
    for i, p in enumerate(pops):
        note(p, 520 + (i % 4) * 130, 0.12, 0.35, 'pop')
    # 엔드카드: 낫이 휙 날아와(27.0) 로고를 쾅(27.3) — 큰 bonk = 낮은 thud + 높은 pop
    note(27.0, 0, 0.3, 1.3 if sfx_only else 0.55, 'whoosh')
    note(27.3, 95, 0.7, 1.1, 'thud'); note(27.3, 60, 0.9, 0.6, 'thud')
    note(27.3, 640, 0.18, 0.7, 'pop'); note(27.31, 960, 0.14, 0.4, 'pop')
    # 인게임 구간엔 잔잔한 수확 틱
    rnd = random.Random(5)
    for a, b in [(4.0, 7.0), (8.0, 11.0), (21.5, 25.5)]:
        x = a
        while x < b:
            note(x, rnd.choice([880, 990, 1175, 1320]), 0.06, 0.06, 'pop'); x += rnd.uniform(0.09, 0.22)
    # 끝: 페이드아웃
    fade = np.clip((DUR - tt) / 0.8, 0, 1)
    out *= fade
    out /= max(1e-6, np.abs(out).max()) / (1.0 if sfx_only else 0.8)
    st = np.stack([out, out], 1)
    # 아주 짧은 스테레오 지연으로 넓게
    d = int(0.012 * SR); st[d:, 1] = st[:-d, 1] * 0.9 + st[d:, 1] * 0.1
    import wave
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((st * 32767).astype('<i2').tobytes())

def make_audio(wav):
    """곡이 있으면 곡(MUSIC_START부터, 끝 1.1초 페이드아웃) + 효과음을 섞고, 없으면 합성 음악"""
    if not os.path.exists(MUSIC):
        synth_audio(wav); return
    sfx = os.path.join(OUT, 'pv_sfx.wav'); synth_audio(sfx, sfx_only=True)
    # bonk가 묻히지 않게 낫이 날아올 때(27.0) 곡을 살짝, 쾅(27.3) 직후엔 크게 낮췄다가 되돌린다
    duck = "if(between(t,27.0,27.3),0.7,if(between(t,27.3,27.55),0.45,if(between(t,27.55,27.85),0.45+0.55*(t-27.55)/0.3,1)))"
    fc = (f"[0:a]atrim=0:{DUR},asetpts=PTS-STARTPTS,afade=t=out:st={DUR - 1.1}:d=1.1,volume={MUSIC_VOL},volume='{duck}':eval=frame[m];"
          f"[1:a]volume={SFX_VOL}[s];[m][s]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95[o]")
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-ss', str(MUSIC_START), '-i', MUSIC, '-i', sfx,
                    '-filter_complex', fc, '-map', '[o]', '-ar', str(SR), '-ac', '2', wav], check=True)

# ---------- 출력 ----------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--preview', action='store_true')
    ap.add_argument('--stills', default='')
    ap.add_argument('--from', dest='t0', type=float, default=0)
    ap.add_argument('--to', dest='t1', type=float, default=DUR)
    ap.add_argument('--audio-only', action='store_true', help='이미 만든 mp4의 소리만 새로 섞어 바꾼다 (영상은 그대로 복사)')
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    if a.stills:
        for s in a.stills.split(','):
            frame(float(s)).convert('RGB').save(os.path.join(OUT, f'still_{s}.jpg'), quality=88)
        return
    wav = os.path.join(OUT, 'pv_audio.wav'); make_audio(wav)
    ow, oh = (960, 540) if a.preview else (W, H)
    name = 'MoreMustMush_PV_30s_preview.mp4' if a.preview else 'MoreMustMush_PV_30s.mp4'
    if a.audio_only:
        src = os.path.join(OUT, name); tmp = src[:-4] + '_tmp.mp4'
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', src, '-i', wav, '-map', '0:v', '-map', '1:a', '-c:v', 'copy',
                        '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', tmp], check=True)
        os.replace(tmp, src); print('->', src); return
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
