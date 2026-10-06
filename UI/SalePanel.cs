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
    internal class SalePanel : UserControl
    {
        private readonly AppUser _user;
        private readonly TextBox _searchBox = new TextBox();
        private readonly ListBox _medicineList = new ListBox();
        private readonly Label _selectedObatLabel = new Label();
        private readonly NumericUpDown _qty = new NumericUpDown();
        private readonly TextBox _note = new TextBox();
        private readonly DataGridView _cart = Ui.Grid();
        private readonly Label _grandTotalDisplay = new Label();
        private readonly List<SaleLine> _lines = new List<SaleLine>();
        private Button _save;
        private bool _saving;

        public SalePanel(AppUser user)
        {
            _user = user;
            Dock = DockStyle.Fill;
            BackColor = Ui.Background;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            Controls.Add(mainLayout);

            // Left panel (Cart + Grand Total display)
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 16, 0) };
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var totalPanel = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White, Padding = new Padding(16) };
            totalPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, totalPanel.Width - 1, totalPanel.Height - 1);
                }
            };
            var totalTitle = new Label { Text = "TOTAL BELANJA:", Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(16, 22), AutoSize = true };
            _grandTotalDisplay.Text = "Rp 0";
            _grandTotalDisplay.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            _grandTotalDisplay.ForeColor = Ui.Accent;
            _grandTotalDisplay.AutoSize = true;
            _grandTotalDisplay.Location = new Point(160, 12);
            totalPanel.Controls.Add(totalTitle);
            totalPanel.Controls.Add(_grandTotalDisplay);
            leftPanel.Controls.Add(totalPanel);

            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 16, 0, 0) };
            leftPanel.Controls.Add(gridContainer);
            gridContainer.BringToFront();
            gridContainer.Controls.Add(_cart);

            // Right panel (Search + Controls)
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18) };
            rightPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, rightPanel.Width - 1, rightPanel.Height - 1);
                }
            };
            mainLayout.Controls.Add(rightPanel, 1, 0);

            var y = 18;
            var searchLabel = Ui.Label("Cari Obat (F1)", 0, 9.5F, FontStyle.Bold);
            searchLabel.Location = new Point(18, y); rightPanel.Controls.Add(searchLabel);
            y += 24;

            _searchBox.Location = new Point(18, y); _searchBox.Width = 284;
            _searchBox.Font = new Font("Segoe UI", 10F);
            _searchBox.TextChanged += (s, e) => SearchObat();
            rightPanel.Controls.Add(_searchBox);
            y += 32;

            _medicineList.Location = new Point(18, y); _medicineList.Width = 284; _medicineList.Height = 110;
            _medicineList.Font = new Font("Segoe UI", 9.5F);
            _medicineList.SelectedIndexChanged += (s, e) => UpdateSelectedObatInfo();
            rightPanel.Controls.Add(_medicineList);
            y += 120;

            var infoGroup = new GroupBox { Text = "Detail Obat", Location = new Point(18, y), Width = 284, Height = 120, Font = new Font("Segoe UI Semibold", 8.5F) };
            _selectedObatLabel.Font = new Font("Segoe UI", 9F);
            _selectedObatLabel.Location = new Point(12, 18);
            _selectedObatLabel.AutoSize = true;
            infoGroup.Controls.Add(_selectedObatLabel);
            rightPanel.Controls.Add(infoGroup);
            y += 130;

            var qtyLabel = Ui.Label("Qty (F2)", 0, 9.5F, FontStyle.Bold);
            qtyLabel.Location = new Point(18, y); rightPanel.Controls.Add(qtyLabel);
            y += 24;

            _qty.Location = new Point(18, y); _qty.Width = 80;
            _qty.Font = new Font("Segoe UI", 10F);
            _qty.Minimum = 1; _qty.Maximum = 100000;
            rightPanel.Controls.Add(_qty);

            var addBtn = Ui.Button("Tambah ke Keranjang", 195, Ui.Accent);
            addBtn.Location = new Point(107, y - 2);
            addBtn.Click += (s, e) => AddItem();
            rightPanel.Controls.Add(addBtn);
            y += 38;

            var removeBtn = Ui.Button("Hapus Item Terpilih", 284, Ui.Danger);
            removeBtn.Location = new Point(18, y);
            removeBtn.Click += (s, e) => RemoveItem();
            rightPanel.Controls.Add(removeBtn);
            y += 42;

            var noteLabel = Ui.Label("Catatan Transaksi", 0, 9.5F, FontStyle.Bold);
            noteLabel.Location = new Point(18, y); rightPanel.Controls.Add(noteLabel);
            y += 24;

            _note.Location = new Point(18, y); _note.Width = 284;
            _note.Font = new Font("Segoe UI", 10F);
            rightPanel.Controls.Add(_note);
            y += 40;

            _save = Ui.Button("Bayar / Checkout (F5)", 284, Ui.Success);
            _save.Height = 42;
            _save.Font = new Font("Segoe UI Semibold", 10.5F);
            _save.Location = new Point(18, y);
            _save.Click += (s, e) => SaveSale();
            rightPanel.Controls.Add(_save);

            ReloadMedicines();
            RefreshCart();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                return true;
            }
            if (keyData == Keys.F2)
            {
                _qty.Focus();
                _qty.Select(0, _qty.Text.Length);
                return true;
            }
            if (keyData == Keys.F5)
            {
                SaveSale();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                if (_lines.Count > 0 && MessageBox.Show("Kosongkan keranjang belanja?", "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _lines.Clear();
                    _note.Clear();
                    RefreshCart();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ReloadMedicines()
        {
            SearchObat();
        }

        private void SearchObat()
        {
            var dt = MedicineService.List(_searchBox.Text);
            _medicineList.DataSource = dt;
            _medicineList.DisplayMember = "Nama Barang";
            _medicineList.ValueMember = "id";
            UpdateSelectedObatInfo();
        }

        private void UpdateSelectedObatInfo()
        {
            if (_medicineList.SelectedItem == null)
            {
                _selectedObatLabel.Text = "Pilih obat untuk melihat detail.";
                return;
            }
            var rowView = _medicineList.SelectedItem as DataRowView;
            if (rowView == null) return;
            var r = rowView.Row;
            var id = Convert.ToInt64(r["id"]);
            var name = Convert.ToString(r["Nama Barang"]);
            var stock = Convert.ToInt32(r["SISA"]);
            var sellableStock = MedicineService.SellableStock(id);
            var price = Convert.ToDouble(r["Harga Jual"]);
            var unit = Convert.ToString(r["SATUAN"]);
            var cat = Convert.ToString(r["Kategori"]);

            _selectedObatLabel.Text = $"Nama: {name}\nStok jual: {sellableStock} {unit}\nStok total: {stock} {unit}\nHarga: {Ui.Rp(price)}\nKategori: {cat}";
        }

        private void AddItem()
        {
            if (_medicineList.SelectedValue == null) return;
            var rowView = _medicineList.SelectedItem as DataRowView;
            if (rowView == null) return;
            var r = rowView.Row;
            var id = Convert.ToInt64(r["id"]);
            var quantity = (int)_qty.Value;
            var stock = Convert.ToInt32(r["SISA"]);
            var sellableStock = MedicineService.SellableStock(id);

            var existing = _lines.Find(x => x.MedicineId == id);
            var requested = quantity + (existing == null ? 0 : existing.Qty);
            if (requested > sellableStock)
            {
                MessageBox.Show("Stok layak jual tidak cukup. Tersedia " + sellableStock + ". Total stok " + stock + ". Cek batch expired.", "Stok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (existing == null)
            {
                _lines.Add(new SaleLine
                {
                    MedicineId = id,
                    Name = Convert.ToString(r["Nama Barang"]),
                    Qty = quantity,
                    UnitPrice = Convert.ToDouble(r["Harga Jual"])
                });
            }
            else
            {
                existing.Qty += quantity;
            }

            _qty.Value = 1;
            RefreshCart();
        }

        private void RemoveItem()
        {
            if (_cart.CurrentRow == null) return;
            var id = Convert.ToInt64(_cart.CurrentRow.Cells["MedicineId"].Value);
            _lines.RemoveAll(x => x.MedicineId == id);
            RefreshCart();
        }

        private void RefreshCart()
        {
            var dt = new DataTable();
            dt.Columns.Add("MedicineId", typeof(long));
            dt.Columns.Add("No", typeof(int));
            dt.Columns.Add("Nama Obat");
            dt.Columns.Add("Qty", typeof(int));
            dt.Columns.Add("Harga", typeof(double));
            dt.Columns.Add("Subtotal", typeof(double));

            double subtotal = 0;
            var no = 1;
            foreach (var l in _lines)
            {
                var sub = l.Qty * l.UnitPrice;
                subtotal += sub;
                dt.Rows.Add(l.MedicineId, no++, l.Name, l.Qty, l.UnitPrice, sub);
            }
            _cart.DataSource = dt;
            if (_cart.Columns.Contains("MedicineId")) _cart.Columns["MedicineId"].Visible = false;

            if (_cart.Columns.Contains("Harga")) _cart.Columns["Harga"].DefaultCellStyle.Format = "N0";
            if (_cart.Columns.Contains("Subtotal")) _cart.Columns["Subtotal"].DefaultCellStyle.Format = "N0";

            _grandTotalDisplay.Text = Ui.Rp(subtotal);
        }

        private void SaveSale()
        {
            if (_saving) return;
            try
            {
                if (_lines.Count == 0) throw new InvalidOperationException("Keranjang masih kosong.");
                double subtotal = 0;
                foreach (var l in _lines) subtotal += l.Qty * l.UnitPrice;

                double discount = 0;
                string paymentMethod = "TUNAI";
                double cashPaid = subtotal;

                using (var d = new CheckoutDialog(subtotal))
                {
                    if (d.ShowDialog() != DialogResult.OK) return;
                    discount = d.Discount;
                    paymentMethod = d.PaymentMethod;
                    cashPaid = d.CashPaid;
                }

                var grandTotal = subtotal - discount;
                if (paymentMethod == "TUNAI" && cashPaid < grandTotal)
                {
                    throw new InvalidOperationException("Uang bayar kurang dari total belanja.");
                }

                _saving = true;
                _save.Enabled = false;

                var result = TransactionService.SaveSale(_user, _lines, discount, paymentMethod, _note.Text);

                var change = cashPaid - grandTotal;
                var successMsg = $"Penjualan berhasil.\nKode: {result.ReportCode}\nTotal: {Ui.Rp(result.GrandTotal)}";
                if (paymentMethod == "TUNAI")
                {
                    successMsg += $"\nBayar: {Ui.Rp(cashPaid)}\nKembali: {Ui.Rp(change)}";
                }

                MessageBox.Show(successMsg, "Transaksi Berhasil", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (MessageBox.Show("Cetak struk sekarang?", "Struk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try { ReceiptService.PrintSale(result.SaleId); }
                    catch (Exception ex) { Ui.MessageError(ex, "Cetak Struk"); }
                }

                _lines.Clear();
                _note.Clear();
                RefreshCart();
                ReloadMedicines();
            }
            catch (Exception ex) { Ui.MessageError(ex, "Checkout Penjualan"); }
            finally { _saving = false; _save.Enabled = true; }
        }
    }

    internal class CheckoutDialog : Form
    {
        private readonly double _subtotal;
        private readonly NumericUpDown _discount = new NumericUpDown();
        private readonly ComboBox _payment = new ComboBox();
        private readonly NumericUpDown _cashPaid = new NumericUpDown();
        private readonly Label _grandTotalLabel = new Label();
        private readonly Label _changeLabel = new Label();

        public double Discount => (double)_discount.Value;
        public string PaymentMethod => _payment.SelectedItem.ToString();
        public double CashPaid => (double)_cashPaid.Value;

        public CheckoutDialog(double subtotal)
        {
            _subtotal = subtotal;
            Text = "Checkout Transaksi";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(540, 480);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Color.White;

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Add(table, "Subtotal", new Label { Text = Ui.Rp(subtotal), Font = new Font("Segoe UI", 12F, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105) });

            _discount.Maximum = (decimal)subtotal;
            _discount.DecimalPlaces = 0;
            _discount.ThousandsSeparator = true;
            _discount.Width = 220;
            _discount.Font = new Font("Segoe UI", 11F);
            _discount.ValueChanged += (s, e) => UpdateTotals();
            _discount.KeyUp += (s, e) => UpdateTotals();
            Add(table, "Diskon (Rp)", _discount);

            _grandTotalLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _grandTotalLabel.ForeColor = Ui.Accent;
            _grandTotalLabel.AutoSize = true;
            Add(table, "Total Akhir", _grandTotalLabel);

            _payment.DropDownStyle = ComboBoxStyle.DropDownList;
            _payment.Items.AddRange(new object[] { "TUNAI", "QRIS" });
            _payment.SelectedIndex = 0;
            _payment.Font = new Font("Segoe UI", 11F);
            _payment.Width = 220;
            _payment.SelectedIndexChanged += (s, e) =>
            {
                var isCash = _payment.SelectedItem.ToString() == "TUNAI";
                _cashPaid.Enabled = isCash;
                if (!isCash)
                {
                    _cashPaid.Value = (decimal)(subtotal - (double)_discount.Value);
                }
                UpdateTotals();
            };
            Add(table, "Metode Pembayaran", _payment);

            _cashPaid.Maximum = 1000000000;
            _cashPaid.DecimalPlaces = 0;
            _cashPaid.ThousandsSeparator = true;
            _cashPaid.Width = 220;
            _cashPaid.Font = new Font("Segoe UI", 11F);
            _cashPaid.ValueChanged += (s, e) => UpdateTotals();
            _cashPaid.KeyUp += (s, e) => UpdateTotals();
            Add(table, "Uang Bayar (Rp)", _cashPaid);

            _changeLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _changeLabel.ForeColor = Ui.Success;
            _changeLabel.AutoSize = true;
            Add(table, "Kembalian", _changeLabel);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0) };
            var ok = Ui.Button("Simpan Transaksi", 160, Ui.Success);
            ok.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Batal", 100);
            cancel.DialogResult = DialogResult.Cancel;
            btnPanel.Controls.Add(ok);
            btnPanel.Controls.Add(cancel);

            table.Controls.Add(new Label(), 0, 6);
            table.Controls.Add(btnPanel, 1, 6);
            Controls.Add(table);

            AcceptButton = ok;
            CancelButton = cancel;

            UpdateTotals();

            _cashPaid.Value = (decimal)subtotal;
        }

        private void UpdateTotals()
        {
            var disc = (double)_discount.Value;
            var total = Math.Max(0, _subtotal - disc);
            _grandTotalLabel.Text = Ui.Rp(total);

            if (_payment.SelectedItem.ToString() == "TUNAI")
            {
                var paid = (double)_cashPaid.Value;
                var change = paid - total;
                if (change < 0)
                {
                    _changeLabel.Text = "Kurang " + Ui.Rp(Math.Abs(change));
                    _changeLabel.ForeColor = Ui.Danger;
                }
                else
                {
                    _changeLabel.Text = Ui.Rp(change);
                    _changeLabel.ForeColor = Ui.Success;
                }
            }
            else
            {
                _changeLabel.Text = "Rp 0 (Non-Tunai)";
                _changeLabel.ForeColor = Color.DimGray;
            }
        }

        private static void Add(TableLayoutPanel t, string label, Control c)
        {
            c.Dock = DockStyle.Fill;
            t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI Semibold", 9.5F) });
            t.Controls.Add(c);
        }
    }
}
