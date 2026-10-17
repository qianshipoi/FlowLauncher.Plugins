using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.PaperTodo.Services;

using Microsoft.Win32;

using System.Diagnostics;
using System.IO;

namespace FlowLauncher.Plugin.PaperTodo.ViewModels;

internal partial class SettingsViewModel : ObservableObject
{
    private readonly PaperTodoImportService _importService;
    private readonly BackupService _backupService;
    private readonly AppPaths _paths;
    private readonly Action _onChanged;
    private readonly Func<Task> _prepareRestoreAsync;

    public SettingsViewModel(
        Settings settings,
        PaperTodoImportService importService,
        BackupService backupService,
        AppPaths paths,
        Action onChanged,
        Func<Task> prepareRestoreAsync)
    {
        Settings = settings;
        _importService = importService;
        _backupService = backupService;
        _paths = paths;
        _onChanged = onChanged;
        _prepareRestoreAsync = prepareRestoreAsync;
        _completionPolicy = settings.CompletionPolicy;
        _remindersEnabled = settings.RemindersEnabled;
        _reminderSoundEnabled = settings.ReminderSoundEnabled;
        _autoCompressImages = settings.AutoCompressImages;
        _backupRetentionCount = settings.BackupRetentionCount;
    }

    public Settings Settings { get; }
    public IReadOnlyList<CompletionPolicyOption> CompletionPolicies { get; } =
    [
        new(TodoCompletionPolicy.MoveToBottom, "完成后移到已完成区域末尾"),
        new(TodoCompletionPolicy.KeepInPlace, "完成后原位保留"),
        new(TodoCompletionPolicy.Delete, "完成后立即清除"),
    ];

    [ObservableProperty]
    private TodoCompletionPolicy _completionPolicy;

    [ObservableProperty]
    private bool _remindersEnabled;

    [ObservableProperty]
    private bool _reminderSoundEnabled;

    [ObservableProperty]
    private bool _autoCompressImages;

    [ObservableProperty]
    private int _backupRetentionCount;

    [ObservableProperty]
    private string _statusText = "";

    partial void OnCompletionPolicyChanged(TodoCompletionPolicy value)
    {
        Settings.CompletionPolicy = value;
        _onChanged();
    }

    partial void OnRemindersEnabledChanged(bool value)
    {
        Settings.RemindersEnabled = value;
        _onChanged();
    }

    partial void OnReminderSoundEnabledChanged(bool value)
    {
        Settings.ReminderSoundEnabled = value;
        _onChanged();
    }

    partial void OnAutoCompressImagesChanged(bool value)
    {
        Settings.AutoCompressImages = value;
        _onChanged();
    }

    partial void OnBackupRetentionCountChanged(int value)
    {
        var normalized = Math.Clamp(value, 1, 60);
        if (normalized != value)
        {
            BackupRetentionCount = normalized;
            return;
        }
        Settings.BackupRetentionCount = normalized;
        _onChanged();
    }

    [RelayCommand]
    private async Task ImportPaperTodoAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 PaperTodo data.json",
            Filter = "PaperTodo 数据 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            "导入前请确认 PaperTodo 已关闭，避免读取到写入中的图片库。\n继续导入吗？",
            "导入 PaperTodo",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            StatusText = "正在导入...";
            var result = await _importService.ImportAsync(dialog.FileName);
            StatusText =
                $"导入完成：{result.ImportedPapers} 张纸片、{result.ImportedItems} 个事项、" +
                $"{result.ImportedImages} 张图片；跳过 {result.SkippedPapers} 张已存在纸片。";
            if (result.Warnings.Count > 0)
            {
                var preview = string.Join(
                    Environment.NewLine,
                    result.Warnings.Take(8));
                if (result.Warnings.Count > 8)
                {
                    preview += $"{Environment.NewLine}…另有 {result.Warnings.Count - 8} 条警告";
                }
                System.Windows.MessageBox.Show(
                    preview,
                    "导入完成，但有项目被跳过",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败：{ex.Message}";
            System.Windows.MessageBox.Show(
                ex.Message,
                "导入失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportDatabaseAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 PaperTodo 数据库",
            Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
            FileName = $"papertodo-{DateTime.Now:yyyyMMdd-HHmmss}.db",
            DefaultExt = ".db",
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _backupService.ExportAsync(dialog.FileName);
            StatusText = $"数据库已导出：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "恢复 PaperTodo 数据库",
            Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            "恢复会用所选数据库替换当前数据，并先创建应急备份。\n继续吗？",
            "恢复数据库",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _prepareRestoreAsync();
            await _backupService.RestoreAsync(dialog.FileName);
            StatusText = "数据库已恢复。可重新查询 Todo 或 Note。";
        }
        catch (Exception ex)
        {
            StatusText = $"恢复失败：{ex.Message}";
            System.Windows.MessageBox.Show(
                ex.Message,
                "恢复失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        Process.Start(new ProcessStartInfo(_paths.DataDirectory) { UseShellExecute = true });
    }
}

internal sealed record CompletionPolicyOption(TodoCompletionPolicy Value, string Label);
