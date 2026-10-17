using CommunityToolkit.Mvvm.ComponentModel;

using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;

namespace FlowLauncher.Plugin.PaperTodo.ViewModels;

internal partial class NotePaperViewModel : ObservableObject
{
    private readonly PaperDatabase _database;

    public NotePaperViewModel(PaperDatabase database, Paper paper)
    {
        _database = database;
        Paper = paper;
        _title = paper.Title;
        _content = paper.Content;
    }

    public Paper Paper { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _content;

    [ObservableProperty]
    private string _statusText = "已保存";

    public async Task SaveTitleAsync(CancellationToken token = default)
    {
        var normalized = string.IsNullOrWhiteSpace(Title) ? "未命名笔记" : Title.Trim();
        if (normalized.Length > 80)
        {
            normalized = normalized[..80];
        }
        Title = normalized;
        await _database.UpdateTitleAsync(Paper.Id, normalized, token);
        Paper.Title = normalized;
    }

    public async Task SaveContentAsync(string markdown, CancellationToken token = default)
    {
        var stored = MarkdownImageReferences.ToStoredMarkdown(markdown);
        await _database.UpdateNoteContentAsync(Paper.Id, stored, token);
        await _database.PruneUnreferencedImagesAsync(
            Paper.Id,
            MarkdownImageReferences.CollectImageIds(stored),
            token);
        Content = stored;
        Paper.Content = stored;
    }
}
