import sys, os, subprocess, shutil
sys.path.insert(0, r'C:\Project\MoreMustMush\Tools\itch')
import build_itch as B
out = sys.argv[1]
B.CLIP_W, B.CLIP_H = 640, 360
for name, p in B.PLAN.items():
    clog = os.path.join(B.CAP, 'tree_cursor.txt') if p.get('cursor') else None
    clicks = []
    if clog:
        for ln in open(clog):
            q = ln.split()
            if len(q) == 3 and q[1] == 'buy': clicks.append(int(q[0]) + 1)
    d, k = B.make_clip(p['clip'], p['start'], p['count'], p['crop'], cursor_log=clog, click_frames=clicks)
    g = os.path.join(out, name.split('_', 1)[1] + '.gif')
    flt = 'split[s0][s1];[s0]palettegen=max_colors=%d:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle' % p.get('colors', 200)
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-framerate', '12.5', '-i', os.path.join(d, 'c%04d.png'), '-filter_complex', flt, '-loop', '0', g], check=True)
    shutil.copy(os.path.join(d, 'c%04d.png' % min(k, 40)), g.replace('.gif', '_still.png'))
    print(g, round(os.path.getsize(g) / 1e6, 2), 'MB')
