# DbImport.cs(Unity 메뉴 MoreMush/DB/가져오기)를 그대로 옮긴 것: xlsx → db.json. Unity를 열 수 없을 때만 쓴다.
import sys, zipfile, re, math, xml.etree.ElementTree as ET
NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
RNS = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
SHEETS = ["Upgrade", "Upgrade_Cost", "Skill", "Ball", "Ball_Efx", "Weader", "Boss", "Const", "String"]

def col_index(ref):
    n = 0
    for ch in ref:
        if not ("A" <= ch <= "Z"): break
        n = n * 26 + (ord(ch) - 64)
    return n - 1

def text_of(el, skip_rph):
    out = []
    def walk(e, parent):
        if e.tag == NS + "t" and not (skip_rph and parent is not None and parent.tag == NS + "rPh"):
            out.append(e.text or "")
        for c in e: walk(c, e)
    walk(el, None)
    return "".join(out)

def read(path):
    z = zipfile.ZipFile(path)
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        shared = [text_of(si, True) for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall(NS + "si")]
    rels = {r.get("Id"): r.get("Target") for r in ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))}
    book = {}
    for s in ET.fromstring(z.read("xl/workbook.xml")).iter(NS + "sheet"):
        t = rels.get(s.get(RNS + "id"))
        if t is None: continue
        entry = t[1:] if t.startswith("/") else "xl/" + t
        if entry in z.namelist(): book[s.get("name")] = sheet(ET.fromstring(z.read(entry)), shared)
    return book

def sheet(root, shared):
    rows = []
    data = root.find(NS + "sheetData")
    if data is None: return rows
    for row in data.findall(NS + "row"):
        idx = int(row.get("r")) - 1 if row.get("r") else len(rows)
        while len(rows) < idx: rows.append([])
        cur = []
        for c in row.findall(NS + "c"):
            col = col_index(c.get("r")) if c.get("r") else len(cur)
            t = c.get("t")
            if t == "inlineStr": val = text_of(c, False)
            else:
                v = c.find(NS + "v"); val = v.text if v is not None else None
                if val is None and v is not None: val = ""
            if t == "s" and val is not None: val = shared[int(val)]
            while len(cur) < col: cur.append("")
            cur.append(val if val is not None else "")
        rows.append(cur)
    return rows

def num(d):
    # Newtonsoft double: 왕복 형식, 정수면 ".0", 지수는 대문자 E
    if d == math.floor(d) and abs(d) < 1e15: return repr(float(d))
    s = repr(d)
    if "e" in s:
        m, e = s.split("e"); ex = int(e)
        s = f"{m}E{'-' if ex < 0 else '+'}{abs(ex):02d}"
    return s

def jstr(s):
    B = chr(92)
    esc = {'"': B + '"', B: B + B, chr(10): B + "n", chr(13): B + "r", chr(9): B + "t", chr(8): B + "b", chr(12): B + "f"}
    out = ['"']
    for ch in s:
        if ch in esc: out.append(esc[ch])
        elif ord(ch) < 0x20: out.append(B + "u%04x" % ord(ch))
        else: out.append(ch)
    out.append('"'); return "".join(out)

def table(name, rows, errors):
    keys, types = rows[1], rows[2]
    res = []
    for r in range(3, len(rows)):
        row = rows[r]
        if len(row) == 0 or row[0] == "": continue
        o = []
        for c, k in enumerate(keys):
            if not k: continue
            v = row[c] if c < len(row) else ""
            t = (types[c] if c < len(types) else "string").strip().lower()
            if t in ("int", "float"):
                if v == "": errors.append(f"{name} {r+1}행 {k}: 빈 칸"); continue
                d = float(v)
                if t == "int":
                    if d != math.floor(d): errors.append(f"{name} {r+1}행 {k}: 정수 아님")
                    o.append((k, str(int(d))))
                else: o.append((k, num(d)))
            else: o.append((k, jstr(v)))
        res.append(o)
    return res

def dump(root, nl):
    L = ["{"]
    items = list(root.items())
    for i, (k, v) in enumerate(items):
        comma = "," if i < len(items) - 1 else ""
        if isinstance(v, str): L.append(f"  {jstr(k)}: {v}{comma}"); continue
        if not v: L.append(f"  {jstr(k)}: []{comma}"); continue
        L.append(f"  {jstr(k)}: [")
        for j, o in enumerate(v):
            L.append("    {")
            for m, (kk, vv) in enumerate(o):
                L.append(f"      {jstr(kk)}: {vv}{',' if m < len(o) - 1 else ''}")
            L.append("    }" + ("," if j < len(v) - 1 else ""))
        L.append(f"  ]{comma}")
    L.append("}")
    return nl.join(L)

if __name__ == "__main__":
    src, out, name = sys.argv[1], sys.argv[2], sys.argv[3]
    book = read(src); errors = []
    root = {"_source": jstr(name)}
    for s in SHEETS: root[s] = table(s, book[s], errors)
    if errors: print("\n".join(errors)); sys.exit(1)
    open(out, "w", encoding="utf-8", newline="").write(dump(root, "\n"))
