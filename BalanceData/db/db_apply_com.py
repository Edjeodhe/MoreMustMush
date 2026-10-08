# -*- coding: utf-8 -*-
"""엑셀 DB(MMM_DB)에 코드 기준 변경을 Excel COM으로 적용한다. (openpyxl로 저장하면 Skima 도형이 사라지므로 COM만 쓴다.)

사용: python BalanceData/db/db_apply_com.py <대상 xlsx>

하는 일 (한 번에 모두 하고 맨 끝에 저장. 중간에 오류가 나면 저장하지 않는다)
  (a) Upgrade · Upgrade_Cost · Info 시트를 갱신 (db_sync.compute()와 같은 결과 = MMM_DB_staged.xlsx와 같은 값)
  (b) Balance_Log 시트에 BalanceData/db/T*.json 의 실험 줄을 쓴다 (db_log.py의 COLS 형식, 같은 Exp_ID는 덮어씀)
  (c) 백업(BalanceData/db_backup/MMM_DB_Ver.02_원본_20261008.xlsx)의 Skima 시트 도형(화살표)을 대상 Skima 시트로 복사
      (대상 Skima에 이미 도형이 있으면 건너뜀)
Excel은 숨김(Visible=False, DisplayAlerts=False)으로 따로 띄우고 끝나면 반드시 Quit.
대상 파일이 다른 Excel에서 열려 있으면 (잠금 파일 ~$ 있음 / 쓰기 열기 실패 / 실행 중인 Excel이 그 파일을 열고 있음) 아무것도 안 하고 중단.
"""
import glob
import io
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import db_sync                      # noqa: E402
import db_log                       # noqa: E402  (COLS 형식)

BACKUP = os.path.abspath(os.path.join(HERE, "..", "db_backup", "MMM_DB_Ver.02_원본_20261008.xlsx"))
MAXLV_COLS = 11                     # Upgrade A~K
DATA_ROW = db_sync.DATA_ROW


def abort(msg):
    sys.stderr.write("중단: %s\n" % msg)
    sys.exit(2)


def check_not_open(path):
    d, name = os.path.split(path)
    lock = os.path.join(d, "~$" + name)
    if os.path.exists(lock):
        abort("잠금 파일이 있다 (%s). 다른 Excel에서 열려 있다." % lock)
    try:
        with open(path, "r+b"):
            pass
    except OSError as e:
        abort("파일을 쓰기로 열 수 없다 (%s). 다른 프로그램이 쓰고 있다." % e)
    try:                            # 이미 실행 중인 Excel이 이 파일을 열고 있는지
        import win32com.client
        app = win32com.client.GetActiveObject("Excel.Application")
        for wb in app.Workbooks:
            if os.path.normcase(os.path.abspath(wb.FullName)) == os.path.normcase(path):
                abort("실행 중인 Excel이 이 파일을 열고 있다.")
    except Exception as e:           # noqa: BLE001  (실행 중인 Excel이 없으면 여기로 옴)
        if isinstance(e, SystemExit):
            raise


def cv(v):
    """COM으로 보낼 값: 큰 정수는 float로 (Excel은 64비트 정수를 못 받는다)."""
    if isinstance(v, bool):
        return int(v)
    if isinstance(v, int) and abs(v) > 2 ** 31 - 1:
        return float(v)
    return v


def block(ws, r0, c0, rows):
    """rows(리스트의 리스트)를 (r0,c0)부터 한 번에 쓴다."""
    if not rows:
        return
    n, m = len(rows), len(rows[0])
    rng = ws.Range(ws.Cells(r0, c0), ws.Cells(r0 + n - 1, c0 + m - 1))
    rng.Value = tuple(tuple(cv(v) for v in row) for row in rows)


def retry(fn, tries=6, wait=0.6):
    import pywintypes
    for i in range(tries):
        try:
            return fn()
        except pywintypes.com_error:
            if i == tries - 1:
                raise
            time.sleep(wait)


def apply_upgrade(wb, R):
    ws = wb.Worksheets("Upgrade")
    wc = wb.Worksheets("Upgrade_Cost")
    rows = R["rows"]
    # K열 헤더 (4레벨)
    for r, v in ((1, "4레벨"), (2, "Efx_Lv4"), (3, "float")):
        ws.Cells(r, 11).Value = v
    ws.Columns(11).ColumnWidth = ws.Columns(10).ColumnWidth
    last = max(R["old_max_row"], DATA_ROW + len(rows) - 1)
    ws.Range(ws.Cells(DATA_ROW, 1), ws.Cells(last, MAXLV_COLS)).ClearContents()
    block(ws, DATA_ROW, 1, [[x["id"], x["name"], x["str"], x["pre"], x["type"], x["max"], x["cost"]] + list(x["lv"])
                            for x in rows])
    # Upgrade_Cost E열 (4레벨)
    for r, v in ((1, "4레벨"), (2, "Cost_Lv4"), (3, "int")):
        wc.Cells(r, 5).Value = v
    wc.Columns(5).ColumnWidth = 12
    ids = R["used_costs"]
    last = max(R["old_cost_max_row"], DATA_ROW + len(ids) - 1)
    wc.Range(wc.Cells(DATA_ROW, 1), wc.Cells(last, 5)).ClearContents()
    block(wc, DATA_ROW, 1, [[cid] + [int(t) for t in R["final_cost"][cid]] for cid in ids])
    # Info 문구
    info = wb.Worksheets("Info")
    for coord, old, new in R["info_changes"]:
        info.Range(coord).Value = "'" + new


def apply_log(excel, wb):
    keys = [k for _, k, _ in db_log.COLS]
    recs = []
    for f in sorted(glob.glob(os.path.join(HERE, "T*.json"))):
        recs.append(json.load(io.open(f, encoding="utf-8")))
    names = [s.Name for s in wb.Worksheets]
    if db_log.SHEET in names:
        ws = wb.Worksheets(db_log.SHEET)
    else:
        ws = wb.Worksheets.Add(After=wb.Worksheets(wb.Worksheets.Count))
        ws.Name = db_log.SHEET
        for c, (kr, key, typ) in enumerate(db_log.COLS, 1):
            for r, v in enumerate((kr, key, typ), 1):
                ws.Cells(r, c).Value = "'" + v
            ws.Range(ws.Cells(1, c), ws.Cells(3, c)).Font.Bold = True
            ws.Range(ws.Cells(1, c), ws.Cells(2, c)).Interior.Color = 221 + 235 * 256 + 247 * 65536
            ws.Columns(c).ColumnWidth = max(10, min(40, len(kr) * 2 + 4))
        try:
            ws.Activate()
            excel.ActiveWindow.FreezePanes = False
            ws.Range("B4").Select()
            excel.ActiveWindow.FreezePanes = True
        except Exception:            # noqa: BLE001
            pass
    written = []
    for rec in recs:
        last = ws.Cells(ws.Rows.Count, 1).End(-4162).Row            # xlUp
        row = None
        for r in range(DATA_ROW, last + 1):
            if ws.Cells(r, 1).Value == rec["Exp_ID"]:
                row = r
                break
        if row is None:
            row = max(last + 1, DATA_ROW)
        vals = []
        for k in keys:
            v = rec.get(k, 0)
            v = 0 if v is None else v
            vals.append("'" + v if isinstance(v, str) else v)       # '7/7' 같은 글자가 날짜로 바뀌지 않게
        block(ws, row, 1, [vals])
        written.append((rec["Exp_ID"], row))
    return written


def restore_shapes(excel, wb):
    """백업 Skima 시트의 도형을 대상 Skima 시트로 복사 (위치·크기 그대로)."""
    dst = wb.Worksheets("Skima")
    if dst.Shapes.Count > 0:
        return "건너뜀 (대상 Skima에 도형 %d개가 이미 있다)" % dst.Shapes.Count
    if not os.path.exists(BACKUP):
        return "건너뜀 (백업 파일이 없다)"
    src_wb = excel.Workbooks.Open(BACKUP, 0, True)                  # UpdateLinks=0, ReadOnly
    try:
        src = src_wb.Worksheets("Skima")
        n = src.Shapes.Count
        for i in range(1, n + 1):
            s = src.Shapes(i)
            geo = (s.Left, s.Top, s.Width, s.Height)
            top_left = s.TopLeftCell.Address
            retry(lambda: s.Copy())
            retry(lambda: dst.Paste(dst.Range(top_left)))
            p = dst.Shapes(dst.Shapes.Count)
            p.Left, p.Top = geo[0], geo[1]
            if abs(p.Width - geo[2]) > 0.01:
                p.Width = geo[2]
            if abs(p.Height - geo[3]) > 0.01:
                p.Height = geo[3]
            p.Name = s.Name
        return "도형 %d개 복사" % n
    finally:
        src_wb.Close(False)


def main(target):
    target = os.path.abspath(target)
    if not os.path.exists(target):
        abort("대상 파일이 없다: %s" % target)
    check_not_open(target)
    R = db_sync.compute(target)       # 읽기만 한다 (openpyxl, 저장 안 함)
    import win32com.client
    excel = win32com.client.DispatchEx("Excel.Application")        # 사용자의 Excel과 별개로 새로 띄운다
    try:
        excel.Visible = False
        excel.DisplayAlerts = False
        excel.EnableEvents = False
        wb = excel.Workbooks.Open(target, 0, False)
        try:
            apply_upgrade(wb, R)
            print("Upgrade %d행 · Upgrade_Cost %d행 · Info %d칸 갱신" % (len(R["rows"]), len(R["used_costs"]), len(R["info_changes"])))
            print("Balance_Log:", apply_log(excel, wb))
            print("Skima 도형:", restore_shapes(excel, wb))
            wb.Worksheets("Upgrade").Activate()
            wb.Save()
            print("저장:", target)
        finally:
            wb.Close(False)
    finally:
        excel.Quit()
    with io.open(db_sync.CODE_MAP, "w", encoding="utf-8") as f:
        json.dump({n["id"]: R["assign"][n["id"]] for n in R["nodes"]}, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
