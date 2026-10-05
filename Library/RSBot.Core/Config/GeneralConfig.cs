using System;
using System.Collections.Generic;

namespace RSBot.Core;

public static class GeneralConfig
{
    /// <summary>
    /// Checks if the specified key exists in the settings config.
    /// </summary>
    public static bool Exists(string key) => Config.Settings != null && Config.Settings.Exists(key);

    /// <summary>
    /// Gets a value from the settings config.
    /// </summary>
    public static T Get<T>(string key, T defaultValue = default) =>
        Config.Settings != null ? Config.Settings.Get(key, defaultValue) : defaultValue;

    /// <summary>
    /// Gets an enum value from the settings config.
    /// </summary>
    public static TEnum GetEnum<TEnum>(string key, TEnum defaultValue = default)
        where TEnum : struct => Config.Settings != null ? Config.Settings.GetEnum(key, defaultValue) : defaultValue;

    /// <summary>
    /// Sets a value in the settings config.
    /// </summary>
    public static void Set<T>(string key, T value) => Config.Settings?.Set(key, value);

    /// <summary>
    /// Gets an array from the settings config.
    /// </summary>
    public static T[] GetArray<T>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    ) => Config.Settings != null ? Config.Settings.GetArray<T>(key, delimiter, options) : Array.Empty<T>();

    /// <summary>
    /// Gets enums array from the settings config.
    /// </summary>
    public static TEnum[] GetEnums<TEnum>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    )
        where TEnum : struct =>
        Config.Settings != null ? Config.Settings.GetEnums<TEnum>(key, delimiter, options) : Array.Empty<TEnum>();

    /// <summary>
    /// Sets an array in the settings config.
    /// </summary>
    public static void SetArray<T>(string key, IEnumerable<T> values, string delimiter = ",") =>
        Config.Settings?.SetArray(key, values, delimiter);

    /// <summary>
    /// Removes a key from the settings config.
    /// </summary>
    public static void Remove(string key) => Config.Settings?.Remove(key);

    /// <summary>
    /// Reloads the settings config.
    /// </summary>
    public static void Load() => Config.Settings?.Load();

    /// <summary>
    /// Saves the settings config.
    /// </summary>
    public static void Save() => Config.Settings?.Save();
}
