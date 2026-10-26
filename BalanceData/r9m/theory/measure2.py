# 2차 보정 실측 집계: it2(새 트리, Efx 초안) vs T9-0(옛 트리) 판별 기록을 표시 스테이지 구간별로 비교한다.
# 사용: python measure2.py → measure2.json + 표 출력. 표시 스테이지 = stage-2 (TUT_ROUNDS 2)
import csv, json, os, statistics as S
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
NEW = [os.path.join(ROOT, f'BalanceData/r9m/test/it2/rounds_k06_s{i}.csv') for i in (1, 2, 3)]
OLD = [os.path.join(ROOT, f'BalanceData/r9/test/T9-0/rounds_k06_s{i}.csv') for i in (1, 2, 3)]
db = json.load(open(os.path.join(ROOT, 'Assets/Resources/DB/db.json'), encoding='utf-8'))
U = {u['Upg_Key']: u for u in db['Upgrade']}
import subprocess
odb = json.loads(subprocess.check_output(['git', 'show', 'cf1abf52:Assets/Resources/DB/db.json'], cwd=ROOT).decode('utf-8'))
OU = {u['Upg_Key']: u for u in odb['Upgrade']}
def oe(lv, k): L = lv.get(k, 0); return OU[k].get(f'Efx_Lv{min(L, OU[k]["Efx_MaxLv"])}', 0) or 0
def old_mult(lv):   # 옛 식의 노드 몫: 타격 고정값 F·정산·gm_score·희귀·콤보
    F = sum(oe(lv, k) for k in ('ed_score', 'ed_score1p', 'ed_score2', 'ed_score2p', 'ed_score3', 'ed_score3p'))
    s = oe(lv, 'ed_price') - 1 + oe(lv, 'ed_price2') + oe(lv, 'ed_trade') - 1 + 0.1
    return {'F': F, 'settle': s, 'gmS': oe(lv, 'gm_score'), 'rare': oe(lv, 'ed_rare') * oe(lv, 'ed_rare0'), 'comboK': oe(lv, 'md_combo') + oe(lv, 'md_combo2')}
LINES = {}
for u in db['Upgrade']:
    if u.get('Line'): LINES.setdefault(u['Line'], []).append(u['Upg_Key'])
for l, ks in LINES.items():
    ks.sort(key=lambda k: int(k[len(l):]) if k[len(l):].isdigit() else 0)

def line_v(lv, line):
    for k in reversed(LINES[line]):
        L = lv.get(k, 0)
        if L > 0: return U[k].get(f'Efx_Lv{min(L, U[k]["Efx_MaxLv"])}', 0) or 0
    return U[LINES[line][0]].get('Efx_Lv0', 0) or 0

def zone(ds):   # 숲 1~, 지역 바뀔 때 ×10 (THEMES from: 표시 1·11·21·31·41·... 가정이 아니라 기록의 zone 열로 센다)
    return None

def load(path, new):
    rows, lv = [], {}
    zones = []
    for r in csv.DictReader(open(path, encoding='utf-8')):
        for b in (r['bought'] or '').split(';'):
            if ':' in b:
                k, ab = b.split(':'); lv[k] = int(ab.split('>')[1])
        if r['zone'] not in zones: zones.append(r['zone'])
        ds = int(r['stage']) - 2
        d = {'ds': ds, 'boss': r['bossFlag'] not in ('0', '2'), 'net': float(r['net']), 'atk': float(r['atk']), 'balls': int(r['balls']),
             'gold': float(r['gold']), 'min': float(r['playMin']), 'zi': zones.index(r['zone']), 'dur': float(r['dur']),
             'nlv': sum(v for k, v in lv.items() if not k.startswith('inf_'))}
        if new:
            d['hunt'] = line_v(lv, 'ed_hunt'); d['settle'] = 0.1 + line_v(lv, 'ed_haggle') + line_v(lv, 'ed_regular')
            d['G'] = (1 + d['hunt']) * (1 + d['settle'])
        if not new: d.update(old_mult(lv))
        else: d['comboK'] = 0.1 * (1 + line_v(lv, 'ps_combo'))
        rows.append(d)
    return rows

def band(ds): return (max(ds, 1) - 1) // 10   # 0: 1~10, 1: 11~20 ...

def summarize(runs, new):
    out = {}
    for b in range(10):
        nets, atks, balls, Gs, H, mins, nlv, ex = [], [], [], [], [], [], [], {}
        for rows in runs:
            sel = [r for r in rows if band(r['ds']) == b and not r['boss'] and r['ds'] >= 1]
            if not sel: continue
            first = [r for r in rows if band(r['ds']) == b]
            mins.append(first[0]['min'])
            for r in sel:
                for kk in ('F', 'settle', 'gmS', 'rare', 'comboK', 'dur'):
                    if kk in r: ex.setdefault(kk, []).append(r[kk])
                nets.append(r['net']); atks.append(r['atk']); balls.append(r['balls']); nlv.append(r['nlv'])
                z = 10 ** r['zi'] * 1.01 ** (r['ds'] - 1)
                if new: Gs.append(r['G']); H.append(r['net'] / (r['G'] * z) / r['balls'] / r['dur'])
                else: H.append(r['net'] / z / r['balls'] / r['dur'])
        if not nets: continue
        out[f'{b*10+1}-{b*10+10}'] = {'n': len(nets), 'net': S.median(nets), 'atk': S.median(atks), 'balls': S.median(balls),
            'firstMin': S.median(mins), 'nodeLv': S.median(nlv), 'G': S.median(Gs) if Gs else None, 'H_per_ball_sec': S.median(H), **{kk: S.median(v) for kk, v in ex.items()}}
    return out

if __name__ == '__main__':
    newr = [load(p, True) for p in NEW]; oldr = [load(p, False) for p in OLD]
    res = {'new_it2': summarize(newr, True), 'old_T9_0': summarize(oldr, False)}
    # 표시 40 구간 상세 (막힌 곳): 마지막 100판
    tail = []
    for rows in newr:
        t = [r for r in rows[-100:] if not r['boss']]
        tail.append({'net': S.median(r['net'] for r in t), 'G': t[-1]['G'], 'hunt': t[-1]['hunt'], 'settle': t[-1]['settle'], 'atk': t[-1]['atk'], 'balls': t[-1]['balls'], 'dur': t[-1]['dur'], 'zi': t[-1]['zi']})
    res['new_tail'] = tail
    json.dump(res, open(os.path.join(os.path.dirname(__file__), 'measure2.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    for k in ('new_it2', 'old_T9_0'):
        print(k)
        for b, v in res[k].items(): print(' ', b, {kk: (round(vv, 3) if isinstance(vv, float) and vv < 1e4 else (f'{vv:.3g}' if isinstance(vv, float) else vv)) for kk, vv in v.items()})
    print('tail', tail)
