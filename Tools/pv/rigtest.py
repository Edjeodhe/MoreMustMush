import sys, math
sys.path.insert(0, r'C:\Project\MoreMustMush\Tools\pv')
import build_pv as B
from PIL import Image
out = Image.new('RGBA', (1600, 700), (200, 220, 190, 255))
for i, k in enumerate(['leaf', 'fire', 'dew', 'star', 'moon']):
    for j, ph in enumerate([0.25, 0.75]):
        B.paste_kid_wave(out, k, 160 + i * 300 + j * 0, 340 + j * 330, t=ph * 0.5, scale=0.55)
out.save(sys.argv[1])
