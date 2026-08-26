using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RSBot.Core.Components;

namespace RSBot.Core;

public static class MigrationManager
{
    private static Dictionary<string, string> ParseLegacyRsFile(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return dict;

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var splitIndex = line.IndexOf('{');
            if (splitIndex == -1)
                continue;

            var key = line.Substring(0, splitIndex);
            var rest = line.Substring(splitIndex + 1);
            var closingIndex = rest.LastIndexOf('}');
            if (closingIndex == -1)
                continue;

            var value = rest.Substring(0, closingIndex);

            if (!dict.ContainsKey(key))
                dict[key] = value;
        }

        return dict;
    }

    private static bool IsMigrationNeeded()
    {
        var userDirectory = Path.Combine(Kernel.BasePath, "User");
        if (!Directory.Exists(userDirectory))
            return false;

        // 1. Check legacy Settings/Profiles
        if (
            File.Exists(Path.Combine(userDirectory, "Settings.rs"))
            || File.Exists(Path.Combine(userDirectory, "Profiles.rs"))
        )
            return true;

        // 2. Check profile *.rs files directly under User/
        var files = Directory.GetFiles(userDirectory, "*.rs");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (
                !fileName.Equals("Settings", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals("Profiles", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        var directories = Directory.GetDirectories(userDirectory);

        // 3. Check autologin.data in profile directories
        foreach (var dir in directories)
        {
            var profileName = Path.GetFileName(dir);
            if (profileName.Equals("Logs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(Path.Combine(dir, "autologin.data")))
                return true;

            // 4. Check Character *.rs files inside profile subdirectories
            if (Directory.GetFiles(dir, "*.rs").Length > 0)
                return true;
        }

        // 5. Check NormalizeProfileCasing json files
        var jsonFiles = Directory
            .GetFiles(userDirectory, "*.json")
            .Where(f => !Path.GetFileNameWithoutExtension(f).Equals("settings", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var file in jsonFiles)
        {
            var oldName = Path.GetFileNameWithoutExtension(file);
            var newName = oldName.ToLowerInvariant();
            if (oldName != newName)
                return true;
        }

        // 6. Check NormalizeProfileCasing directories
        var profileDirs = Directory
            .GetDirectories(userDirectory)
            .Where(d => !Path.GetFileName(d).Equals("Logs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var dir in profileDirs)
        {
            var oldName = Path.GetFileName(dir);
            var newName = oldName.ToLowerInvariant();
            if (oldName != newName)
                return true;
        }

        return false;
    }

    private static void BackupUserDirectory()
    {
        try
        {
            var userDir = Path.Combine(Kernel.BasePath, "User");
            var backupDir = Path.Combine(Kernel.BasePath, "User_backup");

            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, true);
            }

            CopyDirectory(userDir, backupDir);
            Log.Notify("[MigrationManager] Created backup of User/ to User_backup/.");
        }
        catch (Exception ex)
        {
            Log.Warn($"[MigrationManager] Failed to create User/ backup: {ex.Message}");
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    /// <summary>
    ///     Migrates all legacy .rs and separate autologin files to unified JSON structure.
    /// </summary>
    public static bool MigrateLegacyConfigs()
    {
        var migrationPerformed = false;
        var userDirectory = Path.Combine(Kernel.BasePath, "User");
        if (!Directory.Exists(userDirectory))
            return false;

        if (IsMigrationNeeded())
        {
            BackupUserDirectory();
        }

        try
        {
            // 1. Migrate settings.rs to settings.json
            var legacySettingsPath = Path.Combine(userDirectory, "Settings.rs");
            var newSettingsPath = Path.Combine(userDirectory, "settings.json");

            // Also check for legacy Profiles.rs that we used previously
            var legacyProfilesPath = Path.Combine(userDirectory, "Profiles.rs");
            if (File.Exists(legacyProfilesPath))
            {
                migrationPerformed = true;
                var legacyProfiles = ParseLegacyRsFile(legacyProfilesPath);
                if (legacyProfiles.TryGetValue("RSBot.Profiles", out var profiles))
                {
                    GeneralConfig.Set(
                        "RSBot.Profiles",
                        profiles.Split('|').Select(p => p.ToLowerInvariant()).ToArray()
                    );
                }
                if (legacyProfiles.TryGetValue("RSBot.SelectedProfile", out var selectedProfile))
                {
                    ProfileManager.SelectedProfile = selectedProfile.ToLowerInvariant();
                }
                try
                {
                    File.Delete(legacyProfilesPath);
                }
                catch { }
            }

            if (File.Exists(legacySettingsPath))
            {
                migrationPerformed = true;
                var legacySettings = ParseLegacyRsFile(legacySettingsPath);
                foreach (var kvp in legacySettings)
                {
                    // Filter out legacy unused keys
                    if (
                        kvp.Key.Equals("RSBot.SelectedProfile", StringComparison.OrdinalIgnoreCase)
                        || kvp.Key.Equals("RSBot.ShowProfileDialog", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }

                    // For profiles, store it as array if it contains delimiters
                    if (kvp.Key.Equals("RSBot.Profiles", StringComparison.OrdinalIgnoreCase))
                    {
                        GeneralConfig.Set(kvp.Key, kvp.Value.Split('|').Select(p => p.ToLowerInvariant()).ToArray());
                    }
                    else
                    {
                        GeneralConfig.Set(kvp.Key, kvp.Value);
                    }
                }
                GeneralConfig.Save();
                try
                {
                    File.Delete(legacySettingsPath);
                }
                catch { }
                Log.Notify("[MigrationManager] Migrated Settings.rs to settings.json.");
            }

            // 2. Migrate Profile files (*.rs directly under User/)
            var files = Directory.GetFiles(userDirectory, "*.rs");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (
                    fileName.Equals("Settings", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("Profiles", StringComparison.OrdinalIgnoreCase)
                )
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                    continue;
                }

                migrationPerformed = true;

                // Parse legacy profile config
                var legacyProfileData = ParseLegacyRsFile(file);
                var newProfilePath = Path.Combine(userDirectory, $"{fileName}.json");
                var profileConfig = new ConfigContainer(newProfilePath);

                foreach (var kvp in legacyProfileData)
                {
                    // PR #934 Migration: "RSBot.Default" was moved to "RSBot.Training"
                    if (
                        kvp.Key.Equals("RSBot.BotName", StringComparison.OrdinalIgnoreCase)
                        && kvp.Value == "RSBot.Default"
                    )
                    {
                        profileConfig.Set(kvp.Key, "RSBot.Training");
                    }
                    else
                    {
                        profileConfig.Set(kvp.Key, kvp.Value);
                    }
                }

                // Merge Autologin data into profile's config
                var profileDirectory = Path.Combine(userDirectory, fileName);
                var legacyAutoLoginDataPath = Path.Combine(profileDirectory, "autologin.data");

                string autoLoginJsonContent = null;

                if (File.Exists(legacyAutoLoginDataPath))
                {
                    try
                    {
                        var buffer = File.ReadAllBytes(legacyAutoLoginDataPath);
                        if (buffer.Length > 0)
                        {
                            if (buffer[0] == '[' || buffer[0] == '{')
                            {
                                autoLoginJsonContent = System.Text.Encoding.UTF8.GetString(buffer);
                            }
                            else
                            {
                                var blowfish = new RSBot.Core.Network.Protocol.Blowfish();
                                var decoded = blowfish.Decode(buffer);
                                autoLoginJsonContent = System.Text.Encoding.UTF8.GetString(decoded).Trim('\0');
                            }
                        }
                        File.Delete(legacyAutoLoginDataPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(
                            $"[MigrationManager] Failed to read legacy autologin.data for profile {fileName}: {ex.Message}"
                        );
                    }
                }

                if (!string.IsNullOrEmpty(autoLoginJsonContent))
                {
                    try
                    {
                        var accountsElement = JsonSerializer.Deserialize<JsonElement>(autoLoginJsonContent);
                        profileConfig.Set("Accounts", accountsElement);
                        Log.Notify($"[MigrationManager] Merged accounts into {fileName}.json config.");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(
                            $"[MigrationManager] Failed to parse and merge autologin accounts JSON for profile {fileName}: {ex.Message}"
                        );
                    }
                }

                profileConfig.Save();
                try
                {
                    File.Delete(file);
                }
                catch { }
                Log.Notify($"[MigrationManager] Migrated profile {fileName}.rs to {fileName}.json.");
            }

            // 3. Migrate Autologin data (.data) for all existing profile folders
            var directories = Directory.GetDirectories(userDirectory);
            foreach (var dir in directories)
            {
                var profileName = Path.GetFileName(dir);
                if (profileName.Equals("Logs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var legacyAutoLoginDataPath = Path.Combine(dir, "autologin.data");

                if (File.Exists(legacyAutoLoginDataPath))
                {
                    var profileJsonPath = Path.Combine(userDirectory, $"{profileName}.json");
                    if (File.Exists(profileJsonPath))
                    {
                        var profileConfig = new ConfigContainer(profileJsonPath);
                        if (!profileConfig.Exists("Accounts"))
                        {
                            string autoLoginJsonContent = null;
                            try
                            {
                                var buffer = File.ReadAllBytes(legacyAutoLoginDataPath);
                                if (buffer.Length > 0)
                                {
                                    if (buffer[0] == '[' || buffer[0] == '{')
                                    {
                                        autoLoginJsonContent = System.Text.Encoding.UTF8.GetString(buffer);
                                    }
                                    else
                                    {
                                        var blowfish = new RSBot.Core.Network.Protocol.Blowfish();
                                        var decoded = blowfish.Decode(buffer);
                                        autoLoginJsonContent = System.Text.Encoding.UTF8.GetString(decoded).Trim('\0');
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warn(
                                    $"[MigrationManager] Failed to read legacy autologin.data for profile {profileName}: {ex.Message}"
                                );
                            }

                            if (!string.IsNullOrEmpty(autoLoginJsonContent))
                            {
                                try
                                {
                                    var accountsElement = JsonSerializer.Deserialize<JsonElement>(autoLoginJsonContent);
                                    profileConfig.Set("Accounts", accountsElement);
                                    profileConfig.Save();
                                    migrationPerformed = true;
                                    Log.Notify($"[MigrationManager] Merged accounts into {profileName}.json config.");
                                }
                                catch (Exception ex)
                                {
                                    Log.Warn(
                                        $"[MigrationManager] Failed to parse and merge autologin accounts JSON for profile {profileName}: {ex.Message}"
                                    );
                                }
                            }
                        }

                        try
                        {
                            File.Delete(legacyAutoLoginDataPath);
                        }
                        catch { }
                    }
                }
            }

            // 4. Migrate Character files (*.rs inside profile subdirectories)
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Equals("Logs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var charFiles = Directory.GetFiles(dir, "*.rs");
                foreach (var charFile in charFiles)
                {
                    migrationPerformed = true;
                    var charName = Path.GetFileNameWithoutExtension(charFile);
                    var legacyCharData = ParseLegacyRsFile(charFile);
                    var newCharPath = Path.Combine(dir, $"{charName}.json");
                    var charConfig = new ConfigContainer(newCharPath);

                    foreach (var kvp in legacyCharData)
                    {
                        charConfig.Set(kvp.Key, kvp.Value);
                    }

                    charConfig.Save();
                    try
                    {
                        File.Delete(charFile);
                    }
                    catch { }
                    Log.Notify(
                        $"[MigrationManager] Migrated character config {charName}.rs to {charName}.json in profile {dirName}."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[MigrationManager] Error occurred during legacy configurations migration: {ex.Message}");
        }

        if (NormalizeProfileCasing())
            migrationPerformed = true;

        return migrationPerformed;
    }

    /// <summary>
    ///     Renames profile directories and JSON files to lowercase.
    ///     Resolves clashes by appending a numeric suffix.
    /// </summary>
    private static bool NormalizeProfileCasing()
    {
        var migrated = false;
        var userDirectory = Path.Combine(Kernel.BasePath, "User");
        if (!Directory.Exists(userDirectory))
            return false;

        string[] reserved = { "settings", "logs" };
        var takenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect already-lowercase names to detect clashes
        foreach (var file in Directory.GetFiles(userDirectory, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            takenNames.Add(name);
        }

        try
        {
            // 1. Normalize profile JSON files
            var jsonFiles = Directory
                .GetFiles(userDirectory, "*.json")
                .Where(f => !Path.GetFileNameWithoutExtension(f).Equals("settings", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var renamedProfiles = new Dictionary<string, string>(); // old name -> new name

            foreach (var file in jsonFiles)
            {
                var oldName = Path.GetFileNameWithoutExtension(file);
                var newName = oldName.ToLowerInvariant();

                if (oldName == newName)
                    continue;

                // Resolve clash
                var candidate = newName;
                var counter = 1;
                while (takenNames.Contains(candidate) && !candidate.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = $"{newName}{counter}";
                    counter++;
                }

                if (reserved.Contains(candidate))
                    continue;

                takenNames.Remove(oldName);
                takenNames.Add(candidate);
                renamedProfiles[oldName] = candidate;

                var dest = Path.Combine(userDirectory, $"{candidate}.json");
                File.Move(file, dest, true);
                migrated = true;
                Log.Notify($"[MigrationManager] Renamed profile {oldName}.json -> {candidate}.json");
            }

            // 2. Normalize profile directories
            var directories = Directory
                .GetDirectories(userDirectory)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !name.Equals("Logs", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            foreach (var dir in directories)
            {
                var oldName = Path.GetFileName(dir);
                // Use the same resolved name if we already renamed the json
                var newName = renamedProfiles.TryGetValue(oldName, out var resolved)
                    ? resolved
                    : oldName.ToLowerInvariant();

                if (oldName == newName)
                    continue;

                var dest = Path.Combine(userDirectory, newName);
                if (Directory.Exists(dest) && !dest.Equals(dir, StringComparison.OrdinalIgnoreCase))
                {
                    // Merge: move files into existing directory
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        var destFile = Path.Combine(dest, Path.GetFileName(file));
                        File.Move(file, destFile, true);
                    }
                    Directory.Delete(dir, true);
                }
                else
                {
                    Directory.Move(dir, dest);
                }

                migrated = true;
                Log.Notify($"[MigrationManager] Renamed profile directory {oldName} -> {newName}");
            }

            // 3. Update profiles array in settings
            if (migrated)
            {
                var profiles = GeneralConfig.GetArray<string>("RSBot.Profiles");
                var normalized = profiles
                    .Select(p =>
                    {
                        if (renamedProfiles.TryGetValue(p, out var n))
                            return n;
                        return p.ToLowerInvariant();
                    })
                    .Distinct()
                    .ToArray();

                GeneralConfig.SetArray("RSBot.Profiles", normalized);
                GeneralConfig.Save();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[MigrationManager] Error normalizing profile casing: {ex.Message}");
        }

        return migrated;
    }

    /// <summary>
    ///     Copies the old profile data to the new profile.
    /// </summary>
    public static void CopyProfileData(string sourceProfile, string targetProfile)
    {
        try
        {
            var oldProfileFilePath = ProfileManager.GetProfileFile(sourceProfile);
            var newProfileFilePath = ProfileManager.GetProfileFile(targetProfile);
            var oldProfileDir = ProfileManager.GetProfileDirectory(sourceProfile);
            var newProfileDir = ProfileManager.GetProfileDirectory(targetProfile);

            if (File.Exists(oldProfileFilePath))
            {
                File.Copy(oldProfileFilePath, newProfileFilePath, true);
            }

            if (Directory.Exists(oldProfileDir))
            {
                if (!Directory.Exists(newProfileDir))
                {
                    Directory.CreateDirectory(newProfileDir);
                }

                // Copy all character JSON files
                var files = Directory.GetFiles(oldProfileDir, "*.json");
                foreach (var file in files)
                {
                    var destFile = Path.Combine(newProfileDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(
                $"[MigrationManager] Could not copy profile data from {sourceProfile} to {targetProfile}: {ex.Message}"
            );
        }
    }
}
