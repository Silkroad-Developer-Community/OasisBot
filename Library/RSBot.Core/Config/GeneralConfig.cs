using System;
using System.IO;

namespace RSBot.Core;

public static class GeneralConfig
{
    /// <summary>
    /// The config
    /// </summary>
    private static Config<GeneralConfigProfile> _config;

    /// <summary>
    /// Direktes typisiertes Profil
    /// </summary>
    public static GeneralConfigProfile Profile => _config?.Data ?? (_config = LoadGeneralConfig()).Data;

    /// <summary>
    /// Load config from file
    /// </summary>
    public static void Load()
    {
        _config = LoadGeneralConfig();
    }

    /// <summary>
    /// Load config from file
    /// </summary>
    private static Config<GeneralConfigProfile> LoadGeneralConfig()
    {
        var path = Path.Combine(Kernel.BasePath, "User", "Settings.json");
        var cfg = new Config<GeneralConfigProfile>(path);
        cfg.Load();
        return cfg;
    }     

    /// <summary>
    /// Saves the specified file.
    /// </summary>
    public static void Save()
    {
        if (_config == null)
            return;

        try
        {
            _config.Save();
        }
        catch (Exception ex)
        {
            Log.Debug($"[GeneralConfig] Could not save settings: {ex.Message}");
        }
    }

    public class GeneralConfigProfile
    {
        public bool AutoTheme { get; set; } = true;
        public bool AutoUpdate { get; set; } = true;
        public bool ShowExitDialog { get; set; } = true;
        public string Language { get; set; } = "en_US";
        public DateTime LastUpdateCheck { get; set; } = DateTime.Now;
    }
}
