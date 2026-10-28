using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RSBot.Core.Components;
using RSBot.Core.Event;

namespace RSBot.Core.Network;

public class Proxy
{
    private enum ConnectionTarget
    {
        None = 0,
        Gateway = 1,
        Agent = 2,
    }

    private ConnectionTarget _connectionTarget;
    private readonly object _probeGate = new();
    private XigncodeHostSession _probeHost;
    private volatile bool _shutdown;

    // Avoid log spam when the remote end drops quickly or connection attempts fail repeatedly.
    private DateTime _lastGatewayDisconnectLogUtc;
    private DateTime _lastAgentDisconnectLogUtc;

    /// <summary>
    ///     Gets a value indicating whether [client connected].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [client connected]; otherwise, <c>false</c>.
    /// </value>
    public bool ClientConnected { get; private set; }

    /// <summary>
    ///     Gets the port.
    /// </summary>
    /// <value>
    ///     The port.
    /// </value>
    public ushort Port { get; private set; }

    /// <summary>
    ///     Gets the client.
    /// </summary>
    /// <value>
    ///     The client.
    /// </value>
    public Client Client { get; private set; }

    /// <summary>
    ///     Gets the server.
    /// </summary>
    /// <value>
    ///     The server.
    /// </value>
    public Server Server { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether [connected to gatewayserver].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [connected to gatewayserver]; otherwise, <c>false</c>.
    /// </value>
    public bool IsConnectedToGatewayserver { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether [connected to agentserver].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [connected to agentserver]; otherwise, <c>false</c>.
    /// </value>
    public bool IsConnectedToAgentserver { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether we are in the Gateway -> Agent switch phase.
    ///     In this phase, the Gateway connection is expected to drop and should not trigger relogin/shutdown logic.
    /// </summary>
    public bool IsSwitchingToAgentserver { get; private set; }

    /// <summary>
    ///     Gets the session.
    /// </summary>
    /// <value>
    ///     The session.
    /// </value>
    public uint Token { get; internal set; }

    /// <summary>
    ///     Starts the specified client port.
    /// </summary>
    /// <param name="clientPort">The client port.</param>
    /// <param name="gatewayIp">The gateway ip.</param>
    /// <param name="gatewayPort">The gateway port.</param>
    public void Start(ushort clientPort, string gatewayIp, ushort gatewayPort)
    {
        Port = clientPort;
        _gatewayIp = gatewayIp;
        _gatewayPort = gatewayPort;

        if (!Game.Clientless)
        {
            CreateNewClientInstance(clientPort);
            Port = Client.BoundPort;
        }
        if (Game.ClientType == GameClientType.VTC_Game)
            StartVtcProtection();
        else if (Game.Clientless)
            ConnectToGatewayserver();
    }

    /// <summary>
    ///     Shutdowns this instance.
    /// </summary>
    public void Shutdown()
    {
        Client client;
        Server server;
        XigncodeHostSession host;
        ConnectionTarget disconnectedFrom;
        lock (_probeGate)
        {
            if (_shutdown)
                return;
            disconnectedFrom =
                IsConnectedToAgentserver ? ConnectionTarget.Agent
                : IsConnectedToGatewayserver ? ConnectionTarget.Gateway
                : _connectionTarget;
            _shutdown = true;
            _connectionTarget = ConnectionTarget.None;
            IsConnectedToGatewayserver = false;
            IsConnectedToAgentserver = false;
            IsSwitchingToAgentserver = false;
            ClientConnected = false;
            if (ReferenceEquals(Kernel.Proxy, this))
            {
                Game.Ready = false;
                Game.Started = false;
            }
            host = _probeHost;
            _probeHost = null;
            client = Client;
            Client = null;
            server = Server;
            Server = null;
        }

        host?.Dispose();
        client?.Shutdown();
        server?.Disconnect();

        _agentIp = null;
        _agentPort = 0;
        _gatewayIp = null;
        _gatewayPort = 0;

        if (!ReferenceEquals(Kernel.Proxy, this))
            return;
        if (disconnectedFrom == ConnectionTarget.Agent)
        {
            Log.Warn("Disconnected from game server!");
            EventManager.FireEvent("OnAgentServerDisconnected");
        }
        else if (disconnectedFrom == ConnectionTarget.Gateway)
        {
            Log.Debug("Disconnected from login server!");
            EventManager.FireEvent("OnGatewayServerDisconnected");
        }
    }

    private async void StartVtcProtection()
    {
        XigncodeHostSession host = null;
        try
        {
            host = new XigncodeHostSession(
                data => OnProbeReply(host, data),
                message => OnProbeFailure(host, message),
                message => Log.Debug("VTC XIGNCODE: " + message)
            );
            bool stopped;
            lock (_probeGate)
            {
                stopped = _shutdown;
                if (!stopped)
                    _probeHost = host;
            }
            if (stopped)
            {
                host.Dispose();
                return;
            }
            var directory = GlobalConfig.Get<string>("RSBot.SilkroadDirectory");
            await host.StartAsync(
                    Path.Combine(Kernel.BasePath, "XigncodeHost.exe"),
                    Path.Combine(directory, GlobalConfig.Get<string>("RSBot.SilkroadExecutable")),
                    Path.Combine(directory, "XIGNCODE"),
                    Game.Clientless
                        ? GlobalConfig.Get<string>("RSBot.General.AutoLoginAccountUsername") ?? string.Empty
                        : string.Empty
                )
                .ConfigureAwait(false);
            lock (_probeGate)
            {
                if (_shutdown || !ReferenceEquals(_probeHost, host) || !ReferenceEquals(Kernel.Proxy, this))
                    return;
                Log.Debug("VTC: XIGNCODE session host ready.");
            }
            TryConnectVtcGateway();
        }
        catch (Exception error)
        {
            OnProbeFailure(host, error.Message);
        }
    }

    private void TryConnectVtcGateway()
    {
        lock (_probeGate)
        {
            if (
                _shutdown
                || _probeHost == null
                || !_probeHost.IsReady
                || !ReferenceEquals(Kernel.Proxy, this)
                || (!Game.Clientless && !ClientConnected)
                || _connectionTarget != ConnectionTarget.None
            )
                return;
            ConnectToGatewayserver();
        }
    }

    internal async Task<VtcClientLayout> WaitForVtcProtectionAsync()
    {
        XigncodeHostSession host;
        lock (_probeGate)
        {
            if (_shutdown || _probeHost == null)
                throw new IOException("VTC XIGNCODE session host is unavailable.");
            host = _probeHost;
        }
        await host.WaitUntilReadyAsync().ConfigureAwait(false);
        lock (_probeGate)
        {
            if (
                _shutdown
                || !host.IsReady
                || !ReferenceEquals(_probeHost, host)
                || !ReferenceEquals(Kernel.Proxy, this)
            )
                throw new OperationCanceledException("VTC session was replaced during SDK initialization.");
            return host.ClientLayout;
        }
    }

    internal bool TrySwitchToClientless(out string error)
    {
        Client previous;
        lock (_probeGate)
        {
            error = null;
            if (
                _shutdown
                || !ReferenceEquals(Kernel.Proxy, this)
                || !IsConnectedToAgentserver
                || Server == null
                || !Server.IsConnected
            )
            {
                error = "VTC: an active game-server session is required for this switch.";
                return false;
            }
            if (_probeHost == null || !_probeHost.IsReady)
            {
                error =
                    "VTC: restart the client with this build so its XIGNCODE session host is initialized before login.";
                return false;
            }

            Game.Clientless = true;
            ClientConnected = false;
            previous = Client;
            Client = null;
            if (previous != null)
            {
                previous.Connected -= Client_OnConnected;
                previous.Disconnected -= Client_OnDisconnected;
                previous.PacketReceived -= Client_OnPacketReceived;
            }
        }
        previous?.Shutdown();
        Log.Debug("VTC: switched to clientless; the existing XIGNCODE session host is retained.");
        return true;
    }

    private void OnProbeReply(XigncodeHostSession host, byte[] data)
    {
        lock (_probeGate)
        {
            if (
                _shutdown
                || !ReferenceEquals(_probeHost, host)
                || !ReferenceEquals(Kernel.Proxy, this)
                || Server == null
                || !Server.IsConnected
            )
                return;
            var packet = new Packet(0x2110);
            packet.WriteByte(2);
            packet.WriteBytes(data);
            PacketManager.SendPacket(packet, PacketDestination.Server, this);
        }
    }

    private void OnProbeFailure(XigncodeHostSession host, string message)
    {
        bool failedBeforeConnect;
        lock (_probeGate)
        {
            if (_shutdown || !ReferenceEquals(_probeHost, host))
                return;
            failedBeforeConnect =
                _connectionTarget == ConnectionTarget.None && !IsConnectedToAgentserver && !IsConnectedToGatewayserver;
        }
        Log.Error("VTC XIGNCODE: " + message);
        Shutdown();
        if (failedBeforeConnect && ReferenceEquals(Kernel.Proxy, this))
        {
            Game.Clientless = false;
            EventManager.FireEvent("OnClientDisconnected");
        }
    }

    private bool HandleVtcProbe(Packet packet, PacketDestination destination)
    {
        XigncodeHostSession host;
        lock (_probeGate)
            host = _probeHost;
        if (host == null || packet.Opcode != 0x2110)
            return false;
        if (destination == PacketDestination.Server)
        {
            Log.Debug("VTC: consumed game-client Probe reply; the session host owns 0x2110.");
            return true;
        }

        var bytes = packet.GetBytes();
        if (bytes.Length != 4001 || bytes[0] != 1)
            OnProbeFailure(host, "Unsupported incoming 0x2110 framing.");
        else
        {
            var challenge = new byte[4000];
            Buffer.BlockCopy(bytes, 1, challenge, 0, challenge.Length);
            if (!host.TrySubmit(challenge))
                OnProbeFailure(host, "Could not queue Probe challenge.");
        }
        return true;
    }

    /// <summary>
    ///     Connects to gatewayserver.
    /// </summary>
    private void ConnectToGatewayserver()
    {
        Log.Notify($"Connecting to gateway server [{_gatewayIp}:{_gatewayPort}]...");

        CreateNewServerInstance();

        _connectionTarget = ConnectionTarget.Gateway;

        Server.Connect(_gatewayIp, _gatewayPort);
    }

    /// <summary>
    ///     Connects to agentserver.
    /// </summary>
    private void ConnectToAgentserver()
    {
        Log.Notify($"Connecting to agentserver [{_agentIp}:{_agentPort}]...");
        CreateNewServerInstance();

        _connectionTarget = ConnectionTarget.Agent;

        Server.Connect(_agentIp, _agentPort);
    }

    /// <summary>
    ///     Initializes the new server instance.
    /// </summary>
    private void CreateNewServerInstance()
    {
        // We switch between gateway and agent by replacing the Server instance.
        // Important: detach handlers before disconnecting the old server; otherwise its delayed
        // disconnect can be misclassified as the new connection target and trigger relogin.
        var oldServer = Server;
        if (oldServer != null)
        {
            oldServer.Connected -= Server_OnConnected;
            oldServer.Disconnected -= Server_OnDisconnected;
            oldServer.PacketReceived -= Server_OnPacketReceived;

            if (oldServer.IsConnected)
                oldServer.Disconnect();
        }

        IsConnectedToGatewayserver = false;
        IsConnectedToAgentserver = false;
        _connectionTarget = ConnectionTarget.None;

        Server = new Server();
        Server.Connected += Server_OnConnected;
        Server.Disconnected += Server_OnDisconnected;
        Server.PacketReceived += Server_OnPacketReceived;
    }

    /// <summary>
    ///     Creates the new client instance.
    /// </summary>
    /// <param name="clientPort">The client port.</param>
    private void CreateNewClientInstance(ushort clientPort)
    {
        Client = new Client();
        Client.Connected += Client_OnConnected;
        Client.PacketReceived += Client_OnPacketReceived;
        Client.Disconnected += Client_OnDisconnected;
        Client.Listen(clientPort);
    }

    /// <summary>
    ///     Handle received packet
    /// </summary>
    /// <param name="packet">The packet</param>
    private void HandleReceivedPacket(Packet packet, PacketDestination destination)
    {
        if (_shutdown || HandleVtcProbe(packet, destination))
            return;
        //if(packet.Opcode != 0x2002)
        //   Log.Notify(packet.ToString());

        try
        {
            packet = PacketManager.CallHook(packet, destination);
        }
        catch (Exception e)
        {
            Log.Fatal(e);
        }
        finally
        {
            if (packet != null && !_shutdown && (destination != PacketDestination.Server || UpdateVtcIdentity(packet)))
                try
                {
                    PacketManager.SendPacket(packet, destination, this);

                    packet.SeekRead(0, SeekOrigin.Begin);

                    PacketManager.CallHandler(packet, destination);
                    PacketManager.CallCallback(packet);
                }
                catch (Exception e)
                {
                    Log.Fatal(e);
                }
        }
    }

    #region Fields

    private string _agentIp;
    private ushort _agentPort;
    private string _gatewayIp;
    private ushort _gatewayPort;

    #endregion Fields

    #region Event Listeners

    #region Client

    /// <summary>
    ///     Fired when the client connected.
    /// </summary>
    private void Client_OnConnected()
    {
        Log.Notify("A new silkroad client connected.");

        EventManager.FireEvent("OnClientConnected");

        ClientConnected = true;

        if (Game.ClientType == GameClientType.VTC_Game)
            TryConnectVtcGateway();
        else if (!IsConnectedToGatewayserver && !IsConnectedToAgentserver)
            ConnectToGatewayserver();
    }

    /// <summary>
    ///     Fired when the client disconnected.
    /// </summary>
    private void Client_OnDisconnected()
    {
        ClientConnected = false;
    }

    /// <summary>
    ///     Fired when a client packet was received
    /// </summary>
    /// <param name="packet">The packet.</param>
    private bool UpdateVtcIdentity(Packet packet)
    {
        XigncodeHostSession host;
        lock (_probeGate)
            host = _probeHost;
        if (host != null && (packet.Opcode == 0x6102 || packet.Opcode == 0x610A))
        {
            var bytes = packet.GetBytes();
            if (bytes.Length >= 3)
            {
                var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(1, 2));
                if (
                    length > 0
                    && length <= 1024
                    && bytes.Length >= 3 + length
                    && !host.TrySetIdentity(Encoding.UTF8.GetString(bytes, 3, length))
                )
                {
                    OnProbeFailure(host, "Could not update SDK client identity.");
                    return false;
                }
            }
        }
        return true;
    }

    private void Client_OnPacketReceived(Packet packet)
    {
        if (_shutdown || Game.Clientless)
            return;
        if (IsConnectedToAgentserver && packet.Opcode == 0x6100)
            return;

        HandleReceivedPacket(packet, PacketDestination.Server);
        if (!_shutdown && !Game.Clientless)
            EventManager.FireEvent("OnClientPacketReceive", packet);
    }

    #endregion Client

    #region Server

    /// <summary>
    ///     Fired when a server packet was received
    /// </summary>
    /// <param name="packet">The packet.</param>
    private void Server_OnPacketReceived(Packet packet)
    {
        HandleReceivedPacket(packet, PacketDestination.Client);

        EventManager.FireEvent("OnServerPacketReceive", packet);
    }

    /// <summary>
    ///     Sets the agentserver address.
    /// </summary>
    /// <param name="ip">The ip.</param>
    /// <param name="port">The port.</param>
    internal void SetAgentserverAddress(string ip, ushort port)
    {
        _agentIp = ip;
        _agentPort = port;

        IsSwitchingToAgentserver = true;
        ConnectToAgentserver();
    }

    /// <summary>
    ///     Fired when the server disconnected
    /// </summary>
    private void Server_OnDisconnected()
    {
        ConnectionTarget disconnectedFrom;
        XigncodeHostSession host;
        bool writeLog;
        lock (_probeGate)
        {
            if (_shutdown)
                return;
            disconnectedFrom =
                IsConnectedToAgentserver ? ConnectionTarget.Agent
                : IsConnectedToGatewayserver ? ConnectionTarget.Gateway
                : _connectionTarget;
            if (disconnectedFrom == ConnectionTarget.None)
                return;

            var wasConnected =
                disconnectedFrom == ConnectionTarget.Agent ? IsConnectedToAgentserver : IsConnectedToGatewayserver;
            _connectionTarget = ConnectionTarget.None;
            IsConnectedToAgentserver = false;
            IsConnectedToGatewayserver = false;
            if (disconnectedFrom == ConnectionTarget.Gateway && IsSwitchingToAgentserver)
                return;
            IsSwitchingToAgentserver = false;

            var now = DateTime.UtcNow;
            var lastLog =
                disconnectedFrom == ConnectionTarget.Agent ? _lastAgentDisconnectLogUtc : _lastGatewayDisconnectLogUtc;
            writeLog = wasConnected || (now - lastLog).TotalSeconds >= 3;
            if (writeLog)
            {
                if (disconnectedFrom == ConnectionTarget.Agent)
                    _lastAgentDisconnectLogUtc = now;
                else
                    _lastGatewayDisconnectLogUtc = now;
            }
            if (disconnectedFrom == ConnectionTarget.Agent && ReferenceEquals(Kernel.Proxy, this))
            {
                Game.Ready = false;
                Game.Started = false;
            }
            host = _probeHost;
            _probeHost = null;
        }
        host?.Dispose();
        if (!ReferenceEquals(Kernel.Proxy, this))
            return;
        if (writeLog)
        {
            if (disconnectedFrom == ConnectionTarget.Agent)
                Log.Warn("Disconnected from game server!");
            else
                Log.Debug("Disconnected from login server!");
        }
        if (!ReferenceEquals(Kernel.Proxy, this))
            return;
        EventManager.FireEvent(
            disconnectedFrom == ConnectionTarget.Agent ? "OnAgentServerDisconnected" : "OnGatewayServerDisconnected"
        );
    }

    /// <summary>
    ///     Fired when the server conntected
    /// </summary>
    private void Server_OnConnected()
    {
        if (_connectionTarget == ConnectionTarget.Gateway)
        {
            IsConnectedToGatewayserver = true;
            IsConnectedToAgentserver = false;
            EventManager.FireEvent("OnGatewayServerConntected");
        }
        else if (_connectionTarget == ConnectionTarget.Agent)
        {
            IsConnectedToGatewayserver = false;
            IsConnectedToAgentserver = true;
            IsSwitchingToAgentserver = false;
            EventManager.FireEvent("OnAgentServerConnected");
        }
    }

    #endregion Server

    #endregion Event Listeners
}
