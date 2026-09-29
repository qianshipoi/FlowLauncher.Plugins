using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;

using Microsoft.Win32;

using System;
using System.IO;

namespace FlowLauncher.Plugin.PasswordManager.ViewModels;

/// <summary>
/// 设置面板 ViewModel
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ImportExportService _importExport;

    /// <summary>设置模型，属性直接绑定到视图</summary>
    public Settings Settings { get; }

    [ObservableProperty]
    private string _statusText = "";

    public SettingsViewModel(Settings settings, ImportExportService importExport)
    {
        Settings = settings;
        _importExport = importExport;
    }

    [RelayCommand]
    private void ExportEncryptedDb()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出加密数据库",
            Filter = "数据库文件|*.db",
            FileName = $"passwords_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _importExport.ExportEncryptedDb(dialog.FileName);
                var keyPath = Path.ChangeExtension(dialog.FileName, ".dbkey");
                StatusText = $"导出成功！\nDB: {dialog.FileName}\n密钥: {keyPath}";
            }
            catch (Exception ex)
            {
                StatusText = $"导出失败: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ExportExcel()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出为 Excel",
            Filter = "Excel 文件|*.xlsx",
            FileName = $"passwords_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var count = _importExport.ExportToExcel(dialog.FileName);
                StatusText = $"导出成功！共导出 {count} 条记录。\n文件: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"导出失败: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出为 CSV",
            Filter = "CSV 文件|*.csv",
            FileName = $"passwords_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var count = _importExport.ExportToCsv(dialog.FileName);
                StatusText = $"导出成功！共导出 {count} 条记录。\n文件: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"导出失败: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ImportExcel()
    {
        var dialog = new OpenFileDialog
        {
            Title = "从 Excel 导入",
            Filter = "Excel 文件|*.xlsx",
        };

        if (dialog.ShowDialog() == true)
            Import(dialog.FileName, _importExport.ImportFromExcel);
    }

    [RelayCommand]
    private void ImportCsv()
    {
        var dialog = new OpenFileDialog
        {
            Title = "从 CSV 导入",
            Filter = "CSV 文件|*.csv",
        };

        if (dialog.ShowDialog() == true)
            Import(dialog.FileName, _importExport.ImportFromCsv);
    }

    private void Import(string filePath, Func<string, System.Collections.Generic.List<PasswordEntry>> parser)
    {
        try
        {
            var entries = parser(filePath);
            if (entries.Count == 0)
            {
                StatusText = "文件中没有找到有效数据。";
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"找到 {entries.Count} 条记录，确认导入？",
                "确认导入",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var count = _importExport.ImportEntries(entries);
                StatusText = $"导入成功！共导入 {count} 条记录。";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败: {ex.Message}";
        }
    }
}