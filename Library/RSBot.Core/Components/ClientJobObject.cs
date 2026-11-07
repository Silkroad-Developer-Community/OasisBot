using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static RSBot.Core.Extensions.NativeExtensions;

namespace RSBot.Core.Components;

/// <summary>
///     Owns a Windows job object with <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c> so the game client is
///     terminated by the operating system when the bot process ends, no matter how it ends.
/// </summary>
public static class ClientJobObject
{
    private static readonly object Gate = new();

    private static IntPtr _jobHandle = IntPtr.Zero;
    private static uint _assignedProcessId;

    /// <summary>
    ///     Assigns the given process to the bot's job object. Every process previously assigned to the
    ///     job stays there, so a relaunched client is always covered.
    /// </summary>
    /// <returns><c>true</c> when the process is registered in the job; otherwise <c>false</c>.</returns>
    public static bool Attach(Process process, IntPtr processHandle)
    {
        if (process == null)
            return false;

        lock (Gate)
        {
            if (_jobHandle == IntPtr.Zero && !TryCreateJob())
                return false;

            try
            {
                var handle = processHandle != IntPtr.Zero ? processHandle : process.Handle;

                if (!AssignProcessToJobObject(_jobHandle, handle))
                {
                    Log.Warn(
                        $"Could not assign client process {process.Id} to the job object "
                            + $"(Win32 error {Marshal.GetLastWin32Error()}). Falling back to explicit termination."
                    );
                    return false;
                }

                _assignedProcessId = (uint)process.Id;
                Log.Debug($"Client process {process.Id} is now guarded by the kill-on-close job object.");

                return true;
            }
            catch (Exception error)
            {
                Log.Warn($"Could not assign client process to the job object: {error.Message}");
                return false;
            }
        }
    }

    /// <summary>
    ///     PID of the process the client manager currently owns, or 0 when nothing is assigned.
    /// </summary>
    public static uint AssignedProcessId
    {
        get
        {
            lock (Gate)
                return _assignedProcessId;
        }
    }

    private static bool TryCreateJob()
    {
        var jobHandle = CreateJobObjectW(IntPtr.Zero, null);
        if (jobHandle == IntPtr.Zero)
        {
            Log.Warn($"Could not create job object (Win32 error {Marshal.GetLastWin32Error()}).");
            return false;
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var buffer = Marshal.AllocHGlobal(length);

        try
        {
            Marshal.StructureToPtr(info, buffer, false);

            if (!SetInformationJobObject(jobHandle, JobObjectExtendedLimitInformation, buffer, (uint)length))
            {
                Log.Warn(
                    $"Could not configure job object (Win32 error {Marshal.GetLastWin32Error()}). "
                        + "The client will not be killed automatically if the bot crashes."
                );
                CloseHandle(jobHandle);

                return false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        // The handle is intentionally kept for the whole bot lifetime and never closed:
        // closing it is exactly what makes Windows terminate every process in the job.
        _jobHandle = jobHandle;

        return true;
    }
}
