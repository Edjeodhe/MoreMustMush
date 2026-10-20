using UnityEngine;

namespace MoreMush
{
    // 분석 로그의 version 칸에 넣는 빌드 식별: "앱버전+커밋.DB" (예: 0.0.7+9aa7819.db06). 서버 version 칸은 32자까지.
    // 버전만으로는 같은 0.0.7 안에서 수치가 바뀐 빌드를 D1에서 가를 수 없었다 (2026-10-20, 밸런스 5회차 인계 4번).
    // 커밋은 빌드할 때 Editor/BuildTagWriter가 Resources/BuildInfo.txt로 넣고 빌드 뒤 지운다. 에디터·파일 없음 = "dev"
    public static class BuildTag
    {
        static string version;

        public static string Version
        {
            get
            {
                if (version != null) return version;
                var info = Resources.Load<TextAsset>("BuildInfo");
                string commit = info != null ? info.text.Trim() : "dev";
                if (string.IsNullOrEmpty(commit)) commit = "dev";
                string v = Application.version + "+" + commit + "." + DbTag();
                return version = v.Length <= 32 ? v : v.Substring(0, 32);
            }
        }

        // "MMM_DB_Ver.06.xlsx" → "db06" (읽지 못하면 "db")
        static string DbTag()
        {
            string src = null;
            try { src = Db.Data?._source; } catch { }
            if (string.IsNullOrEmpty(src)) return "db";
            int i = src.IndexOf("Ver.");
            if (i < 0) return "db";
            int s = i + 4, e = s;
            while (e < src.Length && char.IsDigit(src[e])) e++;
            return e > s ? "db" + src.Substring(s, e - s) : "db";
        }
    }
}
