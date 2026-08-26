using System;
using System.Collections.Generic;

namespace RSBot.Core;

public static class GlobalConfig
{
    /// <summary>
    /// Checks if the specified key exists in the profile config.
    /// </summary>
    public static bool Exists(string key) => Config.Profile != null && Config.Profile.Exists(key);

    /// <summary>
    /// Gets a value from the profile config.
    /// </summary>
    public static T Get<T>(string key, T defaultValue = default) =>
        Config.Profile != null ? Config.Profile.Get(key, defaultValue) : defaultValue;

    /// <summary>
    /// Gets an enum value from the profile config.
    /// </summary>
    public static TEnum GetEnum<TEnum>(string key, TEnum defaultValue = default)
        where TEnum : struct => Config.Profile != null ? Config.Profile.GetEnum(key, defaultValue) : defaultValue;

    /// <summary>
    /// Sets a value in the profile config.
    /// </summary>
    public static void Set<T>(string key, T value) => Config.Profile?.Set(key, value);

    /// <summary>
    /// Gets an array from the profile config.
    /// </summary>
    public static T[] GetArray<T>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    ) => Config.Profile != null ? Config.Profile.GetArray<T>(key, delimiter, options) : Array.Empty<T>();

    /// <summary>
    /// Gets enums array from the profile config.
    /// </summary>
    public static TEnum[] GetEnums<TEnum>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    )
        where TEnum : struct =>
        Config.Profile != null ? Config.Profile.GetEnums<TEnum>(key, delimiter, options) : Array.Empty<TEnum>();

    /// <summary>
    /// Sets an array in the profile config.
    /// </summary>
    public static void SetArray<T>(string key, IEnumerable<T> values, string delimiter = ",") =>
        Config.Profile?.SetArray(key, values, delimiter);

    /// <summary>
    /// Removes a key from the profile config.
    /// </summary>
    public static void Remove(string key) => Config.Profile?.Remove(key);

    /// <summary>
    /// Reloads the profile config.
    /// </summary>
    public static void Load() => Config.Profile?.Load();

    /// <summary>
    /// Saves the profile config.
    /// </summary>
    public static void Save() => Config.Profile?.Save();
}
