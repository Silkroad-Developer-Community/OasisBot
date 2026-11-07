using System;
using System.IO;

namespace RSBot.Core.Plugins;

/// <summary>
///     Writes plugin and botbase boot errors next to the logs.
/// </summary>
internal static class BootErrorLog
{
    /// <summary>
    ///     Writes the error into <c>Data/Logs/boot-error.log</c>.
    ///     <para>
    ///         The directory is created first: the portable package does not ship an empty Data\Logs
    ///         folder, and a failing diagnostic write must never break the startup itself.
    ///     </para>
    /// </summary>
    public static void Write(string message, Exception error)
    {
        try
        {
            var directory = Path.Combine(Kernel.BasePath, "Data", "Logs");
            Directory.CreateDirectory(directory);

            File.WriteAllText(
                Path.Combine(directory, "boot-error.log"),
                $"{message}\n{error.Message} at {error.StackTrace}"
            );
        }
        catch (Exception writeError)
        {
            Log.Warn("Could not write the boot error log: " + writeError.Message);
        }
    }
}
