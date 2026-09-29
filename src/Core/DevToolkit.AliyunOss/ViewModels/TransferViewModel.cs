using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevToolkit.AliyunOss.Models;
using DevToolkit.AliyunOss.Transfer;
using DevToolkit.AliyunOss.Utils;

using System.IO;
using System.Windows.Threading;

namespace DevToolkit.AliyunOss.ViewModels;

/// <summary>
/// 传输窗口的 ViewModel：自行汇报进度、取消、复制链接、打开文件夹，
/// 不依赖宿主（Flow Launcher）的进度弹窗。
/// </summary>
public partial class TransferViewModel : ObservableObject, ITransferUi
{
    /// <summary>UI 刷新节流间隔；进度回调可能远密于此。</summary>
    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(100);

    private readonly TransferEngine _engine;
    private readonly Settings _settings;
    private readonly ITransferHost _host;
    private readonly ILocalization _loc;
    private readonly TransferRateTracker _rate = new();
    private readonly IProgress<TransferProgress> _progress;

    private CancellationTokenSource? _cts;
    private TaskCompletionSource<UploadConflictChoice>? _conflict;
    private DateTimeOffset _lastUiUpdate = DateTimeOffset.MinValue;

    public TransferViewModel(TransferEngine engine, Settings settings, ITransferHost host, ILocalization loc)
    {
        _engine = engine;
        _settings = settings;
        _host = host;
        _loc = loc;
        // 在 UI 线程构造，Progress 会把回调投递回 UI 线程
        _progress = new Progress<TransferProgress>(OnProgress);
    }

    /// <summary>取本地化文案的快捷方式。</summary>
    private string T(string key, params object[] args) => _loc.Get(key, args);

    /// <summary>窗口关闭请求（取消/关闭按钮触发）。</summary>
    public event Action? CloseRequested;

    [ObservableProperty]
    private string _targetText = "";

    /// <summary>短状态文案，气泡中央显示（受圆形空间限制，务必简短）。</summary>
    [ObservableProperty]
    private string _statusText = "";

    /// <summary>中心大字：百分比，如「45%」。长度未知时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressHeadline))]
    [NotifyPropertyChangedFor(nameof(ProgressHeadlineFontSize))]
    [NotifyPropertyChangedFor(nameof(HasProgressHeadline))]
    private string _percentText = "";

    /// <summary>已传/总字节，点击抬头可切换显示它。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressHeadline))]
    [NotifyPropertyChangedFor(nameof(ProgressHeadlineFontSize))]
    [NotifyPropertyChangedFor(nameof(HasProgressHeadline))]
    private string _detailText = "";

    /// <summary>抬头是否显示字节数而非百分比（点击切换）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressHeadline))]
    [NotifyPropertyChangedFor(nameof(ProgressHeadlineFontSize))]
    private bool _showSizeInsteadOfPercent;

    /// <summary>中心小字：实时速率，如「1.2 MB/s」。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeedText))]
    private string _speedText = "";

    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    /// <summary>
    /// 是否正在传输。初始为 false：文件/保存位置选择框弹出时尚未开始传输，
    /// 此时取消选择应能直接关闭窗口；<see cref="Begin"/> 在流程真正开始时置为 true。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>失败时的完整错误信息；圆形空间放不下，放到气泡的 ToolTip 上。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloseTooltip))]
    private string _errorText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLink))]
    [NotifyPropertyChangedFor(nameof(DoneShowsCopy))]
    private string _link = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DoneShowsReveal))]
    [NotifyPropertyChangedFor(nameof(DoneShowsCopy))]
    private bool _hasLocalFile;

    [ObservableProperty]
    private bool _isConflictVisible;

    [ObservableProperty]
    private string _conflictMessage = "";

    /// <summary>
    /// 取消确认中：点过取消后进入此状态，等用户二次确认；
    /// 超时未确认则自动回到传输中，避免误触后传输被悄悄丢弃。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelTooltip))]
    private bool _isCancelPending;

    /// <summary>
    /// 取消已被用户确认：此后窗口的 Close 请求（来自确认取消）直接放行，
    /// 不再被 OnClosing 的确认流程拦下重新询问。
    /// </summary>
    [ObservableProperty]
    private bool _isCancelConfirmed;

    /// <summary>本地文件路径（下载完成后用于定位文件）。</summary>
    private string? _localPath;

    public bool HasLink => !string.IsNullOrEmpty(Link);

    /// <summary>
    /// 完成态的主操作：下载的产物是本地文件，主操作是「打开文件所在位置」；上传才需要复制链接。
    /// </summary>
    public bool DoneShowsReveal => HasLocalFile;

    public bool DoneShowsCopy => !HasLocalFile && HasLink;

    /// <summary>传输进行中：环形进度可见，鼠标移入时中央换成取消图标。</summary>
    public bool IsRunning => IsBusy;

    /// <summary>成功结束：中央换成主操作按钮（下载为「打开文件所在位置」，上传为「复制链接」）与「关闭」。</summary>
    public bool IsFinished => IsCompleted && !IsBusy;

    /// <summary>中心小字在有待显示内容时才占位，避免空行把布局顶偏。</summary>
    public bool HasSpeedText => !string.IsNullOrEmpty(SpeedText);

    /// <summary>
    /// 抬头文字：默认显示百分比，点击后切换为已传/总字节。
    /// 字节数可能很长（如「1024.00 GB / 2048.00 GB」），字号需按长度自适应。
    /// </summary>
    public string ProgressHeadline => ShowSizeInsteadOfPercent ? DetailText : PercentText;

    /// <summary>
    /// 抬头字号按可用宽度反推，避免大数据量时超出圆内被裁掉。
    /// 上限 20 是百分比态的视觉基准，下限 10 保证 132px 圆内仍可读。
    /// </summary>
    public double ProgressHeadlineFontSize =>
        TextFit.FontSizeFor(ProgressHeadline, HeadlineAvailableWidth, min: 10, max: 20);

    /// <summary>抬头可用宽度：内容区 132px 减去左右各 8px 留白。</summary>
    private const double HeadlineAvailableWidth = 116;

    public bool HasProgressHeadline => !string.IsNullOrEmpty(ProgressHeadline);

    // ---- 工具提示文案（随语言切换；窗口只负责绑定）----

    /// <summary>取消按钮提示：确认态下换成"再点一次确认取消"。</summary>
    public string CancelTooltip =>
        IsCancelPending ? T("transfer_cancel_confirm") : T("transfer_cancel_button");

    /// <summary>关闭按钮提示：失败时显示完整错误信息（圆内放不下）。</summary>
    public string CloseTooltip =>
        string.IsNullOrEmpty(ErrorText) ? T("transfer_close_tooltip") : ErrorText;

    public string HeadlineTooltip => T("transfer_headline_toggle");

    public string OpenFolderTooltip => T("transfer_open_folder_tooltip");

    public string CopyLinkTooltip => T("transfer_copy_link_tooltip");

    public string ConflictBackupTooltip => T("transfer_conflict_backup");

    public string ConflictReplaceTooltip => T("transfer_conflict_replace");

    public string ConflictCancelTooltip => T("transfer_conflict_cancel");

    // ---- 流程入口 ----

    /// <summary>上传：可选地先检查同名对象/备份，再上传并生成访问链接。</summary>
    public async Task RunUploadAsync(UploadRequest request, bool checkExists, bool forceBackup)
    {
        var cts = Begin();
        try
        {
            SetTarget($"{request.Bucket}/{request.Key}");

            if (checkExists || forceBackup)
            {
                var choice = await _engine.ResolveConflictAsync(
                    request.Bucket, request.Key, forceBackup, this, cts.Token);

                if (choice == UploadConflictChoice.Cancel)
                {
                    FinishCancelled();
                    return;
                }
            }

            var outcome = await _engine.UploadAsync(request, this, _progress, cts.Token);
            Complete(outcome);
        }
        catch (OperationCanceledException)
        {
            FinishCancelled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    /// <summary>批量上传（拖拽场景）：逐个上传，进度按当前文件显示，结束后汇总。</summary>
    public async Task RunUploadBatchAsync(IReadOnlyList<UploadRequest> requests)
    {
        var cts = Begin();
        var total = requests.Count;
        var succeeded = 0;
        string? lastLink = null;
        string? failure = null;

        try
        {
            for (var i = 0; i < total; i++)
            {
                cts.Token.ThrowIfCancellationRequested();

                var request = requests[i];
                var label = $"{i + 1}/{total}";
                SetTarget($"{request.Bucket}/{request.Key}");
                StatusText = T("transfer_status_uploading_item", label);

                ResetProgress();

                try
                {
                    var outcome = await _engine.UploadAsync(request, this, _progress, cts.Token);
                    lastLink = outcome.Link;
                    succeeded++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 单个文件失败不中断整批，记录后在结尾汇总
                    failure = $"{Path.GetFileName(request.FilePath)}: {ex.Message}";
                    StatusText = $"{T("transfer_status_failed")} {label}: {ex.Message}";
                }
            }

            IsBusy = false;
            IsCompleted = true;
            IsIndeterminate = false;
            Percentage = 100;
            Link = lastLink ?? "";
            PercentText = "";
            DetailText = "";
            SpeedText = "";

            if (Link.Length > 0 && _settings.AutoCopyLink)
            {
                _host.CopyText(Link);
            }

            StatusText = failure is null
                ? T("transfer_status_uploaded", succeeded)
                : string.Format(T("transfer_status_partial"), succeeded, total);

            if (failure is not null)
            {
                HasError = true;
                ErrorText = failure;
            }
        }
        catch (OperationCanceledException)
        {
            FinishCancelled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    /// <summary>
    /// 下载：保存到指定路径。
    /// 完成后不自动弹出资源管理器（那会抢焦点），改由用户点「打开文件所在位置」按钮决定。
    /// </summary>
    public async Task RunDownloadAsync(string url, string filePath)
    {
        var cts = Begin();
        try
        {
            SetTarget(filePath);

            var outcome = await _engine.DownloadAsync(url, filePath, this, _progress, cts.Token);
            Complete(outcome);
        }
        catch (OperationCanceledException)
        {
            FinishCancelled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    // ---- ITransferUi ----

    public void SetStatus(string text) => StatusText = text;

    private void SetTarget(string text) => TargetText = text;

    public Task<UploadConflictChoice> AskConflictAsync(string objectName, CancellationToken cancellationToken)
    {
        ConflictMessage = T("transfer_conflict_exists", objectName);
        _conflict = new TaskCompletionSource<UploadConflictChoice>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IsConflictVisible = true;
        return _conflict.Task.WaitAsync(cancellationToken);
    }

    // ---- 命令 ----

    /// <summary>
    /// 取消按钮：同一颗按钮按阶段分派。
    /// 非传输中 → 直接关闭；传输中首次点 → 垃圾桶变对勾进入确认态；对勾态再点 → 确认取消。
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (!IsBusy)
        {
            CloseRequested?.Invoke();
            return;
        }

        if (IsCancelPending)
        {
            ConfirmCancel();
            return;
        }

        RequestCancel();
    }

    /// <summary>进入取消确认态：由取消按钮首次点击或窗口关闭（Alt+F4）触发。</summary>
    public void RequestCancel() => EnterCancelPending();

    /// <summary>确认态超时。超时或点击其它区域都会恢复垃圾桶图标，传输继续。</summary>
    private static readonly TimeSpan CancelPendingTimeout = TimeSpan.FromSeconds(5);

    /// <summary>确认态超时定时器。</summary>
    private DispatcherTimer? _cancelPendingTimer;

    private void EnterCancelPending()
    {
        IsCancelPending = true;
        StatusText = T("transfer_cancel_confirm");

        _cancelPendingTimer?.Stop();
        _cancelPendingTimer = new DispatcherTimer { Interval = CancelPendingTimeout };
        _cancelPendingTimer.Tick += (_, _) =>
        {
            _cancelPendingTimer?.Stop();
            if (!IsCancelPending) return;
            ExitCancelPending();
        };
        _cancelPendingTimer.Start();
    }

    /// <summary>退出确认态、恢复垃圾桶图标与传输文案。鼠标点到按钮外或超时都会走到这里。</summary>
    public void ExitCancelPending()
    {
        _cancelPendingTimer?.Stop();
        IsCancelPending = false;
        IsCancelConfirmed = false;
        StatusText = T("transfer_status_uploading");
    }

    /// <summary>若处于确认态则退出（点按钮外的分支调用）；不在确认态时无操作。</summary>
    public void CancelIfPending()
    {
        if (IsCancelPending) ExitCancelPending();
    }

    /// <summary>用户确认取消：中止传输并请求关闭窗口。对勾点击与 Alt+F4 确认共用。</summary>
    public void ConfirmCancel()
    {
        _cancelPendingTimer?.Stop();
        IsCancelPending = false;
        IsCancelConfirmed = true;
        CancelAndClose();
    }

    /// <summary>中止进行中的传输并请求关闭窗口（仅确认取消与窗口关闭路径调用）。</summary>
    public void CancelAndClose()
    {
        StatusText = T("transfer_status_cancelled");
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        CloseRequested?.Invoke();
    }

    /// <summary>点击抬头在「百分比」与「已传/总字节」之间切换。</summary>
    [RelayCommand]
    private void ToggleHeadline() => ShowSizeInsteadOfPercent = !ShowSizeInsteadOfPercent;

    [RelayCommand]
    private void CopyLink()
    {
        if (!HasLink) return;
        _host.CopyText(Link);
        StatusText = T("transfer_status_copied");
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (_localPath is null) return;
        _host.RevealInExplorer(_localPath);
    }

    [RelayCommand]
    private void ChooseReplace() => ResolveConflict(UploadConflictChoice.Replace);

    [RelayCommand]
    private void ChooseBackupAndReplace() => ResolveConflict(UploadConflictChoice.BackupAndReplace);

    [RelayCommand]
    private void ChooseCancelReplace() => ResolveConflict(UploadConflictChoice.Cancel);

    /// <summary>窗口关闭时由代码调用，中止进行中的传输。</summary>
    public void Abort()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // ---- 内部 ----

    private CancellationTokenSource Begin()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _rate.Reset();

        _lastUiUpdate = DateTimeOffset.MinValue;
        _localPath = null;
        IsBusy = true;
        IsCompleted = false;
        HasError = false;
        ErrorText = "";
        IsConflictVisible = false;
        Link = "";
        HasLocalFile = false;
        Percentage = 0;
        IsIndeterminate = true;
        PercentText = "";
        DetailText = "";
        SpeedText = "";
        ShowSizeInsteadOfPercent = false;
        IsCancelPending = false;
        IsCancelConfirmed = false;
        StatusText = T("transfer_status_preparing");
        return _cts;
    }

    private void OnProgress(TransferProgress progress)
    {
        _rate.Report(progress, DateTimeOffset.Now);

        var now = DateTimeOffset.Now;
        var isFinal = progress.Total > 0 && progress.Transferred >= progress.Total;
        if (!isFinal && now - _lastUiUpdate < UiUpdateInterval) return;
        _lastUiUpdate = now;

        var snapshot = _rate.Snapshot();
        Percentage = snapshot.Fraction * 100;
        IsIndeterminate = !snapshot.HasTotal;
        ApplySnapshot(snapshot);
    }

    /// <summary>
    /// 把一次进度采样拆成气泡中心的三个字段（百分比 / 字节 / 速率）。
    /// 字节用紧凑格式：圆形中央宽度有限，大容量下省略小数位才放得下。
    /// </summary>
    private void ApplySnapshot(TransferSnapshot snapshot)
    {
        PercentText = snapshot.HasTotal ? $"{snapshot.Fraction * 100:F0}%" : "";
        DetailText = snapshot.HasTotal
            ? $"{FileSizeFormatter.FormatCompact(snapshot.Transferred)} / {FileSizeFormatter.FormatCompact(snapshot.Total)}"
            : FileSizeFormatter.FormatCompact(snapshot.Transferred);
        SpeedText = FileSizeFormatter.FormatSpeed(snapshot.BytesPerSecond);
    }

    private void Complete(TransferOutcome outcome)
    {
        IsBusy = false;
        IsCompleted = true;
        IsIndeterminate = false;
        Percentage = 100;
        HasError = false;
        ErrorText = "";
        Link = outcome.Link ?? "";
        PercentText = "";
        DetailText = "";
        SpeedText = "";

        var isUpload = outcome.LocalPath is null;
        if (outcome.LocalPath is { } path)
        {
            _localPath = path;
            HasLocalFile = true;
            TargetText = path;
        }

        // 自动复制只对上传有意义（产物是访问链接）；下载的产物是本地文件
        if (isUpload && HasLink && _settings.AutoCopyLink)
        {
            _host.CopyText(Link);
            StatusText = T("transfer_status_copied");
            return;
        }

        StatusText = T(isUpload ? "transfer_status_uploaded_done" : "transfer_status_downloaded_done");
    }

    private void FinishCancelled()
    {
        IsBusy = false;
        IsConflictVisible = false;
        IsCancelPending = false;
        IsCancelConfirmed = false;
        // 停掉不确定态的旋转，否则窗口在关闭前还会空转
        IsIndeterminate = false;
        PercentText = "";
        DetailText = "";
        SpeedText = "";
        StatusText = T("transfer_status_cancelled");
    }

    private void Fail(Exception ex)
    {
        IsBusy = false;
        IsConflictVisible = false;
        IsCancelPending = false;
        IsCancelConfirmed = false;
        HasError = true;
        ErrorText = ex.Message;
        PercentText = "";
        DetailText = "";
        SpeedText = "";

        // 停止不确定态旋转；百分比沿用失败那一刻的实际进度（画满会让人误以为已传完）
        IsIndeterminate = false;
        StatusText = T("transfer_status_failed");
    }

    private void ResolveConflict(UploadConflictChoice choice)
    {
        IsConflictVisible = false;
        _conflict?.TrySetResult(choice);
        _conflict = null;
    }

    /// <summary>切换文件时清空上一文件的进度显示，避免速率/剩余时间里混入旧样本。</summary>
    private void ResetProgress()
    {
        _rate.Reset();
        _lastUiUpdate = DateTimeOffset.MinValue;
        Percentage = 0;
        IsIndeterminate = true;
        PercentText = "";
        DetailText = "";
        SpeedText = "";
    }
}
