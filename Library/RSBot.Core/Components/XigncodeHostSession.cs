using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RSBot.Core.Components;

/// <summary>Owns one native SDK process and one ordered IPC stream for a game session.</summary>
internal sealed class XigncodeHostSession : IDisposable
{
    private const int MaximumFrame = 65536;
    private readonly NamedPipeServerStream _pipe;
    private readonly CancellationTokenSource _cancel = new();
    private readonly Channel<(uint Kind, byte[] Data)> _outgoing = Channel.CreateBounded<(uint, byte[])>(
        new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }
    );
    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<byte[]> _onReply;
    private readonly Action<string> _onFailure;
    private readonly Action<string> _onDiagnostic;
    private readonly string _pipeName;
    private readonly object _processGate = new();
    private Process _process;
    private int _disposed;
    private int _failed;
    private volatile VtcClientLayout _clientLayout;

    internal VtcClientLayout ClientLayout => _clientLayout;

    internal XigncodeHostSession(Action<byte[]> onReply, Action<string> onFailure, Action<string> onDiagnostic)
    {
        _onReply = onReply;
        _onFailure = onFailure;
        _onDiagnostic = onDiagnostic;
        _pipeName = $"RSBot.Xigncode.{Environment.ProcessId}.{Guid.NewGuid():N}";
        _pipe = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );
    }

    internal async Task StartAsync(
        string hostExecutable,
        string gameExecutable,
        string sdkDirectory,
        string identity,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(hostExecutable))
            throw new FileNotFoundException("XigncodeHost.exe is missing. Run scripts/build.ps1.", hostExecutable);
        if (!File.Exists(gameExecutable) || !File.Exists(Path.Combine(sdkDirectory, "x3.xem")))
            throw new FileNotFoundException("VTC executable or original XIGNCODE SDK is missing.");

        var info = new ProcessStartInfo(hostExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(gameExecutable)),
        };
        info.ArgumentList.Add(_pipeName);
        info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add(Path.GetFullPath(gameExecutable));
        info.ArgumentList.Add(Path.GetFullPath(sdkDirectory));

        Task connected;
        lock (_processGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_process != null)
                throw new InvalidOperationException("This SDK session has already been started.");
            _process = new Process { StartInfo = info, EnableRaisingEvents = true };
            _process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null)
                    _onDiagnostic(args.Data);
            };
            _process.Exited += (_, _) => _ = ObserveExitAsync();
            connected = _pipe.WaitForConnectionAsync(_cancel.Token);
            if (!_process.Start())
                throw new IOException("Could not start XIGNCODE host.");
            _process.BeginErrorReadLine();
        }
        _ = RunTransportAsync(connected, identity ?? string.Empty);
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(90), cancellationToken).ConfigureAwait(false);
    }

    private async Task RunTransportAsync(Task connected, string identity)
    {
        try
        {
            await connected.ConfigureAwait(false);
            if (!_outgoing.Writer.TryWrite((9, Encoding.UTF8.GetBytes(identity))))
                throw new IOException("Could not queue SDK initialization.");
            var writer = WriteLoopAsync();
            var reader = ReadLoopAsync();
            await Task.WhenAny(writer, reader).ConfigureAwait(false);
            _cancel.Cancel();
            await Task.WhenAll(writer, reader).ConfigureAwait(false);
            Fail("XIGNCODE IPC connection closed.");
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0) { }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0) { }
        catch (Exception error)
        {
            _onDiagnostic("transport: " + error.Message);
            Fail("XIGNCODE host: " + error.Message);
        }
    }

    private async Task ObserveExitAsync()
    {
        // Give the pipe reader a chance to deliver a more precise native failure frame.
        await Task.Delay(100).ConfigureAwait(false);
        if (Volatile.Read(ref _disposed) != 0)
            return;
        int? code = null;
        try
        {
            code = _process?.ExitCode;
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        Fail("XIGNCODE host exited (code " + code + ").");
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var frame in _outgoing.Reader.ReadAllAsync(_cancel.Token).ConfigureAwait(false))
        {
            var header = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(header, frame.Kind);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)frame.Data.Length);
            await _pipe.WriteAsync(header, _cancel.Token).ConfigureAwait(false);
            await _pipe.WriteAsync(frame.Data, _cancel.Token).ConfigureAwait(false);
            await _pipe.FlushAsync(_cancel.Token).ConfigureAwait(false);
        }
    }

    private async Task ReadLoopAsync()
    {
        var header = new byte[8];
        while (!_cancel.IsCancellationRequested)
        {
            await _pipe.ReadExactlyAsync(header, _cancel.Token).ConfigureAwait(false);
            var kind = BinaryPrimitives.ReadUInt32LittleEndian(header);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
            if (length > MaximumFrame)
                throw new InvalidDataException("Invalid XIGNCODE IPC frame size.");
            var data = new byte[(int)length];
            await _pipe.ReadExactlyAsync(data, _cancel.Token).ConfigureAwait(false);
            switch (kind)
            {
                case 10:
                    if (_clientLayout != null || _ready.Task.IsCompleted)
                        throw new InvalidDataException("Unexpected duplicate client discovery data.");
                    _clientLayout = VtcClientLayout.Parse(data);
                    break;
                case 2:
                    if (_clientLayout == null || data.Length != 4 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 1)
                        throw new InvalidDataException("Unsupported XIGNCODE host protocol.");
                    _ready.TrySetResult(true);
                    break;
                case 4:
                    if (!_ready.Task.IsCompletedSuccessfully || data.Length > 4000)
                        throw new InvalidDataException("Invalid XIGNCODE Probe reply.");
                    _onReply(data);
                    break;
                case 5:
                    throw new IOException(Encoding.UTF8.GetString(data));
                case 7:
                    _onDiagnostic(Encoding.UTF8.GetString(data));
                    break;
                default:
                    throw new InvalidDataException("Unknown XIGNCODE IPC message.");
            }
        }
    }

    internal bool IsReady =>
        _clientLayout != null
        && _ready.Task.IsCompletedSuccessfully
        && Volatile.Read(ref _disposed) == 0
        && Volatile.Read(ref _failed) == 0;

    internal Task WaitUntilReadyAsync() => _ready.Task;

    internal bool TrySetIdentity(string identity)
    {
        var bytes = Encoding.UTF8.GetBytes(identity ?? string.Empty);
        if (bytes.Length > 1024)
            throw new ArgumentException("Client identity is too long.", nameof(identity));
        if (!IsReady)
            return false;
        if (_outgoing.Writer.TryWrite((8, bytes)))
            return true;
        Fail("XIGNCODE input queue is full.");
        return false;
    }

    internal bool TrySubmit(byte[] challenge)
    {
        if (challenge == null || challenge.Length != 4000)
            throw new ArgumentException("Probe challenge must contain 4000 bytes.", nameof(challenge));
        if (!IsReady)
            return false;
        // Enqueue submission, not a request/response wait: fragments may have no callback.
        if (_outgoing.Writer.TryWrite((3, (byte[])challenge.Clone())))
            return true;
        Fail("XIGNCODE input queue is full.");
        return false;
    }

    private void Fail(string message)
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _failed, 1) != 0)
            return;
        _ready.TrySetException(new IOException(message));
        _onFailure(message);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _ready.TrySetCanceled();
        _outgoing.Writer.TryComplete();
        _cancel.Cancel();
        _pipe.Dispose();
        lock (_processGate)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            _process?.Dispose();
        }
    }
}
