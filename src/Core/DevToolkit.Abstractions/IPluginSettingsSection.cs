using System.Windows;

namespace DevToolkit.Abstractions;

/// <summary>
/// 插件配置表单分区：宿主的设置页聚合所有插件的分区并动态渲染，
/// 由宿主统一管理插件配置表单。
/// </summary>
public interface IPluginSettingsSection
{
    /// <summary>分区标题（如 "工具箱"）。</summary>
    string Title { get; }

    /// <summary>分区说明。</summary>
    string Description { get; }

    /// <summary>创建分区视图（FrameworkElement）。</summary>
    FrameworkElement CreateView();
}
