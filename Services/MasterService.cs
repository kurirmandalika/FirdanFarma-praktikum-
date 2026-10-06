using System;
using System.Data;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public static class MasterService
    {
        public static DataTable Categories(bool includeInactive = true)
        {
            return Database.Query("SELECT id,name AS Nama,CASE is_active WHEN 1 THEN 'Aktif' ELSE 'Nonaktif' END AS Status FROM categories WHERE @all=1 OR is_active=1 ORDER BY name", new SQLiteParameter("@all", includeInactive ? 1 : 0));
        }

        public static DataTable Suppliers(bool includeInactive = true)
        {
            return Database.Query("SELECT id,name AS Nama,phone AS Telepon,address AS Alamat,CASE is_active WHEN 1 THEN 'Aktif' ELSE 'Nonaktif' END AS Status FROM suppliers WHERE @all=1 OR is_active=1 ORDER BY name", new SQLiteParameter("@all", includeInactive ? 1 : 0));
        }

        public static long SaveCategory(AppUser actor, long id, string name, bool active)
        {
            UserService.RequireSuper(actor);
            name = (name ?? "").Trim(); if (name.Length == 0) throw new InvalidOperationException("Nama kategori wajib diisi.");
            using (var c = Database.Open()) using (var tx = c.BeginTransaction())
            {
                long result = id;
                if (id == 0)
                {
                    using (var cmd = new SQLiteCommand("INSERT INTO categories(name,is_active) VALUES(@n,@a); SELECT last_insert_rowid();", c, tx))
                    { cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@a", active ? 1 : 0); result = Convert.ToInt64(cmd.ExecuteScalar()); }
                }
                else
                {
                    using (var cmd = new SQLiteCommand("UPDATE categories SET name=@n,is_active=@a WHERE id=@id", c, tx))
                    { cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@a", active ? 1 : 0); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
                }
                AuditService.LogDetailed(actor, id == 0 ? "TAMBAH_KATEGORI" : "UBAH_KATEGORI", "category", result, name, null, "aktif=" + active, null, null, null, null, name, "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit(); return result;
            }
        }

        public static long SaveSupplier(AppUser actor, long id, string name, string phone, string address, bool active)
        {
            UserService.RequireSuper(actor);
            name = (name ?? "").Trim(); if (name.Length == 0) throw new InvalidOperationException("Nama supplier wajib diisi.");
            using (var c = Database.Open()) using (var tx = c.BeginTransaction())
            {
                long result = id;
                if (id == 0)
                {
                    using (var cmd = new SQLiteCommand("INSERT INTO suppliers(name,phone,address,is_active) VALUES(@n,@p,@addr,@a); SELECT last_insert_rowid();", c, tx))
                    { cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@p", phone ?? ""); cmd.Parameters.AddWithValue("@addr", address ?? ""); cmd.Parameters.AddWithValue("@a", active ? 1 : 0); result = Convert.ToInt64(cmd.ExecuteScalar()); }
                }
                else
                {
                    using (var cmd = new SQLiteCommand("UPDATE suppliers SET name=@n,phone=@p,address=@addr,is_active=@a WHERE id=@id", c, tx))
                    { cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@p", phone ?? ""); cmd.Parameters.AddWithValue("@addr", address ?? ""); cmd.Parameters.AddWithValue("@a", active ? 1 : 0); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
                }
                AuditService.LogDetailed(actor, id == 0 ? "TAMBAH_SUPPLIER" : "UBAH_SUPPLIER", "supplier", result, name, null, "aktif=" + active, null, null, null, null, name, "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit(); return result;
            }
        }

        public static long EnsureSupplier(SQLiteConnection c, SQLiteTransaction tx, string name)
        {
            name = (name ?? "").Trim(); if (name.Length == 0) return 0;
            using (var q = new SQLiteCommand("SELECT id FROM suppliers WHERE name=@n COLLATE NOCASE LIMIT 1", c, tx))
            { q.Parameters.AddWithValue("@n", name); var o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) return Convert.ToInt64(o); }
            using (var cmd = new SQLiteCommand("INSERT INTO suppliers(name,is_active) VALUES(@n,1); SELECT last_insert_rowid();", c, tx))
            { cmd.Parameters.AddWithValue("@n", name); return Convert.ToInt64(cmd.ExecuteScalar()); }
        }

        public static long EnsureCategory(SQLiteConnection c, SQLiteTransaction tx, string name)
        {
            name = (name ?? "").Trim(); if (name.Length == 0) return 0;
            using (var q = new SQLiteCommand("SELECT id FROM categories WHERE name=@n COLLATE NOCASE LIMIT 1", c, tx))
            { q.Parameters.AddWithValue("@n", name); var o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) return Convert.ToInt64(o); }
            using (var cmd = new SQLiteCommand("INSERT INTO categories(name,is_active) VALUES(@n,1); SELECT last_insert_rowid();", c, tx))
            { cmd.Parameters.AddWithValue("@n", name); return Convert.ToInt64(cmd.ExecuteScalar()); }
        }
    }
}
