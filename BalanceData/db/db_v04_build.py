# -*- coding: utf-8 -*-
"""엑셀 DB Ver.04를 만든다: Ver.03을 복사한 뒤 지금 게임 데이터(ver04_source.json, Unity 메뉴 MoreMush/DB/Ver.04용 데이터 내보내기)로
Upgrade · Upgrade_Cost · Ball · Ball_Efx · Weader · String 시트를 다시 쓰고 Skill · Boss · Const 시트를 더한다. Info에 규칙을 덧붙인다.
쓰기는 Excel COM으로만 한다 (openpyxl 저장은 Skima 도형을 지운다). 그 뒤로는 Ver.04 엑셀이 게임 데이터의 원본이다.

사용: python BalanceData/db/db_v04_build.py <Ver.03 xlsx> <Ver.04 xlsx>
"""
import io
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from db_apply_com import abort, block, check_not_open, retry  # noqa: E402

SRC = os.path.join(HERE, "ver04_source.json")
DATA_ROW = 4
PREFIX_STR = lambda g: int(str(g)[0] + "0" + str(g)[1:])  # noqa: E731  Str_Group_ID 21002 -> Str_ID 201002 (Info 규칙)


def header(ws, cols):
    """cols = [(한글, 영문 키, 타입)] -> 1~3행. 진하게·연한 색."""
    block(ws, 1, 1, [[c[0] for c in cols], [c[1] for c in cols], [c[2] for c in cols]])
    rng = ws.Range(ws.Cells(1, 1), ws.Cells(3, len(cols)))
    rng.Font.Bold = True
    ws.Range(ws.Cells(1, 1), ws.Cells(2, len(cols))).Interior.Color = 221 + 235 * 256 + 247 * 65536


def sheet(wb, name, after=None):
    names = [s.Name for s in wb.Worksheets]
    if name in names:
        ws = wb.Worksheets(name)
        ws.Cells.ClearContents()
        ws.Cells.Interior.ColorIndex = -4142   # xlNone
        return ws
    ws = wb.Worksheets.Add(After=wb.Worksheets(after or wb.Worksheets.Count))
    ws.Name = name
    return ws


def table(wb, name, cols, rows, after=None):
    ws = sheet(wb, name, after)
    header(ws, cols)
    if rows:
        block(ws, DATA_ROW, 1, rows)
    for c in range(1, len(cols) + 1):
        ws.Columns(c).AutoFit()
        if ws.Columns(c).ColumnWidth > 60:
            ws.Columns(c).ColumnWidth = 60
    return ws


def old_types(wb):
    """Ver.03 Upgrade의 Upg_ID -> Type (state 타입) — 새 표에 그대로 옮긴다."""
    ws = wb.Worksheets("Upgrade")
    out = {}
    r = DATA_ROW
    while True:
        v = ws.Cells(r, 1).Value
        if v is None:
            break
        t = ws.Cells(r, 5).Value
        out[int(v)] = t if t not in (None, 0, 0.0) else ""
        r += 1
    return out


def build(wb, d):
    nodes = d["nodes"]
    types = old_types(wb)
    key_id = {n["key"]: n["id"] for n in nodes}
    group = {n["key"]: n["id"] + 10000 for n in nodes}          # 노드 문구 그룹 = Upg_ID + 10000 (Ver.03 규칙)

    # ── Upgrade · Upgrade_Cost ──
    cols = [("ID", "Upg_ID", "int"), ("키", "Upg_Key", "string"), ("이름", "Upg_Name", "string"), ("설명 그룹", "Str_Group_ID", "int"),
            ("선행(부모)", "Upg_Pre", "int"), ("최대 레벨 선행", "Upg_Req", "int"), ("가지", "Branch", "string"), ("타입", "Type", "string"),
            ("최대 레벨", "Efx_MaxLv", "int"), ("비용", "Cost_ID", "int"), ("단계", "Tier", "int"), ("작은 노드", "Sub", "int"),
            ("스킬", "Skill", "string"), ("아이콘", "Icon", "string"), ("색", "Color", "string")] + \
           [("%d레벨" % L, "Efx_Lv%d" % L, "float") for L in range(5)]
    rows = []
    for i, n in enumerate(nodes):
        rows.append([n["id"], n["key"], n["name"], group[n["key"]], key_id.get(n["parent"], 0) if n["parent"] else 0,
                     key_id.get(n["req"], 0) if n["req"] else 0, n["br"], types.get(n["id"], ""), n["max"], 90001 + i, n["tier"],
                     1 if n["sub"] else 0, n["skill"] or "", n["icon"] or "", n["col"] or ""] + list(n["efx"]))
    table(wb, "Upgrade", cols, rows)
    cols = [("ID", "Cost_ID", "int")] + [("%d레벨 골드" % L, "Cost_Lv%d" % L, "float") for L in range(1, 5)] + \
           [("%d레벨 균사석" % L, "Gem_Lv%d" % L, "float") for L in range(1, 5)]
    table(wb, "Upgrade_Cost", cols, [[90001 + i] + list(n["gold"]) + list(n["gem"]) for i, n in enumerate(nodes)])

    # ── Skill (새 시트): 스킬 노드의 동작 정보. 가격·레벨·트리 위치는 Upgrade에 ──
    cols = [("키", "Skill_Key", "string"), ("이름", "Skill_Name", "string"), ("발동", "Kind", "string"), ("색", "Color", "string"),
            ("아이콘", "Icon", "string"), ("발동률 없음", "No_Rate", "int")]
    table(wb, "Skill", cols, [[s["key"], s["name"], s["kind"], s["col"], s["icon"], 1 if s["noRate"] else 0] for s in d["skills"]], "Upgrade_Cost")

    # ── Ball · Ball_Efx (수확기) ──
    cols = [("ID", "Ball_ID", "int"), ("키", "Ball_Key", "string"), ("이름", "Ball_Name", "string"), ("문구 그룹", "Ball_Str", "int"),
            ("유형", "Ball_Type", "string"), ("생김새", "Ball_Look", "string"), ("해금 골드", "Ball_Unlock", "float"),
            ("레벨 골드", "Ball_LvGold", "float"), ("능력치", "Ball_Efx_ID", "int")]
    hv = d["harvesters"]
    table(wb, "Ball", cols, [[1001 + i, h["key"], h["name"], 10001 + i, h["type"], h["look"], h["unlock"], h["lvGold"], 2001 + i]
                             for i, h in enumerate(hv)])
    cols = [("ID", "Ball_Efx_ID", "int"), ("공격력", "Atk", "float"), ("속도", "Spd", "float"), ("크기", "Size", "float"), ("치명타 확률 +", "Cri", "float")]
    table(wb, "Ball_Efx", cols, [[2001 + i, h["atk"], h["spd"], h["size"], h["crit"]] for i, h in enumerate(hv)])

    # ── Weader (날씨) ──
    cols = [("ID", "Wea_ID", "int"), ("키", "Wea_Key", "string"), ("이름", "Wea_Name", "string"), ("설명 그룹", "Str_Group_ID", "int"),
            ("등장 가중치", "Wea_Rate", "int"), ("날씨 유형", "Weader_Type", "int"), ("아이콘", "Icon", "string"),
            ("선호 배율", "Fav_Mul", "float"), ("선호 등급", "Fav_Tier", "int"), ("선호 버섯", "Fav", "string")]
    ws_ = d["weathers"]
    table(wb, "Weader", cols, [[5001 + i, w["key"], w["name"], 28001 + i, 1, 1 if w["good"] else 0, w["icon"], w["favMul"], w["favTier"], w["fav"]]
                               for i, w in enumerate(ws_)])

    # ── Boss (새 시트) ──
    cols = [("ID", "Boss_ID", "int"), ("키", "Boss_Key", "string"), ("이름", "Boss_Name", "string"), ("기믹 문구 그룹", "Str_Group_ID", "int"),
            ("계열", "Cat", "string"), ("그림 버섯", "Look", "string"), ("색", "Color", "string"), ("체력 배율", "Hp_Mul", "float")]
    bs = d["bosses"]
    table(wb, "Boss", cols, [[6001 + i, b["key"], b["name"], 29001 + i, b["cat"], b["look"], b["col"], b["hpMul"]] for i, b in enumerate(bs)], "Weader")

    # ── Const (새 시트): 밸런스 상수 ──
    cols = [("키", "Const_Key", "string"), ("값", "Value", "float"), ("설명", "Desc", "string")]
    table(wb, "Const", cols, [[c["key"], c["value"], c["desc"]] for c in d["consts"]], "Boss")

    # ── String: 게임의 모든 한국어 문구. 한국어가 키, 영어·일본어는 번역 (빈 칸 = 번역 전) ──
    ctx = {}
    for n in nodes:
        ctx.setdefault(n["desc"], (group[n["key"]], "upg:%s:desc" % n["key"]))
        ctx.setdefault(n["name"], (group[n["key"]], "upg:%s:name" % n["key"]))
    for i, h in enumerate(hv):
        for k in ("name", "type", "look"):
            ctx.setdefault(h[k], (10001 + i, "ball:%s:%s" % (h["key"], k)))
    for i, w in enumerate(ws_):
        ctx.setdefault(w["desc"], (28001 + i, "wea:%s:desc" % w["key"]))
        ctx.setdefault(w["name"], (28001 + i, "wea:%s:name" % w["key"]))
    for i, b in enumerate(bs):
        ctx.setdefault(b["gimmick"], (29001 + i, "boss:%s:gimmick" % b["key"]))
        ctx.setdefault(b["name"], (29001 + i, "boss:%s:name" % b["key"]))
    rows, used, ui = [], set(), 0
    for s in d["strings"]:
        if s in ctx:
            g, name = ctx[s]
        else:
            ui += 1
            g, name = 90000 + ui, "ui"
        sid = PREFIX_STR(g)
        while sid in used:            # 같은 그룹의 둘째 문구(이름·설명)
            sid += 100000 if g < 90000 else 1
        used.add(sid)
        rows.append([sid, g, name, "'" + s if s[:1] in "=+-@'" else s, "", ""])
    cols = [("ID", "Str_ID", "int"), ("그룹ID", "Str_Group_ID", "int"), ("이름", "Str_Name", "string"),
            ("내용_KR", "Str_KR", "string"), ("내용_EN", "Str_EN", "string"), ("내용_JA", "Str_JA", "string")]
    ws = sheet(wb, "String")
    ws.Columns(4).NumberFormat = "@"
    ws.Columns(5).NumberFormat = "@"
    ws.Columns(6).NumberFormat = "@"
    header(ws, cols)
    block(ws, DATA_ROW, 1, rows)
    ws.Columns(4).ColumnWidth = 70

    # ── Info: 새 규칙 ──
    info = wb.Worksheets("Info")
    last = info.Cells(info.Rows.Count, 2).End(-4162).Row
    notes = [
        "[Ver.04 규칙 — 게임이 이 파일을 직접 읽는다 (2026-10-13)]",
        "Unity: Assets/DB/MMM_DB_Ver.04.xlsx 저장 → Unity가 자동으로 Assets/Resources/DB/db.json을 다시 만든다 (메뉴 MoreMush/DB/가져오기)",
        "각 시트: 1행 한글 이름 · 2행 영문 키(코드가 읽음, 바꾸지 말 것) · 3행 타입 · 4행부터 데이터. 행 순서 = 게임 안 순서",
        "Upgrade: Upg_Key가 코드 이름. 새 노드는 기존 키를 쓰는 효과만 동작한다(새 효과는 코드 필요). Upg_Pre = 부모 Upg_ID(0 = 시작 노드)",
        "Upgrade: Upg_Req = 최대 레벨까지 올려야 열리는 선행 Upg_ID(0 = 없음). Efx_Lv0~4 = 그 레벨일 때 값(0레벨 = 기본값)",
        "Upgrade_Cost: Cost_Lv1~4 = 1~4레벨을 살 때 골드, Gem_Lv1~4 = 균사석",
        "Skill: 스킬 노드의 발동 종류·색. 스킬 노드 레벨: 1 해금 · 2 발동률 · 3 위력 · 4 둘 다 (발동률 없는 스킬은 2~4 위력)",
        "Ball·Ball_Efx: 수확기. 고유 능력(휘두르기 등)은 코드",
        "Weader: Weader_Type 1 = 일반 채집 날씨, 0 = 보스 채집 날씨. 같은 유형끼리 Wea_Rate 가중치로 뽑는다",
        "Boss: 순서대로 10·20·30… 스테이지 보스. Const: 밸런스 상수",
        "String: 게임 안 모든 문구. Str_KR = 키(코드·데이터의 한국어와 같아야 함), Str_EN·Str_JA = 번역(빈 칸이면 한국어로 보임)",
        "String: 새 문구는 Unity 메뉴 MoreMush/다국어/3 번역표 갱신이 맨 아래에 추가한다(Str_Name = ui). 그룹 9nnnn = UI 문구",
    ]
    for i, t in enumerate(notes):
        info.Cells(last + 2 + i, 2).Value = "'" + t
    return len(rows)


def main():
    if len(sys.argv) != 3:
        abort("사용: python db_v04_build.py <Ver.03 xlsx> <Ver.04 xlsx>")
    src, dst = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    if os.path.exists(dst):
        abort("이미 있다: %s (지우고 다시 실행)" % dst)
    check_not_open(src)
    d = json.load(io.open(SRC, encoding="utf-8"))
    shutil.copyfile(src, dst)
    import pythoncom
    import win32com.client
    pythoncom.CoInitialize()
    excel = win32com.client.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    wb = None
    try:
        wb = retry(lambda: excel.Workbooks.Open(dst))
        n = build(wb, d)
        wb.Worksheets("Info").Activate()
        wb.Save()
        print("완료: %s (문자열 %d줄)" % (dst, n))
    except Exception:
        if wb is not None:
            wb.Close(SaveChanges=False)
            wb = None
        os.remove(dst)
        raise
    finally:
        if wb is not None:
            wb.Close(SaveChanges=False)
        excel.Quit()


if __name__ == "__main__":
    main()
