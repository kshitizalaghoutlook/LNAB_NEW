using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LNAB
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, __) => Form1.ShutdownBrowser();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => Form1.ShutdownBrowser();
            Application.ThreadException += (_, __) => Form1.ShutdownBrowser();
            TaskScheduler.UnobservedTaskException += (_, __) => Form1.ShutdownBrowser();
            Application.ApplicationExit += (_, __) => Form1.ShutdownBrowser();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
