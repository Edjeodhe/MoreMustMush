# -*- coding: utf-8 -*-
"""Unity 도구가 만든 결과를 엑셀 DB(Ver.04 이상)에 Excel COM으로 쓴다. (openpyxl로 저장하면 Skima 도형이 사라지므로 COM만 쓴다.)

사용:
  python BalanceData/db/db_write.py strings <xlsx> <json>   # String 시트 맨 아래에 새 문구 추가 (Unity 메뉴 MoreMush/다국어/3 번역표 갱신)
  python BalanceData/db/db_write.py prices  <xlsx> <json>   # Upgrade_Cost의 골드 가격을 바꿈 (BalanceSim.WritePriceFile 결과)

strings json = ["한국어 문구", ...]  ·  prices json = {"노드 키": [1레벨 가격, 2레벨 가격, ...]}
파일이 다른 Excel에서 열려 있으면 아무것도 안 하고 중단한다(종료 코드 2). 중간에 오류가 나면 저장하지 않는다.
저장하면 Unity가 Assets/Resources/DB/db.json을 다시 만든다.
"""
import io
import json
import os
import shutil
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from db_apply_com import abort, block, check_not_open, retry  # noqa: E402

DATA_ROW = 4
UI_GROUP0 = 90000          # UI 문구 그룹 = 9nnnn, Str_ID = 9 0 nnnn


def header_cols(ws):
    """2행 영문 키 -> 열 번호."""
    cols, c = {}, 1
    while True:
        v = ws.Cells(2, c).Value
        if v is None:
            return cols
        cols[str(v)] = c
        c += 1


def last_row(ws):
    r = ws.Cells(ws.Rows.Count, 1).End(-4162).Row     # xlUp
    return max(r, DATA_ROW - 1)


def safe_text(s):
    """=, +, -, @, ' 로 시작하는 글이 수식으로 읽히지 않게."""
    return "'" + s if s[:1] in "=+-@'" else s


def add_strings(wb, items):
    ws = wb.Worksheets("String")
    c = header_cols(ws)
    end = last_row(ws)
    have, max_group = set(), UI_GROUP0
    if end >= DATA_ROW:
        vals = ws.Range(ws.Cells(DATA_ROW, 1), ws.Cells(end, max(c.values()))).Value
        for row in vals:
            kr = row[c["Str_KR"] - 1]
            if kr is not None:
                have.add(str(kr))
            g = row[c["Str_Group_ID"] - 1]
            if g is not None and int(g) > max_group and int(g) < 100000:
                max_group = int(g)
    rows = []
    for s in items:
        if not s or s in have:
            continue
        have.add(s)
        max_group += 1
        sid = int("9" + "0" + str(max_group)[1:])
        row = [None] * max(c.values())
        row[c["Str_ID"] - 1] = sid
        row[c["Str_Group_ID"] - 1] = max_group
        row[c["Str_Name"] - 1] = "ui"
        row[c["Str_KR"] - 1] = safe_text(s)
        row[c["Str_EN"] - 1] = ""
        row[c["Str_JA"] - 1] = ""
        rows.append(row)
    if rows:
        block(ws, end + 1, 1, rows)
    return "String %d줄 추가" % len(rows)


def set_prices(wb, prices):
    up, uc = wb.Worksheets("Upgrade"), wb.Worksheets("Upgrade_Cost")
    cu, cc = header_cols(up), header_cols(uc)
    cost_id = {}
    for r in range(DATA_ROW, last_row(up) + 1):
        k = up.Cells(r, cu["Upg_Key"]).Value
        if k is not None:
            cost_id[str(k)] = int(up.Cells(r, cu["Cost_ID"]).Value)
    cost_row = {}
    for r in range(DATA_ROW, last_row(uc) + 1):
        v = uc.Cells(r, cc["Cost_ID"]).Value
        if v is not None:
            cost_row[int(v)] = r
    n, missing = 0, []
    for key, levels in prices.items():
        if key not in cost_id or cost_id[key] not in cost_row:
            missing.append(key)
            continue
        r = cost_row[cost_id[key]]
        for i, v in enumerate(levels[:4]):
            uc.Cells(r, cc["Cost_Lv%d" % (i + 1)]).Value = float(v)
        n += 1
    if missing:
        abort("DB에 없는 노드: %s (저장 안 함)" % ", ".join(missing))
    return "Upgrade_Cost %d개 노드 가격 갱신" % n


def main():
    if len(sys.argv) != 4 or sys.argv[1] not in ("strings", "prices"):
        sys.exit(__doc__)
    cmd, target, src = sys.argv[1], os.path.abspath(sys.argv[2]), os.path.abspath(sys.argv[3])
    if not os.path.exists(target):
        abort("대상 파일이 없다: %s" % target)
    check_not_open(target)
    data = json.load(io.open(src, encoding="utf-8"))
    # 같은 파일을 고치므로 쓰기 전에 백업 (BalanceData/db_backup/auto/<이름>_<시각>.xlsx, git 제외)
    backup_dir = os.path.abspath(os.path.join(HERE, "..", "db_backup", "auto"))
    os.makedirs(backup_dir, exist_ok=True)
    stem, ext = os.path.splitext(os.path.basename(target))
    shutil.copyfile(target, os.path.join(backup_dir, "%s_%s%s" % (stem, time.strftime("%Y%m%d_%H%M%S"), ext)))
    import pythoncom
    import win32com.client
    pythoncom.CoInitialize()
    excel = win32com.client.DispatchEx("Excel.Application")    # 사용자의 Excel과 별개로 새로 띄운다
    excel.Visible = False
    excel.DisplayAlerts = False
    wb = None
    try:
        wb = retry(lambda: excel.Workbooks.Open(target))
        msg = add_strings(wb, data) if cmd == "strings" else set_prices(wb, data)
        wb.Save()
        print("%s · 저장: %s" % (msg, target))
    finally:
        if wb is not None:
            wb.Close(False)
        excel.Quit()


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    main()
