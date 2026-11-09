using System.IO;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.GlobalConfigProfile;

namespace RSBot.Core;

public static class GlobalConfig
{
    /// <summary>
    ///     The config
    /// </summary>
    private static Config<GlobalConfigProfile> _config;

    /// <summary>
    /// Direktes typisiertes Profil
    /// </summary>
    public static GlobalConfigProfile Profile => _config?.Data ?? (_config = LoadGlobalConfig()).Data;

    /// <summary>
    ///     Load config from file
    /// </summary>
    public static void Load()
    {
        _config = LoadGlobalConfig();
        Log.Notify("[Global] settings have been loaded!");
    }

    private static Config<GlobalConfigProfile> LoadGlobalConfig()
    {
        var path = Path.Combine(Kernel.BasePath, "User", $"{ProfileManager.SelectedProfile}.json");
        var cfg = new Config<GlobalConfigProfile>(path);
        cfg.Load();
        return cfg;
    }

    /// <summary>
    ///     Saves the specified file.
    /// </summary>
    /// <param name="file">The file.</param>
    public static void Save()
    {
        if (_config == null)
            return;

        _config.Save();

        Log.Notify("[Global] settings have been saved!");
        EventManager.FireEvent("OnSaveGlobalConfig");
    }

    public static string GetSilkroadDirectoryFromProfile(string profileName)
    {
        var path = Path.Combine(Kernel.BasePath, "User", $"{profileName}.json");
        var cfg = new Config<GlobalConfigProfile>(path);
        cfg.Load();
        return cfg.Data.SilkroadDirectory;
    }

    public class GlobalConfigProfile
    {
        public BotConfig Bot { get; set; } = new();
        public JsroConfig Jsro { get; set; } = new();
        public JcPlanetConfig JcPlanet { get; set; } = new();
        public RuSroConfig RuSro { get; set; } = new();

        public bool EnableCollisionDetection { get; set; } = false;
        public bool DebugEnvironment { get; set; } = false;
        public bool TrayWhenMinimize { get; set; } = true;
        public bool ShowSidebar { get; set; } = true;
        public bool ShowPythonPlugins { get; set; } = true;
        public bool LoaderDebugMode { get; set; } = false;
        public string Pk2Key { get; set; } = "169841";
        public int DivisionIndex { get; set; } = 0;
        public int GatewayIndex { get; set; } = 0;
        public string SilkroadDirectory { get; set; } = string.Empty;
        public string SilkroadExecutable { get; set; } = string.Empty;
        public int TranslationIndex { get; set; } = -1;
        public int ThemeColor { get; set; } = 0;
        public int[] CustomColors { get; set; } = [];
    }
}
