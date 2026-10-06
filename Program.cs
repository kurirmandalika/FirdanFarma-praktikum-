using System;
using System.Windows.Forms;
using ApotekFirdanFarma.Core;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.UI;

namespace ApotekFirdanFarma
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                AppClock.Initialize();
                Database.Initialize();
                while (true)
                {
                    using (var login = new LoginForm())
                    {
                        if (login.ShowDialog() != DialogResult.OK) break;
                        using (var main = new MainForm(login.CurrentUser))
                        {
                            Application.Run(main);
                            if (!main.RequestRelogin) break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Aplikasi gagal dijalankan:\n" + ex.Message, "Firdan Farma", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
