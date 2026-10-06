using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    internal class StockAdjustmentPanel : UserControl
    {
        private readonly AppUser _user;
        private readonly ComboBox _medicine = new ComboBox();
        private readonly NumericUpDown _delta = new NumericUpDown();
        private readonly TextBox _reason = new TextBox();
        private readonly Label _currentStock = new Label();
        private readonly DataGridView _grid = Ui.Grid();

        public StockAdjustmentPanel(AppUser user)
        {
            _user = user; Dock = DockStyle.Fill;
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 128, AutoScroll = true, WrapContents = true, Padding = new Padding(6) };
            _medicine.Width = 320; _medicine.DropDownStyle = ComboBoxStyle.DropDownList; _medicine.DataSource = MedicineService.List(); _medicine.DisplayMember = "Nama Barang"; _medicine.ValueMember = "id";
            _delta.Minimum = -1000000; _delta.Maximum = 1000000; _delta.Width = 110;
            _reason.Width = 380;
            var save = Ui.Button("Simpan Koreksi", 135);
            var plus1 = Ui.Button("+1", 44); var plus5 = Ui.Button("+5", 44); var minus1 = Ui.Button("-1", 44); var minus5 = Ui.Button("-5", 44); var clear = Ui.Button("Reset", 60);
            _currentStock.AutoSize = true; _currentStock.Padding = new Padding(6, 10, 0, 0); _currentStock.ForeColor = Color.FromArgb(71, 85, 105);
            top.Controls.Add(Ui.Label("Obat")); top.Controls.Add(_medicine); top.Controls.Add(_currentStock);
            top.Controls.Add(Ui.Label("Delta (+/-)")); top.Controls.Add(_delta);
            top.Controls.Add(plus1); top.Controls.Add(plus5); top.Controls.Add(minus1); top.Controls.Add(minus5); top.Controls.Add(clear);
            top.Controls.Add(Ui.Label("Alasan wajib")); top.Controls.Add(_reason); top.Controls.Add(save);
            Controls.Add(top); Controls.Add(_grid); _grid.BringToFront();
            _medicine.SelectedIndexChanged += delegate { Reload(); RefreshCurrentStock(); };
            plus1.Click += delegate { BumpDelta(1); };
            plus5.Click += delegate { BumpDelta(5); };
            minus1.Click += delegate { BumpDelta(-1); };
            minus5.Click += delegate { BumpDelta(-5); };
            clear.Click += delegate { _delta.Value = 0; };
            save.Click += delegate
            {
                try
                {
                    if (_medicine.SelectedValue == null) return;
                    if (MessageBox.Show("Koreksi stok akan tercatat di audit. Lanjut?", "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    MedicineService.AdjustStock(_user, Convert.ToInt64(_medicine.SelectedValue), Convert.ToInt32(_delta.Value), _reason.Text);
                    _delta.Value = 0; _reason.Clear(); Reload();
                }
                catch (Exception ex) { Ui.MessageError(ex, "Koreksi Stok"); }
            };
            Reload();
            RefreshCurrentStock();
        }

        private void BumpDelta(int step)
        {
            var value = (int)_delta.Value + step;
            if (value < _delta.Minimum) value = (int)_delta.Minimum;
            if (value > _delta.Maximum) value = (int)_delta.Maximum;
            _delta.Value = value;
        }

        private void RefreshCurrentStock()
        {
            var row = _medicine.SelectedItem as DataRowView;
            if (row == null)
            {
                _currentStock.Text = "Stok saat ini: -";
                return;
            }
            _currentStock.Text = "Stok saat ini: " + Convert.ToString(row.Row["SISA"]) + " | Harga jual: " + Ui.Rp(Convert.ToDouble(row.Row["Harga Jual"]));
        }

        private void Reload()
        {
            if (_medicine.SelectedValue == null) return;
            _grid.DataSource = ReportService.StockCard(Convert.ToInt64(_medicine.SelectedValue));
        }
    }

    internal class StockCardPanel : UserControl
    {
        private readonly AppUser _user;
        private readonly ComboBox _medicine = new ComboBox();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly TextBox _batch = new TextBox();
        private readonly TextBox _type = new TextBox();
        private readonly TextBox _code = new TextBox();
        private readonly TextBox _username = new TextBox();
        private readonly DataGridView _grid = Ui.Grid();

        public StockCardPanel(AppUser user)
        {
            _user = user; Dock = DockStyle.Fill;
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, AutoScroll = true, WrapContents = true };
            _medicine.Width = 280; _medicine.DropDownStyle = ComboBoxStyle.DropDownList; _medicine.DataSource = MedicineService.List(); _medicine.DisplayMember = "Nama Barang"; _medicine.ValueMember = "id";
            _from.Width = 120; _from.Format = DateTimePickerFormat.Short; _from.Value = DateTime.Today.AddDays(-30);
            _to.Width = 120; _to.Format = DateTimePickerFormat.Short; _to.Value = DateTime.Today;
            _batch.Width = 110; _type.Width = 120; _code.Width = 100; _username.Width = 110;
            var load = Ui.Button("Tampilkan", 105); var export = Ui.Button("Export Excel", 110);
            top.Controls.Add(Ui.Label("Obat")); top.Controls.Add(_medicine); top.Controls.Add(Ui.Label("Dari")); top.Controls.Add(_from); top.Controls.Add(Ui.Label("Sampai")); top.Controls.Add(_to);
            top.Controls.Add(Ui.Label("Batch")); top.Controls.Add(_batch); top.Controls.Add(Ui.Label("Jenis")); top.Controls.Add(_type); top.Controls.Add(Ui.Label("Kode")); top.Controls.Add(_code);
            if (_user.IsSuperAdmin) { top.Controls.Add(Ui.Label("User")); top.Controls.Add(_username); }
            top.Controls.Add(load); top.Controls.Add(export);
            Controls.Add(top); Controls.Add(_grid); _grid.BringToFront();
            load.Click += delegate { Reload(); };
            export.Click += delegate { Export(); };
            _medicine.SelectedIndexChanged += delegate { Reload(); };
            Reload();
        }

        private void Reload()
        {
            if (_medicine.SelectedValue == null) return;
            _grid.DataSource = ReportService.StockCard(Convert.ToInt64(_medicine.SelectedValue), _from.Value.Date, _to.Value.Date, _batch.Text, _type.Text, _code.Text, _user.IsSuperAdmin ? _username.Text : "");
            if (!_user.IsSuperAdmin && _grid.Columns.Contains("User")) _grid.Columns["User"].Visible = false;
        }

        private void Export()
        {
            var dt = _grid.DataSource as DataTable; if (dt == null) return;
            using (var d = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "kartu_stok_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx" })
                if (d.ShowDialog() == DialogResult.OK)
                {
                    try { ExcelBackupService.ExportTable(dt, d.FileName, "Kartu Stok"); MessageBox.Show("Export selesai."); } catch (Exception ex) { Ui.MessageError(ex); }
                }
        }
    }

    internal class StockOpnamePanel : UserControl
    {
        private readonly AppUser _user;
        private readonly ComboBox _opname = new ComboBox();
        private readonly Label _status = new Label();
        private readonly TextBox _search = new TextBox();
        private readonly ListBox _itemResults = new ListBox();
        private readonly Label _itemDetail = new Label();
        private readonly Label _summary = new Label();
        private readonly NumericUpDown _physical = new NumericUpDown();
        private readonly TextBox _reason = new TextBox();
        private readonly DataGridView _grid = Ui.Grid();
        private readonly Button _saveCount;
        private readonly Button _complete;
        private readonly Button _cancel;
        private DataTable _itemsTable;
        private bool _syncingSelection;

        public StockOpnamePanel(AppUser user)
        {
            UserService.RequireSuper(user);
            _user = user; Dock = DockStyle.Fill; BackColor = Ui.Background;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Ui.Background };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

            var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, AutoScroll = true, WrapContents = false, BackColor = Color.White, Padding = new Padding(8) };
            _opname.Width = 290; _opname.DropDownStyle = ComboBoxStyle.DropDownList; _opname.Font = new Font("Segoe UI", 9F);
            _status.AutoSize = true; _status.Padding = new Padding(8, 8, 18, 0); _status.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            var start = Ui.Button("Opname Baru", 105, Ui.Accent);
            var refresh = Ui.Button("Refresh", 80);
            _saveCount = Ui.Button("Simpan Hitungan", 125, Ui.Success);
            _complete = Ui.Button("Posting Stok Opname", 155, Ui.Success);
            _cancel = Ui.Button("Batalkan", 90, Ui.Danger);
            var export = Ui.Button("Export Excel", 105);

            top.Controls.Add(Ui.Label("Sesi Opname")); top.Controls.Add(_opname); top.Controls.Add(_status);
            top.Controls.Add(start); top.Controls.Add(refresh); top.Controls.Add(_complete);
            if (_user.IsSuperAdmin) { top.Controls.Add(_cancel); }
            top.Controls.Add(export);
            left.Controls.Add(_grid);
            left.Controls.Add(top);
            _grid.BringToFront();

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18) };
            right.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, right.Width - 1, right.Height - 1);
            };

            var y = 18;
            var inputTitle = new Label { Text = "Input Stok Fisik", AutoSize = true, Location = new Point(18, y), Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            right.Controls.Add(inputTitle); y += 42;

            var searchLabel = Ui.Label("Cari Obat / Batch", 0, 9.5F, FontStyle.Bold);
            searchLabel.Location = new Point(18, y); right.Controls.Add(searchLabel); y += 24;
            _search.Location = new Point(18, y); _search.Width = 306; _search.Font = new Font("Segoe UI", 10F);
            right.Controls.Add(_search); y += 32;

            _itemResults.Location = new Point(18, y); _itemResults.Width = 306; _itemResults.Height = 130; _itemResults.Font = new Font("Segoe UI", 9.5F);
            right.Controls.Add(_itemResults); y += 144;

            var detailBox = new GroupBox { Text = "Detail Item", Location = new Point(18, y), Width = 306, Height = 150, Font = new Font("Segoe UI Semibold", 8.5F) };
            _itemDetail.Location = new Point(12, 22); _itemDetail.Width = 278; _itemDetail.Height = 112; _itemDetail.Font = new Font("Segoe UI", 9F); _itemDetail.ForeColor = Color.FromArgb(51, 65, 85);
            detailBox.Controls.Add(_itemDetail);
            right.Controls.Add(detailBox); y += 164;

            var physicalLabel = Ui.Label("Stok Fisik", 0, 9.5F, FontStyle.Bold);
            physicalLabel.Location = new Point(18, y); right.Controls.Add(physicalLabel); y += 24;
            _physical.Location = new Point(18, y); _physical.Width = 130; _physical.Minimum = 0; _physical.Maximum = 10000000; _physical.Font = new Font("Segoe UI", 11F);
            right.Controls.Add(_physical);
            _saveCount.Location = new Point(158, y - 2); _saveCount.Width = 166; _saveCount.Height = 36;
            right.Controls.Add(_saveCount); y += 48;

            var reasonLabel = Ui.Label("Alasan Selisih", 0, 9.5F, FontStyle.Bold);
            reasonLabel.Location = new Point(18, y); right.Controls.Add(reasonLabel); y += 24;
            _reason.Location = new Point(18, y); _reason.Width = 306; _reason.Height = 72; _reason.Multiline = true; _reason.ScrollBars = ScrollBars.Vertical;
            right.Controls.Add(_reason); y += 88;

            _summary.Location = new Point(18, y); _summary.Width = 306; _summary.Height = 110; _summary.Font = new Font("Segoe UI Semibold", 9F); _summary.ForeColor = Color.FromArgb(71, 85, 105);
            right.Controls.Add(_summary);

            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);
            Controls.Add(layout);

            start.Click += delegate { StartNew(); };
            refresh.Click += delegate { LoadOpnames(); };
            _opname.SelectedIndexChanged += delegate { ReloadItems(); };
            _grid.SelectionChanged += delegate { LoadSelectedItem(); };
            _search.TextChanged += delegate { FilterItemResults(); };
            _itemResults.SelectedIndexChanged += delegate { SelectItemFromSearch(); };
            _itemResults.DoubleClick += delegate { _physical.Focus(); _physical.Select(0, _physical.Text.Length); };
            _saveCount.Click += delegate { SaveCount(); };
            _complete.Click += delegate { Complete(); };
            _cancel.Click += delegate { Cancel(); };
            export.Click += delegate { Export(); };
            LoadOpnames();
        }

        private long SelectedOpnameId
        {
            get
            {
                if (_opname.SelectedValue == null || _opname.SelectedValue is DataRowView) return 0;
                long id; return long.TryParse(Convert.ToString(_opname.SelectedValue), out id) ? id : 0;
            }
        }

        private string SelectedStatus
        {
            get
            {
                var row = _opname.SelectedItem as DataRowView;
                return row == null ? null : Convert.ToString(row["Status"]);
            }
        }

        private long SelectedItemId
        {
            get
            {
                if (_grid.CurrentRow == null || !_grid.Columns.Contains("id")) return 0;
                long id; return long.TryParse(Convert.ToString(_grid.CurrentRow.Cells["id"].Value), out id) ? id : 0;
            }
        }

        private void LoadOpnames(long selectId = 0)
        {
            try
            {
                var table = StockOpnameService.List(_user);
                _opname.DataSource = table;
                _opname.DisplayMember = "Opname"; _opname.ValueMember = "id";
                if (selectId > 0) _opname.SelectedValue = selectId;
                UpdateButtons();
            }
            catch (Exception ex) { Ui.MessageError(ex, "Stok Opname"); }
        }

        private void ReloadItems()
        {
            var id = SelectedOpnameId;
            _itemsTable = id == 0 ? null : StockOpnameService.Items(id);
            _grid.DataSource = _itemsTable;
            FormatOpnameGrid();
            FilterItemResults();
            LoadSelectedItem(); UpdateButtons();
        }

        private void LoadSelectedItem()
        {
            if (_syncingSelection) return;
            var row = _grid.CurrentRow;
            if (row == null || !_grid.Columns.Contains("Stok Fisik"))
            {
                _itemDetail.Text = "Pilih item stok opname.";
                return;
            }
            var value = row.Cells["Stok Fisik"].Value;
            _physical.Value = value == null || value == DBNull.Value ? 0 : Math.Max(0, Math.Min(_physical.Maximum, Convert.ToDecimal(value)));
            _reason.Text = Convert.ToString(row.Cells["Alasan"].Value);
            UpdateItemDetail(row);
            SyncSearchSelectionFromGrid(row);
            UpdateSummary();
        }

        private void FilterItemResults()
        {
            var result = new DataTable();
            result.Columns.Add("id", typeof(long));
            result.Columns.Add("Display");
            if (_itemsTable != null)
            {
                var term = (_search.Text ?? "").Trim();
                foreach (DataRow row in _itemsTable.Rows)
                {
                    var obat = Convert.ToString(row["Obat"]);
                    var batch = Convert.ToString(row["Batch"]);
                    var expired = Convert.ToString(row["Expired"]);
                    var haystack = obat + " " + batch + " " + expired;
                    if (term.Length > 0 && haystack.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    result.Rows.Add(Convert.ToInt64(row["id"]), obat + " | Batch " + batch + " | ED " + expired);
                }
            }

            _syncingSelection = true;
            try
            {
                _itemResults.DataSource = result;
                _itemResults.DisplayMember = "Display";
                _itemResults.ValueMember = "id";
                if (result.Rows.Count > 0) _itemResults.SelectedIndex = 0;
            }
            finally { _syncingSelection = false; }

            if (result.Rows.Count > 0) SelectItemById(Convert.ToInt64(result.Rows[0]["id"]));
            UpdateSummary();
        }

        private void SelectItemFromSearch()
        {
            if (_syncingSelection || _itemResults.SelectedValue == null || _itemResults.SelectedValue is DataRowView) return;
            long id;
            if (long.TryParse(Convert.ToString(_itemResults.SelectedValue), out id)) SelectItemById(id);
        }

        private void SelectItemById(long itemId)
        {
            if (!_grid.Columns.Contains("id")) return;
            _syncingSelection = true;
            try
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (Convert.ToInt64(row.Cells["id"].Value) != itemId) continue;
                    row.Selected = true;
                    _grid.CurrentCell = row.Cells[_grid.Columns.Contains("Obat") ? "Obat" : _grid.Columns[0].Name];
                    break;
                }
            }
            finally { _syncingSelection = false; }
            LoadSelectedItem();
        }

        private void SyncSearchSelectionFromGrid(DataGridViewRow row)
        {
            if (_itemResults.DataSource == null || row == null) return;
            var id = Convert.ToInt64(row.Cells["id"].Value);
            _syncingSelection = true;
            try { _itemResults.SelectedValue = id; }
            catch { }
            finally { _syncingSelection = false; }
        }

        private void UpdateItemDetail(DataGridViewRow row)
        {
            var system = GetGridInt(row, "Stok Sistem");
            var physicalText = Convert.ToString(row.Cells["Stok Fisik"].Value);
            int physical;
            var hasPhysical = int.TryParse(physicalText, out physical);
            var variance = hasPhysical ? physical - system : 0;
            var status = !hasPhysical ? "Belum dihitung" : (variance == 0 ? "Sesuai" : (variance < 0 ? "Stok berkurang" : "Stok bertambah"));
            _itemDetail.Text =
                Convert.ToString(row.Cells["Obat"].Value) + "\n" +
                "Batch: " + Convert.ToString(row.Cells["Batch"].Value) + " | ED: " + Convert.ToString(row.Cells["Expired"].Value) + "\n" +
                "Stok Sistem : " + system + "\n" +
                "Stok Fisik  : " + (hasPhysical ? physical.ToString() : "belum diisi") + "\n" +
                "Selisih     : " + (hasPhysical ? variance.ToString("+0;-0;0") : "-") + " (" + status + ")";
            _itemDetail.ForeColor = variance < 0 ? Ui.Danger : (variance > 0 ? Ui.Success : Color.FromArgb(51, 65, 85));
        }

        private void UpdateSummary()
        {
            if (_itemsTable == null)
            {
                _summary.Text = "";
                return;
            }
            var total = _itemsTable.Rows.Count; var counted = 0; var equal = 0; var less = 0; var more = 0;
            foreach (DataRow row in _itemsTable.Rows)
            {
                if (row["Stok Fisik"] == DBNull.Value) continue;
                counted++;
                var diff = Convert.ToInt32(row["Stok Fisik"]) - Convert.ToInt32(row["Stok Sistem"]);
                if (diff == 0) equal++;
                else if (diff < 0) less++;
                else more++;
            }
            _summary.Text = "Review draft\n" +
                            "Total item       : " + total + "\n" +
                            "Sudah dihitung   : " + counted + "\n" +
                            "Sesuai           : " + equal + "\n" +
                            "Selisih kurang   : " + less + "\n" +
                            "Selisih lebih    : " + more;
        }

        private static int GetGridInt(DataGridViewRow row, string column)
        {
            var value = row.Cells[column].Value;
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private void FormatOpnameGrid()
        {
            if (_grid.Columns.Contains("opname_id")) _grid.Columns["opname_id"].Visible = false;
            if (_grid.Columns.Contains("medicine_id")) _grid.Columns["medicine_id"].Visible = false;
            if (_grid.Columns.Contains("batch_id")) _grid.Columns["batch_id"].Visible = false;
            if (_grid.Columns.Contains("Harga Beli")) _grid.Columns["Harga Beli"].DefaultCellStyle.Format = "N0";
            if (_grid.Columns.Contains("Nilai Selisih")) _grid.Columns["Nilai Selisih"].DefaultCellStyle.Format = "N0";
        }

        private void UpdateButtons()
        {
            var status = SelectedStatus;
            _status.Text = string.IsNullOrWhiteSpace(status) ? "Belum ada sesi" : "Status: " + status;
            _status.ForeColor = status == "SELESAI" ? Ui.Success : (status == "BATAL" ? Ui.Danger : Color.FromArgb(180, 83, 9));
            var draft = status == "DRAFT";
            _saveCount.Enabled = draft;
            _complete.Enabled = draft || status == "MENUNGGU_PEMERIKSAAN";
            _cancel.Enabled = _user.IsSuperAdmin && (draft || status == "MENUNGGU_PEMERIKSAAN");
            _physical.Enabled = draft; _reason.Enabled = draft;
        }

        private void StartNew()
        {
            try
            {
                using (var prompt = new TextPromptDialog("Stok Opname Baru", "Catatan sesi (opsional):"))
                {
                    if (prompt.ShowDialog(this) != DialogResult.OK) return;
                    var id = StockOpnameService.Start(_user, prompt.Value);
                    LoadOpnames(id);
                    Ui.MessageInfo("Sesi stok opname dibuat. Hitung setiap item, lalu selesaikan opname.", "Stok Opname");
                }
            }
            catch (Exception ex) { Ui.MessageError(ex, "Stok Opname"); }
        }

        private void SaveCount()
        {
            try
            {
                if (SelectedItemId == 0) { Ui.MessageInfo("Pilih item stok yang akan dihitung."); return; }
                StockOpnameService.SaveCount(_user, SelectedItemId, Convert.ToInt32(_physical.Value), _reason.Text);
                ReloadItems();
            }
            catch (Exception ex) { Ui.MessageError(ex, "Simpan Hitungan"); }
        }

        private void Complete()
        {
            try
            {
                if (SelectedOpnameId == 0) return;
                var summary = StockOpnameService.PostingSummary(SelectedOpnameId);
                if (MessageBox.Show(summary + "\n\nPosting semua selisih ke stok dan kartu stok? Aksi ini tidak dapat dibatalkan.", "Konfirmasi Posting", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                StockOpnameService.Complete(_user, SelectedOpnameId); LoadOpnames(SelectedOpnameId);
                Ui.MessageInfo("Stok opname selesai. Selisih sudah dicatat ke kartu stok.", "Stok Opname");
            }
            catch (Exception ex) { Ui.MessageError(ex, "Selesaikan Stok Opname"); }
        }

        private void Cancel()
        {
            try
            {
                if (SelectedOpnameId == 0) return;
                using (var prompt = new TextPromptDialog("Batalkan Stok Opname", "Alasan pembatalan wajib diisi:", false))
                {
                    if (prompt.ShowDialog(this) != DialogResult.OK) return;
                    StockOpnameService.Cancel(_user, SelectedOpnameId, prompt.Value); LoadOpnames(SelectedOpnameId);
                }
            }
            catch (Exception ex) { Ui.MessageError(ex, "Batalkan Stok Opname"); }
        }

        private void Export()
        {
            var table = _grid.DataSource as DataTable;
            if (table == null) { Ui.MessageInfo("Pilih sesi stok opname terlebih dahulu."); return; }
            using (var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "stok_opname_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try { ExcelBackupService.ExportTable(table, dialog.FileName, "Stok Opname"); Ui.MessageInfo("Export selesai."); }
                    catch (Exception ex) { Ui.MessageError(ex, "Export Stok Opname"); }
                }
        }
    }
}
