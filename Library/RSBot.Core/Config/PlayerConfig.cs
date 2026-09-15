using System;
using System.Collections.Generic;

namespace RSBot.Core;

public static class PlayerConfig
{
    /// <summary>
    /// Checks if the specified key exists in the player config.
    /// </summary>
    public static bool Exists(string key) => Config.Player != null && Config.Player.Exists(key);

    /// <summary>
    /// Gets a value from the player config.
    /// </summary>
    public static T Get<T>(string key, T defaultValue = default) =>
        Config.Player != null ? Config.Player.Get(key, defaultValue) : defaultValue;

    /// <summary>
    /// Gets an enum value from the player config.
    /// </summary>
    public static TEnum GetEnum<TEnum>(string key, TEnum defaultValue = default)
        where TEnum : struct => Config.Player != null ? Config.Player.GetEnum(key, defaultValue) : defaultValue;

    /// <summary>
    /// Sets a value in the player config.
    /// </summary>
    public static void Set<T>(string key, T value) => Config.Player?.Set(key, value);

    /// <summary>
    /// Gets an array from the player config.
    /// </summary>
    public static T[] GetArray<T>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    ) => Config.Player != null ? Config.Player.GetArray<T>(key, delimiter, options) : Array.Empty<T>();

    /// <summary>
    /// Gets enums array from the player config.
    /// </summary>
    public static TEnum[] GetEnums<TEnum>(
        string key,
        char delimiter = ',',
        StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries
    )
        where TEnum : struct =>
        Config.Player != null ? Config.Player.GetEnums<TEnum>(key, delimiter, options) : Array.Empty<TEnum>();

    /// <summary>
    /// Sets an array in the player config.
    /// </summary>
    public static void SetArray<T>(string key, IEnumerable<T> values, string delimiter = ",") =>
        Config.Player?.SetArray(key, values, delimiter);

    /// <summary>
    /// Removes a key from the player config.
    /// </summary>
    public static void Remove(string key) => Config.Player?.Remove(key);

    /// <summary>
    /// Reloads the player config.
    /// </summary>
    public static void Load() => Config.Player?.Load();

    /// <summary>
    /// Saves the player config.
    /// </summary>
    public static void Save() => Config.Player?.Save();
}
