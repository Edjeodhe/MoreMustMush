# itch.io 페이지 "How to Play" GIF 카드 4장 (몽환 수채화판: 크림 종이·갈색 글씨·번호 배지).
# 입력: Temp/itchcap/{round,tree,farm}/f00001.png… (Unity 플레이 모드에서 25fps로 찍은 1920×1080 프레임), tree_cursor.txt
#       Marketing/itch_dream/out/deco_badge_1~4.png (cut_deco.py)
# 출력: Marketing/itch_dream/out/step1_launch.gif … step4_worldtree.gif
# 사용: python Tools/itch/build_itch_dream_gifs.py [step1_launch …]
import os, sys, shutil, subprocess
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
import build_itch as old          # make_clip(녹화 프레임 → 잘라 줄인 시퀀스)·커서 그리기를 그대로 쓴다
import build_itch_dream as dream  # 팔레트·글꼴

CAP, WORK = old.CAP, old.WORK
OUT = dream.OUT
PAPER, INK, INK2 = dream.PAPER, dream.INK, dream.INK2
EDGE = (222, 198, 160)            # 종이 테두리 (연한 갈색)
CW, CH = old.CW, old.CH           # 424×372, 두 장씩 나란히
CLIP_W, CLIP_H, CLIP_X, CLIP_Y = old.CLIP_W, old.CLIP_H, old.CLIP_X, old.CLIP_Y

def card_layers(num, title, body, color):
    bg = old.card_base(CW, CH, fill=PAPER, edge=EDGE)
    d = ImageDraw.Draw(bg)
    x = 18; y = CLIP_Y + CLIP_H + 14
    badge = Image.open(os.path.join(OUT, f'deco_badge_{num}.png')).convert('RGBA')
    bh = 40; badge = badge.resize((round(badge.width * bh / badge.height), bh), Image.LANCZOS)
    bg.alpha_composite(badge, (x - 4, y - 2))
    d.text((x + badge.width + 6, y + bh // 2), title, font=dream.font(dream.HEAD, 26), fill=color, anchor='lm')
    y += bh + 6
    f = dream.font(dream.BODY, 15)
    for ln in dream.wrap(d, body, f, CW - x * 2): d.text((x, y), ln, font=f, fill=INK2); y += 22
    # 클립 모서리를 종이색으로 둥글게 가린다
    fg = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    hole = old.rounded_mask((CLIP_W, CLIP_H), 12)
    corner = Image.new('RGBA', (CLIP_W, CLIP_H), PAPER + (255,))
    corner.putalpha(Image.eval(hole, lambda a: 255 - a))
    fg.alpha_composite(corner, (CLIP_X, CLIP_Y))
    return bg, fg

def make_card_gif(name, num, title, body, color, clip_dir, fps, colors=200):
    bg, fg = card_layers(num, title, body, color)
    frames = sorted(f for f in os.listdir(clip_dir) if f.startswith('c'))
    dst = os.path.join(WORK, name + '_card'); shutil.rmtree(dst, ignore_errors=True); os.makedirs(dst)
    for i, f in enumerate(frames, 1):
        im = bg.copy()
        im.alpha_composite(Image.open(os.path.join(clip_dir, f)).convert('RGBA'), (CLIP_X, CLIP_Y))
        im.alpha_composite(fg)
        flat = Image.new('RGB', im.size, PAPER); flat.paste(im, (0, 0), im)   # 카드 바깥 모서리 = 본문 종이색(BG2)
        flat.save(os.path.join(dst, f'k{i:04d}.png'))
    out = os.path.join(OUT, name + '.gif')
    flt = 'split[s0][s1];[s0]palettegen=max_colors=%d:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle' % colors
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-framerate', str(fps), '-i', os.path.join(dst, 'k%04d.png'),
                    '-filter_complex', flt, '-loop', '0', out], check=True)
    print(name, round(os.path.getsize(out) / 1e6, 2), 'MB')

MOSS, CORAL, DUSK = dream.MOSS, dream.CORAL, dream.DUSK
HONEY = (196, 140, 52)
STEPS = {
    'step1_launch': dict(num=1, title='Launch', color=MOSS,
        body='Pull back the harvester and let go. Slide the bar with your mouse to keep it bouncing through the mushrooms.'),
    'step2_upgrade': dict(num=2, title='Upgrade', color=DUSK,
        body='Spend your gold on the Mycelium Tree. Each node makes your harvesters stronger, faster and more numerous.'),
    'step3_combo': dict(num=3, title='Combos & Skills', color=CORAL,
        body='Chain hits into combos. Skills go off on their own: lightning, spore bursts, magnets and little helper harvesters.'),
    'step4_worldtree': dict(num=4, title='World Tree', color=HONEY,
        body='Grow the World Tree with your harvest. Mushroom spirits move in and keep gathering while you are away.'),
}
# 녹화 클립: round = 47스테이지 한 판(바 자동 추적), tree = 노드 툴팁·구매(커서 기록), farm = 세계수 8단계·정령 8명
PLAN = {
    'step1_launch': dict(clip='round', start=1, count=150, crop=(240, 270, 1680, 1080)),
    'step2_upgrade': dict(clip='tree', start=1, count=258, crop=(340, 60, 1760, 859), cursor=True),
    'step3_combo': dict(clip='round', start=140, count=120, crop=(240, 270, 1680, 1080), colors=160),
    'step4_worldtree': dict(clip='farm', start=1, count=150, crop=(360, 150, 1610, 853)),
}

def build(only=None):
    for name, p in PLAN.items():
        if only and name not in only: continue
        if not os.path.isdir(os.path.join(CAP, p['clip'])): print('skip', name); continue
        clog = os.path.join(CAP, 'tree_cursor.txt') if p.get('cursor') else None
        clicks = []
        if clog:
            for ln in open(clog):
                q = ln.split()
                if len(q) == 2 and q[1] == 'buy': clicks.append(int(q[0]) + 1)
        d, k = old.make_clip(p['clip'], p['start'], p['count'], p['crop'], cursor_log=clog, click_frames=clicks)
        s = STEPS[name]
        make_card_gif(name, s['num'], s['title'], s['body'], s['color'], d, 12.5, p.get('colors', 200))

if __name__ == '__main__':
    build(sys.argv[1:] or None)
