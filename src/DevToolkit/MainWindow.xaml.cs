using DevToolkit.Core.Wpf;

using DevToolkit.PortKiller;

using DevToolkit.Toolbox.ViewModels;

using DevToolkit.VarName;

using DevToolkit.ViewModels;

using DevToolkit.Views;

using DevToolkit.Views.Tools;

using System.IO;

using System.Windows;

using System.Windows.Controls;

using NavigationViewItem = iNKORE.UI.WPF.Modern.Controls.NavigationViewItem;
using ToolboxSettingsViewModel = DevToolkit.Toolbox.ViewModels.SettingsViewModel;
using VarNameSettingsViewModel = DevToolkit.VarName.SettingsViewModel;

namespace DevToolkit;

public partial class MainWindow : Window
{
    private static readonly (string Command, string DisplayName)[] ToolCatalog =
    [
        ("guid", "GUID 生成"),
        ("json", "JSON 编辑器"),
        ("fhash", "文件哈希"),
        ("dt", "时间与时间戳"),
        ("enc", "编码转换"),
        ("pw", "密码生成"),
        ("base", "进制转换"),
        ("ts", "时间戳转日期"),
        ("jwt", "JWT 解码"),
        ("cron", "Cron 解析"),
    ];

    private readonly Dictionary<string, UserControl> _toolViews = [];
    private readonly SettingsPage _settingsPage;
    private readonly PortKillerPage _portKillerPage;
    private readonly VarNamePage _varNamePage;
    private readonly ToolboxSettingsViewModel _toolboxSettingsVm;

    private JsonEditorWindow? _jsonEditorWindow;
    private string _currentTag = "tool:guid";

    public MainWindow()
    {
        InitializeComponent();

        _toolboxSettingsVm = new ToolboxSettingsViewModel(AppServices.ToolboxSettings, OnToolboxSettingsChanged);
        _settingsPage = new SettingsPage(_toolboxSettingsVm, new VarNameSettingsViewModel(
            AppServices.VarNameSettings, AppServices.SaveVarNameSettings));
        _portKillerPage = new PortKillerPage(new PortKillerViewModel(
            new PortHistoryManager(AppServices.DataRoot)));
        _varNamePage = new VarNamePage(new VarNamePageViewModel(
            AppServices.DataRoot, AppServices.VarNameSettings));

        NavView.ItemInvoked += (_, args) =>
        {
            if (args.InvokedItemContainer?.Tag is string tag && tag != _currentTag)
                Navigate(tag);
        };

        BuildNav();
        Navigate(_currentTag);
    }

    private void OnToolboxSettingsChanged()
    {
        AppServices.SaveToolboxSettings();
        BuildNav();
    }

    /// <summary>导航图标（Segoe MDL2 Assets）。</summary>
    private static string GetGlyph(string tag) => tag switch
    {
        "tool:guid" => "\uE8E5",       // TwoPage
        "tool:json" => "\uE70F",       // Edit
        "tool:fhash" => "\uE7C1",      // Page
        "tool:dt" => "\uE823",         // Recent
        "tool:enc" => "\uE8AB",        // Switch
        "tool:pw" => "\uE72E",         // Lock
        "tool:base" => "\uE8EF",       // Calculator
        "tool:ts" => "\uE787",         // Calendar
        "tool:jwt" => "\uE8A5",        // Document
        "tool:cron" => "\uE9D9",       // Diagnostic
        "portkiller" => "\uE71A",      // Stop
        "varname" => "\uE8AC",         // Rename
        _ => "\uE713",                 // Settings
    };

    /// <summary>按设置重建导航：启用的工具与端口占用/变量名/设置同级排列。</summary>
    private void BuildNav()
    {
        NavView.MenuItems.Clear();

        foreach (var (command, displayName) in ToolCatalog)
        {
            if (!AppServices.ToolboxSettings.IsEnabled(command))
                continue;
            NavView.MenuItems.Add(MakeItem("tool:" + command, displayName));
        }

        NavView.MenuItems.Add(MakeItem("portkiller", "端口占用"));
        NavView.MenuItems.Add(MakeItem("varname", "变量名"));
        NavView.MenuItems.Add(MakeItem("settings", "设置"));

        // 当前选中项被禁用时，回退到第一个可用的工具
        if (FindByTag(_currentTag) is null && _currentTag.StartsWith("tool:"))
        {
            var first = ToolCatalog.FirstOrDefault(t => AppServices.ToolboxSettings.IsEnabled(t.Command));
            _currentTag = first.Command is not null ? "tool:" + first.Command : "portkiller";
        }

        if (FindByTag(_currentTag) is { } selected)
            selected.IsSelected = true;

        Navigate(_currentTag);
    }

    private NavigationViewItem? FindByTag(string tag) =>
        NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag as string == tag);

    private static NavigationViewItem MakeItem(string tag, string content) => new()
    {
        Content = content,
        Tag = tag,
        Icon = new iNKORE.UI.WPF.Modern.Controls.FontIcon { Glyph = GetGlyph(tag) },
    };

    private void Navigate(string tag)
    {
        _currentTag = tag;
        ContentHost.Content = tag switch
        {
            "settings" => _settingsPage,
            "portkiller" => _portKillerPage,
            "varname" => _varNamePage,
            _ => GetToolView(tag["tool:".Length..]),
        };
    }

    private UserControl GetToolView(string command)
    {
        if (!_toolViews.TryGetValue(command, out var view))
        {
            view = CreateToolView(command);
            _toolViews[command] = view;
        }
        return view;
    }

    private UserControl CreateToolView(string command) => command switch
    {
        "guid" => new GuidView(),
        "json" => new JsonView(OpenJsonEditor),
        "fhash" => new HashView(),
        "dt" => new DtView(),
        "enc" => new EncView(),
        "pw" => new PwView(),
        "base" => new BaseView(),
        "ts" => new TsView(),
        "jwt" => new JwtView(OpenJsonEditor),
        "cron" => new CronView(),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    private void OpenJsonEditor(string json)
    {
        if (_jsonEditorWindow is null)
        {
            _jsonEditorWindow = new JsonEditorWindow(AppServices.WwwRoot, AppServices.WebView2CacheDir, json);
            _jsonEditorWindow.Closed += (_, _) => _jsonEditorWindow = null;
            _jsonEditorWindow.Show();
        }
        else
        {
            _jsonEditorWindow.Activate();
            _jsonEditorWindow.UpdateJson(json);
        }
    }
}
