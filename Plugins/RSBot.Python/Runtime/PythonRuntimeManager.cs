using System;
using System.IO;
using System.Linq;
using Python.Runtime;

namespace RSBot.Python.Runtime
{
    public class PythonRuntimeManager
    {
        private readonly Action<string> _log;

        public PythonRuntimeManager(Action<string> log)
        {
            _log = log;
        }

        public bool IsInitialized { get; private set; }
        public string RuntimeVersion { get; private set; } = "Not initialized";

        public void Initialize()
        {
            if (IsInitialized)
                return;

            PythonPaths.EnsureDirectories();

            string pythonHome = PythonPaths.RuntimeRoot;
            string pythonDll = Directory
                .GetFiles(pythonHome, "python3*.dll", SearchOption.TopDirectoryOnly)
                .Where(x => !Path.GetFileName(x).Equals("python3.dll", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x)
                .FirstOrDefault();

            if (pythonDll == null)
            {
                _log?.Invoke(
                    $"Python runtime DLL was not found in '{pythonHome}'. Check if Python was downloaded correctly"
                );
                return;
            }

            string zipPath = Directory
                .GetFiles(pythonHome, "python3*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(x => x)
                .FirstOrDefault();
            string lib = Path.Combine(pythonHome, "Lib");
            string sitePackages = Path.Combine(lib, "site-packages");

            global::Python.Runtime.Runtime.PythonDLL = pythonDll;

            PythonEngine.PythonHome = pythonHome;
            PythonEngine.PythonPath = string.Join(
                ";",
                new[] { pythonHome, zipPath, lib, sitePackages, PythonPaths.PluginsRoot }.Where(x =>
                    !string.IsNullOrWhiteSpace(x)
                )
            );

            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();

            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                RuntimeVersion = ((string)sys.version).Split(' ')[0];
            }

            IsInitialized = true;
            _log?.Invoke($"Python {RuntimeVersion} initialized from {pythonHome}.");
        }
    }
}
