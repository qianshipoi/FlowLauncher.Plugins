using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;

using System.Collections.ObjectModel;

namespace FlowLauncher.Plugin.PaperTodo.ViewModels;

internal partial class TodoPaperViewModel : ObservableObject, IDisposable
{
    private readonly PaperDatabase _database;
    private readonly Settings _settings;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _undoExpiry;
    private TodoItem? _deletedItem;

    public TodoPaperViewModel(PaperDatabase database, Settings settings, Paper paper)
    {
        _database = database;
        _settings = settings;
        Paper = paper;
        _title = paper.Title;
        Items = new ObservableCollection<TodoItemViewModel>(
            paper.Items.OrderBy(item => item.Order).Select(item => new TodoItemViewModel(item)));
    }

    public Paper Paper { get; }
    public ObservableCollection<TodoItemViewModel> Items { get; }
    public event Action<string>? ErrorOccurred;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _newItemText = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    public async Task LoadAsync()
    {
        var refreshed = await _database.GetPaperAsync(Paper.Id, _lifetime.Token);
        if (refreshed is null)
        {
            return;
        }
        Paper.Title = refreshed.Title;
        Title = refreshed.Title;
        ReplaceItems(refreshed.Items);
    }

    public async Task CommitTitleAsync()
    {
        var normalized = string.IsNullOrWhiteSpace(Title) ? "待办" : Title.Trim();
        if (normalized.Length > 80)
        {
            normalized = normalized[..80];
        }
        Title = normalized;
        await _database.UpdateTitleAsync(Paper.Id, normalized, _lifetime.Token);
        Paper.Title = normalized;
    }

    public async Task<bool> AddNewAsync()
    {
        var lines = TodoTextParser.Split(NewItemText);
        if (lines.Count == 0)
        {
            return false;
        }

        try
        {
            await _database.AppendTodosAsync(
                Paper.Id,
                lines.Select(text => new TodoItem { Text = text }).ToArray(),
                _lifetime.Token);
            NewItemText = "";
            await ReloadItemsAsync();
            StatusText = lines.Count == 1 ? "已添加" : $"已添加 {lines.Count} 项";
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task<bool> CommitAllAsync()
    {
        try
        {
            await CommitTitleAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            return false;
        }
        foreach (var item in Items.ToArray())
        {
            if (!await CommitItemTextAsync(item))
            {
                return false;
            }
        }
        return true;
    }

    public async Task<bool> CommitItemTextAsync(TodoItemViewModel item)
    {
        var text = item.Text.Trim();
        if (text.Length == 0)
        {
            return await DeleteItemAsync(item);
        }
        if (text.Length > 5000)
        {
            text = text[..5000];
            item.Text = text;
        }
        if (string.Equals(text, item.Model.Text, StringComparison.Ordinal))
        {
            return true;
        }
        try
        {
            await _database.UpdateTodoTextAsync(item.Id, text, _lifetime.Token);
            item.ApplyEditorText(text);
            StatusText = "已保存";
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task SetDoneAsync(TodoItemViewModel item, bool done)
    {
        try
        {
            var result = await _database.SetTodoDoneAsync(
                item.Id,
                done,
                _settings.CompletionPolicy,
                _lifetime.Token);
            if (result.WasDeleted)
            {
                ShowUndo(result.Item);
                StatusText = "已完成并清除，可撤销";
            }
            else
            {
                StatusText = done ? "已完成" : "已恢复";
            }
            await ReloadItemsAsync();
        }
        catch (Exception ex)
        {
            item.IsDone = !done;
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task<bool> DeleteItemAsync(TodoItemViewModel item)
    {
        try
        {
            var model = item.Model;
            await _database.DeleteTodoAsync(item.Id, _lifetime.Token);
            ShowUndo(model);
            await ReloadItemsAsync();
            StatusText = "已删除，可撤销";
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task SetReminderAsync(TodoItemViewModel item, DateTimeOffset? reminderAt)
    {
        try
        {
            await _database.SetTodoReminderAsync(item.Id, reminderAt, _lifetime.Token);
            await ReloadItemsAsync();
            StatusText = reminderAt.HasValue
                ? $"提醒：{reminderAt.Value.ToLocalTime():MM-dd HH:mm}"
                : "已清除提醒";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task SetFileLinkAsync(string itemId, string path, bool isDirectory)
    {
        try
        {
            await _database.SetTodoLinkAsync(itemId, null, path, isDirectory, _lifetime.Token);
            await ReloadItemsAsync();
            StatusText = "已关联文件";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task SetNoteLinkAsync(string itemId, string noteId)
    {
        try
        {
            await _database.SetTodoLinkAsync(itemId, noteId, null, null, _lifetime.Token);
            await ReloadItemsAsync();
            StatusText = "已关联笔记";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task ClearLinkAsync(string itemId)
    {
        try
        {
            await _database.SetTodoLinkAsync(itemId, null, null, null, _lifetime.Token);
            await ReloadItemsAsync();
            StatusText = "已取消关联";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task ReorderAsync(IReadOnlyList<TodoItemViewModel> orderedItems)
    {
        try
        {
            await _database.ReorderTodoItemsAsync(
                Paper.Id,
                orderedItems.Select(item => item.Id).ToArray(),
                _lifetime.Token);
            StatusText = "顺序已保存";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            await ReloadItemsAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        if (_deletedItem is null)
        {
            return;
        }

        try
        {
            var restored = _deletedItem;
            _deletedItem = null;
            CanUndo = false;
            _undoExpiry?.Cancel();
            await _database.AppendTodoAsync(Paper.Id, restored, _lifetime.Token);
            await ReloadItemsAsync();
            StatusText = "已撤销";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    private void ShowUndo(TodoItem item)
    {
        _deletedItem = item;
        CanUndo = true;
        _undoExpiry?.Cancel();
        _undoExpiry?.Dispose();
        _undoExpiry = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _ = ClearUndoAfterDelayAsync(_undoExpiry.Token);
    }

    private async Task ClearUndoAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            _deletedItem = null;
            CanUndo = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReloadItemsAsync()
    {
        var items = await _database.GetTodoItemsAsync(Paper.Id, _lifetime.Token);
        ReplaceItems(items);
    }

    private void ReplaceItems(IEnumerable<TodoItem> items)
    {
        Items.Clear();
        foreach (var item in items.OrderBy(item => item.Order))
        {
            Items.Add(new TodoItemViewModel(item));
        }
    }

    public void Dispose()
    {
        _undoExpiry?.Cancel();
        _undoExpiry?.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
