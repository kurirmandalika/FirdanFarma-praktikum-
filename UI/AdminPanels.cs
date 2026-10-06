using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Core;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    internal class UserManagementPanel : UserControl
    {
        private readonly AppUser _actor;
        private readonly DataGridView _grid = Ui.Grid();
        public UserManagementPanel(AppUser actor)
        {
            _actor = actor; Dock = DockStyle.Fill; BackColor = Ui.Background;
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White, Padding = new Padding(6) };
            var add = Ui.Button("Tambah Pengguna", 0, Ui.Accent);
            var edit = Ui.Button("Ubah Pengguna");
            var refresh = Ui.Button("Refresh");
            top.Controls.Add(add); top.Controls.Add(edit); top.Controls.Add(refresh);

            var gridWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
            gridWrapper.Controls.Add(_grid); _grid.BringToFront();
            Controls.Add(gridWrapper);
            Controls.Add(top);

            add.Click += delegate { using (var f = new UserDialog(_actor, null)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
            edit.Click += delegate { if (_grid.CurrentRow == null) return; using (var f = new UserDialog(_actor, _grid.CurrentRow)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
            refresh.Click += delegate { Reload(); }; Reload();
        }
        private void Reload() { _grid.DataSource = UserService.List(); }
    }

    internal class UserDialog : Form
    {
        private readonly AppUser _actor;
        private readonly long _id;
        private readonly TextBox _username = new TextBox();
        private readonly TextBox _display = new TextBox();
        private readonly TextBox _password = new TextBox();
        private readonly ComboBox _role = new ComboBox();
        private readonly CheckBox _active = new CheckBox();
        private readonly CheckBox _purchase = new CheckBox();
        private readonly CheckBox _adjust = new CheckBox();

        public UserDialog(AppUser actor, DataGridViewRow row)
        {
            _actor = actor; _id = row == null ? 0 : Convert.ToInt64(row.Cells["id"].Value);
            Text = _id == 0 ? "Tambah Pengguna" : "Ubah Pengguna"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 420);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Add(table, "Username", _username); Add(table, "Nama Tampilan", _display);
            _password.UseSystemPasswordChar = true; _password.Font = new Font("Segoe UI", 9.5F);
            Add(table, _id == 0 ? "Password" : "Password baru", _password);
            _role.DropDownStyle = ComboBoxStyle.DropDownList; _role.Items.AddRange(new object[] { "KARYAWAN", "SUPER_ADMIN" }); _role.SelectedIndex = 0;
            _role.Font = new Font("Segoe UI", 9.5F);
            Add(table, "Role", _role);
            _active.Text = "Akun aktif"; _active.Checked = true; _active.Font = new Font("Segoe UI", 9.5F); Add(table, "Status", _active);
            _purchase.Text = "Boleh input stok masuk"; _purchase.Checked = true; _purchase.Font = new Font("Segoe UI", 9.5F); Add(table, "Izin Stok Masuk", _purchase);
            _adjust.Text = "Boleh koreksi stok"; _adjust.Font = new Font("Segoe UI", 9.5F); Add(table, "Izin Koreksi", _adjust);
            var note = new Label { Text = _id == 0 ? "Password minimal 6 karakter." : "Kosongkan password jika tidak ingin mengubahnya.", AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8F) };
            table.Controls.Add(new Label(), 0, 7); table.Controls.Add(note, 1, 7);
            var save = Ui.Button("Simpan", 140, Ui.Success); table.Controls.Add(new Label(), 0, 8); table.Controls.Add(save, 1, 8); Controls.Add(table); save.Click += Save; AcceptButton = save;

            if (row != null)
            {
                _username.Text = Convert.ToString(row.Cells["Username"].Value); _display.Text = Convert.ToString(row.Cells["Nama Tampilan"].Value);
                _role.SelectedItem = Convert.ToString(row.Cells["Role"].Value); _active.Checked = Convert.ToString(row.Cells["Status"].Value) == "Aktif";
                _purchase.Checked = Convert.ToString(row.Cells["Boleh Stok Masuk"].Value) == "Ya"; _adjust.Checked = Convert.ToString(row.Cells["Boleh Koreksi Stok"].Value) == "Ya";
            }
            SyncRolePermissions();
            _role.SelectedIndexChanged += delegate { SyncRolePermissions(); };
        }

        private static void Add(TableLayoutPanel table, string label, Control control)
        {
            control.Dock = DockStyle.Fill; table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) }); table.Controls.Add(control);
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                UserService.Save(_actor, _id, _username.Text, _display.Text, Convert.ToString(_role.SelectedItem), _password.Text, _active.Checked, _purchase.Checked, _adjust.Checked);
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { Ui.MessageError(ex, "Pengguna"); }
        }

        private void SyncRolePermissions()
        {
            var super = string.Equals(Convert.ToString(_role.SelectedItem), "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase);
            _purchase.Enabled = !super;
            _adjust.Enabled = !super;
            if (super)
            {
                _purchase.Checked = true;
                _adjust.Checked = true;
            }
            else if (!_purchase.Enabled)
            {
                _purchase.Enabled = true;
                _adjust.Enabled = true;
            }
        }
    }

    internal class MasterDataPanel : UserControl
    {
        private readonly AppUser _actor;
        private readonly DataGridView _categories = Ui.Grid();
        private readonly DataGridView _suppliers = Ui.Grid();
        public MasterDataPanel(AppUser actor)
        {
            _actor = actor; Dock = DockStyle.Fill; BackColor = Ui.Background;
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

            var cat = new TabPage("Kategori Obat") { BackColor = Color.White };
            var catTop = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(6) };
            var catAdd = Ui.Button("Tambah Kategori", 0, Ui.Accent);
            var catEdit = Ui.Button("Ubah Kategori");
            catTop.Controls.Add(catAdd); catTop.Controls.Add(catEdit); cat.Controls.Add(catTop); cat.Controls.Add(_categories); _categories.BringToFront();
            var catInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(8, 6, 8, 0),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Text = "Kategori dipakai untuk mengelompokkan obat, misalnya tablet, sirup, salep, alat kesehatan, atau kategori lain yang memudahkan pencarian."
            };
            cat.Controls.Add(catInfo); catInfo.BringToFront();

            var sup = new TabPage("Supplier") { BackColor = Color.White };
            var supTop = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, Padding = new Padding(6) };
            var supAdd = Ui.Button("Tambah Supplier", 0, Ui.Accent);
            var supEdit = Ui.Button("Ubah Supplier");
            supTop.Controls.Add(supAdd); supTop.Controls.Add(supEdit); sup.Controls.Add(supTop); sup.Controls.Add(_suppliers); _suppliers.BringToFront();

            tabs.TabPages.Add(cat); tabs.TabPages.Add(sup); Controls.Add(tabs);
            catAdd.Click += delegate { using (var f = new CategoryDialog(_actor, null)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
            catEdit.Click += delegate { if (_categories.CurrentRow == null) return; using (var f = new CategoryDialog(_actor, _categories.CurrentRow)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
            supAdd.Click += delegate { using (var f = new SupplierDialog(_actor, null)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
            supEdit.Click += delegate { if (_suppliers.CurrentRow == null) return; using (var f = new SupplierDialog(_actor, _suppliers.CurrentRow)) if (f.ShowDialog() == DialogResult.OK) Reload(); };
            Reload();
        }
        private void Reload() { _categories.DataSource = MasterService.Categories(true); _suppliers.DataSource = MasterService.Suppliers(true); }
    }

    internal class CategoryDialog : Form
    {
        private readonly AppUser _actor; private readonly long _id; private readonly TextBox _name = new TextBox(); private readonly CheckBox _active = new CheckBox { Text = "Aktif", Checked = true };
        public CategoryDialog(AppUser actor, DataGridViewRow row)
        {
            _actor = actor; _id = row == null ? 0 : Convert.ToInt64(row.Cells["id"].Value);
            Text = _id == 0 ? "Tambah Kategori" : "Ubah Kategori"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(440, 195);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            Controls.Add(new Label { Text = "Nama kategori", AutoSize = true, Location = new Point(25, 25), Font = new Font("Segoe UI Semibold", 9F) });
            _name.Location = new Point(25, 50); _name.Width = 380; _name.Font = new Font("Segoe UI", 10F); Controls.Add(_name);
            _active.Location = new Point(25, 95); _active.Font = new Font("Segoe UI", 9.5F); Controls.Add(_active);

            var save = Ui.Button("Simpan", 120, Ui.Success); save.Location = new Point(285, 135); Controls.Add(save);
            save.Click += delegate { try { MasterService.SaveCategory(_actor, _id, _name.Text, _active.Checked); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { Ui.MessageError(ex); } };
            if (row != null) { _name.Text = Convert.ToString(row.Cells["Nama"].Value); _active.Checked = Convert.ToString(row.Cells["Status"].Value) == "Aktif"; }
        }
    }

    internal class SupplierDialog : Form
    {
        private readonly AppUser _actor; private readonly long _id;
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _phone = new TextBox();
        private readonly TextBox _address = new TextBox();
        private readonly CheckBox _active = new CheckBox { Text = "Aktif", Checked = true };

        public SupplierDialog(AppUser actor, DataGridViewRow row)
        {
            _actor = actor; _id = row == null ? 0 : Convert.ToInt64(row.Cells["id"].Value);
            Text = _id == 0 ? "Tambah Supplier" : "Ubah Supplier"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, 340);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24) }; table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Add(table, "Nama", _name); Add(table, "Telepon", _phone); _address.Multiline = true; _address.Height = 90; _address.Font = new Font("Segoe UI", 9.5F);
            Add(table, "Alamat", _address);
            _active.Font = new Font("Segoe UI", 9.5F);
            Add(table, "Status", _active);
            var save = Ui.Button("Simpan", 120, Ui.Success); table.Controls.Add(new Label(), 0, 4); table.Controls.Add(save, 1, 4); Controls.Add(table);

            save.Click += delegate { try { MasterService.SaveSupplier(_actor, _id, _name.Text, _phone.Text, _address.Text, _active.Checked); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { Ui.MessageError(ex); } };
            if (row != null) { _name.Text = Convert.ToString(row.Cells["Nama"].Value); _phone.Text = Convert.ToString(row.Cells["Telepon"].Value); _address.Text = Convert.ToString(row.Cells["Alamat"].Value); _active.Checked = Convert.ToString(row.Cells["Status"].Value) == "Aktif"; }
        }
        private static void Add(TableLayoutPanel t, string label, Control c)
        {
            c.Dock = DockStyle.Fill;
            t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI Semibold", 9F) });
            t.Controls.Add(c);
        }
    }

    internal class ToolsPanel : UserControl
    {
        private readonly AppUser _user;

        public ToolsPanel(AppUser user)
        {
            _user = user; Dock = DockStyle.Fill; BackColor = Ui.Background;

            // ── Panel Backup / Export ─────────────────────────────────────────
            var backupCard = MakeCard("Backup Data", 140);

            var buttons = new FlowLayoutPanel { Location = new Point(24, 62), Width = 780, Height = 50, BackColor = Color.Transparent };
            var backup = Ui.Button("Backup Database", 0, Ui.Accent);
            var restore = Ui.Button("Restore Database");
            buttons.Controls.Add(backup); buttons.Controls.Add(restore);
            backupCard.Controls.Add(buttons);

            backup.Enabled = _user.IsSuperAdmin; restore.Enabled = _user.IsSuperAdmin;

            var info = new Label
            {
                AutoSize = true,
                Location = new Point(24, 122),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Text = "Database: " + AppPaths.DatabasePath
                    + "\nSchema: v" + Database.CurrentSchemaVersion + "  |  Mode: Offline / SQLite lokal"
                    + "\n\n• Backup: salin file .db ke folder backup."
                    + "\n• Restore: ganti database aktif (hanya Super Admin)."

            };
            backupCard.Controls.Add(info);
            Controls.Add(backupCard);


            // Event handlers ────────────────────────────────────────────────
            backup.Click += delegate { try { MessageBox.Show("Backup dibuat:\n" + ExcelBackupService.BackupDatabase(_user)); } catch (Exception ex) { Ui.MessageError(ex); } };
            restore.Click += delegate
            {
                using (var d = new OpenFileDialog { Filter = "SQLite backup|*.db|Semua file|*.*" })
                    if (d.ShowDialog() == DialogResult.OK && MessageBox.Show("Restore akan mengganti database aktif. Backup keselamatan dibuat otomatis. Lanjut?", "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        try { ExcelBackupService.RestoreDatabase(_user, d.FileName); MessageBox.Show("Restore selesai. Tutup dan buka ulang aplikasi agar seluruh layar memakai data terbaru."); } catch (Exception ex) { Ui.MessageError(ex, "Restore"); }
                    }
            };
        }

        private static Panel MakeCard(string title, int height)
        {
            var p = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            p.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };
            p.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(24, 20)
            });
            return p;
        }
    }
}


