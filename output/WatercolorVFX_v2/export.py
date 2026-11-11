from math import atan2, degrees, hypot
from pathlib import Path
import json
import zipfile

from PIL import Image, ImageChops, ImageDraw, ImageOps

root = Path(__file__).parent
sources = json.loads((root / 'sources.json').read_text(encoding='utf-8'))
assets = {}
resample = Image.Resampling.LANCZOS


def load(key):
    return Image.open(sources[key]).convert('RGBA')


def clean(im, floor):
    im = im.copy()
    im.putalpha(im.getchannel('A').point(lambda a: round(max(0, a-floor)*255/(255-floor))))
    return im


def grid(im, columns, rows):
    for y in range(rows):
        for x in range(columns):
            yield im.crop((round(x*im.width/columns), round(y*im.height/rows),
                           round((x+1)*im.width/columns), round((y+1)*im.height/rows)))


def pad(im, size, offset=(0, 0)):
    out = Image.new('RGBA', (size, size))
    out.paste(im, ((size-im.width)//2+offset[0], (size-im.height)//2+offset[1]))
    return out


def trim(im, threshold):
    box = im.getchannel('A').point(lambda a: 255 if a >= threshold else 0).getbbox()
    assert box, 'Empty art'
    return im.crop(box)


def save(name, im, **metadata):
    dest = root / 'PNG' / (name + '.png')
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest, dpi=(72,72))
    with Image.open(dest) as result:
        assert result.mode == 'RGBA' and result.size == im.size
        assert result.getchannel('A').getextrema()[0] == 0
        assert all(abs(v-72) < .02 for v in result.info['dpi'])
    assets[name] = {'file': dest.relative_to(root).as_posix(), 'pixels': list(im.size), **metadata}


def caption(name, im, label):
    art = trim(clean(im, 6), 20)
    scale = 984 / max(art.size)
    art = art.resize(tuple(round(v*scale) for v in art.size), resample)
    save(name, pad(art,1024), text=label)


for name in ('level_up_arrow','sparkle_01','sparkle_02','sparkle_03'):
    path = root.parent/'WatercolorVFX'/'PNG'/'growth'/(name+'.png')
    save('growth/'+name, Image.open(path).convert('RGBA'))

for name, label, cell in zip(('combo','super_combo'), ('Combo!','SuperCombo!'), grid(load('combo'),1,2)):
    caption('text/'+name, cell, label)

locales = [('en','Boss Incoming!'), ('ko','보스 등장!'), ('ja','ボス出現！'),
           ('ru','Босс появился!'), ('uk','Бос з’явився!'), ('pt-BR','Chefe à vista!')]
for (locale,label),cell in zip(locales,grid(load('boss_text'),2,3)):
    caption('text/boss_entrance_'+locale, load('text_'+locale) if 'text_'+locale in sources else cell, label)

# The generated stroke is fitted to a centered circular orbit on the 512 canvas.
wind = trim(clean(load('wind'), 9), 64).resize((454,454),resample)
wind = pad(wind,512)
wind_fade = Image.new('L',(512,512))
wind_fade.putdata([round(255*max(0,min(1,(350-((-49-degrees(atan2(y-255.5,x-255.5)))%360))/240)))
                   for y in range(512) for x in range(512)])
wind.putalpha(ImageChops.multiply(wind.getchannel('A'),wind_fade))
save('wind/wind_orbit',wind,pivot=[.5,.5])
assert wind.getpixel((256,256))[3] == 0

hit = []
for i,cell in enumerate(grid(load('hit'),3,2)):
    art = ImageOps.contain(clean(cell,55),(236,236),resample)
    im = pad(art,256,[(-1,-4),(-2,-1),(-2,-4),(-2,4),(-2,6),(-2,6)][i])
    fades = [1,1,1,.85,.60,.30]
    im.putalpha(im.getchannel('A').point(lambda a: round(a*fades[i])))
    hit.append(im)

palette = {
    'hit_beige': ('#9e6d47','#e0ba8a','#fff5d9'),
    'critical_orange': ('#953f24','#ea8737','#ffedc0'),
    'skill_skyblue': ('#40688b','#72b9dc','#e9f9ff'),
}
for name, (dark, mid, light) in palette.items():
    strip = Image.new('RGBA',(1536,256))
    for i,im in enumerate(hit):
        tinted = ImageOps.colorize(ImageOps.grayscale(im),dark,light,mid=mid,midpoint=221)
        tinted.putalpha(im.getchannel('A'))
        strip.paste(tinted,(256*i,0))
    save('impact/'+name, strip, frames=6, frame_pixels=[256,256], pivot=[.5,.5])

edge = Image.new('L',(1024,1024))
edge.putdata([round(255*max(0,min(1,(478-hypot(x-511.5,y-511.5))/66)))
              for y in range(1024) for x in range(1024)])
for kind in ('entrance','defeat'):
    name = 'boss_'+kind
    atlas = load(name)
    if kind == 'entrance':
        row_bounds = [290, 540, 844, 1120]
        origin_x = [133,409,678,951,137,413,679,948,137,415,679,950]
        origin_y = [411,416,425,429,741,755,751,725,1009,1018,1018,1007]
        scale = 2.55
    else:
        row_bounds = [0,362,724,1086]
        origin_x = [180,543,906,1266,180,544,905,1267,182,545,906,1267]
        origin_y = [187,186,186,189,540,540,541,539,902,903,903,903]
        scale = 2.55
    for i,(cx,cy) in enumerate(zip(origin_x,origin_y),1):
        row,col = divmod(i-1,4)
        l = round(col*atlas.width/4)
        r = round((col+1)*atlas.width/4)
        t,b = row_bounds[row:row+2]
        cell = atlas.crop((l+5,t+5,r-5,b-5))
        art = clean(cell,22).resize(tuple(round(v*scale) for v in cell.size),resample)
        im = Image.new('RGBA',(1024,1024))
        im.paste(art,(round(512-(cx-l-5)*scale),round(512-(cy-t-5)*scale)))
        fade = {10:.8,11:.55,12:.24}.get(i,1)
        alpha = im.getchannel('A').point(lambda a: round(a*fade))
        im.putalpha(ImageChops.multiply(alpha,edge))
        assert im.getchannel('A').getextrema()[1] > 10
        save(f'boss/{kind}/frame_{i:02}', im, pivot=[.5,.5], frame=i)

assert len(assets)==40
(root/'manifest.json').write_text(json.dumps({
    'dpi':72, 'hit_order':'left-to-right', 'boss_order':'frame_01 through frame_12',
    'assets':assets
},ensure_ascii=False,indent=2),encoding='utf-8')

preview = Image.new('RGB',(1200,1024),'#35473c')
draw = ImageDraw.Draw(preview)


def show(name, x, y, limit, tight=False):
    im = Image.open(root/assets[name]['file']).convert('RGBA')
    if tight:
        im = trim(im,8)
    im.thumbnail(limit,resample)
    preview.paste(im,(x,y),im)


draw.text((16,14),'WIND / GROWTH',fill='white')
show('wind/wind_orbit',16,28,(144,144))
for i,name in enumerate(('level_up_arrow','sparkle_01','sparkle_02','sparkle_03')):
    show('growth/'+name,176+i*144,28,(144,144))
for i,name in enumerate(palette):
    y=178+i*90
    draw.text((16,y),name,fill='white')
    show('impact/'+name,16,y+14,(432,72))
for i,kind in enumerate(('entrance','defeat')):
    y=450+i*108
    draw.text((16,y),f'BOSS {kind.upper()} / 12 FRAMES',fill='white')
    for j in range(12):
        show(f'boss/{kind}/frame_{j+1:02}',16+j*97,y+15,(95,95))
show('text/combo',16,678,(320,94),True)
show('text/super_combo',350,678,(420,94),True)
for i,(locale,_) in enumerate(locales):
    show('text/boss_entrance_'+locale,16+(i%3)*390,792+(i//3)*106,(370,94),True)
preview.save(root/'preview.jpg',quality=94,dpi=(72,72))

with zipfile.ZipFile(root/'MoreMustMush_Watercolor_VFX_v2.zip','w',zipfile.ZIP_DEFLATED) as z:
    for f in [root/'manifest.json', root/'preview.jpg', *(root/r['file'] for r in assets.values())]:
        z.write(f,f.relative_to(root))
print(f'{len(assets)} verified transparent PNG files')
