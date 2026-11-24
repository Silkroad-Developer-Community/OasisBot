using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Network;
using RSBot.Python.API.Handler;
using RSBot.Python.API.Module;
using RSBot.Python.Manager;
using RSBot.Python.Models;
using RSBot.Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RSBot.Python
{
    public class PythonManager 
    {
        private readonly Timer _eventLoopTimer;
        private int _eventLoopRunning;
        private bool _initialized;

        public PythonManager()
        {
            Executor = new PythonExecutor();
            GuiHost = new PythonGuiHost();
            Runtime = new PythonRuntimeManager(LogMessage);
            ModuleLoader = new ModuleLoader(LogMessage);
            PluginManager = new PythonPluginManager(this, Executor, GuiHost, LogMessage);

            _eventLoopTimer = new Timer(OnEventLoopTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public PythonExecutor Executor { get; }
        public PythonGuiHost GuiHost { get; }
        public PythonRuntimeManager Runtime { get; }
        public ModuleLoader ModuleLoader { get; }
        public PythonPluginManager PluginManager { get; }
        public string CurrentLoadingPluginId { get; internal set; }
        public IReadOnlyList<PythonPluginInfo> FoundPlugins { get; private set; } = [];

        public event Action PluginsChanged;

        public event Action<string> LogAdded;

        public void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                PythonPaths.EnsureDirectories();
                Runtime.Initialize();
                Executor
                    .ExecuteAsync(() =>
                    {
                        ModuleLoader.InitAll(new PythonContext(this, GuiHost, Executor, LogMessage));
                        PythonStubGenerator.Generate(ModuleLoader.Plugins, PythonPaths.StubPath);
                    })
                    .GetAwaiter()
                    .GetResult();

                SubscribeEvents();
                FindPlugins();

                _eventLoopTimer.Change(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
                _initialized = true;

                _ = RestoreSavedPluginsOnStartupAsync();
            }
            catch (Exception ex)
            {
                LogMessage($"Python initialization failed: {ex.Message}");
            }
        }

        private async Task RestoreSavedPluginsOnStartupAsync()
        {
            try
            {
                await RestoreSavedPluginsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogMessage($"Restoring saved Python plugins failed: {ex.Message}");
            }
        }

        public void FindPlugins()
        {
            var discovered = PluginManager.ScanPlugins().ToList();

            foreach (var item in discovered)
            {
                item.IsEnabled = PluginManager.IsLoaded(item.Id);
            }

            FoundPlugins = discovered;
            PluginsChanged?.Invoke();
        }

        public async Task RestoreSavedPluginsAsync()
        {
            var pluginsToEnable = FoundPlugins.Where(x => GlobalConfig.Profile.PythonPlugins.EnabledPlugins.Contains(x.Name)).ToArray();

            foreach (var plugin in pluginsToEnable)
            {
                if (PluginManager.IsLoaded(plugin.Id))
                {
                    plugin.IsEnabled = true;
                    continue;
                }

                await SetEnabledAsync(plugin, true, saveState: false).ConfigureAwait(false);
            }

            PluginsChanged?.Invoke();
        }

        public async Task SetEnabledAsync(PythonPluginInfo plugin, bool enabled, bool saveState = true)
        {
            if (plugin == null)
                return;

            var currentlyLoaded = PluginManager.IsLoaded(plugin.Id);
            if (currentlyLoaded == enabled)
            {
                plugin.IsEnabled = enabled;
                if (saveState)
                    SaveActivePlugins();

                PluginsChanged?.Invoke();
                return;
            }

            try
            {
                if (enabled)
                    await PluginManager.LoadAsync(plugin).ConfigureAwait(false);
                else
                    await PluginManager.UnloadAsync(plugin.Id, plugin.Name).ConfigureAwait(false);

                plugin.IsEnabled = enabled;

                if (saveState)
                    SaveActivePlugins();
            }
            catch (Exception ex)
            {
                plugin.IsEnabled = PluginManager.IsLoaded(plugin.Id);
                var err = ex is PythonException pex ? PythonErrorHandler.FormatException(pex) : ex.Message;
                LogMessage($"[{plugin.Name}] {err}");
            }
            finally
            {
                PluginsChanged?.Invoke();
            }
        }

        public void SaveActivePlugins()
        {
            var activePlugins = FoundPlugins.Where(p => p.IsEnabled).Select(p => p.Name).ToList();
            GlobalConfig.Profile.PythonPlugins.EnabledPlugins = activePlugins;
            GlobalConfig.Save();
        }

        public async Task ReloadAsync()
        {
            try
            {
                LogMessage("Reloading Python plugins...");

                foreach (var plugin in FoundPlugins.Where(p => p.IsEnabled).ToArray())
                {
                    try
                    {
                        await PluginManager.UnloadAsync(plugin.Id, plugin.Name).ConfigureAwait(false);
                        plugin.IsEnabled = false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to unload '{plugin.Name}': {ex.Message}");
                    }
                }

                GuiHost.Clear();

                FindPlugins();

                await RestoreSavedPluginsAsync().ConfigureAwait(false);

                LogMessage("Python plugins reloaded successfully.");
            }
            catch (Exception ex)
            {
                LogMessage($"Reload failed: {ex.Message}");
            }
            finally
            {
                PluginsChanged?.Invoke();
            }
        }

        private void SubscribeEvents()
        {
            EventManager.SubscribeEvent("OnEnterGame", new Action(() => PluginManager.CallEvent("on_enter_game")));
            EventManager.SubscribeEvent("OnAgentServerDisconnected", new Action(() => PluginManager.CallEvent("on_disconnect")));
            EventManager.SubscribeEvent("OnTeleportStart", new Action(() => PluginManager.CallEvent("on_teleported", 1)));
            EventManager.SubscribeEvent("OnTeleportComplete", new Action(() => PluginManager.CallEvent("on_teleported", 2)));
            EventManager.SubscribeEvent("OnClientPacketReceive", new Action<Packet>(packet => ForwardPacket("on_packet_from_client", packet)));
            EventManager.SubscribeEvent("OnServerPacketReceive", new Action<Packet>(packet => ForwardPacket("on_packet_from_server", packet)));
        }

        private void ForwardPacket(string eventName, Packet packet)
        {
            if (packet == null)
                return;

            try
            {
                var type = packet.GetType();
                var opcodeValue =
                    type.GetProperty("Opcode", BindingFlags.Instance | BindingFlags.Public)?.GetValue(packet)
                    ?? type.GetProperty("OpCode", BindingFlags.Instance | BindingFlags.Public)?.GetValue(packet)
                    ?? 0;
                var opcode = Convert.ToUInt16(opcodeValue);

                byte[] data = null;
                foreach (var methodName in new[] { "GetBytes", "ToArray" })
                {
                    var method = type.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        Type.EmptyTypes,
                        null
                    );
                    if (method?.Invoke(packet, null) is byte[] bytes)
                    {
                        data = bytes;
                        break;
                    }
                }

                data ??= [];
                var hex = BitConverter.ToString(data).Replace("-", " ");
                PluginManager.CallEvent(eventName, $"0x{opcode:X4}", hex);
            }
            catch (Exception ex)
            {
                LogMessage($"Packet event forwarding failed: {ex.Message}");
            }
        }

        private async void OnEventLoopTick(object state)
        {
            if (Interlocked.Exchange(ref _eventLoopRunning, 1) != 0)
                return;

            try
            {
                await PluginManager.CallEventAsync("event_loop").ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref _eventLoopRunning, 0);
            }
        }

        public string ResolvePluginIdForGui()
        {
            if (!string.IsNullOrWhiteSpace(CurrentLoadingPluginId))
                return CurrentLoadingPluginId;

            return "python_gui_" + Guid.NewGuid().ToString("N");
        }

        private void LogMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            try
            {
                Log.AppendFormat(LogLevel.Notify, "[Python] {0}", message);
            }
            catch { }

            LogAdded?.Invoke(message);
        }
    }
}
