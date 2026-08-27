using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.PasswordManager.Services;

using Microsoft.Win32;

using System;
using System.IO;

namespace FlowLauncher.Plugin.PasswordManager.ViewModels;

/// <summary>
/// 导出弹窗 ViewModel（选择格式和目标路径）
/// </summary>
internal partial class ExportViewModel : ObservableObject
{
    private const int FormatDb = 0;
    private const int FormatExcel = 1;
    private const int FormatCsv = 2;

    private readonly ImportExportService _importExport;

    [ObservableProperty]
    private int _selectedFormatIndex;

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _statusText = "";

    public string[] Formats { get; } = ["加密数据库 (.db)", "Excel (.xlsx)", "CSV (.csv)"];

    public ExportViewModel(ImportExportService importExport)
    {
        _importExport = importExport;
    }

    partial void OnSelectedFormatIndexChanged(int value)
    {
        // 格式变化时清空路径，下次浏览自动带默认文件名
        FilePath = "";
        StatusText = "";
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new SaveFileDialog
        {
            Title = "选择导出位置",
            Filter = SelectedFormatIndex switch
            {
                FormatDb => "数据库文件|*.db",
                FormatExcel => "Excel 文件|*.xlsx",
                _ => "CSV 文件|*.csv",
            },
            FileName = SelectedFormatIndex switch
            {
                FormatDb => $"passwords_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                FormatExcel => $"passwords_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                _ => $"passwords_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            },
        };

        if (dialog.ShowDialog() == true)
            FilePath = dialog.FileName;
    }

    /// <summary>
    /// 执行导出，返回是否成功（失败时弹窗保持打开）
    /// </summary>
    public bool TryExport()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            StatusText = "请先选择导出位置";
            return false;
        }

        try
        {
            switch (SelectedFormatIndex)
            {
                case FormatDb:
                    _importExport.ExportEncryptedDb(FilePath);
                    break;
                case FormatExcel:
                    _importExport.ExportToExcel(FilePath);
                    break;
                default:
                    _importExport.ExportToCsv(FilePath);
                    break;
            }
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败: {ex.Message}";
            return false;
        }
    }
}