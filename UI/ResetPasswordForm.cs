using System;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    public class ResetPasswordForm : Form
    {
        private readonly TextBox _txtUser = new TextBox();
        private readonly TextBox _txtOldPass = new TextBox();
        private readonly TextBox _txtNewPass = new TextBox();
        private readonly TextBox _txtConfirmPass = new TextBox();
        private readonly CheckBox _chkShowPass = new CheckBox();

        public string ResetUsername { get; private set; }

        public ResetPasswordForm(string initialUsername = "")
        {
            Text = "Firdan Farma - Reset Password";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, 460);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var title = new Label
            {
                Text = "RESET PASSWORD",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = true,
                Location = new Point(45, 20)
            };
            var sub = new Label
            {
                Text = "Masukkan username & password lama untuk reset",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.DimGray,
                Location = new Point(45, 52)
            };
            Controls.Add(title);
            Controls.Add(sub);

            int y = 90;
            int x = 45;
            int inputWidth = 340;

            // Username
            Controls.Add(new Label { Text = "Username", Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            y += 20;
            _txtUser.Location = new Point(x, y);
            _txtUser.Width = inputWidth;
            _txtUser.Font = new Font("Segoe UI", 9.5F);
            _txtUser.Text = initialUsername ?? "";
            Controls.Add(_txtUser);
            y += 35;

            // Password Sebelumnya / Lama
            Controls.Add(new Label { Text = "Password Sebelumnya (Lama)", Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            y += 20;
            _txtOldPass.Location = new Point(x, y);
            _txtOldPass.Width = inputWidth;
            _txtOldPass.Font = new Font("Segoe UI", 9.5F);
            _txtOldPass.UseSystemPasswordChar = true;
            Controls.Add(_txtOldPass);
            y += 35;

            // Password Baru
            Controls.Add(new Label { Text = "Password Baru", Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            y += 20;
            _txtNewPass.Location = new Point(x, y);
            _txtNewPass.Width = inputWidth;
            _txtNewPass.Font = new Font("Segoe UI", 9.5F);
            _txtNewPass.UseSystemPasswordChar = true;
            Controls.Add(_txtNewPass);
            y += 35;

            // Konfirmasi Password Baru
            Controls.Add(new Label { Text = "Konfirmasi Password Baru", Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            y += 20;
            _txtConfirmPass.Location = new Point(x, y);
            _txtConfirmPass.Width = inputWidth;
            _txtConfirmPass.Font = new Font("Segoe UI", 9.5F);
            _txtConfirmPass.UseSystemPasswordChar = true;
            Controls.Add(_txtConfirmPass);
            y += 30;

            // Checkbox Show Password
            _chkShowPass.Text = "Tampilkan Password";
            _chkShowPass.Location = new Point(x, y);
            _chkShowPass.AutoSize = true;
            _chkShowPass.Font = new Font("Segoe UI", 8.5F);
            _chkShowPass.ForeColor = Color.FromArgb(71, 85, 105);
            _chkShowPass.CheckedChanged += TogglePasswordVisibility;
            Controls.Add(_chkShowPass);
            y += 35;

            // Buttons
            var btnSave = Ui.Button("SIMPAN PASSWORD BARU", 340, Ui.Accent, Color.White);
            btnSave.Location = new Point(x, y);
            btnSave.Height = 38;
            btnSave.Click += Save;
            Controls.Add(btnSave);
            y += 45;

            var btnCancel = Ui.Button("BATAL", 340);
            btnCancel.Location = new Point(x, y);
            btnCancel.Height = 32;
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void TogglePasswordVisibility(object sender, EventArgs e)
        {
            bool show = _chkShowPass.Checked;
            _txtOldPass.UseSystemPasswordChar = !show;
            _txtNewPass.UseSystemPasswordChar = !show;
            _txtConfirmPass.UseSystemPasswordChar = !show;
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                AuthService.ResetPasswordWithOldPassword(_txtUser.Text, _txtOldPass.Text, _txtNewPass.Text, _txtConfirmPass.Text);
                MessageBox.Show("Password berhasil diperbarui!\nSilakan login dengan password baru Anda.", "Reset Password", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetUsername = _txtUser.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Reset Password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
