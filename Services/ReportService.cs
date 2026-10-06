using System;
using System.Data;
using System.Data.SQLite;
using System.Text;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public static class ReportService
    {
        private static string SegmentClause(string column, int segmentFilter)
        {
            if (segmentFilter == 0) return " AND time(" + column + ") < '15:00:00'";
            if (segmentFilter == 1) return " AND time(" + column + ") >= '15:00:00'";
            return "";
        }

        private static DataTable AddDateSeparators(DataTable source, string dateColumn)
        {
            var result = source.Clone();
            if (!result.Columns.Contains("__separator"))
                result.Columns.Add("__separator", typeof(int));
            result.Columns["__separator"].SetOrdinal(0);

            string lastDate = null;
            foreach (DataRow row in source.Rows)
            {
                var currentDate = Convert.ToString(row[dateColumn]);
                if (!string.Equals(lastDate, currentDate, StringComparison.Ordinal))
                {
                    if (result.Rows.Count > 0)
                    {
                        var separator = result.NewRow();
                        separator["__separator"] = 1;
                        result.Rows.Add(separator);
                    }
                    lastDate = currentDate;
                }

                var copy = result.NewRow();
                foreach (DataColumn col in source.Columns)
                    copy[col.ColumnName] = row[col.ColumnName];
                copy["__separator"] = 0;
                result.Rows.Add(copy);
            }

            return result;
        }

        public static DataTable Dashboard(AppUser user)
        {
            var reportDate = TransactionService.GetReportDate(DateTime.Now).ToString("yyyy-MM-dd");
            var sql = @"SELECT
@report AS report_date,
(SELECT COUNT(*) FROM medicines WHERE is_active=1) AS active_medicines,
(SELECT IFNULL(SUM(awl+msk-klr),0) FROM medicines WHERE is_active=1) AS total_stock,
(SELECT COUNT(*) FROM medicines WHERE is_active=1 AND (awl+msk-klr)<=min_stock) AS low_stock,
(SELECT COUNT(*) FROM sales WHERE report_date=@report AND status='FINAL') AS transaction_count,
(SELECT IFNULL(SUM(total),0) FROM sales WHERE report_date=@report AND status='FINAL') AS sales_total,
(SELECT IFNULL(SUM(total),0) FROM sales WHERE report_date=@report AND payment_method='TUNAI' AND status='FINAL') AS cash_total,
(SELECT IFNULL(SUM(total),0) FROM sales WHERE report_date=@report AND payment_method='QRIS' AND status='FINAL') AS qris_total,
(SELECT IFNULL(SUM(discount),0) FROM sales WHERE report_date=@report AND status='FINAL') AS discount_total,
(SELECT IFNULL(SUM(total),0) FROM purchases WHERE purchase_date=@report AND status='SELESAI') AS purchase_total,
(SELECT IFNULL(SUM(qty_in),0) FROM stock_movements WHERE date(movement_datetime)=@calendar) AS stock_in,
(SELECT IFNULL(SUM(qty_out),0) FROM stock_movements WHERE date(movement_datetime)=@calendar) AS stock_out,
(SELECT COUNT(*) FROM medicine_batches WHERE qty_available>0 AND expired_date IS NOT NULL AND expired_date<>'' AND date(expired_date)<=date('now','+90 day')) AS expiring_count,
(SELECT COUNT(*) FROM sales WHERE report_date=@report AND status='DIBATALKAN') AS canceled_count,
(SELECT COUNT(*) FROM audit_logs WHERE date(created_at)=@calendar AND action='EDIT_TRANSAKSI' AND status='BERHASIL') AS edited_count,
(SELECT COUNT(*) FROM stock_movements WHERE date(movement_datetime)=@calendar AND (movement_type LIKE 'PENYESUAIAN%' OR movement_type LIKE 'STOK_OPNAME%')) AS adjustment_count,
(SELECT COUNT(*) FROM audit_logs WHERE date(created_at)=@calendar AND (status='GAGAL' OR action IN ('PEMBATALAN_TRANSAKSI','PENYESUAIAN_STOK','RESTORE_DATABASE','UBAH_AKUN'))) AS risk_count,
(SELECT IFNULL(SUM(total),0) FROM sales WHERE status='FINAL') AS grand_sales_total,
(SELECT IFNULL(SUM(total),0) FROM purchases WHERE status='SELESAI') AS grand_purchase_total,
(SELECT IFNULL(ROUND(IFNULL(SUM((awl+msk-klr) * purchase_price),0),0),0) FROM medicines WHERE is_active=1) AS grand_stock_value,
(SELECT IFNULL(ROUND(
    IFNULL((SELECT SUM((si.unit_price - si.unit_cost) * si.qty)
            FROM sales s JOIN sale_items si ON si.sale_id = s.id
            WHERE s.status='FINAL'), 0)
    - IFNULL((SELECT SUM(discount) FROM sales WHERE status='FINAL'), 0),
0), 0)) AS grand_profit_total";
            return Database.Query(sql,
                new SQLiteParameter("@report", reportDate),
                new SQLiteParameter("@calendar", DateTime.Today.ToString("yyyy-MM-dd")));
        }

        public static DataTable RecentActivity(AppUser user, int limit = 25)
        {
            var sql = @"WITH base AS (
SELECT a.id,a.created_at,IFNULL(a.username,'-') AS username,IFNULL(a.role,'-') AS role,a.action,
IFNULL(a.transaction_code,'') AS transaction_code,IFNULL(m.name,'-') AS medicine_name,IFNULL(a.batch_no,'-') AS batch_no,
IFNULL(a.qty_in,0) AS qty_in,IFNULL(a.qty_out,0) AS qty_out,IFNULL(a.amount,0) AS amount,IFNULL(a.discount,0) AS discount,
IFNULL(a.payment_method,'-') AS payment_method,IFNULL(a.reason,'') AS reason,IFNULL(a.notes,'') AS notes,
IFNULL(a.old_value,'') AS old_value,IFNULL(a.new_value,'') AS new_value,a.status,
CASE WHEN IFNULL(a.transaction_code,'')<>'' THEN a.transaction_code ELSE 'AUDIT-' || a.id END AS grp_key
FROM audit_logs a LEFT JOIN medicines m ON m.id=a.medicine_id
WHERE (@super=1 OR a.user_id=@uid)
)
SELECT MIN(created_at) AS Waktu,MAX(username) AS User,MAX(role) AS Role,
REPLACE(GROUP_CONCAT(DISTINCT action), ',', ' + ') AS Aktivitas,
CASE WHEN MAX(transaction_code)='' THEN '-' ELSE MAX(transaction_code) END AS Kode,
IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN medicine_name<>'-' THEN medicine_name END), ',', ' | '), '-') AS Obat,
IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN batch_no<>'-' THEN batch_no END), ',', ' | '), '-') AS Batch,
SUM(qty_in) AS Masuk,SUM(qty_out) AS Keluar,MAX(amount) AS Nominal,MAX(discount) AS Diskon,
MAX(payment_method) AS Metode,IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN reason<>'' THEN reason END), ',', ' | '), '') AS Alasan,
IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN notes<>'' THEN notes END), ',', ' | '), '') AS Catatan,MAX(status) AS Status
FROM base GROUP BY grp_key ORDER BY MAX(id) DESC LIMIT @lim";
            return Database.Query(sql,
                new SQLiteParameter("@super", user != null && user.IsSuperAdmin ? 1 : 0),
                new SQLiteParameter("@uid", user == null ? 0 : user.Id),
                new SQLiteParameter("@lim", limit));
        }

        public static DataTable Activity(AppUser requester, DateTime from, DateTime to, long? userId = null, string action = "", string username = "")
        {
            if (requester == null) throw new UnauthorizedAccessException("Sesi pengguna tidak valid.");
            var detailed = requester.IsSuperAdmin;
            var sql = new StringBuilder(@"WITH base AS (
SELECT a.id,a.created_at,IFNULL(a.username,'-') AS username,IFNULL(a.role,'-') AS role,a.action,
IFNULL(a.transaction_code,'') AS transaction_code,IFNULL(m.name,'-') AS medicine_name,IFNULL(a.batch_no,'-') AS batch_no,
IFNULL(a.qty_in,0) AS qty_in,IFNULL(a.qty_out,0) AS qty_out,IFNULL(a.amount,0) AS amount,IFNULL(a.discount,0) AS discount,
IFNULL(a.payment_method,'-') AS payment_method,IFNULL(a.reason,'') AS reason,IFNULL(a.notes,'') AS notes,
IFNULL(a.old_value,'') AS old_value,IFNULL(a.new_value,'') AS new_value,a.status,
CASE WHEN IFNULL(a.transaction_code,'')<>'' THEN a.transaction_code ELSE 'AUDIT-' || a.id END AS grp_key
FROM audit_logs a LEFT JOIN medicines m ON m.id=a.medicine_id
WHERE date(a.created_at) BETWEEN @f AND @t");

            var p = new System.Collections.Generic.List<SQLiteParameter>
            {
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@t", to.ToString("yyyy-MM-dd"))
            };
            if (!detailed)
            {
                sql.Append(" AND a.user_id=@uid");
                p.Add(new SQLiteParameter("@uid", requester.Id));
            }
            else if (userId.HasValue)
            {
                sql.Append(" AND a.user_id=@uid");
                p.Add(new SQLiteParameter("@uid", userId.Value));
            }
            sql.Append(@")
SELECT MIN(created_at) AS Waktu,MAX(username) AS User,MAX(role) AS Role,
REPLACE(GROUP_CONCAT(DISTINCT action), ',', ' + ') AS Aksi,
CASE WHEN MAX(transaction_code)='' THEN '-' ELSE MAX(transaction_code) END AS Kode,
IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN medicine_name<>'-' THEN medicine_name END), ',', ' | '), '-') AS Obat,
IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN batch_no<>'-' THEN batch_no END), ',', ' | '), '-') AS Batch,
SUM(qty_in) AS Masuk,SUM(qty_out) AS Keluar,MAX(amount) AS Nominal,MAX(discount) AS Diskon,
MAX(payment_method) AS Metode,IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN reason<>'' THEN reason END), ',', ' | '), '') AS Alasan,
IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN notes<>'' THEN notes END), ',', ' | '), '') AS Catatan,MAX(status) AS Status");
            if (detailed)
                sql.Append(",IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN old_value<>'' THEN old_value END), ',', ' | '), '') AS [Nilai Sebelum],IFNULL(REPLACE(GROUP_CONCAT(DISTINCT CASE WHEN new_value<>'' THEN new_value END), ',', ' | '), '') AS [Nilai Sesudah]");
            sql.Append(" FROM base GROUP BY grp_key");
            if (!string.IsNullOrWhiteSpace(action))
            {
                if (string.Equals(action.Trim(), "LOGIN_GAGAL", StringComparison.OrdinalIgnoreCase))
                    sql.Append(" HAVING SUM(CASE WHEN action='LOGIN' AND status='GAGAL' THEN 1 ELSE 0 END) > 0");
                else
                    sql.Append(" HAVING SUM(CASE WHEN action LIKE @action THEN 1 ELSE 0 END) > 0");
            }
            if (detailed && !string.IsNullOrWhiteSpace(username))
            {
                sql.Append(string.IsNullOrWhiteSpace(action) ? " HAVING" : " AND");
                sql.Append(" SUM(CASE WHEN username LIKE @username THEN 1 ELSE 0 END) > 0");
                p.Add(new SQLiteParameter("@username", "%" + username.Trim() + "%"));
            }
            sql.Append(" ORDER BY MAX(id) DESC");
            if (!string.IsNullOrWhiteSpace(action) && !string.Equals(action.Trim(), "LOGIN_GAGAL", StringComparison.OrdinalIgnoreCase))
                p.Add(new SQLiteParameter("@action", "%" + action.Trim() + "%"));
            return Database.Query(sql.ToString(), p.ToArray());
        }

        public static DataTable Sales(DateTime from, DateTime to, string payment = "", long? userId = null, int discountFilter = -1, int segmentFilter = -1)
        {
            var sql = new StringBuilder(@"SELECT s.id,s.report_date AS [Tanggal Laporan],s.report_code AS Kode,s.transaction_no AS Transaksi,
s.transaction_time AS Waktu,s.payment_method AS Pembayaran,
(SELECT GROUP_CONCAT(m.name || ' x' || si.qty, '; ') FROM sale_items si JOIN medicines m ON m.id=si.medicine_id WHERE si.sale_id=s.id) AS Item,
s.subtotal AS Subtotal,s.discount AS Diskon,s.total AS [Total Bersih],s.status AS Status,u.username AS User,IFNULL(s.note,'') AS Catatan,
ROUND((SELECT IFNULL(SUM((si.unit_price-si.unit_cost)*si.qty),0) FROM sale_items si WHERE si.sale_id=s.id)-s.discount,2) AS Laba
FROM sales s JOIN users u ON u.id=s.user_id WHERE s.report_date BETWEEN @f AND @t");
            if (!string.IsNullOrWhiteSpace(payment)) sql.Append(" AND s.payment_method=@pay");
            if (userId.HasValue) sql.Append(" AND s.user_id=@uid");
            if (discountFilter == 1) sql.Append(" AND s.discount>0");
            else if (discountFilter == 0) sql.Append(" AND s.discount=0");
            sql.Append(SegmentClause("s.transaction_time", segmentFilter));
            sql.Append(" ORDER BY s.report_date DESC,s.id DESC");

            var parameters = new System.Collections.Generic.List<SQLiteParameter>
            {
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@t", to.ToString("yyyy-MM-dd"))
            };
            if (!string.IsNullOrWhiteSpace(payment)) parameters.Add(new SQLiteParameter("@pay", payment.Trim().ToUpperInvariant()));
            if (userId.HasValue) parameters.Add(new SQLiteParameter("@uid", userId.Value));
            var table = Database.Query(sql.ToString(), parameters.ToArray());
            return AddDateSeparators(table, "Tanggal Laporan");
        }

        public static DataTable SalesSummary(DateTime from, DateTime to)
        {
            return Database.Query(@"SELECT
COUNT(CASE WHEN status='FINAL' THEN 1 END) AS [Jumlah Transaksi],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='TUNAI' THEN subtotal ELSE 0 END),0) AS [Bruto Tunai],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='TUNAI' THEN discount ELSE 0 END),0) AS [Diskon Tunai],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='TUNAI' THEN total ELSE 0 END),0) AS [Total Tunai],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='QRIS' THEN subtotal ELSE 0 END),0) AS [Bruto QRIS],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='QRIS' THEN discount ELSE 0 END),0) AS [Diskon QRIS],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='QRIS' THEN total ELSE 0 END),0) AS [Total QRIS],
IFNULL(SUM(CASE WHEN status='FINAL' THEN discount ELSE 0 END),0) AS [Total Diskon],
IFNULL(SUM(CASE WHEN status='FINAL' THEN total ELSE 0 END),0) AS [Total Penjualan Bersih],
COUNT(CASE WHEN status='DIBATALKAN' THEN 1 END) AS [Transaksi Dibatalkan]
FROM sales WHERE report_date BETWEEN @f AND @t",
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")), new SQLiteParameter("@t", to.ToString("yyyy-MM-dd")));
        }

        public static DataTable SalesOperational(DateTime from, DateTime to, string payment = "", long? userId = null, int discountFilter = -1, int segmentFilter = -1)
        {
            var sql = new StringBuilder(@"SELECT s.id,
s.report_date AS [Tanggal Laporan],
s.report_code AS Kode,s.transaction_no AS Transaksi,
s.transaction_time AS Waktu,s.payment_method AS Pembayaran,
(SELECT GROUP_CONCAT(m.name || ' x' || si.qty, '; ') FROM sale_items si JOIN medicines m ON m.id=si.medicine_id WHERE si.sale_id=s.id) AS Item,
s.subtotal AS Subtotal,s.discount AS Diskon,s.total AS [Total Bersih],s.status AS Status,u.username AS User,IFNULL(s.note,'') AS Catatan,
ROUND((SELECT IFNULL(SUM((si.unit_price-si.unit_cost)*si.qty),0) FROM sale_items si WHERE si.sale_id=s.id)-s.discount,2) AS Laba
FROM sales s JOIN users u ON u.id=s.user_id WHERE s.report_date BETWEEN @f AND @t");
            if (!string.IsNullOrWhiteSpace(payment)) sql.Append(" AND s.payment_method=@pay");
            if (userId.HasValue) sql.Append(" AND s.user_id=@uid");
            if (discountFilter == 1) sql.Append(" AND s.discount>0");
            else if (discountFilter == 0) sql.Append(" AND s.discount=0");
            sql.Append(SegmentClause("s.transaction_time", segmentFilter));
            sql.Append(" ORDER BY s.report_date DESC, CASE WHEN time(s.transaction_time) < '15:00:00' THEN 0 ELSE 1 END, s.transaction_time DESC, s.id DESC");

            var parameters = new System.Collections.Generic.List<SQLiteParameter>
            {
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@t", to.ToString("yyyy-MM-dd"))
            };
            if (!string.IsNullOrWhiteSpace(payment)) parameters.Add(new SQLiteParameter("@pay", payment.Trim().ToUpperInvariant()));
            if (userId.HasValue) parameters.Add(new SQLiteParameter("@uid", userId.Value));
            var table = Database.Query(sql.ToString(), parameters.ToArray());
            return AddDateSeparators(table, "Tanggal Laporan");
        }

        public static DataTable SalesOperationalSummary(DateTime from, DateTime to, int segmentFilter = -1)
        {
            var sql = new StringBuilder(@"SELECT
COUNT(CASE WHEN status='FINAL' THEN 1 END) AS [Jumlah Transaksi],
COUNT(CASE WHEN status='FINAL' AND time(transaction_time) < '15:00:00' THEN 1 END) AS [Transaksi Sebelum 15:00],
COUNT(CASE WHEN status='FINAL' AND time(transaction_time) >= '15:00:00' THEN 1 END) AS [Transaksi Sesudah 15:00],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='TUNAI' THEN subtotal ELSE 0 END),0) AS [Bruto Tunai],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='TUNAI' THEN discount ELSE 0 END),0) AS [Diskon Tunai],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='TUNAI' THEN total ELSE 0 END),0) AS [Total Tunai],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='QRIS' THEN subtotal ELSE 0 END),0) AS [Bruto QRIS],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='QRIS' THEN discount ELSE 0 END),0) AS [Diskon QRIS],
IFNULL(SUM(CASE WHEN status='FINAL' AND payment_method='QRIS' THEN total ELSE 0 END),0) AS [Total QRIS],
IFNULL(SUM(CASE WHEN status='FINAL' THEN discount ELSE 0 END),0) AS [Total Diskon],
IFNULL(SUM(CASE WHEN status='FINAL' THEN total ELSE 0 END),0) AS [Total Penjualan Bersih],
COUNT(CASE WHEN status='DIBATALKAN' THEN 1 END) AS [Transaksi Dibatalkan]
FROM sales WHERE report_date BETWEEN @f AND @t");
            sql.Append(SegmentClause("transaction_time", segmentFilter));
            return Database.Query(sql.ToString(),
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")), new SQLiteParameter("@t", to.ToString("yyyy-MM-dd")));
        }

        public static DataTable StockIn(DateTime from, DateTime to, bool includeSupplier = true, int segmentFilter = -1)
        {
            var supplierColumn = includeSupplier ? "p.supplier AS Supplier," : "";
            var sql = new StringBuilder(@"SELECT p.id,p.purchase_date AS [Tanggal],p.purchase_no AS Nomor,p.purchase_time AS Waktu,");
            sql.Append(supplierColumn);
            sql.Append(@"
IFNULL(p.supplier_invoice_number,'') AS [Faktur Supplier],
(SELECT GROUP_CONCAT(IFNULL(pi.medicine_name,m.name) || ' x' || pi.qty || ' | Batch ' || IFNULL(pi.batch_no,'-') || ' | ED ' || IFNULL(pi.expiry_date,'-'), '; ')
 FROM purchase_items pi JOIN medicines m ON m.id=pi.medicine_id WHERE pi.purchase_id=p.id) AS Detail,
  p.total AS Total,p.status AS Status,CASE WHEN IFNULL(p.entry_type,'FAKTUR')='TUNGGAL' THEN 'Tunggal' ELSE 'Batch (Faktur)' END AS [Jenis Input],u.username AS User,IFNULL(p.note,'') AS Catatan
FROM purchases p JOIN users u ON u.id=p.user_id WHERE p.purchase_date BETWEEN @f AND @t");
            sql.Append(SegmentClause("p.purchase_time", segmentFilter));
            sql.Append(" ORDER BY p.purchase_date DESC,p.purchase_time DESC");
            var table = Database.Query(sql.ToString(),
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")), new SQLiteParameter("@t", to.ToString("yyyy-MM-dd")));
            return AddDateSeparators(table, "Tanggal");
        }

        public static DataTable Purchases(DateTime from, DateTime to, bool includeSupplier = true, int segmentFilter = -1)
        {
            return StockIn(from, to, includeSupplier, segmentFilter);
        }

        public static DataTable StockSnapshot(bool includeSupplier = true)
        {
            var supplierColumn = includeSupplier ? ",m.supplier AS Supplier" : "";
            return Database.Query(@"SELECT m.id,m.code AS Kode,m.name AS [Nama Obat],m.unit AS Satuan,m.awl AS AWL,m.msk AS MSK,m.klr AS KLR,
(m.awl+m.msk-m.klr) AS SISA,m.min_stock AS [Stok Minimal],m.purchase_price AS [Harga Beli],m.sale_price AS [Harga Jual],
m.category AS Kategori" + supplierColumn + @",CASE WHEN (m.awl+m.msk-m.klr)<=m.min_stock THEN 'MENIPIS' ELSE 'AMAN' END AS Kondisi
FROM medicines m WHERE m.is_active=1 ORDER BY m.name");
        }

        public static DataTable StockCard(long medicineId, DateTime? from = null, DateTime? to = null, string batch = "", string movementType = "", string code = "", string username = "")
        {
            var sql = new StringBuilder(@"SELECT sm.id,date(sm.movement_datetime) AS Tanggal,time(sm.movement_datetime) AS Jam,
IFNULL(sm.transaction_code,'-') AS Kode,sm.movement_type AS Aktivitas,
CASE
    WHEN sm.movement_type='STOK_AWAL' THEN 'Stok Awal'
    WHEN sm.movement_type='MASUK' THEN 'Stok Masuk'
    WHEN sm.movement_type='KELUAR' THEN 'Penjualan'
    WHEN sm.movement_type LIKE 'PENYESUAIAN%' THEN 'Koreksi Stok'
    WHEN sm.movement_type LIKE 'STOK_OPNAME%' THEN 'Stok Opname'
    ELSE sm.movement_type
END AS Sumber,
IFNULL(b.batch_number,'-') AS Batch,
IFNULL(b.expired_date,'-') AS Expired,sm.qty_in AS Masuk,sm.qty_out AS Keluar,(sm.qty_in-sm.qty_out) AS Selisih,
sm.stock_before AS [Saldo Total Sebelum],sm.stock_after AS [Saldo Total Sesudah],
CASE WHEN sm.batch_id IS NULL THEN NULL ELSE IFNULL((SELECT SUM(p.qty_in-p.qty_out) FROM stock_movements p
    WHERE p.batch_id=sm.batch_id AND (p.movement_datetime<sm.movement_datetime OR
        (p.movement_datetime=sm.movement_datetime AND p.id<sm.id))),0) END AS [Saldo Batch Sebelum],
CASE WHEN sm.batch_id IS NULL THEN NULL ELSE IFNULL((SELECT SUM(p.qty_in-p.qty_out) FROM stock_movements p
    WHERE p.batch_id=sm.batch_id AND (p.movement_datetime<sm.movement_datetime OR
        (p.movement_datetime=sm.movement_datetime AND p.id<=sm.id))),0) END AS [Saldo Batch Sesudah],
IFNULL(sm.payment_method,'-') AS Metode,
IFNULL(u.username,'-') AS User,IFNULL(sm.reference_type,'-') AS Referensi,IFNULL(sm.reason,'') AS Reason,IFNULL(sm.note,'') AS Catatan
FROM stock_movements sm LEFT JOIN medicine_batches b ON b.id=sm.batch_id LEFT JOIN users u ON u.id=sm.user_id
WHERE sm.medicine_id=@id");
            var p = new System.Collections.Generic.List<SQLiteParameter> { new SQLiteParameter("@id", medicineId) };
            if (from.HasValue) { sql.Append(" AND date(sm.movement_datetime)>=@f"); p.Add(new SQLiteParameter("@f", from.Value.ToString("yyyy-MM-dd"))); }
            if (to.HasValue) { sql.Append(" AND date(sm.movement_datetime)<=@t"); p.Add(new SQLiteParameter("@t", to.Value.ToString("yyyy-MM-dd"))); }
            if (!string.IsNullOrWhiteSpace(batch)) { sql.Append(" AND IFNULL(b.batch_number,'') LIKE @batch"); p.Add(new SQLiteParameter("@batch", "%" + batch.Trim() + "%")); }
            if (!string.IsNullOrWhiteSpace(movementType)) { sql.Append(" AND sm.movement_type LIKE @type"); p.Add(new SQLiteParameter("@type", "%" + movementType.Trim() + "%")); }
            if (!string.IsNullOrWhiteSpace(code)) { sql.Append(" AND IFNULL(sm.transaction_code,'') LIKE @code"); p.Add(new SQLiteParameter("@code", "%" + code.Trim() + "%")); }
            if (!string.IsNullOrWhiteSpace(username)) { sql.Append(" AND IFNULL(u.username,'') LIKE @user"); p.Add(new SQLiteParameter("@user", "%" + username.Trim() + "%")); }
            sql.Append(" ORDER BY sm.id DESC");
            return Database.Query(sql.ToString(), p.ToArray());
        }

        public static DataTable Expiring(int days, bool includeSupplier = true)
        {
            var supplierColumn = includeSupplier ? ",IFNULL(s.name,'-') AS Supplier" : "";
            return Database.Query(@"SELECT m.name AS Obat,b.batch_number AS Batch,b.expired_date AS Expired,b.qty_available AS Sisa,
CAST(julianday(b.expired_date)-julianday(date('now')) AS INTEGER) AS [Sisa Hari]" + supplierColumn + @"
FROM medicine_batches b JOIN medicines m ON m.id=b.medicine_id LEFT JOIN suppliers s ON s.id=b.supplier_id
WHERE b.qty_available>0 AND b.expired_date IS NOT NULL AND b.expired_date<>'' AND date(b.expired_date)<=date('now',@days)
ORDER BY b.expired_date", new SQLiteParameter("@days", "+" + days + " day"));
        }

        public static DataTable Batches()
        {
            return Database.Query(@"SELECT m.name AS Obat,b.batch_number AS Batch,b.expired_date AS Expired,b.qty_in AS Masuk,b.qty_out AS Keluar,b.qty_available AS Sisa,
IFNULL(s.name,'-') AS Supplier,b.created_at AS Dibuat FROM medicine_batches b JOIN medicines m ON m.id=b.medicine_id
LEFT JOIN suppliers s ON s.id=b.supplier_id ORDER BY m.name,b.expired_date,b.id");
        }

        public static DataTable Audit(AppUser requester, DateTime from, DateTime to, long? userId = null, string action = "")
        {
            UserService.RequireSuper(requester);
            var sql = new StringBuilder(@"SELECT id,created_at AS Waktu,IFNULL(username,'-') AS User,IFNULL(role,'-') AS Role,action AS Aksi,
IFNULL(entity,'-') AS Entitas,IFNULL(transaction_code,'-') AS Kode,IFNULL(batch_no,'-') AS Batch,qty_in AS Masuk,qty_out AS Keluar,
old_value AS [Nilai Sebelum],new_value AS [Nilai Sesudah],amount AS Nominal,discount AS Diskon,IFNULL(payment_method,'-') AS Pembayaran,
IFNULL(reason,'') AS Alasan,IFNULL(notes,'') AS Catatan,status AS Status
FROM audit_logs WHERE date(created_at) BETWEEN @f AND @t");
            var p = new System.Collections.Generic.List<SQLiteParameter> { new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")), new SQLiteParameter("@t", to.ToString("yyyy-MM-dd")) };
            if (userId.HasValue) { sql.Append(" AND user_id=@uid"); p.Add(new SQLiteParameter("@uid", userId.Value)); }
            if (!string.IsNullOrWhiteSpace(action)) { sql.Append(" AND action LIKE @action"); p.Add(new SQLiteParameter("@action", "%" + action.Trim() + "%")); }
            sql.Append(" ORDER BY id DESC");
            return Database.Query(sql.ToString(), p.ToArray());
        }

        public static DataTable Corrections(DateTime from, DateTime to)
        {
            return Database.Query(@"SELECT sm.movement_datetime AS Waktu,m.name AS Obat,IFNULL(b.batch_number,'-') AS Batch,sm.movement_type AS Jenis,
sm.qty_in AS Masuk,sm.qty_out AS Keluar,sm.stock_before AS [Saldo Sebelum],sm.stock_after AS [Saldo Sesudah],u.username AS User,
IFNULL(sm.reason,'') AS Alasan,IFNULL(sm.note,'') AS Catatan
FROM stock_movements sm JOIN medicines m ON m.id=sm.medicine_id LEFT JOIN medicine_batches b ON b.id=sm.batch_id LEFT JOIN users u ON u.id=sm.user_id
WHERE date(sm.movement_datetime) BETWEEN @f AND @t AND (sm.movement_type LIKE 'PENYESUAIAN%' OR sm.movement_type LIKE 'STOK_OPNAME%' OR sm.movement_type='BATAL_PENJUALAN') ORDER BY sm.id DESC",
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")), new SQLiteParameter("@t", to.ToString("yyyy-MM-dd")));
        }

        public static DataTable DiscountReport(DateTime from, DateTime to, int segmentFilter = -1)
        {
            var sql = new StringBuilder(@"SELECT report_date AS [Tanggal Laporan],report_code AS Kode,transaction_time AS Waktu,payment_method AS Metode,
subtotal AS Subtotal,discount AS Diskon,total AS [Total Bersih],u.username AS User,status AS Status,IFNULL(note,'') AS Catatan
FROM sales s JOIN users u ON u.id=s.user_id WHERE s.report_date BETWEEN @f AND @t AND s.discount>0");
            sql.Append(SegmentClause("s.transaction_time", segmentFilter));
            sql.Append(" ORDER BY s.report_date DESC,s.id DESC");
            return Database.Query(sql.ToString(),
                new SQLiteParameter("@f", from.ToString("yyyy-MM-dd")), new SQLiteParameter("@t", to.ToString("yyyy-MM-dd")));
        }

        public static DataTable RecentFinalSales(int limit = 100)
        {
            return Database.Query(@"SELECT s.id,s.report_date AS [Tanggal Laporan],s.report_code AS Kode,s.transaction_time AS Waktu,s.payment_method AS Metode,
s.total AS Total,s.discount AS Diskon,u.username AS User,s.status AS Status FROM sales s JOIN users u ON u.id=s.user_id ORDER BY s.id DESC LIMIT @lim",
                new SQLiteParameter("@lim", limit));
        }
    }
}
