using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace MoreMush
{
    // 엑셀 DB(Assets/DB/MMM_DB_Ver.NN.xlsx)를 에디터가 바꿔 둔 Assets/Resources/DB/db.json을 읽는다 (Assets/Editor/DbImport.cs).
    // 필드 이름 = 엑셀 2행의 영문 키. 노드·스킬·수확기·날씨·보스·상수·문구가 모두 여기서 온다 (Defs가 시작할 때 한 번 읽는다).
    public static class Db
    {
        public const string ResPath = "DB/db";
#if UNITY_EDITOR
        // 에디터는 파일을 바로 읽는다 (배치 모드·정적 생성자에서도, 가져오기 직후 값으로). 메인 스레드에서 쓴다
        const string EditorPath = "Assets/Resources/DB/db.json";
#endif

        public sealed class Upgrade
        {
            public int Upg_ID, Str_Group_ID, Upg_Pre, Upg_Req, Efx_MaxLv, Cost_ID, Tier, Sub;
            public string Upg_Key, Upg_Name, Branch, Type, Skill, Icon, Color;
            // 균사 트리 A안: Line = 같은 스킬(줄) 키 (줄의 행들은 앞 행을 최대로 올려야 다음 행이 열린다) · Pos_X·Pos_Y = 트리 네모 칸 좌표 (가운데 0,0, y 아래 +)
            public string Line;
            public int Pos_X, Pos_Y;
            public double Efx_Lv0, Efx_Lv1, Efx_Lv2, Efx_Lv3, Efx_Lv4, Efx_Lv5, Efx_Lv6, Efx_Lv7, Efx_Lv8, Efx_Lv9, Efx_Lv10;
            public double[] Efx => new[] { Efx_Lv0, Efx_Lv1, Efx_Lv2, Efx_Lv3, Efx_Lv4, Efx_Lv5, Efx_Lv6, Efx_Lv7, Efx_Lv8, Efx_Lv9, Efx_Lv10 };
        }
        public sealed class UpgradeCost
        {
            public int Cost_ID;
            public double Cost_Lv1, Cost_Lv2, Cost_Lv3, Cost_Lv4, Cost_Lv5, Cost_Lv6, Cost_Lv7, Cost_Lv8, Cost_Lv9, Cost_Lv10;
            public double Gem_Lv1, Gem_Lv2, Gem_Lv3, Gem_Lv4, Gem_Lv5, Gem_Lv6, Gem_Lv7, Gem_Lv8, Gem_Lv9, Gem_Lv10;
            public double[] Gold => new[] { Cost_Lv1, Cost_Lv2, Cost_Lv3, Cost_Lv4, Cost_Lv5, Cost_Lv6, Cost_Lv7, Cost_Lv8, Cost_Lv9, Cost_Lv10 };
            public double[] Gem => new[] { Gem_Lv1, Gem_Lv2, Gem_Lv3, Gem_Lv4, Gem_Lv5, Gem_Lv6, Gem_Lv7, Gem_Lv8, Gem_Lv9, Gem_Lv10 };
        }
        public sealed class SkillRow { public string Skill_Key, Skill_Name, Kind, Color, Icon; public int No_Rate; }
        public sealed class Ball
        {
            public int Ball_ID, Ball_Str, Ball_Efx_ID;
            public string Ball_Key, Ball_Name, Ball_Type, Ball_Look;
            public double Ball_Unlock, Ball_LvGold;
        }
        public sealed class BallEfx { public int Ball_Efx_ID; public double Atk, Spd, Size, Cri; }
        public sealed class Weader
        {
            public int Wea_ID, Str_Group_ID, Wea_Rate, Weader_Type, Fav_Tier;
            public string Wea_Key, Wea_Name, Icon, Fav;
            public double Fav_Mul;
        }
        public sealed class Boss
        {
            public int Boss_ID, Str_Group_ID;
            public string Boss_Key, Boss_Name, Cat, Look, Color;
            public double Hp_Mul;
        }
        public sealed class Const { public string Const_Key, Desc; public double Value; }
        public sealed class Str { public int Str_ID, Str_Group_ID; public string Str_Name, Str_KR, Str_EN, Str_JA, Str_RU, Str_UK; }

        public sealed class Book
        {
            public string _source;
            public List<Upgrade> Upgrade;
            public List<UpgradeCost> Upgrade_Cost;
            public List<SkillRow> Skill;
            public List<Ball> Ball;
            public List<BallEfx> Ball_Efx;
            public List<Weader> Weader;
            public List<Boss> Boss;
            public List<Const> Const;
            public List<Str> String;
        }

        static Book data;
        static Dictionary<string, double> consts;
        static Dictionary<string, string> named;   // Str_Name → Str_KR (데이터 문구: "upg:ps_atk:desc" 등)

        public static Book Data
        {
            get
            {
                if (data == null) data = Load();
                return data;
            }
        }

        // 다음에 읽을 때 파일을 다시 읽는다 (에디터 도구가 db.json을 새로 만든 뒤). 이미 만들어진 Defs 표는 스크립트를 다시 불러와야 바뀐다
        public static void Reload() { data = null; consts = null; named = null; }

        static Book Load()
        {
            string json;
#if UNITY_EDITOR
            if (!System.IO.File.Exists(EditorPath)) throw new InvalidOperationException($"[DB] {EditorPath}이 없어요. MoreMush/DB/가져오기를 먼저 실행하세요");
            json = System.IO.File.ReadAllText(EditorPath);
#else
            var asset = Resources.Load<TextAsset>(ResPath);
            if (asset == null) throw new InvalidOperationException($"[DB] Resources/{ResPath}.json이 빌드에 없어요");
            json = asset.text;
#endif
            var b = JsonConvert.DeserializeObject<Book>(json);
            if (b?.Upgrade == null || b.Upgrade_Cost == null || b.Skill == null || b.Ball == null || b.Ball_Efx == null
                || b.Weader == null || b.Boss == null || b.Const == null || b.String == null)
                throw new InvalidOperationException("[DB] db.json에 빠진 시트가 있어요. MoreMush/DB/가져오기를 다시 실행하세요");
            consts = new Dictionary<string, double>();
            foreach (var c in b.Const)
            {
                if (string.IsNullOrEmpty(c.Const_Key) || consts.ContainsKey(c.Const_Key)) throw new InvalidOperationException($"[DB] Const 시트: 비었거나 겹친 키 '{c.Const_Key}'");
                consts[c.Const_Key] = c.Value;
            }
            named = new Dictionary<string, string>();
            foreach (var s in b.String) if (!string.IsNullOrEmpty(s.Str_Name) && s.Str_Name != "ui") named[s.Str_Name] = s.Str_KR;
            return b;
        }

        // 상수 (Const 시트). 없으면 바로 알린다 — 조용히 0을 쓰면 밸런스가 깨진 채로 돌아간다
        public static double C(string key)
        {
            _ = Data;
            if (consts.TryGetValue(key, out var v)) return v;
            throw new KeyNotFoundException($"[DB] Const 시트에 {key}가 없어요");
        }

        // 정수 상수: 엑셀 값의 소수 잡음(2.9999999)에 버림으로 틀어지지 않게 반올림하고, 최솟값을 확인한다
        public static int CInt(string key, int min)
        {
            int v = (int)Math.Round(C(key));
            if (v < min) throw new InvalidOperationException($"[DB] Const {key} = {v}: {min} 이상이어야 해요");
            return v;
        }

        // 양수여야 하는 상수 (배율·간격 등). 0이면 체력·시간이 조용히 0이 된다
        public static double CPos(string key)
        {
            double v = C(key);
            if (!(v > 0)) throw new InvalidOperationException($"[DB] Const {key} = {v}: 0보다 커야 해요");
            return v;
        }

        // 데이터 문구 (String 시트의 Str_Name). 한국어 원문을 돌려준다 — 화면에는 Loc.T로 번역해서 쓴다
        public static string Text(string strName)
        {
            _ = Data;
            if (named.TryGetValue(strName, out var v)) return v;
            throw new KeyNotFoundException($"[DB] String 시트에 이름이 {strName}인 문구가 없어요");
        }

        // 다른 언어 번역표: 한국어 원문 → 그 언어 (빈 칸 = 번역 전)
        public static Dictionary<string, string> LangTable(string lang)
        {
            var map = new Dictionary<string, string>();
            foreach (var s in Data.String)
            {
                if (string.IsNullOrEmpty(s.Str_KR)) continue;
                map[s.Str_KR] = lang switch { "en" => s.Str_EN, "ja" => s.Str_JA, "ru" => s.Str_RU, "uk" => s.Str_UK, _ => null };
            }
            return map;
        }
    }
}
