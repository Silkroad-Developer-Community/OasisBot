using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.Logging;
using Python.Runtime;
using RSBot.Core;
using RSBot.Python.API.Handler;
using RSBot.Python.Models;
using RSBot.Python.Runtime;

namespace RSBot.Python.Manager
{
    public class PythonPluginManager
    {
        private readonly PythonManager _manager;
        private readonly PythonExecutor _executor;
        private readonly PythonGuiHost _guiHost;
        private readonly Action<string> _log;
        private readonly ConcurrentDictionary<string, PyObject> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

        public PythonPluginManager(
            PythonManager manager,
            PythonExecutor executor,
            PythonGuiHost guiHost,
            Action<string> log
        )
        {
            _log = log;
            if (manager == null)
            {
                _log?.Invoke("PythonManager is missing. Cannot initialize PythonPluginManager");
                return;
            }
            _manager = manager;
            _executor = executor;
            _guiHost = guiHost;
        }

        public IReadOnlyList<PythonPluginInfo> ScanPlugins()
        {
            PythonPaths.EnsureDirectories();
            return Directory
                .GetFiles(PythonPaths.PluginsRoot, "*.py", SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).Equals("__init__.py", StringComparison.OrdinalIgnoreCase))
                .Select(PluginMetaReader.ReadPythonPluginInfo)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool IsLoaded(string pluginId) =>
            !string.IsNullOrWhiteSpace(pluginId) && _loadedPlugins.ContainsKey(pluginId);

        public async Task LoadAsync(PythonPluginInfo plugin)
        {
            if (plugin == null || IsLoaded(plugin.Id))
                return;

            await _executor
                .ExecuteAsync(() =>
                {
                    _manager.CurrentLoadingPluginId = plugin.Id;
                    try
                    {
                        dynamic importlib = Py.Import("importlib.util");
                        dynamic sys = Py.Import("sys");
                        dynamic spec = importlib.spec_from_file_location(plugin.Id, plugin.FullPath);
                        if (spec == null)
                        {
                            _log?.Invoke($"Unable to create module spec for {plugin.FullPath}");
                            return;
                        }

                        PyObject module = importlib.module_from_spec(spec);
                        sys.modules[plugin.Id] = module;
                        spec.loader.exec_module(module);
                        _loadedPlugins[plugin.Id] = module;

                        OptionalInvoke(module, "on_load");
                        _log?.Invoke($"Activated Python plugin '{plugin.Name}'.");
                    }
                    catch
                    {
                        try
                        {
                            dynamic sys = Py.Import("sys");
                            sys.modules.pop(plugin.Id, null);
                        }
                        catch { }
                        throw;
                    }
                    finally
                    {
                        _manager.CurrentLoadingPluginId = null;
                    }
                })
                .ConfigureAwait(false);
        }

        public async Task UnloadAsync(string pluginId, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return;

            await _executor
                .ExecuteAsync(() =>
                {
                    if (_loadedPlugins.TryGetValue(pluginId, out var module))
                    {
                        OptionalInvoke(module, "on_unload");
                        _loadedPlugins.TryRemove(pluginId, out _);

                        dynamic sys = Py.Import("sys");
                        sys.modules.pop(pluginId, null);
                        module.Dispose();

                        dynamic gc = Py.Import("gc");
                        gc.collect();
                    }
                })
                .ConfigureAwait(false);

            _guiHost.RemovePluginPage(pluginId);
            _log?.Invoke($"Deactivated Python plugin {displayName ?? pluginId}");
        }

        public Task CallEventAsync(string eventName, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(eventName) || _loadedPlugins.Count == 0)
                return Task.CompletedTask;

            return _executor.ExecuteAsync(() =>
            {
                foreach (var entry in _loadedPlugins.ToArray())
                {
                    try
                    {
                        if (!entry.Value.HasAttr(eventName))
                            continue;

                        using var callback = entry.Value.GetAttr(eventName);
                        var pyArgs =
                            args?.Select(x => x == null ? string.Empty.ToPython() : x.ToPython()).ToArray()
                            ?? Array.Empty<PyObject>();
                        try
                        {
                            using var _ = callback.Invoke(pyArgs);
                        }
                        finally
                        {
                            foreach (var arg in pyArgs)
                                arg.Dispose();
                        }
                    }
                    catch (PythonException ex)
                    {
                        _log?.Invoke($"[{entry.Key}] {PythonErrorHandler.FormatException(ex)}");
                    }
                    catch (Exception ex)
                    {
                        _log?.Invoke($"[{entry.Key}] {eventName}: {ex.Message}");
                    }
                }
            });
        }

        public void CallEvent(string eventName, params object[] args)
        {
            _ = CallEventAsync(eventName, args);
        }

        private static void OptionalInvoke(PyObject module, string functionName)
        {
            if (!module.HasAttr(functionName))
                return;
            using var callback = module.GetAttr(functionName);
            using var _ = callback.Invoke();
        }
    }
}
