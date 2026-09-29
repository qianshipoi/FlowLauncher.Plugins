using FlowLauncher.Plugin.AliyunOss;
using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.Transfer;
using FlowLauncher.Plugin.AliyunOss.ViewModels;

using Microsoft.Win32;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FlowLauncher.Plugin.AliyunOss.Views;

/// <summary>
/// 圆形气泡传输窗口：外圈为环形进度，中心显示进度/速率；
/// 传输中鼠标移入显示取消按钮，完成后显示主操作与关闭按钮。
/// 需要显示按钮时气泡缓动放大，给按钮留出余白（不使用遮罩）。
/// </summary>
public partial class TransferWindow : Window
{
    /// <summary>紧凑态直径：只显示进度文字时。</summary>
    private const double BubbleCompact = 152;

    /// <summary>展开态直径：需要容纳按钮时。</summary>
    private const double BubbleExpanded = 188;

    private readonly TransferViewModel _viewModel;
    private readonly ITransferHost _host;
    private readonly ILocalization _loc;
    private DownloadSession? _download;
    private IReadOnlyList<UploadRequest>? _batch;
    private bool _isHovering;
    private Point _dragOrigin;
    private bool _isDragging;
    private bool _dragSession;
    private bool _positionRestored;
    private double _bubbleTarget = BubbleCompact;

    private TransferWindow(TransferViewModel viewModel, ITransferHost host, ILocalization loc)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _host = host;
        _loc = loc;
        DataContext = viewModel;

        _viewModel.CloseRequested += Close;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closing += OnClosing;

        // 无边框窗口自带拖动：按住气泡任意位置移动
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        // 恢复上次位置（在屏幕内才生效），再进入首个状态
        Loaded += (_, _) =>
        {
            RestorePosition();
            UpdateVisualState();
        };
    }

    /// <summary>
    /// 恢复上次的气泡位置：记录存在且窗口至少有一部分落在虚拟屏幕（所有显示器拼合区域）内才应用，
    /// 否则保持 XAML 的屏幕居中。分辨率变化、拔掉外接屏后旧坐标可能落在任何屏幕之外。
    /// </summary>
    private void RestorePosition()
    {
        if (_positionRestored) return;
        _positionRestored = true;

        var saved = _host.LoadBubblePosition();
        if (saved is null) return;

        // WPF 的 VirtualScreen* 已经是设备无关像素，直接可比
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        var intersects =
            saved.X + ActualWidth > virtualLeft &&
            saved.X < virtualRight &&
            saved.Y + ActualHeight > virtualTop &&
            saved.Y < virtualBottom;

        if (!intersects) return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = saved.X;
        Top = saved.Y;
    }

    /// <summary>
    /// 环的颜色随状态变化：进行中蓝、冲突琥珀、确认取消深红、成功绿、失败红。
    /// 常驻气泡空闲时环不是进度（恒为 0），用浅灰避免被误读成"传到 0% 卡住了"；
    /// 有文件拖到上方时转回强调蓝，作为"可以松手"的反馈。
    /// </summary>
    private void UpdateRingColor()
    {
        var color = _viewModel switch
        {
            { HasError: true } => Color.FromRgb(0xCF, 0x22, 0x2E),
            { IsCancelPending: true } => Color.FromRgb(0x8A, 0x0C, 0x1F),
            { IsConflictVisible: true } => Color.FromRgb(0xD4, 0xA7, 0x2C),
            { IsFinished: true } => Color.FromRgb(0x1A, 0x7F, 0x37),
            { IsDragOver: true } => Color.FromRgb(0x09, 0x69, 0xDA),
            { IsDropIdle: true } => Color.FromRgb(0xC6, 0xCF, 0xD8),
            _ => Color.FromRgb(0x09, 0x69, 0xDA),
        };

        Ring.Foreground = new SolidColorBrush(color);
    }

    /// <summary>
    /// 状态切换：文字与按钮同层（文字在上、按钮在下），按钮不再是浮在文字上的浮层。
    /// 需要按钮时气泡缓动放大以留出余白，不使用任何遮罩。
    /// </summary>
    private void UpdateVisualState()
    {
        var conflict = _viewModel.IsConflictVisible;
        var cancelPending = _viewModel.IsCancelPending;
        var running = _viewModel.IsBusy && !conflict && !cancelPending;
        var finished = _viewModel.IsFinished;

        // 既不冲突、也不在传输、也未成功 → 终止态（失败 / 已取消 / 等待选择文件）
        var terminal = !conflict && !cancelPending && !running && !finished;

        // 取消确认期间保持展开（悬停与否不再影响）
        var showActions = running && _isHovering;

        // 常驻气泡的关闭按钮要一直够得着（它是唯一的出口），因此传输中该按钮组常显；
        // 组内的取消按钮仍按悬停出现，避免平时占着圆内空间
        var cancelPanelVisible = showActions || cancelPending || (_viewModel.IsDropZone && running);

        // 传输中显示「抬头 + 状态 + 速率」，其余状态共用一行短文案
        Apply(ProgressPanel, running);
        Apply(StateText, !running);
        StateText.Foreground = new SolidColorBrush(_viewModel.HasError
            ? Color.FromRgb(0xCF, 0x22, 0x2E)
            : Color.FromRgb(0x57, 0x60, 0x6A));

        Apply(CancelPanel, cancelPanelVisible);
        // 确认态下取消按钮始终可见（不随悬停消失）：对勾正等用户点击，鼠标短暂滑出不应隐藏它。
        // 常驻气泡里也常显：取消与关闭是两颗独立按钮，取消必须找得到
        Apply(CancelButton, showActions || cancelPending || _viewModel.IsDropZone);
        Apply(DonePanel, finished);
        Apply(ConflictPanel, conflict);
        Apply(TerminalCloseButton, terminal);

        // 取消按钮图标：确认态显示对勾，平时显示垃圾桶。
        // ToolTip 由绑定提供（见 TransferViewModel 的 CancelTooltip / CloseTooltip），不再在此处直接赋值
        ShowGlyph(TrashGlyphPath, !cancelPending);
        ShowGlyph(ConfirmGlyphPath, cancelPending);

        // 有按钮（或处于确认态）就用展开尺寸，纯进度态收紧
        var needsRoom = !running || showActions || _viewModel.IsDropZone;
        AnimateBubble(needsRoom ? BubbleExpanded : BubbleCompact);
    }

    /// <summary>在两个叠放的图标间切换（垃圾桶 ↔ 对勾）。</summary>
    private static void ShowGlyph(UIElement show, bool visible) =>
        show.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 缓动改变气泡直径。
    /// 窗口本身尺寸固定、气泡居中，所以缩放不会有窗口重排的顿挫；
    /// 用与显隐过渡同一档 170ms 的缓动，二者节奏一致。
    /// </summary>
    private void AnimateBubble(double diameter)
    {
        // 用记录的目标值判断，不能读 Width：动画进行中它返回的是当前插值，会反复重启
        if (Math.Abs(_bubbleTarget - diameter) < 0.5) return;
        _bubbleTarget = diameter;

        var duration = FadeTransition.Default;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        BubbleHost.BeginAnimation(
            FrameworkElement.WidthProperty,
            new DoubleAnimation(diameter, duration) { EasingFunction = ease });

        BubbleHost.BeginAnimation(
            FrameworkElement.HeightProperty,
            new DoubleAnimation(diameter, duration) { EasingFunction = ease });
    }

    /// <summary>
    /// 按目标可见性淡入或淡出。
    /// 显示时一律调用 Show：淡出途中可见性仍是 Visible，不能靠它判断，
    /// 否则从淡出切回显示时会停在半透明状态。
    /// </summary>
    private static void Apply(UIElement element, bool visible)
    {
        if (visible)
        {
            FadeTransition.Show(element);
        }
        else if (element.Visibility == Visibility.Visible)
        {
            FadeTransition.Hide(element);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TransferViewModel.IsBusy):
            case nameof(TransferViewModel.IsCompleted):
            case nameof(TransferViewModel.HasError):
            case nameof(TransferViewModel.IsConflictVisible):
            case nameof(TransferViewModel.IsCancelPending):
            case nameof(TransferViewModel.IsDropZone):
            case nameof(TransferViewModel.IsDragOver):
                UpdateRingColor();
                UpdateVisualState();
                break;
        }
    }

    /// <summary>
    /// 悬停判定按圆形范围：窗口是方形而气泡是圆形，
    /// 用 MouseEnter/MouseLeave 会把四角（气泡之外）也算作移入。
    /// 半径取气泡当前实际半径：气泡会随状态缩放，用固定值会在放大后判定偏小。
    /// </summary>
    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        // 拖动期间冻结悬停判定：窗口跟随鼠标移动有延迟，光标相对窗口的坐标会跳动，
        // 据此判定会误认为"移出气泡"而把气泡缩回紧凑尺寸。
        // 拖动本就意味着光标仍在气泡上，保持当前状态即可。
        if (_dragSession) return;

        SetHovering(IsInsideBubble(e.GetPosition(Root)));
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        // 拖动中光标可能短暂离开窗口矩形（窗口移动滞后于光标），
        // 此时不应判定为移出，否则气泡会在拖动途中缩回
        if (_dragSession) return;

        SetHovering(false);
    }

    /// <summary>光标是否落在气泡圆内（外缘放宽 2px，贴边时不抖动）。</summary>
    private bool IsInsideBubble(Point position)
    {
        var dx = position.X - Root.ActualWidth / 2;
        var dy = position.Y - Root.ActualHeight / 2;
        var radius = Math.Min(BubbleHost.ActualWidth, BubbleHost.ActualHeight) / 2 + 2;

        return dx * dx + dy * dy <= radius * radius;
    }

    private void SetHovering(bool value)
    {
        if (_isHovering == value) return;

        _isHovering = value;
        UpdateVisualState();
    }

    // ---- 拖动气泡 ----

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        var hit = InputHitTest(e.GetPosition(this)) as DependencyObject;

        // 按在按钮上时不要夺取鼠标捕获，否则按钮收不到抬起事件、点击失效
        if (IsInteractiveControl(hit)) return;

        // 确认态下点到按钮以外的任何区域 → 退出确认、恢复垃圾桶（视为放弃取消）
        _viewModel.CancelIfPending();

        _dragOrigin = e.GetPosition(this);
        _isDragging = false;
        _dragSession = true;

        // 按下瞬间的位置是准确的（窗口尚未移动），据此判定一次；
        // 之后整个拖动过程冻结判定，窗口跟随鼠标移动的滞后就不会被误判成"移出"
        SetHovering(IsInsideBubble(e.GetPosition(Root)));

        CaptureMouse();
    }

    /// <summary>命中目标或其祖先是否是按钮类控件（气泡里所有可点元素都是 Button）。</summary>
    private static bool IsInteractiveControl(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase) return true;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(this);
        var delta = _dragOrigin - current;

        // 阈值避免点击按钮时的微小抖动被当成拖动
        if (!_isDragging && Math.Abs(delta.X) < 3 && Math.Abs(delta.Y) < 3) return;
        _isDragging = true;

        Left -= delta.X;
        Top -= delta.Y;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();

        if (!_dragSession) return;
        _dragSession = false;

        // 拖动结束后按光标当前位置重新判定：松开时若已移出气泡就收回紧凑尺寸
        SetHovering(IsInsideBubble(e.GetPosition(Root)));

        // 真实拖动过才保存位置：单纯点击不算
        if (_isDragging)
        {
            _host.SaveBubblePosition(new BubblePosition(Left, Top));
        }
    }

    // ---- 窗口工厂 ----

    /// <summary>下载窗口。</summary>
    public static TransferWindow ForDownload(
        OssService service,
        Settings settings,
        ITransferHost host,
        string url,
        ILocalization loc)
    {
        var window = new TransferWindow(
            new TransferViewModel(new TransferEngine(service, loc), settings, host, loc), host, loc)
        {
            Title = loc.Get("transfer_title_download"),
        };
        window._download = new DownloadSession(url);
        window.Loaded += window.OnDownloadLoaded;
        return window;
    }

    /// <summary>批量上传窗口（拖拽场景）：打开即开始上传，无需文件选择框。</summary>
    public static TransferWindow ForUploadBatch(
        OssService service,
        Settings settings,
        ITransferHost host,
        IReadOnlyList<UploadRequest> requests,
        ILocalization loc)
    {
        var window = new TransferWindow(
            new TransferViewModel(new TransferEngine(service, loc), settings, host, loc), host, loc)
        {
            Title = requests.Count == 1 ? loc.Get("transfer_title_upload") : loc.Get("transfer_title_upload_many", requests.Count),
        };
        window._batch = requests;
        window.Loaded += window.OnBatchLoaded;
        return window;
    }

    /// <summary>
    /// 常驻拖拽上传气泡：打开后停在屏幕等待拖入，可连续使用。
    /// 传输完（或取消）都不自动关闭，由独立的关闭按钮结束。
    /// </summary>
    /// <param name="key">目标对象名；为空表示临时上传，每个文件现场生成随机路径。</param>
    public static TransferWindow ForDropUpload(
        OssService service,
        Settings settings,
        ITransferHost host,
        ILocalization loc,
        string bucket,
        string key,
        bool checkExists = false,
        bool forceBackup = false)
    {
        var window = new TransferWindow(
            new TransferViewModel(new TransferEngine(service, loc), settings, host, loc), host, loc)
        {
            Title = key.Length == 0 ? loc.Get("transfer_title_upload_temp") : loc.Get("transfer_title_upload"),
        };

        window._viewModel.ConfigureDropZone(bucket, key, checkExists, forceBackup);
        window.AllowDrop = true;
        window.DragEnter += window.OnDropDragEnter;
        window.DragOver += window.OnDropDragEnter;
        window.DragLeave += window.OnDropDragLeave;
        window.Drop += window.OnDropFiles;
        return window;
    }

    // ---- 常驻气泡的拖拽上传 ----

    /// <summary>
    /// 拖拽进入：仅接受文件拖放，其余（文本、图片等）光标显示禁止。
    /// DragOver 与 DragEnter 共用：前者必须持续设置 Effects，否则光标会在窗口内变回禁止。
    /// </summary>
    private void OnDropDragEnter(object sender, DragEventArgs e)
    {
        var accepts = FilesFrom(e).Length > 0;
        e.Effects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (accepts) _viewModel.SetDragOver(true);
    }

    private void OnDropDragLeave(object sender, DragEventArgs e)
    {
        e.Handled = true;
        _viewModel.SetDragOver(false);
    }

    private async void OnDropFiles(object sender, DragEventArgs e)
    {
        e.Handled = true;
        _viewModel.SetDragOver(false);

        var files = FilesFrom(e);
        if (files.Length == 0) return;

        await _viewModel.RunDropUploadAsync(files);
    }

    /// <summary>取出拖入的文件；非文件拖放（文本/图片等）返回空数组。</summary>
    private static string[] FilesFrom(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
            ? files
            : [];

    private async void OnDownloadLoaded(object sender, RoutedEventArgs e)
    {
        var session = _download!;

        var suggested = Path.GetFileName(new Uri(session.Url).AbsolutePath);
        var dialog = new SaveFileDialog
        {
            Title = _loc.Get("transfer_select_save_location"),
            FileName = string.IsNullOrWhiteSpace(suggested) ? "download" : Uri.UnescapeDataString(suggested),
        };

        if (dialog.ShowDialog(this) != true)
        {
            Close();
            return;
        }

        await _viewModel.RunDownloadAsync(session.Url, dialog.FileName);
    }

    private async void OnBatchLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RunUploadBatchAsync(_batch!);
    }

    /// <summary>
    /// 关闭窗口（含 Alt+F4）时统一收口。
    /// 传输中第一次关闭只拦下并进入取消确认态，已在确认态则视为确认关闭；
    /// 关闭按钮与确认取消会先置 <see cref="TransferViewModel.IsCloseConfirmed"/> 直接放行。
    /// 注意「取消」不在此列：常驻气泡里取消只中止本轮、气泡留在原地继续接拖拽。
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.IsCloseConfirmed) return;

        if (_viewModel.IsBusy)
        {
            if (_viewModel.IsCancelPending)
            {
                // 确认态下再关一次 = 确认关闭
                _viewModel.ConfirmCancel(closeAfter: true);
            }
            else
            {
                e.Cancel = true;
                _viewModel.RequestCancel();
            }

            return;
        }

        // 非传输中：确保不留下还在跑的后台任务
        _viewModel.Abort();
    }

    private sealed record DownloadSession(string Url);
}
