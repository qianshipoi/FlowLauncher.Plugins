using System.IO;
using System.Reflection;

using DevToolkit.Abstractions;

using Microsoft.Extensions.Logging;

namespace DevToolkit.Services;

/// <summary>插件配置表单分区（插件 + 分区视图）。</summary>
/// <param name="Plugin">所属插件。</param>
/// <param name="Section">配置表单分区。</param>
public readonly record struct PluginSettingsSection(IDevToolkitPlugin Plugin, IPluginSettingsSection Section);

/// <summary>
/// 插件管理器：编译期注册内置插件 + 启动时扫描 Plugins 目录动态加载第三方插件。
/// 负责初始化插件并聚合导航项与配置表单。
/// </summary>
public sealed class PluginManager : IDisposable
{
    private readonly IHostServices _host;
    private readonly ILogger _logger;
    private readonly Dictionary<string, IDevToolkitPlugin> _plugins = [];
    private readonly List<PluginLoadContext> _contexts = [];

    /// <summary>已加载插件（按 Id 去重，后加载覆盖先加载的同 Id 插件）。</summary>
    public IReadOnlyCollection<IDevToolkitPlugin> Plugins => _plugins.Values;

    public PluginManager(IHostServices host, IEnumerable<IDevToolkitPlugin> builtInPlugins)
    {
        _host = host;
        _logger = host.CreateLogger(nameof(PluginManager));

        foreach (var plugin in builtInPlugins)
            Register(plugin);

        ScanPluginsDirectory();
    }

    /// <summary>注册并初始化单个插件；失败仅记日志不阻断宿主。</summary>
    private void Register(IDevToolkitPlugin plugin)
    {
        try
        {
            plugin.Initialize(_host);
            _plugins[plugin.Id] = plugin;
            _logger.LogInformation("插件已加载: {Name} ({Id}) v{Version}", plugin.Name, plugin.Id, plugin.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插件初始化失败: {Id}", plugin.Id);
        }
    }

    /// <summary>扫描运行时插件目录（Plugins/*.dll），反射查找 IDevToolkitPlugin 实现。</summary>
    private void ScanPluginsDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Plugins");
        if (!Directory.Exists(dir))
            return;

        foreach (var dllPath in Directory.GetFiles(dir, "*.dll"))
        {
            try
            {
                var context = new PluginLoadContext(dllPath);
                var assembly = context.LoadFromAssemblyName(AssemblyName.GetAssemblyName(dllPath));
                var loaded = false;

                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(IDevToolkitPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                        continue;
                    if (type.GetConstructor(Type.EmptyTypes) is null)
                        continue;
                    if (Activator.CreateInstance(type) is not IDevToolkitPlugin plugin)
                        continue;

                    _contexts.Add(context);
                    Register(plugin);
                    loaded = true;
                }

                if (!loaded)
                    context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载插件程序集失败: {Dll}", dllPath);
            }
        }
    }

    /// <summary>聚合所有插件的导航项（单个插件异常不影响其它插件）。</summary>
    public IReadOnlyList<PluginNavigationItem> GetNavigationItems()
    {
        var items = new List<PluginNavigationItem>();
        foreach (var plugin in _plugins.Values)
        {
            try
            {
                items.AddRange(plugin.GetNavigationItems());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "插件获取导航项失败: {Id}", plugin.Id);
            }
        }
        return items;
    }

    /// <summary>聚合所有插件的配置表单分区。</summary>
    public IReadOnlyList<PluginSettingsSection> GetSettingsSections()
    {
        var sections = new List<PluginSettingsSection>();
        foreach (var plugin in _plugins.Values)
        {
            try
            {
                foreach (var section in plugin.GetSettingsSections())
                    sections.Add(new PluginSettingsSection(plugin, section));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "插件获取配置分区失败: {Id}", plugin.Id);
            }
        }
        return sections;
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            try
            {
                context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "卸载插件上下文失败");
            }
        }
        _contexts.Clear();
        _plugins.Clear();
    }
}
