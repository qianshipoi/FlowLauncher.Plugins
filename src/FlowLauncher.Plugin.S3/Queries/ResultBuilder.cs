using Flow.Launcher.Plugin;

namespace FlowLauncher.Plugin.S3.Queries;

public class ResultBuilder
{
    private readonly string _title;
    private string? _subtitle;
    private Func<ActionContext, ValueTask<bool>>? _asyncAction;
    private Func<ActionContext, bool>? _action;
    private Result.PreviewInfo? _previewInfo;

    private ResultBuilder(string title)
    {
        _title = title;
    }

    public ResultBuilder Subtitle(string subtitle)
    {
        _subtitle = subtitle;
        return this;
    }

    public ResultBuilder AsyncAction(Func<ActionContext, ValueTask<bool>> asyncAction)
    {
        _asyncAction = asyncAction;
        return this;
    }

    public ResultBuilder AsyncAction(Func<ValueTask<bool>> asyncAction)
    {
        _asyncAction = _ => asyncAction();
        return this;
    }

    public ResultBuilder AsyncAction(Func<ValueTask> asyncAction)
    {
        _asyncAction = async _ => { await asyncAction(); return true; };
        return this;
    }

    public ResultBuilder Action(Func<ActionContext, bool> action)
    {
        _action = action;
        return this;
    }

    public ResultBuilder Action(Action<ActionContext> action)
    {
        _action = _ => { action(_); return true; };
        return this;
    }

    public ResultBuilder Action(Action action, bool result = true)
    {
        _action = _ => { action(); return result; };
        return this;
    }

    public ResultBuilder Preview(Result.PreviewInfo previewInfo)
    {
        _previewInfo = previewInfo;
        return this;
    }

    public static ResultBuilder Create(string title) => new(title);

    public Result Build()
    {
        return new Result
        {
            IcoPath = "favicon.ico",
            Title = _title,
            SubTitle = _subtitle,
            AsyncAction = _asyncAction,
            Action = _action,
            // Preview = _previewInfo,
        };
    }
}
