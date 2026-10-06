using System;
using System.Data;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Security;

namespace ApotekFirdanFarma.Services
{
    public static class UserService
    {
        public static DataTable List()
        {
            return Database.Query(@"SELECT id,username AS Username,display_name AS [Nama Tampilan],role AS Role,
CASE is_active WHEN 1 THEN 'Aktif' ELSE 'Nonaktif' END AS Status,
CASE WHEN role='SUPER_ADMIN' THEN 'Ya' WHEN can_purchase=1 THEN 'Ya' ELSE 'Tidak' END AS [Boleh Stok Masuk],
CASE WHEN role='SUPER_ADMIN' THEN 'Ya' WHEN can_adjust_stock=1 THEN 'Ya' ELSE 'Tidak' END AS [Boleh Koreksi Stok],
created_at AS Dibuat,updated_at AS Diperbarui
FROM users ORDER BY CASE role WHEN 'SUPER_ADMIN' THEN 0 ELSE 1 END,username");
        }

        public static long Save(AppUser actor, long id, string username, string displayName, string role, string password, bool active, bool canPurchase, bool canAdjustStock)
        {
            RequireSuper(actor);
            username = (username ?? "").Trim();
            displayName = (displayName ?? "").Trim();
            role = (role ?? "").Trim().ToUpperInvariant();
            if (username.Length < 3) throw new InvalidOperationException("Username minimal 3 karakter.");
            if (displayName.Length == 0) throw new InvalidOperationException("Nama tampilan wajib diisi.");
            if (role != "KARYAWAN" && role != "SUPER_ADMIN") throw new InvalidOperationException("Role tidak valid.");
            if (id == 0 && string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Password akun baru wajib diisi.");
            if (!string.IsNullOrEmpty(password) && password.Length < 6) throw new InvalidOperationException("Password minimal 6 karakter.");
            if (id == actor.Id && !active) throw new InvalidOperationException("Akun yang sedang dipakai tidak boleh dinonaktifkan.");
            if (id == actor.Id && role != "SUPER_ADMIN") throw new InvalidOperationException("Role akun yang sedang dipakai tidak dapat diturunkan selama sesi aktif.");
            if (role == "SUPER_ADMIN")
            {
                canPurchase = true;
                canAdjustStock = true;
            }

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                var old = id == 0 ? null : GetSnapshot(c, tx, id);
                long resultId = id;
                var now = DateTime.Now.ToString("s");
                try
                {
                    if (id == 0)
                    {
                        using (var cmd = new SQLiteCommand(@"INSERT INTO users(username,display_name,role,password_hash,is_active,can_purchase,can_adjust_stock,created_at,updated_at)
VALUES(@u,@d,@r,@p,@active,@cp,@ca,@now,@now); SELECT last_insert_rowid();", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@u", username);
                            cmd.Parameters.AddWithValue("@d", displayName);
                            cmd.Parameters.AddWithValue("@r", role);
                            cmd.Parameters.AddWithValue("@p", PasswordHasher.Hash(password));
                            cmd.Parameters.AddWithValue("@active", active ? 1 : 0);
                            cmd.Parameters.AddWithValue("@cp", canPurchase ? 1 : 0);
                            cmd.Parameters.AddWithValue("@ca", canAdjustStock ? 1 : 0);
                            cmd.Parameters.AddWithValue("@now", now);
                            resultId = Convert.ToInt64(cmd.ExecuteScalar());
                        }
                    }
                    else
                    {
                        var sql = string.IsNullOrWhiteSpace(password)
                            ? @"UPDATE users SET username=@u,display_name=@d,role=@r,is_active=@active,can_purchase=@cp,can_adjust_stock=@ca,updated_at=@now WHERE id=@id"
                            : @"UPDATE users SET username=@u,display_name=@d,role=@r,password_hash=@p,is_active=@active,can_purchase=@cp,can_adjust_stock=@ca,updated_at=@now WHERE id=@id";
                        using (var cmd = new SQLiteCommand(sql, c, tx))
                        {
                            cmd.Parameters.AddWithValue("@u", username);
                            cmd.Parameters.AddWithValue("@d", displayName);
                            cmd.Parameters.AddWithValue("@r", role);
                            if (!string.IsNullOrWhiteSpace(password)) cmd.Parameters.AddWithValue("@p", PasswordHasher.Hash(password));
                            cmd.Parameters.AddWithValue("@active", active ? 1 : 0);
                            cmd.Parameters.AddWithValue("@cp", canPurchase ? 1 : 0);
                            cmd.Parameters.AddWithValue("@ca", canAdjustStock ? 1 : 0);
                            cmd.Parameters.AddWithValue("@now", now);
                            cmd.Parameters.AddWithValue("@id", id);
                            if (cmd.ExecuteNonQuery() == 0) throw new InvalidOperationException("Akun tidak ditemukan.");
                        }
                    }
                }
                catch (SQLiteException ex)
                {
                    if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0) throw new InvalidOperationException("Username sudah digunakan.");
                    throw;
                }

                if (!HasAnotherActiveSuperAdmin(c, tx, resultId, role, active))
                    throw new InvalidOperationException("Minimal harus ada satu Super Admin aktif.");

                var current = username + " | " + displayName + " | " + role + " | aktif=" + active + " | stok_masuk=" + canPurchase + " | koreksi=" + canAdjustStock + (string.IsNullOrWhiteSpace(password) ? "" : " | password=diubah");
                AuditService.LogDetailed(actor, id == 0 ? "TAMBAH_AKUN" : "UBAH_AKUN", "user", resultId, username, old, current, null, null, null, null, "Pengelolaan akun", "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
                return resultId;
            }
        }

        private static bool HasAnotherActiveSuperAdmin(SQLiteConnection c, SQLiteTransaction tx, long changedId, string newRole, bool newActive)
        {
            if (newRole == "SUPER_ADMIN" && newActive) return true;
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM users WHERE role='SUPER_ADMIN' AND is_active=1 AND id<>@id", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", changedId);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static string GetSnapshot(SQLiteConnection c, SQLiteTransaction tx, long id)
        {
            using (var cmd = new SQLiteCommand("SELECT username,display_name,role,is_active,can_purchase,can_adjust_stock FROM users WHERE id=@id", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("Akun tidak ditemukan.");
                    var role = Convert.ToString(r["role"]);
                    var stockIn = string.Equals(role, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase) || Convert.ToInt32(r["can_purchase"]) == 1;
                    var adjust = string.Equals(role, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase) || Convert.ToInt32(r["can_adjust_stock"]) == 1;
                    return Convert.ToString(r["username"]) + " | " + Convert.ToString(r["display_name"]) + " | " + role +
                           " | aktif=" + (Convert.ToInt32(r["is_active"]) == 1) + " | stok_masuk=" + stockIn +
                           " | koreksi=" + adjust;
                }
            }
        }

        public static void RequireSuper(AppUser user)
        {
            if (user == null || !user.IsSuperAdmin) throw new UnauthorizedAccessException("Fitur ini hanya untuk Super Admin.");
        }
    }
}
