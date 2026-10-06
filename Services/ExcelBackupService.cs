using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using ClosedXML.Excel;
using ApotekFirdanFarma.Core;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public static class ExcelBackupService
    {
        public static string BackupDatabase(AppUser user = null, string reason = "Backup manual")
        {
            var dest = Path.Combine(AppPaths.BackupDirectory, "firdan_farma_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".db");
            using (var source = Database.Open())
            using (var target = new SQLiteConnection("Data Source=" + dest + ";Version=3;"))
            {
                target.Open();
                source.BackupDatabase(target, "main", "main", -1, null, 0);
            }
            if (user != null) AuditService.LogDetailed(user, "BACKUP_DATABASE", "database", null, Path.GetFileName(dest), null, dest, null, null, null, reason, "Backup berhasil");
            return dest;
        }

        public static void RestoreDatabase(AppUser user, string sourceFile)
        {
            UserService.RequireSuper(user);
            if (Database.HasActiveStockOpname()) throw new InvalidOperationException("Restore tidak dapat dilakukan selama stok opname aktif. Selesaikan atau batalkan opname terlebih dahulu.");
            if (!File.Exists(sourceFile)) throw new FileNotFoundException("File backup tidak ditemukan.");
            var safety = BackupDatabase(user, "Backup otomatis sebelum restore");
            try
            {
                using (var source = new SQLiteConnection("Data Source=" + sourceFile + ";Version=3;Read Only=True;"))
                using (var target = Database.Open())
                {
                    source.Open();
                    source.BackupDatabase(target, "main", "main", -1, null, 0);
                }
                Database.Initialize();
                AuditService.LogDetailed(user, "RESTORE_DATABASE", "database", null, Path.GetFileName(sourceFile), safety, sourceFile, null, null, null, "Restore database", "Restore berhasil");
            }
            catch
            {
                try
                {
                    using (var source = new SQLiteConnection("Data Source=" + safety + ";Version=3;Read Only=True;"))
                    using (var target = Database.Open())
                    { source.Open(); source.BackupDatabase(target, "main", "main", -1, null, 0); }
                }
                catch { }
                throw;
            }
        }

        public static void ExportTable(DataTable dt, string path, string sheetName)
        {
            if (dt == null) throw new InvalidOperationException("Tidak ada data untuk diekspor.");
            var exportTable = PrepareExportTable(dt);
            using (var wb = new XLWorkbook())
            {
                var safeName = string.IsNullOrWhiteSpace(sheetName) ? "Data" : sheetName;
                if (safeName.Length > 31) safeName = safeName.Substring(0, 31);
                var ws = wb.Worksheets.Add(safeName);
                ws.Cell(1, 1).InsertTable(exportTable, true);
                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
        }

        private static DataTable PrepareExportTable(DataTable source)
        {
            if (!source.Columns.Contains("__separator")) return source;

            var export = source.Clone();
            export.Columns.Remove("__separator");

            foreach (DataRow row in source.Rows)
            {
                var marker = row["__separator"] == DBNull.Value ? 0 : Convert.ToInt32(row["__separator"]);
                var copy = export.NewRow();

                if (marker != 1)
                {
                    foreach (DataColumn col in source.Columns)
                    {
                        if (col.ColumnName == "__separator") continue;
                        copy[col.ColumnName] = row[col.ColumnName];
                    }
                }

                export.Rows.Add(copy);
            }

            return export;
        }

        public static void ExportMedicines(string path)
        {
            var raw = Database.Query(@"SELECT name AS [Nama Barang],unit AS SATUAN,awl AS AWL,msk AS MSK,klr AS KLR,(awl+msk-klr) AS SISA,purchase_price AS HargaBeli,sale_price AS HargaJual
FROM medicines WHERE is_active=1 ORDER BY name");
            var dt = raw.Clone();
            dt.Columns.Add("No", typeof(int));
            dt.Columns["No"].SetOrdinal(0);
            var no = 1;
            foreach (DataRow row in raw.Rows)
            {
                var nr = dt.NewRow();
                nr["No"] = no++;
                foreach (DataColumn col in raw.Columns) nr[col.ColumnName] = row[col.ColumnName];
                dt.Rows.Add(nr);
            }
            ExportTable(dt, path, "Obat");
        }

        public static Tuple<int, int, int> ImportMedicines(AppUser user, string path)
        {
            UserService.RequireSuper(user);
            BackupDatabase(user, "Backup otomatis sebelum import Excel");
            int success = 0, skipped = 0, failed = 0;
            using (var wb = new XLWorkbook(path))
            {
                var ws = wb.Worksheet(1); int headerRow = 0;
                for (int r = 1; r <= Math.Min(10, ws.LastRowUsed() == null ? 0 : ws.LastRowUsed().RowNumber()); r++)
                {
                    var vals = new List<string>();
                    for (int c = 1; c <= Math.Min(15, ws.LastColumnUsed() == null ? 0 : ws.LastColumnUsed().ColumnNumber()); c++) vals.Add(ws.Cell(r, c).GetString().Trim().ToUpperInvariant());
                    if (vals.Contains("NAMA BARANG") && vals.Contains("AWL")) { headerRow = r; break; }
                }
                if (headerRow == 0) throw new InvalidOperationException("Header Excel tidak ditemukan. Wajib ada Nama Barang, AWL, MSK, KLR.");
                var headers = new Dictionary<string, int>();
                for (int c = 1; c <= ws.LastColumnUsed().ColumnNumber(); c++) headers[ws.Cell(headerRow, c).GetString().Trim().ToUpperInvariant().Replace(" ", "")] = c;
                Func<string, int> col = key => headers.ContainsKey(key.ToUpperInvariant().Replace(" ", "")) ? headers[key.ToUpperInvariant().Replace(" ", "")] : 0;
                var last = ws.LastRowUsed().RowNumber();
                for (int r = headerRow + 1; r <= last; r++)
                {
                    try
                    {
                        var name = col("NAMABARANG") > 0 ? ws.Cell(r, col("NAMABARANG")).GetString().Trim() : "";
                        if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }
                        var upper = name.ToUpperInvariant();
                        if (upper.Contains("GRAND TOTAL") || upper == "SISA STOK" || upper == "PEMBELIAN" || upper == "STOK MASUK" || upper == "PENJUALAN" || upper == "LABA") { skipped++; continue; }
                        int awl = IntCell(ws, r, col("AWL")), msk = IntCell(ws, r, col("MSK")), klr = IntCell(ws, r, col("KLR"));
                        if (awl + msk - klr < 0) { failed++; continue; }
                        var unit = col("SATUAN") > 0 ? ws.Cell(r, col("SATUAN")).GetString().Trim() : "";
                        var hb = DoubleCell(ws, r, col("HARGABELI")); var hj = DoubleCell(ws, r, col("HARGAJUAL"));
                        long id = 0;
                        using (var c = Database.Open()) using (var q = new SQLiteCommand("SELECT id FROM medicines WHERE name=@n COLLATE NOCASE LIMIT 1", c))
                        { q.Parameters.AddWithValue("@n", name); var o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) id = Convert.ToInt64(o); }
                        MedicineService.Save(user, id, null, name, unit, awl, msk, klr, hb, hj, 0, null, null, true);
                        success++;
                    }
                    catch { failed++; }
                }
            }
            AuditService.LogDetailed(user, "IMPORT_EXCEL", "medicine", null, null, null, null, null, null, null, null,
                "Berhasil " + success + ", dilewati " + skipped + ", gagal " + failed, failed == 0 ? "BERHASIL" : "SELESAI_DENGAN_CATATAN");
            return Tuple.Create(success, skipped, failed);
        }

        private static int IntCell(IXLWorksheet ws, int r, int c)
        {
            if (c <= 0) return 0; double d; return ws.Cell(r, c).TryGetValue<double>(out d) ? Convert.ToInt32(d) : 0;
        }
        private static double DoubleCell(IXLWorksheet ws, int r, int c)
        {
            if (c <= 0) return 0; double d; return ws.Cell(r, c).TryGetValue<double>(out d) ? d : 0;
        }
    }
}
