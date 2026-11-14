using RSBot.Core.Components;
using RSBot.Core.Config.PlayerConfigProfile;
using RSBot.Core.Event;
using RSBot.Core.PlayerConfigProfile;
using System.IO;

namespace RSBot.Core;

public static class PlayerConfig
{
    /// <summary>
    ///     The config
    /// </summary>
    private static Config<PlayerConfigProfile> _config;

    /// <summary>
    ///     The config directory
    /// </summary>
    private static string _configDirectory => Path.Combine(Kernel.BasePath, "User", ProfileManager.SelectedProfile);

    /// <summary>
    ///     The joined servername
    /// </summary>
    private static string _server => Game.JoinedServer;

    public static PlayerConfigProfile   Profile => _config?.Data ?? new();

    /// <summary>
    ///     Load config from file
    /// </summary>
    /// <param name="file">The config file path</param>
    public static void Load(string charName)
    {
        _config = new Config<PlayerConfigProfile>(Path.Combine(_configDirectory, _server + "_" + charName + ".json"));
        _config.Load();
        Log.Notify("[Player] Config have been loaded!");
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

        Log.Notify("[Player] have been saved!");
        EventManager.FireEvent("OnSavePlayerConfig");
    }

    public class PlayerConfigProfile
    {
        public ProtectionConfig Protection { get; set; } = new();
        public TrainingConfig Training { get; set; } = new();
        public TradeConfig Trade { get; set; } = new();
        public SkillConfig Skills { get; set; } = new();
        public InventoryConfig Inventory { get; set; } = new();
        public PartyConfig Party { get; set; } = new();
        public TownConfig Shopping { get; set; } = new();
        public PickupConfig Pickup { get; set; } = new();
        public LureConfig Lure { get; set; } = new();
        public AlarmConfig Alarms { get; set; } = new();
        public AvoidanceConfig Avoidance { get; set; } = new();
        public CommandsConfig Commands { get; set; } = new();
        public StatisticsConfig Statistics { get; set; } = new();
        public QuestConfig Quests { get; set; } = new();

        public bool AutoSelectUniqueOnMap { get; set; } = false;
        public bool EnableCommandCenter { get; set; } = false;
        public string LastWhisper { get; set; } = string.Empty;
    }
}
