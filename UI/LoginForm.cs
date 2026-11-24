using System;
using System.Drawing;
using System.Windows.Forms;
using ApotekFirdanFarma.Models;
using ApotekFirdanFarma.Services;

namespace ApotekFirdanFarma.UI
{
    public class LoginForm : Form
    {
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _pass = new TextBox();
        public AppUser CurrentUser { get; private set; }

        public LoginForm()
        {
            Text = "Firdan Farma - Login"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(430, 360); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            var title = new Label { Text="FIRDAN FARMA", Font=new Font("Segoe UI",22,FontStyle.Bold), AutoSize=true, Location=new Point(100,30) };
            var sub = new Label { Text="Sistem Apotek Offline", AutoSize=true, ForeColor=Color.DimGray, Location=new Point(145,75) };
            Controls.Add(title); Controls.Add(sub);
            Controls.Add(new Label { Text="Username", Location=new Point(65,115), AutoSize=true }); _user.Location=new Point(65,135); _user.Width=300; Controls.Add(_user);
            Controls.Add(new Label { Text="Password", Location=new Point(65,170), AutoSize=true }); _pass.Location=new Point(65,190); _pass.Width=300; _pass.UseSystemPasswordChar=true; Controls.Add(_pass);
            var btn = new Button { Text="MASUK", Location=new Point(65,235), Width=300, Height=38 }; btn.Click += Login; Controls.Add(btn); AcceptButton=btn;

            var linkReset = new LinkLabel
            {
                Text = "Lupa / Reset Password?",
                AutoSize = true,
                Location = new Point(142, 290),
                LinkColor = Color.FromArgb(37, 99, 235),
                ActiveLinkColor = Color.FromArgb(29, 78, 216),
                Font = new Font("Segoe UI", 9F)
            };
            linkReset.LinkClicked += OpenResetPassword;
            Controls.Add(linkReset);
        }
        private void Login(object sender, EventArgs e)
        {
            CurrentUser=AuthService.Login(_user.Text,_pass.Text); if(CurrentUser==null){MessageBox.Show("Username/password salah atau akun nonaktif.","Login",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            DialogResult=DialogResult.OK; Close();
        }
        private void OpenResetPassword(object sender, LinkLabelLinkClickedEventArgs e)
        {
            using (var dlg = new ResetPasswordForm(_user.Text))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _user.Text = dlg.ResetUsername;
                    _pass.Text = "";
                    _pass.Focus();
                }
            }
        }
    }
}
