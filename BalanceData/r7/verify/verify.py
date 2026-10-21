# -*- coding: utf-8 -*-
"""7회차 verify 독립 재계산 (theory 코드 import 안 함, 난수 = Python random/numpy 다른 시드).

python verify.py [part...]   part: place prop dyn loose cost  (없으면 전부) → verify_out.txt, verify_out.json

place : 지금 코드 배치(RoundSim.Start → PlaceDevice → SpawnColony(true)×MaxCol) 재현, 측정표 RMSE
prop  : 제안 규칙(s0·30번+8px 격자·×0.93 다시) 168조합 P(전부)·s, + 섞인 등급에서 재생(큰 등급이 작은 구멍에) 실패율
dyn   : 판 동안 대기열 동역학(Effects.cs:126~132 그대로, 프레임 1/60) — 지금/최대치만/제안 묶음 판당 만든 버섯 비
loose : 초반 낱개 버섯(EARLY_LOOSE 10·10·9·8·6·4)이 새 규칙에서 몇 개 놓이나
cost  : 격자 보충 1회 AreaFree 호출 수
"""
import math, json, sys, random
import numpy as np

# ---------------- 코드 상수 ----------------
TR = [32.0, 28.0, 22.0, 20.0]          # Defs.cs:99 Mathf.Round(BASE_R×1.25)
DEV = 1.4                              # Defs.cs:15


def mround(x):                          # Mathf.Round = 짝수 쪽
    return float(np.round(x))


def jsround(x):
    return math.floor(x + 0.5)


def colRR(r):                           # RoundSim.cs:201,208
    rc = mround(1.9 * r); ring = rc + r + 6
    return ring + r + 6


def field(z):                           # RoundSim.cs:21~26
    WW = mround(1920 / z); WH = mround(1080 / z)
    return dict(WW=WW, WH=WH, X0=40.0, X1=WW - 40, Y0=40.0, NSY=WH - 180, LX=WW / 2, LY=WH - 150)


def maxcol(cc, ii, z, p, rain=False, xn=0.0, xp=0.0):   # Game.cs:246,257~258
    b = (cc + ii + xn) * (1 + xp) * (1.2 if rain else 1)
    return max(1, jsround(math.ceil(b - 1e-9) * (1 / (z * z)) ** p))


OLD = dict(cc=[4, 6, 10, 17, 27], ii=[0, 4, 8], p=0.7, regen=[4.0, 3.2, 2.4, 1.6])
NEW = dict(cc=[4, 6, 8, 10, 11], ii=[0, 2, 3], p=0.8, regen=[2.0, 1.4, 0.9, 0.4])


class Field:
    """AreaFree(RoundSim.cs:135~153) — 원 군락만, 장치, 발사점."""

    def __init__(self, z, rng, devs=()):
        self.w = field(z); self.rng = rng
        self.cx = []; self.cy = []; self.cR = []
        self.dc = []; self.dr = []
        for d in devs:
            self.place_dev(d)

    def _free_dev(self, circ, rects):
        w = self.w
        for (x, y, r) in circ:
            if (x - w['LX']) ** 2 + (y - w['LY']) ** 2 < (r + 140) ** 2: return False
            if w['NSY'] - y < r: return False
            for (a, b, rr) in self.dc:
                if (x - a) ** 2 + (y - b) ** 2 < (r + rr + 10) ** 2: return False
            for (x0, y0, x1, y1) in self.dr:
                px = min(max(x, x0), x1); py = min(max(y, y0), y1)
                if (x - px) ** 2 + (y - py) ** 2 < (r + 10) ** 2: return False
        for (x0, y0, x1, y1) in rects:
            px = min(max(w['LX'], x0), x1); py = min(max(w['LY'], y0), y1)
            if (w['LX'] - px) ** 2 + (w['LY'] - py) ** 2 < 140 ** 2: return False
            if y1 > w['NSY']: return False
            for (a, b, rr) in self.dc:
                px = min(max(a, x0), x1); py = min(max(b, y0), y1)
                if (a - px) ** 2 + (b - py) ** 2 < (rr + 10) ** 2: return False
            for (u0, v0, u1, v1) in self.dr:
                if x0 < u1 + 10 and x1 + 10 > u0 and y0 < v1 + 10 and y1 + 10 > v0: return False
        return True

    def place_dev(self, t):              # RoundSim.cs:409~446
        w = self.w; R = self.rng
        for _ in range(40):
            if t == 'stump':
                m = 50 * DEV; x = R.uniform(w['X0'] + m, w['X1'] - m); y = R.uniform(w['Y0'] + m, w['NSY'] - m)
                circ = [(x, y, 40 * DEV)]; rects = []
            elif t == 'moss':
                m = 140 * DEV; x = R.uniform(w['X0'] + m, w['X1'] - m); y = R.uniform(w['Y0'] + m, w['NSY'] - m)
                hz = R.random() < 0.5; ww = (260 if hz else 50) * DEV; hh = (50 if hz else 260) * DEV
                circ = []; rects = [(x - ww / 2, y - hh / 2, x + ww / 2, y + hh / 2)]
            else:
                m = 110 * DEV; x = R.uniform(w['X0'] + m, w['X1'] - m); y = R.uniform(w['Y0'] + m, w['NSY'] - m)
                hz = R.random() < 0.6; g = 70 * DEV
                circ = [(x + (k * g if hz else 0), y + (0 if hz else k * g), 18.0) for k in (-1, 0, 1)]; rects = []
            if self._free_dev(circ, rects):
                self.dc += circ; self.dr += rects; return

    def ok(self, X, Y, RR, extra=None):
        """X,Y 배열에서 반지름 RR 원이 놓일 수 있는지. extra = (cx,cy,cR) 계획 중 임시 원."""
        w = self.w
        m = (X - w['LX']) ** 2 + (Y - w['LY']) ** 2 >= (RR + 140) ** 2
        cx = self.cx; cy = self.cy; cR = self.cR
        if extra is not None:
            cx = cx + extra[0]; cy = cy + extra[1]; cR = cR + extra[2]
        if cx:
            A = np.array(cx); B = np.array(cy); C = np.array(cR)
            m &= ~np.any((X[:, None] - A) ** 2 + (Y[:, None] - B) ** 2 < (C + RR + 10) ** 2, axis=1)
        for (a, b, rr) in self.dc:
            m &= (X - a) ** 2 + (Y - b) ** 2 >= (rr + RR + 10) ** 2
        for (x0, y0, x1, y1) in self.dr:
            px = np.clip(X, x0, x1); py = np.clip(Y, y0, y1)
            m &= (X - px) ** 2 + (Y - py) ** 2 >= (RR + 10) ** 2
        return m

    def bounds(self, RR):
        w = self.w
        if w['X0'] + RR > w['X1'] - RR or w['Y0'] + RR > w['NSY'] - RR: return None
        return (w['X0'] + RR, w['X1'] - RR, w['Y0'] + RR, w['NSY'] - RR)

    def try_random(self, RR, tries=30, extra=None):
        b = self.bounds(RR)
        if b is None: return None
        X = self.rng_np.uniform(b[0], b[1], tries); Y = self.rng_np.uniform(b[2], b[3], tries)
        idx = np.flatnonzero(self.ok(X, Y, RR, extra))
        return (X[idx[0]], Y[idx[0]]) if len(idx) else None

    def try_grid(self, RR, step=8.0, extra=None):
        b = self.bounds(RR)
        if b is None: return None, 0
        gx = np.arange(b[0], b[1] + 1e-6, step); gy = np.arange(b[2], b[3] + 1e-6, step)
        X, Y = np.meshgrid(gx, gy); X = X.ravel(); Y = Y.ravel()
        idx = np.flatnonzero(self.ok(X, Y, RR, extra))
        if not len(idx): return None, len(X)
        j = idx[int(self.rng_np.integers(len(idx)))]
        return (X[j], Y[j]), len(X)


def new_field(z, seed, devs=()):
    R = random.Random(seed); f = Field(z, R, devs); f.rng_np = np.random.default_rng(seed + 99991); return f


# ---------------- place: 지금 코드 재현 ----------------
MEAS = {1.68: [2.0, 2.3, 2.8, 2.3, 2.5], 1.35: [3.0, 4.5, 4.5, 4.8, 4.5], 1.1: [4.0, 7.8, 7.5, 8.0, 8.0], 1.0: [4.0, 8.5, 10.5, 10.3, 10.3]}
CELLS = [(0, 0), (2, 0), (3, 0), (4, 0), (4, 2)]


def place_now(z, n, trials, seed, mix=(1, 0, 0, 0), devs=()):
    p = np.array(mix, float); p /= p.sum(); out = []
    for k in range(trials):
        f = new_field(z, seed * 1000 + k, devs)
        ts = f.rng_np.choice(4, size=n, p=p)
        c = 0
        for t in ts:
            RR = colRR(TR[t]); q = f.try_random(RR)
            if q: f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(RR); c += 1
        out.append(c)
    return out


def part_place(O, T):
    err = []; rows = []
    for z, ms in MEAS.items():
        for (L, II), m in zip(CELLS, ms):
            n = maxcol(OLD['cc'][L], OLD['ii'][II], z, 0.7)
            v = float(np.mean(place_now(z, n, 300, 11)))
            err.append(v - m); rows.append((z, n, m, round(v, 2)))
    e = np.array(err)
    O['place'] = dict(rows=rows, rmse=float(np.sqrt(np.mean(e ** 2))), bias=float(e.mean()), max=float(np.abs(e).max()))
    T.append(f"[place] 지금 코드 재현(독립 구현, 300판): RMSE {O['place']['rmse']:.2f} 치우침 {O['place']['bias']:+.2f} 최대 {O['place']['max']:.2f}")
    for r in rows: T.append(f"  z{r[0]} 최대 {r[1]} 측정 {r[2]} 재계산 {r[3]}")
    # 실제 후반 등급 섞임(T6-3 madeT 31~55)으로 md_map2 35개
    for name, mix in (('T6-3 21~30 섞임', (0.105, 0.207, 0.338, 0.35)), ('T6-3 31~55 섞임', (0.069, 0.235, 0.378, 0.318)), ('theory 가정 50/25/15/10', (50, 25, 15, 10))):
        for n in (31, 35):
            v = float(np.mean(place_now(1.0, n, 150, 21, mix, ('stump', 'moss', 'acorn'))))
            T.append(f"  md_map2 장치3 {name} 최대 {n} → 판 시작 {v:.2f}")
            O.setdefault('place_mix', []).append((name, n, v))


# ---------------- prop: 제안 규칙 ----------------
S_VIS, S_ABS, PACK, SHR = 0.70, 0.45, 0.40, 0.93
DEV_AREA = {'stump': math.pi * 56 ** 2, 'moss': 364 * 70, 'acorn': 3 * math.pi * 18 ** 2}


def smin(z): return max(S_VIS / z, S_ABS)


def s0(z, n, devs):
    w = field(z)
    A = (w['X1'] - w['X0']) * (w['NSY'] - w['Y0']) - math.pi * 140 ** 2 / 2 - sum(DEV_AREA[d] for d in devs)
    rho = math.sqrt(PACK * A / (math.pi * max(n, 1)))
    return min(1.0, max(S_ABS, (rho - 17) / (3.9 * 32)))


def plan(f, ts, s):
    ex = ([], [], [])
    for t in ts:
        RR = colRR(TR[t] * s)
        q = f.try_random(RR, 30, ex)
        if q is None: q, _ = f.try_grid(RR, 8.0, ex)
        if q is not None: ex[0].append(q[0]); ex[1].append(q[1]); ex[2].append(RR)
    return ex


def place_prop(f, z, ts, devs):
    n = len(ts); s = s0(z, n, devs); it = 0
    while True:
        ex = plan(f, ts, s); it += 1
        if len(ex[0]) >= n or s <= S_ABS + 1e-9: break
        nx = s * SHR; sm = smin(z)
        s = sm if (s > sm + 1e-9 and nx < sm) else max(S_ABS, nx)
    f.cx += ex[0]; f.cy += ex[1]; f.cR += ex[2]
    return len(ex[0]), s, it


def part_prop(O, T, trials=40):
    ZS = [1.68, 1.5, 1.35, 1.2, 1.1, 1.0]; LV = [(L, 0) for L in range(5)] + [(4, 1), (4, 2)]
    res = []; worstP = 1; worstS = 9; below = 0; tot = 0; maxit = 0
    for z in ZS:
        for L, II in LV:
            for rain in (False, True):
                for devs in ((), ('stump', 'moss', 'acorn')):
                    n = maxcol(NEW['cc'][L], NEW['ii'][II], z, 0.8, rain)
                    ks = []; ss = []
                    for k in range(trials):
                        f = new_field(z, 500000 + hash((z, L, II, rain, len(devs))) % 10000 * 100 + k, devs)
                        c, s, it = place_prop(f, z, [0] * n, devs)
                        ks.append(c); ss.append(s); maxit = max(maxit, it)
                        below += s < smin(z) - 1e-9; tot += 1
                    pa = float(np.mean([c >= n for c in ks])); worstP = min(worstP, pa); worstS = min(worstS, min(ss))
                    res.append((z, L, II, rain, len(devs), n, pa, float(np.mean(ss)), float(min(ss))))
    O['prop'] = dict(rows=res, worstP=worstP, worstS=worstS, below=below, tot=tot, maxit=maxit)
    T.append(f"[prop] 168조합×{trials}판(일반 등급만): P(전부) 최소 {worstP:.3f}, s 최저 {worstS:.3f}, s<s_min 판 {below}/{tot}, 다시 놓기 최대 {maxit}회")
    for r in res:
        if r[0] in (1.68, 1.0) and r[4] == 3 and r[3]: T.append(f"  z{r[0]} cols{r[1]}+II{r[2]} 비 장치3 N{r[5]} P{r[6]:.2f} s평균 {r[7]:.2f} 최저 {r[8]:.2f}")


def part_refill(O, T, trials=60):
    """제안 규칙: 섞인 등급으로 다 놓은 뒤, 작은 등급 군락 하나를 비우고 일반(가장 큰) 등급이 같은 s로 들어가나(30번+격자)."""
    out = []
    for z, n, mix in ((1.0, 14, (0.069, 0.235, 0.378, 0.318)), (1.0, 14, (1, 0, 0, 0)), (1.68, 6, (0.58, 0.25, 0.14, 0.03)), (1.2, 8, (0.58, 0.25, 0.14, 0.03))):
        p = np.array(mix, float); p /= p.sum(); fail_rand = 0; fail_all = 0; m = 0
        for k in range(trials):
            devs = ('stump', 'moss', 'acorn')
            f = new_field(z, 900000 + k, devs)
            ts = list(f.rng_np.choice(4, size=n, p=p))
            c, s, _ = place_prop(f, z, ts, devs)
            if c < n: continue
            j = int(np.argmax([TR[t] for t in ts])) if False else int(np.argmin([TR[t] for t in ts]))   # 가장 작은 등급을 비운다
            for a in (f.cx, f.cy, f.cR): a.pop(j)
            RR = colRR(TR[0] * s); m += 1
            q = f.try_random(RR)
            if q is None:
                fail_rand += 1
                q, _ = f.try_grid(RR)
                if q is None: fail_all += 1
        out.append((z, n, mix, m, fail_rand / max(m, 1), fail_all / max(m, 1)))
        T.append(f"[refill] z{z} N{n} 섞임{mix}: 작은 군락 빈자리에 일반 등급 재생 — 30번 실패 {fail_rand / max(m, 1):.2f}, 격자까지 실패 {fail_all / max(m, 1):.2f} ({m}판)")
    O['refill'] = out


# ---------------- dyn: 판 동안 대기열 동역학 ----------------
def dyn_round(z, n, T, regen, tc, m, mix, devs, rule, seed, servers=0, dt=1 / 60):
    """rule: 'now' 지금 코드(s=1, 30번) / 'prop' 제안(s·격자). 판당 만든 버섯 수와 평균 놓인 군락 수.
    tc: 군락이 맞을 수 있게 된 뒤 다 비우는 시간(지수분포 평균). servers>0이면 동시에 비울 수 있는 군락 수 제한(핀볼 수)."""
    f = new_field(z, seed, devs); rng = f.rng_np
    p = np.array(mix, float); p /= p.sum()
    s = 1.0
    alive = []           # [hittable_at, clear_at or None]
    made = 0; Hs = 0
    if rule == 'prop':
        ts = list(rng.choice(4, size=n, p=p)); c, s, _ = place_prop(f, z, ts, devs)
        for _ in range(c): alive.append([0.0, None])
    else:
        for _ in range(n):
            t = rng.choice(4, p=p); RR = colRR(TR[t]); q = f.try_random(RR)
            if q: f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(RR); alive.append([0.0, None])
    made += len(alive) * m
    queue = []; t = 0.0; csum = 0.0; steps = 0
    while t < T:
        t += dt; steps += 1
        # 비우기 (k-서버면 대기 중인 것부터)
        busy = sum(1 for a in alive if a[1] is not None)
        for a in alive:
            if a[1] is None and a[0] <= t and (servers == 0 or busy < servers):
                a[1] = t + rng.exponential(tc); busy += 1
        i = 0
        while i < len(alive):
            if alive[i][1] is not None and alive[i][1] <= t:
                alive.pop(i); f.cx.pop(i); f.cy.pop(i); f.cR.pop(i); queue.append(regen); Hs += m
            else: i += 1
        # Effects.cs:126~132
        for k in range(len(queue)): queue[k] -= dt
        for k in range(len(queue) - 1, -1, -1):
            if queue[k] <= 0:
                ok = False
                if len(alive) >= n: ok = True
                else:
                    tt = rng.choice(4, p=p); RR = colRR(TR[tt] * s); q = f.try_random(RR)
                    if q is None and rule == 'prop': q, _ = f.try_grid(RR)
                    if q is not None:
                        f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(RR); alive.append([t + 0.175, None]); made += m; ok = True
                if ok: queue.pop(k)
                else: queue[k] = 0.5
        miss = n - len(alive) - len(queue)
        for _ in range(miss): queue.append(regen)
        csum += len(alive)
    return made, csum / steps, s


STATES = {
    # 이름: z, cols, II, regenL, devs, m, (theory T, made, mix), (T6-3 T, made, mix)
    '1~5': (1.68, 1, 0, 0, (), 5, (12, 28, (1, 0, 0, 0)), (14.3, 30, (0.97, 0.01, 0.02, 0.0))),
    '6~13': (1.5, 3, 0, 1, ('stump',), 5, (17, 39, (1, 0, 0, 0)), (21.3, 101, (0.76, 0.17, 0.07, 0.0))),
    '14~20': (1.2, 4, 0, 2, ('stump', 'moss', 'acorn'), 5, (28, 320, (50, 5, 3, 2)), (29.3, 567, (0.58, 0.25, 0.14, 0.03))),
    '21~30': (1.0, 4, 1, 3, ('stump', 'moss', 'acorn'), 6, (31, 1600, (50, 25, 15, 10)), (35.3, 2101, (0.105, 0.207, 0.338, 0.35))),
    '31~55': (1.0, 4, 2, 3, ('stump', 'moss', 'acorn'), 8, (35, 2550, (50, 25, 15, 10)), (39.2, 4091, (0.069, 0.235, 0.378, 0.318))),
}


def mean_made(z, n, T, regen, tc, m, mix, devs, rule, reps, seed0, servers=0):
    r = [dyn_round(z, n, T, regen, tc, m, mix, devs, rule, seed0 + i, servers) for i in range(reps)]
    return float(np.mean([x[0] for x in r])), float(np.mean([x[1] for x in r])), float(np.mean([x[2] for x in r]))


def fit_tc(z, n, T, regen, m, mix, devs, target, reps, servers=0):
    lo, hi = 0.05, 40.0
    for _ in range(14):
        mid = math.sqrt(lo * hi)
        v, _, _ = mean_made(z, n, T, regen, mid, m, mix, devs, 'now', reps, 7000, servers)
        if v > target: lo = mid
        else: hi = mid
    return math.sqrt(lo * hi)


def part_dyn(O, T, reps=8, src='both', servers_list=(0,)):
    out = []
    for name, (z, L, II, RL, devs, m, th, ob) in STATES.items():
        for tag, (TT, made, mix) in (('theory입력', th), ('T6-3입력', ob)):
            if src != 'both' and tag != src: continue
            for sv in servers_list:
                n_old = maxcol(OLD['cc'][L], OLD['ii'][II], z, 0.7); n_new = maxcol(NEW['cc'][L], NEW['ii'][II], z, 0.8)
                tc = fit_tc(z, n_old, TT, OLD['regen'][RL], m, mix, devs, made, reps, sv)
                base, c_old, _ = mean_made(z, n_old, TT, OLD['regen'][RL], tc, m, mix, devs, 'now', reps, 8000, sv)
                cap, c_cap, _ = mean_made(z, n_new, TT, OLD['regen'][RL], tc, m, mix, devs, 'now', reps, 8000, sv)       # 최대치만(배치 규칙 그대로)
                p0, c_p0, s_p0 = mean_made(z, n_new, TT, OLD['regen'][RL], tc, m, mix, devs, 'prop', reps, 8000, sv)   # 규칙+최대치, 재생 그대로
                p1, c_p1, s_p1 = mean_made(z, n_new, TT, NEW['regen'][RL], tc, m, mix, devs, 'prop', reps, 8000, sv)   # 묶음(재생 제안)
                row = dict(stage=name, src=tag, servers=sv, tc=round(tc, 2), n_old=n_old, c_old=round(c_old, 1), made_old=round(base),
                           cap=round(cap / base, 2), c_cap=round(c_cap, 1), prop_R0=round(p0 / base, 2), prop_R1=round(p1 / base, 2), c_new=round(c_p1, 1), s=round(s_p1, 2), n_new=n_new)
                out.append(row)
                T.append(f"[dyn] {name} {tag} 서버{sv or '∞'}: t_c {tc:.2f}s 지금 최대{n_old} 평균 놓임 {c_old:.1f} made {base:.0f} | 최대치만 ×{cap / base:.2f}(놓임 {c_cap:.1f}) | 규칙+최대치 재생그대로 ×{p0 / base:.2f} | 묶음(재생 제안) ×{p1 / base:.2f} (놓임 {c_p1:.1f}/{n_new}, s {s_p1:.2f})")
    O['dyn'] = out


def part_queue(O, T):
    """대기열 채움 지연 직접 측정: 31~55(T6-3 섞임) 지금 코드에서, 군락이 빈 뒤 다음 군락이 나기까지 시간."""
    z, L, II, RL, devs, m = 1.0, 4, 2, 3, ('stump', 'moss', 'acorn'), 8
    n = maxcol(OLD['cc'][L], OLD['ii'][II], z, 0.7); mix = (0.069, 0.235, 0.378, 0.318)
    p = np.array(mix); p /= p.sum(); dts = []
    for seed in range(6):
        f = new_field(z, 3000 + seed, devs); rng = f.rng_np
        for _ in range(n):
            t = rng.choice(4, p=p); RR = colRR(TR[t]); q = f.try_random(RR)
            if q: f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(RR)
        for trial in range(40):
            j = int(rng.integers(len(f.cx))); sv = (f.cx.pop(j), f.cy.pop(j), f.cR.pop(j))
            # 대기열 항목 n-c개, 위상은 0~0.5 고르게 퍼졌다고 가정 → 시도 시각 = 위상들
            k = n - len(f.cx) - 1
            ph = sorted(rng.uniform(0, 0.5, max(k, 1)))
            tt = 0.0; got = None
            for cyc in range(40):
                for ph_ in ph:
                    tq = rng.choice(4, p=p); RR = colRR(TR[tq]); q = f.try_random(RR)
                    if q is not None: got = cyc * 0.5 + ph_; f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(RR); break
                if got is not None: break
            dts.append(got if got is not None else 20.0)
            if got is None: f.cx.append(sv[0]); f.cy.append(sv[1]); f.cR.append(sv[2])
    d = np.array(dts)
    O['queue'] = dict(mean=float(d.mean()), median=float(np.median(d)), p90=float(np.percentile(d, 90)))
    T.append(f"[queue] 지금 코드 md_map2 II2(최대 {n}) T6-3 섞임: 빈자리 채움 시간 평균 {d.mean():.2f}s 중앙 {np.median(d):.2f}s 90% {np.percentile(d, 90):.2f}s (theory 가정 0.25)")


# ---------------- loose: 초반 낱개 버섯 ----------------
def part_loose(O, T, trials=200):
    out = []
    for stage, cnt in ((2, 10), (3, 9), (4, 8), (5, 6), (6, 4)):
        for rule in ('now', 'prop'):
            for L in (0, 1):
                got = []
                for k in range(trials):
                    f = new_field(1.68, 40000 + k * 7 + stage, ())
                    if rule == 'now':
                        n = maxcol(OLD['cc'][L], 0, 1.68, 0.7)
                        for _ in range(n):
                            q = f.try_random(colRR(32))
                            if q: f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(colRR(32))
                        s = 1.0
                    else:
                        n = maxcol(NEW['cc'][L], 0, 1.68, 0.8); _, s, _ = place_prop(f, 1.68, [0] * n, ())
                    # 낱개: 원 r=32(크기 배율 없음), AreaFree + 다른 버섯과 여백 10 (군락 둘레 버섯은 RR 안이라 AreaFree로 충분히 근사)
                    lx = []; ly = []; c = 0; w = f.w
                    for _ in range(cnt):
                        X = f.rng_np.uniform(w['X0'] + 32, w['X1'] - 32, 30); Y = f.rng_np.uniform(w['Y0'] + 32, w['NSY'] - 32, 30)
                        okm = f.ok(X, Y, 32.0)
                        if lx: okm &= ~np.any((X[:, None] - np.array(lx)) ** 2 + (Y[:, None] - np.array(ly)) ** 2 < (74) ** 2, axis=1)
                        idx = np.flatnonzero(okm)
                        if len(idx): lx.append(X[idx[0]]); ly.append(Y[idx[0]]); c += 1
                    got.append(c)
                out.append((stage, cnt, rule, L, float(np.mean(got))))
                T.append(f"[loose] 스테이지 {stage} 낱개 {cnt} md_map L0 ed_cols {L} {rule}: 놓인 낱개 평균 {np.mean(got):.2f}")
    O['loose'] = out


def part_cost(O, T):
    for z in (1.68, 1.0):
        for s in (1.0, 0.7, 0.45):
            RR = colRR(32 * s); f = new_field(z, 1)
            b = f.bounds(RR)
            npts = len(np.arange(b[0], b[1] + 1e-6, 8)) * len(np.arange(b[2], b[3] + 1e-6, 8))
            T.append(f"[cost] z{z} s{s}: 격자 점 {npts} (× 군락 수+장치 겹침 검사)")


if __name__ == '__main__':
    parts = sys.argv[1:] or ['place', 'prop', 'refill', 'queue', 'dyn', 'loose', 'cost']
    O = {}; T = []
    for p in parts:
        {'place': part_place, 'prop': part_prop, 'refill': part_refill, 'queue': part_queue, 'dyn': part_dyn, 'loose': part_loose, 'cost': part_cost}[p](O, T)
        print('\n'.join(T[-60:]), flush=True)
    tag = '_'.join(parts)
    open(f'verify_out_{tag}.txt', 'w', encoding='utf-8').write('\n'.join(T) + '\n')
    json.dump(O, open(f'verify_out_{tag}.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
