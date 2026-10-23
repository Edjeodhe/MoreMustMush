# T8-1 집계. 사용: python -X utf8 BalanceData/r8/test/t81_sum.py  (프로젝트 루트) -> T8-1/t81_sum_out.txt 로 리다이렉트
import csv,glob,statistics as S,collections,math
B='BalanceData/r8/test/'
def load(d):
    sd={}
    for f in sorted(glob.glob(B+d+'rounds_s*.csv')):
        r=list(csv.DictReader(open(f,encoding='utf-8'))); sd[int(r[0]['seed'])]=r
    return sd
def med(x): return S.median(x) if x else float('nan')
def g(x): return f'{x:.4g}'
t0=load('T8-0/'); t1=load('T8-1/'); t1h=load('T8-1/skill10/')
def byst(sd,st): return [r for rs in sd.values() for r in rs if int(r['stage'])==st]
print('## 1. T8-0 대비 내부 1~24 (시드 6 중앙값): net / 구매 수 / 수확기 / 군락 시작')
print('내부(표시) | net T8-0 | net T8-1 | 배율 | 구매 T0/T1 | 수확기 T0/T1 | 군락시작 T0/T1 | net T8-1 SKILL1.0')
for st in range(1,25):
    a=byst(t0,st); b=byst(t1,st); h=byst(t1h,st)
    f=lambda x,k:med([float(r[k]) for r in x])
    print(f"{st}({st-4}) | {g(f(a,'net'))} | {g(f(b,'net'))} | x{f(b,'net')/f(a,'net'):.2f} | {f(a,'buys'):.1f}/{f(b,'buys'):.1f} | {f(a,'balls'):.0f}/{f(b,'balls'):.0f} | {f(a,'colsStart'):.0f}/{f(b,'colsStart'):.0f} | {g(f(h,'net'))}")
def seg(sd,lo,hi):
    v=[]
    for rows in sd.values():
        x=[r for r in rows if lo<=int(r['stage'])<=hi and not r['boss'] and r['bossFlag']=='0']
        if x: v.append((S.mean(float(r['net']) for r in x),S.mean(int(r['balls']) for r in x),S.mean(int(r['colsStart']) for r in x),S.mean(int(r['harvests']) for r in x),S.mean(int(r['buys']) for r in x)))
    return [med(c) for c in zip(*v)]
print('\n## 2. 구간 요약 (일반 판, 시드 평균의 중앙): net / 수확기 / 군락시작 / 수확 / 판당 구매')
for name,lo,hi in [('1~4',1,4),('5~13',5,13),('15~23',15,23),('25~33',25,33),('35~43',35,43),('45~53',45,53)]:
    a=seg(t0,lo,hi); b=seg(t1,lo,hi); h=seg(t1h,lo,hi)
    print(f"내부 {name} | T8-0 net {g(a[0])} 수확기 {a[1]:.2f} 군락 {a[2]:.1f} 수확 {a[3]:.0f} | T8-1 net {g(b[0])} (x{b[0]/a[0]:.2f}) 수확기 {b[1]:.2f} 군락 {b[2]:.1f} 수확 {b[3]:.0f} 구매 {b[4]:.2f} | SKILL1.0 net {g(h[0])} 수확기 {h[1]:.2f} 군락 {h[2]:.1f} 구매 {h[4]:.2f}")
print('\n## 3. 보스 도전 수 / 처치 분 (표시 50 처치까지 분)')
for nm,sd in (('T8-0',t0),('T8-1 SKILL0.6',t1),('T8-1 SKILL1.0',t1h)):
    for b in (14,24,34,44,54):
        tr=[];mn=[]; fails=[]
        for s,rows in sd.items():
            rs=[r for r in rows if int(r['stage'])==b and r['boss']]
            tr.append(len(rs)); mn.append(float(rs[-1]['playMin']) if rs else float('nan'))
            fails += [round(float(r['bossLeft']),2) for r in rs if r['bossRes']=='실패']
        print(f"{nm} 보스 {b}({b-4}) 도전 {tr} 처치분 {[round(x,1) for x in mn]} 중앙 {med(mn):.1f} 실패시 남은 체력 {fails}")
    ends=[float(rows[-1]['playMin']) for rows in sd.values()]; print(f"{nm} 표시 50 처치(R54 끝) 분 {[round(x,1) for x in ends]} 중앙 {med(ends):.1f}; 총 판 수 {[len(r) for r in sd.values()]}")
print('\n## 4. verify#25 ①: 내부 1~13 판별 지표 (시드별 비율의 중앙·표본: 시드 6 × 판)')
for nm,sd in (('SKILL0.6',t1),('SKILL1.0',t1h)):
    print('--',nm)
    rat=collections.defaultdict(list); one=[];nxt=[];lt14=0;b2=[];b3=[]
    for s,rows in sd.items():
        N={int(r['stage']):r for r in rows if not r['boss']}
        if float(N[1]['net'])<14: lt14+=1
        for st in range(2,14):
            rat[st].append(float(N[st]['net'])/float(N[st-1]['net']))
        for st in range(1,14):
            r=N[st]
            if int(r['buys'])<=1: one.append((s,st))
            if float(r['nextCost'])/float(r['net'])>0.6: nxt.append((s,st,round(float(r['nextCost'])/float(r['net']),2)))
        b2.append(min([st for st in range(1,14) if int(N[st]['balls'])>=2] or [99])); b3.append(min([st for st in range(1,14) if int(N[st]['balls'])>=3] or [99]))
    print('판 1 net<14 시드 수:',lt14,'/ 6')
    print('판별 net 배율 중앙(2..13):',' '.join(f'{st}:{med(v):.2f}' for st,v in rat.items()))
    print('  배율 <1.5인 판 수(중앙 기준):',sum(1 for v in rat.values() if med(v)<1.5),'/12 ; 시드-판 중 <1.5 비율',sum(1 for v in rat.values() for x in v if x<1.5),'/',sum(len(v) for v in rat.values()))
    tot=1
    gm=[]
    for s,rows in sd.items():
        N={int(r['stage']):float(r['net']) for r in rows if not r['boss']}
        gm.append((N[8]/N[1])**(1/7))
    print('  1→8 판당 기하평균 배율(시드 중앙):',f'{med(gm):.2f}', ' 8→13:',f"{med([ (float([r for r in rows if int(r['stage'])==13][0]['net'])/float([r for r in rows if int(r['stage'])==8][0]['net']))**(1/5) for rows in sd.values()]):.2f}")
    print('  구매 1개 이하 시드-판:',len(one),one)
    print('  다음 가격/판 수입 >0.6 시드-판:',len(nxt),nxt)
    print('  수확기 2개 첫 판(시드별):',b2,'중앙',med(b2),' 3개 첫 판:',b3,'중앙',med(b3))
print('\n## 5. ⑧ 내부 35~48 판당 구매 수·net 배율 (시드 중앙)')
for nm,sd in (('SKILL0.6',t1),('SKILL1.0',t1h)):
    print('--',nm)
    for st in list(range(35,49)):
        rs=[r for rows in sd.values() for r in rows if int(r['stage'])==st and not r['boss']]
        ps=st-1
        while not [1 for rows in sd.values() for r in rows if int(r['stage'])==ps and not r['boss']]: ps-=1
        prev=[float(r['net']) for rows in sd.values() for r in rows if int(r['stage'])==ps and not r['boss']]
        if not rs: print(f'{st}: (보스 판)'); continue
        print(f"{st}({st-4}): 구매 중앙 {med([int(r['buys']) for r in rs]):.1f} 평균 {S.mean(int(r['buys']) for r in rs):.2f} net 중앙 {g(med([float(r['net']) for r in rs]))} 전판 대비 x{med([float(r['net']) for r in rs])/med(prev):.2f}")
    allb=[int(r['buys']) for rows in sd.values() for r in rows if 35<=int(r['stage'])<=48 and not r['boss']]
    print('35~48 판당 구매 평균',f'{S.mean(allb):.2f}','최소',min(allb),'0구매 판 비율',f'{allb.count(0)/len(allb):.2f}')
    print('끝(보스54 처치) 공격력', [round(float(rows[-1]['atk'])) for rows in sd.values()])

print('\n## 6. ② ps_ball 0 vs 1 (내부 10 시작 상태, SKILL 0.6, 시드 6 x 6판, 같은 난수 짝)')
rows=list(csv.DictReader(open(B+'T8-1/ball.csv',encoding='utf-8')))
for bl in ('0','1'):
    x=[float(r['net']) for r in rows if r['psBall']==bl]; h=[int(r['harvests']) for r in rows if r['psBall']==bl]
    print(f'ps_ball={bl}: net 평균 {g(S.mean(x))} 중앙 {g(med(x))} 수확 평균 {S.mean(h):.0f} 판 n={len(x)}')
m0=S.mean(float(r['net']) for r in rows if r['psBall']=='0'); m1=S.mean(float(r['net']) for r in rows if r['psBall']=='1')
print(f'net 비(평균) x{m1/m0:.2f}')
pr=[]
for r0 in rows:
    if r0['psBall']=='0':
        r1=[r for r in rows if r['psBall']=='1' and r['seed']==r0['seed'] and r['rep']==r0['rep']][0]; pr.append(float(r1['net'])/float(r0['net']))
print(f'짝 비 중앙 x{med(pr):.2f} 범위 x{min(pr):.2f}~x{max(pr):.2f}; 시드별 평균비',[round(S.mean(float(r['net']) for r in rows if r['psBall']=='1' and r['seed']==str(s))/S.mean(float(r['net']) for r in rows if r['psBall']=='0' and r['seed']==str(s)),2) for s in range(1,7)])

print('\n## 7. ⑤ ed_cols L4 군락 (스냅 33·53, 스테이지 33/53 일반 판 시작, 시드 6 x 20판)')
rows=list(csv.DictReader(open(B+'T8-1/cols.csv',encoding='utf-8')))
for sn in ('33','53'):
    x=[r for r in rows if r['snap']==sn]
    print(f"스냅 {sn}(표시 {int(sn)-4}): maxCol 중앙 {med([int(r['maxCol']) for r in x])} [{min(int(r['maxCol']) for r in x)}~{max(int(r['maxCol']) for r in x)}] 시작 군락 중앙 {med([int(r['colsStart']) for r in x])} 못 놓음 평균 {S.mean(int(r['unplaced']) for r in x):.2f} (>0 판 {sum(1 for r in x if int(r['unplaced'])>0)}/{len(x)}) colS 중앙 {med([float(r['colS']) for r in x]):.3f} [{min(float(r['colS']) for r in x):.3f}~{max(float(r['colS']) for r in x):.3f}] ColSMin 중앙 {med([float(r['colSMin']) for r in x]):.3f} 닿은 판 {sum(int(r['atMin']) for r in x)}/{len(x)} zoom 중앙 {med([float(r['zoom']) for r in x]):.3f} md_map {sorted(set(r['mdMap'] for r in x))} md_map2 {sorted(set(r['mdMap2'] for r in x))}")
    for w in sorted(set(r['weather'] for r in x)):
        y=[r for r in x if r['weather']==w]; print(f"   날씨 {w}: n={len(y)} maxCol {med([int(r['maxCol']) for r in y])} 시작 {med([int(r['colsStart']) for r in y])} colS {med([float(r['colS']) for r in y]):.3f} 닿음 {sum(int(r['atMin']) for r in y)}")

print('\n## 8. ④ 보스 14·24 사람 상태 / bot (SKILL별 시드 6 x 12판)')
rows=list(csv.DictReader(open(B+'T8-1/c24.csv',encoding='utf-8')))
for stt in ('h14','bot14','h24','h24+atk2','bot24'):
    for sk in ('0.6','1'):
        x=[r for r in rows if r['state']==stt and r['skill']==sk]
        cl=[int(r['cleared']) for r in x]; lf=[float(r['leftPct']) for r in x if not int(r['cleared'])]
        print(f"{stt} SKILL{sk}: 처치 {sum(cl)}/{len(x)} 실패판 남은 체력 중앙 {(med(lf)*100 if lf else 0):.0f}% (n={len(lf)}) 공격력 중앙 {g(med([float(r['atk']) for r in x]))} 핀볼 {med([int(r['balls']) for r in x]):.0f} 총DPS 중앙 {g(med([float(r['grossDps']) for r in x]))} 순DPS {g(med([float(r['netDps']) for r in x]))} 처치시간 중앙 {med([float(r['fightT']) for r in x if int(r['cleared'])]):.1f}s maxHp {g(med([float(r['maxHp']) for r in x]))}")

print('\n## 9. ③ 유도 배율 M (SKILL 1.0, homing 0 vs 2·3·4, 같은 난수 짝). 총 DPS = 보스 피해 합 / 전투 시간')
rows=list(csv.DictReader(open(B+'T8-2/m.csv',encoding='utf-8')))
key=lambda r:(int(r['boss']),int(r['lag']),r['hpScale'])
print('보스(내부) lag hp배율 | homing0 총DPS 평균(공격력 중앙,핀볼) | M2 | M3 | M4  (비 = 평균DPS 비 / 짝 비 중앙) | hp1배율에서 처치율 h0/h2/h3/h4, 남은체력 중앙 h0→h4')
for k in sorted(set(key(r) for r in rows)):
    x=[r for r in rows if key(r)==k]
    d0=[r for r in x if r['homing']=='0']
    base=S.mean(float(r['grossDps']) for r in d0)
    out=[]
    for hm in ('2','3','4'):
        dh=[r for r in x if r['homing']==hm]
        pr=[]
        for r0 in d0:
            r1=[r for r in dh if r['seed']==r0['seed'] and r['rep']==r0['rep']]
            if r1: pr.append(float(r1[0]['grossDps'])/float(r0['grossDps']))
        out.append(f"x{S.mean(float(r['grossDps']) for r in dh)/base:.2f}/x{med(pr):.2f}")
    extra=''
    if k[2]=='1':
        ch=lambda hm:sum(int(r['cleared']) for r in x if r['homing']==hm)/max(1,len([r for r in x if r['homing']==hm]))
        lf=lambda hm:med([float(r['leftPct']) for r in x if r['homing']==hm])
        extra=f" | 처치율 {ch('0'):.2f}/{ch('2'):.2f}/{ch('3'):.2f}/{ch('4'):.2f} 남은 {lf('0'):.2f}→{lf('4'):.2f}"
    print(f"{k[0]}(표시 {k[0]-4}) lag{k[1]} hp x{k[2]} | {g(base)}(공격력 {g(med([float(r['atk']) for r in d0]))}, 핀볼 {med([int(r['balls']) for r in d0]):.0f}, 보스 {d0[0]['bossId']}) | {' | '.join(out)}{extra}")
print('-- 보스별 (lag 0·4·8 합, hp x50 = DPS 전용) 요약: M(homing 2/3/4) = 평균 DPS 비')
for b in (34,44,54):
    for hm in ('2','3','4'):
        a=[float(r['grossDps']) for r in rows if int(r['boss'])==b and r['hpScale']=='50' and r['homing']==hm]
        z=[float(r['grossDps']) for r in rows if int(r['boss'])==b and r['hpScale']=='50' and r['homing']=='0']
        print(f"  보스 {b}: homing {hm} M = x{S.mean(a)/S.mean(z):.2f} (n={len(a)})", end='')
    print()

print('\n## 10. ⑦ 옛 세이브(inf_atk 129·inf_gold 117 + 트리 최대) 표시 60(내부 64)·70(내부 74) 보스')
rows=list(csv.DictReader(open(B+'T8-1/legacy.csv',encoding='utf-8')))
for v in ('tree+inf0','tree+inf129','tree+gem+inf129'):
    for st in ('64','74'):
        for sk in ('0.6','1'):
            x=[r for r in rows if r['variant']==v and r['stage']==st and r['skill']==sk]
            cl=sum(int(r['cleared']) for r in x); lf=[float(r['leftPct']) for r in x if not int(r['cleared'])]
            print(f"{v} 내부{st}(표시 {int(st)-4}) SKILL{sk}: 처치 {cl}/{len(x)} -> 도전 수 기대 {len(x)/max(cl,0.01):.1f} · 보스 {x[0]['boss']} 공격력 중앙 {g(med([float(r['atk']) for r in x]))} maxHp {g(med([float(r['maxHp']) for r in x]))} 실패 남은 체력 중앙 {(med(lf)*100 if lf else 0):.0f}% 총DPS {g(med([float(r['grossDps']) for r in x]))}")
