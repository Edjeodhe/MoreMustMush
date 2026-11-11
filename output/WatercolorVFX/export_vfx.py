from pathlib import Path
from PIL import Image, ImageOps, ImageChops, ImageDraw
from math import hypot
import json
import zipfile

root = Path(__file__).parent
source = json.loads((root / 'sources.json').read_text(encoding='utf-8'))
files = {}
resample = Image.Resampling.LANCZOS

def load(name):
    return Image.open(source[name]).convert('RGBA')

def save(im, name):
    dest = root / 'PNG' / (name + '.png')
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest, dpi=(72, 72))
    check = Image.open(dest)
    assert check.mode == 'RGBA' and check.getchannel('A').getextrema()[0] == 0
    assert all(abs(dpi - 72) < .02 for dpi in check.info['dpi'])
    files[name] = {'file': dest.relative_to(root).as_posix(), 'pixels': list(check.size)}

def cells(im, columns, rows):
    for y in range(rows):
        for x in range(columns):
            yield im.crop((round(x*im.width/columns),round(y*im.height/rows),
                           round((x+1)*im.width/columns),round((y+1)*im.height/rows)))

def clean(im, floor=5):
    im = im.copy()
    im.putalpha(im.getchannel('A').point(lambda a: round(max(0, (a-floor)*255/(255-floor)))))
    return im

def center_fit(im, canvas, content, threshold=18, square=False):
    box = im.getchannel('A').point(lambda a: 255 if a >= threshold else 0).getbbox()
    assert box, 'No visible art'
    art = im.crop(box)
    if square:
        art = art.resize((content, content), resample)
    else:
        scale = content / max(art.size)
        art = art.resize(tuple(max(1, round(d*scale)) for d in art.size), resample)
    out = Image.new('RGBA', (canvas,canvas))
    out.paste(art, ((canvas-art.width)//2,(canvas-art.height)//2))
    return out

def timeline(name, columns, rows, floor=7):
    output = []
    for index, cell in enumerate(cells(load(name), columns, rows)):
        im = clean(ImageOps.contain(cell, (256,256), resample), floor)
        out = Image.new('RGBA', (256,256))
        out.paste(im, ((256-im.width)//2,(256-im.height)//2))
        # Suppress stray atlas gutters without moving the frame pivot.
        edge = Image.new('L', (256,256))
        fade = (.64 if index == 6 else .26 if index == 7 else 1) if name.startswith('boss_') else (.46 if index == 5 else .8 if index == 4 else 1)
        edge.putdata([round(255 * fade * max(0, min(1, (min(x,y,255-x,255-y)-2)/12,
                          (126-hypot(x-127.5,y-127.5))/14)))
                      for y in range(256) for x in range(256)])
        out.putalpha(ImageChops.multiply(out.getchannel('A'),edge))
        output.append(out)
    return output

def strip(frames):
    out = Image.new('RGBA', (256*len(frames),256))
    for i, im in enumerate(frames):
        out.paste(im,(256*i,0))
    return out

growth = list(cells(load('growth'), 2, 2))
arrow = center_fit(clean(growth[0]), 256, 226)
fade = Image.new('L',(1,256))
fade.putdata([round(255*(1-max(0,min(1,(y-117)/124))**1.65)) for y in range(256)])
arrow.putalpha(ImageChops.multiply(arrow.getchannel('A'), fade.resize((256,256))))
save(arrow, 'growth/level_up_arrow')
for i, cell in enumerate(growth[1:],1):
    save(center_fit(clean(cell),256,[64,54,62][i-1]),f'growth/sparkle_{i:02}')

hit = timeline('hit',3,2,10)
save(strip(hit),'impact/hit_beige')
for name, stops in {
    'critical_orange': ('#873824','#e4873b','#fff4d7'),
    'skill_skyblue': ('#355f8a','#64b9e1','#edfaff'),
}.items():
    frames = []
    for im in hit:
        # Map luminance to a palette while retaining exactly the same alpha.
        mapped = ImageOps.colorize(ImageOps.grayscale(im), stops[0], stops[2], mid=stops[1], midpoint=210)
        mapped.putalpha(im.getchannel('A'))
        frames.append(mapped)
    save(strip(frames), 'impact/'+name)

for name, im in zip(('combo','super_combo'),cells(load('combo'),1,2)):
    save(center_fit(clean(im),1024,992), 'text/'+name)
locales = {'en':'Boss Incoming!', 'ko':'보스 등장!', 'ja':'ボス出現！',
           'ru':'Босс появился!', 'uk':'Бос з’явився!', 'pt-BR':'Chefe à vista!'}
for (locale, label), im in zip(locales.items(),cells(load('boss_text'),2,3)):
    if 'text_'+locale in source:
        im = load('text_'+locale)
    name = f'text/boss_entrance_{locale}'
    save(center_fit(clean(im),1024,992), name)
    files[name]['text'] = label

wind = center_fit(clean(load('wind'),12),512,460,112, square=True)
radial = Image.new('L',(512,512))
radial.putdata([round(255*min(1,max(0,(hypot(x-255.5,y-255.5)-136)/9),
                                  max(0,(240-hypot(x-255.5,y-255.5))/9)))
                for y in range(512) for x in range(512)])
wind.putalpha(ImageChops.multiply(wind.getchannel('A'),radial))
save(wind,'wind/wind_orbit')

for kind in ('defeat','entrance'):
    frames = timeline('boss_'+kind,4,2,8)
    for i, frame in enumerate(frames,1):
        save(frame,f'boss/{kind}/frame_{i:02}')
    save(strip(frames),f'boss/{kind}_sheet')

(root/'manifest.json').write_text(json.dumps({
    'dpi':72, 'sprite_pivot':[.5,.5], 'frame_order':'left-to-right',
    'frame_dimensions':[256,256], 'impact_frames':6, 'boss_frames':8, 'assets':files
},ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(root/'MoreMustMush_Watercolor_VFX.zip','w',zipfile.ZIP_DEFLATED) as z:
    for name in ('manifest.json',):
        z.write(root/name,name)
    for f in files.values():
        z.write(root/f['file'],f['file'])
print(f'{len(files)} RGBA PNGs checked at 72 dpi')

preview = Image.new('RGB', (1120,1090), '#34483d')
draw = ImageDraw.Draw(preview)
def preview_row(names, y, scale, trim=False):
    x = 16
    for name in names:
        im = Image.open(root/files[name]['file']).convert('RGBA')
        if trim:
            im = im.crop(im.getchannel('A').getbbox())
            im.thumbnail(scale, resample)
        else:
            im = im.resize(scale, resample)
        preview.paste(im,(x,y),im)
        x += im.width+20

draw.text((16,12), 'GROWTH / SPARKLES / WIND', fill='white')
preview_row(['growth/level_up_arrow', *[f'growth/sparkle_{i:02}' for i in range(1,4)], 'wind/wind_orbit'],30,(148,148))
for i,n in enumerate(['hit_beige','critical_orange','skill_skyblue']):
    y=186+i*109
    draw.text((16,y),n,fill='white')
    preview_row(['impact/'+n],y+14,(576,96))
for i,n in enumerate(['entrance','defeat']):
    y=516+i*129
    draw.text((16,y),'BOSS '+n.upper(),fill='white')
    preview_row([f'boss/{n}_sheet'],y+14,(896,112))
preview_row(['text/combo','text/super_combo'],783,(355,80),True)
preview_row([f'text/boss_entrance_{n}' for n in ('en','ko','ja')],886,(340,84),True)
preview_row([f'text/boss_entrance_{n}' for n in ('ru','uk','pt-BR')],990,(340,84),True)
preview.save(root/'preview.jpg',quality=94,dpi=(72,72))
