# 7-2 재조정 예상: 구간별 판 수입비(7-2안 / 6회차). 입력은 같은 폴더 결과(측정 기반)와 흐름식 한 줄.
#   순서: align.py(모듈) → decomp.py → cumalign.py → stagemul_cap.py → proposal72.py
#   안: stageMul = max(1+0.03(c−1), min(1+0.05(c−1), 2.4)) · ed_regen L0·L1 2.5·1.8 → 3.0·2.2 (L2·L3 그대로) · 폭풍 재생 ×2 → ×1.4
import json,math
D='BalanceData/r7/theory/r72/'
cum=json.load(open(D+'cumalign_out.json')); cap=json.load(open(D+'stagemul_cap_out.json'))['a0.05_cap2.4']
MEAS={'1~5':(1.12,1.25),'6~13':(1.15,1.18),'14~20':(0.90,0.76),'21~30':(0.46,0.36),'31~55':(0.94,0.80)}   # T7-3/compare.txt
STATE={'1~5':None,'6~13':None,'14~20':cum['14~20']['inc'],'21~30':cum['21~30']['inc'],'31~55':cum['31~55']['inc']}
VG={'1~5':1.0,'6~13':cap['6~13'],'14~20':cap['14~20'],'21~30':cap['21~30'],'31~55':cap['31~55']}
TC={'1~5':4.5,'6~13':5.8,'14~20':2.3,'21~30':1.1,'31~55':0.57}   # 군락 하나 비우는 시간(초): 이론_7 5절, 31~55는 B_sum 맑음 역산
REG_NOW={'1~5':2.5,'6~13':1.8,'14~20':0.25,'21~30':0.25,'31~55':0.25}
REG_NEW={'1~5':3.0,'6~13':2.2,'14~20':0.25,'21~30':0.25,'31~55':0.25}
P_STORM=1/6
def flow(tc,tr): return 1/(tc+tr+0.175)
def storm_gain(tc,tr):   # 폭풍 판(속도 ×0.8 → t_c/0.8) 재생 ×2 → ×1.4, 6판에 1판
    a=flow(tc/0.8,2*tr); b=flow(tc/0.8,1.4*tr); return 1+P_STORM*(b-a)/flow(tc,tr)
out={}
print('구간 | 지금 측정 평균(기하) | 같은 상태 지금 | 등급 V × | 재생 × | 폭풍 × | 같은 상태 7-2 | 표시 구간 예상')
for k,(m,g) in MEAS.items():
    rg=flow(TC[k],REG_NEW[k])/flow(TC[k],REG_NOW[k]); sg=storm_gain(TC[k],REG_NEW[k]); vg=VG[k]
    if STATE[k] is None:   # 1~13: 늦어짐 없음 → 표시 평균비에 곱
        lo=hi=m*vg*rg*sg; st=None
    else:
        st=STATE[k]*vg*rg*sg
        amp=math.log(g)/math.log(STATE[k]) if STATE[k]<0.99 else 1.0
        # 늦어짐이 남는 쪽(같은 상태비^증폭)과 없어지는 쪽(같은 상태비) 사이. 14~20은 상태 정렬이 스테이지 배율과 섞여 표시 평균비에 곱한 값도 같이 본다
        c=[st, st**amp] + ([m*vg*rg*sg] if k=='14~20' else [])
        lo,hi=min(c),max(c)
    out[k]=dict(meas_mean=m,meas_geo=g,state_now=STATE[k],Vgain=vg,regen=rg,storm=sg,state_new=st,lo=lo,hi=hi)
    print(f"{k} | ×{m:.2f}(×{g:.2f}) | {('×%.2f'%STATE[k]) if STATE[k] else '-'} | ×{vg:.2f} | ×{rg:.2f} | ×{sg:.3f} | {('×%.2f'%st) if st else '-'} | ×{lo:.2f}~×{hi:.2f}")
json.dump(out,open(D+'proposal72_out.json','w'),indent=1)
