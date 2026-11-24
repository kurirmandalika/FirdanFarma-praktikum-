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

        public static void ResetPasswordWithOldPassword(string username, string oldPassword, string newPassword, string confirmPassword)
        {
            username = (username ?? "").Trim();
            oldPassword = oldPassword ?? "";
            newPassword = newPassword ?? "";
            confirmPassword = confirmPassword ?? "";

            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Username wajib diisi.");
            if (string.IsNullOrWhiteSpace(oldPassword))
                throw new InvalidOperationException("Password sebelumnya wajib diisi.");
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new InvalidOperationException("Password baru wajib diisi.");
            if (newPassword.Length < 6)
                throw new InvalidOperationException("Password baru minimal 6 karakter.");
            if (newPassword != confirmPassword)
                throw new InvalidOperationException("Konfirmasi password baru tidak cocok.");
            if (oldPassword == newPassword)
                throw new InvalidOperationException("Password baru tidak boleh sama dengan password sebelumnya.");

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                long userId = 0;
                string displayName = null;
                string role = null;
                string currentHash = null;
                bool userFound = false;

                using (var cmd = new SQLiteCommand("SELECT id, username, display_name, role, password_hash, is_active FROM users WHERE username=@u", c, tx))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read() && Convert.ToInt32(r["is_active"]) == 1)
                        {
                            userFound = true;
                            userId = Convert.ToInt64(r["id"]);
                            displayName = Convert.ToString(r["display_name"]);
                            role = Convert.ToString(r["role"]);
                            currentHash = Convert.ToString(r["password_hash"]);
                        }
                    }
                }

                if (!userFound)
                {
                    AuditService.LogDetailed(null, "RESET_PASSWORD", "user", null, null, null, null, null, null, null, "Username tidak ditemukan/nonaktif", "Reset password gagal", "GAGAL", null, null, 0, 0, c, tx, username, null);
                    tx.Commit();
                    throw new InvalidOperationException("Username tidak ditemukan atau akun nonaktif.");
                }

                if (!PasswordHasher.Verify(oldPassword, currentHash))
                {
                    AuditService.LogDetailed(null, "RESET_PASSWORD", "user", userId, username, null, null, null, null, null, "Password sebelumnya salah", "Reset password gagal", "GAGAL", null, null, 0, 0, c, tx, username, role);
                    tx.Commit();
                    throw new InvalidOperationException("Password sebelumnya salah.");
                }

                var now = DateTime.Now.ToString("s");
                using (var updateCmd = new SQLiteCommand("UPDATE users SET password_hash=@p, updated_at=@now WHERE id=@id", c, tx))
                {
                    updateCmd.Parameters.AddWithValue("@p", PasswordHasher.Hash(newPassword));
                    updateCmd.Parameters.AddWithValue("@now", now);
                    updateCmd.Parameters.AddWithValue("@id", userId);
                    updateCmd.ExecuteNonQuery();
                }

                var dummyUser = new AppUser { Id = userId, Username = username, DisplayName = displayName, Role = role };
                AuditService.LogDetailed(dummyUser, "RESET_PASSWORD", "user", userId, username, null, "Password berhasil diperbarui via verifikasi password sebelumnya", null, null, null, null, "Reset password berhasil", "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
            }
        }
    }
}
