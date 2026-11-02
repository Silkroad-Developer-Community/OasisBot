using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace RSBot.Core.Components;

/// <summary>
/// Redirects the discovered gateway connect call without replacing XIGNCODE entry points.
/// The wrapper tail-calls the current connect import, including an SDK-provided implementation.
/// </summary>
internal static class VtcGatewayRedirectPatch
{
    private const uint CommitReserve = 0x3000;
    private const uint Release = 0x8000;
    private const uint ReadWrite = 0x04;
    private const uint ExecuteRead = 0x20;
    private const uint ExecuteReadWrite = 0x40;

    internal static bool IsReady(IntPtr process, IntPtr moduleBase, VtcClientLayout layout)
    {
        if (layout == null || !layout.HasGateway)
            return false;
        uint importAddress = VtcClientLayout.Address(moduleBase, layout.ConnectImportRva);
        return ReadExact(
                process,
                Pointer(VtcClientLayout.Address(moduleBase, layout.ConnectCallRva)),
                6,
                out var current
            ) && current.SequenceEqual(ExpectedCall(importAddress));
    }

    // The caller keeps the primary thread suspended until this method has completed.
    internal static bool TryInstall(
        Process process,
        IntPtr processHandle,
        VtcClientLayout layout,
        IEnumerable<string> gatewayHosts,
        ushort gatewayPort,
        ushort proxyPort,
        out string error
    )
    {
        error = null;
        IntPtr remoteCode = IntPtr.Zero;
        bool installed = false;
        try
        {
            if (layout == null || !layout.HasGateway)
                throw new InvalidOperationException("Gateway connect discovery is missing or ambiguous.");
            uint callAddress = VtcClientLayout.Address(process.MainModule.BaseAddress, layout.ConnectCallRva);
            uint importAddress = VtcClientLayout.Address(process.MainModule.BaseAddress, layout.ConnectImportRva);
            byte[] expected = ExpectedCall(importAddress);
            if (
                !ReadExact(processHandle, Pointer(callAddress), expected.Length, out var actual)
                || !actual.SequenceEqual(expected)
            )
            {
                error = "Unsupported VTC client: the verified gateway connect instruction was not found.";
                return false;
            }

            if (
                !ReadExact(processHandle, Pointer(importAddress), 4, out var import)
                || BitConverter.ToUInt32(import, 0) == 0
            )
            {
                error = "VTC connect import is not initialized.";
                return false;
            }

            var addresses = new HashSet<uint>();
            foreach (string host in gatewayHosts)
            {
                var resolved = IPAddress.TryParse(host, out var address)
                    ? new[] { address }
                    : Dns.GetHostAddresses(host);
                foreach (var candidate in resolved.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork))
                    addresses.Add(BitConverter.ToUInt32(candidate.GetAddressBytes(), 0));
            }

            byte[] code = BuildRedirector(importAddress, gatewayPort, proxyPort, addresses.ToArray());
            remoteCode = VirtualAllocEx(processHandle, IntPtr.Zero, (UIntPtr)code.Length, CommitReserve, ReadWrite);
            if (remoteCode == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (
                !WriteExact(processHandle, remoteCode, code)
                || !VirtualProtectEx(processHandle, remoteCode, (UIntPtr)code.Length, ExecuteRead, out _)
                || !FlushInstructionCache(processHandle, remoteCode, (UIntPtr)code.Length)
            )
                throw new Win32Exception(Marshal.GetLastWin32Error());

            byte[] call = BuildCall(callAddress, unchecked((uint)remoteCode.ToInt32()));
            if (
                !VirtualProtectEx(
                    processHandle,
                    Pointer(callAddress),
                    (UIntPtr)call.Length,
                    ExecuteReadWrite,
                    out uint protection
                )
            )
                throw new Win32Exception(Marshal.GetLastWin32Error());

            bool written =
                WriteExact(processHandle, Pointer(callAddress), call)
                && FlushInstructionCache(processHandle, Pointer(callAddress), (UIntPtr)call.Length);
            bool restored = VirtualProtectEx(
                processHandle,
                Pointer(callAddress),
                (UIntPtr)call.Length,
                protection,
                out _
            );
            if (!written || !restored)
            {
                // The caller terminates this newly created suspended process on failure.
                VirtualProtectEx(
                    processHandle,
                    Pointer(callAddress),
                    (UIntPtr)expected.Length,
                    ExecuteReadWrite,
                    out _
                );
                WriteExact(processHandle, Pointer(callAddress), expected);
                FlushInstructionCache(processHandle, Pointer(callAddress), (UIntPtr)expected.Length);
                VirtualProtectEx(processHandle, Pointer(callAddress), (UIntPtr)expected.Length, protection, out _);
                error = "Could not install the VTC gateway redirect.";
                return false;
            }

            installed = true;
            return true;
        }
        catch (Exception exception)
        {
            error = "VTC gateway redirect: " + exception.Message;
            return false;
        }
        finally
        {
            if (!installed && remoteCode != IntPtr.Zero)
                VirtualFreeEx(processHandle, remoteCode, UIntPtr.Zero, Release);
        }
    }

    internal static byte[] ExpectedCall(uint importAddress)
    {
        return new byte[] { 0xFF, 0x15 }
            .Concat(BitConverter.GetBytes(importAddress))
            .ToArray();
    }

    internal static byte[] BuildCall(uint callAddress, uint redirectAddress)
    {
        int displacement = unchecked((int)(redirectAddress - callAddress - 5));
        return new byte[] { 0xE8 }
            .Concat(BitConverter.GetBytes(displacement))
            .Concat(new byte[] { 0x90 })
            .ToArray();
    }

    internal static byte[] BuildRedirector(
        uint importAddress,
        ushort gatewayPort,
        ushort proxyPort,
        uint[] gatewayAddresses
    )
    {
        if (gatewayPort == 0 || proxyPort == 0 || gatewayAddresses == null || gatewayAddresses.Length == 0)
            throw new ArgumentException("A gateway address and both connection ports are required.");

        var code = new Code();
        code.Emit(0x8B, 0x44, 0x24, 0x08); // eax = sockaddr argument
        code.Emit(0x85, 0xC0); // test eax, eax
        code.Jump("tail", 0x0F, 0x84);
        code.Emit(0x83, 0x7C, 0x24, 0x0C, 0x10); // namelen >= sizeof(sockaddr_in)
        code.Jump("tail", 0x0F, 0x8C);
        code.Emit(0x66, 0x83, 0x38, 0x02); // AF_INET
        code.Jump("tail", 0x0F, 0x85);
        code.Emit(0x66, 0x81, 0x78, 0x02);
        code.Emit(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(unchecked((short)gatewayPort))));
        code.Jump("tail", 0x0F, 0x85);
        foreach (uint address in gatewayAddresses.Distinct())
        {
            code.Emit(0x81, 0x78, 0x04);
            code.Emit(BitConverter.GetBytes(address));
            code.Jump("redirect", 0x0F, 0x84);
        }
        code.Jump("tail", 0xE9);
        code.Label("redirect");
        code.Emit(0x66, 0xC7, 0x40, 0x02);
        code.Emit(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(unchecked((short)proxyPort))));
        code.Emit(0xC7, 0x40, 0x04);
        code.Emit(IPAddress.Loopback.GetAddressBytes());
        code.Label("tail");
        code.Emit(0xFF, 0x25); // jmp [current connect IAT]
        code.Emit(BitConverter.GetBytes(importAddress));
        return code.Finish();
    }

    private static IntPtr Pointer(uint address) => new IntPtr(unchecked((int)address));

    private static bool ReadExact(IntPtr process, IntPtr address, int length, out byte[] bytes)
    {
        bytes = new byte[length];
        return ReadProcessMemory(process, address, bytes, length, out var read) && read.ToInt64() == length;
    }

    private static bool WriteExact(IntPtr process, IntPtr address, byte[] bytes)
    {
        return WriteProcessMemory(process, address, bytes, (UIntPtr)bytes.Length, out var written)
            && written.ToUInt64() == (ulong)bytes.Length
            && ReadExact(process, address, bytes.Length, out var actual)
            && bytes.SequenceEqual(actual);
    }

    private sealed class Code
    {
        private readonly List<byte> _bytes = new();
        private readonly Dictionary<string, int> _labels = new();
        private readonly List<(int Offset, string Label)> _jumps = new();

        public void Emit(params byte[] bytes) => _bytes.AddRange(bytes);

        public void Label(string name) => _labels.Add(name, _bytes.Count);

        public void Jump(string label, params byte[] opcode)
        {
            Emit(opcode);
            _jumps.Add((_bytes.Count, label));
            Emit(0, 0, 0, 0);
        }

        public byte[] Finish()
        {
            byte[] result = _bytes.ToArray();
            foreach (var jump in _jumps)
                Buffer.BlockCopy(
                    BitConverter.GetBytes(_labels[jump.Label] - jump.Offset - 4),
                    0,
                    result,
                    jump.Offset,
                    4
                );
            return result;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(
        IntPtr process,
        IntPtr address,
        UIntPtr size,
        uint allocation,
        uint protection
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtectEx(
        IntPtr process,
        IntPtr address,
        UIntPtr size,
        uint protection,
        out uint oldProtection
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        byte[] bytes,
        int size,
        out IntPtr read
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
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
}
