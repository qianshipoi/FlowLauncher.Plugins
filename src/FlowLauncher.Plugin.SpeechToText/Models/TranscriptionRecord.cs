namespace FlowLauncher.Plugin.SpeechToText;

/// <summary>转写历史记录条目。</summary>
public record TranscriptionRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Text { get; init; } = "";
    public string? AudioFilePath { get; init; }
    public TimeSpan Duration { get; init; }
    public string Model { get; init; } = "";
}
