using System;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Security;

namespace ApotekFirdanFarma.Services
{
    public static class AuthService
    {
        public static AppUser Login(string username, string password)
        {
            username = (username ?? "").Trim();
            using (var c = Database.Open())
            using (var cmd = new SQLiteCommand(@"SELECT id,username,display_name,role,password_hash,is_active,
IFNULL(can_purchase,1) AS can_purchase,IFNULL(can_adjust_stock,0) AS can_adjust_stock
FROM users WHERE username=@u", c))
            {
                cmd.Parameters.AddWithValue("@u", username);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read() || Convert.ToInt32(r["is_active"]) != 1)
                    {
                        AuditService.LogDetailed(null, "LOGIN", "user", null, null, null, null, null, null, null, "Login ditolak", "Username tidak ditemukan/nonaktif", "GAGAL", null, null, 0, 0, null, null, username, null);
                        return null;
                    }
                    if (!PasswordHasher.Verify(password ?? "", Convert.ToString(r["password_hash"])))
                    {
                        AuditService.LogDetailed(null, "LOGIN", "user", Convert.ToInt64(r["id"]), null, null, null, null, null, null, "Password salah", "Login gagal", "GAGAL", null, null, 0, 0, null, null, username, Convert.ToString(r["role"]));
                        return null;
                    }
                    var u = new AppUser
                    {
                        Id = Convert.ToInt64(r["id"]),
                        Username = Convert.ToString(r["username"]),
                        DisplayName = Convert.ToString(r["display_name"]),
                        Role = Convert.ToString(r["role"]),
                        IsActive = true,
                        CanPurchase = string.Equals(Convert.ToString(r["role"]), "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase) || Convert.ToInt32(r["can_purchase"]) == 1,
                        CanAdjustStock = string.Equals(Convert.ToString(r["role"]), "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase) || Convert.ToInt32(r["can_adjust_stock"]) == 1
                    };
                    AuditService.LogDetailed(u, "LOGIN", "user", u.Id, null, null, null, null, null, null, null, "Login berhasil");
                    return u;
                }
            }
        }

        public static void Logout(AppUser user)
        {
            if (user != null) AuditService.LogDetailed(user, "LOGOUT", "user", user.Id, null, null, null, null, null, null, null, "Keluar aplikasi");
        }
    }
}
