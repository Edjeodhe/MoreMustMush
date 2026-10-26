# -*- coding: utf-8 -*-
"""8회차 유도(homing) 제거: 보스 판에서 유도가 보스 타격 빈도를 몇 배 올리는지 기하 몬테카를로.
코드 근거: RoundSim.Bar.cs BarHit(각도 = tt*60도) · HomeAim(가장 가까운 보스로 직행, 위로) ·
RoundSim.Boss.cs SpawnBoss(x = WW/2, y = FY0 + (NO_SPAWN_Y-FY0)*0.3, BOSS_R 110) · RoundSim.cs SetWorld ·
BalanceSim.PlayRound 봇 바(noise = U(-1,1)*SKILL*barLen, 0.5초마다).
한 '주기' = 바에서 떠나 다시 바 높이로 올 때까지. 보스 타격 수/초를 유도 켬/끔으로 비교한다.
다시 돌리기: python -X utf8 BalanceData/r8/theory/homing_mc.py  (시드 고정, 결과 homing_mc_out.txt)
"""
import math, random

def world(z):
    WW, WH = round(1920 / z), round(1080 / z)
    return dict(FX0=40, FX1=WW - 40, FY0=40, BAR_Y=WH - 80, NO=WH - 180, WW=WW, WH=WH)

def flight(x, y, dx, dy, w, bx, by, R, rb, maxlen=20000):
    """직선 비행 + 벽·천장·보스 원 반사. 바 높이로 돌아오면 끝. (보스 타격 수, 경로 길이, 끝 x)"""
    hits, L, step = 0, 0.0, 4.0
    cool = 0
    while L < maxlen:
        x += dx * step; y += dy * step; L += step
        if x < w['FX0'] + rb: x = w['FX0'] + rb; dx = abs(dx)
        if x > w['FX1'] - rb: x = w['FX1'] - rb; dx = -abs(dx)
        if y < w['FY0'] + rb: y = w['FY0'] + rb; dy = abs(dy)
        ddx, ddy = x - bx, y - by
        d = math.hypot(ddx, ddy)
        if cool > 0: cool -= 1
        if d < R + rb and cool == 0:
            nx, ny = ddx / d, ddy / d
            dot = dx * nx + dy * ny
            if dot < 0:
                dx, dy = dx - 2 * dot * nx, dy - 2 * dot * ny
                hits += 1; cool = 3
        if abs(dy) < 0.12:
            dy = 0.12 if dy >= 0 else -0.12
            n = math.hypot(dx, dy); dx, dy = dx / n, dy / n
        if y >= w['BAR_Y'] and dy > 0:
            return hits, L, x
    return hits, L, x

def run(z, skill, homing, n=4000, rb=30.0, seed=1):
    rnd = random.Random(seed)
    w = world(z)
    bx, by, R = w['WW'] / 2, w['FY0'] + (w['NO'] - w['FY0']) * 0.3, 110.0
    x = w['WW'] / 2
    H = Lsum = 0.0
    for _ in range(n):
        y = w['BAR_Y']
        if homing:
            a = math.atan2(by - y, bx - x); dx, dy = math.cos(a), math.sin(a)
            if dy > 0: dy = -dy
        else:
            tt = max(-1.0, min(1.0, -rnd.uniform(-1, 1) * skill * 2))  # noise/(len/2), noise = U(-1,1)*skill*len
            a = tt * math.radians(60); dx, dy = math.sin(a), -math.cos(a)
        h, L, x = flight(x, y, dx, dy, w, bx, by, R, rb)
        H += h; Lsum += L
    return H / n, Lsum / n

if __name__ == '__main__':
    out = []
    out.append('z(필드 확대) skill | 끔: 타격/주기 경로px 타격/1000px | 켬: 타격/주기 경로px 타격/1000px | 켬/끔 (시간당)')
    res = {}
    for z in (1.68, 1.35, 1.0):
        for skill in (0.6, 1.0):
            h0, l0 = run(z, skill, False)
            h1, l1 = run(z, skill, True)
            r = (h1 / l1) / (h0 / l0)
            res[(z, skill)] = r
            out.append(f'{z} {skill} | {h0:.3f} {l0:.0f} {1000*h0/l0:.3f} | {h1:.3f} {l1:.0f} {1000*h1/l1:.3f} | x{r:.2f}')
    # 유도가 켜진 비율 f: 바 타격마다 확률 p로 켜지고 D초 지속, 바 타격 간격 T초 → f = 1-(1-p)^(floor(D/T)+1)
    out.append('')
    out.append('유도 켜짐 비율 f와 보스 DPS 배율 M = 1 + f*(r-1)  (r = 위 켬/끔, z 1.0·skill 0.6 값 사용)')
    r = res[(1.0, 0.6)]
    cases = [
        ('유도 1 · 북돋기 1 (p .10+.05, D 3초, T 2초)', 0.15, 3.0, 2.0),
        ('유도 2 · 북돋기 4 (p .20+.20, D 3초, T 2초)', 0.40, 3.0, 2.0),
        ('유도 4 · 북돋기 4 · 바 발동률 3 · 바 지속 3 (p .30+.20+.12+.075, D (5+1.5)x1.0)', 0.70, 6.5, 2.0),
        ('위 + 버프 지속 2 (x1.8 → D 11.7초)', 0.70, 11.7, 2.0),
    ]
    for name, p, D, T in cases:
        k = int(D // T)
        f = 1 - (1 - p) ** (k + 1)
        out.append(f'{name}: f {f:.2f} → M x{1 + f * (r - 1):.2f}')
    txt = '\n'.join(out)
    print(txt)
    open(__file__.replace('.py', '_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
