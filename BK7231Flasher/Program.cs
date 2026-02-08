using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Linq;
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
            // Ensure System.Text.Json dependency chain can load reliably on .NET Framework
            // even when files are marked with MOTW / remote-zone.
            TryPreloadManagedDeps();

            // Extra safety: if CLR still refuses to load a local dependency, resolve it
            // from bytes. (Prevents Zone.Identifier / file-origin from blocking load.)
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
                {
                    try
                    {
                        var an = new AssemblyName(e.Name);
                        var already = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => string.Equals(a.GetName().Name, an.Name, StringComparison.OrdinalIgnoreCase));
                        if (already != null)
                            return already;

                        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        var dllPath = Path.Combine(baseDir, an.Name + ".dll");
                        if (!File.Exists(dllPath))
                            return null;

                        return Assembly.Load(File.ReadAllBytes(dllPath));
                    }
                    catch
                    {
                        return null;
                    }
                };
            }
            catch
            {
                // best effort only
            }

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
