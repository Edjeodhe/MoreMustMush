using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MoreMush
{
    // 로컬 저장 암호화 (AES-CBC + HMAC-SHA256). 변조·손상이면 복구 순서대로 시도하고, 전부 실패하면 false.
    // 사용: SecureStore.Save(path, uid, json);  if (!SecureStore.Load(path, uid, out fmt, out json)) { /* 새로 시작 */ }
    // 출처: 로그 서버 인수인계(HANDOFF.md) SecureStore.cs — 프로젝트 네임스페이스에 넣고 SECRET만 정했다
    public static class SecureStore
    {
        // 키가 게임 코드에 박혀 있어 코드를 뜯으면 노출됨. '쉽게 못 고치게' 하는 용도. 진짜 보안이 필요하면 서버 기준으로.
        // 주의: 이 값을 바꾸면 이전 빌드의 세이브·로그 큐를 못 읽는다(새로 시작). 출시 뒤에는 바꾸지 않는다
        const string SECRET = "MMM-7f3c9a2e-local-store-2026-10-15-b81d";
        public const int FORMAT = 1; // 저장 구조가 바뀌면 올리고, Load가 돌려준 fmt를 보고 호출 측에서 변환

        static byte[] K(string tag, string uid)
        {
            using (var h = SHA256.Create()) return h.ComputeHash(Encoding.UTF8.GetBytes(SECRET + tag + uid));
        }

        public static void Save(string path, string uid, string json)
        {
            var plain = Encoding.UTF8.GetBytes(FORMAT + "|" + json);
            byte[] body;
            using (var aes = Aes.Create())
            {
                aes.Key = K("e", uid); aes.GenerateIV();
                byte[] ct;
                using (var enc = aes.CreateEncryptor()) ct = enc.TransformFinalBlock(plain, 0, plain.Length);
                body = new byte[16 + ct.Length];
                Buffer.BlockCopy(aes.IV, 0, body, 0, 16);
                Buffer.BlockCopy(ct, 0, body, 16, ct.Length);
            }
            byte[] mac;
            using (var h = new HMACSHA256(K("m", uid))) mac = h.ComputeHash(body);
            var all = new byte[32 + body.Length];
            Buffer.BlockCopy(mac, 0, all, 0, 32);
            Buffer.BlockCopy(body, 0, all, 32, body.Length);

            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, all);                                   // 1) 새 파일을 먼저 완성
            if (TryRead(path, uid, out _, out _)) File.Copy(path, path + ".bak", true); // 2) 현재 파일이 정상일 때만 백업 갱신
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);                                           // 3) 교체
        }

        public static bool Load(string path, string uid, out int format, out string json)
        {
            // tmp는 '완성된' 것만 MAC을 통과하므로, 저장 도중 꺼졌어도 가장 최신 정상본을 고른다.
            foreach (var p in new[] { path + ".tmp", path, path + ".bak" })
                if (TryRead(p, uid, out format, out json)) return true;
            format = 0; json = null; return false;
        }

        public static bool Exists(string path) => File.Exists(path) || File.Exists(path + ".tmp") || File.Exists(path + ".bak");

        public static void Delete(string path)
        {
            foreach (var p in new[] { path, path + ".tmp", path + ".bak" })
                if (File.Exists(p)) File.Delete(p);
        }

        static bool TryRead(string p, string uid, out int format, out string json)
        {
            format = 0; json = null;
            try
            {
                if (!File.Exists(p)) return false;
                var all = File.ReadAllBytes(p);
                if (all.Length < 32 + 16 + 16) return false;
                var mac = new byte[32]; Buffer.BlockCopy(all, 0, mac, 0, 32);
                var body = new byte[all.Length - 32]; Buffer.BlockCopy(all, 32, body, 0, body.Length);
                byte[] exp;
                using (var h = new HMACSHA256(K("m", uid))) exp = h.ComputeHash(body);
                int diff = 0; for (int i = 0; i < 32; i++) diff |= mac[i] ^ exp[i];
                if (diff != 0) return false;                                // 변조·손상
                using (var aes = Aes.Create())
                {
                    aes.Key = K("e", uid);
                    var iv = new byte[16]; Buffer.BlockCopy(body, 0, iv, 0, 16); aes.IV = iv;
                    byte[] pt;
                    using (var dec = aes.CreateDecryptor()) pt = dec.TransformFinalBlock(body, 16, body.Length - 16);
                    var s = Encoding.UTF8.GetString(pt);
                    int bar = s.IndexOf('|');
                    format = int.Parse(s.Substring(0, bar)); json = s.Substring(bar + 1);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
