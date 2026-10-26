# 균사 트리 배치안 A(15차, layout15.json) → 게임 DB(Assets/Resources/DB/db.json) + 엑셀 옮기기용 CSV.
# 실행: python3 to_unity_db.py   (이 폴더에서. db.json을 덮어쓰고 ../엑셀옮기기/*.csv를 만든다)
#
# 규칙
# - 배치안의 노드 하나 = Upgrade 시트 한 행. 같은 스킬(줄)의 행들은 Line 열이 같고, 앞 행을 최대 레벨까지 올려야 다음 행이 열린다 (Upg_Req).
# - Efx_Lv0~10 = 그 행을 0~N레벨 올렸을 때 "줄 전체" 값 (Lv0 = 앞 행까지의 값). 게임은 줄에서 마지막으로 산 행의 값을 쓴다.
# - Pos_X·Pos_Y = 배치안 네모 칸 좌표 (가운데 사냥 준비가 0,0 · y는 아래가 +).
# - 가격은 임시: 레어도(자리로 정한 값, depth)마다 ×PRICE_K. 균사석 스킬·사냥 준비 2레벨부터는 균사석.
# - 세계수 노드 5개(md_chef·gm_farm·gm_spore·gm_herb·gm_gourmet)는 Branch "wt"로 남긴다 (트리에 안 보임, 세계수 코드가 씀).
import csv, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
DB = os.path.join(ROOT, 'Assets', 'Resources', 'DB', 'db.json')
LAYOUT = os.path.join(HERE, 'layout15.json')
OUT_CSV = os.path.join(HERE, '..', '엑셀옮기기')
MAXL = 10

PRICE0, PRICE_K, LEVEL_K = 15, 3.0, 1.6
GEM_COST = [10, 20, 40, 80, 160]
ROOT_GOLD = 12
ROOT_GEM = [3, 5, 8, 12, 18, 27, 40, 60, 90]

# 줄: 배치안 이름 → (키, 가지, 태그, 줄 값 [Lv0, Lv1, ...], 아이콘, 색, 설명)
LINES = {
    '사냥 준비': ('power', 'md', 'bar', [2 * i for i in range(11)], 's_power', '#ff9a5a',
              '영구 수확기가 슬라이드 바에 맞을 때마다 3초 동안 공격력이 올라요. 첫 레벨은 골드, 2레벨부터는 균사석으로 올려요.'),
    # ── 식용
    '효율적 사냥': ('ed_hunt', 'ed', 'rate', [0, .1, .2, .3, .4, .5], 'n_score', '', '버섯을 칠 때 얻는 점수가 늘어나요.'),
    '흥정 기술': ('ed_haggle', 'ed', 'rate', [0, .1, .15, .2], 'n_price', '', '라운드가 끝날 때 점수가 골드로 바뀌는 비율이 올라요.'),
    '단골 손님': ('ed_regular', 'ed', 'rate', [0, .3, .4, .5], 'n_trade', '', '라운드가 끝날 때 점수가 골드로 바뀌는 비율이 크게 올라요. 흥정 기술과 더해져요.'),
    '넓은 시야': ('ed_view', 'ed', 'stage', [0, 1, 2, 3], 'n_cols', '', '필드의 버섯 군락지가 늘어나요. 군락지가 늘면 필드도 넓어져요.'),
    '하늘의 시야': ('ed_sky', 'ed', 'stage', [0, 2, 4, 6], 'n_cols', '', '필드의 버섯 군락지가 2개씩 늘어나요. 군락지가 늘면 필드도 넓어져요.'),
    '신의 눈': ('ed_god', 'ed', 'stage', [0, 3], 'n_cols', '', '필드의 버섯 군락지가 3개 늘어나요.'),
    '희귀한 버섯 소문': ('ed_epic', 'ed', 'stage', [0, .1, .15, .2, .25], 'n_rare', '', '에픽 버섯이 더 자주 나와요.'),
    '굉장한 버섯 전설': ('ed_unique', 'ed', 'stage', [0, .05, .1, .15], 'n_rare', '', '유니크 버섯이 더 자주 나와요.'),
    '고속 범람': ('ed_regen', 'ed', 'stage', [7, 6, 5, 3], 'n_regen', '', '다 딴 군락지가 더 빨리 다시 돋아나요.'),
    '더 많은 버섯': ('ed_more', 'ed', 'stage', [3, 4, 5, 6], 'n_size', '', '군락지 둘레에 돋는 단일 버섯이 늘어나요.'),
    '일확천금의 신화': ('ed_gold', 'ed', 'stage', [0, .01, .03, .05], 'n_gold', '', '황금 변종 버섯이 더 자주 나와요. 황금 변종은 점수 ×10, 수확량 ×5예요.'),
    '재빠른 손길': ('ed_hand1', 'ed', 'rate', [0, .05, .1, .15], 'n_bonus', '', '일반 버섯을 딸 때 수확량이 늘어나요.'),
    '섬세한 손길': ('ed_hand2', 'ed', 'rate', [0, .05, .1, .15], 'n_bonus', '', '에픽 버섯을 딸 때 수확량이 늘어나요.'),
    '신들린 손길': ('ed_hand3', 'ed', 'rate', [0, .05, .1, .15], 'n_bonus', '', '유니크 버섯을 딸 때 수확량이 늘어나요.'),
    '은둔 고수': ('ed_hand4', 'ed', 'rate', [0, .03, .06, .09], 'n_bonus', '', '보스를 쓰러뜨리고 얻는 레전더리 버섯 수확량이 늘어나요.'),
    '플렉스': ('ed_flex', 'ed', 'rate', [0, .03, .05, .07], 'n_multi', '#ffe36e', '버섯을 칠 때 확률로 점수를 3배로 얻어요.'),
    '할아버지의 전설': ('ed_legend', 'ed', 'skill', [0, .1, .2, .3, .4, .5], 's_fest', '#ffd84a', '라운드 마지막 5초 동안 점수와 수확량이 늘어나요. 균사석으로 올려요.'),
    # ── 약용
    '스프링': ('md_spring', 'md', 'bar', [0, 3, 4, 5], 's_accel', '#6fd3ff', '바에 맞으면 [가속] 버프: 수확기 속도 +50%.'),
    '관통': ('md_pierce', 'md', 'bar', [0, 3, 4, 5], 's_pierce', '#ffd6d6', '바에 맞으면 [관통] 버프: 버섯 군락에 튕기지 않고 지나가며 피해를 줘요.'),
    '유도': ('md_homing', 'md', 'bar', [0, 3, 4, 5], 's_homing', '#ffa8e0', '바에 맞으면 [유도] 버프: 튕긴 뒤 가장 가까운 버섯 군락으로 날아가요.'),
    '할아버지 손은 약손': ('md_heal', 'md', 'bar', [0, 3, 4, 5], 's_clear', '#bff6ff', '바에 맞으면 포자 디버프를 지우고 [약손] 버프: 그동안 디버프에 걸리지 않아요.'),
    '이독제독': ('md_poison', 'md', 'skill', [0, .03, .04, .05], 's_sharpen', '#dfe6ee', '단일 버섯을 딸 때 확률로 다음 타격 피해 +200%.'),
    '포자 폭발': ('md_burst', 'md', 'skill', [0, .01, .02, .03], 's_burst', '#ffb347', '버섯 군락에 부딪힐 때 확률로 필드의 모든 버섯에 피해(공격력 10%).'),
    '분열': ('md_split', 'md', 'bar', [0, 3, 4, 5], 's_split', '#ffe36e', '바에 맞을 때 10% 확률로 [분열] 버프: 꼬마 수확기(공격력 10%)가 생겨요.'),
    '연속 번개': ('md_bolt', 'md', 'skill', [0, .03, .05, .07], 's_bolt', '#fff36b', '군락을 다 딸 때 확률로 다른 군락에 번개(공격력 30%).'),
    '회오리': ('md_tornado', 'md', 'skill', [0, .03, .04, .05], 's_tornado', '#b8f0e0', '버섯 군락에 부딪힐 때 확률로 그 군락에 지속 피해(공격력 5%).'),
    '환경 복원': ('md_env', 'md', 'stage', [0, 1, 2, 3], 'n_dev_stump', '', '필드에 숲 장치가 놓여요. 1레벨 그루터기, 2레벨 꽃밭, 3레벨 도토리 점등.'),
    '노인의 지혜': ('md_wisdom', 'md', 'bar', [0, .01, .02, .03], 'n_barrate', '#c9f0a0', '바에 맞으면 [지혜] 버프 3초: 수확기 스킬(이독제독·포자 폭발·연속 번개·회오리) 발동률이 올라요.'),
    '피버까지 피버': ('md_fever', 'md', 'bar', [0, 3, 4, 5], 'n_combo', '#ff8a5a', '바에 맞으면 [피버] 버프: 콤보 계수 +50%.'),
    '탈피': ('md_molt', 'md', 'harvester', [0, .1, .2, .3], 'n_resist', '', '포자 구름에 닿아 디버프를 얻을 때 확률로 바로 풀려요.'),
    '전설의 할아버지': ('md_grand', 'md', 'bar', [0, .02, .04, .06, .08, .10], 's_power', '#ffd84a', '라운드를 시작할 때 가진 바 버프가 확률로 바로 걸려요. 균사석으로 올려요.'),
    # ── 독성
    '한가득 포옹': ('ps_hug', 'ps', 'bar', [0, .2, .3, .5], 'n_bar', '', '슬라이드 바가 길어져요. 분신 바도 같은 길이예요.'),
    '분신술': ('ps_clone', 'ps', 'bar', [0, 3, 4, 5], 's_barclone', '#a8d8ff', '바에 맞을 때 10% 확률로 맵 양 끝에 분신 바가 생겨요.'),
    '거울 분신술': ('ps_mirror', 'ps', 'bar', [1, 2], 's_barclone', '#a8d8ff', '분신 바를 2개까지 둘 수 있어요.'),
    '날 갈기': ('ps_blade', 'ps', 'harvester', [1, 3, 5, 7, 10, 15], 'n_atk', '', '수확기 공격력이 올라요.'),
    '노장의 체력': ('ps_stamina', 'ps', 'stage', [0, 2, 5, 8, 11, 14], 'n_dur', '', '라운드 시간이 늘어나요 (기본 10초).'),
    '치명 공격': ('ps_crit', 'ps', 'harvester', [0, .02, .04, .06, .08, .10], 'n_crit', '', '치명타 확률이 올라요. 100%를 넘으면 넘친 만큼 치명타 피해에 더해요 (극한 치명타).'),
    '치명 박살': ('ps_critdmg', 'ps', 'harvester', [.5, 1.0, 1.2, 1.5], 'n_crit2', '', '치명타 피해가 올라요 (기본 150%).'),
    '초토화': ('ps_size', 'ps', 'harvester', [0, .1, .2, .3], 'n_hvsize', '', '수확기가 커져요.'),
    '바람의 길': ('ps_wind', 'ps', 'harvester', [0, .1, .2, .3], 'n_spd', '', '수확기가 빨라져요.'),
    '회전 칼날': ('ps_kid', 'ps', 'harvester', [0, 1, 2, 3], 's_blade', '#e6e6f0', '수확기 둘레를 도는 꼬마 수확기가 늘어나요 (공격력 10%).'),
    '회전 회오리': ('ps_kidatk', 'ps', 'harvester', [0, .05, .1, .15], 's_blade', '#e6e6f0', '도는 꼬마 수확기의 공격력이 올라요.'),
    '콤보 계수': ('ps_combo', 'ps', 'bar', [0, .2, .4, .6, .8, 1.0], 'n_combo', '', '바에 튕길 때마다 오르는 콤보 점수 배율이 커져요.'),
    '장인의 도구': ('ps_tools', 'ps', 'harvester', [0, 1, 2, 3, 4], 'n_ball', '', '수확기를 하나 더 장착할 수 있어요. 균사석으로 올려요.'),
}
GEM_LINES = {'ed_legend', 'md_grand', 'ps_tools'}
WORLD = ['md_chef', 'gm_farm', 'gm_spore', 'gm_herb', 'gm_gourmet']
BASE_ID = {'ed': 13101, 'md': 12101, 'ps': 11101}
COST_ID0 = 91001


def levels_of(lab, nlv):
    if lab == '':
        return [1]
    a = lab.replace('Lv.', '')
    if '~' in a:
        x, y = a.split('~')
        return list(range(int(x), int(y) + 1))
    return [int(a)]


def nice(v):
    if v < 100:
        return float(round(v))
    import math
    e = 10 ** (int(math.floor(math.log10(v))) - 1)
    return float(round(v / e) * e)


def main():
    db = json.load(open(DB, encoding='utf-8'))
    lay = json.load(open(LAYOUT, encoding='utf-8'))['nodes']
    old_up = {u['Upg_Key']: u for u in db['Upgrade']}
    old_cost = {c['Cost_ID']: c for c in db['Upgrade_Cost']}

    # 줄마다 행(배치 노드)을 레벨 순으로
    rows_by_line = {}
    for n in lay:
        rows_by_line.setdefault(n['line'], []).append(n)
    for name, rs in rows_by_line.items():
        if name not in LINES:
            sys.exit(f'줄 정의 없음: {name}')
        rs.sort(key=lambda n: levels_of(n['lv'], 0)[0])
    keyof = {}   # 배치 id → 행 키
    rows = []
    next_id = dict(BASE_ID)
    for name, rs in rows_by_line.items():
        key, br, tag, vals, icon, col, desc = LINES[name]
        multi = len(rs) > 1
        for i, n in enumerate(rs):
            lv = levels_of(n['lv'], len(vals) - 1)
            rk = f'{key}{i + 1}' if multi else key
            keyof[n['id']] = rk
            if key == 'power':
                uid = old_up['power']['Upg_ID']
            else:
                uid = next_id[br]; next_id[br] += 1
            rows.append(dict(n=n, key=rk, line=key, br=br, tag=tag, lv=lv, vals=vals, icon=icon, col=col, desc=desc, name=name,
                             disp=(f"{name} Lv.{lv[0]}" if multi and len(lv) == 1 else name), uid=uid, prev=rows[-1]['key'] if i > 0 else None))
    idof = {r['key']: r['uid'] for r in rows}
    for r in rows:
        if r['vals'][r['lv'][-1]] is None or len(r['vals']) <= r['lv'][-1]:
            sys.exit(f"값 부족: {r['key']}")

    up, cost, strs = [], [], []
    cid = COST_ID0
    for r in rows:
        n = r['n']; lv = r['lv']; mx = len(lv)
        efx = [r['vals'][lv[0] - 1]] + [r['vals'][l] for l in lv]
        efx += [0.0] * (MAXL + 1 - len(efx))
        gold = [0.0] * MAXL; gem = [0.0] * MAXL
        depth = max(1, n['depth'])
        if r['line'] == 'power':
            gold[0] = ROOT_GOLD
            for i in range(1, mx): gem[i] = ROOT_GEM[i - 1]
        elif r['line'] in GEM_LINES:
            for i in range(mx): gem[i] = GEM_COST[i]
        else:
            for i in range(mx): gold[i] = nice(PRICE0 * PRICE_K ** (depth - 1) * LEVEL_K ** i)
        grp = r['uid'] + 10000
        pre = None if n['parent'] is None else keyof[n['parent']]
        row = {
            'Upg_ID': r['uid'], 'Upg_Key': r['key'], 'Upg_Name': r['disp'], 'Str_Group_ID': grp,
            'Upg_Pre': idof[pre] if pre else 0, 'Upg_Req': idof[r['prev']] if r['prev'] else 0,
            'Branch': r['br'], 'Type': r['tag'], 'Line': r['line'], 'Pos_X': n['x'], 'Pos_Y': n['y'],
            'Efx_MaxLv': mx, 'Cost_ID': cid, 'Tier': 1 if depth <= 6 else 2 if depth <= 12 else 3, 'Sub': 0,
            'Skill': '', 'Icon': r['icon'], 'Color': r['col'],
        }
        for i in range(MAXL + 1): row[f'Efx_Lv{i}'] = float(efx[i])
        up.append(row)
        c = {'Cost_ID': cid}
        for i in range(MAXL): c[f'Cost_Lv{i + 1}'] = float(gold[i])
        for i in range(MAXL): c[f'Gem_Lv{i + 1}'] = float(gem[i])
        cost.append(c); cid += 1
        strs.append({'Str_ID': 180000 + grp, 'Str_Group_ID': grp, 'Str_Name': f"upg:{r['key']}:desc", 'Str_KR': r['desc'], 'Str_EN': '', 'Str_JA': '', 'Str_RU': '', 'Str_UK': ''})
        strs.append({'Str_ID': 280000 + grp, 'Str_Group_ID': grp, 'Str_Name': f"upg:{r['key']}:name", 'Str_KR': r['disp'], 'Str_EN': '', 'Str_JA': '', 'Str_RU': '', 'Str_UK': ''})

    # 세계수 노드: 예전 행 그대로, 가지 wt · 부모 사냥 준비
    for k in WORLD:
        u = dict(old_up[k]); c0 = old_cost[u['Cost_ID']]
        u.update({'Branch': 'wt', 'Line': k, 'Pos_X': 0, 'Pos_Y': 0, 'Upg_Pre': idof['power'], 'Upg_Req': 0, 'Cost_ID': cid})
        for i in range(5, MAXL + 1): u[f'Efx_Lv{i}'] = 0.0
        up.append(u)
        c = {'Cost_ID': cid}
        for i in range(MAXL): c[f'Cost_Lv{i + 1}'] = float(c0.get(f'Cost_Lv{i + 1}', 0.0))
        for i in range(MAXL): c[f'Gem_Lv{i + 1}'] = float(c0.get(f'Gem_Lv{i + 1}', 0.0))
        cost.append(c); cid += 1

    # 키 순서를 엑셀 열 순서와 맞춘다
    UPG_COLS = ['Upg_ID', 'Upg_Key', 'Upg_Name', 'Str_Group_ID', 'Upg_Pre', 'Upg_Req', 'Branch', 'Type', 'Line', 'Pos_X', 'Pos_Y',
                'Efx_MaxLv', 'Cost_ID', 'Tier', 'Sub', 'Skill', 'Icon', 'Color'] + [f'Efx_Lv{i}' for i in range(MAXL + 1)]
    COST_COLS = ['Cost_ID'] + [f'Cost_Lv{i + 1}' for i in range(MAXL)] + [f'Gem_Lv{i + 1}' for i in range(MAXL)]
    up = [{k: u[k] for k in UPG_COLS} for u in up]
    cost = [{k: c[k] for k in COST_COLS} for c in cost]
    old_names = {s['Str_Name'] for s in db['String']}
    new_str = [s for s in strs if s['Str_Name'] not in old_names]
    have_ids = {s['Str_ID'] for s in db['String']}
    assert not any(s['Str_ID'] in have_ids for s in new_str), '문구 Str_ID 겹침'

    db['Upgrade'] = up
    db['Upgrade_Cost'] = cost
    db['String'] = db['String'] + new_str
    db['_source'] = db['_source'] + ' + 균사트리 A 15차 (to_unity_db.py)'
    with open(DB, 'w', encoding='utf-8') as f:
        f.write(json.dumps(db, ensure_ascii=False, indent=2))

    os.makedirs(OUT_CSV, exist_ok=True)
    def dump(name, cols, data):
        with open(os.path.join(OUT_CSV, name), 'w', encoding='utf-8-sig', newline='') as f:
            w = csv.DictWriter(f, fieldnames=cols); w.writeheader(); w.writerows(data)
    dump('Upgrade.csv', UPG_COLS, up)
    dump('Upgrade_Cost.csv', COST_COLS, cost)
    dump('String_추가.csv', ['Str_ID', 'Str_Group_ID', 'Str_Name', 'Str_KR', 'Str_EN', 'Str_JA', 'Str_RU', 'Str_UK'], new_str)
    print(f'Upgrade {len(up)}행 · Upgrade_Cost {len(cost)}행 · String +{len(new_str)}행')


if __name__ == '__main__':
    main()
