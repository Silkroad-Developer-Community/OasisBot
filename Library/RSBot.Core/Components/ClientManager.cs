using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core.Event;
using RSBot.Core.Extensions;
using static RSBot.Core.Extensions.NativeExtensions;

namespace RSBot.Core.Components;

public class ClientManager
{
    private const int SroClientImageBase = 0x00400000;
    private static readonly (int Address, byte[] Expected, byte[] Patch)[] ChineseClientPatches =
    {
        (0x0060C6DB, new byte[] { 0x75, 0x2E }, new byte[] { 0xEB, 0x2E }),
        (0x0060C6A1, new byte[] { 0x75, 0x26 }, new byte[] { 0xEB, 0x26 }),
        (0x0067BF56, new byte[] { 0x75, 0x2C }, new byte[] { 0xEB, 0x2C }),
        (
            0x00AD449E,
            new byte[] { 0x68, 0x5C, 0x54, 0xE1, 0x00 },
            new byte[] { 0xEB, 0x2D, 0x90, 0x90, 0x90 }
        ),
    };

    /// <summary>
    ///     The client process
    /// </summary>
    private static Process _process;

    /// <summary>
    ///     Get, has client exited <c>true</c> otherwise; <c>false</c>
    /// </summary>
    public static bool IsRunning => _process != null && !_process.HasExited;

    /// <summary>
    ///     Start the game client
    /// </summary>
    /// <returns>Has successfully started <c>true</c>; otherwise <c>false</c></returns>
    public static async Task<bool> Start()
    {
        var silkroadDirectory = GlobalConfig.Get<string>("RSBot.SilkroadDirectory");
        var path = Path.Combine(silkroadDirectory, GlobalConfig.Get<string>("RSBot.SilkroadExecutable"));

        string libraryDllName = "Client.Library.dll";
        string fullPath = Path.Combine(Kernel.BasePath, libraryDllName);

        byte[] buffer = Encoding.Unicode.GetBytes(fullPath + "\0");
        uint pathLen = (uint)buffer.Length;

        var gatewayIndex = GlobalConfig.Get<byte>("RSBot.GatewayIndex");
        var divisionIndex = GlobalConfig.Get<byte>("RSBot.DivisionIndex");
        var contentId = Game.ReferenceManager.DivisionInfo.Locale;

        var args = $"/{contentId} {divisionIndex} {gatewayIndex} 0";

        var si = new STARTUPINFO();

        bool specialClient =
            Game.ClientType == GameClientType.RuSro
            || Game.ClientType == GameClientType.Global
            || Game.ClientType == GameClientType.Korean
            || Game.ClientType == GameClientType.VTC_Game
            || Game.ClientType == GameClientType.Turkey
            || Game.ClientType == GameClientType.Taiwan
            || Game.ClientType == GameClientType.Japanese;

        if (Game.ClientType == GameClientType.RuSro)
        {
            string login = GlobalConfig.Get<string>("RSBot.RuSro.login");
            string password = GlobalConfig.Get<string>("RSBot.RuSro.password");
            args = $"-LOGIN:{login} -PASSWORD:{password}";
        }

        if (
            !CreateProcess(
                null,
                $"\"{path}\" {args}",
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_SUSPENDED,
                IntPtr.Zero,
                silkroadDirectory,
                ref si,
                out var pi
            )
        )
            return false;

        PrepareTempConfigFile(pi.dwProcessId, divisionIndex);

        if (specialClient)
        {
            try
            {
                File.Copy(
                    Path.Combine(Kernel.BasePath, libraryDllName),
                    Path.Combine(silkroadDirectory, "dsound.dll"),
                    true
                );
            }
            catch (UnauthorizedAccessException)
            {
                Log.Error("Run as administrator!");
            }
            catch (IOException)
            {
                Log.Debug("DLL is using, can't replace");
            }

            Process sroProcess = Process.GetProcessById((int)pi.dwProcessId);

            if (
                Game.ClientType == GameClientType.VTC_Game
                || Game.ClientType == GameClientType.Turkey
                || Game.ClientType == GameClientType.Taiwan
            )
            {
                WaitForXigncodeUnpack(sroProcess, pi.hThread);
                ApplyXigncodePatch(sroProcess, pi);
            }
            else
            {
                ResumeThread(pi.hThread);
                Thread.Sleep(150);
                SuspendThread(pi.hThread);
            }

            BypassLauncherCheck(sroProcess, pi);

            ResumeThread(pi.hThread);

            _process = sroProcess;
        }
        else
        {
            var handle = OpenProcess(PROCESS_ALL_ACCESS, false, pi.dwProcessId);
            if (handle == IntPtr.Zero)
                return false;

            IntPtr kernelHandle = GetModuleHandleW("kernel32.dll");
            if (kernelHandle == IntPtr.Zero)
                return false;

            IntPtr loadLibAddr = GetProcAddress(kernelHandle, "LoadLibraryW");
            if (loadLibAddr == IntPtr.Zero)
                return false;

            IntPtr remotePath = VirtualAllocEx(handle, IntPtr.Zero, pathLen, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

            if (remotePath == IntPtr.Zero)
                return false;

            if (!WriteProcessMemory(handle, remotePath, buffer, pathLen, out _))
                return false;

            IntPtr remoteThread = CreateRemoteThread(handle, IntPtr.Zero, 0, loadLibAddr, remotePath, 0, IntPtr.Zero);

            if (remoteThread == IntPtr.Zero)
                return false;

            WaitForSingleObject(remoteThread, uint.MaxValue);

            if (Game.ClientType == GameClientType.Chinese)
            {
                var sroProcess = System.Diagnostics.Process.GetProcessById((int)pi.dwProcessId);
                WaitForChineseClientPatchTargets(sroProcess, pi.hThread);
                ApplyChineseClientPatch(sroProcess, pi);
            }

            VirtualFreeEx(handle, remotePath, 0, MEM_RELEASE);

            CloseHandle(remoteThread);
            CloseHandle(handle);

            ResumeThread(pi.hThread);
            ResumeThread(pi.hProcess);

            _process = Process.GetProcessById((int)pi.dwProcessId);
            if (_process == null || _process.HasExited)
                return false;
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += ClientProcess_Exited;

        EventManager.FireEvent("OnStartClient");
        return true;
    }

    /// <summary>
    /// Modifies the memory of a running process to bypass launcher security checks.
    /// </summary>
    /// <param name="process">The process whose memory will be patched. Must be a valid, running process and cannot be null.</param>
    /// <param name="pi">A PROCESS_INFORMATION structure containing information about the target process. Must correspond to the
    /// specified process and be valid.</param>
    public static void BypassLauncherCheck(Process process, PROCESS_INFORMATION pi)
    {
        var moduleMemory = new byte[process.MainModule.ModuleMemorySize];
        ReadProcessMemory(
            process.Handle,
            process.MainModule.BaseAddress,
            moduleMemory,
            process.MainModule.ModuleMemorySize,
            out _
        );

        string signature =
            "55 8B EC 83 EC ?? 8B 45 ?? 50 E8 ?? ?? ?? ?? 83 C4 04 89 45 ?? 8B 4D ?? 89 4D ?? 68 ?? ?? ?? ?? 6A 00 6A 00";

        int baseAddress = process.MainModule.BaseAddress.ToInt32();
        var address = FindPattern(signature, moduleMemory, baseAddress);

        byte[] patch = { 0xB0, 0x01, 0xC3 };

        VirtualProtectEx(pi.hProcess, address, (UIntPtr)patch.Length, 0x40, out uint oldProtect);
        WriteProcessMemory(pi.hProcess, address, patch, (uint)patch.Length, out _);
        VirtualProtectEx(pi.hProcess, address, (UIntPtr)patch.Length, oldProtect, out oldProtect);
    }

    /// <summary>
    /// Runs the suspended client in small time slices and stops the instant the packed
    /// executable has unpacked far enough for the "XIGNCODE" string to appear in the main
    /// module. This replaces a fixed Thread.Sleep so the patch is applied just before
    /// XIGNCODE initializes, independent of disk/CPU speed. On return the primary thread is
    /// left suspended (suspend count 1) so the caller can patch memory and resume normally.
    /// </summary>
    private static void WaitForXigncodeUnpack(Process process, IntPtr hThread)
    {
        const int sliceMs = 8;
        const int maxWaitMs = 5000;

        byte[] needle = Encoding.Unicode.GetBytes("XIGNCODE\0");
        var sw = Stopwatch.StartNew();

        while (true)
        {
            ResumeThread(hThread);
            Thread.Sleep(sliceMs);
            SuspendThread(hThread);

            if (process.HasExited)
                return;

            if (ModuleContains(process, needle))
                return;

            if (sw.ElapsedMilliseconds >= maxWaitMs)
            {
                Log.Warn("XIGNCODE: unpack timeout");
                return;
            }
        }
    }

    /// <summary>
    /// Reads the process main module and reports whether the given byte sequence is present.
    /// Returns false (instead of throwing) while the module is not yet readable.
    /// </summary>
    private static bool ModuleContains(Process process, byte[] needle)
    {
        try
        {
            process.Refresh();
            int size = process.MainModule.ModuleMemorySize;
            var buffer = new byte[size];

            if (
                !ReadProcessMemory(process.Handle, process.MainModule.BaseAddress, buffer, size, out var read)
                || read == IntPtr.Zero
            )
                return false;

            int limit = (int)read - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                int j = 0;
                for (; j < needle.Length; j++)
                {
                    if (buffer[i + j] != needle[j])
                        break;
                }

                if (j == needle.Length)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Applies a universal in-memory patch to bypass XIGNCODE in the specified process.
    /// Algorithm:
    ///   1. Find standalone Unicode "XIGNCODE\0" string in module memory
    ///   2. Locate the init call pattern: push 0; push "XIGNCODE"; push code_string; call SysEnter
    ///   3. Resolve XignCode_SysEnter and XignCode_SendCommand function addresses
    ///   4. Patch both functions to return 1 immediately
    /// </summary>
    private static void ApplyXigncodePatch(Process process, PROCESS_INFORMATION pi)
    {
        var moduleMemory = new byte[process.MainModule.ModuleMemorySize];
        ReadProcessMemory(
            process.Handle,
            process.MainModule.BaseAddress,
            moduleMemory,
            process.MainModule.ModuleMemorySize,
            out _
        );

        int baseAddress = process.MainModule.BaseAddress.ToInt32();

        byte[] xigncodeUtf16 = Encoding.Unicode.GetBytes("XIGNCODE\0");
        int anchorOffset = FindXigncodeAnchor(moduleMemory, xigncodeUtf16, baseAddress);
        if (anchorOffset == -1)
        {
            Log.Error("XIGNCODE patching error! Could not find XIGNCODE init pattern.");
            return;
        }

        int sysEnterRelative = BitConverter.ToInt32(moduleMemory, anchorOffset + 13);
        int sysEnterOffset = anchorOffset + 17 + sysEnterRelative;
        if (sysEnterOffset < 0 || sysEnterOffset >= moduleMemory.Length)
        {
            Log.Error("XIGNCODE patching error! Invalid SysEnter function address.");
            return;
        }

        IntPtr sysEnterAddr = (IntPtr)(baseAddress + sysEnterOffset);

        IntPtr sendCommandAddr = IntPtr.Zero;
        int searchEnd = Math.Min(sysEnterOffset + 0x500, moduleMemory.Length - 10);
        var callTargetCounts = new System.Collections.Generic.Dictionary<int, int>();
        for (int i = sysEnterOffset; i < searchEnd; i++)
        {
            if (
                moduleMemory[i] == 0xE8
                && moduleMemory[i + 5] == 0x83
                && moduleMemory[i + 6] == 0xC4
                && moduleMemory[i + 7] == 0x0C
            )
            {
                int rel = BitConverter.ToInt32(moduleMemory, i + 1);
                int targetOffset = i + 5 + rel;
                if (targetOffset >= 0 && targetOffset < moduleMemory.Length)
                {
                    callTargetCounts.TryGetValue(targetOffset, out int count);
                    callTargetCounts[targetOffset] = count + 1;
                }
            }
        }

        // Other cdecl functions (e.g. memset) may also match this pattern,
        // so we collect all call targets and pick the most frequently called one.
        if (callTargetCounts.Count > 0)
        {
            int mostCalledOffset = callTargetCounts.OrderByDescending(kvp => kvp.Value).First().Key;
            sendCommandAddr = (IntPtr)(baseAddress + mostCalledOffset);
        }

        byte[] patchSysEnter = { 0xB8, 0x01, 0x00, 0x00, 0x00, 0xC2, 0x0C, 0x00 };
        PatchProcessMemory(pi.hProcess, sysEnterAddr, patchSysEnter);

        if (sendCommandAddr != IntPtr.Zero)
        {
            byte[] patchSendCmd = { 0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3 };
            PatchProcessMemory(pi.hProcess, sendCommandAddr, patchSendCmd);
        }
        else
        {
            Log.Warn("XIGNCODE: Could not find SendCommand function, only SysEnter was patched.");
        }

        GC.Collect();
    }

    /// <summary>
    /// Runs the suspended Chinese client until the target bytes are unpacked and readable.
    /// On return the primary thread is left suspended so the caller can patch and resume.
    /// </summary>
    private static void WaitForChineseClientPatchTargets(Process process, IntPtr hThread)
    {
        const int sliceMs = 8;
        const int maxWaitMs = 5000;

        var sw = Stopwatch.StartNew();

        while (true)
        {
            ResumeThread(hThread);
            Thread.Sleep(sliceMs);
            SuspendThread(hThread);

            if (process.HasExited)
                return;

            process.Refresh();

            if (ChineseClientPatchTargetsReady(process.Handle, process.MainModule.BaseAddress.ToInt32()))
                return;

            if (sw.ElapsedMilliseconds >= maxWaitMs)
            {
                Log.Warn("Chinese client patch: unpack timeout.");
                return;
            }
        }
    }

    private static bool ChineseClientPatchTargetsReady(IntPtr processHandle, int moduleBaseAddress)
    {
        foreach (var patch in ChineseClientPatches)
        {
            if (
                !TryReadClientBytes(
                    processHandle,
                    moduleBaseAddress,
                    patch.Address,
                    patch.Expected.Length,
                    out var current
                )
            )
                return false;

            if (!current.SequenceEqual(patch.Expected) && !current.SequenceEqual(patch.Patch))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Applies fixed in-memory patches for the Chinese sro_client.exe.
    /// </summary>
    private static void ApplyChineseClientPatch(Process process, PROCESS_INFORMATION pi)
    {
        int moduleBaseAddress = process.MainModule.BaseAddress.ToInt32();

        foreach (var patch in ChineseClientPatches)
            PatchKnownClientBytes(pi.hProcess, moduleBaseAddress, patch.Address, patch.Expected, patch.Patch);
    }

    private static void PatchKnownClientBytes(
        IntPtr processHandle,
        int moduleBaseAddress,
        int clientAddress,
        byte[] expected,
        byte[] patch
    )
    {
        if (!TryReadClientBytes(processHandle, moduleBaseAddress, clientAddress, expected.Length, out var current))
        {
            Log.Warn($"Chinese client patch: could not read 0x{clientAddress:X8}.");
            return;
        }

        if (current.SequenceEqual(patch))
            return;

        if (!current.SequenceEqual(expected))
        {
            Log.Warn(
                $"Chinese client patch: unexpected bytes at 0x{clientAddress:X8}. Expected {FormatBytes(expected)}, got {FormatBytes(current)}."
            );
            return;
        }

        PatchProcessMemory(processHandle, GetClientAddress(moduleBaseAddress, clientAddress), patch);
    }

    private static bool TryReadClientBytes(
        IntPtr processHandle,
        int moduleBaseAddress,
        int clientAddress,
        int length,
        out byte[] bytes
    )
    {
        bytes = new byte[length];

        return ReadProcessMemory(
                processHandle,
                GetClientAddress(moduleBaseAddress, clientAddress),
                bytes,
                bytes.Length,
                out var read
            )
            && read.ToInt32() == bytes.Length;
    }

    private static IntPtr GetClientAddress(int moduleBaseAddress, int clientAddress)
    {
        return (IntPtr)(moduleBaseAddress + clientAddress - SroClientImageBase);
    }

    private static string FormatBytes(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    /// <summary>
    /// Finds the XIGNCODE init call anchor in WinMain by locating the standalone "XIGNCODE" Unicode
    /// string and its reference in the code pattern: push 0; push VA("XIGNCODE"); push code_string; call func.
    /// </summary>
    /// <returns>Offset in the buffer where the anchor pattern starts, or -1 if not found.</returns>
    private static int FindXigncodeAnchor(byte[] memory, byte[] xigncodeUtf16, int baseAddress)
    {
        for (int strIdx = 0; strIdx <= memory.Length - xigncodeUtf16.Length; strIdx++)
        {
            bool strMatch = true;
            for (int j = 0; j < xigncodeUtf16.Length; j++)
            {
                if (memory[strIdx + j] != xigncodeUtf16[j])
                {
                    strMatch = false;
                    break;
                }
            }

            if (!strMatch)
                continue;

            byte[] vaBytes = BitConverter.GetBytes(baseAddress + strIdx);

            for (int ci = 0; ci <= memory.Length - 18; ci++)
            {
                if (
                    memory[ci] == 0x6A
                    && memory[ci + 1] == 0x00
                    && memory[ci + 2] == 0x68
                    && memory[ci + 3] == vaBytes[0]
                    && memory[ci + 4] == vaBytes[1]
                    && memory[ci + 5] == vaBytes[2]
                    && memory[ci + 6] == vaBytes[3]
                    && memory[ci + 7] == 0x68
                    && memory[ci + 12] == 0xE8
                )
                {
                    return ci;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Writes a byte patch to the specified address in a remote process, temporarily
    /// changing memory protection to PAGE_EXECUTE_READWRITE.
    /// </summary>
    private static void PatchProcessMemory(IntPtr processHandle, IntPtr address, byte[] patch)
    {
        VirtualProtectEx(processHandle, address, (UIntPtr)patch.Length, 0x40, out uint oldProtect);
        WriteProcessMemory(processHandle, address, patch, (uint)patch.Length, out _);
        VirtualProtectEx(processHandle, address, (UIntPtr)patch.Length, oldProtect, out _);
    }

    /// <summary>
    ///     Kill the game client process
    /// </summary>
    public static void Kill()
    {
        if (!IsRunning)
            return;

        try
        {
            _process.Kill();
        }
        catch { }
    }

    /// <summary>
    ///     Change client process title
    /// </summary>
    /// <param name="title">The new title</param>
    public static void SetTitle(string title)
    {
        if (_process == null)
            return;

        SetWindowText(_process.MainWindowHandle, title);
    }

    /// <summary>
    ///     Change client visible
    /// </summary>
    /// <param name="visible">The visible</param>
    public static void SetVisible(bool visible)
    {
        if (visible)
            ShowWindow(_process.MainWindowHandle, SW_SHOW);
        else
            ShowWindow(_process.MainWindowHandle, SW_HIDE);
    }

    /// <summary>
    ///     Observes the process.
    /// </summary>
    private static void ClientProcess_Exited(object sender, EventArgs e)
    {
        Log.Warn("Client process exited!");
        EventManager.FireEvent("OnExitClient");
    }

    /// <summary>
    ///     Prepare the config file for loader
    /// </summary>
    /// <param name="processId"></param>
    /// <param name="divisionIndex"></param>
    private static void PrepareTempConfigFile(uint processId, int divisionIndex)
    {
        var tmpConfigFile = $"RSBot_{processId}.tmp";

        var division = Game.ReferenceManager.DivisionInfo.Divisions[divisionIndex];
        var gatewayPort = Game.ReferenceManager.GatewayInfo.Port;

        var redirectIp = "127.0.0.1";
        using var writer = new BinaryWriter(
            new FileStream(Path.Combine(Path.GetTempPath(), tmpConfigFile), FileMode.Create)
        );

        writer.Write(GlobalConfig.Get<bool>("RSBot.Loader.DebugMode"));
        writer.WriteAscii(redirectIp);
        writer.Write(Kernel.Proxy.Port);
        writer.Write(division.GatewayServers.Count);
        foreach (var gatewayServer in division.GatewayServers)
            writer.WriteAscii(gatewayServer);

        writer.Write(gatewayPort);
    }

    /// <summary>
    ///     Searches the specified buffer for the first occurrence of a byte pattern defined by a hexadecimal string
    ///     or masks ?? and ? and returns the corresponding memory address.
    /// </summary>
    /// <param name="stringPattern"></param>
    /// <param name="buffer"></param>
    /// <param name="baseAddress"></param>
    /// <returns></returns>
    private static IntPtr FindPattern(string stringPattern, byte[] buffer, int baseAddress)
    {
        var pattern = stringPattern
            .Split(' ')
            .Select(p => p == "??" || p == "?" ? -1 : int.Parse(p, NumberStyles.AllowHexSpecifier))
            .ToArray();

        int bufferLength = buffer.Length;
        int patternLength = pattern.Length;

        for (int i = 0; i <= bufferLength - patternLength; i++)
        {
            bool found = true;
            for (int j = 0; j < patternLength; j++)
            {
                if (pattern[j] != -1 && buffer[i + j] != (byte)pattern[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
                return (IntPtr)(baseAddress + i);
        }

        return IntPtr.Zero;
    }
}
