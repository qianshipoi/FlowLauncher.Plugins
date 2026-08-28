using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevToolkit.PasswordManager.Models;
using DevToolkit.PasswordManager.Services;

using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DevToolkit.PasswordManager.ViewModels;

/// <summary>
/// 数据管理窗口 ViewModel
/// </summary>
public partial class ManagerViewModel : ObservableObject
{
    private readonly PasswordStore _store;
    private readonly ImportExportService _importExport;

    /// <summary>请求打开编辑窗口（视图订阅后展示对话框）</summary>
    public event Action<EditPasswordViewModel>? EditRequested;

    /// <summary>请求打开导入弹窗</summary>
    public event Action<ImportViewModel>? ImportRequested;

    /// <summary>请求打开导出弹窗</summary>
    public event Action<ExportViewModel>? ExportRequested;

    public ObservableCollection<PasswordEntry> Entries { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private PasswordEntry? _selectedEntry;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _statusText = "";

    public ManagerViewModel(PasswordStore store, ImportExportService importExport)
    {
        _store = store;
        _importExport = importExport;
        LoadEntries();
    }

    partial void OnSearchTextChanged(string value) => LoadEntries();

    public void LoadEntries()
    {
        Entries.Clear();

        var keyword = SearchText?.Trim() ?? "";
        var list = string.IsNullOrWhiteSpace(keyword)
            ? _store.GetAll()
            : _store.Search(keyword);

        foreach (var entry in list)
            Entries.Add(entry);

        // 保持选中
        if (SelectedEntry is not null)
        {
            var match = Entries.FirstOrDefault(x => x.Id == SelectedEntry.Id);
            SelectedEntry = match;
        }

        StatusText = $"共 {Entries.Count} 条记录";
    }

    [RelayCommand]
    private void Refresh() => LoadEntries();

    [RelayCommand]
    private void Import()
    {
        var importViewModel = new ImportViewModel(_importExport);
        importViewModel.Imported += () => LoadEntries();
        ImportRequested?.Invoke(importViewModel);
    }

    [RelayCommand]
    private void Export()
    {
        ExportRequested?.Invoke(new ExportViewModel(_importExport));
    }

    [RelayCommand]
    private void Add()
    {
        var vm = new EditPasswordViewModel(_store, null);
        vm.Saved += _ => LoadEntries();
        EditRequested?.Invoke(vm);
    }

    private bool CanEditOrDelete() => SelectedEntry is not null;

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void Edit()
    {
        var existing = _store.GetById(SelectedEntry!.Id);
        var vm = new EditPasswordViewModel(_store, existing);
        vm.Saved += id => LoadEntries();
        EditRequested?.Invoke(vm);
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void Delete()
    {
        var selected = _store.GetById(SelectedEntry!.Id);
        if (selected is null)
            return;

        var confirm = System.Windows.MessageBox.Show(
            $"确定删除 \"{selected.Title}\" ({selected.Username}) 吗？",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm == System.Windows.MessageBoxResult.Yes)
        {
            _store.Delete(selected.Id);
            LoadEntries();
            StatusText = $"已删除 {selected.Title}";
        }
    }
}