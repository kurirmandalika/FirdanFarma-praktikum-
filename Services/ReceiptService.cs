using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using ApotekFirdanFarma.Data;

namespace ApotekFirdanFarma.Services
{
    public static class ReceiptService
    {
        public static void PrintSale(long saleId)
        {
            var header = Database.Query(@"SELECT s.report_code,s.transaction_no,s.transaction_time,s.report_date,s.payment_method,s.subtotal,s.discount,s.total,u.display_name
FROM sales s JOIN users u ON u.id=s.user_id WHERE s.id=@id", new SQLiteParameter("@id", saleId));
            if (header.Rows.Count == 0) throw new InvalidOperationException("Data transaksi tidak ditemukan.");
            var items = Database.Query(@"SELECT m.name AS Obat,si.qty AS Qty,si.unit_price AS Harga,si.subtotal AS Subtotal,
IFNULL((SELECT GROUP_CONCAT(IFNULL(b.batch_number,'-'), ', ') FROM sale_item_batches sib LEFT JOIN medicine_batches b ON b.id=sib.batch_id WHERE sib.sale_item_id=si.id),'-') AS Batch
FROM sale_items si JOIN medicines m ON m.id=si.medicine_id WHERE si.sale_id=@id ORDER BY si.id", new SQLiteParameter("@id", saleId));
            var text = BuildReceipt(header.Rows[0], items);
            using (var doc = new PrintDocument())
            using (var dialog = new PrintDialog())
            {
                doc.DocumentName = "Struk Firdan Farma " + Convert.ToString(header.Rows[0]["report_code"]);
                doc.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
                doc.PrintPage += delegate(object sender, PrintPageEventArgs e)
                {
                    using (var font = new Font("Consolas", 8.5f)) e.Graphics.DrawString(text, font, Brushes.Black, new RectangleF(5, 5, e.MarginBounds.Width, e.MarginBounds.Height));
                    e.HasMorePages = false;
                };
                dialog.Document = doc;
                if (dialog.ShowDialog() == DialogResult.OK) doc.Print();
            }
        }

        private static string BuildReceipt(DataRow h, DataTable items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("        FIRDAN FARMA");
            sb.AppendLine("--------------------------------");
            sb.AppendLine("Kode   : " + h["report_code"]);
            sb.AppendLine("No     : " + h["transaction_no"]);
            sb.AppendLine("Waktu  : " + h["transaction_time"]);
            sb.AppendLine("Laporan: " + h["report_date"]);
            sb.AppendLine("Kasir  : " + h["display_name"]);
            sb.AppendLine("Bayar  : " + h["payment_method"]);
            sb.AppendLine("--------------------------------");
            foreach (DataRow r in items.Rows)
            {
                sb.AppendLine(Convert.ToString(r["Obat"]));
                sb.AppendLine(" " + r["Qty"] + " x " + Rp(r["Harga"]) + " = " + Rp(r["Subtotal"]));
                if (Convert.ToString(r["Batch"]) != "-") sb.AppendLine(" Batch: " + r["Batch"]);
            }
            sb.AppendLine("--------------------------------");
            sb.AppendLine("Subtotal : " + Rp(h["subtotal"]));
            sb.AppendLine("Diskon   : " + Rp(h["discount"]));
            sb.AppendLine("TOTAL    : " + Rp(h["total"]));
            sb.AppendLine("--------------------------------");
            sb.AppendLine("Terima kasih");
            return sb.ToString();
        }

        private static string Rp(object value)
        {
            return "Rp" + Convert.ToDouble(value).ToString("N0", new CultureInfo("id-ID"));
        }
    }
}
