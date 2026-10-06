using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    internal class DashboardPanel : UserControl
    {
        private readonly AppUser _user;
        public DashboardPanel(AppUser user)
        {
            _user = user; Dock = DockStyle.Fill; BackColor = Ui.Background;
            LoadData();
        }

        private void LoadData()
        {
            Controls.Clear();
            var table = ReportService.Dashboard(_user);
            if (table.Rows.Count == 0)
            {
                Controls.Add(new Label
                {
                    Text = "Data dashboard belum tersedia.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(71, 85, 105)
                });
                return;
            }

            var d = table.Rows[0];

            var summary = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.White, Padding = new Padding(16, 14, 16, 12) };
            summary.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, summary.Width - 1, summary.Height - 1);
                }
            };
            summary.Controls.Add(new Label
            {
                Text = "Ringkasan modul",
                AutoSize = true,
                Location = new Point(16, 12),
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42)
            });
            summary.Controls.Add(new Label
            {
                Text = BuildFeatureSummary(),
                AutoSize = true,
                Location = new Point(16, 35),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            });
            Controls.Add(summary);

            var cards = new FlowLayoutPanel { Dock = DockStyle.Top, Height = _user.IsSuperAdmin ? 260 : 140, AutoScroll = true, WrapContents = true, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
            cards.Controls.Add(Ui.Card("Penjualan", Ui.Rp(d["sales_total"]), "Transaksi " + Convert.ToString(d["transaction_count"]) + " | Tunai " + Ui.Rp(d["cash_total"]) + " | QRIS " + Ui.Rp(d["qris_total"])));
            cards.Controls.Add(Ui.Card("Stok", Convert.ToString(d["low_stock"]) + " menipis", "Aktif " + Convert.ToString(d["active_medicines"]) + " | Total " + Convert.ToString(d["total_stock"])));
            cards.Controls.Add(Ui.Card("Aktivitas", "Terbaru", _user.IsSuperAdmin ? "Lihat log terbaru di bawah" : "Tabel aktivitas di bawah"));

            if (_user.IsSuperAdmin)
            {
                cards.Controls.Add(Ui.Card("Stok Masuk", Ui.Rp(d["purchase_total"]), "Masuk " + Convert.ToString(d["stock_in"]) + " | Keluar " + Convert.ToString(d["stock_out"])));
                cards.Controls.Add(Ui.Card("Pengawasan", Convert.ToString(d["risk_count"]) + " item", "Diskon " + Ui.Rp(d["discount_total"]) + " | Edit " + Convert.ToString(d["edited_count"]) + " | Batal " + Convert.ToString(d["canceled_count"])));
            }

            Controls.Add(cards);

            if (_user.IsSuperAdmin)
            {
                var grandWrap = new Panel { Dock = DockStyle.Top, Height = 164, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
                grandWrap.Controls.Add(new Label
                {
                    Text = "Grand Total",
                    AutoSize = true,
                    Location = new Point(0, 0),
                    Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42)
                });

                var grandCards = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 116, AutoScroll = true, WrapContents = true, BackColor = Color.Transparent, Padding = new Padding(0, 10, 0, 0) };
                grandCards.Controls.Add(Ui.Card("Grand Laba", Ui.Rp(d["grand_profit_total"]), "Akumulasi laba bersih semua transaksi final"));
                grandCards.Controls.Add(Ui.Card("Grand Stok", Ui.Rp(d["grand_stock_value"]), "Sisa stok aktif berdasarkan harga beli"));
                grandCards.Controls.Add(Ui.Card("Grand Pembelian", Ui.Rp(d["grand_purchase_total"]), "Total pembelian selesai"));
                grandCards.Controls.Add(Ui.Card("Grand Terjual", Ui.Rp(d["grand_sales_total"]), "Total penjualan final"));
                grandWrap.Controls.Add(grandCards);
                Controls.Add(grandWrap);
            }

            var spacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };
            Controls.Add(spacer);
            spacer.BringToFront();

            var tableContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
            tableContainer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1)) // Slate 200
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, tableContainer.Width - 1, tableContainer.Height - 1);
                }
            };

            var tableTitle = new Label
            {
                Text = _user.IsSuperAdmin ? "Aktivitas Terbaru Semua Pengguna" : "Aktivitas Saya",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42), // Slate 900
                AutoSize = true,
                Location = new Point(16, 16)
            };
            tableContainer.Controls.Add(tableTitle);

            var gridWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 32, 0, 0) };
            tableContainer.Controls.Add(gridWrapper);
            gridWrapper.BringToFront();

            var activity = Ui.Grid();
            activity.DataSource = ReportService.RecentActivity(_user, 35);
            gridWrapper.Controls.Add(activity);

            Controls.Add(tableContainer);
            tableContainer.BringToFront();
        }

        private string BuildFeatureSummary()
        {
            var ops = new List<string> { "Penjualan", "Kartu Stok", "Laporan", "Aktivitas" };
            if (_user.CanPurchaseEffective) ops.Insert(1, "Stok Masuk");
            if (_user.CanAdjustStockEffective) ops.Add("Koreksi Stok");
            if (_user.IsSuperAdmin) ops.Add("Stok Opname");

            var line1 = "Operasional: " + string.Join(", ", ops);
            var line2 = _user.IsSuperAdmin
                ? "Admin: Pengguna, Master Data, Backup, Audit, Batch, Expired."
                : "Laporan: Stok, Expired, dan riwayat sesuai hak akses.";
            return line1 + "\n" + line2;
        }
    }
}
