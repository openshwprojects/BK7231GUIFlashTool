using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Reflection;

namespace BK7231Flasher
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // IMPORTANT: Preload JSON-related dependencies from the app directory.
            // Some deployment/distribution paths can change .NET Framework assembly binding/load context,
            // causing runtime FileLoadException for System.Memory (and friends) when JSON rendering happens.
            SetupJsonDependencyLoading();

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


        private static void SetupJsonDependencyLoading()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                TryPreloadJsonDependencies();
            }
            catch
            {
                // Non-fatal: the app can still run, but enhanced JSON pretty-print may fall back.
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
        {
            try
            {
                var an = new AssemblyName(e.Name);
                var simpleName = an.Name;
                if (!IsJsonDependency(simpleName))
                    return null;

                // If already loaded, reuse it.
                var loaded = FindLoadedAssembly(simpleName);
                if (loaded != null)
                    return loaded;

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(baseDir, simpleName + ".dll");
                if (!File.Exists(path))
                    return null;

                // Load from bytes to avoid LoadFrom-context and "downloaded file" restrictions.
                return Assembly.Load(File.ReadAllBytes(path));
            }
            catch
            {
                return null;
            }
        }

        private static void TryPreloadJsonDependencies()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Order matters a bit (System.Text.Json pulls several of these in).
            string[] deps = new string[]
            {
                "System.Runtime.CompilerServices.Unsafe",
                "System.Buffers",
                "System.Memory",
                "System.Numerics.Vectors",
                "System.Threading.Tasks.Extensions",
                "System.ValueTuple",
                "System.Text.Encodings.Web",
                "Microsoft.Bcl.AsyncInterfaces",
                "System.IO.Pipelines",
                "System.Text.Json",
            };

            foreach (var name in deps)
            {
                try
                {
                    if (FindLoadedAssembly(name) != null)
                        continue;

                    var path = Path.Combine(baseDir, name + ".dll");
                    if (!File.Exists(path))
                        continue;

                    // Prefer byte-load to avoid file-zone issues.
                    Assembly.Load(File.ReadAllBytes(path));
                }
                catch
                {
                    // Ignore individual preload failures; AssemblyResolve still provides a second chance.
                }
            }
        }

        private static bool IsJsonDependency(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return false;

            switch (simpleName)
            {
                case "System.Text.Json":
                case "System.Text.Encodings.Web":
                case "System.Memory":
                case "System.Buffers":
                case "System.Numerics.Vectors":
                case "System.Runtime.CompilerServices.Unsafe":
                case "System.Threading.Tasks.Extensions":
                case "System.ValueTuple":
                case "Microsoft.Bcl.AsyncInterfaces":
                case "System.IO.Pipelines":
                    return true;
            }

            return false;
        }

        private static Assembly FindLoadedAssembly(string simpleName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var n = asm.GetName().Name;
                        if (string.Equals(n, simpleName, StringComparison.OrdinalIgnoreCase))
                            return asm;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
