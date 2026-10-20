# -*- coding: utf-8 -*-
"""6회차: 봇 측정(T5-1·T5-2·T5-3②)에서 보스 도전 판마다 (공격력, 판 시간, 보스 체력, 결과)를 뽑는다.
판 시간 = playR 차이 − 30초(BalanceSim OVERHEAD_SEC). 보스 체력 = 40 × 10^지역 × 1.01^(표시−1) × Hp_Mul
(지역 = Game.LatestTheme: 표시 20부터 밤, 45부터 들판 — 실제 BossHp와 같음)."""
import json, os, glob
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
T5 = os.path.join(ROOT, 'BalanceData', 'r5', 'test')
OFF = 4
ORDER = ['king', 'cleaner', 'angel', 'phantom', 'mover']
SETS = {
    'T5-1': (os.path.join(T5, 'T5-1', 'phase_k06_s*.json'), dict(king=0.35, cleaner=2.097, angel=84, phantom=115.5, mover=147)),
    'T5-2': (os.path.join(T5, 'T5-2', 'phase_k06_s*.json'), dict(king=1.75, cleaner=20, angel=740, phantom=1650, mover=1100)),
    'T5-3b': (os.path.join(T5, 'T5-3', 'phase_ph1500b50_s*.json'), dict(king=1.75, cleaner=20, angel=740, phantom=1500, mover=1100)),
}
def zone(d): return 2 if d >= 45 else 1 if d >= 20 else 0
def boss_hp(d, mul): return 40 * 10 ** zone(d) * 1.01 ** (d - 1) * mul
def rows():
    out = []
    for tag, (pat, hp) in SETS.items():
        for p in sorted(glob.glob(pat)):
            ph = json.load(open(p)); seed = int(p.split('_s')[-1].split('.')[0])
            prev = 0.0
            for i, (st, br, atk, pl) in enumerate(zip(ph['stageR'], ph['bossR'], ph['atkR'], ph['playR'])):
                T = pl - prev - 30; prev = pl
                d = st - OFF
                if br in (1, -1):
                    b = ORDER[(d // 10 - 1) % 5]
                    out.append(dict(set=tag, seed=seed, rnd=i + 1, disp=d, boss=b, atk=atk, T=round(T, 2), hp=boss_hp(d, hp[b]), hpmul=hp[b], kill=br == 1))
    return out
if __name__ == '__main__':
    R = rows()
    json.dump(R, open(os.path.join(HERE, 'attempts.json'), 'w'), ensure_ascii=False, indent=0)
    for r in R: print(r)
