using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    internal class MedicinePanel : UserControl
    {
        private readonly AppUser _user;
        private readonly DataGridView _grid = Ui.Grid();
        private readonly TextBox _search = new TextBox { Width = 300 };
        private readonly CheckBox _inactive = new CheckBox { Text = "Tampilkan nonaktif", AutoSize = true, Padding = new Padding(8, 8, 0, 0) };

        public MedicinePanel(AppUser user)
        {
            _user = user; Dock = DockStyle.Fill; BackColor = Ui.Background;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White, Padding = new Padding(6) };
            _search.Font = new Font("Segoe UI", 9.5F);
            _inactive.Font = new Font("Segoe UI", 9F);
            _inactive.ForeColor = Color.FromArgb(71, 85, 105);

            top.Controls.Add(Ui.Label("Cari Obat", 6)); top.Controls.Add(_search);

            if (_user.CanPurchaseEffective)
            {
                var stockIn = Ui.Button("Stok Masuk", 0, Ui.Success);
                top.Controls.Add(stockIn);
                stockIn.Click += delegate
                {
                    using (var f = new StockInForm(_user))
                    {
                        f.ShowDialog();
                    }
                    Reload();
                };
            }

            if (_user.IsSuperAdmin)
            {
                var edit = Ui.Button("Ubah");
                var toggle = Ui.Button("Aktif / Nonaktif", 0, Ui.Danger);
                top.Controls.Add(edit); top.Controls.Add(toggle); top.Controls.Add(_inactive);

                edit.Click += delegate { if (_grid.CurrentRow == null) return; using (var f = new MedicineDialog(_user, _grid.CurrentRow)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
                toggle.Click += delegate
                {
                    if (_grid.CurrentRow == null) return;
                    try
                    {
                        var id = Convert.ToInt64(_grid.CurrentRow.Cells["id"].Value);
                        var active = Convert.ToString(_grid.CurrentRow.Cells["Status"].Value) == "Aktif";
                        var msg = active ? "Nonaktifkan obat ini? Histori transaksi tetap disimpan." : "Aktifkan kembali obat ini?";
                        if (MessageBox.Show(msg, "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { MedicineService.SetActive(_user, id, !active); Reload(); }
                    }
                    catch (Exception ex) { Ui.MessageError(ex); }
                };
            }

            var batches = Ui.Button("Lihat Batch"); top.Controls.Add(batches);
            batches.Click += delegate { ShowBatches(); };

            _search.TextChanged += delegate { Reload(); }; _inactive.CheckedChanged += delegate { Reload(); };

            var gridWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
            gridWrapper.Controls.Add(_grid); _grid.BringToFront();
            Controls.Add(gridWrapper);
            Controls.Add(top);
            Reload();
        }

        private void Reload()
        {
            _grid.DataSource = MedicineService.List(_search.Text, _user.IsSuperAdmin && _inactive.Checked);
        }

        private void ShowBatches()
        {
            if (_grid.CurrentRow == null) return;
            var id = Convert.ToInt64(_grid.CurrentRow.Cells["id"].Value);
            var name = Convert.ToString(_grid.CurrentRow.Cells["Nama Barang"].Value);
            using (var f = new Form { Text = "Batch - " + name, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(900, 480), BackColor = Color.White, Font = new Font("Segoe UI", 9.5F) })
            {
                var g = Ui.Grid();
                Action reload = delegate { g.DataSource = MedicineService.Batches(id); };
                if (_user.IsSuperAdmin)
                {
                    var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(6) };
                    var edit = Ui.Button("Koreksi Batch / Expired", 0, Ui.Accent);
                    top.Controls.Add(edit); f.Controls.Add(top);
                    edit.Click += delegate
                    {
                        if (g.CurrentRow == null) return;
                        using (var d = new BatchCorrectionDialog(_user, g.CurrentRow)) if (d.ShowDialog() == DialogResult.OK) reload();
                    };
                }
                var wrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
                wrapper.Controls.Add(g); g.BringToFront();
                f.Controls.Add(wrapper);
                reload(); f.ShowDialog();
            }
        }
    }

    internal class StockInForm : Form
    {
        public StockInForm(AppUser user)
        {
            Text = "Stok Masuk / Faktur Masuk";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1180, 700);
            BackColor = Color.White;
            Controls.Add(new PurchasePanel(user));
        }
    }

    internal class MedicineDialog : Form
    {
        private readonly AppUser _user;
        private readonly long _id;
        private readonly Dictionary<string, TextBox> _text = new Dictionary<string, TextBox>();
        private readonly ComboBox _category = new ComboBox();
        private NumericUpDown _awl, _hb, _hj, _min;
        private int _msk, _klr;

        public MedicineDialog(AppUser user, DataGridViewRow row)
        {
            _user = user; _id = row == null ? 0 : Convert.ToInt64(row.Cells["id"].Value);
            Text = _id == 0 ? "Data Obat" : "Ubah Obat"; ClientSize = new Size(640, 540); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24), AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddText(table, "Kode", "Kode", row); AddText(table, "Nama Barang", "Nama Barang", row); AddText(table, "Satuan", "SATUAN", row);
            AddLookup(table, "Kategori", _category);
            _awl = AddNum(table, "Stok Awal (AWL)", row == null ? 0 : Convert.ToDecimal(row.Cells["AWL"].Value), _id == 0);
            _msk = row == null ? 0 : Convert.ToInt32(row.Cells["MSK"].Value); _klr = row == null ? 0 : Convert.ToInt32(row.Cells["KLR"].Value);
            table.Controls.Add(new Label { Text = "MSK / KLR", AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) });
            table.Controls.Add(new Label { Text = _msk + " / " + _klr + " (diubah oleh transaksi/koreksi)", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(0, 6, 0, 0) });
            _hb = AddMoney(table, "Harga Beli", row, "Harga Beli"); _hj = AddMoney(table, "Harga Jual", row, "Harga Jual"); _min = AddNum(table, "Stok Minimal", row == null ? 0 : Convert.ToDecimal(row.Cells["Stok Minimal"].Value), true);
            LoadLookupValues(_category, MasterService.Categories(false), "Nama", row == null ? null : Convert.ToString(row.Cells["Kategori"].Value));

            var info = new Label { Text = _id == 0 ? "AWL hanya dipakai saat obat baru dibuat. Kategori bisa dipilih dari daftar atau diketik baru." : "AWL, MSK dan KLR tidak diedit langsung agar histori stok tetap konsisten.", AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8F) };
            table.Controls.Add(new Label(), 0, 10); table.Controls.Add(info, 1, 10);

            var save = Ui.Button("Simpan", 160, Ui.Success); save.Click += Save; table.Controls.Add(new Label(), 0, 11); table.Controls.Add(save, 1, 11);
            Controls.Add(table); AcceptButton = save;
        }

        private void AddText(TableLayoutPanel p, string label, string column, DataGridViewRow row)
        {
            var tb = new TextBox { Dock = DockStyle.Fill };
            if (row != null && row.DataGridView.Columns.Contains(column)) tb.Text = Convert.ToString(row.Cells[column].Value);
            _text[label] = tb; p.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) }); p.Controls.Add(tb);
        }

        private void AddLookup(TableLayoutPanel p, string label, ComboBox combo)
        {
            combo.Dock = DockStyle.Fill;
            combo.DropDownStyle = ComboBoxStyle.DropDown;
            combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            combo.Font = new Font("Segoe UI", 9.5F);
            p.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) });
            p.Controls.Add(combo);
        }

        private static void LoadLookupValues(ComboBox combo, DataTable source, string displayMember, string selected)
        {
            combo.DataSource = source;
            combo.DisplayMember = displayMember;
            combo.ValueMember = displayMember;
            combo.SelectedIndex = -1;
            combo.Text = selected ?? "";
        }

        private NumericUpDown AddNum(TableLayoutPanel p, string label, decimal value, bool enabled)
        {
            var n = new NumericUpDown { Maximum = 100000000, Width = 190, Value = Math.Max(0, Math.Min(100000000, value)), Enabled = enabled };
            p.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) }); p.Controls.Add(n); return n;
        }

        private NumericUpDown AddMoney(TableLayoutPanel p, string label, DataGridViewRow row, string column)
        {
            var n = new NumericUpDown { Maximum = 1000000000, DecimalPlaces = 2, ThousandsSeparator = true, Width = 220 };
            if (row != null && row.DataGridView.Columns.Contains(column)) n.Value = Convert.ToDecimal(row.Cells[column].Value);
            p.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) }); p.Controls.Add(n); return n;
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                var awl = _id == 0 ? (int)_awl.Value : 0;
                MedicineService.Save(_user, _id, _text["Kode"].Text, _text["Nama Barang"].Text, _text["Satuan"].Text, awl, _msk, _klr,
                    (double)_hb.Value, (double)_hj.Value, (int)_min.Value, _category.Text, "", false);
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { Ui.MessageError(ex, "Validasi Obat"); }
        }
    }

    internal class BatchCorrectionDialog : Form
    {
        private readonly AppUser _user;
        private readonly long _batchId;
        private readonly TextBox _batch = new TextBox();
        private readonly DateTimePicker _expired = new DateTimePicker();
        private readonly CheckBox _hasExpired = new CheckBox { Text = "Ada expired date", AutoSize = true };
        private readonly TextBox _reason = new TextBox();

        public BatchCorrectionDialog(AppUser user, DataGridViewRow row)
        {
            _user = user; _batchId = Convert.ToInt64(row.Cells["id"].Value);
            Text = "Koreksi Batch / Expired"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 330);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _batch.Text = Convert.ToString(row.Cells["Batch"].Value);
            _expired.Format = DateTimePickerFormat.Short;
            var expText = Convert.ToString(row.Cells["Expired"].Value);
            DateTime exp; if (DateTime.TryParse(expText, out exp)) { _hasExpired.Checked = true; _expired.Value = exp; } else { _hasExpired.Checked = false; }
            _expired.Enabled = _hasExpired.Checked; _reason.Multiline = true; _reason.Height = 80;
            table.Controls.Add(new Label { Text = "Nomor batch", AutoSize = true, Padding = new Padding(0, 7, 0, 0), Font = new Font("Segoe UI Semibold", 9F) }); table.Controls.Add(_batch);
            table.Controls.Add(new Label { Text = "Expired", AutoSize = true, Padding = new Padding(0, 7, 0, 0), Font = new Font("Segoe UI Semibold", 9F) });
            var expPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 35 }; expPanel.Controls.Add(_hasExpired); expPanel.Controls.Add(_expired); table.Controls.Add(expPanel);
            table.Controls.Add(new Label { Text = "Alasan koreksi", AutoSize = true, Padding = new Padding(0, 7, 0, 0), Font = new Font("Segoe UI Semibold", 9F) }); table.Controls.Add(_reason);
            var save = Ui.Button("Simpan Koreksi", 150, Ui.Success); table.Controls.Add(new Label(), 0, 3); table.Controls.Add(save, 1, 3); Controls.Add(table);
            _hasExpired.CheckedChanged += delegate { _expired.Enabled = _hasExpired.Checked; };
            save.Click += delegate
            {
                try
                {
                    MedicineService.CorrectBatch(_user, _batchId, _batch.Text, _hasExpired.Checked ? (DateTime?)_expired.Value.Date : null, _reason.Text);
                    DialogResult = DialogResult.OK; Close();
                }
                catch (Exception ex) { Ui.MessageError(ex, "Koreksi Batch"); }
            };
        }
    }
}
