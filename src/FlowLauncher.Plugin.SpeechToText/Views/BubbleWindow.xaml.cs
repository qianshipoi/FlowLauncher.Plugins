using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace FlowLauncher.Plugin.SpeechToText.Views;

public partial class BubbleWindow : Window
{
    private readonly FlowLauncher.Plugin.SpeechToText.Settings _settings;
    private readonly Action _saveSettings;
    private bool _ready;

    public BubbleWindow(FlowLauncher.Plugin.SpeechToText.Settings settings, Action saveSettings)
    {
        InitializeComponent();
        _settings = settings;
        _saveSettings = saveSettings;
        Loaded += (_, _) => RestorePosition();
        LocationChanged += (_, _) => RememberPosition();
        Closed += (_, _) => SavePosition();
    }

    /// <summary>设置 ViewModel 并绑定事件。</summary>
    public void BindViewModel(FlowLauncher.Plugin.SpeechToText.SpeechToTextViewModel vm)
    {
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.RecordingStopped += OnRecordingStopped;
    }

    /// <summary>实时识别时结果持续增长，自动滚到底部以显示最新内容。</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FlowLauncher.Plugin.SpeechToText.SpeechToTextViewModel.ResultText)) return;

        // ResultText 由后台线程（识别回调）更新，需切回 UI 线程操作控件
        ResultScroller.Dispatcher.InvokeAsync(() => ResultScroller.ScrollToEnd());
    }

    /// <summary>无边框窗口需自行处理拖拽；按钮会吃掉该事件，点击按钮不会误触发拖拽。</summary>
    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount > 1) return;
        DragMove();
    }

    /// <summary>恢复上次记录的位置；无记录或位置已失效（如拔掉外接屏）时回退到默认定位。</summary>
    private void RestorePosition()
    {
        _ready = true;

        var left = _settings.BubbleLeft;
        var top = _settings.BubbleTop;
        if (left.HasValue && top.HasValue && IsVisibleOnVirtualScreen(left.Value, top.Value))
        {
            Left = left.Value;
            Top = top.Value;
            return;
        }

        PositionNearScreenCenter();
        RememberPosition();
    }

    /// <summary>记录当前位置（仅更新内存，关闭时才落盘，避免拖拽过程中频繁写文件）。</summary>
    private void RememberPosition()
    {
        if (!_ready) return;
        _settings.BubbleLeft = Left;
        _settings.BubbleTop = Top;
    }

    private void SavePosition()
    {
        if (!_ready) return;

        RememberPosition();
        try
        {
            _saveSettings();
        }
        catch
        {
            // 位置保存失败不应影响窗口关闭
        }
    }

    /// <summary>校验位置是否仍落在虚拟屏幕内，避免窗口跑到不可见区域。</summary>
    private bool IsVisibleOnVirtualScreen(double left, double top)
    {
        const double margin = 8;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : MinHeight;

        return left + width > SystemParameters.VirtualScreenLeft + margin
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - margin
            && top + height > SystemParameters.VirtualScreenTop + margin
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - margin;
    }

    private void PositionNearScreenCenter()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 20;
        Top = screen.Bottom - Height - 60;
    }

    private void OnRecordingStopped()
    {
        Dispatcher.Invoke(() =>
        {
            CopyBtn.Visibility = Visibility.Visible;
            StopBtn.Content = "关闭";
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FlowLauncher.Plugin.SpeechToText.SpeechToTextViewModel vm
            && !string.IsNullOrEmpty(vm.ResultText))
        {
            System.Windows.Clipboard.SetText(vm.ResultText);
            CopyBtn.Content = "已复制 ✓";
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FlowLauncher.Plugin.SpeechToText.SpeechToTextViewModel vm && vm.IsRecording)
            vm.StopCommand.Execute(null);
        else
            Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is FlowLauncher.Plugin.SpeechToText.SpeechToTextViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.RecordingStopped -= OnRecordingStopped;
            if (vm.IsRecording)
                vm.StopCommand.Execute(null);
        }
    }
}
