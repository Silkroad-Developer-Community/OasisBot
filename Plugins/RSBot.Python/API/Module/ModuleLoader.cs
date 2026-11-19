using Python.Runtime;
using RSBot.Core;
using RSBot.Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RSBot.Python.API.Module
{
    /// <summary>
    /// Finds and loads all plugin classes that implement the IPythonPlugin interface.
    /// Saves them in a dictionary for later access.
    /// </summary>
    public class ModuleLoader
    {
        private readonly Action<string> _log;

        public ModuleLoader(Action<string> log)
        {
            _log = log;
        }
        private readonly List<IPythonPlugin> _plugins = [];
        private readonly List<PyObject> _pythonObjects = [];

        public IReadOnlyList<IPythonPlugin> Plugins => _plugins;

        /// <summary>
        /// Finds all plugin classes, initializes them, and stores them.
        /// This method is called once at the start of the application.
        /// </summary>
        public void InitAll(PythonContext context)
        {
            _plugins.Clear();
            var pluginType = typeof(IPythonPlugin);
            var pluginInstances = typeof(ModuleLoader).Assembly
            .GetTypes()
            .Where(t => pluginType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (IPythonPlugin)Activator.CreateInstance(t))
            .OrderBy(x => x.ModuleName)
            .ToArray();

            foreach (var plugin in pluginInstances)
            {
                plugin.Init(context);
                _plugins.Add(plugin);
                _log?.Invoke($"Python Plugin loaded: {plugin.ModuleName}");
            }

            BuildOasisBotModule();
        }

        /// <summary>
        /// Creates the OasisBot module in Python and adds all plugin methods as functions.
        /// </summary>
        private void BuildOasisBotModule()
        {
            dynamic types = Py.Import("types");
            dynamic sys = Py.Import("sys");
            PyObject oasisbot = types.ModuleType("OasisBot");
            var exported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var api in _plugins)
            {
                var pyApi = api.ToPython();
                _pythonObjects.Add(pyApi);

                foreach (var method in api.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (method.DeclaringType == typeof(object) || method.IsSpecialName || method.Name is nameof(IPythonPlugin.Init))
                        continue;

                    if (!exported.Add(method.Name))
                    {
                        _log?.Invoke($"API name collision: {method.Name} ({api.ModuleName}) - keeping first export.");
                        continue;
                    }

                    using var callable = pyApi.GetAttr(method.Name);
                    oasisbot.SetAttr(method.Name, callable);
                }
            }

            sys.modules["OasisBot"] = oasisbot;
        }
    }
}
