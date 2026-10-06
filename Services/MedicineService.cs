using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public static class MedicineService
    {
        private static string NormalizeKey(string value)
        {
            var text = (value ?? "").Trim().ToUpperInvariant();
            if (text.Length == 0) return "";
            var chars = new System.Text.StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch)) chars.Append(ch);
            }
            return chars.ToString();
        }

        public static double CalculateFinalPurchasePrice(double invoicePrice, double discountAmount, bool hasPpn, double ppnPercent = 11d)
        {
            var baseAmount = Math.Max(0d, invoicePrice - discountAmount);
            if (!hasPpn) return baseAmount;
            return baseAmount + (baseAmount * (ppnPercent / 100d));
        }

        public static double SuggestSalePrice(double purchasePrice)
        {
            var raw = Math.Max(0d, purchasePrice * 1.2d);
            return Math.Ceiling(raw / 100d) * 100d;
        }

        public static DataTable List(string search = "", bool includeInactive = false)
        {
            var sql = @"SELECT id, code AS Kode, name AS [Nama Barang], unit AS SATUAN, awl AS AWL, msk AS MSK, klr AS KLR,
(awl+msk-klr) AS SISA, purchase_price AS [Harga Beli], sale_price AS [Harga Jual], min_stock AS [Stok Minimal],
category AS Kategori, CASE is_active WHEN 1 THEN 'Aktif' ELSE 'Nonaktif' END AS Status
FROM medicines WHERE (@all=1 OR is_active=1) AND (name LIKE @contains COLLATE NOCASE OR IFNULL(code,'') LIKE @contains COLLATE NOCASE OR IFNULL(category,'') LIKE @contains COLLATE NOCASE OR IFNULL(unit,'') LIKE @contains COLLATE NOCASE)
ORDER BY CASE
    WHEN @term='' THEN 0
    WHEN UPPER(name)=UPPER(@term) THEN 0
    WHEN UPPER(IFNULL(code,''))=UPPER(@term) THEN 1
    WHEN name LIKE @prefix COLLATE NOCASE THEN 2
    WHEN IFNULL(code,'') LIKE @prefix COLLATE NOCASE THEN 3
    WHEN INSTR(UPPER(name),UPPER(@term))>0 THEN 4
    WHEN INSTR(UPPER(IFNULL(code,'')),UPPER(@term))>0 THEN 5
    WHEN IFNULL(category,'') LIKE @contains COLLATE NOCASE THEN 6
    WHEN IFNULL(unit,'') LIKE @contains COLLATE NOCASE THEN 7
    ELSE 8
END,
CASE WHEN @term='' THEN name ELSE INSTR(UPPER(name),UPPER(@term)) END,
name";
            var term = (search ?? "").Trim();
            return Database.Query(sql,
                new SQLiteParameter("@all", includeInactive ? 1 : 0),
                new SQLiteParameter("@term", term),
                new SQLiteParameter("@prefix", term.Length == 0 ? "%" : term + "%"),
                new SQLiteParameter("@contains", term.Length == 0 ? "%" : "%" + term + "%"));
        }

        public static int SellableStock(long medicineId)
        {
            var medicine = Database.Query(@"SELECT awl+msk-klr AS total_stock FROM medicines WHERE id=@id AND is_active=1",
                new SQLiteParameter("@id", medicineId));
            if (medicine.Rows.Count == 0) return 0;

            var batches = Database.Query(@"SELECT qty_available,expired_date FROM medicine_batches WHERE medicine_id=@id AND qty_available>0",
                new SQLiteParameter("@id", medicineId));

            var totalStock = Convert.ToInt32(medicine.Rows[0]["total_stock"]);
            var batchStock = 0;
            var sellableBatchStock = 0;
            foreach (DataRow row in batches.Rows)
            {
                var qty = Convert.ToInt32(row["qty_available"]);
                batchStock += qty;
                if (IsBatchSellable(Convert.ToString(row["expired_date"]), DateTime.Today))
                    sellableBatchStock += qty;
            }
            var legacyStock = Math.Max(0, totalStock - batchStock);
            return sellableBatchStock + legacyStock;
        }

        internal static DateTime? ParseBatchExpiry(string expiredDateText)
        {
            if (string.IsNullOrWhiteSpace(expiredDateText)) return null;
            DateTime parsed;
            var text = expiredDateText.Trim();
            var formats = new[]
            {
                "yyyy-MM-dd","yyyy/MM/dd","yyyy.M.d","yyyy.M.dd","yyyy.MM.dd",
                "dd/MM/yyyy","d/M/yyyy","dd-MM-yyyy","d-M-yyyy",
                "MM/dd/yyyy","M/d/yyyy"
            };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;
            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;
            return null;
        }

        internal static bool IsBatchSellable(string expiredDateText, DateTime today)
        {
            var parsed = ParseBatchExpiry(expiredDateText);
            return !parsed.HasValue || parsed.Value.Date >= today.Date;
        }

        internal static long FindMedicineIdByName(SQLiteConnection c, SQLiteTransaction tx, string name)
        {
            var key = NormalizeKey(name);
            if (key.Length == 0) return 0;
            using (var q = new SQLiteCommand(@"SELECT id FROM medicines
WHERE REPLACE(REPLACE(REPLACE(UPPER(IFNULL(name,'')),' ',''),'-',''),'.','')=@n
   OR REPLACE(REPLACE(REPLACE(UPPER(IFNULL(code,'')),' ',''),'-',''),'.','')=@n
LIMIT 1", c, tx))
            {
                q.Parameters.AddWithValue("@n", key);
                var o = q.ExecuteScalar();
                if (o != null && o != DBNull.Value) return Convert.ToInt64(o);
            }
            return 0;
        }

        internal static long UpsertMedicineFromInvoice(SQLiteConnection c, SQLiteTransaction tx, AppUser user, long? medicineId, string name, string unit, string category, string supplier, double purchasePrice, double salePrice, bool allowCreate, out bool created)
        {
            created = false;
            name = (name ?? "").Trim();
            unit = (unit ?? "").Trim();
            category = (category ?? "").Trim();
            supplier = (supplier ?? "").Trim();
            if (name.Length == 0) throw new InvalidOperationException("Nama obat wajib diisi.");
            if (purchasePrice < 0 || salePrice < 0) throw new InvalidOperationException("Harga tidak boleh negatif.");

            var now = DateTime.Now.ToString("s");
            var id = medicineId.HasValue && medicineId.Value > 0 ? medicineId.Value : FindMedicineIdByName(c, tx, name);
            long categoryId = MasterService.EnsureCategory(c, tx, category);
            long supplierId = MasterService.EnsureSupplier(c, tx, supplier);

            if (id > 0)
            {
                using (var cmd = new SQLiteCommand(@"UPDATE medicines SET
name=@name,
unit=CASE WHEN IFNULL(@unit,'')='' THEN unit ELSE @unit END,
category_id=CASE WHEN @catid=0 THEN category_id ELSE @catid END,
supplier_id=CASE WHEN @supid=0 THEN supplier_id ELSE @supid END,
purchase_price=@hb,
sale_price=@hj,
category=CASE WHEN IFNULL(@cat,'')='' THEN category ELSE @cat END,
supplier=CASE WHEN IFNULL(@sup,'')='' THEN supplier ELSE @sup END,
is_active=1,
updated_at=@now
WHERE id=@id", c, tx))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@unit", unit);
                    cmd.Parameters.AddWithValue("@catid", categoryId);
                    cmd.Parameters.AddWithValue("@supid", supplierId);
                    cmd.Parameters.AddWithValue("@hb", purchasePrice);
                    cmd.Parameters.AddWithValue("@hj", salePrice);
                    cmd.Parameters.AddWithValue("@cat", category);
                    cmd.Parameters.AddWithValue("@sup", supplier);
                    cmd.Parameters.AddWithValue("@now", now);
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0) throw new InvalidOperationException("Obat tidak ditemukan.");
                }
                return id;
            }

            if (!allowCreate) throw new InvalidOperationException("Obat belum ada di master.");
            created = true;
            using (var cmd = new SQLiteCommand(@"INSERT INTO medicines(code,name,category_id,supplier_id,unit,awl,msk,klr,purchase_price,sale_price,min_stock,category,supplier,is_active,created_at,updated_at)
VALUES(@code,@name,@catid,@supid,@unit,0,0,0,@hb,@hj,0,@cat,@sup,1,@now,@now); SELECT last_insert_rowid();", c, tx))
            {
                cmd.Parameters.AddWithValue("@code", DBNull.Value);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@catid", categoryId == 0 ? (object)DBNull.Value : categoryId);
                cmd.Parameters.AddWithValue("@supid", supplierId == 0 ? (object)DBNull.Value : supplierId);
                cmd.Parameters.AddWithValue("@unit", unit);
                cmd.Parameters.AddWithValue("@hb", purchasePrice);
                cmd.Parameters.AddWithValue("@hj", salePrice);
                cmd.Parameters.AddWithValue("@cat", category);
                cmd.Parameters.AddWithValue("@sup", supplier);
                cmd.Parameters.AddWithValue("@now", now);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public static long Save(AppUser user, long id, string code, string name, string unit, int awl, int msk, int klr, double purchasePrice, double salePrice, int minStock, string category, string supplier, bool allowStockOverwrite = false)
        {
            if (user == null || !user.IsSuperAdmin) throw new UnauthorizedAccessException("Data master obat hanya dapat diubah oleh Super Admin.");
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Nama barang wajib diisi.");
            if (awl < 0 || msk < 0 || klr < 0 || awl + msk - klr < 0) throw new InvalidOperationException("Stok tidak valid. SISA tidak boleh negatif.");
            if (purchasePrice < 0 || salePrice < 0) throw new InvalidOperationException("Harga tidak boleh negatif.");

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                long entityId;
                var now = DateTime.Now.ToString("s");
                var oldSnapshot = id == 0 ? null : Snapshot(c, tx, id);
                long categoryId = MasterService.EnsureCategory(c, tx, category);
                long supplierId = MasterService.EnsureSupplier(c, tx, supplier);

                if (id == 0)
                {
                    using (var cmd = new SQLiteCommand(@"INSERT INTO medicines(code,name,category_id,supplier_id,unit,awl,msk,klr,purchase_price,sale_price,min_stock,category,supplier,is_active,created_at,updated_at)
VALUES(@code,@name,@catid,@supid,@unit,@awl,@msk,@klr,@hb,@hj,@min,@cat,@sup,1,@now,@now); SELECT last_insert_rowid();", c, tx))
                    {
                        Add(cmd, code, name, unit, awl, msk, klr, purchasePrice, salePrice, minStock, category, supplier, categoryId, supplierId, now);
                        entityId = Convert.ToInt64(cmd.ExecuteScalar());
                    }
                    var sisa = awl + msk - klr;
                    if (sisa > 0)
                    {
                        long batchId;
                        using (var b = new SQLiteCommand(@"INSERT INTO medicine_batches(medicine_id,batch_number,expired_date,qty_in,qty_out,qty_available,supplier_id,created_at)
VALUES(@m,'STOK-AWAL',NULL,@q,0,@q,NULL,@now); SELECT last_insert_rowid();", c, tx))
                        { b.Parameters.AddWithValue("@m", entityId); b.Parameters.AddWithValue("@q", sisa); b.Parameters.AddWithValue("@now", now); batchId = Convert.ToInt64(b.ExecuteScalar()); }
                        InsertMovement(c, tx, entityId, batchId, "STOK_AWAL", "MEDICINE", entityId, null, sisa, 0, 0, sisa, null, user.Id, null, "Pembuatan data obat", now);
                    }
                    AuditService.LogDetailed(user, "TAMBAH_OBAT", "medicine", entityId, code, null, Snapshot(c, tx, entityId), null, null, null, null, "Tambah obat", "BERHASIL", entityId, "STOK-AWAL", sisa, 0, c, tx);
                }
                else
                {
                    if (!allowStockOverwrite)
                    {
                        using (var q = new SQLiteCommand("SELECT awl,msk,klr FROM medicines WHERE id=@id", c, tx))
                        {
                            q.Parameters.AddWithValue("@id", id);
                            using (var r = q.ExecuteReader())
                            {
                                if (!r.Read()) throw new InvalidOperationException("Obat tidak ditemukan.");
                                awl = Convert.ToInt32(r["awl"]); msk = Convert.ToInt32(r["msk"]); klr = Convert.ToInt32(r["klr"]);
                            }
                        }
                    }
                    using (var cmd = new SQLiteCommand(@"UPDATE medicines SET code=@code,name=@name,category_id=@catid,supplier_id=@supid,unit=@unit,awl=@awl,msk=@msk,klr=@klr,purchase_price=@hb,sale_price=@hj,min_stock=@min,category=@cat,supplier=@sup,updated_at=@now WHERE id=@id", c, tx))
                    {
                        Add(cmd, code, name, unit, awl, msk, klr, purchasePrice, salePrice, minStock, category, supplier, categoryId, supplierId, now);
                        cmd.Parameters.AddWithValue("@id", id);
                        if (cmd.ExecuteNonQuery() == 0) throw new InvalidOperationException("Obat tidak ditemukan.");
                    }
                    entityId = id;
                    AuditService.LogDetailed(user, "UBAH_OBAT", "medicine", entityId, code, oldSnapshot, Snapshot(c, tx, entityId), null, null, null, null, "Ubah data obat", "BERHASIL", entityId, null, 0, 0, c, tx);
                }
                tx.Commit();
                return entityId;
            }
        }

        private static void Add(SQLiteCommand cmd, string code, string name, string unit, int awl, int msk, int klr, double hb, double hj, int min, string cat, string sup, long catId, long supId, string now)
        {
            cmd.Parameters.AddWithValue("@code", string.IsNullOrWhiteSpace(code) ? (object)DBNull.Value : code.Trim());
            cmd.Parameters.AddWithValue("@name", name.Trim());
            cmd.Parameters.AddWithValue("@catid", catId == 0 ? (object)DBNull.Value : catId);
            cmd.Parameters.AddWithValue("@supid", supId == 0 ? (object)DBNull.Value : supId);
            cmd.Parameters.AddWithValue("@unit", (object)(unit ?? "").Trim());
            cmd.Parameters.AddWithValue("@awl", awl); cmd.Parameters.AddWithValue("@msk", msk); cmd.Parameters.AddWithValue("@klr", klr);
            cmd.Parameters.AddWithValue("@hb", hb); cmd.Parameters.AddWithValue("@hj", hj); cmd.Parameters.AddWithValue("@min", min);
            cmd.Parameters.AddWithValue("@cat", (object)(cat ?? "").Trim()); cmd.Parameters.AddWithValue("@sup", (object)(sup ?? "").Trim()); cmd.Parameters.AddWithValue("@now", now);
        }

        public static void SetActive(AppUser user, long id, bool active)
        {
            UserService.RequireSuper(user);
            using (var c = Database.Open()) using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                var old = Snapshot(c, tx, id);
                using (var cmd = new SQLiteCommand("UPDATE medicines SET is_active=@a,updated_at=@now WHERE id=@id", c, tx))
                { cmd.Parameters.AddWithValue("@a", active ? 1 : 0); cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("s")); cmd.Parameters.AddWithValue("@id", id); if (cmd.ExecuteNonQuery() == 0) throw new InvalidOperationException("Obat tidak ditemukan."); }
                AuditService.LogDetailed(user, active ? "AKTIFKAN_OBAT" : "NONAKTIFKAN_OBAT", "medicine", id, null, old, Snapshot(c, tx, id), null, null, null, null, active ? "Aktifkan obat" : "Soft delete obat", "BERHASIL", id, null, 0, 0, c, tx);
                tx.Commit();
            }
        }

        public static void AdjustStock(AppUser user, long medicineId, int delta, string reason)
        {
            if (user == null || !user.CanAdjustStockEffective) throw new UnauthorizedAccessException("Anda tidak memiliki izin koreksi stok.");
            if (delta == 0) throw new InvalidOperationException("Jumlah penyesuaian tidak boleh 0.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Alasan penyesuaian wajib diisi.");

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                int before;
                using (var q = new SQLiteCommand("SELECT awl+msk-klr FROM medicines WHERE id=@id AND is_active=1", c, tx))
                { q.Parameters.AddWithValue("@id", medicineId); var o = q.ExecuteScalar(); if (o == null || o == DBNull.Value) throw new InvalidOperationException("Obat tidak ditemukan."); before = Convert.ToInt32(o); }
                var after = before + delta;
                if (after < 0) throw new InvalidOperationException("Stok tidak boleh negatif.");
                var now = DateTime.Now;

                if (delta > 0)
                {
                    using (var u = new SQLiteCommand("UPDATE medicines SET msk=msk+@q,updated_at=@now WHERE id=@id", c, tx))
                    { u.Parameters.AddWithValue("@q", delta); u.Parameters.AddWithValue("@now", now.ToString("s")); u.Parameters.AddWithValue("@id", medicineId); u.ExecuteNonQuery(); }
                    var batchNo = "ADJ-" + now.ToString("yyyyMMddHHmmss");
                    long batchId;
                    using (var b = new SQLiteCommand(@"INSERT INTO medicine_batches(medicine_id,batch_number,qty_in,qty_out,qty_available,created_at)
VALUES(@m,@batch,@q,0,@q,@now); SELECT last_insert_rowid();", c, tx))
                    { b.Parameters.AddWithValue("@m", medicineId); b.Parameters.AddWithValue("@batch", batchNo); b.Parameters.AddWithValue("@q", delta); b.Parameters.AddWithValue("@now", now.ToString("s")); batchId = Convert.ToInt64(b.ExecuteScalar()); }
                    InsertMovement(c, tx, medicineId, batchId, "PENYESUAIAN_MASUK", "ADJUSTMENT", null, null, delta, 0, before, after, null, user.Id, reason.Trim(), "Koreksi stok", now.ToString("s"));
                    AuditService.LogDetailed(user, "PENYESUAIAN_STOK", "medicine", medicineId, null, "stok=" + before, "stok=" + after, null, null, null, reason, "Stok masuk manual", "BERHASIL", medicineId, batchNo, delta, 0, c, tx);
                }
                else
                {
                    var takeTotal = Math.Abs(delta);
                    using (var u = new SQLiteCommand("UPDATE medicines SET klr=klr+@q,updated_at=@now WHERE id=@id", c, tx))
                    { u.Parameters.AddWithValue("@q", takeTotal); u.Parameters.AddWithValue("@now", now.ToString("s")); u.Parameters.AddWithValue("@id", medicineId); u.ExecuteNonQuery(); }
                    var remaining = takeTotal; var running = before;
                    var batches = GetAvailableBatches(c, tx, medicineId);
                    foreach (var b in batches)
                    {
                        if (remaining <= 0) break;
                        var take = Math.Min(remaining, b.QtyAvailable);
                        using (var u = new SQLiteCommand("UPDATE medicine_batches SET qty_out=qty_out+@q,qty_available=qty_available-@q WHERE id=@id", c, tx))
                        { u.Parameters.AddWithValue("@q", take); u.Parameters.AddWithValue("@id", b.Id); u.ExecuteNonQuery(); }
                        InsertMovement(c, tx, medicineId, b.Id, "PENYESUAIAN_KELUAR", "ADJUSTMENT", null, null, 0, take, running, running - take, null, user.Id, reason.Trim(), "Koreksi stok FEFO", now.ToString("s"));
                        AuditService.LogDetailed(user, "PENYESUAIAN_STOK", "medicine", medicineId, null, "stok=" + running, "stok=" + (running - take), null, null, null, reason, "Stok keluar manual", "BERHASIL", medicineId, b.BatchNumber, 0, take, c, tx);
                        running -= take; remaining -= take;
                    }
                    if (remaining > 0)
                    {
                        InsertMovement(c, tx, medicineId, null, "PENYESUAIAN_KELUAR", "ADJUSTMENT", null, null, 0, remaining, running, running - remaining, null, user.Id, reason.Trim(), "Koreksi stok tanpa batch legacy", now.ToString("s"));
                        AuditService.LogDetailed(user, "PENYESUAIAN_STOK", "medicine", medicineId, null, "stok=" + running, "stok=" + (running - remaining), null, null, null, reason, "Stok keluar tanpa batch legacy", "BERHASIL", medicineId, null, 0, remaining, c, tx);
                    }
                }
                tx.Commit();
            }
        }

        public static DataTable Batches(long medicineId)
        {
            return Database.Query(@"SELECT b.id,b.batch_number AS Batch,b.expired_date AS Expired,b.qty_in AS Masuk,b.qty_out AS Keluar,b.qty_available AS Sisa,
IFNULL(s.name,'-') AS Supplier,b.created_at AS Dibuat
FROM medicine_batches b LEFT JOIN suppliers s ON s.id=b.supplier_id WHERE b.medicine_id=@id ORDER BY CASE WHEN b.expired_date IS NULL OR b.expired_date='' THEN 1 ELSE 0 END,b.expired_date,b.id", new SQLiteParameter("@id", medicineId));
        }


        public static void CorrectBatch(AppUser user, long batchId, string batchNumber, DateTime? expiredDate, string reason)
        {
            UserService.RequireSuper(user);
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Alasan koreksi batch wajib diisi.");
            batchNumber = (batchNumber ?? "").Trim();
            if (batchNumber.Length == 0) throw new InvalidOperationException("Nomor batch wajib diisi.");
            using (var c = Database.Open()) using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                long medicineId; string oldBatch; string oldExpired;
                using (var q = new SQLiteCommand("SELECT medicine_id,batch_number,expired_date FROM medicine_batches WHERE id=@id", c, tx))
                {
                    q.Parameters.AddWithValue("@id", batchId);
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Batch tidak ditemukan.");
                        medicineId = Convert.ToInt64(r["medicine_id"]); oldBatch = Convert.ToString(r["batch_number"]); oldExpired = Convert.ToString(r["expired_date"]);
                    }
                }
                var newExpired = expiredDate.HasValue ? expiredDate.Value.ToString("yyyy-MM-dd") : null;
                using (var u = new SQLiteCommand("UPDATE medicine_batches SET batch_number=@b,expired_date=@e WHERE id=@id", c, tx))
                { u.Parameters.AddWithValue("@b", batchNumber); u.Parameters.AddWithValue("@e", (object)newExpired ?? DBNull.Value); u.Parameters.AddWithValue("@id", batchId); u.ExecuteNonQuery(); }
                AuditService.LogDetailed(user, "KOREKSI_BATCH", "medicine_batch", batchId, batchNumber,
                    "batch=" + oldBatch + "; expired=" + oldExpired,
                    "batch=" + batchNumber + "; expired=" + (newExpired ?? ""), null, null, null, reason, "Koreksi batch/expired", "BERHASIL", medicineId, batchNumber, 0, 0, c, tx);
                if (!string.Equals(oldExpired, newExpired ?? "", StringComparison.OrdinalIgnoreCase))
                    AuditService.LogDetailed(user, "UBAH_EXPIRED_DATE", "medicine_batch", batchId, batchNumber, oldExpired, newExpired, null, null, null, reason, "Perubahan expired date", "BERHASIL", medicineId, batchNumber, 0, 0, c, tx);
                tx.Commit();
            }
        }

        private class BatchInfo { public long Id; public string BatchNumber; public int QtyAvailable; }
        private static List<BatchInfo> GetAvailableBatches(SQLiteConnection c, SQLiteTransaction tx, long medicineId)
        {
            var list = new List<BatchInfo>();
            using (var q = new SQLiteCommand(@"SELECT id,batch_number,qty_available FROM medicine_batches WHERE medicine_id=@m AND qty_available>0
ORDER BY CASE WHEN expired_date IS NULL OR expired_date='' THEN 1 ELSE 0 END,expired_date,id", c, tx))
            {
                q.Parameters.AddWithValue("@m", medicineId);
                using (var r = q.ExecuteReader()) while (r.Read()) list.Add(new BatchInfo { Id = Convert.ToInt64(r["id"]), BatchNumber = Convert.ToString(r["batch_number"]), QtyAvailable = Convert.ToInt32(r["qty_available"]) });
            }
            return list;
        }

        internal static void InsertMovement(SQLiteConnection c, SQLiteTransaction tx, long medicineId, long? batchId, string type, string referenceType, long? referenceId, string code, int qtyIn, int qtyOut, int before, int after, string payment, long userId, string reason, string note, string when)
        {
            using (var m = new SQLiteCommand(@"INSERT INTO stock_movements(medicine_id,batch_id,movement_datetime,movement_type,reference_type,reference_id,transaction_code,qty_in,qty_out,stock_before,stock_after,payment_method,user_id,reason,note)
VALUES(@med,@batch,@when,@type,@rt,@rid,@code,@qin,@qout,@before,@after,@pay,@u,@reason,@note)", c, tx))
            {
                m.Parameters.AddWithValue("@med", medicineId); m.Parameters.AddWithValue("@batch", (object)batchId ?? DBNull.Value); m.Parameters.AddWithValue("@when", when);
                m.Parameters.AddWithValue("@type", type); m.Parameters.AddWithValue("@rt", (object)referenceType ?? DBNull.Value); m.Parameters.AddWithValue("@rid", (object)referenceId ?? DBNull.Value);
                m.Parameters.AddWithValue("@code", (object)code ?? DBNull.Value); m.Parameters.AddWithValue("@qin", qtyIn); m.Parameters.AddWithValue("@qout", qtyOut);
                m.Parameters.AddWithValue("@before", before); m.Parameters.AddWithValue("@after", after); m.Parameters.AddWithValue("@pay", (object)payment ?? DBNull.Value);
                m.Parameters.AddWithValue("@u", userId); m.Parameters.AddWithValue("@reason", (object)reason ?? DBNull.Value); m.Parameters.AddWithValue("@note", (object)note ?? DBNull.Value); m.ExecuteNonQuery();
            }
        }

        private static string Snapshot(SQLiteConnection c, SQLiteTransaction tx, long id)
        {
            using (var cmd = new SQLiteCommand(@"SELECT code,name,unit,awl,msk,klr,purchase_price,sale_price,min_stock,category,supplier,is_active FROM medicines WHERE id=@id", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("Obat tidak ditemukan.");
                    return "kode=" + Convert.ToString(r["code"]) + "; nama=" + Convert.ToString(r["name"]) + "; satuan=" + Convert.ToString(r["unit"]) +
                           "; AWL=" + r["awl"] + "; MSK=" + r["msk"] + "; KLR=" + r["klr"] + "; HB=" + r["purchase_price"] + "; HJ=" + r["sale_price"] +
                           "; min=" + r["min_stock"] + "; kategori=" + Convert.ToString(r["category"]) + "; supplier=" + Convert.ToString(r["supplier"]) + "; aktif=" + r["is_active"];
                }
            }
        }
    }
}
