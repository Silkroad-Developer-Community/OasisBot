using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RSBot.Core.Event;

namespace RSBot.Core.Plugins;

public class BotbaseManager
{
    /// <summary>
    ///     Gets the extension directory.
    /// </summary>
    /// <value>
    ///     The extension directory.
    /// </value>
    public string DirectoryPath => Path.Combine(Kernel.BasePath, "Data", "Bots");

    /// <summary>
    ///     Gets the extensions.
    /// </summary>
    /// <value>
    ///     The extensions.
    /// </value>
    public Dictionary<string, IBotbase> Bots { get; private set; }
    public Dictionary<string, IBotbaseView> BotsViews { get; private set; }

    /// <summary>
    ///     Loads the assemblies.
    /// </summary>
    public bool LoadAssemblies(bool isHeadless = false)
    {
        if (Bots != null)
            return false;

        try
        {
            Bots = new Dictionary<string, IBotbase>();
            BotsViews = new Dictionary<string, IBotbaseView>();

            foreach (var file in Directory.GetFiles(DirectoryPath, "*.dll"))
            {
                (Dictionary<string, IBotbase> bots, Dictionary<string, IBotbaseView> views) result;

                try
                {
                    result = GetExtensionsFromAssembly(file);
                }
                catch (Exception error)
                {
                    Log.Warn($"Could not load the botbase assembly {Path.GetFileName(file)}: {error.Message}");

                    continue;
                }

                foreach (var bot in result.bots)
                {
                    Bots[bot.Key] = bot.Value;
                    bot.Value.Register();
                }

                if (!isHeadless)
                {
                    foreach (var view in result.views)
                    {
                        BotsViews[view.Key] = view.Value;
                        Log.Debug($"Loaded botbase [{view.Value.Name}]");
                    }
                }
            }

            EventManager.FireEvent("OnLoadBotbases");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("The botbase manager encountered a problem: " + ex.Message);

            BootErrorLog.Write("The botbase manager encountered a problem:", ex);

            return false;
        }
    }

    /// <summary>
    ///     Gets the extensions from assembly.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns></returns>
    private static (
        Dictionary<string, IBotbase> bots,
        Dictionary<string, IBotbaseView> views
    ) GetExtensionsFromAssembly(string file)
    {
        var bots = new Dictionary<string, IBotbase>();
        var views = new Dictionary<string, IBotbaseView>();

        var assembly = AssemblyLoader.Load(file);

        try
        {
            var types = assembly.GetTypes();

            foreach (var type in types.Where(t => t.IsPublic && !t.IsAbstract))
            {
                object instance = null;

                if (typeof(IBotbase).IsAssignableFrom(type))
                {
                    instance = Activator.CreateInstance(type);
                    var bot = (IBotbase)instance;
                    bots[bot.Name] = bot;
                }

                if (typeof(IBotbaseView).IsAssignableFrom(type))
                {
                    instance ??= Activator.CreateInstance(type);
                    var view = (IBotbaseView)instance;
                    views[view.Name] = view;
                }
            }
        }
        catch
        {
            /* ignore, it's an invalid botbase */
        }

        return (bots, views);
    }
}
