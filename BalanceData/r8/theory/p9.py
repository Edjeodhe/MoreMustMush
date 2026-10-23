# -*- coding: utf-8 -*-
"""8-R P-9 중반 정체 풀기: 골드 노드 중 DB 가격 2.22e9~1.67e11(화면 2e9~1.5e11) 칸이 하나라도 있는 노드는
그 노드의 2.22e9 이상 모든 레벨 가격 ×0.35 (노드 안 ×4 이하 유지). Cost_ID별 지금→새 표와 가격 조건 검사.
다시 돌리기: python -X utf8 BalanceData/r8/theory/p9.py → p9_out.txt · p9_overrides.json"""
import json, math, os
from collections import Counter
from tree import load
HERE = os.path.dirname(os.path.abspath(__file__))
LO, HI, K = 2e9 / 0.9, 1.5e11 / 0.9, 0.35
nodes, _, _ = load(drop=('homing',))
cid_count = Counter(n['cid'] for n in nodes.values())
out, ov = ['Upg_ID 키 (Cost_ID) | 지금 Cost_Lv | 새 Cost_Lv | 노드 안 최대 배율 새'], {}
for k, n in sorted(nodes.items(), key=lambda kv: kv[1]['costs'][0]):
    if n['gem'] or n['inf']:
        continue
    c = [x for x in n['costs'][:n['max']]]
    if not any(LO <= x <= HI for x in c):
        continue
    new = [round(x * K, -int(math.floor(math.log10(x * K))) + 2) if x >= LO else x for x in c]
    r = max((b / a for a, b in zip(new, new[1:])), default=1)
    ov[k] = new
    out.append(f"{n['uid']} {k} ({n['cid']}{' 공유!' if cid_count[n['cid']] > 1 else ''}) | {' · '.join(f'{x:.3g}' for x in c)} | {' · '.join(f'{x:.3g}' for x in new)} | ×{r:.2f}")
# 사다리 (2단계 이상 골드 가격 전체, 1e8~4e12)
lad = sorted((x, k) for k, n in nodes.items() if not n['gem'] and not n['inf'] for x in (ov.get(k) or n['costs'][:n['max']]) if 1e8 <= x <= 4e12)
jumps = sorted(((b[0] / a[0], a, b) for a, b in zip(lad, lad[1:])), reverse=True)[:5]
out.append('\n새 가격 사다리(1e8~4e12) 큰 뜀: ' + ' · '.join(f'×{r:.2f} {a[1]} {a[0]:.3g}→{b[1]} {b[0]:.3g}' for r, a, b in jumps))
lad0 = sorted((x, k) for k, n in nodes.items() if not n['gem'] and not n['inf'] for x in n['costs'][:n['max']] if 1e8 <= x <= 4e12)
j0 = sorted(((b[0] / a[0], a, b) for a, b in zip(lad0, lad0[1:])), reverse=True)[:3]
out.append('지금 사다리 큰 뜀: ' + ' · '.join(f'×{r:.2f} {a[1]} {a[0]:.3g}→{b[1]} {b[0]:.3g}' for r, a, b in j0))
txt = '\n'.join(out); print(txt)
open(os.path.join(HERE, 'p9_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
json.dump(ov, open(os.path.join(HERE, 'p9_overrides.json'), 'w'), indent=1)
