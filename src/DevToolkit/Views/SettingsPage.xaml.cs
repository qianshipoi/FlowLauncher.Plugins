using System.Windows.Controls;

using DevToolkit.Services;

namespace DevToolkit.Views;

/// <summary>宿主设置页数据项：一个插件配置分区对应一个 <see cref="DevToolkit.Abstractions.IPluginSettingsSection"/>。</summary>
/// <param name="Title">分区标题。</param>
/// <param name="Description">分区说明。</param>
/// <param name="View">分区表单视图。</param>
public sealed record SettingsSectionItem(string Title, string Description, object View);

/// <summary>
/// 宿主设置页：聚合所有插件注册的 <see cref="DevToolkit.Abstractions.IPluginSettingsSection"/> 配置表单分区，
/// 每个分区渲染为 iNKORE SettingsExpander 分组。
/// </summary>
public partial class SettingsPage : UserControl
{
    public SettingsPage(PluginManager pluginManager)
    {
        InitializeComponent();
        DataDirText.Text = $"数据目录：{AppServices.DataRoot}";

        var items = new List<SettingsSectionItem>();
        foreach (var (_, section) in pluginManager.GetSettingsSections())
            items.Add(new SettingsSectionItem(section.Title, section.Description, section.CreateView()));

        SectionsHost.ItemsSource = items;
    }
}
