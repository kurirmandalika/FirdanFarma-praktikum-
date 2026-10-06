using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public static class StockOpnameService
    {
        public static DataTable List(AppUser user)
        {
            UserService.RequireSuper(user);
            return Database.Query(@"SELECT so.id,so.opname_no AS Opname,so.started_at AS Mulai,
IFNULL(so.submitted_at,'-') AS Diajukan,IFNULL(so.completed_at,'-') AS Selesai,
so.status AS Status,IFNULL(started.username,'-') AS Petugas,
IFNULL(completed.username,'-') AS Pemeriksa,IFNULL(so.note,'') AS Catatan
FROM stock_opnames so JOIN users started ON started.id=so.started_by
LEFT JOIN users completed ON completed.id=so.completed_by
ORDER BY so.id DESC");
        }

        public static DataTable Items(long opnameId)
        {
            return Database.Query(@"SELECT soi.id,soi.opname_id,soi.medicine_id,soi.batch_id,
m.name AS Obat,IFNULL(m.unit,'-') AS Satuan,IFNULL(soi.batch_number,'Tanpa Batch') AS Batch,
IFNULL(soi.expired_date,'-') AS Expired,soi.system_qty AS [Stok Sistem],soi.physical_qty AS [Stok Fisik],
CASE WHEN soi.physical_qty IS NULL THEN NULL ELSE soi.physical_qty-soi.system_qty END AS Selisih,
CASE
    WHEN soi.physical_qty IS NULL THEN 'Belum dihitung'
    WHEN soi.physical_qty=soi.system_qty THEN 'Sesuai'
    WHEN soi.physical_qty<soi.system_qty THEN 'Stok berkurang'
    ELSE 'Stok bertambah'
END AS Status,
soi.unit_cost AS [Harga Beli],CASE WHEN soi.physical_qty IS NULL THEN NULL ELSE (soi.physical_qty-soi.system_qty)*soi.unit_cost END AS [Nilai Selisih],
IFNULL(soi.reason,'') AS Alasan,IFNULL(u.username,'-') AS Penghitung,
IFNULL(soi.counted_at,'-') AS [Waktu Hitung]
FROM stock_opname_items soi JOIN medicines m ON m.id=soi.medicine_id
LEFT JOIN users u ON u.id=soi.counted_by WHERE soi.opname_id=@id
ORDER BY m.name,IFNULL(soi.expired_date,'9999-12-31'),soi.batch_number,soi.id", new SQLiteParameter("@id", opnameId));
        }

        public static string PostingSummary(long opnameId)
        {
            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                var header = ReadHeader(c, tx, opnameId);
                if (header.Status != "DRAFT" && header.Status != "MENUNGGU_PEMERIKSAAN")
                    throw new InvalidOperationException("Hanya opname aktif yang dapat direview.");
                ValidateCountedItems(c, tx, opnameId);

                var items = ReadItemRows(c, tx, opnameId);
                var equal = 0; var less = 0; var more = 0;
                foreach (var item in items)
                {
                    var currentSystem = GetCurrentItemStock(c, tx, item);
                    var variance = item.PhysicalQty.Value - currentSystem;
                    if (variance == 0) equal++;
                    else if (variance < 0) less++;
                    else more++;
                }

                tx.Commit();
                return "Review posting stok opname " + header.OpnameNo + "\n\n" +
                       "Total item       : " + items.Count + "\n" +
                       "Sesuai           : " + equal + "\n" +
                       "Selisih kurang   : " + less + "\n" +
                       "Selisih lebih    : " + more + "\n\n" +
                       "Posting akan menghitung ulang selisih dari stok sistem terbaru saat ini.";
            }
        }

        public static string Status(long opnameId)
        {
            var dt = Database.Query("SELECT status FROM stock_opnames WHERE id=@id", new SQLiteParameter("@id", opnameId));
            return dt.Rows.Count == 0 ? null : Convert.ToString(dt.Rows[0][0]);
        }

        public static long Start(AppUser user, string note = null)
        {
            UserService.RequireSuper(user);
            note = (note ?? "").Trim();
            var now = DateTime.Now;

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                if (Database.HasActiveStockOpname(c, tx))
                    throw new InvalidOperationException("Masih ada stok opname yang belum selesai. Selesaikan atau batalkan opname tersebut terlebih dahulu.");

                var opnameNo = NextOpnameNo(c, tx, now);
                long opnameId;
                using (var cmd = new SQLiteCommand(@"INSERT INTO stock_opnames(opname_no,started_at,status,started_by,note,created_at)
VALUES(@no,@now,'DRAFT',@user,@note,@now); SELECT last_insert_rowid();", c, tx))
                {
                    cmd.Parameters.AddWithValue("@no", opnameNo);
                    cmd.Parameters.AddWithValue("@now", now.ToString("s"));
                    cmd.Parameters.AddWithValue("@user", user.Id);
                    cmd.Parameters.AddWithValue("@note", note.Length == 0 ? (object)DBNull.Value : note);
                    opnameId = Convert.ToInt64(cmd.ExecuteScalar());
                }

                using (var medicines = new SQLiteCommand("SELECT id, name, purchase_price, awl+msk-klr AS stock FROM medicines WHERE is_active=1 ORDER BY name", c, tx))
                {
                    var rows = new List<MedicineSnapshot>();
                    using (var reader = medicines.ExecuteReader())
                    {
                        while (reader.Read()) rows.Add(new MedicineSnapshot
                        {
                            Id = Convert.ToInt64(reader["id"]),
                            Name = Convert.ToString(reader["name"]),
                            PurchasePrice = Convert.ToDouble(reader["purchase_price"]),
                            Stock = Convert.ToInt32(reader["stock"])
                        });
                    }

                    foreach (var medicine in rows) AddMedicineItems(c, tx, opnameId, medicine);
                }

                AuditService.LogDetailed(user, "MULAI_STOK_OPNAME", "stock_opname", opnameId, opnameNo,
                    null, "status=DRAFT", null, null, null, note, "Sesi stok opname dibuat", "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
                return opnameId;
            }
        }

        public static void SaveCount(AppUser user, long itemId, int physicalQty, string reason)
        {
            UserService.RequireSuper(user);
            if (physicalQty < 0) throw new InvalidOperationException("Stok fisik tidak boleh negatif.");

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                string opnameNo, status, oldPhysical, batchNumber;
                long medicineId; long? batchId; int systemQty;
                using (var q = new SQLiteCommand(@"SELECT so.opname_no,so.status,soi.medicine_id,soi.batch_id,soi.batch_number,soi.system_qty,soi.physical_qty
FROM stock_opname_items soi JOIN stock_opnames so ON so.id=soi.opname_id WHERE soi.id=@id", c, tx))
                {
                    q.Parameters.AddWithValue("@id", itemId);
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Item stok opname tidak ditemukan.");
                        opnameNo = Convert.ToString(r["opname_no"]); status = Convert.ToString(r["status"]);
                        medicineId = Convert.ToInt64(r["medicine_id"]); batchNumber = Convert.ToString(r["batch_number"]);
                        batchId = r["batch_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["batch_id"]);
                        systemQty = Convert.ToInt32(r["system_qty"]);
                        oldPhysical = r["physical_qty"] == DBNull.Value ? "belum diisi" : Convert.ToString(r["physical_qty"]);
                    }
                }
                if (status != "DRAFT") throw new InvalidOperationException("Hitungan hanya dapat diubah saat opname masih berstatus DRAFT.");

                var trimmedReason = (reason ?? "").Trim();
                using (var u = new SQLiteCommand(@"UPDATE stock_opname_items SET physical_qty=@qty,variance=@variance,reason=@reason,counted_by=@user,counted_at=@now WHERE id=@id", c, tx))
                {
                    u.Parameters.AddWithValue("@qty", physicalQty); u.Parameters.AddWithValue("@variance", physicalQty - systemQty);
                    u.Parameters.AddWithValue("@reason", trimmedReason.Length == 0 ? (object)DBNull.Value : trimmedReason);
                    u.Parameters.AddWithValue("@user", user.Id); u.Parameters.AddWithValue("@now", DateTime.Now.ToString("s")); u.Parameters.AddWithValue("@id", itemId);
                    u.ExecuteNonQuery();
                }
                AuditService.LogDetailed(user, "HITUNG_STOK_OPNAME", "stock_opname_item", itemId, opnameNo,
                    "fisik=" + oldPhysical, "fisik=" + physicalQty + "; sistem=" + systemQty + "; selisih=" + (physicalQty - systemQty),
                    null, null, null, trimmedReason, "Hitungan stok fisik disimpan", "BERHASIL", medicineId, batchNumber, 0, 0, c, tx);
                tx.Commit();
            }
        }

        public static void Complete(AppUser user, long opnameId)
        {
            UserService.RequireSuper(user);
            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                var header = ReadHeader(c, tx, opnameId);
                if (header.Status != "MENUNGGU_PEMERIKSAAN" && header.Status != "DRAFT") throw new InvalidOperationException("Opname sudah selesai atau dibatalkan.");

                using (var guard = new SQLiteCommand("SELECT COUNT(*) FROM stock_movements WHERE reference_type=@rt AND reference_id=@id", c, tx))
                {
                    guard.Parameters.AddWithValue("@rt", "STOCK_OPNAME");
                    guard.Parameters.AddWithValue("@id", opnameId);
                    var existing = Convert.ToInt32(guard.ExecuteScalar());
                    if (existing > 0)
                        throw new InvalidOperationException("Stok opname ini sudah diposting. Tidak dapat membuat movement stok opname ganda.");
                }

                ValidateCountedItems(c, tx, opnameId);
                var items = ReadItemRows(c, tx, opnameId);

                var now = DateTime.Now.ToString("s");
                foreach (var item in items)
                {
                    var currentSystemQty = GetCurrentItemStock(c, tx, item);
                    var variance = item.PhysicalQty.Value - currentSystemQty;
                    if (variance != 0 && string.IsNullOrWhiteSpace(item.Reason))
                        throw new InvalidOperationException("Item selisih wajib diberi alasan: " + item.MedicineName + " / batch " + (string.IsNullOrWhiteSpace(item.BatchNumber) ? "Tanpa Batch" : item.BatchNumber) + ".");

                    using (var uItem = new SQLiteCommand("UPDATE stock_opname_items SET system_qty=@system,variance=@variance WHERE id=@id", c, tx))
                    {
                        uItem.Parameters.AddWithValue("@system", currentSystemQty);
                        uItem.Parameters.AddWithValue("@variance", variance);
                        uItem.Parameters.AddWithValue("@id", item.Id);
                        uItem.ExecuteNonQuery();
                    }

                    if (variance == 0) continue;
                    var currentStock = GetMedicineStock(c, tx, item.MedicineId);
                    var after = currentStock + variance;
                    if (after < 0) throw new InvalidOperationException("Hasil opname membuat stok negatif untuk " + item.MedicineName + ".");
                    if (variance < 0 && item.BatchId.HasValue)
                    {
                        using (var q = new SQLiteCommand("SELECT qty_available FROM medicine_batches WHERE id=@id AND medicine_id=@m", c, tx))
                        {
                            q.Parameters.AddWithValue("@id", item.BatchId.Value); q.Parameters.AddWithValue("@m", item.MedicineId);
                            var available = q.ExecuteScalar();
                            if (available == null || available == DBNull.Value || Convert.ToInt32(available) < Math.Abs(variance))
                                throw new InvalidOperationException("Selisih batch " + item.BatchNumber + " melebihi stok batch yang tersedia.");
                        }
                    }

                    if (item.BatchId.HasValue)
                    {
                        var batchSql = variance > 0
                            ? "UPDATE medicine_batches SET qty_in=qty_in+@q,qty_available=qty_available+@q WHERE id=@id"
                            : "UPDATE medicine_batches SET qty_out=qty_out+@q,qty_available=qty_available-@q WHERE id=@id AND qty_available>=@q";
                        using (var b = new SQLiteCommand(batchSql, c, tx))
                        { b.Parameters.AddWithValue("@q", Math.Abs(variance)); b.Parameters.AddWithValue("@id", item.BatchId.Value); if (b.ExecuteNonQuery() == 0) throw new InvalidOperationException("Batch tidak dapat diperbarui."); }
                    }
                    using (var m = new SQLiteCommand(variance > 0
                        ? "UPDATE medicines SET msk=msk+@q,updated_at=@now WHERE id=@id"
                        : "UPDATE medicines SET klr=klr+@q,updated_at=@now WHERE id=@id", c, tx))
                    { m.Parameters.AddWithValue("@q", Math.Abs(variance)); m.Parameters.AddWithValue("@now", now); m.Parameters.AddWithValue("@id", item.MedicineId); m.ExecuteNonQuery(); }

                    MedicineService.InsertMovement(c, tx, item.MedicineId, item.BatchId,
                        "STOK_OPNAME", "STOCK_OPNAME", opnameId, header.OpnameNo,
                        variance > 0 ? variance : 0, variance < 0 ? Math.Abs(variance) : 0, currentStock, after, null, user.Id,
                        item.Reason, "Penyesuaian hasil stok opname", now);
                    AuditService.LogDetailed(user, "SELESAI_STOK_OPNAME_ITEM", "stock_opname_item", item.Id, header.OpnameNo,
                        "sistem=" + currentSystemQty + "; fisik=" + item.PhysicalQty.Value,
                        "selisih=" + variance + "; saldo=" + after, null, null, null, item.Reason,
                        "Mutasi selisih stok opname", "BERHASIL", item.MedicineId, item.BatchNumber,
                        variance > 0 ? variance : 0, variance < 0 ? Math.Abs(variance) : 0, c, tx);
                }

                using (var u = new SQLiteCommand("UPDATE stock_opnames SET status='SELESAI',completed_at=@now,completed_by=@user WHERE id=@id", c, tx))
                { u.Parameters.AddWithValue("@now", now); u.Parameters.AddWithValue("@user", user.Id); u.Parameters.AddWithValue("@id", opnameId); u.ExecuteNonQuery(); }
                AuditService.LogDetailed(user, "SELESAI_STOK_OPNAME", "stock_opname", opnameId, header.OpnameNo,
                    "status=" + header.Status, "status=SELESAI", null, null, null, null, "Stok opname disetujui dan selisih diposting", "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
            }
        }

        public static void Cancel(AppUser user, long opnameId, string reason)
        {
            UserService.RequireSuper(user);
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Alasan pembatalan opname wajib diisi.");
            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                var header = ReadHeader(c, tx, opnameId);
                if (header.Status != "DRAFT" && header.Status != "MENUNGGU_PEMERIKSAAN") throw new InvalidOperationException("Opname ini sudah selesai atau dibatalkan.");
                var now = DateTime.Now.ToString("s");
                using (var u = new SQLiteCommand("UPDATE stock_opnames SET status='BATAL',completed_at=@now,completed_by=@user,note=CASE WHEN IFNULL(note,'')='' THEN @reason ELSE note || ' | Batal: ' || @reason END WHERE id=@id", c, tx))
                { u.Parameters.AddWithValue("@now", now); u.Parameters.AddWithValue("@user", user.Id); u.Parameters.AddWithValue("@reason", reason.Trim()); u.Parameters.AddWithValue("@id", opnameId); u.ExecuteNonQuery(); }
                AuditService.LogDetailed(user, "BATAL_STOK_OPNAME", "stock_opname", opnameId, header.OpnameNo,
                    "status=" + header.Status, "status=BATAL", null, null, null, reason.Trim(), "Sesi stok opname dibatalkan", "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
            }
        }

        private static void AddMedicineItems(SQLiteConnection c, SQLiteTransaction tx, long opnameId, MedicineSnapshot medicine)
        {
            var batchTotal = 0; var hasBatch = false;
            var batches = new List<OpnameBatch>();
            using (var q = new SQLiteCommand("SELECT id,batch_number,expired_date,qty_available FROM medicine_batches WHERE medicine_id=@id ORDER BY CASE WHEN expired_date IS NULL OR expired_date='' THEN 1 ELSE 0 END,expired_date,id", c, tx))
            {
                q.Parameters.AddWithValue("@id", medicine.Id);
                using (var r = q.ExecuteReader())
                {
                    while (r.Read())
                    {
                        hasBatch = true;
                        var qty = Convert.ToInt32(r["qty_available"]); batchTotal += qty;
                        batches.Add(new OpnameBatch { Id = Convert.ToInt64(r["id"]), BatchNumber = Convert.ToString(r["batch_number"]), ExpiredDate = Convert.ToString(r["expired_date"]), Qty = qty });
                    }
                }
            }
            foreach (var batch in batches)
                InsertItem(c, tx, opnameId, medicine.Id, batch.Id, batch.BatchNumber, batch.ExpiredDate, batch.Qty, medicine.PurchasePrice);
            var legacy = medicine.Stock - batchTotal;
            if (legacy < 0) throw new InvalidOperationException("Data stok batch tidak konsisten untuk " + medicine.Name + ". Periksa kartu stok sebelum memulai opname.");
            if (legacy > 0 || !hasBatch) InsertItem(c, tx, opnameId, medicine.Id, null, null, null, legacy, medicine.PurchasePrice);
        }

        private static void InsertItem(SQLiteConnection c, SQLiteTransaction tx, long opnameId, long medicineId, long? batchId, string batchNumber, string expiredDate, int qty, double unitCost)
        {
            using (var cmd = new SQLiteCommand(@"INSERT INTO stock_opname_items(opname_id,medicine_id,batch_id,batch_number,expired_date,system_qty,unit_cost)
VALUES(@opname,@medicine,@batch,@batchNo,@expired,@qty,@cost)", c, tx))
            {
                cmd.Parameters.AddWithValue("@opname", opnameId); cmd.Parameters.AddWithValue("@medicine", medicineId);
                cmd.Parameters.AddWithValue("@batch", (object)batchId ?? DBNull.Value); cmd.Parameters.AddWithValue("@batchNo", (object)batchNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@expired", string.IsNullOrWhiteSpace(expiredDate) ? (object)DBNull.Value : expiredDate);
                cmd.Parameters.AddWithValue("@qty", qty); cmd.Parameters.AddWithValue("@cost", unitCost); cmd.ExecuteNonQuery();
            }
        }

        private static string NextOpnameNo(SQLiteConnection c, SQLiteTransaction tx, DateTime now)
        {
            var prefix = "SO-" + now.ToString("yyyyMMdd") + "-";
            using (var q = new SQLiteCommand("SELECT IFNULL(MAX(CAST(SUBSTR(opname_no,@start) AS INTEGER)),0) FROM stock_opnames WHERE opname_no LIKE @prefix", c, tx))
            {
                q.Parameters.AddWithValue("@start", prefix.Length + 1);
                q.Parameters.AddWithValue("@prefix", prefix + "%");
                return prefix + (Convert.ToInt32(q.ExecuteScalar()) + 1).ToString("000");
            }
        }

        private static void ValidateCountedItems(SQLiteConnection c, SQLiteTransaction tx, long opnameId)
        {
            using (var q = new SQLiteCommand("SELECT COUNT(*) FROM stock_opname_items WHERE opname_id=@id AND physical_qty IS NULL", c, tx))
            { q.Parameters.AddWithValue("@id", opnameId); if (Convert.ToInt32(q.ExecuteScalar()) > 0) throw new InvalidOperationException("Masih ada item yang belum dihitung."); }
        }

        private static int GetMedicineStock(SQLiteConnection c, SQLiteTransaction tx, long medicineId)
        {
            using (var q = new SQLiteCommand("SELECT awl+msk-klr FROM medicines WHERE id=@id", c, tx))
            { q.Parameters.AddWithValue("@id", medicineId); return Convert.ToInt32(q.ExecuteScalar()); }
        }

        private static int GetCurrentItemStock(SQLiteConnection c, SQLiteTransaction tx, OpnameItem item)
        {
            using (var medicine = new SQLiteCommand("SELECT awl+msk-klr FROM medicines WHERE id=@id AND is_active=1", c, tx))
            {
                medicine.Parameters.AddWithValue("@id", item.MedicineId);
                var total = medicine.ExecuteScalar();
                if (total == null || total == DBNull.Value)
                    throw new InvalidOperationException("Obat opname tidak ditemukan atau sudah nonaktif: " + item.MedicineName + ".");
            }

            if (item.BatchId.HasValue)
            {
                using (var q = new SQLiteCommand("SELECT qty_available FROM medicine_batches WHERE id=@id AND medicine_id=@m", c, tx))
                {
                    q.Parameters.AddWithValue("@id", item.BatchId.Value);
                    q.Parameters.AddWithValue("@m", item.MedicineId);
                    var value = q.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("Batch opname tidak ditemukan untuk " + item.MedicineName + ".");
                    return Convert.ToInt32(value);
                }
            }

            using (var q = new SQLiteCommand(@"SELECT (SELECT awl+msk-klr FROM medicines WHERE id=@m) -
IFNULL((SELECT SUM(qty_available) FROM medicine_batches WHERE medicine_id=@m),0)", c, tx))
            {
                q.Parameters.AddWithValue("@m", item.MedicineId);
                var value = q.ExecuteScalar();
                var legacy = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
                return Math.Max(0, legacy);
            }
        }

        private static OpnameHeader ReadHeader(SQLiteConnection c, SQLiteTransaction tx, long id)
        {
            using (var q = new SQLiteCommand("SELECT opname_no,status FROM stock_opnames WHERE id=@id", c, tx))
            {
                q.Parameters.AddWithValue("@id", id);
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("Stok opname tidak ditemukan.");
                    return new OpnameHeader { OpnameNo = Convert.ToString(r["opname_no"]), Status = Convert.ToString(r["status"]) };
                }
            }
        }

        private static List<OpnameItem> ReadItemRows(SQLiteConnection c, SQLiteTransaction tx, long opnameId)
        {
            var result = new List<OpnameItem>();
            using (var q = new SQLiteCommand(@"SELECT soi.id,soi.medicine_id,soi.batch_id,soi.batch_number,soi.system_qty,soi.physical_qty,soi.reason,m.name
FROM stock_opname_items soi JOIN medicines m ON m.id=soi.medicine_id WHERE soi.opname_id=@id ORDER BY soi.id", c, tx))
            {
                q.Parameters.AddWithValue("@id", opnameId);
                using (var r = q.ExecuteReader())
                    while (r.Read()) result.Add(new OpnameItem
                    {
                        Id = Convert.ToInt64(r["id"]), MedicineId = Convert.ToInt64(r["medicine_id"]),
                        BatchId = r["batch_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["batch_id"]),
                        BatchNumber = Convert.ToString(r["batch_number"]), SystemQty = Convert.ToInt32(r["system_qty"]),
                        PhysicalQty = r["physical_qty"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["physical_qty"]),
                        Reason = Convert.ToString(r["reason"]), MedicineName = Convert.ToString(r["name"])
                    });
            }
            return result;
        }

        private class MedicineSnapshot { public long Id; public string Name; public double PurchasePrice; public int Stock; }
        private class OpnameBatch { public long Id; public string BatchNumber; public string ExpiredDate; public int Qty; }
        private class OpnameHeader { public string OpnameNo; public string Status; }
        private class OpnameItem
        {
            public long Id; public long MedicineId; public long? BatchId; public string BatchNumber; public int SystemQty; public int? PhysicalQty; public string Reason; public string MedicineName;
        }
    }
}
