# -*- coding: utf-8 -*-
"""에이전트 사이 요청 현황. 사용: python 밸런스설계/요청보기.py [내이름]
내이름을 주면 나에게 온 열린 요청과 내 요청에 달린 답만, 없으면 전체(메인 조율용)를 보여 준다."""
import io, os, re, sys

NAMES = ["manager", "research", "theory", "verify", "test"]
DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "현황")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

REQ = re.compile(r"^- \[( |x)\] #(\d+) → (\w+): (.*)$")
ANS = re.compile(r"^- \[x\] (\w+)#(\d+) → (.*)$")


def sections(name):
    path = os.path.join(DIR, name + ".md")
    if not os.path.exists(path):
        return {}, ""
    cur, out, now = None, {}, ""
    for line in open(path, encoding="utf-8").read().splitlines():
        if line.startswith("지금:"):
            now = line[3:].strip()
        if line.startswith("## "):
            cur = line[3:].strip()
            out[cur] = []
        elif cur:
            out[cur].append(line.strip())
    return out, now


reqs, answers, nows = [], {}, {}
for n in NAMES:
    sec, nows[n] = sections(n)
    for line in sec.get("요청", []):
        m = REQ.match(line)
        if m:
            reqs.append(dict(frm=n, no=int(m.group(2)), to=m.group(3), text=m.group(4), closed=m.group(1) == "x"))
    for line in sec.get("받은 요청 처리", []):
        m = ANS.match(line)
        if m:
            answers[(m.group(1), int(m.group(2)))] = (n, m.group(3))

me = sys.argv[1] if len(sys.argv) > 1 else None
open_reqs = [r for r in reqs if not r["closed"] and (r["frm"], r["no"]) not in answers]
answered = [r for r in reqs if not r["closed"] and (r["frm"], r["no"]) in answers]

if me:
    mine = [r for r in open_reqs if r["to"] == me]
    print(f"[{me}] 나에게 온 열린 요청 {len(mine)}개")
    for r in mine:
        print(f"  {r['frm']}#{r['no']}: {r['text']}")
    got = [r for r in answered if r["frm"] == me]
    print(f"[{me}] 내 요청에 달린 답 {len(got)}개 (확인하면 내 파일에서 [x]로)")
    for r in got:
        who, a = answers[(r["frm"], r["no"])]
        print(f"  {me}#{r['no']} ← {who}: {a}")
else:
    print("지금:")
    for n in NAMES:
        print(f"  {n}: {nows[n] or '(현황 없음)'}")
    print(f"열린 요청 {len(open_reqs)}개")
    for r in open_reqs:
        block = "막힘" if "[막힘]" in r["text"] else "참고"
        print(f"  {r['frm']}#{r['no']} → {r['to']} [{block}] 받는 쪽: {nows.get(r['to'], '?')}")
        print(f"     {r['text']}")
    print(f"답 달림·보낸 쪽 미확인 {len(answered)}개")
    for r in answered:
        print(f"  {r['frm']}#{r['no']} ← {answers[(r['frm'], r['no'])][0]}")
