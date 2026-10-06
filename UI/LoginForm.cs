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
            Text = "Firdan Farma - Login"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(430, 330); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            var title = new Label { Text="FIRDAN FARMA", Font=new Font("Segoe UI",22,FontStyle.Bold), AutoSize=true, Location=new Point(100,35) };
            var sub = new Label { Text="Sistem Apotek Offline", AutoSize=true, ForeColor=Color.DimGray, Location=new Point(145,80) };
            Controls.Add(title); Controls.Add(sub);
            Controls.Add(new Label { Text="Username", Location=new Point(65,125), AutoSize=true }); _user.Location=new Point(65,145); _user.Width=300; Controls.Add(_user);
            Controls.Add(new Label { Text="Password", Location=new Point(65,180), AutoSize=true }); _pass.Location=new Point(65,200); _pass.Width=300; _pass.UseSystemPasswordChar=true; Controls.Add(_pass);
            var btn = new Button { Text="MASUK", Location=new Point(65,245), Width=300, Height=38 }; btn.Click += Login; Controls.Add(btn); AcceptButton=btn;
        }
        private void Login(object sender, EventArgs e)
        {
            CurrentUser=AuthService.Login(_user.Text,_pass.Text); if(CurrentUser==null){MessageBox.Show("Username/password salah atau akun nonaktif.","Login",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            DialogResult=DialogResult.OK; Close();
        }
    }
}
