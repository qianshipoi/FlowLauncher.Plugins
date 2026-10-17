using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;
using FlowLauncher.Plugin.PaperTodo.ViewModels;
using FlowLauncher.Plugin.PaperTodo.Views;

using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace FlowLauncher.Plugin.PaperTodo.Tests;

public sealed class WindowSmokeTests : IAsyncLifetime
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "papertodo-window-tests-" + Guid.NewGuid().ToString("N"));
    private PaperDatabase _database = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        _database = await PaperDatabase.OpenAsync(Path.Combine(_tempDirectory, "papertodo.db"));
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void SettingsAndPaperWindows_ConstructOnSta()
    {
        RunOnSta(() =>
        {
            var paths = new AppPaths(_tempDirectory);
            paths.EnsureDirectories();
            var settings = new Settings();
            var imageService = new NoteImageService();
            var manager = new PaperWindowManager(
                _database,
                settings,
                imageService,
                Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                paths.WebViewDataDirectory);
            var importer = new PaperTodoImportService(_database, new LmdbImageImporter());
            var backup = new BackupService(_database, paths, settings);
            var settingsVm = new SettingsViewModel(
                settings,
                importer,
                backup,
                paths,
                () => { },
                () => Task.CompletedTask);

            Assert.NotNull(new SettingsView(settingsVm));

            var todo = new Paper
            {
                Type = PaperTypes.Todo,
                Title = "测试待办",
                Items = [new TodoItem { Text = "事项" }],
            };
            Assert.NotNull(new TodoPaperWindow(_database, settings, todo, manager));

            var note = new Paper
            {
                Type = PaperTypes.Note,
                Title = "测试笔记",
                Content = "# 标题",
            };
            Assert.NotNull(new NotePaperWindow(
                _database,
                note,
                settings,
                imageService,
                manager,
                Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                paths.WebViewDataDirectory));
        });
    }

    [Fact]
    public void SettingsView_BindingPathsResolve()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FlowLauncher.Plugin.PaperTodo",
            "Views",
            "SettingsView.xaml"));
        var paths = System.Text.RegularExpressions.Regex
            .Matches(xaml, @"\{Binding\s+([A-Za-z_][A-Za-z0-9_.]*)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            Assert.True(
                ResolvesOn(typeof(SettingsViewModel), path),
                $"绑定路径不存在：{path}");
        }
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

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"STA 线程内失败: {failure}");
        }
    }

    private static bool ResolvesOn(Type rootType, string path)
    {
        var current = rootType;
        foreach (var segment in path.Split('.'))
        {
            var property = current.GetProperty(
                segment,
                BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return false;
            }
            current = property.PropertyType;
        }
        return true;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FlowLauncher.Plugins.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("无法定位仓库根目录。");
    }
}
