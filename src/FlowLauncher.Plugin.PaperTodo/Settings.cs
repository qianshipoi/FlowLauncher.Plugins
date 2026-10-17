namespace FlowLauncher.Plugin.PaperTodo;

public enum TodoCompletionPolicy
{
    MoveToBottom,
    KeepInPlace,
    Delete,
}

public sealed class Settings
{
    public TodoCompletionPolicy CompletionPolicy { get; set; } = TodoCompletionPolicy.MoveToBottom;
    public bool RemindersEnabled { get; set; } = true;
    public bool ReminderSoundEnabled { get; set; }
    public bool AutoCompressImages { get; set; } = true;
    public int BackupRetentionCount { get; set; } = 7;
    public int RecentItemsCount { get; set; } = 10;
}
