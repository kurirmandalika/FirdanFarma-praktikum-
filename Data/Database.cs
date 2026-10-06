using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using ApotekFirdanFarma.Core;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Security;

namespace ApotekFirdanFarma.Data
{
    public static class Database
    {
        public const int CurrentSchemaVersion = 9;
        public static string ConnectionString => $"Data Source={AppPaths.DatabasePath};Version=3;Foreign Keys=True;Journal Mode=WAL;Synchronous=Normal;";
        private const string DefaultSuperAdminUsername = "alfian";
        private const string DefaultSuperAdminDisplay = "Alfian";
        private const string DefaultSuperAdminPassword = "alfian";
        private const string DefaultEmployeeUsername = "apotek01";
        private const string DefaultEmployeeDisplay = "Apotek 01";
        private const string DefaultEmployeePassword = "apotek01";

        public static SQLiteConnection Open()
        {
            var c = new SQLiteConnection(ConnectionString);
            c.Open();
            using (var cmd = new SQLiteCommand("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;", c))
                cmd.ExecuteNonQuery();
            return c;
        }

        public static void Initialize()
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var existed = File.Exists(AppPaths.DatabasePath);
            if (!existed) SQLiteConnection.CreateFile(AppPaths.DatabasePath);
            if (existed) CreatePreMigrationBackupIfNeeded();

            using (var c = Open())
            using (var tx = c.BeginTransaction())
            {
                Exec(c, @"
CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL);
INSERT INTO schema_info(version) SELECT 1 WHERE NOT EXISTS(SELECT 1 FROM schema_info);

CREATE TABLE IF NOT EXISTS app_settings(
 key TEXT PRIMARY KEY,
 value TEXT
);

CREATE TABLE IF NOT EXISTS users(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 username TEXT NOT NULL UNIQUE,
 display_name TEXT NOT NULL,
 role TEXT NOT NULL CHECK(role IN ('KARYAWAN','SUPER_ADMIN')),
 password_hash TEXT NOT NULL,
 is_active INTEGER NOT NULL DEFAULT 1,
 can_purchase INTEGER NOT NULL DEFAULT 1,
 can_adjust_stock INTEGER NOT NULL DEFAULT 0,
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS categories(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 name TEXT NOT NULL UNIQUE,
 is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS suppliers(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 name TEXT NOT NULL UNIQUE,
 phone TEXT,
 address TEXT,
 is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS medicines(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 code TEXT UNIQUE,
 name TEXT NOT NULL,
 category_id INTEGER,
 supplier_id INTEGER,
 unit TEXT,
 awl INTEGER NOT NULL DEFAULT 0,
 msk INTEGER NOT NULL DEFAULT 0,
 klr INTEGER NOT NULL DEFAULT 0,
 purchase_price REAL NOT NULL DEFAULT 0,
 sale_price REAL NOT NULL DEFAULT 0,
 min_stock INTEGER NOT NULL DEFAULT 0,
 category TEXT,
 supplier TEXT,
 is_active INTEGER NOT NULL DEFAULT 1,
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL,
 CHECK(awl >= 0 AND msk >= 0 AND klr >= 0),
 CHECK((awl + msk - klr) >= 0),
 CHECK(purchase_price >= 0 AND sale_price >= 0),
 FOREIGN KEY(category_id) REFERENCES categories(id),
 FOREIGN KEY(supplier_id) REFERENCES suppliers(id)
);

CREATE TABLE IF NOT EXISTS medicine_lots(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 medicine_id INTEGER NOT NULL,
 batch_no TEXT,
 expiry_date TEXT,
 qty_received INTEGER NOT NULL,
 qty_remaining INTEGER NOT NULL,
 purchase_item_id INTEGER,
 created_at TEXT NOT NULL,
 FOREIGN KEY(medicine_id) REFERENCES medicines(id)
);

CREATE TABLE IF NOT EXISTS medicine_batches(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 medicine_id INTEGER NOT NULL,
 batch_number TEXT,
 expired_date TEXT,
 qty_in INTEGER NOT NULL DEFAULT 0,
 qty_out INTEGER NOT NULL DEFAULT 0,
 qty_available INTEGER NOT NULL DEFAULT 0,
 supplier_id INTEGER,
 purchase_detail_id INTEGER,
 created_at TEXT NOT NULL,
 FOREIGN KEY(medicine_id) REFERENCES medicines(id),
 FOREIGN KEY(supplier_id) REFERENCES suppliers(id)
);

CREATE TABLE IF NOT EXISTS sales(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 transaction_no TEXT NOT NULL UNIQUE,
 transaction_time TEXT NOT NULL,
 report_date TEXT NOT NULL,
 report_code TEXT NOT NULL,
 payment_method TEXT NOT NULL,
 subtotal REAL NOT NULL,
 discount REAL NOT NULL DEFAULT 0,
 total REAL NOT NULL,
 grand_total REAL,
 user_id INTEGER NOT NULL,
 status TEXT NOT NULL DEFAULT 'FINAL',
 note TEXT,
 created_at TEXT NOT NULL,
 FOREIGN KEY(user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS sale_items(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 sale_id INTEGER NOT NULL,
 medicine_id INTEGER NOT NULL,
 batch_id INTEGER,
 qty INTEGER NOT NULL,
 unit_price REAL NOT NULL,
 unit_cost REAL NOT NULL DEFAULT 0,
 subtotal REAL NOT NULL,
 FOREIGN KEY(sale_id) REFERENCES sales(id),
 FOREIGN KEY(medicine_id) REFERENCES medicines(id),
 FOREIGN KEY(batch_id) REFERENCES medicine_batches(id)
);

CREATE TABLE IF NOT EXISTS sale_item_batches(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 sale_item_id INTEGER NOT NULL,
 batch_id INTEGER,
 qty INTEGER NOT NULL,
 FOREIGN KEY(sale_item_id) REFERENCES sale_items(id),
 FOREIGN KEY(batch_id) REFERENCES medicine_batches(id)
);

CREATE TABLE IF NOT EXISTS purchases(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 purchase_no TEXT NOT NULL UNIQUE,
 supplier_id INTEGER,
 supplier TEXT,
 supplier_invoice_number TEXT,
 purchase_time TEXT NOT NULL,
 purchase_date TEXT,
 status TEXT NOT NULL DEFAULT 'SELESAI',
 total REAL NOT NULL,
 user_id INTEGER NOT NULL,
 note TEXT,
 entry_type TEXT NOT NULL DEFAULT 'FAKTUR',
 created_at TEXT NOT NULL,
 FOREIGN KEY(supplier_id) REFERENCES suppliers(id),
 FOREIGN KEY(user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS purchase_items(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 purchase_id INTEGER NOT NULL,
 medicine_id INTEGER NOT NULL,
 medicine_name TEXT,
 medicine_unit TEXT,
 qty INTEGER NOT NULL,
 purchase_price REAL NOT NULL,
 subtotal REAL NOT NULL,
 batch_no TEXT,
 expiry_date TEXT,
 invoice_price REAL NOT NULL DEFAULT 0,
 discount_amount REAL NOT NULL DEFAULT 0,
 ppn_percent REAL NOT NULL DEFAULT 0,
 ppn_amount REAL NOT NULL DEFAULT 0,
 sale_price_suggested REAL NOT NULL DEFAULT 0,
 FOREIGN KEY(purchase_id) REFERENCES purchases(id),
 FOREIGN KEY(medicine_id) REFERENCES medicines(id)
);

CREATE TABLE IF NOT EXISTS stock_mutations(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 medicine_id INTEGER NOT NULL,
 mutation_type TEXT NOT NULL,
 qty INTEGER NOT NULL,
 stock_before INTEGER NOT NULL,
 stock_after INTEGER NOT NULL,
 note TEXT,
 reference_type TEXT,
 reference_id INTEGER,
 report_code TEXT,
 batch_no TEXT,
 expiry_date TEXT,
 user_id INTEGER,
 created_at TEXT NOT NULL,
 FOREIGN KEY(medicine_id) REFERENCES medicines(id),
 FOREIGN KEY(user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS stock_movements(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 medicine_id INTEGER NOT NULL,
 batch_id INTEGER,
 movement_datetime TEXT NOT NULL,
 movement_type TEXT NOT NULL,
 reference_type TEXT,
 reference_id INTEGER,
 transaction_code TEXT,
 qty_in INTEGER NOT NULL DEFAULT 0,
 qty_out INTEGER NOT NULL DEFAULT 0,
 stock_before INTEGER NOT NULL,
 stock_after INTEGER NOT NULL,
 payment_method TEXT,
 user_id INTEGER,
 reason TEXT,
 note TEXT,
 FOREIGN KEY(medicine_id) REFERENCES medicines(id),
 FOREIGN KEY(batch_id) REFERENCES medicine_batches(id),
 FOREIGN KEY(user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS audit_logs(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 action TEXT NOT NULL,
 entity TEXT,
 entity_id INTEGER,
 user_id INTEGER,
 username TEXT,
 role TEXT,
 reason TEXT,
 amount REAL,
 discount REAL,
 payment_method TEXT,
 transaction_code TEXT,
 medicine_id INTEGER,
 batch_no TEXT,
 qty_in INTEGER NOT NULL DEFAULT 0,
 qty_out INTEGER NOT NULL DEFAULT 0,
 old_value TEXT,
 new_value TEXT,
 status TEXT NOT NULL DEFAULT 'BERHASIL',
 notes TEXT,
 created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS stock_opnames(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 opname_no TEXT NOT NULL UNIQUE,
 started_at TEXT NOT NULL,
 submitted_at TEXT,
 completed_at TEXT,
 status TEXT NOT NULL DEFAULT 'DRAFT' CHECK(status IN ('DRAFT','MENUNGGU_PEMERIKSAAN','SELESAI','BATAL')),
 started_by INTEGER NOT NULL,
 submitted_by INTEGER,
 completed_by INTEGER,
 note TEXT,
 created_at TEXT NOT NULL,
 FOREIGN KEY(started_by) REFERENCES users(id),
 FOREIGN KEY(submitted_by) REFERENCES users(id),
 FOREIGN KEY(completed_by) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS stock_opname_items(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 opname_id INTEGER NOT NULL,
 medicine_id INTEGER NOT NULL,
 batch_id INTEGER,
 batch_number TEXT,
 expired_date TEXT,
 system_qty INTEGER NOT NULL DEFAULT 0,
 physical_qty INTEGER,
 variance INTEGER,
 unit_cost REAL NOT NULL DEFAULT 0,
 reason TEXT,
 counted_by INTEGER,
 counted_at TEXT,
 FOREIGN KEY(opname_id) REFERENCES stock_opnames(id),
 FOREIGN KEY(medicine_id) REFERENCES medicines(id),
 FOREIGN KEY(batch_id) REFERENCES medicine_batches(id),
 FOREIGN KEY(counted_by) REFERENCES users(id),
 UNIQUE(opname_id,medicine_id,batch_id)
);
", tx);

                // Migrasi aman untuk database v4 yang sudah dipakai pengguna.
                EnsureColumn(c, tx, "users", "can_purchase", "INTEGER NOT NULL DEFAULT 1");
                EnsureColumn(c, tx, "users", "can_adjust_stock", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "medicines", "category_id", "INTEGER");
                EnsureColumn(c, tx, "medicines", "supplier_id", "INTEGER");
                EnsureColumn(c, tx, "sales", "grand_total", "REAL");
                EnsureColumn(c, tx, "sales", "status", "TEXT NOT NULL DEFAULT 'FINAL'");
                EnsureColumn(c, tx, "sales", "note", "TEXT");
                EnsureColumn(c, tx, "sale_items", "batch_id", "INTEGER");
                EnsureColumn(c, tx, "purchases", "supplier_id", "INTEGER");
                EnsureColumn(c, tx, "purchases", "supplier_invoice_number", "TEXT");
                EnsureColumn(c, tx, "purchases", "purchase_date", "TEXT");
                EnsureColumn(c, tx, "purchases", "note", "TEXT");
                EnsureColumn(c, tx, "purchases", "entry_type", "TEXT NOT NULL DEFAULT 'FAKTUR'");
                EnsureColumn(c, tx, "purchase_items", "medicine_name", "TEXT");
                EnsureColumn(c, tx, "purchase_items", "medicine_unit", "TEXT");
                EnsureColumn(c, tx, "purchase_items", "invoice_price", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "purchase_items", "discount_amount", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "purchase_items", "ppn_percent", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "purchase_items", "ppn_amount", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "purchase_items", "sale_price_suggested", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "audit_logs", "role", "TEXT");
                EnsureColumn(c, tx, "audit_logs", "discount", "REAL");
                EnsureColumn(c, tx, "audit_logs", "medicine_id", "INTEGER");
                EnsureColumn(c, tx, "audit_logs", "batch_no", "TEXT");
                EnsureColumn(c, tx, "audit_logs", "qty_in", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "audit_logs", "qty_out", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(c, tx, "audit_logs", "old_value", "TEXT");
                EnsureColumn(c, tx, "audit_logs", "new_value", "TEXT");
                EnsureColumn(c, tx, "audit_logs", "status", "TEXT NOT NULL DEFAULT 'BERHASIL'");

                // Salin histori tabel v4 ke struktur v5 tanpa menghapus data lama.
                // Matikan foreign_keys sementara agar migrasi data lama tidak gagal
                // karena referensi medicine_id/supplier_id yang mungkin tidak konsisten.
                Exec(c, "PRAGMA foreign_keys=OFF;", tx);

                Exec(c, @"INSERT OR IGNORE INTO medicine_batches(id,medicine_id,batch_number,expired_date,qty_in,qty_out,qty_available,purchase_detail_id,created_at)
SELECT id,medicine_id,batch_no,expiry_date,qty_received,(qty_received-qty_remaining),qty_remaining,purchase_item_id,created_at
FROM medicine_lots;", tx);

                Exec(c, @"INSERT OR IGNORE INTO stock_movements(id,medicine_id,movement_datetime,movement_type,reference_type,reference_id,transaction_code,qty_in,qty_out,stock_before,stock_after,user_id,reason,note)
SELECT id,medicine_id,created_at,mutation_type,reference_type,reference_id,report_code,
       CASE WHEN mutation_type IN ('MASUK','STOK_AWAL','PENYESUAIAN_MASUK') THEN qty ELSE 0 END,
       CASE WHEN mutation_type IN ('KELUAR','PENYESUAIAN_KELUAR') THEN qty ELSE 0 END,
       stock_before,stock_after,user_id,
       CASE WHEN mutation_type LIKE 'PENYESUAIAN%' THEN note ELSE NULL END,note
FROM stock_mutations;", tx);

                Exec(c, @"UPDATE purchase_items
SET medicine_name = IFNULL(medicine_name,(SELECT name FROM medicines WHERE medicines.id=purchase_items.medicine_id)),
    medicine_unit = IFNULL(medicine_unit,(SELECT unit FROM medicines WHERE medicines.id=purchase_items.medicine_id)),
    invoice_price = CASE WHEN invoice_price = 0 THEN purchase_price ELSE invoice_price END,
    discount_amount = CASE WHEN discount_amount = 0 THEN 0 ELSE discount_amount END,
    ppn_percent = CASE WHEN ppn_percent = 0 THEN 0 ELSE ppn_percent END,
    ppn_amount = CASE WHEN ppn_amount = 0 THEN 0 ELSE ppn_amount END,
    sale_price_suggested = CASE WHEN sale_price_suggested = 0 THEN ROUND(purchase_price * 1.2, 0) ELSE sale_price_suggested END;", tx);

                Exec(c, "UPDATE sales SET grand_total=total WHERE grand_total IS NULL;", tx);

                Exec(c, @"UPDATE sales
SET report_date = CASE
    WHEN time(transaction_time) < '15:00:00' THEN substr(transaction_time,1,10)
    ELSE date(substr(transaction_time,1,10), '+1 day')
END
WHERE report_date IS NULL OR report_date = '' OR report_date <> CASE
    WHEN time(transaction_time) < '15:00:00' THEN substr(transaction_time,1,10)
    ELSE date(substr(transaction_time,1,10), '+1 day')
END;", tx);

                Exec(c, @"UPDATE purchases
SET purchase_date = CASE
    WHEN time(purchase_time) < '15:00:00' THEN substr(purchase_time,1,10)
    ELSE date(substr(purchase_time,1,10), '+1 day')
END
WHERE purchase_date IS NULL OR purchase_date = '' OR purchase_date <> CASE
    WHEN time(purchase_time) < '15:00:00' THEN substr(purchase_time,1,10)
    ELSE date(substr(purchase_time,1,10), '+1 day')
END;", tx);

                // Aktifkan kembali foreign_keys setelah migrasi data lama selesai
                Exec(c, "PRAGMA foreign_keys=ON;", tx);

                Exec(c, @"
CREATE INDEX IF NOT EXISTS idx_medicine_name ON medicines(name);
CREATE INDEX IF NOT EXISTS idx_medicine_code ON medicines(code);
CREATE INDEX IF NOT EXISTS idx_sales_report_date ON sales(report_date);
CREATE INDEX IF NOT EXISTS idx_sales_payment ON sales(report_date,payment_method,status);
CREATE INDEX IF NOT EXISTS idx_stock_movement_medicine ON stock_movements(medicine_id,movement_datetime);
CREATE INDEX IF NOT EXISTS idx_batch_fefo ON medicine_batches(medicine_id,expired_date,id);
CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_logs(created_at);
CREATE INDEX IF NOT EXISTS idx_audit_user ON audit_logs(user_id,created_at);
CREATE INDEX IF NOT EXISTS idx_stock_opname_status ON stock_opnames(status,started_at);
CREATE INDEX IF NOT EXISTS idx_stock_opname_item ON stock_opname_items(opname_id,medicine_id,batch_id);
", tx);

                if (Convert.ToInt32(Scalar(c, tx, "SELECT COUNT(*) FROM (SELECT report_date,report_code,COUNT(*) c FROM sales GROUP BY report_date,report_code HAVING c>1)")) == 0)
                    Exec(c, "CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_report_code ON sales(report_date,report_code);", tx);

                EnsureDefaultAccounts(c, tx);
                Exec(c, "UPDATE schema_info SET version=" + CurrentSchemaVersion + ";", tx);
                SeedInitialMedicines(c, tx);
                tx.Commit();
            }
        }

        private static void EnsureDefaultAccounts(SQLiteConnection c, SQLiteTransaction tx)
        {
            EnsureDefaultAccount(c, tx, "SUPER_ADMIN", DefaultSuperAdminUsername, DefaultSuperAdminDisplay, DefaultSuperAdminPassword, true, true, "admin1", "admin");
            EnsureDefaultAccount(c, tx, "KARYAWAN", DefaultEmployeeUsername, DefaultEmployeeDisplay, DefaultEmployeePassword, true, false, "karyawan");
        }

        private static void EnsureDefaultAccount(SQLiteConnection c, SQLiteTransaction tx, string role, string username, string displayName, string password, bool canPurchase, bool canAdjustStock, params string[] legacyUsernames)
        {
            var id = FindUserId(c, tx, username);
            if (id.HasValue) return;

            for (var i = 0; i < legacyUsernames.Length; i++)
            {
                id = FindUserId(c, tx, legacyUsernames[i]);
                if (id.HasValue) break;
            }

            if (id.HasValue)
            {
                using (var cmd = new SQLiteCommand(@"UPDATE users SET username=@u,display_name=@d,role=@r,password_hash=@p,
is_active=1,can_purchase=@cp,can_adjust_stock=@ca,updated_at=@now WHERE id=@id", c, tx))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@d", displayName);
                    cmd.Parameters.AddWithValue("@r", role);
                    cmd.Parameters.AddWithValue("@p", PasswordHasher.Hash(password));
                    cmd.Parameters.AddWithValue("@cp", canPurchase ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ca", canAdjustStock ? 1 : 0);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                    cmd.Parameters.AddWithValue("@id", id.Value);
                    cmd.ExecuteNonQuery();
                }
                return;
            }

            SeedUser(c, tx, username, displayName, role, password, canPurchase, canAdjustStock);
        }

        private static long? FindUserId(SQLiteConnection c, SQLiteTransaction tx, string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            using (var cmd = new SQLiteCommand("SELECT id FROM users WHERE username=@u LIMIT 1", c, tx))
            {
                cmd.Parameters.AddWithValue("@u", username);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return null;
                return Convert.ToInt64(result);
            }
        }

        private static void SeedInitialMedicines(SQLiteConnection c, SQLiteTransaction tx)
        {
            using (var check = new SQLiteCommand("SELECT COUNT(*) FROM medicines", c, tx))
            {
                var medCount = Convert.ToInt32(check.ExecuteScalar());
                if (medCount > 0)
                {
                    // Di data launching ada 462 obat yang punya stok (>0).
                    // Jika obat yang punya stok < 400, berarti database ini masih berisi data testing lama.
                    var countWithStock = Convert.ToInt32(Scalar(c, tx, "SELECT COUNT(*) FROM medicines WHERE (awl+msk-klr) > 0"));
                    if (countWithStock >= 400) return;

                    // Bersihkan data obat dummy lama beserta relasinya agar data launching masuk bersih
                    Exec(c, "DELETE FROM sale_item_batches;", tx);
                    Exec(c, "DELETE FROM sale_items;", tx);
                    Exec(c, "DELETE FROM sales;", tx);
                    Exec(c, "DELETE FROM purchase_items;", tx);
                    Exec(c, "DELETE FROM purchases;", tx);
                    Exec(c, "DELETE FROM stock_opname_items;", tx);
                    Exec(c, "DELETE FROM stock_opnames;", tx);
                    Exec(c, "DELETE FROM stock_movements;", tx);
                    Exec(c, "DELETE FROM stock_mutations;", tx);
                    Exec(c, "DELETE FROM medicine_batches;", tx);
                    Exec(c, "DELETE FROM medicine_lots;", tx);
                    Exec(c, "DELETE FROM medicines;", tx);
                }
            }

            var asm = typeof(Database).Assembly;
            var resName = "ApotekFirdanFarma.Data.initial_medicines.csv";
            using (var stream = asm.GetManifestResourceStream(resName))
            {
                if (stream == null) return;
                using (var reader = new StreamReader(stream))
                {
                    var header = reader.ReadLine();
                    if (header == null) return;

                    var now = DateTime.Now.ToString("s");
                    using (var cmd = new SQLiteCommand(@"INSERT INTO medicines(name,unit,purchase_price,sale_price,awl,msk,klr,is_active,created_at,updated_at)
VALUES(@name,@unit,@hb,@hj,@awl,@msk,@klr,1,@now,@now); SELECT last_insert_rowid();", c, tx))
                    using (var bCmd = new SQLiteCommand(@"INSERT INTO medicine_batches(medicine_id,batch_number,expired_date,qty_in,qty_out,qty_available,supplier_id,created_at)
VALUES(@m,'STOK-AWAL',NULL,@q,0,@q,NULL,@now); SELECT last_insert_rowid();", c, tx))
                    using (var smCmd = new SQLiteCommand(@"INSERT INTO stock_movements(medicine_id,batch_id,movement_datetime,movement_type,reference_type,reference_id,qty_in,qty_out,stock_before,stock_after,user_id,note)
VALUES(@m,@b,@now,'STOK_AWAL','MEDICINE',@m,@q,0,0,@q,NULL,'Stok awal launching');", c, tx))
                    {
                        cmd.Parameters.Add("@name", DbType.String);
                        cmd.Parameters.Add("@unit", DbType.String);
                        cmd.Parameters.Add("@hb", DbType.Double);
                        cmd.Parameters.Add("@hj", DbType.Double);
                        cmd.Parameters.Add("@awl", DbType.Int32);
                        cmd.Parameters.Add("@msk", DbType.Int32);
                        cmd.Parameters.Add("@klr", DbType.Int32);
                        cmd.Parameters.Add("@now", DbType.String);
                        cmd.Parameters["@now"].Value = now;

                        bCmd.Parameters.Add("@m", DbType.Int64);
                        bCmd.Parameters.Add("@q", DbType.Int32);
                        bCmd.Parameters.Add("@now", DbType.String);
                        bCmd.Parameters["@now"].Value = now;

                        smCmd.Parameters.Add("@m", DbType.Int64);
                        smCmd.Parameters.Add("@b", DbType.Int64);
                        smCmd.Parameters.Add("@now", DbType.String);
                        smCmd.Parameters.Add("@q", DbType.Int32);
                        smCmd.Parameters["@now"].Value = now;

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var parts = ParseCsvLine(line);
                            if (parts.Length < 4) continue;

                            cmd.Parameters["@name"].Value = parts[0].Trim();
                            cmd.Parameters["@unit"].Value = parts[1].Trim();

                            double hb;
                            double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out hb);
                            cmd.Parameters["@hb"].Value = hb;

                            double hj;
                            double.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out hj);
                            cmd.Parameters["@hj"].Value = hj;

                            int awl = 0;
                            if (parts.Length > 4) int.TryParse(parts[4], out awl);
                            cmd.Parameters["@awl"].Value = awl;

                            int msk = 0;
                            if (parts.Length > 5) int.TryParse(parts[5], out msk);
                            cmd.Parameters["@msk"].Value = msk;

                            int klr = 0;
                            if (parts.Length > 6) int.TryParse(parts[6], out klr);
                            cmd.Parameters["@klr"].Value = klr;

                            var medId = Convert.ToInt64(cmd.ExecuteScalar());
                            var sisa = awl + msk - klr;
                            if (sisa > 0)
                            {
                                bCmd.Parameters["@m"].Value = medId;
                                bCmd.Parameters["@q"].Value = sisa;
                                var batchId = Convert.ToInt64(bCmd.ExecuteScalar());

                                smCmd.Parameters["@m"].Value = medId;
                                smCmd.Parameters["@b"].Value = batchId;
                                smCmd.Parameters["@q"].Value = sisa;
                                smCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var inQuotes = false;
            var current = new System.Text.StringBuilder();
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (ch == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private static void CreatePreMigrationBackupIfNeeded()
        {
            using (var source = Open())
            {
                using (var check = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_info'", source))
                {
                    if (Convert.ToInt32(check.ExecuteScalar()) == 0) return;
                }
                int version;
                using (var v = new SQLiteCommand("SELECT IFNULL(MAX(version),1) FROM schema_info", source)) version = Convert.ToInt32(v.ExecuteScalar());
                if (version >= CurrentSchemaVersion) return;
                var dest = Path.Combine(AppPaths.BackupDirectory, "pre_migration_v" + version + "_to_v" + CurrentSchemaVersion + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
                using (var target = new SQLiteConnection("Data Source=" + dest + ";Version=3;"))
                {
                    target.Open(); source.BackupDatabase(target, "main", "main", -1, null, 0);
                }
            }
        }

        private static void SeedUser(SQLiteConnection c, SQLiteTransaction tx, string username, string display, string role, string password, bool canPurchase, bool canAdjust)
        {
            using (var check = new SQLiteCommand("SELECT COUNT(*) FROM users WHERE username=@u", c, tx))
            {
                check.Parameters.AddWithValue("@u", username);
                if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;
            }
            using (var cmd = new SQLiteCommand(@"INSERT INTO users(username,display_name,role,password_hash,is_active,can_purchase,can_adjust_stock,created_at,updated_at)
VALUES(@u,@d,@r,@p,1,@cp,@ca,@now,@now)", c, tx))
            {
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@d", display);
                cmd.Parameters.AddWithValue("@r", role);
                cmd.Parameters.AddWithValue("@p", PasswordHasher.Hash(password));
                cmd.Parameters.AddWithValue("@cp", canPurchase ? 1 : 0);
                cmd.Parameters.AddWithValue("@ca", canAdjust ? 1 : 0);
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureColumn(SQLiteConnection c, SQLiteTransaction tx, string table, string column, string definition)
        {
            if (HasColumn(c, tx, table, column)) return;
            using (var cmd = new SQLiteCommand("ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition, c, tx)) cmd.ExecuteNonQuery();
        }

        private static bool HasColumn(SQLiteConnection c, SQLiteTransaction tx, string table, string column)
        {
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", c, tx))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) if (string.Equals(Convert.ToString(r["name"]), column, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static object Scalar(SQLiteConnection c, SQLiteTransaction tx, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, c, tx)) return cmd.ExecuteScalar();
        }

        private static void Exec(SQLiteConnection c, string sql, SQLiteTransaction tx)
        {
            using (var cmd = new SQLiteCommand(sql, c, tx)) cmd.ExecuteNonQuery();
        }

        public static DataTable Query(string sql, params SQLiteParameter[] parameters)
        {
            using (var c = Open())
            using (var cmd = new SQLiteCommand(sql, c))
            {
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                using (var a = new SQLiteDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    a.Fill(dt);
                    return dt;
                }
            }
        }

        public static bool HasActiveStockOpname()
        {
            using (var c = Open()) return HasActiveStockOpname(c, null);
        }

        public static bool HasActiveStockOpname(SQLiteConnection c, SQLiteTransaction tx)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM stock_opnames WHERE status IN ('DRAFT','MENUNGGU_PEMERIKSAAN')", c, tx))
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public static void RequireNoActiveStockOpname(SQLiteConnection c, SQLiteTransaction tx)
        {
            if (HasActiveStockOpname(c, tx))
                throw new InvalidOperationException("Stok opname sedang berjalan. Selesaikan atau batalkan stok opname sebelum melakukan transaksi stok.");
        }

    }
}
