using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RSBot.Core;

public class Config<T> where T : class, new()
{
    /// <summary>
    ///     The object that stores the configuration
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _config;

    /// <summary>
    ///     Gets the path.
    /// </summary>
    private readonly string _path;

    public T Data { get; set; } = new();

    /// <summary>
    ///     The json options for serialization and deserialization
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    
    public Config(string file)
    {
        _path = file;

        CheckPath();
    }

    /// <summary>
    ///     Loads the specified file.
    /// </summary>
    /// <param name="file">The file.</param>
    public void Load()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        if (!File.Exists(_path))
        {
            Data = new T();
            Save();
            return;
        }
        try
        {
            var json = File.ReadAllText(_path);
            Data = JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }
        catch (Exception ex)
        {
            Log.Debug($"[Config] Error loading config from '{_path}': {ex.Message}");
            Data = new T();
        }
    }

    /// <summary>
    ///     Saves the specified file.
    /// </summary>
    /// <param name="file">The file.</param>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(Data, _jsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Log.Debug($"[Config] Error saving config to '{_path}': {ex.Message}");
        }
    }

    /// <summary>
    ///     Check directories
    /// </summary>
    private void CheckPath()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(_path))
            File.Create(_path).Dispose();
    }

}
