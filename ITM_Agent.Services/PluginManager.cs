// ITM_Agent.Services/PluginManager.cs
using ITM_Agent.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ITM_Agent.Services
{
    public class PluginManager
    {
        private readonly ILogger _logger;
        private readonly IAppServiceProvider _serviceProvider; // IServiceProvider -> IAppServiceProvider
        private readonly string _pluginsPath;
        private readonly List<IPlugin> _loadedPlugins = new List<IPlugin>();

        // 생성자 파라미터를 IAppServiceProvider로 수정
        public PluginManager(IAppServiceProvider serviceProvider, string pluginsPath = "Library")
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = _serviceProvider.GetService<ILogger>();
            if (_logger == null)
            {
                throw new InvalidOperationException("ILogger service could not be resolved.");
            }

            _pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginsPath);
            if (!Directory.Exists(_pluginsPath))
            {
                Directory.CreateDirectory(_pluginsPath);
                _logger.LogEvent($"[PluginManager] Plugins directory created at: '{_pluginsPath}'");
            }
        }

        public void LoadPlugins()
        {
            _loadedPlugins.Clear();
            _logger.LogEvent("[PluginManager] Loading plugins...");

            foreach (var dllFile in Directory.GetFiles(_pluginsPath, "*.dll"))
            {
                try
                {
                    byte[] assemblyBytes = File.ReadAllBytes(dllFile);
                    Assembly assembly = Assembly.Load(assemblyBytes);

                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is IPlugin pluginInstance)
                        {
                            // IAppServiceProvider를 올바르게 전달
                            pluginInstance.Initialize(_serviceProvider);
                            _loadedPlugins.Add(pluginInstance);
                            _logger.LogEvent($"[PluginManager] Plugin loaded successfully: {pluginInstance.Name} (v{pluginInstance.Version})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[PluginManager] Failed to load plugin from '{Path.GetFileName(dllFile)}'. Error: {ex.Message}");
                }
            }
        }

        public IEnumerable<IPlugin> GetLoadedPlugins()
        {
            return _loadedPlugins;
        }

        public IPlugin GetPlugin(string pluginName)
        {
            return _loadedPlugins.FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
