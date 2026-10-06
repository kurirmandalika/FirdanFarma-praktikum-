using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ApotekFirdanFarma.UI
{
    internal static class Ui
    {
        public static readonly Color Sidebar = Color.FromArgb(30, 41, 59); // Slate 800
        public static readonly Color Background = Color.FromArgb(241, 245, 249); // Slate 100
        public static readonly Color Accent = Color.FromArgb(37, 99, 235); // Blue 600
        public static readonly Color Success = Color.FromArgb(22, 163, 74); // Green 600
        public static readonly Color Danger = Color.FromArgb(220, 38, 38); // Red 600

        public static Button Button(string text, int width = 0, Color? bg = null, Color? fg = null)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Height = 34,
                Font = new Font("Segoe UI Semibold", 9F),
                BackColor = bg ?? Color.FromArgb(226, 232, 240), // Slate 200
                ForeColor = fg ?? (bg.HasValue ? Color.White : Color.FromArgb(51, 65, 85)), // Slate 700
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Padding = new Padding(12, 0, 12, 0);
            b.AutoSize = width <= 0;
            if (width > 0) b.Width = width;
            return b;
        }

        public static DataGridView Grid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                GridColor = Color.FromArgb(241, 245, 249),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false
            };

            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(71, 85, 105); // Slate 600
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            g.ColumnHeadersHeight = 36;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.DefaultCellStyle.BackColor = Color.White;
            g.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42); // Slate 900
            g.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255); // Blue 50
            g.DefaultCellStyle.SelectionForeColor = Color.FromArgb(29, 78, 216); // Blue 700
            g.RowTemplate.Height = 32;

            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); // Slate 50

            g.DataBindingComplete += delegate
            {
                if (g.Columns.Contains("id")) g.Columns["id"].Visible = false;
                if (g.Columns.Contains("__separator")) g.Columns["__separator"].Visible = false;
                foreach (DataGridViewRow row in g.Rows)
                {
                    if (row.IsNewRow) continue;
                    var marker = g.Columns.Contains("__separator") && row.Cells["__separator"].Value != null ? Convert.ToInt32(row.Cells["__separator"].Value) : 0;
                    if (marker == 1)
                    {
                        row.Height = 16;
                        row.DefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
                        row.DefaultCellStyle.ForeColor = Color.White;
                        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 245, 249);
                        row.DefaultCellStyle.SelectionForeColor = Color.White;
                        row.DefaultCellStyle.Font = new Font("Segoe UI", 1F, FontStyle.Regular);
                    }
                }
            };
            return g;
        }

        public static Label Label(string text, int topPadding = 8, float fontSize = 9f, FontStyle style = FontStyle.Regular, Color? color = null)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", fontSize, style),
                ForeColor = color ?? Color.FromArgb(71, 85, 105), // Slate 600
                Padding = new Padding(0, topPadding, 0, 0)
            };
        }

        public static string Rp(object value)
        {
            var d = value == null || value == DBNull.Value ? 0d : Convert.ToDouble(value);
            return "Rp " + d.ToString("N0", new CultureInfo("id-ID"));
        }

        public static Panel Card(string label, string value, string sub = null)
        {
            var p = new Panel { Width = 210, Height = 108, BackColor = Color.White, Margin = new Padding(0, 0, 16, 16), Padding = new Padding(16) };
            p.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1)) // Slate 200
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                }
            };

            var lbl = new Label { Text = label, AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI Semibold", 8.5f), Location = new Point(16, 14) };
            var val = new Label { Text = value, AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(16, 38) };
            p.Controls.Add(lbl);
            p.Controls.Add(val);
            if (!string.IsNullOrWhiteSpace(sub))
            {
                var s = new Label { Text = sub, AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 8), Location = new Point(16, 76) };
                p.Controls.Add(s);
            }
            return p;
        }

        public static void MessageError(Exception ex, string title = "Firdan Farma")
        {
            MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void MessageInfo(string message, string title = "Firdan Farma")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    internal class TextPromptDialog : Form
    {
        private readonly TextBox _text;
        public string Value { get { return _text.Text.Trim(); } }

        public TextPromptDialog(string title, string label, bool multiline = true)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, multiline ? 240 : 170);
            Font = new Font("Segoe UI", 9F);
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(20, 20) });
            _text = new TextBox { Location = new Point(20, 48), Width = 475, Height = multiline ? 105 : 28, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            Controls.Add(_text);
            var ok = Ui.Button("OK", 110, Ui.Accent); ok.Location = new Point(270, multiline ? 175 : 105); ok.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Batal", 110); cancel.Location = new Point(390, multiline ? 175 : 105); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
