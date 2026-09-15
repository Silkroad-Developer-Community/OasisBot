using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace RSBot.Core.Components;

public class ProfileManager
{
    /// <summary>
    ///     Get active profiles
    /// </summary>
    private static readonly ObservableCollection<string> _profiles;

    /// <summary>
    ///     Initialize static ctor
    /// </summary>
    static ProfileManager()
    {
        Config.Initialize();

        var loadedProfiles = GeneralConfig.GetArray<string>("RSBot.Profiles");
        string[] reservedNames = { "Settings", "Logs" };
        var validProfiles = loadedProfiles
            .Select(p => p.ToLowerInvariant())
            .Where(p => !reservedNames.Any(n => n.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        _profiles = new ObservableCollection<string>(validProfiles);

        var isNew = _profiles.Count == 0;
        if (isNew)
            _profiles.Insert(0, "default");

        _profiles.CollectionChanged += Profiles_CollectionChanged;

        if (isNew || loadedProfiles.Length != validProfiles.Count)
        {
            GeneralConfig.SetArray("RSBot.Profiles", _profiles);
            GeneralConfig.Save();
        }
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
    public static string SelectedProfile { get; set; } = "default";

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
        GeneralConfig.SetArray("RSBot.Profiles", _profiles);
        GeneralConfig.Save();
    }

    /// <summary>
    ///     Set selected profile
    /// </summary>
    /// <param name="profile">The profile</param>
    public static bool SetSelectedProfile(string profile)
    {
        var normalized = profile.ToLowerInvariant();
        if (!_profiles.Any(p => p == normalized))
            return false;

        SelectedProfile = normalized;
        Config.LoadProfile(normalized);

        return true;
    }

    /// <summary>
    ///     Is profile exists <c>true</c>; otherwise <c>false</c>
    /// </summary>
    /// <param name="profile">The profile</param>
    public static bool ProfileExists(string profile)
    {
        return _profiles.Any(p => p == profile.ToLowerInvariant());
    }

    /// <summary>
    ///     Create new profile
    /// </summary>
    /// <param name="profile">The profile</param>
    /// <param name="useAsBase">Use as base <c>true</c>; otherwise <c>false</c></param>
    /// <returns>Is created <c>true</c>; otherwise <c>false</c></returns>
    public static bool Add(string profile, bool useAsBase = false)
    {
        var normalized = profile.ToLowerInvariant();
        string[] reservedNames = { "settings", "logs" };

        if (reservedNames.Contains(normalized))
            return false;

        if (ProfileExists(normalized))
        {
            SetSelectedProfile(normalized);
            return true;
        }

        _profiles.Add(normalized);

        if (useAsBase)
            MigrationManager.CopyProfileData(SelectedProfile, profile);

        var newProfileDirectory = GetProfileDirectory(profile);
        if (!Directory.Exists(newProfileDirectory))
            Directory.CreateDirectory(newProfileDirectory);

        SetSelectedProfile(normalized);

        return true;
    }

    /// <summary>
    ///     Remove the profile
    /// </summary>
    /// <param name="profile">The profile</param>
    /// <returns>Is removed <c>true</c>; otherwise <c>false</c></returns>
    public static bool Remove(string profile)
    {
        return _profiles.Remove(profile.ToLowerInvariant());
    }

    public static string GetProfileFile(string profileName)
    {
        return Path.Combine(Kernel.BasePath, "User", $"{profileName}.json");
    }

    public static string GetProfileDirectory(string profileName)
    {
        return Path.Combine(Kernel.BasePath, "User", profileName);
    }
}
