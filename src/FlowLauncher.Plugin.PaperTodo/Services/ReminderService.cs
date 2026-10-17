using System.Windows.Threading;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class ReminderService(
    PaperDatabase database,
    Settings settings,
    Action<string> notify) : IDisposable
{
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMinutes(1),
    };
    private bool _started;
    private bool _checking;

    public void Start()
    {
        if (_started)
        {
            return;
        }
        _started = true;
        _timer.Tick += Timer_Tick;
        _timer.Start();
        _ = CheckAsync();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        await CheckAsync();
    }

    private async Task CheckAsync()
    {
        if (_checking || !settings.RemindersEnabled)
        {
            return;
        }

        _checking = true;
        try
        {
            var now = DateTimeOffset.Now;
            var due = await database.GetDueRemindersAsync(now);
            foreach (var item in due)
            {
                notify(item.Text);
                if (settings.ReminderSoundEnabled)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
                await database.MarkReminderTriggeredAsync(item.Id, now);
            }
        }
        catch
        {
            // Reminder checks are best effort; database errors are surfaced by normal CRUD paths.
        }
        finally
        {
            _checking = false;
        }
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }
        _started = false;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }
}
