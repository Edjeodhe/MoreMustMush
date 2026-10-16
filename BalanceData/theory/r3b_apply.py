# -*- coding: utf-8 -*-
"""R3 개정 최종 적용표 (관리자가 db_write.py·코드에 옮긴다 — 이 스크립트는 파일만 만든다).
  r3b_apply_set.json    : db_write.py set    — Upgrade 효과값·최대 레벨·부모 (ed_cols, ps_dur), String 201006
  r3b_apply_prices.json : db_write.py prices — 1단계 앞쪽 골드 가격 (노드 키 → Cost_Lv1~최대, 위 레벨은 지금 DB 값)
  r3b_rare0_rows.json   : db_write.py rows   — 새 노드 ed_rare0 (Upgrade·Upgrade_Cost·String) — 사용자 결정 ① 채택
  r3b_code.json         : 코드 상수·식 (파일·식별자·값) — 관리자가 손으로
  r3b_migration.json    : 옛 저장 레벨 옮김 표 (SaveMigration v8)
  r3b_apply_boss.json   : db_write.py set    — [사용자 결정] Boss king Hp_Mul 0.63 → 0.10 (첫 보스 10, R3-22)
실행: python BalanceData/theory/r3b_apply.py   (r3b_model.LADDER·LOOSE·STUMP_MUL_NEW와 r3_model 효과표를 그대로 쓴다)
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r3_model as R3  # noqa: E402
import r3b_model as M  # noqa: E402

OLD_COLS = [12, 17, 22, 27]        # tuned ColCount = ceil(12 + 5L), L 0~3 (Defs.cs:123, 프리셋 tuned)
OLD_DUR = [0, 2, 4, 6]             # 지금 DB ps_dur Efx, BASE_TIME 15
OLD_BASE, NEW_BASE = 15, 10


def migration():
    """옛 레벨 → 효과가 줄지 않는 가장 낮은 새 레벨 (스테이지 5 이상 기준. 1~4는 EARLY_SEC 10 그대로라 새 레벨이 같거나 큼)"""
    cols = {L: next(n for n, v in enumerate(R3.ED_COLS_EFX) if v >= OLD_COLS[L]) for L in range(4)}
    dur = {L: next(n for n, v in enumerate(R3.PS_DUR_EFX) if NEW_BASE + v >= OLD_BASE + OLD_DUR[L]) for L in range(4)}
    rows = []
    for L in range(4):
        rows.append(dict(old_lv=L, ed_cols_old=OLD_COLS[L], ed_cols_new_lv=cols[L], ed_cols_new=R3.ED_COLS_EFX[cols[L]],
                         ps_dur_old_sec=OLD_BASE + OLD_DUR[L], ps_dur_new_lv=dur[L], ps_dur_new_sec=NEW_BASE + R3.PS_DUR_EFX[dur[L]]))
    return dict(
        rule="옛 레벨 → 효과가 줄지 않는 가장 낮은 새 레벨 (관리자 결정 verify#13). 군락은 ColCount 값(지역·지도 배율 전), 시간은 스테이지 5 이상 기본+노드",
        table=rows,
        ed_rare0="옛 저장(v<8)이고 stage >= 7이면 ed_rare0 = 3 (가중치 1.0 = 지금). stage <= 6이면 0 (스테이지 1~6은 0등급만 나와 영향 없음)",
        parent_ok="ed_cols2·ps_dur2의 req는 '부모 최대(새 4)'. 옛 저장에서 ed_cols2·ps_dur2 ≥ 1이면 옛 부모가 3(최대) → 새 4 = 최대라 잠기지 않음. 옛 0~1 → 새 3은 옛에도 2단계를 못 샀던 경우뿐",
        ps_dur_parent="ps_dur 부모가 ps_atk → power로 바뀜. 옛 ps_dur ≥ 1이면 power ≥ 1 (ps_atk 부모가 power)라 문제 없음",
        free_value_gold=dict(ed_cols_L0_to_L3=sum(M.LADDER["ed_cols"][:3]), ps_dur_L0_to_L3=sum(M.LADDER["ps_dur"][:3])),
    )


def code_constants():
    return {
        "Defs.cs:106 BASE_TIME": {"now": 15, "new": 10, "note": "EARLY_TIME_STAGES 4 · EARLY_SEC 10 분기는 그대로. 주석 '처음 15초' 고침"},
        "Defs.cs:115~116 EARLY_COL_MUL · EARLY_REGEN_MUL": {"now": "{0.4,…,0.85} · {1.6,…,1.1}", "new": "삭제 (Game.cs:255~259 초반 블록도 삭제)"},
        "Defs.cs:123 ColCount(int L)": {"now": "Mathf.CeilToInt(TUNE.col0 + TUNE.colStep * L)", "new": "(int)Math.Round(V(\"ed_cols\", L))  // DB Efx 4·6·10·17·27"},
        "Game.cs:242 Math.Max(3, …) · Game.cs:254 Math.Max(4, …)": {"new": "둘 다 Math.Max(1, …)"},
        "Defs.cs (새) EARLY_LOOSE": {"new": [M.LOOSE.get(1, M.LOOSE[2])] + [M.LOOSE[s] for s in range(2, 7)],
                                    "note": "스테이지 1~6 낱개 버섯 수 {6,6,5,4,3,2}. [0](스테이지 1)은 튜토리얼이 아닐 때만 쓴다(튜토리얼은 TUTORIAL_LOOSE 8을 첫 발사 때)"},
        "RoundSim.cs Start() (군락 스폰 다음 줄)": {"new": "if (st.boss == null && !r.tutorial && EarlyStage(st.stage)) r.SpawnLoose(EARLY_LOOSE[st.stage - 1]);",
                                                  "note": "판 시작 때 한 번. 다시 생기지 않음(SpawnLoose 그대로: col==null, 빈 자리, PickSpecies). 노드와 무관 — 군락을 사도 줄지 않는다"},
        "RoundSim.Balls.cs:222 그루터기 점수 * 5": {"now": 5, "new": M.STUMP_MUL_NEW},
        "Defs.Nodes.cs NF (새)": {"new": "public static double ed_rare0(int L) => V(\"ed_rare0\", L);"},
        "Defs.Nodes.cs E (새)": {"new": "E[\"ed_rare0\"] = L => Loc.F(\"에픽 이상 가중치 ×{0}\", F(NF.ed_rare0(L)));"},
        "Game.cs:244 rareMul": {"now": "NF.ed_rare(Lv(\"ed_rare\")) + ab[\"rare\"]",
                                "new": "(NF.ed_rare(Lv(\"ed_rare\")) + ab[\"rare\"]) * NF.ed_rare0(Lv(\"ed_rare0\"))"},
        "SaveMigration.cs CURRENT · ToV8": {"now": 7, "new": 8, "note": "r3b_migration.json 표대로 ed_cols·ps_dur 레벨 옮김 + stage ≥ 7이면 ed_rare0 = 3"},
        "BalanceSim.cs EnforceShape 줄 규칙(:483 근처) · CapLines(:497 근처)": {
            "new": "for 루프 안 맨 앞에 `if (t == 1 && per[1] == per[0]) continue;` — 1단계 형제(I → I+, 효과 같은 줄) ×4 규칙만 뺀다 (verify#12 안 a)",
            "note": "공격력 줄 t=1(ps_atk → ps_atk2)은 per가 달라 그대로 검사. 수확량 줄 ed_bonus → ed_bonus1p도 빠지지만 지금 ×3.3이라 영향 없음"},
    }


def main():
    d = json.load(open(R3.DB, encoding="utf-8"))
    up = {u["Upg_Key"]: u for u in d["Upgrade"]}
    cost = {c["Cost_ID"]: c for c in d["Upgrade_Cost"]}
    upg_set = {
        "ed_cols": {"Efx_MaxLv": 4, **{f"Efx_Lv{i}": v for i, v in enumerate(R3.ED_COLS_EFX)}},
        "ps_dur": {"Efx_MaxLv": 4, "Upg_Pre": up["power"]["Upg_ID"], **{f"Efx_Lv{i}": v for i, v in enumerate(R3.PS_DUR_EFX)}},
    }
    strings = {"201006": {"Str_KR": "한 라운드의 제한시간을 늘려요. 라운드는 1~4스테이지 10초, 그 뒤로는 10초에 스테이지마다 0.5초씩(최대 +10초) 길어지고, 여기에 더해져요."}}
    json.dump({"Upgrade": upg_set, "String": strings}, open(os.path.join(HERE, "r3b_apply_set.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    prices = {}
    for k, lv in M.LADDER.items():
        if k == "ed_rare0":
            continue
        c = cost[up[k]["Cost_ID"]]
        maxlv = 4 if k in ("ed_cols", "ps_dur") else up[k]["Efx_MaxLv"]
        prices[k] = [float(lv[L - 1]) if L <= len(lv) else c[f"Cost_Lv{L}"] for L in range(1, maxlv + 1)]
    json.dump(prices, open(os.path.join(HERE, "r3b_apply_prices.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    rare = M.LADDER["ed_rare0"]
    rows = {
        "Upgrade": [{"Upg_ID": 13033, "Upg_Key": "ed_rare0", "Upg_Name": "희귀 포자", "Str_Group_ID": 23033,
                     "Upg_Pre": up["ed_cols"]["Upg_ID"], "Upg_Req": 0, "Branch": "ed", "Type": "rare_mul", "Efx_MaxLv": 3,
                     "Cost_ID": 90090, "Tier": 1, "Sub": 0, "Skill": "", "Icon": "", "Color": "",
                     **{f"Efx_Lv{i}": (R3.ED_RARE0_EFX[i] if i < len(R3.ED_RARE0_EFX) else 0) for i in range(5)}}],
        "Upgrade_Cost": [{"Cost_ID": 90090, **{f"Cost_Lv{L}": (float(rare[L - 1]) if L <= len(rare) else 0) for L in range(1, 5)},
                          "Gem_Lv1": 0, "Gem_Lv2": 0, "Gem_Lv3": 0, "Gem_Lv4": 0}],
        "String": [
            {"Str_ID": 303033, "Str_Group_ID": 23033, "Str_Name": "upg:ed_rare0:name", "Str_KR": "희귀 포자", "Str_EN": "", "Str_JA": ""},
            {"Str_ID": 203033, "Str_Group_ID": 23033, "Str_Name": "upg:ed_rare0:desc",
             "Str_KR": "에픽 이상 버섯이 더 자주 돋아요. 다 올리면 숲의 본래 빈도가 돼요.", "Str_EN": "", "Str_JA": ""},
        ],
    }
    json.dump(rows, open(os.path.join(HERE, "r3b_rare0_rows.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump(code_constants(), open(os.path.join(HERE, "r3b_code.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump({"Boss": {"king": {"Hp_Mul": 0.1}}}, open(os.path.join(HERE, "r3b_apply_boss.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    mig = migration()
    json.dump(mig, open(os.path.join(HERE, "r3b_migration.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(upg_set, ensure_ascii=False))
    print(json.dumps(prices, ensure_ascii=False))
    print(json.dumps(rows["Upgrade_Cost"], ensure_ascii=False))
    print(json.dumps(mig["table"], ensure_ascii=False))
    print(mig["free_value_gold"])
    assert math.isclose(sum(R3.PS_DUR_EFX[-1:]) + NEW_BASE, OLD_BASE + OLD_DUR[-1])


if __name__ == "__main__":
    main()
