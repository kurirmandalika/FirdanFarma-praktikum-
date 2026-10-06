using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    internal class ReportsPanel : UserControl
    {
        private readonly AppUser _user;
        public ReportsPanel(AppUser user)
        {
            _user = user; Dock = DockStyle.Fill;
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };
            tabs.TabPages.Add(new StaticReportTab("Stok", delegate { return ReportService.StockSnapshot(_user.IsSuperAdmin); }, "Laporan Stok"));
            tabs.TabPages.Add(new SalesReportTab("Tunai", _user, "TUNAI", false));
            tabs.TabPages.Add(new SalesReportTab("QRIS", _user, "QRIS", false));
            tabs.TabPages.Add(new SalesReportTab("Gabungan", _user, "", true));
            if (_user.CanPurchaseEffective) tabs.TabPages.Add(new PurchaseReportTab(_user, _user.IsSuperAdmin));
            tabs.TabPages.Add(new StaticReportTab("Expired", delegate { return ReportService.Expiring(180, _user.IsSuperAdmin); }, "Laporan Expired"));
            if (_user.IsSuperAdmin)
            {
                tabs.TabPages.Add(new StaticReportTab("Batch", delegate { return ReportService.Batches(); }, "Laporan Batch"));
                tabs.TabPages.Add(new DateReportTab("Koreksi", delegate (DateTime f, DateTime t) { return ReportService.Corrections(f, t); }));
                tabs.TabPages.Add(new DateReportTab("Diskon", delegate (DateTime f, DateTime t) { return ReportService.DiscountReport(f, t); }));
                tabs.TabPages.Add(new DateReportTab("Audit", delegate (DateTime f, DateTime t) { return ReportService.Audit(_user, f, t); }));
            }
            Controls.Add(tabs);
        }
    }

    internal class PurchaseReportTab : TabPage
    {
        private readonly AppUser _user;
        private readonly bool _includeSupplier;
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = Ui.Grid();

        public PurchaseReportTab(AppUser user, bool includeSupplier) : base("Stok Masuk")
        {
            _user = user;
            _includeSupplier = includeSupplier;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            var reportToday = TransactionService.GetReportDate(DateTime.Now);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(4) };
            _from.Width = 115; _from.Format = DateTimePickerFormat.Short; _from.Value = reportToday;
            _to.Width = 115; _to.Format = DateTimePickerFormat.Short; _to.Value = reportToday;
            var load = Ui.Button("Tampilkan", 0, Ui.Accent);
            var export = Ui.Button("Download Laporan");

            top.Controls.Add(Ui.Label("Dari")); top.Controls.Add(_from);
            top.Controls.Add(Ui.Label("Sampai")); top.Controls.Add(_to);
            top.Controls.Add(load); top.Controls.Add(export);

            Controls.Add(top); Controls.Add(_grid); _grid.BringToFront();
            load.Click += delegate { Reload(); };
            export.Click += delegate { Export(); };
            Reload();
        }

        private void Reload()
        {
            _grid.DataSource = ReportService.StockIn(_from.Value.Date, _to.Value.Date, _includeSupplier);
        }

        private void Export()
        {
            var dt = ReportService.StockIn(_from.Value.Date, _to.Value.Date, _includeSupplier);
            using (var d = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "stok_masuk_" + _from.Value.ToString("yyyyMMdd") + "_" + _to.Value.ToString("yyyyMMdd") + ".xlsx" })
                if (d.ShowDialog() == DialogResult.OK) { try { ExcelBackupService.ExportTable(dt, d.FileName, "Stok Masuk"); MessageBox.Show("Download selesai."); } catch (Exception ex) { Ui.MessageError(ex); } }
        }
    }

    internal class SalesReportTab : TabPage
    {
        private readonly AppUser _user;
        private readonly string _payment;
        private readonly bool _combined;
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly ComboBox _discount = new ComboBox();
        private readonly DataGridView _grid = Ui.Grid();
        private readonly Label _summary = new Label();

        public SalesReportTab(string title, AppUser user, string payment, bool combined) : base(title)
        {
            _user = user; _payment = payment; _combined = combined;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            var reportToday = TransactionService.GetReportDate(DateTime.Now);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(4) };
            _from.Width = 115; _from.Format = DateTimePickerFormat.Short; _from.Value = reportToday;
            _to.Width = 115; _to.Format = DateTimePickerFormat.Short; _to.Value = reportToday;
            _discount.Width = 120; _discount.DropDownStyle = ComboBoxStyle.DropDownList; _discount.Items.AddRange(new object[] { "Semua diskon", "Dengan diskon", "Tanpa diskon" }); _discount.SelectedIndex = 0;
            _discount.Font = new Font("Segoe UI", 9F);

            var load = Ui.Button("Tampilkan", 0, Ui.Accent);
            var export = Ui.Button("Download Laporan");
            var print = Ui.Button("Cetak Struk");

            top.Controls.Add(Ui.Label("Dari")); top.Controls.Add(_from);
            top.Controls.Add(Ui.Label("Sampai")); top.Controls.Add(_to);
            top.Controls.Add(_discount); top.Controls.Add(load);
            top.Controls.Add(export); top.Controls.Add(print);

            if (_combined && _user.IsSuperAdmin)
            {
                var cancel = Ui.Button("Batalkan Transaksi", 0, Ui.Danger);
                top.Controls.Add(cancel);
                cancel.Click += delegate { CancelSelected(); };
            }
            Controls.Add(top);

            _summary.Dock = DockStyle.Top; _summary.Height = 36; _summary.Padding = new Padding(8, 8, 0, 0);
            _summary.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            _summary.ForeColor = Color.FromArgb(47, 55, 65);
            Controls.Add(_summary);

            Controls.Add(_grid); _grid.BringToFront();
            load.Click += delegate { Reload(); }; export.Click += delegate { Export(); }; print.Click += delegate { PrintSelected(); };
            Reload();
        }

        private void Reload()
        {
            var df = _discount.SelectedIndex == 1 ? 1 : (_discount.SelectedIndex == 2 ? 0 : -1);
            _grid.DataSource = ReportService.SalesOperational(_from.Value.Date, _to.Value.Date, _payment, null, df);
            var summary = ReportService.SalesOperationalSummary(_from.Value.Date, _to.Value.Date);
            if (summary.Rows.Count == 0)
            {
                _summary.Text = "Belum ada data pada periode ini.";
                return;
            }
            var s = summary.Rows[0];
            var sesi = "Sebelum 15:00: " + Convert.ToString(s["Transaksi Sebelum 15:00"]) + " trx | Sesudah 15:00: " + Convert.ToString(s["Transaksi Sesudah 15:00"]) + " trx";
            if (_payment == "TUNAI") _summary.Text = "Tunai - Bruto: " + Ui.Rp(s["Bruto Tunai"]) + " | Diskon: " + Ui.Rp(s["Diskon Tunai"]) + " | Bersih: " + Ui.Rp(s["Total Tunai"]) + " | " + sesi;
            else if (_payment == "QRIS") _summary.Text = "QRIS - Bruto: " + Ui.Rp(s["Bruto QRIS"]) + " | Diskon: " + Ui.Rp(s["Diskon QRIS"]) + " | Bersih: " + Ui.Rp(s["Total QRIS"]) + " | " + sesi;
            else _summary.Text = "Tunai: " + Ui.Rp(s["Total Tunai"]) + " | QRIS: " + Ui.Rp(s["Total QRIS"]) + " | Diskon: " + Ui.Rp(s["Total Diskon"]) + " | Bersih: " + Ui.Rp(s["Total Penjualan Bersih"]) + " | Batal: " + s["Transaksi Dibatalkan"] + " | " + sesi;
        }

        private void Export()
        {
            var df = _discount.SelectedIndex == 1 ? 1 : (_discount.SelectedIndex == 2 ? 0 : -1);
            var dt = ReportService.SalesOperational(_from.Value.Date, _to.Value.Date, _payment, null, df);
            using (var d = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "penjualan_" + (_payment == "" ? "gabungan" : _payment.ToLowerInvariant()) + "_" + _from.Value.ToString("yyyyMMdd") + "_" + _to.Value.ToString("yyyyMMdd") + ".xlsx" })
                if (d.ShowDialog() == DialogResult.OK) { try { ExcelBackupService.ExportTable(dt, d.FileName, "Penjualan"); MessageBox.Show("Download selesai."); } catch (Exception ex) { Ui.MessageError(ex); } }
        }

        private void PrintSelected()
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains("id")) return;
            var idValue = _grid.CurrentRow.Cells["id"].Value;
            if (idValue == null || idValue == DBNull.Value) return;
            try { ReceiptService.PrintSale(Convert.ToInt64(idValue)); } catch (Exception ex) { Ui.MessageError(ex, "Cetak Struk"); }
        }

        private void CancelSelected()
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains("id")) return;
            var idValue = _grid.CurrentRow.Cells["id"].Value;
            if (idValue == null || idValue == DBNull.Value) return;
            var status = Convert.ToString(_grid.CurrentRow.Cells["Status"].Value);
            if (status != "FINAL") { MessageBox.Show("Hanya transaksi berstatus FINAL yang dapat dibatalkan."); return; }
            using (var p = new TextPromptDialog("Pembatalan Transaksi", "Alasan pembatalan (wajib):"))
            {
                if (p.ShowDialog() != DialogResult.OK) return;
                if (string.IsNullOrWhiteSpace(p.Value)) { MessageBox.Show("Alasan wajib diisi."); return; }
                if (MessageBox.Show("Batalkan transaksi dan kembalikan stok? Aksi ini tercatat di audit.", "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { TransactionService.CancelSale(_user, Convert.ToInt64(idValue), p.Value); Reload(); MessageBox.Show("Transaksi dibatalkan. Stok dikembalikan dan audit dibuat."); } catch (Exception ex) { Ui.MessageError(ex, "Pembatalan"); }
            }
        }
    }

    internal class DateReportTab : TabPage
    {
        private readonly Func<DateTime, DateTime, DataTable> _loader;
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = Ui.Grid();
        public DateReportTab(string title, Func<DateTime, DateTime, DataTable> loader) : base(title)
        {
            _loader = loader;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            var reportToday = TransactionService.GetReportDate(DateTime.Now);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(4) };
            _from.Width = 115; _from.Format = DateTimePickerFormat.Short; _from.Value = reportToday.AddDays(-30);
            _to.Width = 115; _to.Format = DateTimePickerFormat.Short; _to.Value = reportToday;

            var load = Ui.Button("Tampilkan", 0, Ui.Accent);
            var export = Ui.Button("Download Laporan");

            top.Controls.Add(Ui.Label("Dari")); top.Controls.Add(_from);
            top.Controls.Add(Ui.Label("Sampai")); top.Controls.Add(_to);
            top.Controls.Add(load); top.Controls.Add(export);

            Controls.Add(top); Controls.Add(_grid); _grid.BringToFront();
            load.Click += delegate { Reload(); }; export.Click += delegate { Export(); }; Reload();
        }
        private void Reload()
        {
            _grid.DataSource = _loader(_from.Value.Date, _to.Value.Date);
        }
        private void Export()
        {
            var dt = _loader(_from.Value.Date, _to.Value.Date);
            using (var d = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = Text.ToLowerInvariant() + "_" + _from.Value.ToString("yyyyMMdd") + "_" + _to.Value.ToString("yyyyMMdd") + ".xlsx" })
                if (d.ShowDialog() == DialogResult.OK) { try { ExcelBackupService.ExportTable(dt, d.FileName, Text); MessageBox.Show("Download selesai."); } catch (Exception ex) { Ui.MessageError(ex); } }
        }
    }

    internal class StaticReportTab : TabPage
    {
        private readonly Func<DataTable> _loader;
        private readonly string _sheet;
        private readonly DataGridView _grid = Ui.Grid();
        public StaticReportTab(string title, Func<DataTable> loader, string sheet) : base(title)
        {
            _loader = loader; _sheet = sheet;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(4) };
            var load = Ui.Button("Refresh", 0, Ui.Accent);
            var export = Ui.Button("Download Laporan");
            top.Controls.Add(load); top.Controls.Add(export);

            Controls.Add(top); Controls.Add(_grid); _grid.BringToFront();
            load.Click += delegate { Reload(); }; export.Click += delegate { Export(); }; Reload();
        }
        private void Reload() { _grid.DataSource = _loader(); }
        private void Export()
        {
            var dt = _grid.DataSource as DataTable; if (dt == null) return;
            using (var d = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = Text.ToLowerInvariant() + "_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx" })
                if (d.ShowDialog() == DialogResult.OK) { try { ExcelBackupService.ExportTable(dt, d.FileName, _sheet); MessageBox.Show("Export selesai."); } catch (Exception ex) { Ui.MessageError(ex); } }
        }
    }

    internal class ActivityPanel : UserControl
    {
        private readonly AppUser _user;
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly ComboBox _action = new ComboBox();
        private readonly TextBox _username = new TextBox();
        private readonly DataGridView _grid = Ui.Grid();

        public ActivityPanel(AppUser user)
        {
            _user = user;
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = user.IsSuperAdmin ? 82 : 48, AutoScroll = true, WrapContents = true, Padding = new Padding(6) };
            _from.Width = 115; _from.Format = DateTimePickerFormat.Short; _from.Value = DateTime.Today.AddDays(-30);
            _to.Width = 115; _to.Format = DateTimePickerFormat.Short; _to.Value = DateTime.Today;
            _action.Width = 150; _action.DropDownStyle = ComboBoxStyle.DropDownList;
            _action.Items.AddRange(new object[]
            {
                "Semua aktivitas", "LOGIN", "LOGIN_GAGAL", "PENJUALAN", "STOK_MASUK",
                "STOK_KELUAR", "PENYESUAIAN", "PEMBATALAN", "DISKON",
                "BACKUP", "RESTORE"
            });
            _action.SelectedIndex = 0;
            _username.Width = 120;

            top.Controls.Add(Ui.Label("Dari")); top.Controls.Add(_from);
            top.Controls.Add(Ui.Label("Sampai")); top.Controls.Add(_to);
            top.Controls.Add(Ui.Label("Aksi")); top.Controls.Add(_action);
            if (_user.IsSuperAdmin)
            {
                top.Controls.Add(Ui.Label("User")); top.Controls.Add(_username);
            }
            var load = Ui.Button("Tampilkan", 105, Ui.Accent);
            var export = Ui.Button("Download Laporan");
            top.Controls.Add(load); top.Controls.Add(export);

            Controls.Add(top); Controls.Add(_grid); _grid.BringToFront();
            load.Click += delegate { Reload(); };
            export.Click += delegate { Export(); };
            Reload();
        }

        private void Reload()
        {
            var action = _action.SelectedIndex <= 0 ? "" : Convert.ToString(_action.SelectedItem);
            _grid.DataSource = ReportService.Activity(_user, _from.Value.Date, _to.Value.Date, null, action, _username.Text);
            if (!_user.IsSuperAdmin)
            {
                HideColumn("Role"); HideColumn("Batch"); HideColumn("Diskon"); HideColumn("Alasan");
                HideColumn("Nilai Sebelum"); HideColumn("Nilai Sesudah");
            }
        }

        private void HideColumn(string name)
        {
            if (_grid.Columns.Contains(name)) _grid.Columns[name].Visible = false;
        }

        private void Export()
        {
            var dt = _grid.DataSource as DataTable;
            if (dt == null) return;
            using (var d = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "aktivitas_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx" })
                if (d.ShowDialog() == DialogResult.OK)
                {
                    try { ExcelBackupService.ExportTable(dt, d.FileName, "Aktivitas"); MessageBox.Show("Export selesai."); }
                    catch (Exception ex) { Ui.MessageError(ex, "Export Aktivitas"); }
                }
        }
    }
}
