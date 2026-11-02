using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace RSBot.Core.Components;

/// <summary>Disables only the two verified VTC SDK wrappers; the companion owns the SDK.</summary>
internal static class VtcXigncodeDisablePatch
{
    private static readonly byte[] EnterStub = { 0xB8, 1, 0, 0, 0, 0xC2, 0x0C, 0 };
    private static readonly byte[] CommandStub = { 0xB8, 1, 0, 0, 0, 0xC3 };

    private static (uint Rva, byte[] Original, byte[] Mask, byte[] Replacement)[] Targets(VtcClientLayout layout) =>
        new[]
        {
            (layout.SysEnterRva, layout.SysEnterBytes, layout.SysEnterMask, EnterStub),
            (layout.SendCommandRva, layout.SendCommandBytes, layout.SendCommandMask, CommandStub),
        };

    internal static bool IsReady(IntPtr process, IntPtr moduleBase, VtcClientLayout layout)
    {
        return layout != null
            && Targets(layout)
                .All(target =>
                    Read(process, Address(moduleBase, target.Rva), target.Original.Length, out var bytes)
                    && (
                        VtcClientLayout.Matches(bytes, target.Original, target.Mask)
                        || bytes.SequenceEqual(target.Replacement)
                    )
                );
    }

    internal static bool HasGameSdk(Process process)
    {
        process.Refresh();
        return process
            .Modules.Cast<ProcessModule>()
            .Any(module =>
                string.Equals(module.ModuleName, "x3.xem", StringComparison.OrdinalIgnoreCase)
                || string.Equals(module.ModuleName, "xcorona.xem", StringComparison.OrdinalIgnoreCase)
            );
    }

    internal static bool TryInstall(Process process, IntPtr handle, VtcClientLayout layout, out string error)
    {
        try
        {
            if (HasGameSdk(process))
            {
                error =
                    "VTC: the game's XIGNCODE loaded before its startup patch. The new game process will be stopped.";
                return false;
            }
            if (!TryInstall(handle, process.MainModule.BaseAddress, layout, out error))
                return false;
            if (!HasGameSdk(process))
                return true;
            error = "VTC: unexpected XIGNCODE module in the game process after patching.";
            return false;
        }
        catch (Exception exception)
        {
            error = "VTC SDK startup patch: " + exception.Message;
            return false;
        }
    }

    internal static bool TryInstall(IntPtr process, IntPtr moduleBase, VtcClientLayout layout, out string error)
    {
        error = null;
        if (!IsReady(process, moduleBase, layout))
        {
            error = "VTC: unsupported SDK wrapper bytes; no SDK patch was applied.";
            return false;
        }
        try
        {
            foreach (var target in Targets(layout))
            {
                var address = Address(moduleBase, target.Rva);
                if (!VirtualProtectEx(process, address, (UIntPtr)target.Replacement.Length, 0x40, out var previous))
                    throw new Win32Exception();
                try
                {
                    if (
                        !WriteProcessMemory(
                            process,
                            address,
                            target.Replacement,
                            (UIntPtr)target.Replacement.Length,
                            out var written
                        )
                        || written.ToUInt64() != (ulong)target.Replacement.Length
                    )
                        throw new Win32Exception();
                    if (
                        !Read(process, address, target.Replacement.Length, out var bytes)
                        || !bytes.SequenceEqual(target.Replacement)
                    )
                        throw new InvalidOperationException("SDK wrapper verification failed.");
                    if (!FlushInstructionCache(process, address, (UIntPtr)target.Replacement.Length))
                        throw new Win32Exception();
                }
                finally
                {
                    if (!VirtualProtectEx(process, address, (UIntPtr)target.Replacement.Length, previous, out _))
                        throw new Win32Exception();
                }
            }
            return true;
        }
        catch (Exception exception)
        {
            error = "VTC SDK startup patch: " + exception.Message;
            return false;
        }
    }

    private static IntPtr Address(IntPtr moduleBase, uint rva) =>
        new(unchecked((int)VtcClientLayout.Address(moduleBase, rva)));

    private static bool Read(IntPtr process, IntPtr address, int size, out byte[] bytes)
    {
        bytes = new byte[size];
        return ReadProcessMemory(process, address, bytes, (UIntPtr)size, out var count)
            && count.ToUInt64() == (ulong)size;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        byte[] bytes,
        UIntPtr size,
        out UIntPtr read
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr process,
        IntPtr address,
        byte[] bytes,
        UIntPtr size,
        out UIntPtr written
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtectEx(
        IntPtr process,
        IntPtr address,
        UIntPtr size,
        uint protection,
        out uint previous
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
}
