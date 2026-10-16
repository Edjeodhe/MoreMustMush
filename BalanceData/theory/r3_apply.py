# -*- coding: utf-8 -*-
"""R3 적용안 DB 초안 만들기 (관리자가 db_write.py로 쓴다 — 이 스크립트는 파일만 만든다).
  r3_apply_set.json    : db_write.py set    — Upgrade 효과값·최대 레벨·부모 (ed_cols, ps_dur)
  r3_apply_prices.json : db_write.py prices — 1단계 앞쪽 골드 가격 (노드 키 → 레벨별 가격, 위 레벨은 지금 값 유지)
  r3_rare0_rows.json   : db_write.py rows   — [선택, 사용자 결정] 새 노드 ed_rare0 (Upgrade·Upgrade_Cost·String)
실행: python BalanceData/theory/r3_apply.py   (r3_model.py의 PRICING·효과표를 그대로 쓴다)
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r3_model as M  # noqa: E402


def main():
    d = json.load(open(M.DB, encoding="utf-8"))
    up = {u["Upg_Key"]: u for u in d["Upgrade"]}
    cost = {c["Cost_ID"]: c for c in d["Upgrade_Cost"]}

    upg_set = {
        "ed_cols": {"Efx_MaxLv": 4, **{f"Efx_Lv{i}": v for i, v in enumerate(M.ED_COLS_EFX)}},
        "ps_dur": {"Efx_MaxLv": 4, "Upg_Pre": up["power"]["Upg_ID"], **{f"Efx_Lv{i}": v for i, v in enumerate(M.PS_DUR_EFX)}},
    }
    strings = {"201006": {"Str_KR": "한 라운드의 제한시간을 늘려요. 라운드는 1~4스테이지 10초, 그 뒤로는 10초에 스테이지마다 0.5초씩(최대 +10초) 길어지고, 여기에 더해져요."}}
    json.dump({"Upgrade": upg_set, "String": strings}, open(os.path.join(HERE, "r3_apply_set.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    prices = {}
    for k, (p1, q, n) in M.PRICING.items():
        if k == "ed_rare0":
            continue
        u = up[k]
        c = cost[u["Cost_ID"]]
        maxlv = 4 if k in ("ed_cols", "ps_dur") else u["Efx_MaxLv"]
        lv = []
        for L in range(1, maxlv + 1):
            lv.append(M.nice(p1 * q ** (L - 1)) if L <= n else c[f"Cost_Lv{L}"])
        prices[k] = lv
    json.dump(prices, open(os.path.join(HERE, "r3_apply_prices.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    p1, q, n = M.PRICING["ed_rare0"]
    rows = {
        "Upgrade": [{"Upg_ID": 13033, "Upg_Key": "ed_rare0", "Upg_Name": "희귀 포자", "Str_Group_ID": 23033,
                     "Upg_Pre": up["ed_cols"]["Upg_ID"], "Upg_Req": 0, "Branch": "ed", "Type": "rare_mul", "Efx_MaxLv": 3,
                     "Cost_ID": 90090, "Tier": 1, "Sub": 0, "Skill": "", "Icon": "", "Color": "",
                     **{f"Efx_Lv{i}": (M.ED_RARE0_EFX[i] if i < len(M.ED_RARE0_EFX) else 0) for i in range(5)}}],
        "Upgrade_Cost": [{"Cost_ID": 90090, **{f"Cost_Lv{L}": (M.nice(p1 * q ** (L - 1)) if L <= n else 0) for L in range(1, 5)},
                          "Gem_Lv1": 0, "Gem_Lv2": 0, "Gem_Lv3": 0, "Gem_Lv4": 0}],
        "String": [
            {"Str_ID": 303033, "Str_Group_ID": 23033, "Str_Name": "upg:ed_rare0:name", "Str_KR": "희귀 포자", "Str_EN": "", "Str_JA": ""},
            {"Str_ID": 203033, "Str_Group_ID": 23033, "Str_Name": "upg:ed_rare0:desc",
             "Str_KR": "에픽 이상 버섯이 더 자주 돋아요. 다 올리면 숲의 본래 빈도가 돼요.", "Str_EN": "", "Str_JA": ""},
        ],
    }
    json.dump(rows, open(os.path.join(HERE, "r3_rare0_rows.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(upg_set, ensure_ascii=False))
    print(json.dumps(prices, ensure_ascii=False))
    print(json.dumps(rows["Upgrade_Cost"], ensure_ascii=False))


if __name__ == "__main__":
    main()
