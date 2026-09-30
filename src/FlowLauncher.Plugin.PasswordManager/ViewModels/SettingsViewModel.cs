using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;

using Microsoft.Win32;

using System;
using System.IO;

namespace FlowLauncher.Plugin.PasswordManager.ViewModels;

/// <summary>
/// 设置面板 ViewModel。
/// 通用设置直接绑定 <see cref="Settings"/>；随机密码生成器的选项经 <c>onChanged</c> 回调持久化。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ImportExportService _importExport;
    private readonly Action _onChanged;

    /// <summary>设置模型，通用项直接绑定到视图</summary>
    public Settings Settings { get; }

    [ObservableProperty]
    private string _statusText = "";

    /// <summary>当前配置对应的字符集，供预览与摘要使用</summary>
    public PasswordCharacterSet CharacterSet { get; private set; } = default!;

    public SettingsViewModel(Settings settings, ImportExportService importExport, Action? onChanged = null)
    {
        Settings = settings;
        _importExport = importExport;
        _onChanged = onChanged ?? (() => { });

        _generatorLengths = settings.GeneratorLengths;
        _generatorLowercase = settings.GeneratorLowercase;
        _generatorUppercase = settings.GeneratorUppercase;
        _generatorDigits = settings.GeneratorDigits;
        _generatorSymbols = settings.GeneratorSymbols;
        _generatorExcludeChars = settings.GeneratorExcludeChars;

        RefreshGeneratorPreview();
    }

    // ===== 通用设置 =====

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

    // ===== 随机密码生成器 =====

    [ObservableProperty]
    private string _generatorLengths;

    [ObservableProperty]
    private bool _generatorLowercase;

    [ObservableProperty]
    private bool _generatorUppercase;

    [ObservableProperty]
    private bool _generatorDigits;

    [ObservableProperty]
    private bool _generatorSymbols;

    [ObservableProperty]
    private string _generatorExcludeChars;

    /// <summary>长度列表校验提示（为空表示合法）</summary>
    [ObservableProperty]
    private string _lengthsError = "";

    /// <summary>字符集摘要，例如「小写+大写+数字+符号，共 87 个可用字符」</summary>
    [ObservableProperty]
    private string _alphabetSummary = "";

    /// <summary>用当前配置生成的示例密码</summary>
    [ObservableProperty]
    private string _previewText = "";

    partial void OnGeneratorLengthsChanged(string value) => PersistAndRefresh(s => s.GeneratorLengths = value);
    partial void OnGeneratorLowercaseChanged(bool value) => PersistAndRefresh(s => s.GeneratorLowercase = value);
    partial void OnGeneratorUppercaseChanged(bool value) => PersistAndRefresh(s => s.GeneratorUppercase = value);
    partial void OnGeneratorDigitsChanged(bool value) => PersistAndRefresh(s => s.GeneratorDigits = value);
    partial void OnGeneratorSymbolsChanged(bool value) => PersistAndRefresh(s => s.GeneratorSymbols = value);
    partial void OnGeneratorExcludeCharsChanged(string value) => PersistAndRefresh(s => s.GeneratorExcludeChars = value ?? "");

    private void PersistAndRefresh(Action<Settings> apply)
    {
        apply(Settings);
        _onChanged();
        RefreshGeneratorPreview();
    }

    /// <summary>按当前输入重新计算字符集摘要、长度列表校验与预览密码</summary>
    public void RefreshGeneratorPreview()
    {
        CharacterSet = new PasswordCharacterSet(
            GeneratorLowercase, GeneratorUppercase, GeneratorDigits, GeneratorSymbols, GeneratorExcludeChars ?? "");

        var alphabet = CharacterSet.BuildAlphabet();
        AlphabetSummary = alphabet.Length == 0
            ? "当前没有可用字符，请至少勾选一类字符，或减少忽略字符"
            : $"{CharacterSet.Describe()}，共 {alphabet.Length} 个可用字符";

        var valid = PasswordLengthParser.TryParse(GeneratorLengths, out var lengths);
        LengthsError = valid
            ? PasswordLengthParser.CountNumericTokens(GeneratorLengths) > PasswordGeneratorService.MaxConfiguredLengths
                ? $"最多保留 {PasswordGeneratorService.MaxConfiguredLengths} 个长度，多余的已忽略"
                : ""
            : $"长度列表无效，将回退为 8, 12, 16, 20, 24（每项 {PasswordGeneratorService.MinLength}-{PasswordGeneratorService.MaxLength}）";

        var previewLength = lengths.Count > 0 ? lengths[^1] : 16;
        var preview = PasswordGeneratorService.TryGenerate(previewLength, CharacterSet);
        PreviewText = preview == null ? "—" : $"{preview}（{previewLength} 位示例）";
    }

    [RelayCommand]
    private void RefreshPreview() => RefreshGeneratorPreview();

    /// <summary>快捷开关：忽略易混淆字符（0O1lI）</summary>
    [RelayCommand]
    private void ToggleAmbiguous()
    {
        GeneratorExcludeChars = GeneratorExcludeChars == PasswordGeneratorService.AmbiguousChars
            ? ""
            : PasswordGeneratorService.AmbiguousChars;
    }
}
