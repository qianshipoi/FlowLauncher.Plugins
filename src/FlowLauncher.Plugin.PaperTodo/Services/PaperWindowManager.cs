using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Views;

using System.Windows;
using System.Windows.Threading;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class PaperWindowManager(
    PaperDatabase database,
    Settings settings,
    NoteImageService imageService,
    string webRoot,
    string webViewDataDirectory)
{
    private readonly Dictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DispatcherTimer> _geometryTimers = new(StringComparer.Ordinal);

    public void OpenPaper(Paper paper, string? focusItemId = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_windows.TryGetValue(paper.Id, out var existing))
            {
                ActivateWindow(existing);
                if (existing is TodoPaperWindow todo)
                {
                    todo.FocusItem(focusItemId);
                }
                return;
            }

            Window window = paper.IsTodo
                ? new TodoPaperWindow(database, settings, paper, this)
                : new NotePaperWindow(
                    database,
                    paper,
                    settings,
                    imageService,
                    this,
                    webRoot,
                    webViewDataDirectory);
            PrepareGeometry(window, paper);
            _windows[paper.Id] = window;
            AttachWindow(window, paper.Id);
            window.Show();
            window.Activate();
            if (window is TodoPaperWindow todoWindow)
            {
                todoWindow.FocusItem(focusItemId);
            }
        });
    }

    public async Task OpenPaperByIdAsync(string paperId)
    {
        var paper = await database.GetPaperAsync(paperId);
        if (paper is null)
        {
            return;
        }
        OpenPaper(paper);
    }

    public void SaveGeometrySoon(Window window, string paperId)
    {
        if (!_geometryTimers.TryGetValue(paperId, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                await SaveGeometryAsync(window, paperId);
            };
            _geometryTimers[paperId] = timer;
        }
        timer.Stop();
        timer.Start();
    }

    public void CloseAll()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var window in _windows.Values.ToArray())
            {
                switch (window)
                {
                    case TodoPaperWindow todo:
                        todo.CloseWithoutPrompt();
                        break;
                    case NotePaperWindow note:
                        note.CloseWithoutPrompt();
                        break;
                    default:
                        window.Close();
                        break;
                }
            }
            _windows.Clear();
            foreach (var timer in _geometryTimers.Values)
            {
                timer.Stop();
            }
            _geometryTimers.Clear();
        });
    }

    private void AttachWindow(Window window, string paperId)
    {
        window.LocationChanged += (_, _) => SaveGeometrySoon(window, paperId);
        window.SizeChanged += (_, _) => SaveGeometrySoon(window, paperId);
        window.Closed += (_, _) =>
        {
            _windows.Remove(paperId);
            if (_geometryTimers.Remove(paperId, out var timer))
            {
                timer.Stop();
            }
            _ = SaveGeometryAsync(window, paperId);
        };
    }

    private static void PrepareGeometry(Window window, Paper paper)
    {
        var workArea = SystemParameters.WorkArea;
        window.Width = Math.Clamp(paper.Width, window.MinWidth, workArea.Width);
        window.Height = Math.Clamp(paper.Height, window.MinHeight, workArea.Height);
        var maxLeft = workArea.Right - window.Width;
        var maxTop = workArea.Bottom - window.Height;
        window.Left = Math.Clamp(paper.X, workArea.Left, Math.Max(workArea.Left, maxLeft));
        window.Top = Math.Clamp(paper.Y, workArea.Top, Math.Max(workArea.Top, maxTop));
        window.Topmost = paper.AlwaysOnTop;
    }

    private static void ActivateWindow(Window window)
    {
        var wasTopmost = window.Topmost;
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Show();
        window.Topmost = true;
        window.Activate();
        if (!wasTopmost)
        {
            window.Topmost = false;
        }
    }

    private async Task SaveGeometryAsync(Window window, string paperId)
    {
        try
        {
            var bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;
            if (bounds.IsEmpty || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height))
            {
                return;
            }
            await database.SaveGeometryAsync(
                paperId,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                window.Topmost);
        }
        catch
        {
            // Geometry persistence is best effort and must not block window close.
        }
    }
}
