using System;
using System.Security.Cryptography;

namespace ApotekFirdanFarma.Security
{
    public static class PasswordHasher
    {
        private const int Iterations = 120000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password)
        {
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(pbkdf2.GetBytes(HashSize))}";
            }
        }

        public static bool Verify(string password, string encoded)
        {
            try
            {
                var p = encoded.Split('$');
                if (p.Length != 4 || p[0] != "PBKDF2-SHA256") return false;
                var iterations = int.Parse(p[1]);
                var salt = Convert.FromBase64String(p[2]);
                var expected = Convert.FromBase64String(p[3]);
                byte[] actual;
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                    actual = pbkdf2.GetBytes(expected.Length);
                return FixedTimeEquals(expected, actual);
            }
            catch { return false; }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
