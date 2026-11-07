using System;
using System.Linq;
using System.Reflection;

namespace RSBot.Core.Plugins;

/// <summary>
///     Loads plugin and botbase assemblies from disk.
/// </summary>
internal static class AssemblyLoader
{
    /// <summary>
    ///     Loads the assembly from the given file and reuses an already loaded assembly with the same name.
    ///     <para>
    ///         The portable package ships some assemblies twice (for example RSBot.Training.dll in both
    ///         Data\Plugins and Data\Bots). Loading the second copy from another path throws
    ///         "Assembly with same name is already loaded", which used to abort the whole loading pass and
    ///         the start of the bot.
    ///     </para>
    /// </summary>
    public static Assembly Load(string file)
    {
        var name = AssemblyName.GetAssemblyName(file);

        var loaded = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase)
            );

        return loaded ?? Assembly.LoadFrom(file);
    }
}
