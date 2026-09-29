using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;

namespace FlowLauncher.Plugin.PasswordManager.ViewModels;

/// <summary>
/// 导入弹窗 ViewModel（支持拖拽或点击选择文件）
/// </summary>
public partial class ImportViewModel : ObservableObject
{
    private readonly ImportExportService _importExport;
    private List<PasswordEntry>? _entries;

    /// <summary>导入成功触发，用于通知管理窗口刷新</summary>
    public event Action? Imported;

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _statusText = "点击选择或拖入 Excel/CSV 文件";

    /// <summary>解析出的有效条目数（0 表示未解析）</summary>
    [ObservableProperty]
    private int _parsedCount;

    public ImportViewModel(ImportExportService importExport)
    {
        _importExport = importExport;
    }

    /// <summary>
    /// 尝试加载并解析文件（拖拽/点击共用）
    /// </summary>
    public bool TryLoadFile(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            _entries = ext switch
            {
                ".xlsx" => _importExport.ImportFromExcel(path),
                ".csv" => _importExport.ImportFromCsv(path),
                _ => null,
            };

            if (_entries is null)
            {
                FilePath = "";
                ParsedCount = 0;
                StatusText = "不支持的文件格式，请选择 .xlsx 或 .csv";
                return false;
            }

            FilePath = path;
            ParsedCount = _entries.Count;
            StatusText = ParsedCount > 0
                ? $"解析成功，共 {ParsedCount} 条记录，点击\"导入\"写入"
                : "文件中没有找到有效数据";
            return ParsedCount > 0;
        }
        catch (Exception ex)
        {
            FilePath = "";
            ParsedCount = 0;
            _entries = null;
            StatusText = $"解析失败: {ex.Message}";
            return false;
        }
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择导入文件",
            Filter = "Excel 或 CSV 文件|*.xlsx;*.csv",
        };

        if (dialog.ShowDialog() == true)
            TryLoadFile(dialog.FileName);
    }

    /// <summary>
    /// 执行导入，返回是否成功（失败时弹窗保持打开）
    /// </summary>
    public bool TryImport()
    {
        if (_entries is null || _entries.Count == 0)
        {
            StatusText = "请先选择有效的文件";
            return false;
        }

        try
        {
            _importExport.ImportEntries(_entries);
            Imported?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败: {ex.Message}";
            return false;
        }
    }
}