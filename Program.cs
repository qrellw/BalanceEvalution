using System;
using System.Windows.Forms;

namespace BalanceApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Khởi tạo cấu hình mặc định (.NET 6+ WinForms template)
            ApplicationConfiguration.Initialize();
            Application.Run(new LoginForm()); // form khởi động
        }
    }
}