using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // 엑셀 DB(Assets/DB/MMM_DB_Ver.NN.xlsx 중 가장 높은 버전)를 읽어 게임이 쓰는 Assets/Resources/DB/db.json으로 바꾼다.
    // 시트 규칙: 1행 한글 이름 · 2행 영문 키 · 3행 타입(int/float/string/enum) · 4행부터 데이터. 첫 칸이 빈 행은 건너뛴다.
    // 엑셀을 저장하면 자동으로 다시 만든다 (DbImportWatcher). 외부 라이브러리 없이 xlsx(zip + XML)를 직접 읽는다.
    public static class DbImport
    {
        public const string DbDir = "Assets/DB", OutPath = "Assets/Resources/DB/db.json";
        // 게임이 읽는 시트. 나머지(Info·Skima·Balance_Log·Resource·Cook·state)는 사람용 문서라 넣지 않는다
        public static readonly string[] Sheets = { "Upgrade", "Upgrade_Cost", "Skill", "Ball", "Ball_Efx", "Weader", "Boss", "Const", "String" };
        const int KeyRow = 1, TypeRow = 2, DataRow = 3;   // 0부터 센 행 번호

        // 시트 이름 → 게임이 읽는 행 형식 (Db.cs). 형식의 필드 = 꼭 있어야 하는 열(2행 영문 키)
        static readonly Dictionary<string, Type> SheetTypes = new Dictionary<string, Type>
        {
            ["Upgrade"] = typeof(Db.Upgrade), ["Upgrade_Cost"] = typeof(Db.UpgradeCost), ["Skill"] = typeof(Db.SkillRow), ["Ball"] = typeof(Db.Ball),
            ["Ball_Efx"] = typeof(Db.BallEfx), ["Weader"] = typeof(Db.Weader), ["Boss"] = typeof(Db.Boss), ["Const"] = typeof(Db.Const), ["String"] = typeof(Db.Str),
        };
        static readonly System.Text.RegularExpressions.Regex VerName = new System.Text.RegularExpressions.Regex(@"^MMM_DB_Ver\.(\d+)\.xlsx$");

        // Assets/DB에서 버전 번호가 가장 큰 MMM_DB_Ver.NN.xlsx (사본·잠금 파일 제외, 번호는 숫자로 비교)
        public static string LatestDb()
        {
            if (!Directory.Exists(DbDir)) return null;
            return Directory.GetFiles(DbDir, "*.xlsx")
                .Select(p => (p, m: VerName.Match(Path.GetFileName(p))))
                .Where(x => x.m.Success)
                .OrderBy(x => int.Parse(x.m.Groups[1].Value, CultureInfo.InvariantCulture))
                .Select(x => x.p.Replace('\\', '/')).LastOrDefault();
        }

        [MenuItem("MoreMush/DB/가져오기 (엑셀 → db.json)")]
        public static string Import()
        {
            string path = LatestDb();
            if (path == null) { Debug.LogError("[DB] Assets/DB/MMM_DB_Ver.NN.xlsx가 없어요"); return "DB 없음"; }
            Dictionary<string, List<string[]>> book;
            try { book = XlsxReader.Read(path); }
            catch (Exception e) { Debug.LogError($"[DB] {path}를 읽지 못했어요 (엑셀에서 저장 중이면 잠시 뒤 메뉴로 다시): {e.GetType().Name} {e.Message}"); return "읽기 실패"; }
            var root = new Dictionary<string, object> { ["_source"] = Path.GetFileName(path) };
            var errors = new List<string>();
            foreach (var name in Sheets)
            {
                if (!book.TryGetValue(name, out var rows)) { errors.Add($"시트 없음: {name}"); continue; }
                root[name] = Table(name, rows, errors);
            }
            if (errors.Count > 0) { Debug.LogError("[DB] 가져오기 실패, db.json을 바꾸지 않았어요:\n" + string.Join("\n", errors)); return "오류 " + errors.Count; }
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            string json = JsonConvert.SerializeObject(root, Newtonsoft.Json.Formatting.Indented);
            if (File.Exists(OutPath) && File.ReadAllText(OutPath) == json) return $"{path} → 변경 없음";
            File.WriteAllText(OutPath, json);
            AssetDatabase.ImportAsset(OutPath);
            // 게임 표(Defs)는 정적 생성자에서 한 번 만들어지므로, 플레이 중이 아니면 스크립트를 다시 불러와 새 값으로 다시 만든다.
            // 플레이 중에는 Db도 그대로 둔다 (번역만 새것이 되어 데이터와 어긋나지 않게)
            // 배치 모드(-executeMethod)는 Defs를 만들기 전에 부르므로 다시 읽기만 한다
            if (!EditorApplication.isPlayingOrWillChangePlaymode) { Db.Reload(); if (!Application.isBatchMode) EditorUtility.RequestScriptReload(); }
            string msg = $"[DB] {Path.GetFileName(path)} → {OutPath} (" + string.Join(", ", Sheets.Select(s => $"{s} {((List<Dictionary<string, object>>)root[s]).Count}")) + ")";
            Debug.Log(msg + (EditorApplication.isPlaying ? "\n플레이 중이라 게임 데이터는 플레이를 끝낸 뒤부터 바뀌어요" : ""));
            return msg;
        }

        // 지금 db.json의 _source (파일이 없거나 읽지 못하면 null). 엑셀에서 만든 db.json은 "MMM_DB_Ver.NN.xlsx" 그대로다
        public static string CurrentSource()
        {
            if (!File.Exists(OutPath)) return null;
            try
            {
                using (var r = new JsonTextReader(new StreamReader(OutPath)))
                    while (r.Read())
                        if (r.TokenType == JsonToken.PropertyName && (string)r.Value == "_source" && r.Depth == 1) return r.ReadAsString();
            }
            catch (Exception e) { Debug.LogWarning($"[DB] {OutPath}의 _source를 읽지 못했어요: {e.Message}"); }
            return null;
        }

        // 엑셀이 아니라 도구(밸런스 스크립트 등)로 만든 db.json인가: _source가 엑셀 파일 이름 하나가 아니다
        public static bool MadeByTool(string source) => File.Exists(OutPath) && (source == null || !VerName.IsMatch(source));

        static List<Dictionary<string, object>> Table(string sheet, List<string[]> rows, List<string> errors)
        {
            var list = new List<Dictionary<string, object>>();
            if (rows.Count <= DataRow) { errors.Add($"{sheet}: 머리 3행이 없음"); return list; }
            string[] keys = rows[KeyRow], types = rows[TypeRow];
            foreach (var f in SheetTypes[sheet].GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (Array.IndexOf(keys, f.Name) < 0) errors.Add($"{sheet}: 2행에 {f.Name} 열이 없음 (영문 키 오타·삭제)");
            for (int r = DataRow; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length == 0 || string.IsNullOrEmpty(row[0])) continue;
                var o = new Dictionary<string, object>();
                for (int c = 0; c < keys.Length; c++)
                {
                    string k = keys[c];
                    if (string.IsNullOrEmpty(k)) continue;
                    string v = c < row.Length ? row[c] ?? "" : "";
                    string t = c < types.Length ? (types[c] ?? "").Trim().ToLowerInvariant() : "string";
                    if (t == "int" || t == "float")
                    {
                        // 빈 숫자 칸은 오류: 0으로 넣으면 배율·가격·체력이 조용히 0이 된다 (0이 맞으면 0을 적을 것)
                        if (v == "") { errors.Add($"{sheet} {r + 1}행 {k}: 빈 칸 (숫자 열은 0이라도 적어 주세요)"); continue; }
                        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) { errors.Add($"{sheet} {r + 1}행 {k}: 숫자가 아님 '{v}'"); continue; }
                        if (t == "int") { if (d != Math.Floor(d)) errors.Add($"{sheet} {r + 1}행 {k}: 정수가 아님 '{v}'"); o[k] = (long)d; }
                        else o[k] = d;
                    }
                    else o[k] = v;
                }
                list.Add(o);
            }
            return list;
        }
    }

    // 엑셀 파일이 저장되면 db.json을 다시 만든다.
    // 단, 지금 db.json이 도구로 만든 것(_source가 엑셀 파일 이름이 아님)이면 덮지 않고 경고만 띄운다:
    // 브랜치를 바꾸거나 받을 때 엑셀이 다시 임포트되면 엑셀보다 앞선 db.json이 옛 엑셀 값으로 덮여 Defs가 죽었다 (2026-10-26 KHD).
    // 엑셀 값으로 덮어야 하면 메뉴 MoreMush/DB/가져오기를 직접 실행한다
    public class DbImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Any(p => p.StartsWith(DbImport.DbDir + "/") && p.EndsWith(".xlsx") && !Path.GetFileName(p).StartsWith("~$"))) return;
            EditorApplication.delayCall += () =>
            {
                string src = DbImport.CurrentSource();
                if (DbImport.MadeByTool(src))
                {
                    Debug.LogWarning($"[DB] 엑셀이 다시 임포트됐지만 db.json을 덮지 않았어요: 지금 db.json은 엑셀이 아니라 도구로 만든 것이에요 (_source: {src ?? "없음"}).\n" +
                        $"엑셀({Path.GetFileName(DbImport.LatestDb() ?? "없음")}) 값으로 바꾸려면 메뉴 MoreMush/DB/가져오기를 실행하세요");
                    return;
                }
                DbImport.Import();
            };
        }
    }

    // 최소 xlsx 읽기: 시트 이름 → 행 목록(칸 문자열). 숫자는 엑셀이 저장한 글자 그대로(불변 문화권), 날짜·수식 결과도 저장된 값
    public static class XlsxReader
    {
        static readonly XNamespace X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public static Dictionary<string, List<string[]>> Read(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var shared = SharedStrings(zip);
            var rels = new Dictionary<string, string>();
            using (var r = XmlReader.Create(zip.GetEntry("xl/_rels/workbook.xml.rels").Open()))
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.LocalName == "Relationship")
                        rels[r.GetAttribute("Id")] = r.GetAttribute("Target");
            var book = new Dictionary<string, List<string[]>>();
            using (var r = XmlReader.Create(zip.GetEntry("xl/workbook.xml").Open()))
                while (r.Read())
                {
                    if (r.NodeType != XmlNodeType.Element || r.LocalName != "sheet") continue;
                    string name = r.GetAttribute("name");
                    string rid = r.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                    if (rid == null || !rels.TryGetValue(rid, out var target)) continue;
                    string entry = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
                    var e = zip.GetEntry(entry);
                    if (e != null) book[name] = Sheet(e, shared);
                }
            return book;
        }

        static XDocument Load(ZipArchiveEntry e)
        {
            using var st = e.Open();
            return XDocument.Load(st, LoadOptions.PreserveWhitespace);
        }

        // 공유 문자열: <si> 하나 = 문자열 하나. 서식 조각(<r><t>)은 이어 붙이고, 후리가나(<rPh>)는 뺀다
        static List<string> SharedStrings(ZipArchive zip)
        {
            var e = zip.GetEntry("xl/sharedStrings.xml");
            if (e == null) return new List<string>();
            return Load(e).Root.Elements(X + "si")
                .Select(si => string.Concat(si.Descendants(X + "t").Where(t => t.Parent.Name != X + "rPh").Select(t => t.Value)))
                .ToList();
        }

        static List<string[]> Sheet(ZipArchiveEntry entry, List<string> shared)
        {
            var rows = new List<string[]>();
            var data = Load(entry).Root.Element(X + "sheetData");
            if (data == null) return rows;
            foreach (var row in data.Elements(X + "row"))
            {
                var ra = row.Attribute("r");
                int idx = ra != null ? int.Parse(ra.Value, CultureInfo.InvariantCulture) - 1 : rows.Count;
                while (rows.Count < idx) rows.Add(new string[0]);
                var cur = new List<string>();
                foreach (var c in row.Elements(X + "c"))
                {
                    var ca = c.Attribute("r");
                    int col = ca != null ? ColIndex(ca.Value) : cur.Count;
                    string t = (string)c.Attribute("t");
                    string val = t == "inlineStr" ? string.Concat(c.Descendants(X + "t").Select(x => x.Value)) : c.Element(X + "v")?.Value;
                    if (t == "s" && val != null)
                    {
                        int si = int.Parse(val, CultureInfo.InvariantCulture);
                        if (si < 0 || si >= shared.Count) throw new InvalidDataException($"공유 문자열 번호 {si}가 범위 밖 (시트 {entry.FullName}, 칸 {ca?.Value})");
                        val = shared[si];
                    }
                    while (cur.Count < col) cur.Add("");
                    cur.Add(val ?? "");
                }
                rows.Add(cur.ToArray());
            }
            return rows;
        }

        // "AB12" → 27 (0부터)
        static int ColIndex(string cellRef)
        {
            int n = 0;
            foreach (char ch in cellRef) { if (ch < 'A' || ch > 'Z') break; n = n * 26 + (ch - 'A' + 1); }
            return n - 1;
        }
    }
}
