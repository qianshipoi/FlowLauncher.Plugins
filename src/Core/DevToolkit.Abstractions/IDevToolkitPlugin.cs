namespace DevToolkit.Abstractions;

/// <summary>
/// DevToolkit 插件入口接口。宿主启动时加载插件，先调用 <see cref="Initialize"/> 注入宿主能力，
/// 再通过 <see cref="GetNavigationItems"/> 与 <see cref="GetSettingsSections"/> 聚合导航项与配置表单。
/// </summary>
public interface IDevToolkitPlugin
{
    /// <summary>插件唯一标识（如 "toolbox"），用于导航标签、数据目录与设置文件命名。</summary>
    string Id { get; }

    /// <summary>插件显示名称（用于设置页与关于页）。</summary>
    string Name { get; }

    /// <summary>插件版本号。</summary>
    string Version { get; }

    /// <summary>
    /// 插件初始化：注入宿主能力（<see cref="IHostServices"/>），并完成插件自身初始化。
    /// 插件应在此阶段保存宿主引用，供页面/VM 使用。
    /// </summary>
    void Initialize(IHostServices host);

    /// <summary>插件注册的导航项。一个插件可注册多个导航项/页面（如 Toolbox 注册多个工具页面）。</summary>
    IReadOnlyList<PluginNavigationItem> GetNavigationItems();

    /// <summary>插件注册的配置表单分区；无配置返回空列表。</summary>
    IReadOnlyList<IPluginSettingsSection> GetSettingsSections();

    /// <summary>导航结构变化（如工具开关切换）时触发，宿主据此重建导航。</summary>
    event EventHandler? NavigationChanged;
}
