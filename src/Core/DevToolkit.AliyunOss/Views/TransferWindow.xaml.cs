using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Services;
using DevToolkit.AliyunOss.Transfer;
using DevToolkit.AliyunOss.Utils;
using DevToolkit.AliyunOss.ViewModels;

using Microsoft.Win32;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DevToolkit.AliyunOss.Views;

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
    private UploadSession? _upload;
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

    /// <summary>环的颜色随状态变化：进行中蓝、冲突琥珀、确认取消深红、成功绿、失败红。</summary>
    private void UpdateRingColor()
    {
        var color = _viewModel switch
        {
            { HasError: true } => Color.FromRgb(0xCF, 0x22, 0x2E),
            { IsCancelPending: true } => Color.FromRgb(0x8A, 0x0C, 0x1F),
            { IsConflictVisible: true } => Color.FromRgb(0xD4, 0xA7, 0x2C),
            { IsFinished: true } => Color.FromRgb(0x1A, 0x7F, 0x37),
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

        // 传输中显示「抬头 + 状态 + 速率」，其余状态共用一行短文案
        Apply(ProgressPanel, running);
        Apply(StateText, !running);
        StateText.Foreground = new SolidColorBrush(_viewModel.HasError
            ? Color.FromRgb(0xCF, 0x22, 0x2E)
            : Color.FromRgb(0x57, 0x60, 0x6A));

        // 确认态下取消按钮始终可见（不随悬停消失）：对勾正等用户点击，鼠标短暂滑出不应隐藏它
        Apply(CancelButton, showActions || cancelPending);
        Apply(DonePanel, finished);
        Apply(ConflictPanel, conflict);
        Apply(TerminalCloseButton, terminal);

        // 取消按钮图标：确认态显示对勾，平时显示垃圾桶。
        // ToolTip 由绑定提供（见 TransferViewModel 的 CancelTooltip / CloseTooltip），不再在此处直接赋值
        ShowGlyph(TrashGlyphPath, !cancelPending);
        ShowGlyph(ConfirmGlyphPath, cancelPending);

        // 有按钮（或处于确认态）就用展开尺寸，纯进度态收紧
        var needsRoom = !running || showActions;
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

    /// <summary>上传窗口。对象名在文件选定后确定（临时上传时现场生成）。</summary>
    public static TransferWindow ForUpload(
        OssService service,
        Settings settings,
        ITransferHost host,
        ILocalization loc,
        string bucket,
        string key,
        bool checkExists = false,
        bool forceBackup = false,
        bool tempUpload = false)
    {
        var window = new TransferWindow(
            new TransferViewModel(new TransferEngine(service, loc), settings, host, loc), host, loc)
        {
            Title = tempUpload ? loc.Get("transfer_title_upload_temp") : loc.Get("transfer_title_upload"),
        };
        window._upload = new UploadSession(bucket, key, checkExists, forceBackup, tempUpload);
        window.Loaded += window.OnUploadLoaded;
        return window;
    }

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

    private async void OnUploadLoaded(object sender, RoutedEventArgs e)
    {
        var session = _upload!;

        var dialog = new OpenFileDialog
        {
            Title = _loc.Get("transfer_select_file"),
            Multiselect = false,
            DefaultExt = session.TempUpload ? null : Path.GetExtension(session.Key),
            Filter = session.TempUpload ? FileDialogFilter.AllFiles(_loc.Get("transfer_filter_all")) : GetFilter(session.Key),
        };

        if (dialog.ShowDialog(this) != true)
        {
            Close();
            return;
        }

        var key = session.TempUpload
            ? TransferEngine.BuildTempKey(dialog.FileName)
            : session.Key;

        await _viewModel.RunUploadAsync(
            new UploadRequest(session.Bucket, key, dialog.FileName),
            session.CheckExists,
            session.ForceBackup);
    }

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
    /// 传输进行中关闭（含 Alt+F4）：与取消按钮同一条确认路径。
    /// 第一次关闭请求只进入确认态；用户确认后再次关闭（或确认取消流程主动触发）才放行。
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.IsBusy) return;

        // 取消已被确认（确认按钮/再次 Alt+F4）→ 放行；否则拦截并进入确认态
        if (_viewModel.IsCancelConfirmed || _viewModel.IsCancelPending && _closingAfterConfirm) return;

        e.Cancel = true;
        _closingAfterConfirm = true;
        _viewModel.RequestCancel();
    }

    /// <summary>窗口关闭请求已走过一次确认，放行下一次 Closing。</summary>
    private bool _closingAfterConfirm;

    /// <summary>
    /// 文件选择框的过滤器。描述文案来自本地化文件，模式（<c>*.*</c>/<c>*.ext</c>）
    /// 由 <see cref="FileDialogFilter"/> 拼接，翻译改文案不会破坏过滤器语法。
    /// </summary>
    private string GetFilter(string key) =>
        FileDialogFilter.FromObjectKey(
            key, _loc.Get("transfer_filter_all"), _loc.Get("transfer_filter_named"));

    private sealed record UploadSession(
        string Bucket,
        string Key,
        bool CheckExists,
        bool ForceBackup,
        bool TempUpload);

    private sealed record DownloadSession(string Url);
}
