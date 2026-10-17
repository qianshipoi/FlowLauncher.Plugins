using CommunityToolkit.Mvvm.ComponentModel;

using FlowLauncher.Plugin.PaperTodo.Models;

using System.IO;

namespace FlowLauncher.Plugin.PaperTodo.ViewModels;

public partial class TodoItemViewModel : ObservableObject
{
    public TodoItemViewModel(TodoItem model)
    {
        Model = model;
        _text = model.Text;
        _isDone = model.Done;
    }

    public TodoItem Model { get; private set; }

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isDone;

    public string Id => Model.Id;
    public string PaperId => Model.PaperId;
    public DateTimeOffset? ReminderAt => Model.ReminderAt;
    public string? LinkedPaperId => Model.LinkedPaperId;
    public string? LinkedPath => Model.LinkedPath;

    public string ReminderLabel => Model.ReminderAt is { } reminder
        ? reminder.ToLocalTime().ToString("MM-dd HH:mm")
        : "";

    public string LinkLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Model.LinkedPaperId))
            {
                return "笔记";
            }
            if (string.IsNullOrWhiteSpace(Model.LinkedPath))
            {
                return "";
            }

            var path = Model.LinkedPath;
            var name = Model.LinkedPathIsDirectory == true
                ? new DirectoryInfo(path).Name
                : Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? "文件" : name;
        }
    }

    public bool IsOverdue =>
        !IsDone &&
        Model.ReminderAt is { } reminder &&
        reminder <= DateTimeOffset.Now &&
        Model.ReminderTriggeredAt is null;

    public void ApplyEditorText(string text)
    {
        Text = text;
        Model.Text = text;
    }

    public void Refresh(TodoItem model)
    {
        Model = model;
        Text = model.Text;
        IsDone = model.Done;
        OnPropertyChanged(nameof(ReminderLabel));
        OnPropertyChanged(nameof(LinkLabel));
        OnPropertyChanged(nameof(IsOverdue));
    }

    public void NotifyReminderChanged()
    {
        OnPropertyChanged(nameof(ReminderLabel));
        OnPropertyChanged(nameof(IsOverdue));
    }

    public void NotifyLinkChanged()
    {
        OnPropertyChanged(nameof(LinkLabel));
        OnPropertyChanged(nameof(LinkedPaperId));
        OnPropertyChanged(nameof(LinkedPath));
    }
}
