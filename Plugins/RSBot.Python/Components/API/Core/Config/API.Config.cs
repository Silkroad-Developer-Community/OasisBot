using System.IO;
using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Python.Components.API.Interface;
using RSBot.Python.Views;

namespace RSBot.Python.Components.API.Core.Entity
{
    public class ConfigAPI : IPythonPlugin
    {
        private Main _main;

        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "config";

        /// <summary>
        /// Called once at startup to provide the main form to the plugin.
        /// </summary>
        /// <param name="main">Main form</param>
        public void Init(Main main)
        {
            _main = main;
        }

        private string GetConfigDir()
        {
            return Path.Combine(Kernel.BasePath, "User");
        }

        private string GetConfigPath()
        {
            var player = Game.Player;
            return player == null
                ? ProfileManager.GetProfileDirectory(ProfileManager.SelectedProfile)
                : PlayerConfig.GetConfigPath(player.Name);
        }

        private string GetLogDir()
        {
            return Path.Combine(GetConfigDir(), "Logs");
        }

        private string GetLogPath()
        {
            var player = Game.Player;
            var logDirectory = GetLogDir();
            return player == null
                ? logDirectory
                : Path.Combine(logDirectory, $"{ProfileManager.SelectedProfile}-{player.Name}");
        }

        public string get_config_dir()
        {
            return GetConfigDir();
        }

        public string get_config_path()
        {
            return GetConfigPath();
        }

        public string get_log_dir()
        {
            return GetLogDir();
        }

        public string get_log_path()
        {
            return GetLogPath();
        }
    }
}
