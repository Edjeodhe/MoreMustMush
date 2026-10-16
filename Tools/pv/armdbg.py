import sys; sys.path.insert(0, r'C:\Project\MoreMustMush\Tools\pv')
import build_pv as B
from PIL import Image, ImageDraw
l = B.evo_look('leaf')
for n in [l['armUp'], l['armLo']]:
    im = B.evo_part(n); print(n, im.size, im.getbbox())
out = Image.new('RGBA', (1800, 600), (220, 230, 210, 255))
for i, (r, e) in enumerate([(0, 0), (60, 0), (120, 0), (140, 20), (140, 60)]):
    lay = Image.new('RGBA', (900, 900), (0, 0, 0, 0))
    B._arm(lay, l, 1, r, e)
    ox, oy = 450, 900 * 0.85
    d = ImageDraw.Draw(lay); sx, sy = ox + 0.4464 * 100, oy - 2.197 * 100
    d.ellipse((sx - 6, sy - 6, sx + 6, sy + 6), fill='red'); d.ellipse((ox - 6, oy - 6, ox + 6, oy + 6), fill='blue')
    out.alpha_composite(lay.crop((150, 200, 750, 800)).resize((360, 360)), (i * 360, 100))
out.save(sys.argv[1])
