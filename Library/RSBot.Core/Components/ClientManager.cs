using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core.Event;
using RSBot.Core.Extensions;
using RSBot.Core.Network;
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
        (0x00AD449E, new byte[] { 0x68, 0x5C, 0x54, 0xE1, 0x00 }, new byte[] { 0xEB, 0x2D, 0x90, 0x90, 0x90 }),
    };

    /// <summary>
    ///     The client process
    /// </summary>
    private static Process _process;

    /// <summary>
    ///     Get, has client exited <c>true</c> otherwise; <c>false</c>
    /// </summary>
    public static bool IsRunning
    {
        get
        {
            var process = _process;
            if (process == null)
                return false;

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Start the game client
    /// </summary>
    /// <returns>Has successfully started <c>true</c>; otherwise <c>false</c></returns>
    public static async Task<bool> Start()
    {
        // A client we launched earlier has to go first: otherwise _process gets overwritten below and
        // the previous (often still suspended) process stays in memory with nobody able to reach it.
        // Kill() runs unconditionally, also when no client is tracked: in that case it still cleans up
        // leftovers of a previous session, and such a process keeps the game's single instance mutex and
        // makes the new client refuse to start with no visible reason. The cleanup itself stays opt-out
        // through RSBot.Silkroad.KillStaleClients.
        Kill();

        var sessionProxy = Kernel.Proxy;
        VtcClientLayout vtcLayout = null;
        if (Game.ClientType == GameClientType.VTC_Game)
        {
            try
            {
                if (sessionProxy == null)
                    return false;
                vtcLayout = await sessionProxy.WaitForVtcProtectionAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Log.Error("VTC game startup: " + error.Message);
                return false;
            }
        }
        var silkroadDirectory = GlobalConfig.Get<string>("RSBot.SilkroadDirectory");
        var path = Path.Combine(silkroadDirectory, GlobalConfig.Get<string>("RSBot.SilkroadExecutable"));
        if (vtcLayout != null)
        {
            try
            {
                vtcLayout.ValidateForGame(path);
            }
            catch (Exception error)
            {
                Log.Error(error.Message);
                sessionProxy?.Shutdown();
                return false;
            }
        }

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

        if (specialClient)
        {
            try
            {
                ClientLoaderFile.Ensure(fullPath, Path.Combine(silkroadDirectory, "dsound.dll"));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Error("Could not prepare client loader: " + error.Message);
                return false;
            }
        }

        if (
            !CreateProcess(
                null,
                $"\"{path}\" {args}",
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                vtcLayout != null ? VtcClientStartup.DebugOnlyThisProcess : CREATE_SUSPENDED,
                IntPtr.Zero,
                silkroadDirectory,
                ref si,
                out var pi
            )
        )
            return false;

        // The client is created suspended and is only resumed at the very end of the setup below, so
        // every early exit has to terminate it explicitly. Otherwise an orphaned, never-resumed
        // process stays in memory and, once _process is overwritten, can never be reached again.
        Process sroProcess;
        try
        {
            sroProcess = Process.GetProcessById((int)pi.dwProcessId);
        }
        catch (Exception error)
        {
            Log.Error("Could not open the started client process: " + error.Message);

            TerminateQuietly(null, pi.hProcess);
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);

            return false;
        }

        _process = sroProcess;

        // 4game's RU client refuses to run twice: it acquires the machine wide mutex
        // "Global\Silkroad Client RU" and exits with "Already executing..." when another copy owns it.
        // The check can be bypassed so several characters run on one machine at the same time.
        if (Game.ClientType == GameClientType.RuSro && GlobalConfig.Get<bool>("RSBot.Silkroad.MultiClient", true))
            ApplyMultiClientPatch(sroProcess, pi);

        // Guard the client with a kill-on-close job: Windows then terminates it together with the bot,
        // even when the bot crashes or is killed from the task manager.
        ClientJobObject.Attach(sroProcess, pi.hProcess);

        var started = false;

        try
        {
            using var vtcStartup = vtcLayout != null ? new VtcClientStartup(pi) : null;

            started = SetupClient(
                sroProcess,
                pi,
                vtcStartup,
                vtcLayout,
                specialClient,
                sessionProxy,
                divisionIndex,
                buffer,
                pathLen
            );
        }
        catch (Exception error)
        {
            Log.Error("Client startup failed: " + error.Message);
        }
        finally
        {
            if (!started)
                TerminateQuietly(sroProcess, pi.hProcess);

            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
        }

        if (!started)
        {
            _process = null;

            return false;
        }

        EventManager.FireEvent("OnStartClient");

        return true;
    }

    /// <summary>
    ///     Applies the loader injection, the protection bypasses and the client patches to the freshly
    ///     created, still suspended process, then resumes it.
    /// </summary>
    /// <returns><c>true</c> when the client was resumed and is running; otherwise <c>false</c>.</returns>
    private static bool SetupClient(
        Process sroProcess,
        PROCESS_INFORMATION pi,
        VtcClientStartup vtcStartup,
        VtcClientLayout vtcLayout,
        bool specialClient,
        Proxy sessionProxy,
        int divisionIndex,
        byte[] buffer,
        uint pathLen
    )
    {
        PrepareTempConfigFile(pi.dwProcessId, divisionIndex);

        if (specialClient)
        {
            if (Game.ClientType == GameClientType.VTC_Game)
            {
                string error;
                if (
                    !vtcStartup.TryPrepare(sroProcess, vtcLayout, out error)
                    || !VtcXigncodeDisablePatch.TryInstall(sroProcess, pi.hProcess, vtcLayout, out error)
                    || !VtcGatewayRedirectPatch.TryInstall(
                        sroProcess,
                        pi.hProcess,
                        vtcLayout,
                        Game.ReferenceManager.DivisionInfo.Divisions[divisionIndex].GatewayServers,
                        Game.ReferenceManager.GatewayInfo.Port,
                        sessionProxy.Port,
                        out error
                    )
                )
                {
                    Log.Error(error);

                    vtcStartup.Dispose();
                    sessionProxy?.Shutdown();
                    return false;
                }

                Log.Debug("VTC: game XIGNCODE disabled; the session host owns initialization and Probe commands.");
            }
            else if (Game.ClientType == GameClientType.Turkey || Game.ClientType == GameClientType.Taiwan)
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

            if (vtcStartup != null)
            {
                if (!vtcStartup.TryResume(out var error))
                {
                    Log.Error(error);
                    vtcStartup.Dispose();
                    sessionProxy?.Shutdown();
                    return false;
                }
            }
            else
                ResumeThread(pi.hThread);
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
                WaitForChineseClientPatchTargets(sroProcess, pi.hThread);
                ApplyChineseClientPatch(sroProcess, pi);
            }

            VirtualFreeEx(handle, remotePath, 0, MEM_RELEASE);

            CloseHandle(remoteThread);
            CloseHandle(handle);

            ResumeThread(pi.hThread);
            ResumeThread(pi.hProcess);

            if (sroProcess.HasExited)
                return false;
        }

        sroProcess.EnableRaisingEvents = true;
        sroProcess.Exited += ClientProcess_Exited;

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

    private static readonly object MultiClientPatchLock = new();

    private static string _multiClientPatchPath;
    private static long _multiClientPatchStamp;
    private static long _multiClientPatchLength;
    private static int _multiClientPatchAddress;

    /// <summary>
    ///     Returns the guard address that was resolved earlier for the same client file, so the
    ///     executable is not read and parsed again on every launch.
    /// </summary>
    private static bool TryGetCachedMultiClientPatch(string clientPath, out int patchAddress)
    {
        patchAddress = 0;

        try
        {
            var file = new FileInfo(clientPath);

            lock (MultiClientPatchLock)
            {
                if (
                    _multiClientPatchAddress != 0
                    && string.Equals(_multiClientPatchPath, clientPath, StringComparison.OrdinalIgnoreCase)
                    && _multiClientPatchLength == file.Length
                    && _multiClientPatchStamp == file.LastWriteTimeUtc.Ticks
                )
                {
                    patchAddress = _multiClientPatchAddress;

                    return true;
                }
            }
        }
        catch (Exception error)
        {
            Log.Debug("Multi client: could not inspect the client executable: " + error.Message);
        }

        return false;
    }

    /// <summary>
    ///     Remembers the resolved guard address for the given client file.
    /// </summary>
    private static void CacheMultiClientPatch(string clientPath, int patchAddress)
    {
        try
        {
            var file = new FileInfo(clientPath);

            lock (MultiClientPatchLock)
            {
                _multiClientPatchPath = clientPath;
                _multiClientPatchLength = file.Length;
                _multiClientPatchStamp = file.LastWriteTimeUtc.Ticks;
                _multiClientPatchAddress = patchAddress;
            }
        }
        catch (Exception error)
        {
            Log.Debug("Multi client: could not cache the guard address: " + error.Message);
        }
    }

    /// <summary>
    ///     Writes the guard patch (75 -> EB) into the still suspended client process.
    /// </summary>
    private static void ApplyGuardByte(Process process, PROCESS_INFORMATION pi, IntPtr patchAddress)
    {
        if (
            !VirtualProtectEx(pi.hProcess, patchAddress, (UIntPtr)1, PAGE_EXECUTE_READWRITE, out var oldProtect)
        )
        {
            Log.Warn(
                "Multi client: could not unprotect the guard instruction, Windows error "
                    + Marshal.GetLastWin32Error()
            );

            return;
        }

        try
        {
            // 75 (jne) -> EB (jmp): always continue as if the mutex was acquired.
            var patch = new byte[] { 0xEB };

            if (
                !WriteProcessMemory(pi.hProcess, patchAddress, patch, (uint)patch.Length, out var written)
                || written.ToInt32() != patch.Length
            )
            {
                Log.Warn(
                    "Multi client: could not write the guard patch, Windows error " + Marshal.GetLastWin32Error()
                );

                return;
            }
        }
        finally
        {
            VirtualProtectEx(pi.hProcess, patchAddress, (UIntPtr)1, oldProtect, out _);
        }

        Log.Notify(
            $"Multi client patch applied at 0x{patchAddress.ToInt32():X8} (pid {process.Id}): "
                + "the client will not block on the single instance guard."
        );
    }

    /// <summary>
    ///     Bypasses the single instance guard of the RuSro client. The client creates the machine wide
    ///     mutex "Global\Silkroad Client RU", waits for it with a zero timeout and, when the wait returns
    ///     WAIT_TIMEOUT (0x102) because another copy owns it, closes the mutex and exits with
    ///     "Already executing...". The conditional jump after that wait is turned into an unconditional
    ///     one, so the error branch is never taken and the client continues normally.
    ///     <para>
    ///         The patch address is resolved from the PE image on disk (ImageBase + RVA) instead of
    ///         reading the running process: while the client is still suspended its module list is not
    ///         initialized yet, so both <see cref="Process.MainModule" /> and Toolhelp fail with
    ///         ERROR_PARTIAL_COPY, but writing to a known address works.
    ///     </para>
    /// </summary>
    private static void ApplyMultiClientPatch(Process process, PROCESS_INFORMATION pi)
    {
        try
        {
            var clientPath = GetConfiguredClientPath();
            if (clientPath == null)
            {
                Log.Debug("Multi client: the client executable is not configured, skipping.");

                return;
            }

            // The guard address only depends on the client file, so a successful lookup is reused until
            // the executable changes.
            if (TryGetCachedMultiClientPatch(clientPath, out var cachedAddress))
            {
                ApplyGuardByte(process, pi, (IntPtr)cachedAddress);

                return;
            }

            byte[] image;

            try
            {
                image = File.ReadAllBytes(clientPath);
            }
            catch (Exception error)
            {
                Log.Warn("Multi client: could not read the client executable: " + error.Message);

                return;
            }

            if (!TryGetImageLayout(image, out var imageBase, out var sections, out var dynamicBase))
            {
                Log.Warn("Multi client: could not parse the client executable.");

                return;
            }

            if (dynamicBase)
            {
                Log.Warn(
                    "Multi client: the client image is relocatable (DYNAMICBASE), the guard address cannot "
                        + "be predicted, skipping the patch."
                );

                return;
            }

            byte[] mutexName = Encoding.Unicode.GetBytes("Global\\Silkroad Client RU\0");
            var stringOffset = IndexOf(image, mutexName, 0);

            if (stringOffset < 0 || !TryFileOffsetToRva(sections, stringOffset, out var stringRva))
            {
                Log.Debug("Multi client: the single instance mutex string was not found, skipping.");

                return;
            }

            var stringAddress = imageBase + stringRva;
            var pushString = new byte[]
            {
                0x68,
                (byte)stringAddress,
                (byte)(stringAddress >> 8),
                (byte)(stringAddress >> 16),
                (byte)(stringAddress >> 24),
            };

            // The address may also occur somewhere as plain data, so every candidate is validated
            // against the expected instruction layout and the first matching one is patched.
            var pushOffset = -1;
            var searchFrom = 0;

            while (true)
            {
                var candidate = IndexOf(image, pushString, searchFrom);
                if (candidate < 0)
                    break;

                if (IsExpectedMultiClientCode(image, candidate))
                {
                    pushOffset = candidate;

                    break;
                }

                searchFrom = candidate + 1;
            }

            if (pushOffset < 0)
            {
                Log.Warn(
                    "Multi client: the single instance check has an unexpected layout in this client "
                        + "build, skipping the patch."
                );

                return;
            }

            var errorJumpOffset = FindErrorJumpOffset(image, pushOffset);
            if (errorJumpOffset < 0 || !TryFileOffsetToRva(sections, errorJumpOffset, out var jumpRva))
            {
                Log.Warn("Multi client: the error branch of the single instance check was not found.");

                return;
            }

            var patchAddress = (IntPtr)(imageBase + jumpRva);

            CacheMultiClientPatch(clientPath, patchAddress.ToInt32());
            ApplyGuardByte(process, pi, patchAddress);
        }
        catch (Exception error)
        {
            Log.Warn("Multi client: could not apply the patch: " + error.Message);
        }
    }

    /// <summary>
    ///     Returns the full path of the configured client executable, or <c>null</c> when it is not set
    ///     or does not exist.
    /// </summary>
    private static string GetConfiguredClientPath()
    {
        try
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    GlobalConfig.Get<string>("RSBot.SilkroadDirectory", ""),
                    GlobalConfig.Get<string>("RSBot.SilkroadExecutable", "")
                )
            );

            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     A section of a PE image: where it lives in the file and where it is mapped to in memory.
    /// </summary>
    private readonly struct ImageSection
    {
        public ImageSection(int virtualAddress, int rawSize, int rawPointer)
        {
            VirtualAddress = virtualAddress;
            RawSize = rawSize;
            RawPointer = rawPointer;
        }

        public int VirtualAddress { get; }
        public int RawSize { get; }
        public int RawPointer { get; }
    }

    /// <summary>
    ///     Reads the image base and the section table from a PE file on disk.
    /// </summary>
    private static bool TryGetImageLayout(
        byte[] image,
        out int imageBase,
        out List<ImageSection> sections,
        out bool dynamicBase
    )
    {
        imageBase = 0;
        sections = null;
        dynamicBase = false;

        if (image == null || image.Length < 0x40)
            return false;

        try
        {
            var peOffset = BitConverter.ToInt32(image, 0x3C);
            if (peOffset <= 0 || peOffset + 24 > image.Length)
                return false;

            var numberOfSections = BitConverter.ToUInt16(image, peOffset + 6);
            var optionalHeaderSize = BitConverter.ToUInt16(image, peOffset + 20);
            var optionalHeaderOffset = peOffset + 24;
            var magic = BitConverter.ToUInt16(image, optionalHeaderOffset);

            imageBase =
                magic == 0x20B
                    ? (int)BitConverter.ToUInt64(image, optionalHeaderOffset + 24)
                    : BitConverter.ToInt32(image, optionalHeaderOffset + 28);

            // DYNAMICBASE (0x0040) means the loader may relocate the image, so ImageBase + RVA is not a
            // reliable address and the guard must not be patched blindly.
            if (optionalHeaderOffset + 72 <= image.Length)
                dynamicBase = (BitConverter.ToUInt16(image, optionalHeaderOffset + 70) & 0x0040) != 0;

            var sectionOffset = optionalHeaderOffset + optionalHeaderSize;
            if (numberOfSections == 0 || sectionOffset + numberOfSections * 40 > image.Length)
                return false;

            sections = new List<ImageSection>(numberOfSections);

            for (var i = 0; i < numberOfSections; i++)
            {
                var entry = sectionOffset + i * 40;
                var virtualAddress = BitConverter.ToInt32(image, entry + 12);
                var rawSize = BitConverter.ToInt32(image, entry + 16);
                var rawPointer = BitConverter.ToInt32(image, entry + 20);

                if (rawSize > 0)
                    sections.Add(new ImageSection(virtualAddress, rawSize, rawPointer));
            }

            return imageBase != 0 && sections.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Converts a file offset inside a PE section into the relative virtual address it is mapped to.
    /// </summary>
    private static bool TryFileOffsetToRva(List<ImageSection> sections, int fileOffset, out int rva)
    {
        rva = 0;

        foreach (var section in sections)
        {
            if (fileOffset < section.RawPointer || fileOffset >= section.RawPointer + section.RawSize)
                continue;

            rva = section.VirtualAddress + (fileOffset - section.RawPointer);

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Validates the instructions that immediately follow the mutex name push, so a different client
    ///     build is never patched at a random address.
    /// </summary>
    private static bool IsExpectedMultiClientCode(byte[] memory, int pushOffset)
    {
        // push name; push 2; push 2; push 0; call dword ptr [CreateMutex...]
        var expected = new byte[] { 0x6A, 0x02, 0x6A, 0x02, 0x6A, 0x00, 0xFF, 0x15 };

        if (pushOffset + 5 + expected.Length > memory.Length)
            return false;

        for (var i = 0; i < expected.Length; i++)
        {
            if (memory[pushOffset + 5 + i] != expected[i])
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Finds the "cmp eax, 102h; jne" pair that follows the mutex wait and returns the offset of the
    ///     jump opcode.
    /// </summary>
    private static int FindErrorJumpOffset(byte[] memory, int pushOffset)
    {
        var expected = new byte[] { 0x3D, 0x02, 0x01, 0x00, 0x00, 0x75 };
        var limit = Math.Min(pushOffset + 0x60, memory.Length - expected.Length);

        for (var i = pushOffset; i < limit; i++)
        {
            var match = true;

            for (var j = 0; j < expected.Length; j++)
            {
                if (memory[i + j] != expected[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i + expected.Length - 1; // opcode of the conditional jump
        }

        return -1;
    }

    /// <summary>
    ///     Searches <paramref name="haystack" /> for the first occurrence of <paramref name="needle" />
    ///     starting at the given offset.
    /// </summary>
    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return -1;

        var limit = haystack.Length - needle.Length;

        for (var i = Math.Max(0, start); i <= limit; i++)
        {
            if (haystack[i] != needle[0])
                continue;

            var match = true;

            for (var j = 1; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }

    /// <summary>
    ///     Kill the game client process, including every process it spawned. Leftovers of a previous
    ///     session are cleaned up as well, unless <c>RSBot.Silkroad.KillStaleClients</c> is disabled.
    /// </summary>
    public static void Kill()
    {
        var process = Interlocked.Exchange(ref _process, null);

        if (process != null)
        {
            // Detach first: an intentional shutdown must not fire OnExitClient and trigger a relogin.
            try
            {
                process.EnableRaisingEvents = false;
                process.Exited -= ClientProcess_Exited;
            }
            catch { }

            TerminateClient(process);
        }

        // A protected client (XIGNCODE) or a client that was launched outside of ClientManager can still
        // be alive here. Such a leftover keeps the single instance mutex and blocks the next launch.
        // The cleanup is opt-out, because it touches processes the bot did not launch itself.
        if (GlobalConfig.Get<bool>("RSBot.Silkroad.KillStaleClients", true))
            KillStaleClients();
    }

    /// <summary>
    ///     Shuts a client process down step by step, from the gentle way to the hardest one, and reports
    ///     which step actually worked.
    /// </summary>
    /// <returns><c>true</c> when the process is gone; otherwise <c>false</c>.</returns>
    private static bool TerminateClient(Process process)
    {
        if (HasExited(process))
            return true;

        var pid = process.Id;
        var name = SafeProcessName(process);

        // 1. Ask the client to close itself. A protected client releases its driver and its single
        //    instance mutex only on a normal shutdown, so this step matters more than TerminateProcess.
        try
        {
            if (process.CloseMainWindow() && WaitForExit(process, 2500))
            {
                Log.Debug($"Client process {pid} ({name}) closed its main window and exited.");

                return true;
            }
        }
        catch (Exception error)
        {
            Log.Debug($"Client process {pid}: graceful close failed: {error.Message}");
        }

        // 2. Kill the process tree.
        try
        {
            process.Kill(entireProcessTree: true);

            if (WaitForExit(process, 3000))
            {
                Log.Debug($"Client process {pid} ({name}) was terminated as a process tree.");

                return true;
            }
        }
        catch (Exception error)
        {
            Log.Warn($"Could not terminate the client process tree of {pid}: {error.Message}");
        }

        // 3. Terminate through a freshly opened handle, which still works when the wrapper is stale.
        if (TerminateByHandle(process))
        {
            Log.Debug($"Client process {pid} ({name}) was terminated by handle.");

            return true;
        }

        Log.Error(
            $"Client process {pid} ({name}) is still alive after graceful close, tree kill and handle "
                + "termination. A protection driver is most likely blocking it; the client will stay in "
                + "memory and block the next launch."
        );

        return false;
    }

    /// <summary>
    ///     Terminates every leftover process of the configured client executable that is not tracked by
    ///     the bot anymore. Only processes started from the exact configured path are touched, so a
    ///     second installation or a manually started client from another folder is never affected.
    /// </summary>
    /// <returns>Number of terminated leftovers.</returns>
    public static int KillStaleClients()
    {
        string expectedPath;
        try
        {
            expectedPath = Path.GetFullPath(
                Path.Combine(
                    GlobalConfig.Get<string>("RSBot.SilkroadDirectory", ""),
                    GlobalConfig.Get<string>("RSBot.SilkroadExecutable", "")
                )
            );
        }
        catch
        {
            return 0;
        }

        if (!File.Exists(expectedPath))
            return 0;

        var processes = SnapshotProcesses();
        var terminated = 0;

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(expectedPath)))
        {
            try
            {
                if (process.Id == Environment.ProcessId || HasExited(process))
                    continue;

                string modulePath = null;

                try
                {
                    modulePath = process.MainModule?.FileName;
                }
                catch
                {
                    // The process is protected or belongs to another user: leave it alone.
                }

                if (!string.Equals(modulePath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only clean up clients that are ours: either started by this bot, or orphaned. A client
                // whose parent process is still alive belongs to another bot instance or was started
                // manually and must never be touched.
                if (!IsLeftoverClient(process, processes, out var owner))
                {
                    Log.Debug($"Leaving client process {process.Id} alone, it {owner}.");

                    continue;
                }

                Log.Warn(
                    $"A leftover client process is still running (pid {process.Id}, started "
                        + $"{SafeStartTime(process)}). Terminating it before the next launch."
                );

                if (TerminateClient(process))
                    terminated++;
            }
            catch (Exception error)
            {
                Log.Warn($"Could not clean up a leftover client process: {error.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        return terminated;
    }

    private readonly struct ProcessSnapshot
    {
        public ProcessSnapshot(uint parentId, string name)
        {
            ParentId = parentId;
            Name = name;
        }

        public uint ParentId { get; }
        public string Name { get; }
    }

    /// <summary>
    ///     Takes a single snapshot of the process list (pid, parent pid, name) via Toolhelp.
    /// </summary>
    private static Dictionary<uint, ProcessSnapshot> SnapshotProcesses()
    {
        var result = new Dictionary<uint, ProcessSnapshot>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return result;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };

            if (!Process32FirstW(snapshot, ref entry))
                return result;

            do
            {
                result[entry.th32ProcessID] = new ProcessSnapshot(
                    entry.th32ParentProcessID,
                    entry.szExeFile ?? string.Empty
                );
            } while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return result;
    }

    /// <summary>
    ///     A client is considered a leftover only when it was started by this bot and is not tracked
    ///     anymore, or when its parent process is already gone. Anything with a living parent (another
    ///     bot instance, a launcher, the user's explorer) is left untouched.
    /// </summary>
    private static bool IsLeftoverClient(
        Process process,
        Dictionary<uint, ProcessSnapshot> processes,
        out string reason
    )
    {
        reason = null;

        // The client this bot is running is never a leftover, even when the cleanup is triggered while
        // it is still alive. Killing it here would also fire OnExitClient and trigger a relogin.
        var tracked = _process;
        if (tracked != null && !HasExited(tracked) && tracked.Id == process.Id)
        {
            reason = "is the client currently used by this bot";

            return false;
        }

        if (!processes.TryGetValue((uint)process.Id, out var info))
        {
            reason = "is not part of the process snapshot";

            return false;
        }

        var parentId = info.ParentId;

        if (parentId == 0 || parentId == (uint)Environment.ProcessId)
            return true;

        if (!processes.TryGetValue(parentId, out var parent))
            return true;

        reason = $"is owned by the running process {parentId} ({parent.Name})";

        return false;
    }

    /// <summary>
    ///     Returns the process name, or a placeholder when the process is already gone.
    /// </summary>
    private static string SafeProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    ///     Returns the process start time, or a placeholder when it cannot be read.
    /// </summary>
    private static string SafeStartTime(Process process)
    {
        try
        {
            return process.StartTime.ToString("HH:mm:ss");
        }
        catch
        {
            return "unknown time";
        }
    }

    /// <summary>
    ///     Waits for the process to exit and reports whether it is gone afterwards.
    /// </summary>
    private static bool WaitForExit(Process process, int milliseconds)
    {
        try
        {
            return process.WaitForExit(milliseconds) || HasExited(process);
        }
        catch
        {
            return HasExited(process);
        }
    }

    /// <summary>
    ///     Terminates a client process through a freshly opened handle. This is the last resort and still
    ///     works when <see cref="Process.Kill()" /> is blocked by a protection driver.
    /// </summary>
    private static bool TerminateByHandle(Process process)
    {
        IntPtr handle;

        try
        {
            handle = OpenProcess(PROCESS_TERMINATE, false, (uint)process.Id);
        }
        catch (Exception error)
        {
            Log.Warn($"Could not open client process {process.Id} for termination: {error.Message}");

            return false;
        }

        if (handle == IntPtr.Zero)
            return false;

        try
        {
            TerminateProcess(handle, 1);

            try
            {
                process.WaitForExit(2000);
            }
            catch { }

            return HasExited(process);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    ///     Terminates the whole client process on a setup failure, using the handle returned by
    ///     <c>CreateProcess</c> so the still suspended process is guaranteed to disappear.
    /// </summary>
    private static void TerminateQuietly(Process process, IntPtr processHandle)
    {
        if (processHandle != IntPtr.Zero)
        {
            try
            {
                TerminateProcess(processHandle, 1);
            }
            catch { }
        }

        if (process == null)
            return;

        try
        {
            if (HasExited(process))
                return;

            process.Kill();

            if (!process.WaitForExit(2000))
                Log.Warn($"Client process {process.Id} could not be terminated after a failed start.");
        }
        catch (Exception error)
        {
            Log.Warn($"Could not terminate client process {process.Id}: {error.Message}");

            TerminateByHandle(process);
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return false;
        }
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
        Interlocked.CompareExchange(ref _process, null, sender as Process);

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
        var tmpConfigFile = $"OasisBot_{processId}.tmp";

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
