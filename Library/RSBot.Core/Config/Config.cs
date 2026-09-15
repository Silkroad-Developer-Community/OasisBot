using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RSBot.Core;

public class ConfigContainer
{
    private readonly string _path;
    private ConcurrentDictionary<string, JsonElement> _data;

    public ConfigContainer(string path)
    {
        _path = path;
        Load();
    }

    public void Load()
    {
        _data = new ConcurrentDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_path))
        {
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        _data[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[ConfigContainer] Failed to load {_path}: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_data, options);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Log.Warn($"[ConfigContainer] Failed to save {_path}: {ex.Message}");
        }
    }

    public bool Exists(string key)
    {
        return _data.ContainsKey(key);
    }

    public T Get<T>(string key, T defaultValue = default)
    {
        if (!_data.TryGetValue(key, out var element))
        {
            Set(key, defaultValue);
            return defaultValue;
        }

        try
        {
            // If T is not string, but element is string, try to parse it for legacy compatibility
            if (typeof(T) != typeof(string) && element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString();
                if (typeof(T) == typeof(bool))
                {
                    if (bool.TryParse(str, out var b))
                        return (T)(object)b;
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(str, out var i))
                        return (T)(object)i;
                }
                else if (typeof(T) == typeof(uint))
                {
                    if (uint.TryParse(str, out var ui))
                        return (T)(object)ui;
                }
                else if (typeof(T) == typeof(double))
                {
                    if (
                        double.TryParse(
                            str,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var d
                        )
                    )
                        return (T)(object)d;
                }
                else if (typeof(T) == typeof(float))
                {
                    if (
                        float.TryParse(
                            str,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var f
                        )
                    )
                        return (T)(object)f;
                }

                try
                {
                    return (T)Convert.ChangeType(str, typeof(T));
                }
                catch { }
            }

            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return defaultValue;
        }
    }

    public TEnum GetEnum<TEnum>(string key, TEnum defaultValue = default)
        where TEnum : struct
    {
        if (!_data.TryGetValue(key, out var element))
        {
            Set(key, defaultValue);
            return defaultValue;
        }

        try
        {
            var rawText = element.GetRawText();
            if (Enum.TryParse<TEnum>(rawText.Trim('"'), out var parsedEnum))
            {
                return parsedEnum;
            }
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        var json = JsonSerializer.SerializeToElement(value);
        _data[key] = json;
    }

    public T[] GetArray<T>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    )
    {
        if (!_data.TryGetValue(key, out var element))
            return Array.Empty<T>();

        if (element.ValueKind == JsonValueKind.Array)
        {
            try
            {
                return JsonSerializer.Deserialize<T[]>(element.GetRawText()) ?? Array.Empty<T>();
            }
            catch
            {
                return Array.Empty<T>();
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (string.IsNullOrEmpty(str))
                return Array.Empty<T>();

            var parts = str.Split(new[] { delimiter }, options);
            var result = new T[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                try
                {
                    result[i] = (T)Convert.ChangeType(parts[i], typeof(T));
                }
                catch
                {
                    result[i] = default;
                }
            }
            return result;
        }

        return Array.Empty<T>();
    }

    public TEnum[] GetEnums<TEnum>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    )
        where TEnum : struct
    {
        if (!_data.TryGetValue(key, out var element))
            return Array.Empty<TEnum>();

        if (element.ValueKind == JsonValueKind.Array)
        {
            try
            {
                return JsonSerializer.Deserialize<TEnum[]>(element.GetRawText()) ?? Array.Empty<TEnum>();
            }
            catch
            {
                return Array.Empty<TEnum>();
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (string.IsNullOrEmpty(str))
                return Array.Empty<TEnum>();

            var parts = str.Split(new[] { delimiter }, options);
            var result = new List<TEnum>();
            foreach (var part in parts)
            {
                if (Enum.TryParse<TEnum>(part, out var val))
                {
                    result.Add(val);
                }
            }
            return result.ToArray();
        }

        return Array.Empty<TEnum>();
    }

    public void SetArray<T>(string key, IEnumerable<T> values, string delimiter = ",")
    {
        Set(key, values);
    }

    public void Remove(string key)
    {
        _data.TryRemove(key, out _);
    }
}

public static class Config
{
    public static ConfigContainer Settings { get; private set; }
    public static ConfigContainer Profile { get; private set; }
    public static ConfigContainer Player { get; private set; }
    public static bool MigrationTriggered { get; private set; }

    public static void Initialize()
    {
        var settingsPath = Path.Combine(Kernel.BasePath, "User", "settings.json");
        Settings = new ConfigContainer(settingsPath);

        // Run migrations
        MigrationTriggered = MigrationManager.MigrateLegacyConfigs();
    }

    public static void LoadProfile(string profileName)
    {
        var profilePath = Path.Combine(Kernel.BasePath, "User", $"{profileName}.json");
        Profile = new ConfigContainer(profilePath);
    }

    public static void LoadPlayer(string characterName)
    {
        var playerPath = Path.Combine(
            Kernel.BasePath,
            "User",
            Components.ProfileManager.SelectedProfile,
            $"{characterName}.json"
        );
        Player = new ConfigContainer(playerPath);
    }
}
