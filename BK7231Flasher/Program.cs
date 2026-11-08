using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Dependency DLL version mismatches (System.Text.Json & friends) are handled by the
            // binding redirects in BK7231Flasher.exe.config, generated at build time
            // (AutoGenerateBindingRedirects in the .csproj). Do not load assemblies from raw bytes
            // here: Assembly.Load(byte[]) is a classic malware-loader pattern and triggers AV/AMSI.

            // If command-line arguments are provided, run in CLI mode
            if (CommandLineRunner.ShouldRunCli(args))
            {
                CommandLineRunner.Run(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
