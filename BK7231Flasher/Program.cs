using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BK7231Flasher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void TryPreloadManagedDeps()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] dlls = new[]
                {
                    // Load order matters (dependencies first).
                    "System.Runtime.CompilerServices.Unsafe.dll",
                    "System.Buffers.dll",
                    "System.Memory.dll",
                    "System.Numerics.Vectors.dll",
                    "System.ValueTuple.dll",
                    "Microsoft.Bcl.AsyncInterfaces.dll",
                    "System.Text.Encodings.Web.dll",
                    "System.Text.Json.dll",
                    "System.IO.Pipelines.dll",
                    "System.Threading.Tasks.Extensions.dll",
                };

                foreach (var dll in dlls)
                {
                    string path = Path.Combine(baseDir, dll);
                    if (!File.Exists(path))
                        continue;

                    try
                    {
                        // Prefer LoadFrom for normal probing.
                        Assembly.LoadFrom(path);
                    }
                    catch (FileLoadException)
                    {
                        // Fallback: bypass Mark-of-the-Web / remote-zone blocking.
                        Assembly.Load(File.ReadAllBytes(path));
                    }
                    catch
                    {
                        // Best-effort only.
                    }
                }
            }
            catch
            {
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
