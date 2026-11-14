using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace RSBot.Core.Components;

public class ProfileManager
{
    /// <summary>
    ///     The profile config
    /// </summary>
    private static readonly Config<ProfileConfig> _config;

    /// <summary>
    ///     Get active profiles
    /// </summary>
    private static readonly ObservableCollection<string> _profiles;

    /// <summary>
    ///     Initialize static ctor
    /// </summary>
    static ProfileManager()
    {
        _config = new Config<ProfileConfig>(GetProfileConfigFileName());
        _config.Load();

        _profiles = new ObservableCollection<string>(_config.Data.Profiles ?? ["Default"]);

        MigrateOldProfiles();

        if (!_profiles.Contains("Default"))
            _profiles.Insert(0, "Default");

        _profiles.CollectionChanged += Profiles_CollectionChanged;
    }

    /// <summary>
    ///     Get active profiles
    /// </summary>
    public static string[] Profiles => _profiles.ToArray();

    /// <summary>
    ///     If the selected profile loaded via program args <c>true</c>; otherwise <c>false</c>.
    /// </summary>
    public static bool IsProfileLoadedByArgs { get; set; }

    /// <summary>
    ///     The selected character
    /// </summary>
    public static string SelectedCharacter { get; set; }

    /// <summary>
    ///     The selected account
    /// </summary>
    public static string SelectedAccount { get; set; }

    /// <summary>
    ///     The selected profile
    /// </summary>
    public static string SelectedProfile => _config.Data.SelectedProfile ?? "Default";

    /// <summary>
    ///     Show the profile dialog <c>true</c>; otherwise <c>false</c>
    /// </summary>
    public static bool ShowProfileDialog
    {
        get => _config.Data.ShowProfileDialog;
        set
        {
            _config.Data.ShowProfileDialog = value;
            _config.Save();
        }
    }

    /// <summary>
    ///     There have any value in the collection <c>true</c>; otherwise <c>false</c>
    /// </summary>
    public static bool Any()
    {
        return _profiles.Any();
    }

    /// <summary>
    ///     Called after Profiles are changed
    /// </summary>
    private static void Profiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _config.Data.Profiles = _profiles.ToList();
        _config.Save();
    }

    /// <summary>
    ///     Set selected profile
    /// </summary>
    /// <param name="profile">The profile</param>
    public static bool SetSelectedProfile(string profile)
    {
        if (!_profiles.Any(p => p.Equals(profile, StringComparison.InvariantCultureIgnoreCase)))
            return false;

        _config.Data.SelectedProfile = profile;
        _config.Save();

        return true;
    }

    /// <summary>
    ///     Is profile exists <c>true</c>; otherwise <c>false</c>
    /// </summary>
    /// <param name="profile">The profile</param>
    public static bool ProfileExists(string profile)
    {
        return _profiles.Any(p => p.Equals(profile, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    ///     Create new profile
    /// </summary>
    /// <param name="profile">The profile</param>
    /// <param name="useAsBase">Use as base <c>true</c>; otherwise <c>false</c></param>
    /// <returns>Is created <c>true</c>; otherwise <c>false</c></returns>
    public static bool Add(string profile, bool useAsBase = false)
    {
        string[] reservedNames = { "Profiles", "Default", "Settings" };

        if (reservedNames.Any(n => n.Equals(profile, StringComparison.InvariantCultureIgnoreCase)))
            return false;

        if (ProfileExists(profile))
        {
            SetSelectedProfile(profile);
            return true;
        }

        _profiles.Add(profile);

        if (useAsBase)
            CopyOldProfileData(profile);

        var newProfileDirectory = GetProfileDirectory(profile);
        if (!Directory.Exists(newProfileDirectory))
            Directory.CreateDirectory(newProfileDirectory);

        SetSelectedProfile(profile);

        return true;
    }

    /// <summary>
    ///     Remove the profile
    /// </summary>
    /// <param name="profile">The profile</param>
    /// <returns>Is removed <c>true</c>; otherwise <c>false</c></returns>
    public static bool Remove(string profile)
    {
        return _profiles.Remove(profile);
    }

    /// <summary>
    ///     Copies the old profile data to the new profile.
    /// </summary>
    /// <param name="profile">Name of the profile.</param>
    private static void CopyOldProfileData(string profile)
    {
        try
        {
            var oldProfileFilePath = GetProfileFile(SelectedProfile);
            var newProfileFilePath = GetProfileFile(profile);
            var oldAutoLoginFile = Path.Combine(GetProfileDirectory(SelectedProfile), "autologin.data");
            var newAutoLoginFile = Path.Combine(GetProfileDirectory(profile), "autologin.data");

            if (File.Exists(oldProfileFilePath))
                File.Copy(oldProfileFilePath, newProfileFilePath);

            if (File.Exists(oldAutoLoginFile))
                File.Copy(oldAutoLoginFile, newAutoLoginFile);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not copy old profile data to the new profile: {ex.Message}");
        }
    }

    /// <summary>
    ///     Get profile config file name
    /// </summary>
    /// <returns></returns>
    public static string GetProfileConfigFileName()
    {
        return Path.Combine(Kernel.BasePath, "User", "Profiles.json");
    }

    public static string GetProfileFile(string profileName)
    {
        return Path.Combine(Kernel.BasePath, "User", $"{profileName}.json");
    }

    public static string GetProfileDirectory(string profileName)
    {
        return Path.Combine(Kernel.BasePath, "User", profileName);
    }

    private static void MigrateOldProfiles()
    {
        var oldPath = Path.Combine(Kernel.BasePath, "User", "Profiles.rs");
        if (File.Exists(oldPath) && !File.Exists(GetProfileConfigFileName()))
        {
            try
            {
                foreach (var line in File.ReadAllLines(oldPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains('{')) continue;
                    var key = line.Split('{')[0].Trim();
                    var value = line.Split('{')[1].TrimEnd('}').Trim();
                    if (key == "RSBot.Profiles" && !string.IsNullOrEmpty(value))
                        _config.Data.Profiles = value.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
                    else if (key == "RSBot.SelectedProfile" && !string.IsNullOrEmpty(value))
                        _config.Data.SelectedProfile = value;
                    else if (key == "RSBot.ShowProfileDialog" && bool.TryParse(value, out var show))
                        _config.Data.ShowProfileDialog = show;
                }
                _config.Save();
            }
            catch (Exception ex)
            {
                Log.Warn($"[ProfileManager] Error migrating old profiles: {ex.Message}");
            }
        }
    }
    public class ProfileConfig
    {
        public List<string> Profiles { get; set; } = ["Default"];
        public string SelectedProfile { get; set; } = "Default";
        public bool ShowProfileDialog { get; set; } = false;
    }
}
