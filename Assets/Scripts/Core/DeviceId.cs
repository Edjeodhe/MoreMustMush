using System;
using System.IO;
using UnityEngine;

namespace MoreMush
{
    // 기기(브라우저) 단위 무작위 ID. 서버 로그의 uid이자 로컬 저장 암호 키의 재료(SecureStore).
    // 한쪽이 지워져도 세이브를 읽을 수 있게 PlayerPrefs와 파일(persistentDataPath/uid.txt) 두 곳에 둔다. IP 등 개인정보는 쓰지 않는다
    public static class DeviceId
    {
        const string PREF = "tlm_uid";
        static string id;
        public static bool Created { get; private set; }   // 이번 실행에서 처음 만들었다 (첫 실행)

        static string FilePath => Path.Combine(Application.persistentDataPath, "uid.txt");

        public static string Get()
        {
            if (id != null) return id;
            try { if (File.Exists(FilePath)) id = File.ReadAllText(FilePath).Trim(); } catch { }
            if (!Valid(id)) id = PlayerPrefs.GetString(PREF, "");
            if (!Valid(id)) { id = Guid.NewGuid().ToString("N"); Created = true; }
            PlayerPrefs.SetString(PREF, id); PlayerPrefs.Save();
            try { File.WriteAllText(FilePath, id); } catch (Exception e) { Debug.LogWarning("uid 파일을 못 씀: " + e.Message); }
            return id;
        }

        // 32자리 소문자 16진수 (서버 입력 검사와 같은 규칙)
        public static bool Valid(string s)
        {
            if (s == null || s.Length != 32) return false;
            foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
