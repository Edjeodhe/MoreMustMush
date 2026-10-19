"""같은 등급(★) 정령끼리 키를 맞추는 배율: spirit_rig2.json 모습마다 gradeScale = (그 등급 평균 키) / (이 모습 키).
키 = height (갓 꼭대기 ~ 발바닥, 리그 단위). ★2·★3은 불 정령과 같은 배율(gradeScale 1). 게임(CritterRig.Rig2)과 웹 '모두 보기'가 이 배율을 곱한다. 리그 안 좌표는 그대로.
python Tools/rig-web/grade_heights.py   (그림·관절을 바꾼 뒤 다시 돌린다)"""
import json
import os

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets/Resources/Rig/spirit_rig2.json")
src = open(P, encoding="utf-8").read()
data = json.loads(src)
by = {}
for l in data["looks"]:
    by.setdefault(l["form"], []).append(l["height"])
avg = {f: sum(v) / len(v) for f, v in by.items()}
data["gradeHeight"] = {str(f): round(avg[f], 4) for f in sorted(avg)}
# ★2·★3 (사람형): 몸통이 모두 같은 공용 틀이고 머리는 불 정령 얼굴 크기에 맞춰 놓았으므로(fit headScale) 배율을 따로 주지 않는다 (= 불과 같은 배율)
for l in data["looks"]:
    l["gradeScale"] = 1.0 if l["form"] >= 2 else round(avg[l["form"]] / l["height"], 4)
out = json.dumps(data, ensure_ascii=False, indent=1) + ("\n" if src.endswith("\n") else "")
open(P, "w", encoding="utf-8", newline="\n").write(out)
for f in sorted(avg):
    print(f"★{f} 평균 키 {avg[f]:.3f}:", ", ".join(f"{l['id']} {l['gradeScale']}" for l in data["looks"] if l["form"] == f))
