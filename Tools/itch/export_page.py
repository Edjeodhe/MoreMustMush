# itch.io 게임 페이지(2026-10-15 적용본)를 이미지까지 품은 HTML 한 파일로 내보낸다.
# 설명 = 원래 설명(description_old_backup.html) 사이에 인트로·GIF 2×2·Features·Controls를 끼운 것 (itch에 저장한 것과 같은 조립)
# 출력: Marketing/itch/MoreMustMush_itch.html (인터넷 없이 열림, 게임 실행 칸은 스크린샷 + itch 링크)
import base64, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
D = os.path.join(ROOT, 'Marketing', 'itch')
GAME_URL = 'https://minsung0421.itch.io/moremustmush'

def data(name):
    ext = name.rsplit('.', 1)[1].lower()
    mime = {'jpg': 'image/jpeg', 'png': 'image/png', 'gif': 'image/gif'}[ext]
    with open(os.path.join(D, name), 'rb') as f:
        return f'data:{mime};base64,' + base64.b64encode(f.read()).decode()

def img(name, alt):
    return f'<img src="{data(name)}" alt="{alt}">'

orig = open(os.path.join(D, 'description_old_backup.html'), encoding='utf-8').read()
orig = re.sub(r'^<!--.*?-->\s*', '', orig, flags=re.S)
H = '<p><strong>How to Play</strong></p>'
s = orig.index(H); e = orig.index('</ol>', s) + 5
before, howlist, after = orig[:s], orig[s + len(H):e], orig[e:]

features = ('<h2>Features</h2><ul>'
    '<li><strong>75 real mushroom species</strong> to harvest and fill your collection with</li>'
    '<li><strong>80+ upgrades</strong> on the Mycelium Tree: more harvesters, bar skills, combos and gold</li>'
    '<li><strong>5 regions</strong> with their own weather, and a boss waiting every 10 stages</li>'
    '<li><strong>A World Tree</strong> that grows with you, with 10 little critters who gather mushrooms while you are away</li>'
    '<li>Skins for your harvesters and critters</li></ul>')
controls = ('<h2>Controls</h2><ul>'
    '<li><strong>Drag &amp; release</strong>: pull back the harvester and let go to launch</li>'
    '<li><strong>Move mouse</strong>: slide the bar left and right</li>'
    '<li><strong>F / Right-click</strong>: game speed x1, x2, x4</li>'
    '<li><strong>Esc</strong>: pause</li></ul>'
    '<p>Your progress is saved in your browser automatically. Language can be changed in Options.</p>')

desc = (f'<p class="c">{img("intro.png", "Giant mushrooms swallowed the village. Bounce. Harvest. Grow.")}</p>'
        + before
        + '<h2>How to Play</h2>'
        + f'<p class="c">{img("step1_launch.gif", "1. Launch")} {img("step2_upgrade.gif", "2. Upgrade")}</p>'
        + f'<p class="c">{img("step3_fever.gif", "3. Fever!")} {img("step4_worldtree.gif", "4. World Tree")}</p>'
        + howlist + features + controls + '<p><br></p>' + after)

shots = ''.join(f'<a href="#" class="shot">{img(n, "")}</a>' for n in
                ['shot1_harvest.jpg', 'shot2_fever.jpg', 'shot3_tree.jpg', 'shot4_worldtree.jpg'])

html = f'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>MoreMustMush by minsung0421</title>
<style>
  /* itch.io 테마 값: BG #5D9E3A · BG2 #FFFFFF(반투명) · Text #263A2C · Link #E0457B · Headers #2E8B4E · Buttons #EC4C8A · Lato Large · 스크린샷 Sidebar */
  :root {{ --bg:#5d9e3a; --bg2:rgba(255,255,255,0.88); --text:#263a2c; --link:#e0457b; --head:#2e8b4e; --btn:#ec4c8a; --line:#d6e9c4; }}
  * {{ box-sizing: border-box; }}
  body {{ margin:0; font:16px/1.5 Lato, "Segoe UI", sans-serif; color:var(--text); background:var(--bg) url("{data('background.jpg')}") no-repeat center top fixed; background-size: cover; }}
  .wrapper {{ max-width:1280px; margin:40px auto 60px; background:var(--bg2); }}
  .header {{ text-align:center; padding:12px 16px 0; }}
  .header img {{ max-width:100%; height:auto; }}
  .game_frame {{ position:relative; aspect-ratio:16/9; background:#1b120b url("{data('shot1_harvest.jpg')}") center/cover; display:grid; place-items:center; }}
  .game_frame a {{ background:var(--btn); color:#fff; text-decoration:none; font-weight:700; font-size:18px; padding:12px 26px; border-radius:4px; box-shadow:0 2px 0 rgba(0,0,0,.2); }}
  .cols {{ display:flex; gap:20px; padding:20px; }}
  .left_col {{ flex:0 1 873px; min-width:0; }}
  .right_col {{ flex:0 0 347px; display:flex; flex-direction:column; gap:10px; }}
  .right_col img {{ width:100%; display:block; border-radius:2px; }}
  .formatted_description img {{ max-width:100%; height:auto; vertical-align:top; }}
  .formatted_description p.c {{ text-align:center; }}
  .formatted_description h2 {{ color:var(--head); font-size:1.5em; font-weight:700; margin:1.2em 0 .6em; }}
  .formatted_description a {{ color:var(--link); }}
  .formatted_description ol, .formatted_description ul {{ padding-left:1.5em; }}
  .footer {{ text-align:center; font-size:13px; color:#5e6f60; padding:0 20px 24px; }}
  @media (max-width:900px) {{ .cols {{ flex-direction:column; }} .right_col {{ flex-direction:row; flex-wrap:wrap; }} .right_col .shot {{ width:calc(50% - 5px); }} .wrapper {{ margin-top:0; }} }}
</style>
</head>
<body>
  <div class="wrapper">
    <div class="header">{img('banner.png', 'MoreMustMush')}</div>
    <div class="game_frame"><a href="{GAME_URL}" target="_blank" rel="noopener">Play on itch.io</a></div>
    <div class="cols">
      <div class="left_col formatted_description">
{desc}
      </div>
      <div class="right_col">{shots}</div>
    </div>
    <div class="footer"><a href="{GAME_URL}" style="color:var(--link)">MoreMustMush</a> by minsung0421 · itch.io</div>
  </div>
</body>
</html>
'''
out = os.path.join(D, 'MoreMustMush_itch.html')
open(out, 'w', encoding='utf-8').write(html)
print(out, round(os.path.getsize(out) / 1e6, 1), 'MB')
