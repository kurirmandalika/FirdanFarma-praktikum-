using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public class SaleLine
    {
        public long MedicineId;
        public string Name;
        public int Qty;
        public double UnitPrice;
    }

    public class PurchaseLine
    {
        public long MedicineId;
        public string Name;
        public string Unit;
        public int Qty;
        public double InvoicePrice;
        public double DiscountAmount;
        public double PpnPercent;
        public double PpnAmount;
        public double PurchasePrice;
        public double SalePrice;
        public string BatchNo;
        public DateTime? ExpiryDate;
    }

    public class SaleResult
    {
        public long SaleId { get; set; }
        public string TransactionNumber { get; set; }
        public string ReportCode { get; set; }
        public DateTime ReportDate { get; set; }
        public double Subtotal { get; set; }
        public double Discount { get; set; }
        public double GrandTotal { get; set; }
        public string PaymentMethod { get; set; }
        public override string ToString() { return TransactionNumber + " / " + ReportCode; }
    }

    public static class TransactionService
    {
        public static DateTime GetReportDate(DateTime when)
        {
            return when.TimeOfDay < new TimeSpan(15, 0, 0) ? when.Date : when.Date.AddDays(1);
        }

        private static string Prefix(string payment)
        {
            var p = (payment ?? "").Trim().ToUpperInvariant();
            if (p == "TUNAI") return "T";
            if (p == "QRIS") return "Q";
            throw new InvalidOperationException("Metode pembayaran wajib Tunai atau QRIS.");
        }

        private static string NextReportCode(SQLiteConnection c, SQLiteTransaction tx, DateTime reportDate, string payment)
        {
            var prefix = Prefix(payment);
            using (var cmd = new SQLiteCommand(@"SELECT IFNULL(MAX(CAST(SUBSTR(report_code,2) AS INTEGER)),0)
FROM sales WHERE report_date=@d AND payment_method=@pay AND report_code LIKE @p", c, tx))
            {
                cmd.Parameters.AddWithValue("@d", reportDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@pay", payment.ToUpperInvariant());
                cmd.Parameters.AddWithValue("@p", prefix + "%");
                return prefix + (Convert.ToInt32(cmd.ExecuteScalar()) + 1);
            }
        }

        public static SaleResult SaveSale(AppUser user, IList<SaleLine> lines, double discount, string payment, string note = null)
        {
            if (user == null) throw new UnauthorizedAccessException("Sesi pengguna tidak valid.");
            if (lines == null || lines.Count == 0) throw new InvalidOperationException("Keranjang masih kosong.");
            note = (note ?? "").Trim();
            payment = (payment ?? "").Trim().ToUpperInvariant();
            Prefix(payment); // validasi Tunai/QRIS.

            var now = DateTime.Now;
            var reportDate = GetReportDate(now);
            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                var reportCode = NextReportCode(c, tx, reportDate, payment);
                var trxNo = "PJ-" + now.ToString("yyyyMMdd-HHmmssfff") + "-" + user.Id;
                double subtotal = 0;
                var snapshots = new List<SaleSnapshot>();

                foreach (var l in lines)
                {
                    if (l == null || l.Qty <= 0) throw new InvalidOperationException("Jumlah jual harus lebih dari 0.");
                    using (var q = new SQLiteCommand(@"SELECT name,awl+msk-klr AS stock,purchase_price,sale_price,
IFNULL((SELECT SUM(qty_available) FROM medicine_batches b WHERE b.medicine_id=medicines.id),0) AS batch_stock
FROM medicines WHERE id=@id AND is_active=1", c, tx))
                    {
                        q.Parameters.AddWithValue("@id", l.MedicineId);
                        using (var r = q.ExecuteReader())
                        {
                            if (!r.Read()) throw new InvalidOperationException("Obat tidak ditemukan/nonaktif: " + l.Name);
                            var stock = Convert.ToInt32(r["stock"]);
                            var batchStock = Convert.ToInt32(r["batch_stock"]);
                            var sellableBatchStock = GetSellableBatchStock(c, tx, l.MedicineId, now.Date);
                            var legacyStock = Math.Max(0, stock - batchStock);
                            var sellableStock = sellableBatchStock + legacyStock;
                            if (l.Qty > sellableStock)
                                throw new InvalidOperationException("Stok layak jual " + Convert.ToString(r["name"]) + " tidak cukup. Tersedia " + sellableStock + ". Batch expired tidak dapat dijual.");
                            var price = l.UnitPrice > 0 ? l.UnitPrice : Convert.ToDouble(r["sale_price"]);
                            if (price < 0) throw new InvalidOperationException("Harga jual tidak valid.");
                            var cost = Convert.ToDouble(r["purchase_price"]);
                            snapshots.Add(new SaleSnapshot { Line = l, StockBefore = stock, UnitCost = cost, MedicineName = Convert.ToString(r["name"]) });
                            l.UnitPrice = price;
                            subtotal += price * l.Qty;
                        }
                    }
                }

                if (discount < 0) throw new InvalidOperationException("Diskon tidak boleh negatif.");
                if (discount > subtotal) throw new InvalidOperationException("Diskon tidak boleh melebihi subtotal.");
                var grandTotal = subtotal - discount;

                long saleId;
                using (var cmd = new SQLiteCommand(@"INSERT INTO sales(transaction_no,transaction_time,report_date,report_code,payment_method,subtotal,discount,total,grand_total,user_id,status,note,created_at)
VALUES(@no,@time,@date,@code,@pay,@sub,@disc,@total,@total,@u,'FINAL',@note,@now); SELECT last_insert_rowid();", c, tx))
                {
                    cmd.Parameters.AddWithValue("@no", trxNo);
                    cmd.Parameters.AddWithValue("@time", now.ToString("s"));
                    cmd.Parameters.AddWithValue("@date", reportDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@code", reportCode);
                    cmd.Parameters.AddWithValue("@pay", payment);
                    cmd.Parameters.AddWithValue("@sub", subtotal);
                    cmd.Parameters.AddWithValue("@disc", discount);
                    cmd.Parameters.AddWithValue("@total", grandTotal);
                    cmd.Parameters.AddWithValue("@u", user.Id);
                    cmd.Parameters.AddWithValue("@note", (object)(note ?? "").Trim());
                    cmd.Parameters.AddWithValue("@now", now.ToString("s"));
                    saleId = Convert.ToInt64(cmd.ExecuteScalar());
                }

                foreach (var snap in snapshots)
                {
                    var l = snap.Line;
                    long saleItemId;
                    using (var si = new SQLiteCommand(@"INSERT INTO sale_items(sale_id,medicine_id,batch_id,qty,unit_price,unit_cost,subtotal)
VALUES(@s,@m,NULL,@q,@p,@c,@sub); SELECT last_insert_rowid();", c, tx))
                    {
                        si.Parameters.AddWithValue("@s", saleId); si.Parameters.AddWithValue("@m", l.MedicineId); si.Parameters.AddWithValue("@q", l.Qty);
                        si.Parameters.AddWithValue("@p", l.UnitPrice); si.Parameters.AddWithValue("@c", snap.UnitCost); si.Parameters.AddWithValue("@sub", l.UnitPrice * l.Qty);
                        saleItemId = Convert.ToInt64(si.ExecuteScalar());
                    }

                    using (var u = new SQLiteCommand("UPDATE medicines SET klr=klr+@q,updated_at=@now WHERE id=@id", c, tx))
                    {
                        u.Parameters.AddWithValue("@q", l.Qty); u.Parameters.AddWithValue("@now", now.ToString("s")); u.Parameters.AddWithValue("@id", l.MedicineId); u.ExecuteNonQuery();
                    }
                    ConsumeBatchesFefo(c, tx, user, saleId, saleItemId, l.MedicineId, snap.MedicineName, l.Qty, snap.StockBefore, reportCode, payment, now);
                }

                AuditService.LogDetailed(user, "PENJUALAN", "sale", saleId, reportCode, null,
                    "subtotal=" + subtotal + "; diskon=" + discount + "; total=" + grandTotal,
                    grandTotal, discount, payment, null, trxNo, "BERHASIL", null, null, 0, 0, c, tx);
                if (discount > 0)
                    AuditService.LogDetailed(user, "PEMBERIAN_DISKON", "sale", saleId, reportCode, "diskon=0", "diskon=" + discount, grandTotal, discount, payment, null, "Diskon transaksi", "BERHASIL", null, null, 0, 0, c, tx);

                tx.Commit();
                return new SaleResult
                {
                    SaleId = saleId,
                    TransactionNumber = trxNo,
                    ReportCode = reportCode,
                    ReportDate = reportDate,
                    Subtotal = subtotal,
                    Discount = discount,
                    GrandTotal = grandTotal,
                    PaymentMethod = payment
                };
            }
        }

        private class SaleSnapshot
        {
            public SaleLine Line;
            public int StockBefore;
            public double UnitCost;
            public string MedicineName;
        }

        private class BatchSnapshot
        {
            public long Id;
            public string BatchNumber;
            public string ExpiredDate;
            public int QtyAvailable;
            public DateTime? ParsedExpiry;
        }

        private static void ConsumeBatchesFefo(SQLiteConnection c, SQLiteTransaction tx, AppUser user, long saleId, long saleItemId, long medicineId, string medicineName, int qty, int stockBefore, string reportCode, string payment, DateTime now)
        {
            var remaining = qty;
            var running = stockBefore;
            var batches = new List<BatchSnapshot>();
            using (var q = new SQLiteCommand(@"SELECT id,batch_number,expired_date,qty_available FROM medicine_batches
WHERE medicine_id=@m AND qty_available>0", c, tx))
            {
                q.Parameters.AddWithValue("@m", medicineId);
                using (var r = q.ExecuteReader())
                    while (r.Read()) batches.Add(new BatchSnapshot
                    {
                        Id = Convert.ToInt64(r["id"]), BatchNumber = Convert.ToString(r["batch_number"]),
                        ExpiredDate = Convert.ToString(r["expired_date"]), QtyAvailable = Convert.ToInt32(r["qty_available"]),
                        ParsedExpiry = MedicineService.ParseBatchExpiry(Convert.ToString(r["expired_date"]))
                    });
            }

            batches.RemoveAll(b => !MedicineService.IsBatchSellable(b.ExpiredDate, now.Date));
            batches.Sort((a, b) =>
            {
                var aKey = a.ParsedExpiry.HasValue ? 0 : 1;
                var bKey = b.ParsedExpiry.HasValue ? 0 : 1;
                var cmp = aKey.CompareTo(bKey);
                if (cmp != 0) return cmp;
                if (a.ParsedExpiry.HasValue && b.ParsedExpiry.HasValue)
                {
                    cmp = a.ParsedExpiry.Value.CompareTo(b.ParsedExpiry.Value);
                    if (cmp != 0) return cmp;
                }
                return a.Id.CompareTo(b.Id);
            });

            long? firstBatchId = null;
            int batchCount = 0;
            foreach (var batch in batches)
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, batch.QtyAvailable);
                using (var u = new SQLiteCommand("UPDATE medicine_batches SET qty_out=qty_out+@q,qty_available=qty_available-@q WHERE id=@id", c, tx))
                { u.Parameters.AddWithValue("@q", take); u.Parameters.AddWithValue("@id", batch.Id); u.ExecuteNonQuery(); }
                using (var map = new SQLiteCommand("INSERT INTO sale_item_batches(sale_item_id,batch_id,qty) VALUES(@si,@b,@q)", c, tx))
                { map.Parameters.AddWithValue("@si", saleItemId); map.Parameters.AddWithValue("@b", batch.Id); map.Parameters.AddWithValue("@q", take); map.ExecuteNonQuery(); }

                if (!firstBatchId.HasValue) firstBatchId = batch.Id;
                batchCount++;
                MedicineService.InsertMovement(c, tx, medicineId, batch.Id, "KELUAR", "SALE", saleId, reportCode, 0, take, running, running - take, payment, user.Id, null, "Penjualan FEFO", now.ToString("s"));
                AuditService.LogDetailed(user, "STOK_KELUAR", "medicine", medicineId, reportCode, "stok=" + running, "stok=" + (running - take), null, null, payment, null,
                    "Penjualan " + medicineName, "BERHASIL", medicineId, batch.BatchNumber, 0, take, c, tx);
                running -= take;
                remaining -= take;
            }

            if (remaining > 0)
            {
                // Mendukung data lama yang mempunyai stok tetapi belum punya batch.
                MedicineService.InsertMovement(c, tx, medicineId, null, "KELUAR", "SALE", saleId, reportCode, 0, remaining, running, running - remaining, payment, user.Id, null, "Penjualan stok legacy tanpa batch", now.ToString("s"));
                AuditService.LogDetailed(user, "STOK_KELUAR", "medicine", medicineId, reportCode, "stok=" + running, "stok=" + (running - remaining), null, null, payment, null,
                    "Penjualan stok legacy " + medicineName, "BERHASIL", medicineId, null, 0, remaining, c, tx);
            }

            if (batchCount == 1 && firstBatchId.HasValue)
                using (var u = new SQLiteCommand("UPDATE sale_items SET batch_id=@b WHERE id=@id", c, tx))
                { u.Parameters.AddWithValue("@b", firstBatchId.Value); u.Parameters.AddWithValue("@id", saleItemId); u.ExecuteNonQuery(); }
        }

        private static int GetSellableBatchStock(SQLiteConnection c, SQLiteTransaction tx, long medicineId, DateTime today)
        {
            var total = 0;
            using (var q = new SQLiteCommand("SELECT qty_available,expired_date FROM medicine_batches WHERE medicine_id=@m AND qty_available>0", c, tx))
            {
                q.Parameters.AddWithValue("@m", medicineId);
                using (var r = q.ExecuteReader())
                    while (r.Read())
                        if (MedicineService.IsBatchSellable(Convert.ToString(r["expired_date"]), today))
                            total += Convert.ToInt32(r["qty_available"]);
            }
            return total;
        }

        public static string SavePurchase(AppUser user, IList<PurchaseLine> lines, string supplier, string supplierInvoiceNumber = null, string note = null)
        {
            if (user == null || !user.CanPurchaseEffective) throw new UnauthorizedAccessException("Anda tidak memiliki izin input stok masuk.");
            return SavePurchase(user, lines, supplier, supplierInvoiceNumber, note, "FAKTUR");
        }

        public static string SavePurchase(AppUser user, IList<PurchaseLine> lines, string supplier, string supplierInvoiceNumber, string note, string entryType)
        {
            if (user == null || !user.CanPurchaseEffective) throw new UnauthorizedAccessException("Anda tidak memiliki izin input stok masuk.");
            return SavePurchaseInternal(user, lines, supplier, supplierInvoiceNumber, note, entryType, null, false, 0d);
        }

        public static string SavePurchase(AppUser user, IList<PurchaseLine> lines, string supplier, string supplierInvoiceNumber, string note, string entryType, bool invoiceHasPpn, double invoicePpnPercent)
        {
            if (user == null || !user.CanPurchaseEffective) throw new UnauthorizedAccessException("Anda tidak memiliki izin input stok masuk.");
            return SavePurchaseInternal(user, lines, supplier, supplierInvoiceNumber, note, entryType, null, invoiceHasPpn, invoicePpnPercent);
        }

        public static string UpdatePurchase(AppUser user, long purchaseId, IList<PurchaseLine> lines, string supplier, string supplierInvoiceNumber = null, string note = null)
        {
            UserService.RequireSuper(user);
            throw new InvalidOperationException("Stok masuk yang sudah tersimpan tidak dapat diedit. Gunakan koreksi stok atau buat pembetulan agar histori tetap utuh.");
        }

        private static string SavePurchaseInternal(AppUser user, IList<PurchaseLine> lines, string supplier, string supplierInvoiceNumber, string note, string entryType, long? editPurchaseId, bool invoiceHasPpn, double invoicePpnPercent)
        {
            if (lines == null || lines.Count == 0) throw new InvalidOperationException("Daftar stok masuk masih kosong.");
            note = (note ?? "").Trim();
            supplier = (supplier ?? "").Trim();
            supplierInvoiceNumber = (supplierInvoiceNumber ?? "").Trim();
            entryType = string.Equals(entryType, "TUNGGAL", System.StringComparison.OrdinalIgnoreCase) ? "TUNGGAL" : "FAKTUR";
            invoicePpnPercent = invoiceHasPpn ? invoicePpnPercent : 0d;
            if (invoicePpnPercent < 0) throw new InvalidOperationException("PPN faktur tidak valid.");

            var now = DateTime.Now;
            var no = "PB-" + now.ToString("yyyyMMdd-HHmmssfff") + "-" + user.Id;
            double baseTotal = 0;
            foreach (var l in lines)
            {
                if (invoiceHasPpn) l.PpnPercent = 0d;
                PreparePurchaseLine(l);
                baseTotal += l.Qty * l.PurchasePrice;
            }
            if (invoiceHasPpn && invoicePpnPercent > 0d) ApplyInvoicePpn(lines, baseTotal, invoicePpnPercent);
            double total = 0;
            foreach (var l in lines) total += l.Qty * l.PurchasePrice;

            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                var supplierId = MasterService.EnsureSupplier(c, tx, supplier);
                if (!editPurchaseId.HasValue && entryType == "FAKTUR" && supplierInvoiceNumber.Length > 0)
                {
                    using (var duplicate = new SQLiteCommand("SELECT COUNT(*) FROM purchases WHERE supplier_invoice_number=@invoice COLLATE NOCASE AND status='SELESAI'", c, tx))
                    {
                        duplicate.Parameters.AddWithValue("@invoice", supplierInvoiceNumber);
                        if (Convert.ToInt32(duplicate.ExecuteScalar()) > 0) throw new InvalidOperationException("No. faktur tersebut sudah pernah dimasukkan ke sistem.");
                    }
                }
                long purchaseId;
                string purchaseNo;
                var purchaseDate = GetReportDate(now);
                if (editPurchaseId.HasValue)
                {
                    purchaseId = editPurchaseId.Value;
                    purchaseNo = LoadPurchaseNoForEdit(c, tx, purchaseId);
                    ReversePurchaseForEdit(c, tx, purchaseId);
                    using (var cmd = new SQLiteCommand(@"UPDATE purchases SET supplier_id=@sid,supplier=@sup,supplier_invoice_number=@invoice,total=@total,note=@note,entry_type=@type WHERE id=@id", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@sid", supplierId == 0 ? (object)DBNull.Value : supplierId);
                        cmd.Parameters.AddWithValue("@sup", supplier);
                        cmd.Parameters.AddWithValue("@invoice", (object)(supplierInvoiceNumber ?? "").Trim());
                        cmd.Parameters.AddWithValue("@total", total);
                        cmd.Parameters.AddWithValue("@note", (object)(note ?? "").Trim());
                        cmd.Parameters.AddWithValue("@type", entryType);
                        cmd.Parameters.AddWithValue("@id", purchaseId);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    purchaseNo = no;
                    using (var cmd = new SQLiteCommand(@"INSERT INTO purchases(purchase_no,supplier_id,supplier,supplier_invoice_number,purchase_time,purchase_date,status,total,user_id,note,entry_type,created_at)
VALUES(@no,@sid,@sup,@invoice,@time,@date,'SELESAI',@total,@u,@note,@type,@now); SELECT last_insert_rowid();", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@no", no); cmd.Parameters.AddWithValue("@sid", supplierId == 0 ? (object)DBNull.Value : supplierId);
                        cmd.Parameters.AddWithValue("@sup", supplier); cmd.Parameters.AddWithValue("@invoice", (object)(supplierInvoiceNumber ?? "").Trim());
                        cmd.Parameters.AddWithValue("@time", now.ToString("s")); cmd.Parameters.AddWithValue("@date", purchaseDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@total", total); cmd.Parameters.AddWithValue("@u", user.Id); cmd.Parameters.AddWithValue("@note", (object)(note ?? "").Trim()); cmd.Parameters.AddWithValue("@type", entryType); cmd.Parameters.AddWithValue("@now", now.ToString("s"));
                        purchaseId = Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    InsertPurchaseLine(c, tx, user, lines[i], supplier, supplierId, purchaseId, purchaseNo, now, i + 1);
                }

                AuditService.LogDetailed(user, editPurchaseId.HasValue ? "EDIT_STOK_MASUK" : "STOK_MASUK", "purchase", purchaseId, purchaseNo, null,
                    "supplier=" + supplier + "; invoice=" + (supplierInvoiceNumber ?? "") + "; total=" + total,
                    total, null, null, null, note, "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
                return purchaseNo;
            }
        }

        private static void PreparePurchaseLine(PurchaseLine l)
        {
            if (l == null || l.Qty <= 0) throw new InvalidOperationException("Jumlah stok masuk harus lebih dari 0.");
            l.Name = (l.Name ?? "").Trim();
            l.Unit = (l.Unit ?? "").Trim();
            if (l.Name.Length == 0) throw new InvalidOperationException("Nama obat wajib diisi.");
            if (l.InvoicePrice <= 0 && l.PurchasePrice > 0) l.InvoicePrice = l.PurchasePrice;
            if (l.InvoicePrice < 0 || l.DiscountAmount < 0) throw new InvalidOperationException("Harga/diskon stok masuk tidak valid.");
            if (l.DiscountAmount > l.InvoicePrice) throw new InvalidOperationException("Diskon tidak boleh melebihi harga beli untuk " + l.Name + ".");
            if (l.PpnPercent < 0) throw new InvalidOperationException("PPN tidak valid.");
            if (l.ExpiryDate.HasValue && l.ExpiryDate.Value.Date < DateTime.Today) throw new InvalidOperationException("Tanggal expired tidak boleh sebelum hari ini untuk " + l.Name + ".");
            var net = Math.Max(0d, l.InvoicePrice - l.DiscountAmount);
            l.PpnAmount = net * (l.PpnPercent / 100d);
            l.PurchasePrice = net + l.PpnAmount;
            if (l.SalePrice <= 0) l.SalePrice = MedicineService.SuggestSalePrice(l.PurchasePrice);
            if (l.SalePrice < 0) throw new InvalidOperationException("Harga jual tidak valid.");
        }

        private static void ApplyInvoicePpn(IList<PurchaseLine> lines, double baseTotal, double ppnPercent)
        {
            if (baseTotal <= 0d) return;
            foreach (var l in lines)
            {
                var lineBase = l.PurchasePrice * l.Qty;
                var linePpnTotal = lineBase * (ppnPercent / 100d);
                l.PpnPercent = ppnPercent;
                l.PpnAmount = l.Qty <= 0 ? 0d : linePpnTotal / l.Qty;
                l.PurchasePrice += l.PpnAmount;
            }
        }

        private static void InsertPurchaseLine(SQLiteConnection c, SQLiteTransaction tx, AppUser user, PurchaseLine l, string supplier, long supplierId, long purchaseId, string purchaseNo, DateTime now, int lineNumber)
        {
            bool created;
            var source = string.IsNullOrWhiteSpace(supplier) ? "input tunggal" : supplier;
            var foundId = l.MedicineId > 0 ? l.MedicineId : MedicineService.FindMedicineIdByName(c, tx, l.Name);
            var existingId = foundId > 0 ? (long?)foundId : null;
            var oldSnapshot = existingId.HasValue ? MedicineSnapshot(c, tx, existingId.Value) : null;
            var medicineId = MedicineService.UpsertMedicineFromInvoice(c, tx, user, existingId, l.Name, l.Unit, "", "", l.PurchasePrice, l.SalePrice, true, out created);

            int before;
            using (var q = new SQLiteCommand("SELECT awl+msk-klr FROM medicines WHERE id=@id AND is_active=1", c, tx))
            { q.Parameters.AddWithValue("@id", medicineId); var o = q.ExecuteScalar(); if (o == null || o == DBNull.Value) throw new InvalidOperationException("Obat stok masuk tidak ditemukan/nonaktif: " + l.Name); before = Convert.ToInt32(o); }

            var batchNo = (l.BatchNo ?? "").Trim();
            if (batchNo.Length == 0) batchNo = "TANPA-BATCH-" + purchaseNo + "-" + lineNumber;
            long itemId;
            using (var pi = new SQLiteCommand(@"INSERT INTO purchase_items(purchase_id,medicine_id,medicine_name,medicine_unit,qty,purchase_price,subtotal,batch_no,expiry_date,invoice_price,discount_amount,ppn_percent,ppn_amount,sale_price_suggested)
VALUES(@p,@m,@name,@unit,@q,@price,@sub,@batch,@exp,@invoice,@disc,@ppnp,@ppn,@sale); SELECT last_insert_rowid();", c, tx))
            {
                pi.Parameters.AddWithValue("@p", purchaseId); pi.Parameters.AddWithValue("@m", medicineId); pi.Parameters.AddWithValue("@name", l.Name); pi.Parameters.AddWithValue("@unit", l.Unit);
                pi.Parameters.AddWithValue("@q", l.Qty); pi.Parameters.AddWithValue("@price", l.PurchasePrice); pi.Parameters.AddWithValue("@sub", l.Qty * l.PurchasePrice); pi.Parameters.AddWithValue("@batch", batchNo);
                pi.Parameters.AddWithValue("@exp", l.ExpiryDate.HasValue ? (object)l.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                pi.Parameters.AddWithValue("@invoice", l.InvoicePrice); pi.Parameters.AddWithValue("@disc", l.DiscountAmount); pi.Parameters.AddWithValue("@ppnp", l.PpnPercent); pi.Parameters.AddWithValue("@ppn", l.PpnAmount); pi.Parameters.AddWithValue("@sale", l.SalePrice);
                itemId = Convert.ToInt64(pi.ExecuteScalar());
            }

            using (var u = new SQLiteCommand("UPDATE medicines SET msk=msk+@q,updated_at=@now WHERE id=@id", c, tx))
            {
                u.Parameters.AddWithValue("@q", l.Qty); u.Parameters.AddWithValue("@now", now.ToString("s")); u.Parameters.AddWithValue("@id", medicineId); u.ExecuteNonQuery();
            }

            long batchId;
            using (var batch = new SQLiteCommand(@"INSERT INTO medicine_batches(medicine_id,batch_number,expired_date,qty_in,qty_out,qty_available,supplier_id,purchase_detail_id,created_at)
VALUES(@m,@batch,@exp,@q,0,@q,@sid,@pi,@now); SELECT last_insert_rowid();", c, tx))
            {
                batch.Parameters.AddWithValue("@m", medicineId); batch.Parameters.AddWithValue("@batch", batchNo);
                batch.Parameters.AddWithValue("@exp", l.ExpiryDate.HasValue ? (object)l.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                batch.Parameters.AddWithValue("@q", l.Qty); batch.Parameters.AddWithValue("@sid", supplierId == 0 ? (object)DBNull.Value : supplierId);
                batch.Parameters.AddWithValue("@pi", itemId); batch.Parameters.AddWithValue("@now", now.ToString("s"));
                batchId = Convert.ToInt64(batch.ExecuteScalar());
            }

            MedicineService.InsertMovement(c, tx, medicineId, batchId, "MASUK", "PURCHASE", purchaseId, purchaseNo, l.Qty, 0, before, before + l.Qty, null, user.Id, null, "Stok masuk " + source, now.ToString("s"));
            AuditService.LogDetailed(user, "STOK_MASUK", "medicine", medicineId, purchaseNo, "stok=" + before, "stok=" + (before + l.Qty), l.Qty * l.PurchasePrice, null, null, null,
                "Stok masuk dari " + source, "BERHASIL", medicineId, batchNo, l.Qty, 0, c, tx);
            AuditService.LogDetailed(user, created ? "TAMBAH_OBAT_DARI_STOK_MASUK" : "UPDATE_OBAT_DARI_STOK_MASUK", "medicine", medicineId, purchaseNo, oldSnapshot, MedicineSnapshot(c, tx, medicineId), l.PurchasePrice, null, null, null,
                "Sinkron master obat dari stok masuk", "BERHASIL", medicineId, batchNo, l.Qty, 0, c, tx);
        }

        private static string LoadPurchaseNoForEdit(SQLiteConnection c, SQLiteTransaction tx, long purchaseId)
        {
            using (var q = new SQLiteCommand("SELECT purchase_no,status FROM purchases WHERE id=@id", c, tx))
            {
                q.Parameters.AddWithValue("@id", purchaseId);
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("Stok masuk tidak ditemukan.");
                    if (Convert.ToString(r["status"]) != "SELESAI") throw new InvalidOperationException("Hanya stok masuk selesai yang dapat diedit.");
                    return Convert.ToString(r["purchase_no"]);
                }
            }
        }

        private static void ReversePurchaseForEdit(SQLiteConnection c, SQLiteTransaction tx, long purchaseId)
        {
            throw new InvalidOperationException("Histori stok masuk tidak boleh dibalik dengan menghapus mutasi stok.");
        }

        public static DataTable PurchaseHeader(long purchaseId)
        {
            return Database.Query(@"SELECT id,purchase_no,supplier,supplier_invoice_number,purchase_time,purchase_date,status,total,note
FROM purchases WHERE id=@id", new SQLiteParameter("@id", purchaseId));
        }

        public static DataTable PurchaseItems(long purchaseId)
        {
            return Database.Query(@"SELECT pi.id,pi.medicine_id,
IFNULL(pi.medicine_name,m.name) AS Obat,
IFNULL(pi.medicine_unit,m.unit) AS Satuan,
pi.qty AS Qty,
CASE WHEN IFNULL(pi.invoice_price,0)=0 THEN pi.purchase_price ELSE pi.invoice_price END AS [Harga Faktur],
IFNULL(pi.discount_amount,0) AS Diskon,
IFNULL(pi.ppn_percent,0) AS [PPN %],
IFNULL(pi.ppn_amount,0) AS [PPN],
pi.purchase_price AS [Harga Beli Final],
IFNULL(pi.sale_price_suggested,m.sale_price) AS [Harga Jual],
IFNULL(pi.batch_no,'') AS Batch,
IFNULL(pi.expiry_date,'') AS Expired,
pi.subtotal AS Subtotal
FROM purchase_items pi JOIN medicines m ON m.id=pi.medicine_id
WHERE pi.purchase_id=@id ORDER BY pi.id", new SQLiteParameter("@id", purchaseId));
        }

        private static string MedicineSnapshot(SQLiteConnection c, SQLiteTransaction tx, long id)
        {
            using (var cmd = new SQLiteCommand(@"SELECT code,name,unit,awl,msk,klr,purchase_price,sale_price,min_stock,category,supplier,is_active FROM medicines WHERE id=@id", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return "kode=" + Convert.ToString(r["code"]) + "; nama=" + Convert.ToString(r["name"]) + "; satuan=" + Convert.ToString(r["unit"]) +
                           "; AWL=" + r["awl"] + "; MSK=" + r["msk"] + "; KLR=" + r["klr"] + "; HB=" + r["purchase_price"] + "; HJ=" + r["sale_price"] +
                           "; min=" + r["min_stock"] + "; kategori=" + Convert.ToString(r["category"]) + "; supplier=" + Convert.ToString(r["supplier"]) + "; aktif=" + r["is_active"];
                }
            }
        }

        public static void CancelSale(AppUser user, long saleId, string reason)
        {
            UserService.RequireSuper(user);
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Alasan pembatalan wajib diisi.");
            using (var c = Database.Open())
            using (var tx = c.BeginTransaction())
            {
                Database.RequireNoActiveStockOpname(c, tx);
                string code, payment, status;
                double total, discount;
                using (var q = new SQLiteCommand("SELECT report_code,payment_method,status,total,discount FROM sales WHERE id=@id", c, tx))
                {
                    q.Parameters.AddWithValue("@id", saleId);
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Transaksi tidak ditemukan.");
                        code = Convert.ToString(r["report_code"]); payment = Convert.ToString(r["payment_method"]); status = Convert.ToString(r["status"]);
                        total = Convert.ToDouble(r["total"]); discount = Convert.ToDouble(r["discount"]);
                    }
                }
                if (status != "FINAL") throw new InvalidOperationException("Transaksi ini sudah tidak berstatus FINAL.");

                var items = new DataTable();
                using (var cmd = new SQLiteCommand("SELECT id,medicine_id,qty FROM sale_items WHERE sale_id=@s", c, tx))
                { cmd.Parameters.AddWithValue("@s", saleId); using (var a = new SQLiteDataAdapter(cmd)) a.Fill(items); }
                var now = DateTime.Now.ToString("s");

                foreach (DataRow item in items.Rows)
                {
                    var saleItemId = Convert.ToInt64(item["id"]); var medicineId = Convert.ToInt64(item["medicine_id"]); var qty = Convert.ToInt32(item["qty"]);
                    int before;
                    using (var q = new SQLiteCommand("SELECT awl+msk-klr FROM medicines WHERE id=@id", c, tx)) { q.Parameters.AddWithValue("@id", medicineId); before = Convert.ToInt32(q.ExecuteScalar()); }
                    using (var u = new SQLiteCommand("UPDATE medicines SET klr=klr-@q,updated_at=@now WHERE id=@id", c, tx))
                    { u.Parameters.AddWithValue("@q", qty); u.Parameters.AddWithValue("@now", now); u.Parameters.AddWithValue("@id", medicineId); u.ExecuteNonQuery(); }

                    var maps = new DataTable();
                    using (var q = new SQLiteCommand("SELECT sib.batch_id,sib.qty,b.batch_number FROM sale_item_batches sib LEFT JOIN medicine_batches b ON b.id=sib.batch_id WHERE sib.sale_item_id=@si", c, tx))
                    { q.Parameters.AddWithValue("@si", saleItemId); using (var a = new SQLiteDataAdapter(q)) a.Fill(maps); }
                    var restored = 0; var running = before;
                    foreach (DataRow map in maps.Rows)
                    {
                        if (map["batch_id"] == DBNull.Value) continue;
                        var batchId = Convert.ToInt64(map["batch_id"]); var qRest = Convert.ToInt32(map["qty"]); var batchNo = Convert.ToString(map["batch_number"]);
                        using (var u = new SQLiteCommand("UPDATE medicine_batches SET qty_out=CASE WHEN qty_out>=@q THEN qty_out-@q ELSE 0 END,qty_available=qty_available+@q WHERE id=@id", c, tx))
                        { u.Parameters.AddWithValue("@q", qRest); u.Parameters.AddWithValue("@id", batchId); u.ExecuteNonQuery(); }
                        MedicineService.InsertMovement(c, tx, medicineId, batchId, "BATAL_PENJUALAN", "SALE_CANCEL", saleId, code, qRest, 0, running, running + qRest, payment, user.Id, reason.Trim(), "Pengembalian stok karena pembatalan", now);
                        AuditService.LogDetailed(user, "STOK_MASUK", "medicine", medicineId, code, "stok=" + running, "stok=" + (running + qRest), null, null, payment, reason, "Pembatalan penjualan", "BERHASIL", medicineId, batchNo, qRest, 0, c, tx);
                        running += qRest; restored += qRest;
                    }
                    if (restored < qty)
                    {
                        var legacyQty = qty - restored;
                        var batchNo = "RETUR-" + code + "-" + saleItemId;
                        long batchId;
                        using (var b = new SQLiteCommand(@"INSERT INTO medicine_batches(medicine_id,batch_number,qty_in,qty_out,qty_available,created_at)
VALUES(@m,@batch,@q,0,@q,@now); SELECT last_insert_rowid();", c, tx))
                        { b.Parameters.AddWithValue("@m", medicineId); b.Parameters.AddWithValue("@batch", batchNo); b.Parameters.AddWithValue("@q", legacyQty); b.Parameters.AddWithValue("@now", now); batchId = Convert.ToInt64(b.ExecuteScalar()); }
                        MedicineService.InsertMovement(c, tx, medicineId, batchId, "BATAL_PENJUALAN", "SALE_CANCEL", saleId, code, legacyQty, 0, running, running + legacyQty, payment, user.Id, reason.Trim(), "Pengembalian stok legacy", now);
                    }
                }

                using (var u = new SQLiteCommand("UPDATE sales SET status='DIBATALKAN',note=CASE WHEN IFNULL(note,'')='' THEN @n ELSE note || ' | ' || @n END WHERE id=@id", c, tx))
                { u.Parameters.AddWithValue("@n", "Batal: " + reason.Trim()); u.Parameters.AddWithValue("@id", saleId); u.ExecuteNonQuery(); }
                AuditService.LogDetailed(user, "PEMBATALAN_TRANSAKSI", "sale", saleId, code, "status=FINAL", "status=DIBATALKAN", total, discount, payment, reason, "Transaksi dibatalkan dan stok dikembalikan", "BERHASIL", null, null, 0, 0, c, tx);
                tx.Commit();
            }
        }
    }
}
