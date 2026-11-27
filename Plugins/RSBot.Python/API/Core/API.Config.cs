using System.IO;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.Core
{
    public class ConfigAPI : IPythonPlugin
    {
        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "config";

        public void Init(PythonContext context) { }

        public string get_config_dir() => Path.Combine(PythonPaths.AppRoot, "User");

        public string get_config_path() =>
            Game.Player == null
                ? Path.Combine(PythonPaths.AppRoot, "User", "Default")
                : Path.Combine(PythonPaths.AppRoot, "User", "Default", $"{Game.Player.Name}.rs");

        public string get_log_dir() => Path.Combine(PythonPaths.AppRoot, "User", "Logs");

        public string get_log_path() =>
            Game.Player == null
                ? Path.Combine(PythonPaths.AppRoot, "User", "Logs")
                : Path.Combine(
                    PythonPaths.AppRoot,
                    "User",
                    "Logs",
                    $"{ProfileManager.SelectedProfile}-{Game.Player.Name}"
                );

        public string get_python_dir() => PythonPaths.PythonRoot;

        public string get_plugin_dir() => PythonPaths.PluginsRoot;
    }
}
