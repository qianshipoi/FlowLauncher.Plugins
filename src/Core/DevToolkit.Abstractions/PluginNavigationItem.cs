namespace DevToolkit.Abstractions;

/// <summary>
/// 插件导航项：描述一个可导航的目标（内嵌页面或独立窗口）。
/// 一个插件可注册多个导航项（如 Toolbox 注册多个工具页面）。
/// </summary>
/// <param name="Tag">唯一标签，用于导航定位（如 "toolbox:guid"）。</param>
/// <param name="DisplayName">导航显示名。</param>
/// <param name="Glyph">导航图标（Segoe MDL2 Assets 字体码）。</param>
/// <param name="ContentFactory">页面工厂，返回 FrameworkElement（页面）或 Window（窗口型导航）。</param>
/// <param name="OpenAsWindow">是否为窗口型导航（true 时宿主以独立窗口打开）。</param>
/// <param name="IsVisible">动态可见性委托（如工具开关）；null 表示始终可见。</param>
public sealed record PluginNavigationItem(
    string Tag,
    string DisplayName,
    string Glyph,
    Func<object> ContentFactory,
    bool OpenAsWindow = false,
    Func<bool>? IsVisible = null);
