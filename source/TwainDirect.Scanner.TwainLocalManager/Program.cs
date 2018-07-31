using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TwainDirect.Scanner.TwainLocalManager
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using (WindowsIdentity windowsidentity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal windowsprincipal = new WindowsPrincipal(windowsidentity);
                if (!windowsprincipal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    MessageBox.Show("Please right-click on this program, and select 'Run as administrator'.", "TWAIN Local Manager");
                }
                Application.Exit();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
