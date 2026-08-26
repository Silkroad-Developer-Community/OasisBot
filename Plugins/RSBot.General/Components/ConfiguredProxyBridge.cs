using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core.Extensions;

namespace RSBot.General.Components;

/// <summary>
///     Exposes a loopback HTTP proxy for browser components that cannot authenticate with SOCKS proxies.
///     Each outgoing connection uses the current RSBot.Network.Proxy configuration.
/// </summary>
internal sealed class ConfiguredProxyBridge
{
    private const int MaxHeaderSize = 64 * 1024;
    private static readonly Lazy<ConfiguredProxyBridge> LazyInstance = new(() => new ConfiguredProxyBridge());

    private readonly TcpListener _listener;
    private readonly object _proxyConfigLock = new();
    private ProxyConfig _proxyConfig;

    private ConfiguredProxyBridge()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}");
        _ = AcceptConnectionsAsync();
    }

    public static ConfiguredProxyBridge Instance => LazyInstance.Value;

    public Uri Address { get; }

    public Uri Configure(ProxyConfig proxyConfig)
    {
        lock (_proxyConfigLock)
            _proxyConfig = proxyConfig;

        return Address;
    }

    private async Task AcceptConnectionsAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                NetworkStream clientStream = client.GetStream();
                byte[] request = await ReadRequestHeaderAsync(clientStream);
                if (
                    !TryParseDestination(request, out string host, out int port, out bool isConnect, out byte[] payload)
                )
                {
                    await WriteErrorAsync(clientStream, "400 Bad Request");
                    return;
                }

                ProxyConfig proxyConfig;
                lock (_proxyConfigLock)
                {
                    proxyConfig = _proxyConfig;
                }

                proxyConfig.Ip = host;
                proxyConfig.Port = port;

                using var destination = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                if (!await destination.ConnectViaProxy(proxyConfig, logConnection: false))
                {
                    await WriteErrorAsync(clientStream, "502 Bad Gateway");
                    return;
                }

                using var destinationStream = new NetworkStream(destination, ownsSocket: false);
                if (isConnect)
                {
                    byte[] established = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                    await clientStream.WriteAsync(established);
                }
                else
                {
                    await destinationStream.WriteAsync(payload);
                }

                Task upload = clientStream.CopyToAsync(destinationStream);
                Task download = destinationStream.CopyToAsync(clientStream);
                await Task.WhenAny(upload, download);
            }
            catch (Exception ex) when (ex is IOException or SocketException or SocketProxyException)
            {
                try
                {
                    await WriteErrorAsync(client.GetStream(), "502 Bad Gateway");
                }
                catch
                {
                    // The browser already closed the connection.
                }
            }
        }
    }

    private static async Task<byte[]> ReadRequestHeaderAsync(NetworkStream stream)
    {
        using var request = new MemoryStream();
        var buffer = new byte[4096];

        while (request.Length < MaxHeaderSize)
        {
            int read = await stream.ReadAsync(buffer);
            if (read == 0)
                break;

            request.Write(buffer, 0, read);
            if (FindHeaderEnd(request.GetBuffer(), (int)request.Length) >= 0)
                return request.ToArray();
        }

        throw new IOException("The local proxy received an incomplete or oversized HTTP request header.");
    }

    private static bool TryParseDestination(
        byte[] request,
        out string host,
        out int port,
        out bool isConnect,
        out byte[] payload
    )
    {
        host = null;
        port = 0;
        isConnect = false;
        payload = null;

        int headerEnd = FindHeaderEnd(request, request.Length);
        if (headerEnd < 0)
            return false;

        string header = Encoding.ASCII.GetString(request, 0, headerEnd);
        string[] lines = header.Split(["\r\n"], StringSplitOptions.None);
        string[] requestLine = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length != 3)
            return false;

        isConnect = string.Equals(requestLine[0], "CONNECT", StringComparison.OrdinalIgnoreCase);
        if (isConnect)
        {
            if (!TryParseAuthority(requestLine[1], 443, out host, out port))
                return false;

            payload = Array.Empty<byte>();
            return true;
        }

        if (!Uri.TryCreate(requestLine[1], UriKind.Absolute, out Uri requestUri))
            return false;

        host = requestUri.Host;
        port = requestUri.Port;
        string pathAndQuery = string.IsNullOrEmpty(requestUri.PathAndQuery) ? "/" : requestUri.PathAndQuery;
        string rewrittenHeader =
            $"{requestLine[0]} {pathAndQuery} {requestLine[2]}\r\n" + string.Join("\r\n", lines.Skip(1));
        byte[] rewrittenHeaderBytes = Encoding.ASCII.GetBytes(rewrittenHeader);
        int bodyLength = request.Length - headerEnd;
        payload = new byte[rewrittenHeaderBytes.Length + bodyLength];
        rewrittenHeaderBytes.CopyTo(payload, 0);
        request.AsSpan(headerEnd, bodyLength).CopyTo(payload.AsSpan(rewrittenHeaderBytes.Length));
        return true;
    }

    private static bool TryParseAuthority(string authority, int defaultPort, out string host, out int port)
    {
        host = null;
        port = 0;
        if (!Uri.TryCreate($"http://{authority}", UriKind.Absolute, out Uri uri))
            return false;

        host = uri.Host;
        port = uri.IsDefaultPort ? defaultPort : uri.Port;
        return !string.IsNullOrWhiteSpace(host) && port is > 0 and <= ushort.MaxValue;
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        for (int index = 3; index < length; index++)
        {
            if (
                buffer[index - 3] == '\r'
                && buffer[index - 2] == '\n'
                && buffer[index - 1] == '\r'
                && buffer[index] == '\n'
            )
            {
                return index + 1;
            }
        }

        return -1;
    }

    private static Task WriteErrorAsync(NetworkStream stream, string status)
    {
        byte[] response = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"
        );
        return stream.WriteAsync(response).AsTask();
    }
}
