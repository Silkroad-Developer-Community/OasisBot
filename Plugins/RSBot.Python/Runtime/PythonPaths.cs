using RSBot.Core;
using System;
using System.IO;

namespace RSBot.Python.Runtime
{
    public static class PythonPaths
    {
        public static string AppRoot => Kernel.BasePath ?? AppDomain.CurrentDomain.BaseDirectory;
        public static string PythonRoot => Path.Combine(AppRoot, "Data", "Python");
        public static string RuntimeRoot => Path.Combine(PythonRoot, "PyRuntime");
        public static string PluginsRoot => Path.Combine(PythonRoot, "Plugins");
        public static string StubPath => Path.Combine(PluginsRoot, "OasisBot.pyi");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(PythonRoot);
            Directory.CreateDirectory(RuntimeRoot);
            Directory.CreateDirectory(PluginsRoot);
        }
    }
}
