using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;
using FlowLauncher.Plugin.PasswordManager.ViewModels;
using FlowLauncher.Plugin.PasswordManager.Views;

using System.IO;
using System.Windows;

using SQLitePCL;

using Xunit;

namespace FlowLauncher.Plugin.PasswordManager.Tests;

/// <summary>
/// 设置面板与生成器的集成冒烟测试：
/// XAML 绑定错误只在运行时暴露，这里在 STA 线程上真实构造控件并驱动 VM。
/// </summary>
public class SettingsPanelSmokeTests : IDisposable
{
    private readonly PasswordStore _store;
    private readonly string _tempDir;

    static SettingsPanelSmokeTests()
    {
        Batteries_V2.Init();
    }

    public SettingsPanelSmokeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pwd_settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _store = new PasswordStore(_tempDir, new DBKeyManager(_tempDir).GetOrCreateKey());
    }

    /// <summary>创建使用临时库的 VM；第二个实例会复用同一个数据库句柄。</summary>
    private SettingsViewModel CreateViewModel(Settings? settings = null) =>
        new(settings ?? new Settings(), new ImportExportService(_store, _tempDir), () => { });

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw new Xunit.Sdk.XunitException($"STA 线程内失败: {failure}");
    }

    /// <summary>
    /// WPF 绑定失败不会抛异常，只在运行时静默失效，所以这里直接把 SettingsView.xaml 里
    /// 出现的每个绑定路径逐段对照 ViewModel 类型解析，拼错属性名会立刻失败。
    /// </summary>
    [Fact]
    public void SettingPanel_AllBindingPathsResolveOnViewModel()
    {
        var xamlPath = LocateSettingsViewXaml();
        var xaml = File.ReadAllText(xamlPath);

        var paths = System.Text.RegularExpressions.Regex
            .Matches(xaml, @"\{Binding\s+([A-Za-z_][A-Za-z0-9_.]*)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(paths);

        var unresolved = paths.Where(p => !ResolvesOn(typeof(SettingsViewModel), p)).ToList();

        Assert.True(unresolved.Count == 0,
            $"以下绑定路径在 {nameof(SettingsViewModel)} 上不存在: {string.Join(", ", unresolved)}");
    }

    /// <summary>从测试输出目录向上找到源码树中的 SettingsView.xaml。</summary>
    private static string LocateSettingsViewXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "FlowLauncher.Plugin.PasswordManager", "Views", "SettingsView.xaml");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("未能定位 SettingsView.xaml，请检查测试的工作目录假设");
    }

    /// <summary>判断点分路径（如 Settings.RecentItemsCount）能否逐段解析到公共属性。</summary>
    private static bool ResolvesOn(Type rootType, string dottedPath)
    {
        var current = rootType;
        foreach (var segment in dottedPath.Split('.'))
        {
            var property = current.GetProperty(segment);
            if (property == null)
                return false;
            current = property.PropertyType;
        }
        return true;
    }

    [Fact]
    public void CreateSettingPanel_DoesNotThrow()
    {
        RunOnSta(() =>
        {
            var view = new SettingsView(CreateViewModel());
            Assert.NotNull(view);
            Assert.IsType<SettingsViewModel>(view.DataContext);
        });
    }

    [Fact]
    public void ViewModel_InitialPreview_IsPopulated()
    {
        var vm = CreateViewModel();

        Assert.NotEqual("", vm.PreviewText);
        Assert.NotEqual("—", vm.PreviewText);
        Assert.Contains("可用字符", vm.AlphabetSummary);
        Assert.Equal("", vm.LengthsError);
    }

    [Fact]
    public void ChangingCategory_PersistsAndRefreshesPreview()
    {
        var settings = new Settings();
        var vm = CreateViewModel(settings);

        // 只留大写：预览中不应混入其他类别
        vm.GeneratorLowercase = false;
        vm.GeneratorDigits = false;
        vm.GeneratorSymbols = false;

        Assert.False(settings.GeneratorLowercase);
        Assert.False(settings.GeneratorDigits);
        Assert.False(settings.GeneratorSymbols);
        Assert.True(settings.GeneratorUppercase);

        var sample = PasswordGeneratorService.Generate(24, settings.GetCharacterSet());
        Assert.All(sample, c => Assert.True(char.IsUpper(c)));
    }

    [Fact]
    public void ChangingExcludeChars_PersistsAndRefreshesPreview()
    {
        var settings = new Settings();
        var vm = CreateViewModel(settings);

        vm.GeneratorExcludeChars = "aeiou";

        Assert.Equal("aeiou", settings.GeneratorExcludeChars);
        var alphabet = settings.GetCharacterSet().BuildAlphabet();
        Assert.All("aeiou", c => Assert.DoesNotContain(c, alphabet));
    }

    [Fact]
    public void ToggleAmbiguous_FillsThenClearsExcludeChars()
    {
        var settings = new Settings();
        var vm = CreateViewModel(settings);

        vm.ToggleAmbiguousCommand.Execute(null);
        Assert.Equal(PasswordGeneratorService.AmbiguousChars, vm.GeneratorExcludeChars);
        Assert.Equal(PasswordGeneratorService.AmbiguousChars, settings.GeneratorExcludeChars);

        vm.ToggleAmbiguousCommand.Execute(null);
        Assert.Equal("", vm.GeneratorExcludeChars);
        Assert.Equal("", settings.GeneratorExcludeChars);
    }

    [Fact]
    public void InvalidLengthList_ShowsErrorAndFallsBack()
    {
        var settings = new Settings();
        var vm = CreateViewModel(settings);

        vm.GeneratorLengths = "abc";

        Assert.NotEqual("", vm.LengthsError);
        Assert.Equal([8, 12, 16, 20, 24], settings.GetGeneratorLengths());
    }

    [Fact]
    public void TooManyLengths_ShowsTruncationHint()
    {
        var settings = new Settings();
        var vm = CreateViewModel(settings);

        vm.GeneratorLengths = "1,2,3,4,5,6,7,8,9,10";

        Assert.Contains("最多保留", vm.LengthsError);
        Assert.Equal(PasswordGeneratorService.MaxConfiguredLengths, settings.GetGeneratorLengths().Count);
    }

    [Fact]
    public void ValidLengthList_ClearsError()
    {
        var settings = new Settings();
        var vm = CreateViewModel(settings);

        vm.GeneratorLengths = "abc";
        Assert.NotEqual("", vm.LengthsError);

        vm.GeneratorLengths = "10, 20";
        Assert.Equal("", vm.LengthsError);
        Assert.Equal([10, 20], settings.GetGeneratorLengths());
    }

    [Fact]
    public void NoCategorySelected_ShowsWarningAndBlankPreview()
    {
        var vm = CreateViewModel();

        vm.GeneratorLowercase = false;
        vm.GeneratorUppercase = false;
        vm.GeneratorDigits = false;
        vm.GeneratorSymbols = false;

        Assert.Contains("没有可用字符", vm.AlphabetSummary);
        Assert.Equal("—", vm.PreviewText);
    }

    [Fact]
    public void RefreshPreviewCommand_RegeneratesPreviewValue()
    {
        var vm = CreateViewModel();

        var first = vm.PreviewText;
        // 多点几次，至少有一次应当不同（24 位随机密码撞车概率可忽略）
        var changed = Enumerable.Range(0, 10).Any(_ =>
        {
            vm.RefreshPreviewCommand.Execute(null);
            return vm.PreviewText != first;
        });

        Assert.True(changed, "多次刷新预览未产生变化");
    }
}
