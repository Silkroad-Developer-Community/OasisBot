using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RSBot.Core.Extensions;

namespace RSBot.Core.Components;

/// <summary>
/// Stops execution at the discovered SDK entries, including after unpacking overwrites their bytes.
/// All methods must run synchronously on the thread that called CreateProcess.
/// Owns the creation handles and keeps every client thread suspended until the caller finishes patching.
/// </summary>
internal sealed class VtcClientStartup : IDisposable
{
    internal const uint DebugOnlyThisProcess = 0x00000002;
    private const uint ExceptionEvent = 1,
        CreateThreadEvent = 2,
        CreateProcessEvent = 3;
    private const uint ExitThreadEvent = 4,
        ExitProcessEvent = 5,
        LoadDllEvent = 6;
    private const uint Breakpoint = 0x80000003,
        SingleStep = 0x80000004;
    private const uint Continue = 0x00010002,
        ExceptionNotHandled = 0x80010001;
    private const uint ContextDebug = 0x00010010,
        ContextControlAndDebug = ContextDebug | 1;
    private const uint GuardEnable = (1u << 0) | (1u << 2) | (1u << 4);
    private const uint GuardSettings = 63u | (15u << 16) | (15u << 20) | (15u << 24);
    private readonly NativeExtensions.PROCESS_INFORMATION _process;
    private readonly Dictionary<uint, GuardedThread> _threads = new();
    private DebugEvent32 _pending;
    private bool _hasPending,
        _attached = true,
        _prepared,
        _released,
        _disposed;

    private sealed class GuardedThread
    {
        internal IntPtr Handle;
        internal Context32 Original;
        internal bool Suspended;
    }

    internal VtcClientStartup(NativeExtensions.PROCESS_INFORMATION process) => _process = process;

    internal bool TryPrepare(Process process, VtcClientLayout layout, out string error)
    {
        error = null;
        try
        {
            if (IntPtr.Size != 4 || layout == null || !layout.HasGateway)
                throw new InvalidOperationException("A discovered 32-bit VTC client layout is required.");
            IntPtr moduleBase = IntPtr.Zero;
            uint enter = 0,
                command = 0,
                connect = 0;
            bool initialBreakpoint = true;
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 15000)
            {
                if (!WaitForDebugEvent(out _pending, 250))
                {
                    int code = Marshal.GetLastWin32Error();
                    if (code == 121) // ERROR_SEM_TIMEOUT
                        continue;
                    throw new Win32Exception(code, "Could not wait for the VTC startup event.");
                }
                _hasPending = true;
                CloseImageFile(_pending);
                if (_pending.ProcessId != _process.dwProcessId)
                    throw new InvalidOperationException("Received a debug event for a different startup process.");
                uint status = Continue;
                switch (_pending.Code)
                {
                    case CreateProcessEvent:
                        moduleBase = _pending.ImageBase;
                        enter = VtcClientLayout.Address(moduleBase, layout.SysEnterRva);
                        command = VtcClientLayout.Address(moduleBase, layout.SendCommandRva);
                        connect = VtcClientLayout.Address(moduleBase, layout.ConnectCallRva);
                        ArmThread(_pending.ThreadId, enter, command, connect);
                        break;
                    case CreateThreadEvent:
                        ArmThread(_pending.ThreadId, enter, command, connect);
                        break;
                    case ExitThreadEvent:
                        if (_threads.TryGetValue(_pending.ThreadId, out var exited))
                        {
                            NativeExtensions.CloseHandle(exited.Handle);
                            _threads.Remove(_pending.ThreadId);
                        }
                        break;
                    case ExitProcessEvent:
                        throw new InvalidOperationException("The VTC client exited before its startup patches.");
                    case ExceptionEvent:
                        status = ExceptionNotHandled;
                        if (
                            _pending.ExceptionCode == SingleStep
                            && _threads.TryGetValue(_pending.ThreadId, out var thread)
                        )
                        {
                            var context = ReadContext(thread.Handle, ContextControlAndDebug);
                            if (
                                (context.Dr6 & 7) != 0
                                && (context.Eip == enter || context.Eip == command || context.Eip == connect)
                            )
                            {
                                if (!VtcXigncodeDisablePatch.TryInstall(process, _process.hProcess, layout, out error))
                                    return false;
                                if (VtcGatewayRedirectPatch.IsReady(_process.hProcess, moduleBase, layout))
                                {
                                    FreezeAndDetach();
                                    _prepared = true;
                                    return true;
                                }
                                if (context.Eip == connect)
                                    throw new InvalidOperationException("The VTC gateway call has unsupported bytes.");
                                context.Dr6 &= ~7u;
                                context.EFlags |= 1u << 16; // RF: execute this instruction once without retriggering.
                                WriteContext(thread.Handle, context);
                                status = Continue;
                            }
                        }
                        if (_pending.ExceptionCode == Breakpoint && initialBreakpoint && _pending.FirstChance != 0)
                        {
                            initialBreakpoint = false;
                            status = Continue;
                        }
                        if (status == ExceptionNotHandled && _pending.FirstChance == 0)
                            throw new InvalidOperationException(
                                "Unhandled VTC startup exception: " + _pending.ExceptionCode + "."
                            );
                        break;
                }
                ContinuePending(status);
            }
            error = "VTC: timed out waiting for the guarded SDK entry; the new client will be stopped.";
            return false;
        }
        catch (Exception exception)
        {
            error = "VTC startup synchronization: " + exception.Message;
            return false;
        }
    }

    private void ArmThread(uint id, uint enter, uint command, uint connect)
    {
        var handle = OpenThread((1u << 1) | (1u << 3) | (1u << 4), false, id);
        if (handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not guard a new VTC thread.");
        var thread = new GuardedThread { Handle = handle };
        _threads.Add(id, thread);
        var context = ReadContext(handle, ContextDebug);
        thread.Original = context;
        if ((context.Dr7 & 63) != 0)
            throw new InvalidOperationException("VTC startup debug registers are already in use.");
        context.Dr0 = enter;
        context.Dr1 = command;
        context.Dr2 = connect;
        context.Dr6 &= ~7u;
        context.Dr7 = (context.Dr7 & ~GuardSettings) | GuardEnable;
        WriteContext(handle, context);
        var actual = ReadContext(handle, ContextDebug);
        if (
            actual.Dr0 != enter
            || actual.Dr1 != command
            || actual.Dr2 != connect
            || (actual.Dr7 & GuardSettings) != GuardEnable
        )
            throw new InvalidOperationException("Could not verify the VTC execution guards.");
    }

    private void FreezeAndDetach()
    {
        foreach (var thread in _threads.Values)
        {
            if (NativeExtensions.SuspendThread(thread.Handle) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not hold all VTC threads for patching.");
            thread.Suspended = true;
            var context = ReadContext(thread.Handle, ContextDebug);
            context.Dr0 = thread.Original.Dr0;
            context.Dr1 = thread.Original.Dr1;
            context.Dr2 = thread.Original.Dr2;
            context.Dr6 = thread.Original.Dr6;
            context.Dr7 = thread.Original.Dr7;
            WriteContext(thread.Handle, context);
            var restored = ReadContext(thread.Handle, ContextDebug);
            if (
                restored.Dr0 != context.Dr0
                || restored.Dr1 != context.Dr1
                || restored.Dr2 != context.Dr2
                || (restored.Dr7 & GuardSettings) != (context.Dr7 & GuardSettings)
            )
                throw new InvalidOperationException("Could not remove the VTC execution guards.");
        }
        ContinuePending(Continue);
        if (!DebugActiveProcessStop(_process.dwProcessId))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not detach the VTC startup debugger.");
        _attached = false;
    }

    internal bool TryResume(out string error)
    {
        error = null;
        if (!_prepared || _disposed || _released)
        {
            error = "VTC startup patches have not been prepared.";
            return false;
        }
        foreach (var thread in _threads.Values)
        {
            if (!thread.Suspended)
                continue;
            if (NativeExtensions.ResumeThread(thread.Handle) == uint.MaxValue)
            {
                error = "Could not resume the patched VTC client.";
                return false;
            }
            thread.Suspended = false;
        }
        _released = true;
        return true;
    }

    private static Context32 ReadContext(IntPtr thread, uint flags)
    {
        var context = new Context32 { Flags = flags };
        if (!GetThreadContext(thread, ref context))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read a VTC thread context.");
        return context;
    }

    private static void WriteContext(IntPtr thread, Context32 context)
    {
        if (!SetThreadContext(thread, ref context))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set a VTC thread context.");
    }

    private void ContinuePending(uint status)
    {
        if (!ContinueDebugEvent(_pending.ProcessId, _pending.ThreadId, status))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not continue the VTC startup event.");
        _hasPending = false;
    }

    private static void CloseImageFile(DebugEvent32 debugEvent)
    {
        if (
            (debugEvent.Code == CreateProcessEvent || debugEvent.Code == LoadDllEvent)
            && debugEvent.File != IntPtr.Zero
        )
            NativeExtensions.CloseHandle(debugEvent.File);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (!_released)
        {
            bool exitPending = _attached && _hasPending && _pending.Code == ExitProcessEvent;
            bool terminating =
                exitPending
                || TerminateProcess(_process.hProcess, 1)
                || NativeExtensions.WaitForSingleObject(_process.hProcess, 0) == 0;
            if (terminating && _attached)
            {
                if (_hasPending && ContinueDebugEvent(_pending.ProcessId, _pending.ThreadId, Continue))
                {
                    _hasPending = false;
                    if (exitPending)
                        _attached = false;
                }
                if (_attached)
                {
                    var timer = Stopwatch.StartNew();
                    while (timer.ElapsedMilliseconds < 2000 && WaitForDebugEvent(out var debugEvent, 100))
                    {
                        CloseImageFile(debugEvent);
                        ContinueDebugEvent(debugEvent.ProcessId, debugEvent.ThreadId, Continue);
                        if (debugEvent.Code == ExitProcessEvent)
                            break;
                    }
                    DebugActiveProcessStop(_process.dwProcessId);
                }
            }
        }
        foreach (var thread in _threads.Values)
            NativeExtensions.CloseHandle(thread.Handle);
        _threads.Clear();
        NativeExtensions.CloseHandle(_process.hThread);
        NativeExtensions.CloseHandle(_process.hProcess);
    }

    [StructLayout(LayoutKind.Explicit, Size = 716)]
    private struct Context32
    {
        [FieldOffset(0)]
        internal uint Flags;

        [FieldOffset(4)]
        internal uint Dr0;

        [FieldOffset(8)]
        internal uint Dr1;

        [FieldOffset(12)]
        internal uint Dr2;

        [FieldOffset(20)]
        internal uint Dr6;

        [FieldOffset(24)]
        internal uint Dr7;

        [FieldOffset(184)]
        internal uint Eip;

        [FieldOffset(192)]
        internal uint EFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    private struct DebugEvent32
    {
        [FieldOffset(0)]
        internal uint Code;

        [FieldOffset(4)]
        internal uint ProcessId;

        [FieldOffset(8)]
        internal uint ThreadId;

        [FieldOffset(12)]
        internal IntPtr File;

        [FieldOffset(12)]
        internal uint ExceptionCode;

        [FieldOffset(24)]
        internal IntPtr ImageBase;

        [FieldOffset(92)]
        internal uint FirstChance;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WaitForDebugEvent(out DebugEvent32 debugEvent, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ContinueDebugEvent(uint processId, uint threadId, uint status);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DebugActiveProcessStop(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetThreadContext(IntPtr thread, ref Context32 context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetThreadContext(IntPtr thread, ref Context32 context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint access, bool inherit, uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
