using System.Windows;

using System.Windows.Controls;

using DevToolkit.Abstractions;
using DevToolkit.Services;
using DevToolkit.Views;

using NavigationViewItem = iNKORE.UI.WPF.Modern.Controls.NavigationViewItem;

namespace DevToolkit;

public partial class MainWindow : Window
{
    private const string AboutTag = "about";
    private const string SettingsTag = "settings";

    private readonly PluginManager _pluginManager;
    private readonly IHostServices _host;
    private readonly AboutPage _aboutPage;
    private readonly Dictionary<string, object> _contentCache = [];
    private string _currentTag = AboutTag;

    public MainWindow(PluginManager pluginManager, IHostServices host)
    {
        InitializeComponent();
        _pluginManager = pluginManager;
        _host = host;
        _aboutPage = new AboutPage(pluginManager);

        foreach (var plugin in pluginManager.Plugins)
            plugin.NavigationChanged += OnNavigationChanged;

        NavView.ItemInvoked += (_, args) =>
        {
            if (args.InvokedItemContainer?.Tag is string tag && tag != _currentTag)
                Navigate(tag);
        };

        BuildNav();
    }

    /// <summary>插件导航结构变化（如工具开关切换）时重建导航。</summary>
    private void OnNavigationChanged(object? sender, EventArgs e) => BuildNav();

    /// <summary>按插件注册的导航项重建导航；"关于/设置"固定在底部。</summary>
    private void BuildNav()
    {
        NavView.MenuItems.Clear();
        NavView.FooterMenuItems.Clear();

        foreach (var item in _pluginManager.GetNavigationItems())
        {
            if (item.IsVisible?.Invoke() == false)
                continue;
            NavView.MenuItems.Add(MakeItem(item));
        }

        NavView.FooterMenuItems.Add(MakeItem(AboutTag, "关于", "\uE946"));
        NavView.FooterMenuItems.Add(MakeItem(SettingsTag, "设置", "\uE713"));

        // 当前选中项不可用时回退到第一个可用导航项
        if (FindByTag(_currentTag) is null)
        {
            var first = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            _currentTag = first?.Tag as string ?? AboutTag;
        }

        if (FindByTag(_currentTag) is { } selected)
            selected.IsSelected = true;

        Navigate(_currentTag);
    }

    private NavigationViewItem? FindByTag(string tag) =>
        NavView.MenuItems.OfType<NavigationViewItem>()
            .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>())
            .FirstOrDefault(i => i.Tag as string == tag);

    private static NavigationViewItem MakeItem(PluginNavigationItem item) => new()
    {
        Content = item.DisplayName,
        Tag = item.Tag,
        Icon = new iNKORE.UI.WPF.Modern.Controls.FontIcon { Glyph = item.Glyph },
    };

    private static NavigationViewItem MakeItem(string tag, string content, string glyph) => new()
    {
        Content = content,
        Tag = tag,
        Icon = new iNKORE.UI.WPF.Modern.Controls.FontIcon { Glyph = glyph },
    };

    private void Navigate(string tag)
    {
        if (tag == AboutTag)
        {
            _currentTag = tag;
            ContentHost.Content = _aboutPage;
            return;
        }

        if (tag == SettingsTag)
        {
            _currentTag = tag;
            ContentHost.Content = new SettingsPage(_pluginManager);
            return;
        }

        var item = _pluginManager.GetNavigationItems().FirstOrDefault(i => i.Tag == tag);
        if (item is null)
            return;

        // 窗口型导航：打开独立窗口，内容区保持当前页面
        if (item.OpenAsWindow)
        {
            if (item.ContentFactory() is Window window)
                _host.ShowWindow(window);
            return;
        }

        _currentTag = tag;
        if (!_contentCache.TryGetValue(tag, out var content))
        {
            content = item.ContentFactory();
            _contentCache[tag] = content;
        }
        ContentHost.Content = content;
    }
}
