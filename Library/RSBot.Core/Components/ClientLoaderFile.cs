using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RSBot.Core.Components;

internal static class ClientLoaderFile
{
    internal static void Ensure(string source, string destination)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(destination.ToUpperInvariant())));
        using var mutex = new Mutex(false, @"Local\RSBot.ClientLoader." + key);
        var held = false;
        try
        {
            try
            {
                held = mutex.WaitOne(TimeSpan.FromSeconds(15));
            }
            catch (AbandonedMutexException)
            {
                held = true;
            }
            if (!held)
                throw new IOException("Another bot is still preparing dsound.dll.");
            if (Matches(source, destination))
                return;

            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary, false);
                // Same-directory rename prevents a concurrent client from reading a partial DLL.
                File.Move(temporary, destination, true);
                if (!Matches(source, destination))
                    throw new IOException("dsound.dll verification failed.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (!Matches(source, destination))
                    throw new IOException(
                        "Could not install the required dsound.dll. An older client may be using a different version.",
                        error
                    );
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            if (held)
                mutex.ReleaseMutex();
        }
    }

    private static bool Matches(string source, string destination)
    {
        if (!File.Exists(destination))
            return false;
        using var expected = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var actual = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(expected), SHA256.HashData(actual));
    }
}
