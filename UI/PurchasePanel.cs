using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    internal class PurchasePanel : UserControl
    {
        private readonly AppUser _user;
        private readonly long? _purchaseId;
        private readonly List<PurchaseLine> _lines = new List<PurchaseLine>();
        private readonly ComboBox _supplier = new ComboBox();
        private readonly TextBox _invoice = new TextBox();
        private readonly TextBox _note = new TextBox();
        private readonly ComboBox _medicine = new ComboBox();
        private readonly TextBox _medicineSearch = new TextBox();
        private readonly ListBox _medicineResults = new ListBox();
        private readonly Label _selectedMedicineInfo = new Label();
        private readonly TextBox _unit = new TextBox();
        private readonly NumericUpDown _qty = new NumericUpDown();
        private readonly NumericUpDown _invoicePrice = new NumericUpDown();
        private readonly NumericUpDown _discount = new NumericUpDown();
        private readonly CheckBox _ppn = new CheckBox();
        private readonly Label _finalPrice = new Label();
        private readonly NumericUpDown _salePrice = new NumericUpDown();
        private readonly CheckBox _autoSale = new CheckBox();
        private readonly TextBox _batch = new TextBox();
        private readonly DateTimePicker _expiry = new DateTimePicker();
        private readonly CheckBox _hasExpiry = new CheckBox();
        private readonly DataGridView _grid = Ui.Grid();
        private readonly Label _total = new Label();
        private readonly Panel _chooser = new Panel();
        private readonly Panel _entry = new Panel();
        private readonly FlowLayoutPanel _header = new FlowLayoutPanel();
        private readonly FlowLayoutPanel _itemBar = new FlowLayoutPanel();
        private readonly Label _modeTitle = new Label();
        private readonly Label _sourceLabel = new Label();
        private readonly Label _invoiceLabel = new Label();
        private readonly Button _saveButton;
        private DataTable _medicineTable;
        private bool _saving;
        private bool _loading;
        private bool _filteringMedicine;
        private bool _singleMode;

        public PurchasePanel(AppUser user, long? purchaseId = null)
        {
            _user = user; _purchaseId = purchaseId; Dock = DockStyle.Fill;
            BackColor = Ui.Background;

            _chooser.Dock = DockStyle.Top; _chooser.Height = 118; _chooser.BackColor = Color.White;
            var chooseHint = new Label { Text = "Pilih cara memasukkan stok baru", AutoSize = true, Location = new Point(18, 16), Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(71, 85, 105) };
            var single = Ui.Button("Tunggal", 170, Ui.Accent); var batch = Ui.Button("Batch (Faktur)", 190, Ui.Success);
            single.Location = new Point(18, 54); batch.Location = new Point(204, 54);
            _chooser.Controls.Add(chooseHint); _chooser.Controls.Add(single); _chooser.Controls.Add(batch);

            _entry.Dock = DockStyle.Fill; _entry.BackColor = Ui.Background; _entry.Visible = false;
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            _entry.Controls.Add(mainLayout);

            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 16, 0), BackColor = Ui.Background };
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var totalPanel = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.White, Padding = new Padding(16) };
            totalPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, totalPanel.Width - 1, totalPanel.Height - 1);
            };
            var totalTitle = new Label { Text = "TOTAL FAKTUR:", Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(16, 16), AutoSize = true };
            _total.Text = "Rp 0";
            _total.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            _total.ForeColor = Ui.Accent;
            _total.AutoSize = true;
            _total.Location = new Point(160, 8);
            _finalPrice.AutoSize = true;
            _finalPrice.Location = new Point(18, 52);
            _finalPrice.Font = new Font("Segoe UI Semibold", 8.8F);
            _finalPrice.ForeColor = Color.FromArgb(71, 85, 105);
            totalPanel.Controls.Add(totalTitle); totalPanel.Controls.Add(_total); totalPanel.Controls.Add(_finalPrice);
            leftPanel.Controls.Add(totalPanel);

            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 16, 0, 0) };
            gridContainer.Controls.Add(_grid); _grid.BringToFront();
            leftPanel.Controls.Add(gridContainer); gridContainer.BringToFront();

            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };
            rightPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, rightPanel.Width - 1, rightPanel.Height - 1);
            };
            mainLayout.Controls.Add(rightPanel, 1, 0);

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 156, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Padding = new Padding(18, 8, 18, 12) };
            rightPanel.Controls.Add(actionPanel);

            var inputPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, Padding = new Padding(18) };
            rightPanel.Controls.Add(inputPanel);
            actionPanel.BringToFront();

            _sourceLabel.Text = "Supplier / PBF"; _sourceLabel.Location = new Point(18, 12); _sourceLabel.AutoSize = true; _sourceLabel.Font = new Font("Segoe UI Semibold", 9F);
            _supplier.Location = new Point(18, 34); _supplier.Width = 306; _supplier.DropDownStyle = ComboBoxStyle.DropDown; _supplier.AutoCompleteMode = AutoCompleteMode.SuggestAppend; _supplier.AutoCompleteSource = AutoCompleteSource.ListItems;
            var suppliers = MasterService.Suppliers(false); _supplier.DataSource = suppliers; _supplier.DisplayMember = "Nama"; _supplier.ValueMember = "id";
            _invoiceLabel.Text = "No. Faktur"; _invoiceLabel.Location = new Point(18, 68); _invoiceLabel.AutoSize = true; _invoiceLabel.Font = new Font("Segoe UI Semibold", 9F);
            _invoice.Location = new Point(18, 90); _invoice.Width = 306;
            inputPanel.Controls.Add(_sourceLabel); inputPanel.Controls.Add(_supplier); inputPanel.Controls.Add(_invoiceLabel); inputPanel.Controls.Add(_invoice);

            var y = 124;
            var searchLabel = Ui.Label("Cari Obat (F1)", 0, 9.5F, FontStyle.Bold); searchLabel.Location = new Point(18, y); inputPanel.Controls.Add(searchLabel); y += 22;
            _medicineSearch.Location = new Point(18, y); _medicineSearch.Width = 306; _medicineSearch.Font = new Font("Segoe UI", 10F); inputPanel.Controls.Add(_medicineSearch); y += 34;
            _medicineResults.Location = new Point(18, y); _medicineResults.Width = 306; _medicineResults.Height = 92; _medicineResults.Font = new Font("Segoe UI", 9.5F); inputPanel.Controls.Add(_medicineResults); y += 102;

            var infoGroup = new GroupBox { Text = "Detail Obat", Location = new Point(18, y), Width = 306, Height = 92, Font = new Font("Segoe UI Semibold", 8.5F) };
            _selectedMedicineInfo.Location = new Point(12, 20); _selectedMedicineInfo.Width = 278; _selectedMedicineInfo.Height = 62; _selectedMedicineInfo.Font = new Font("Segoe UI", 8.8F); _selectedMedicineInfo.ForeColor = Color.FromArgb(51, 65, 85);
            infoGroup.Controls.Add(_selectedMedicineInfo); inputPanel.Controls.Add(infoGroup); y += 102;

            _unit.Location = new Point(18, y + 24); _unit.Width = 74;
            _qty.Location = new Point(102, y + 24); _qty.Width = 72; _qty.Minimum = 1; _qty.Maximum = 100000; _qty.Value = 1;
            _invoicePrice.Location = new Point(184, y + 24); PrepareMoney(_invoicePrice, 140);
            var unitLabel = Ui.Label("Satuan", 0, 9F, FontStyle.Bold); unitLabel.Location = new Point(18, y);
            var qtyLabel = Ui.Label("Qty", 0, 9F, FontStyle.Bold); qtyLabel.Location = new Point(102, y);
            var hbLabel = Ui.Label("Harga Faktur", 0, 9F, FontStyle.Bold); hbLabel.Location = new Point(184, y);
            inputPanel.Controls.Add(unitLabel); inputPanel.Controls.Add(qtyLabel); inputPanel.Controls.Add(hbLabel); inputPanel.Controls.Add(_unit); inputPanel.Controls.Add(_qty); inputPanel.Controls.Add(_invoicePrice); y += 56;

            var discLabel = Ui.Label("Diskon / satuan", 0, 9F, FontStyle.Bold); discLabel.Location = new Point(18, y);
            _discount.Location = new Point(18, y + 24); PrepareMoney(_discount, 140);
            _autoSale.Text = "HJ auto +20%"; _autoSale.AutoSize = true; _autoSale.Checked = true; _autoSale.Location = new Point(172, y + 24);
            inputPanel.Controls.Add(discLabel); inputPanel.Controls.Add(_discount); inputPanel.Controls.Add(_autoSale); y += 56;

            var saleLabel = Ui.Label("Harga Jual", 0, 9F, FontStyle.Bold); saleLabel.Location = new Point(18, y);
            _salePrice.Location = new Point(18, y + 24); PrepareMoney(_salePrice, 140); _salePrice.Enabled = false;
            _ppn.Text = "PPN 11% di akhir faktur"; _ppn.AutoSize = true; _ppn.Location = new Point(172, y + 26);
            inputPanel.Controls.Add(saleLabel); inputPanel.Controls.Add(_salePrice); inputPanel.Controls.Add(_ppn); y += 56;

            var batchLabel = Ui.Label("No. Batch", 0, 9F, FontStyle.Bold); batchLabel.Location = new Point(18, y);
            _batch.Location = new Point(18, y + 24); _batch.Width = 140;
            _hasExpiry.Text = "Ada expired"; _hasExpiry.AutoSize = true; _hasExpiry.Location = new Point(172, y + 26);
            _expiry.Location = new Point(172, y + 50); _expiry.Width = 120; _expiry.Format = DateTimePickerFormat.Short; _expiry.MinDate = DateTime.Today; _expiry.Value = DateTime.Today; _expiry.Enabled = false;
            inputPanel.Controls.Add(batchLabel); inputPanel.Controls.Add(_batch); inputPanel.Controls.Add(_hasExpiry); inputPanel.Controls.Add(_expiry); y += 86;

            var noteLabel = Ui.Label("Catatan Faktur", 0, 9.5F, FontStyle.Bold); noteLabel.Location = new Point(18, y); inputPanel.Controls.Add(noteLabel); y += 24;
            _note.Location = new Point(18, y); _note.Width = 306; inputPanel.Controls.Add(_note); y += 48;

            var add = Ui.Button("Tambah ke Faktur", 306, Ui.Accent); add.Height = 36; add.Margin = new Padding(0, 0, 0, 6); actionPanel.Controls.Add(add);
            var remove = Ui.Button("Hapus Item Terpilih", 306, Ui.Danger); remove.Height = 34; remove.Margin = new Padding(0, 0, 0, 8); actionPanel.Controls.Add(remove);
            _saveButton = Ui.Button(_purchaseId.HasValue ? "Simpan Perubahan" : "Simpan Stok Masuk", 306, Ui.Success); _saveButton.Height = 40; _saveButton.Margin = new Padding(0, 0, 0, 4); actionPanel.Controls.Add(_saveButton);
            var back = Ui.Button("Pilih Mode", 120); back.Height = 28; back.Margin = new Padding(0); actionPanel.Controls.Add(back);

            Controls.Add(_entry); Controls.Add(_chooser);
            _medicine.Width = 1; _medicine.Visible = false; Controls.Add(_medicine);
            LoadMedicineList();

            single.Click += delegate { BeginMode(true); };
            batch.Click += delegate { BeginMode(false); };
            _hasExpiry.CheckedChanged += delegate { _expiry.Enabled = _hasExpiry.Checked; };
            _medicineSearch.TextChanged += delegate { FilterMedicineSearchResults(); };
            _medicineResults.SelectedIndexChanged += delegate { SelectMedicineFromSearch(); };
            _medicineSearch.KeyDown += MedicineSearchKeyDown;
            _medicineResults.KeyDown += MedicineResultsKeyDown;
            _invoicePrice.ValueChanged += delegate { UpdateCalculatedPrices(); };
            _discount.ValueChanged += delegate { UpdateCalculatedPrices(); };
            _ppn.CheckedChanged += delegate { RefreshGrid(); };
            _autoSale.CheckedChanged += delegate { _salePrice.Enabled = !_autoSale.Checked; UpdateCalculatedPrices(); };
            add.Click += delegate { AddItem(); };
            remove.Click += delegate { RemoveItem(); };
            back.Click += delegate { ShowChooser(); };
            _saveButton.Click += delegate { SaveEntry(); };

            UpdateCalculatedPrices();
            if (_purchaseId.HasValue) { BeginMode(false); LoadPurchase(_purchaseId.Value); RefreshGrid(); }
            RefreshGrid();
        }

        private void BeginMode(bool singleMode)
        {
            _singleMode = singleMode;
            _chooser.Visible = false;
            _entry.Visible = true;
            _sourceLabel.Text = singleMode ? "Sumber / supplier (opsional)" : "Nama faktur / supplier";
            _invoiceLabel.Text = singleMode ? "No. referensi (opsional)" : "No. faktur";
            _sourceLabel.Visible = !singleMode;
            _supplier.Visible = !singleMode;
            _invoiceLabel.Visible = !singleMode;
            _invoice.Visible = !singleMode;
            _ppn.Visible = !singleMode;
            if (singleMode) _ppn.Checked = false;
            if (singleMode)
            {
                _supplier.SelectedIndex = -1;
                _supplier.Text = "";
                _invoice.Clear();
                _note.Clear();
            }
            _saveButton.Text = _purchaseId.HasValue ? "Simpan Perubahan" : (singleMode ? "Simpan Obat Masuk" : "Simpan Stok Masuk");
            _medicineSearch.Focus();
        }

        private void ShowChooser()
        {
            _lines.Clear();
            _entry.Visible = false;
            _chooser.Visible = true;
            _singleMode = false;
            _supplier.SelectedIndex = -1;
            _supplier.Text = "";
            _invoice.Clear();
            _note.Clear();
            ResetItemFields();
            RefreshGrid();
        }

        private void SaveEntry()
        {
            if (_singleMode)
            {
                try
                {
                    _lines.Clear();
                    _lines.Add(ReadCurrentLine());
                    SavePurchase(_saveButton);
                }
                catch (Exception ex) { Ui.MessageError(ex, "Validasi Obat Masuk"); }
                return;
            }

            SavePurchase(_saveButton);
        }

        private static void PrepareMoney(NumericUpDown n, int width)
        {
            n.Maximum = 1000000000; n.DecimalPlaces = 2; n.ThousandsSeparator = true; n.Width = width;
        }

        private void LoadMedicineList()
        {
            var previousLoading = _loading;
            _loading = true;
            _medicineTable = MedicineService.List();
            _medicine.DataSource = _medicineTable;
            _medicine.DisplayMember = "Nama Barang";
            _medicine.ValueMember = "id";
            _medicine.SelectedIndex = -1;
            _medicine.Text = "";
            BindMedicineResults(_medicineTable);
            _loading = previousLoading;
        }

        private void FilterMedicineListFromText()
        {
            if (_loading || _filteringMedicine) return;
            var text = _medicine.Text;
            var selectionStart = _medicine.SelectionStart;
            _filteringMedicine = true;
            _loading = true;
            try
            {
                _medicineTable = MedicineService.List(text);
                _medicine.DataSource = _medicineTable;
                _medicine.DisplayMember = "Nama Barang";
                _medicine.ValueMember = "id";
                _medicine.SelectedIndex = -1;
                _medicine.Text = text;
                _medicine.SelectionStart = Math.Min(selectionStart, _medicine.Text.Length);
                _medicine.SelectionLength = 0;
                if (!_medicine.DroppedDown) _medicine.DroppedDown = true;
                Cursor.Current = Cursors.Default;
            }
            finally
            {
                _loading = false;
                _filteringMedicine = false;
            }
        }

        private void FilterMedicineSearchResults()
        {
            if (_loading) return;
            _medicineTable = MedicineService.List(_medicineSearch.Text);
            BindMedicineResults(_medicineTable);
        }

        private void BindMedicineResults(DataTable source)
        {
            _medicineResults.DataSource = source;
            _medicineResults.DisplayMember = "Nama Barang";
            _medicineResults.ValueMember = "id";
            if (_medicineResults.Items.Count > 0) _medicineResults.SelectedIndex = 0;
            else
            {
                _medicine.SelectedIndex = -1;
                _medicine.Text = _medicineSearch.Text.Trim();
                _selectedMedicineInfo.Text = "Obat baru akan dibuat saat stok masuk disimpan.";
            }
        }

        private void SelectMedicineFromSearch()
        {
            if (_loading) return;
            var row = _medicineResults.SelectedItem as DataRowView;
            if (row == null)
            {
                _selectedMedicineInfo.Text = "Ketik nama obat untuk mencari atau membuat obat baru.";
                return;
            }

            _loading = true;
            try
            {
                _medicine.SelectedValue = Convert.ToInt64(row.Row["id"]);
                _medicine.Text = Convert.ToString(row.Row["Nama Barang"]);
                _unit.Text = Convert.ToString(row.Row["SATUAN"]);
                SetMoney(_invoicePrice, row.Row["Harga Beli"]);
                if (!_autoSale.Checked) SetMoney(_salePrice, row.Row["Harga Jual"]);
                _selectedMedicineInfo.Text = Convert.ToString(row.Row["Nama Barang"]) + "\nStok saat ini: " + Convert.ToString(row.Row["SISA"]) + " " + Convert.ToString(row.Row["SATUAN"]);
            }
            finally
            {
                _loading = false;
            }
            UpdateCalculatedPrices();
        }

        private void MedicineSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && _medicineResults.Items.Count > 0)
            {
                _medicineResults.Focus();
                _medicineResults.SelectedIndex = Math.Min(_medicineResults.Items.Count - 1, _medicineResults.SelectedIndex + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                SelectMedicineFromSearch();
                _qty.Focus();
                _qty.Select(0, _qty.Text.Length);
                e.Handled = true;
            }
        }

        private void MedicineResultsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SelectMedicineFromSearch();
                _qty.Focus();
                _qty.Select(0, _qty.Text.Length);
                e.Handled = true;
            }
        }

        private void LoadSelectedMedicineDefaults()
        {
            if (_loading) return;
            var row = _medicine.SelectedItem as DataRowView; if (row == null) return;
            _unit.Text = Convert.ToString(row.Row["SATUAN"]);
            SetMoney(_invoicePrice, row.Row["Harga Beli"]);
            if (!_autoSale.Checked) SetMoney(_salePrice, row.Row["Harga Jual"]);
            UpdateCalculatedPrices();
        }

        private void UpdateCalculatedPrices()
        {
            var final = MedicineService.CalculateFinalPurchasePrice((double)_invoicePrice.Value, (double)_discount.Value, false);
            if (_autoSale.Checked) SetMoney(_salePrice, MedicineService.SuggestSalePrice(final));
            RefreshGrid();
        }

        private static void SetMoney(NumericUpDown n, object value)
        {
            var d = value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
            if (d < n.Minimum) d = n.Minimum;
            if (d > n.Maximum) d = n.Maximum;
            n.Value = d;
        }

        private long FindMedicineId(string name)
        {
            var key = NormalizeKey(name);
            if (key.Length == 0 || _medicineTable == null) return 0;
            foreach (DataRow row in _medicineTable.Rows)
            {
                if (NormalizeKey(Convert.ToString(row["Nama Barang"])) == key || NormalizeKey(Convert.ToString(row["Kode"])) == key)
                    return Convert.ToInt64(row["id"]);
            }
            return 0;
        }

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

        private PurchaseLine ReadCurrentLine()
        {
            var name = _medicine.Text.Trim();
            if (name.Length == 0) name = _medicineSearch.Text.Trim();
            if (name.Length == 0) throw new InvalidOperationException("Nama obat wajib diisi.");
            var final = MedicineService.CalculateFinalPurchasePrice((double)_invoicePrice.Value, (double)_discount.Value, false);
            DateTime? expiryDate = null;
            if (_hasExpiry.Checked)
            {
                if (_expiry.Value.Date < DateTime.Today) throw new InvalidOperationException("Tanggal expired tidak boleh sebelum hari ini.");
                expiryDate = _expiry.Value.Date;
            }
            return new PurchaseLine
            {
                MedicineId = FindMedicineId(name),
                Name = name,
                Unit = _unit.Text.Trim(),
                Qty = (int)_qty.Value,
                InvoicePrice = (double)_invoicePrice.Value,
                DiscountAmount = (double)_discount.Value,
                PpnPercent = 0d,
                PurchasePrice = final,
                SalePrice = (double)_salePrice.Value,
                BatchNo = _batch.Text.Trim(),
                ExpiryDate = expiryDate
            };
        }

        private void AddItem()
        {
            try
            {
                var line = ReadCurrentLine();
                _lines.Add(line);
            }
            catch (Exception ex) { Ui.MessageError(ex, "Validasi Baris"); return; }
            ResetItemFields();
            RefreshGrid();
        }

        private void ResetItemFields()
        {
            _loading = true;
            _medicine.SelectedIndex = -1; _medicine.Text = ""; _medicineSearch.Clear(); _unit.Clear(); _qty.Value = 1; _invoicePrice.Value = 0; _discount.Value = 0;
            _batch.Clear(); _hasExpiry.Checked = false; _expiry.Value = DateTime.Today; _autoSale.Checked = true; _salePrice.Enabled = false;
            _loading = false;
            FilterMedicineSearchResults();
            UpdateCalculatedPrices();
        }

        private void RemoveItem()
        {
            if (_grid.CurrentRow == null) return; var index = _grid.CurrentRow.Index; if (index >= 0 && index < _lines.Count) { _lines.RemoveAt(index); RefreshGrid(); }
        }

        private void RefreshGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("Obat"); dt.Columns.Add("Satuan"); dt.Columns.Add("Qty", typeof(int));
            dt.Columns.Add("Harga Faktur", typeof(double)); dt.Columns.Add("Diskon", typeof(double));
            dt.Columns.Add("Harga Beli", typeof(double)); dt.Columns.Add("Harga Jual", typeof(double));
            dt.Columns.Add("Batch"); dt.Columns.Add("Expired"); dt.Columns.Add("Subtotal", typeof(double));
            double subtotal = 0;
            foreach (var l in _lines)
            {
                var sub = l.Qty * l.PurchasePrice; subtotal += sub;
                dt.Rows.Add(l.Name, l.Unit, l.Qty, l.InvoicePrice, l.DiscountAmount, l.PurchasePrice, l.SalePrice,
                    string.IsNullOrWhiteSpace(l.BatchNo) ? "(otomatis)" : l.BatchNo, l.ExpiryDate.HasValue ? l.ExpiryDate.Value.ToString("yyyy-MM-dd") : "-", sub);
            }
            var ppnAmount = (!_singleMode && _ppn.Checked) ? subtotal * 0.11d : 0d;
            _grid.DataSource = dt;
            _total.Text = Ui.Rp(subtotal + ppnAmount);
            _finalPrice.Text = "Subtotal: " + Ui.Rp(subtotal) + "   |   PPN akhir: " + Ui.Rp(ppnAmount);
            if (_grid.Columns.Contains("Harga Faktur")) _grid.Columns["Harga Faktur"].DefaultCellStyle.Format = "N0";
            if (_grid.Columns.Contains("Diskon")) _grid.Columns["Diskon"].DefaultCellStyle.Format = "N0";
            if (_grid.Columns.Contains("Harga Beli")) _grid.Columns["Harga Beli"].DefaultCellStyle.Format = "N0";
            if (_grid.Columns.Contains("Harga Jual")) _grid.Columns["Harga Jual"].DefaultCellStyle.Format = "N0";
            if (_grid.Columns.Contains("Subtotal")) _grid.Columns["Subtotal"].DefaultCellStyle.Format = "N0";
        }

        private void LoadPurchase(long purchaseId)
        {
            if (!_user.IsSuperAdmin) throw new UnauthorizedAccessException("Edit stok masuk hanya untuk Super Admin.");
            _loading = true;
            var header = TransactionService.PurchaseHeader(purchaseId);
            if (header.Rows.Count == 0) throw new InvalidOperationException("Faktur tidak ditemukan.");
            var h = header.Rows[0];
            _supplier.Text = Convert.ToString(h["supplier"]);
            _invoice.Text = Convert.ToString(h["supplier_invoice_number"]);
            _note.Text = Convert.ToString(h["note"]);
            var items = TransactionService.PurchaseItems(purchaseId);
            _lines.Clear();
            foreach (DataRow r in items.Rows)
            {
                DateTime exp;
                var expText = Convert.ToString(r["Expired"]);
                _lines.Add(new PurchaseLine
                {
                    MedicineId = Convert.ToInt64(r["medicine_id"]),
                    Name = Convert.ToString(r["Obat"]),
                    Unit = Convert.ToString(r["Satuan"]),
                    Qty = Convert.ToInt32(r["Qty"]),
                    InvoicePrice = Convert.ToDouble(r["Harga Faktur"]),
                    DiscountAmount = Convert.ToDouble(r["Diskon"]),
                    PpnPercent = Convert.ToDouble(r["PPN %"]),
                    PpnAmount = Convert.ToDouble(r["PPN"]),
                    PurchasePrice = Convert.ToDouble(r["Harga Beli Final"]),
                    SalePrice = Convert.ToDouble(r["Harga Jual"]),
                    BatchNo = Convert.ToString(r["Batch"]),
                    ExpiryDate = DateTime.TryParse(expText, out exp) ? (DateTime?)exp.Date : null
                });
            }
            _loading = false;
        }

        private void SavePurchase(Button button)
        {
            if (_saving) return;
            try
            {
                if (_lines.Count == 0) throw new InvalidOperationException("Daftar stok masuk masih kosong.");
                if (_purchaseId.HasValue && !_user.IsSuperAdmin) throw new UnauthorizedAccessException("Edit stok masuk hanya untuk Super Admin.");
                var supplierName = _singleMode ? "" : _supplier.Text.Trim();
                var invoiceNumber = _singleMode ? "" : _invoice.Text.Trim();
                if (!_singleMode && string.IsNullOrWhiteSpace(supplierName)) throw new InvalidOperationException("Nama faktur / supplier wajib diisi untuk mode Batch (Faktur).");
                if (!_singleMode && string.IsNullOrWhiteSpace(invoiceNumber)) throw new InvalidOperationException("No. faktur wajib diisi untuk mode Batch (Faktur).");
                var action = _purchaseId.HasValue ? "Simpan perubahan stok masuk dari " : (_singleMode ? "Simpan obat masuk " : "Simpan stok masuk dari ");
                if (MessageBox.Show(BuildConfirmText(action, supplierName), "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _saving = true; button.Enabled = false;
                var no = _purchaseId.HasValue
                    ? TransactionService.UpdatePurchase(_user, _purchaseId.Value, _lines, supplierName, invoiceNumber, _note.Text)
                    : TransactionService.SavePurchase(_user, _lines, supplierName, invoiceNumber, _note.Text, _singleMode ? "TUNGGAL" : "FAKTUR", !_singleMode && _ppn.Checked, 11d);
                MessageBox.Show("Stok masuk berhasil disimpan.\nNomor: " + no, "Berhasil", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (_purchaseId.HasValue)
                {
                    var f = FindForm() as PurchaseEditorForm;
                    if (f != null) { f.DialogResult = DialogResult.OK; f.Close(); return; }
                }
                _lines.Clear(); _invoice.Clear(); _note.Clear(); ResetItemFields(); RefreshGrid(); LoadMedicineList();
                if (!_purchaseId.HasValue) ShowChooser();
            }
            catch (Exception ex) { Ui.MessageError(ex, "Stok Masuk"); }
            finally { _saving = false; button.Enabled = true; }
        }

        private string BuildConfirmText(string action, string supplierName)
        {
            double subtotal = 0;
            foreach (var l in _lines) subtotal += l.Qty * l.PurchasePrice;
            var ppn = (!_singleMode && _ppn.Checked) ? subtotal * 0.11d : 0d;
            var source = string.IsNullOrWhiteSpace(supplierName) ? "obat tunggal" : supplierName;
            var text = action + source + "?\n\n"
                + "Jumlah item: " + _lines.Count + "\n"
                + "Subtotal: " + Ui.Rp(subtotal) + "\n"
                + "PPN akhir: " + Ui.Rp(ppn) + "\n"
                + "Total stok masuk: " + Ui.Rp(subtotal + ppn) + "\n\n"
                + "Setelah disimpan, stok, batch, expired, harga beli, harga jual, dan PPN faktur akan disinkronkan.\n"
                + "Obat yang belum ada otomatis masuk ke master obat.";
            return text;
        }
    }

    internal class PurchaseEditorForm : Form
    {
        public PurchaseEditorForm(AppUser user, long purchaseId)
        {
            Text = "Edit Stok Masuk";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1180, 700);
            BackColor = Color.White;
            Controls.Add(new PurchasePanel(user, purchaseId));
        }
    }
}
