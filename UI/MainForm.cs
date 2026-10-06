using System;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Core;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    public class MainForm : Form
    {
        private readonly AppUser _user;
        private readonly Panel _content = new Panel();
        private readonly Label _pageTitle = new Label();
        private readonly Label _period = new Label();
        private readonly Label _startupClock = new Label();
        private readonly Label _liveClock = new Label();
        private readonly Label _clockWarning = new Label();
        private readonly Timer _clockTimer = new Timer();
        private bool _loggedOut;
        private Button _activeMenuBtn;
        public bool RequestRelogin { get; private set; }

        public MainForm(AppUser user)
        {
            _user = user;
            Text = "Firdan Farma - Apotek Offline";
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1120, 720);
            Font = new Font("Segoe UI", 9F);

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Ui.Sidebar };
            sidebar.Controls.Add(new Label { Text = "FIRDAN\nFARMA", ForeColor = Color.White, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(24, 24) });
            int y = 110;
            AddMenu(sidebar, "Beranda", delegate { ShowPage("Beranda", new DashboardPanel(_user)); }, ref y);
            AddMenu(sidebar, "Obat", delegate { ShowPage("Obat", new MedicinePanel(_user)); }, ref y);
            AddMenu(sidebar, "Penjualan", delegate { ShowPage("Penjualan", new SalePanel(_user)); }, ref y);
            AddMenu(sidebar, "Kartu Stok", delegate { ShowPage("Kartu Stok", new StockCardPanel(_user)); }, ref y);
            if (_user.IsSuperAdmin) AddMenu(sidebar, "Stok Opname", delegate { ShowPage("Stok Opname", new StockOpnamePanel(_user)); }, ref y);
            if (_user.CanAdjustStockEffective) AddMenu(sidebar, "Koreksi Stok", delegate { ShowPage("Koreksi Stok", new StockAdjustmentPanel(_user)); }, ref y);
            AddMenu(sidebar, "Laporan", delegate { ShowPage("Laporan", new ReportsPanel(_user)); }, ref y);
            AddMenu(sidebar, "Aktivitas", delegate { ShowPage("Aktivitas", new ActivityPanel(_user)); }, ref y);
            if (_user.IsSuperAdmin)
            {
                AddMenu(sidebar, "Pengguna", delegate { ShowPage("Pengguna", new UserManagementPanel(_user)); }, ref y);
                AddMenu(sidebar, "Master Data", delegate { ShowPage("Master Data", new MasterDataPanel(_user)); }, ref y);
            }
            if (_user.IsSuperAdmin) AddMenu(sidebar, "Backup", delegate { ShowPage("Backup", new ToolsPanel(_user)); }, ref y);
            AddMenu(sidebar, "Ganti Pengguna", RequestReloginAndClose, ref y);

            var who = new Label { Text = user.DisplayName + "\n" + user.Role, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI Semibold", 8.5F), AutoSize = true, Location = new Point(24, ClientSize.Height - 85), Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            sidebar.Controls.Add(who);
            Controls.Add(sidebar);

            var top = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.White };
            _pageTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold); _pageTitle.ForeColor = Color.FromArgb(15, 23, 42); _pageTitle.AutoSize = true; _pageTitle.Location = new Point(24, 18);
            _period.Font = new Font("Segoe UI", 9F); _period.AutoSize = true; _period.ForeColor = Color.FromArgb(100, 116, 139);
            _startupClock.Font = new Font("Segoe UI", 9F); _startupClock.AutoSize = true; _startupClock.ForeColor = Color.FromArgb(71, 85, 105);
            _liveClock.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold); _liveClock.AutoSize = true; _liveClock.ForeColor = Color.FromArgb(15, 23, 42);
            _clockWarning.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold); _clockWarning.AutoSize = true; _clockWarning.ForeColor = Color.FromArgb(185, 28, 28); _clockWarning.Visible = false;

            var headerRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 520,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 14, 24, 0)
            };
            headerRight.Controls.Add(_period);
            headerRight.Controls.Add(_startupClock);
            headerRight.Controls.Add(_liveClock);
            headerRight.Controls.Add(_clockWarning);
            top.Controls.Add(headerRight);
            top.Controls.Add(_pageTitle);
            Controls.Add(top);

            _content.Dock = DockStyle.Fill; _content.BackColor = Ui.Background; _content.Padding = new Padding(18); Controls.Add(_content); _content.BringToFront();
            FormClosing += MainForm_FormClosing;
            _clockTimer.Interval = 1000;
            _clockTimer.Tick += delegate { RefreshClockHeader(); };
            _clockTimer.Start();
            RefreshClockHeader();
            ShowPage("Beranda", new DashboardPanel(_user));
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_loggedOut) return;

            AuthService.Logout(_user);
            _loggedOut = true;
        }

        private void RequestReloginAndClose()
        {
            if (MessageBox.Show("Keluar ke layar login?", "Ganti Pengguna", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            RequestRelogin = true;
            if (!_loggedOut)
            {
                AuthService.Logout(_user);
                _loggedOut = true;
            }
            Close();
        }

        private void HighlightMenu(Button btn)
        {
            if (_activeMenuBtn != null)
            {
                _activeMenuBtn.BackColor = Ui.Sidebar;
                _activeMenuBtn.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            }
            _activeMenuBtn = btn;
            if (_activeMenuBtn != null)
            {
                _activeMenuBtn.BackColor = Color.FromArgb(71, 85, 105); // Slate 600
                _activeMenuBtn.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            }
        }

        private void AddMenu(Panel p, string text, Action action, ref int y)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Ui.Sidebar,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(24, 0, 0, 0),
                Location = new Point(0, y),
                Width = 220,
                Height = 40,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 65, 85); // Slate 700
            b.Click += delegate
            {
                HighlightMenu(b);
                action();
            };
            p.Controls.Add(b); y += 41;

            if (text == "Beranda" && _activeMenuBtn == null)
            {
                HighlightMenu(b);
            }
        }

        private void ShowPage(string title, Control control)
        {
            _pageTitle.Text = title;
            var reportDate = TransactionService.GetReportDate(DateTime.Now);
            _period.Text = "Tanggal: " + reportDate.ToString("dd MMM yyyy") + "  |  Sekat laporan 15:00";
            RefreshClockHeader();
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
        }

        private void RefreshClockHeader()
        {
            _startupClock.Text = "Mulai sesi: " + AppClock.StartupText;
            _liveClock.Text = "Jam live: " + AppClock.CurrentText;

            string warning;
            AppClock.Observe(out warning);
            if (warning != null)
            {
                _clockWarning.Text = warning;
                _clockWarning.Visible = true;
            }
        }

    }
}
