using System;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Security;
using ApotekFirdanFarma.Services.Abstractions;

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

        // Method public lama: signature tidak berubah. Hanya meneruskan ke overload internal dengan implementasi asli.
        public static void ResetPasswordWithOldPassword(string username, string oldPassword, string newPassword, string confirmPassword)
        {
            ResetPasswordWithOldPassword(username, oldPassword, newPassword, confirmPassword,
                () => new SqliteAuthUnitOfWork(), new DefaultPasswordHasher(), new SystemClock());
        }

        // Overload internal: logika & decision sama dengan versi sebelumnya; akses data lewat abstraksi supaya bisa diuji.
        internal static void ResetPasswordWithOldPassword(string username, string oldPassword, string newPassword, string confirmPassword,
            Func<IAuthUnitOfWork> uowFactory, IPasswordHasher hasher, IClock clock)
        {
            username = (username ?? "").Trim();                      // S1
            oldPassword = oldPassword ?? "";                         // S2
            newPassword = newPassword ?? "";                         // S3
            confirmPassword = confirmPassword ?? "";                 // S4

            if (string.IsNullOrWhiteSpace(username))                 // D1
                throw new InvalidOperationException("Username wajib diisi.");                      // S5
            if (string.IsNullOrWhiteSpace(oldPassword))              // D2
                throw new InvalidOperationException("Password sebelumnya wajib diisi.");           // S6
            if (string.IsNullOrWhiteSpace(newPassword))              // D3
                throw new InvalidOperationException("Password baru wajib diisi.");                 // S7
            if (newPassword.Length < 6)                              // D4
                throw new InvalidOperationException("Password baru minimal 6 karakter.");          // S8
            if (newPassword != confirmPassword)                      // D5
                throw new InvalidOperationException("Konfirmasi password baru tidak cocok.");      // S9
            if (oldPassword == newPassword)                          // D6
                throw new InvalidOperationException("Password baru tidak boleh sama dengan password sebelumnya."); // S10

            using (var uow = uowFactory())                           // S11
            {
                long userId = 0;                                     // S12
                string displayName = null;                           // S13
                string role = null;                                  // S14
                string currentHash = null;                           // S15
                bool userFound = false;                              // S16

                var user = uow.Users.FindByUsername(username);       // S17
                if (user != null && user.IsActive)                   // D7
                {
                    userFound = true;                                // S18
                    userId = user.Id;                                // S19
                    displayName = user.DisplayName;                  // S20
                    role = user.Role;                                // S21
                    currentHash = user.PasswordHash;                 // S22
                }

                if (!userFound)                                      // D8
                {
                    uow.Audit.Log(new AuditEntry                     // S23
                    {
                        Action = "RESET_PASSWORD", Entity = "user",
                        Reason = "Username tidak ditemukan/nonaktif", Notes = "Reset password gagal", Status = "GAGAL",
                        UsernameOverride = username
                    });
                    uow.Commit();                                    // S24
                    throw new InvalidOperationException("Username tidak ditemukan atau akun nonaktif.");  // S25
                }

                if (!hasher.Verify(oldPassword, currentHash))        // D9
                {
                    uow.Audit.Log(new AuditEntry                     // S26
                    {
                        Action = "RESET_PASSWORD", Entity = "user", EntityId = userId, ReferenceCode = username,
                        Reason = "Password sebelumnya salah", Notes = "Reset password gagal", Status = "GAGAL",
                        UsernameOverride = username, RoleOverride = role
                    });
                    uow.Commit();                                    // S27
                    throw new InvalidOperationException("Password sebelumnya salah.");   // S28
                }

                var now = clock.Now.ToString("s");                   // S29
                uow.Users.UpdatePasswordHash(userId, hasher.Hash(newPassword), now);   // S30

                var dummyUser = new AppUser { Id = userId, Username = username, DisplayName = displayName, Role = role };  // S31
                uow.Audit.Log(new AuditEntry                         // S32
                {
                    User = dummyUser, Action = "RESET_PASSWORD", Entity = "user", EntityId = userId, ReferenceCode = username,
                    NewValue = "Password berhasil diperbarui via verifikasi password sebelumnya",
                    Notes = "Reset password berhasil", Status = "BERHASIL"
                });
                uow.Commit();                                        // S33
            }
        }
    }
}
