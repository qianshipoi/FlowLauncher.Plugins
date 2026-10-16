using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.S3.Models;
using FlowLauncher.Plugin.S3.Transfer;
using FlowLauncher.Plugin.S3.Utils;

using System.IO;
using System.Windows.Threading;

namespace FlowLauncher.Plugin.S3.ViewModels;

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
    private readonly ThrottledTransferProgress _progress;

    private CancellationTokenSource? _cts;
    private TaskCompletionSource<UploadConflictChoice>? _conflict;

    public TransferViewModel(TransferEngine engine, Settings settings, ITransferHost host, ILocalization loc)
    {
        _engine = engine;
        _settings = settings;
        _host = host;
        _loc = loc;
        // 在 UI 线程构造，Progress 会把回调投递回 UI 线程。
        // 投递前先按 UiUpdateInterval 过滤：SDK 按数据块回调（128MB 就有千余次），
        // 全部 Post 到 UI 线程会挤满消息队列，大文件下气泡会明显卡顿。
        _progress = new ThrottledTransferProgress(
            new Progress<TransferProgress>(OnProgress),
            UiUpdateInterval);
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
    [NotifyPropertyChangedFor(nameof(IsDropIdle))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    [NotifyPropertyChangedFor(nameof(IsDropIdle))]
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
    /// 关闭已被明确表达（关闭按钮，或确认态下再次 Alt+F4）：
    /// <see cref="Views.TransferWindow"/> 的 OnClosing 据此放行，不再拦下询问。
    /// 与取消分开：常驻气泡里「取消」只中止本轮、并不关窗。
    /// </summary>
    [ObservableProperty]
    private bool _isCloseConfirmed;

    /// <summary>本地文件路径（下载完成后用于定位文件）。</summary>
    private string? _localPath;

    // ---- 常驻拖拽气泡（s3u 打开，拖入文件即上传）----

    private string _dropBucket = "";
    private string _dropKey = "";
    private bool _dropCheckExists;
    private bool _dropForceBackup;

    /// <summary>
    /// 是否常驻拖拽气泡：空闲时等待拖入而不自动关闭，
    /// 取消传输也只回到等待态（关闭由独立的关闭按钮负责）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDropIdle))]
    private bool _isDropZone;

    /// <summary>空闲等待拖入：常驻气泡既不忙、也未完成。</summary>
    public bool IsDropIdle => IsDropZone && !IsBusy && !IsCompleted;

    /// <summary>有文件正悬停在气泡上方（拖拽进入但未松手）。</summary>
    [ObservableProperty]
    private bool _isDragOver;

    /// <summary>
    /// 拖拽进出气泡时更新提示文案，告诉用户松手就能上传。
    /// 仅在空闲态生效：传输中或已有结果时不覆盖当前状态。
    /// </summary>
    public void SetDragOver(bool value)
    {
        if (!IsDropZone) return;

        IsDragOver = value;

        if (IsBusy || IsCompleted) return;
        StatusText = T(value ? "transfer_drop_release" : "transfer_drop_hint");
    }

    /// <summary>
    /// 配置为常驻拖拽气泡。<paramref name="key"/> 为空表示临时上传，
    /// 此时每个文件现场生成随机对象名，避免互相覆盖。
    /// </summary>
    public void ConfigureDropZone(string bucket, string key, bool checkExists, bool forceBackup)
    {
        _dropBucket = bucket;
        _dropKey = key;
        _dropCheckExists = checkExists;
        _dropForceBackup = forceBackup;

        // 等待拖入不是"传输中"：停掉不确定态旋转，呈静止圆环，免得看着像卡在 0%
        IsIndeterminate = false;
        IsDropZone = true;
        StatusText = T("transfer_drop_hint");
    }

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

    /// <summary>批量上传：逐个上传，进度按当前文件显示，结束后汇总。</summary>
    public Task RunUploadBatchAsync(IReadOnlyList<UploadRequest> requests)
    {
        var cts = Begin();
        return RunUploadsAsync(requests, cts, resolveConflict: false, forceBackup: false);
    }

    /// <summary>
    /// 常驻气泡的拖拽上传：把拖入的文件逐个传上去，结束后气泡保留并展示结果（可复制链接）。
    /// 传输中再次拖入会被忽略，避免并发写同一个气泡的进度。
    /// </summary>
    public Task RunDropUploadAsync(IReadOnlyList<string> files)
    {
        // 只在传输中忽略拖入；空闲或上一次已完成时都可以继续拖，气泡是常驻的上传入口
        if (!IsDropZone || files.Count == 0 || IsBusy) return Task.CompletedTask;

        var cts = Begin();
        var requests = files
            .Select(file => new UploadRequest(_dropBucket, ResolveDropKey(file), file))
            .ToList();

        return RunUploadsAsync(requests, cts, resolveConflict: _dropCheckExists || _dropForceBackup, _dropForceBackup);
    }

    /// <summary>
    /// 常驻气泡的目标对象名：用户指定了就每次都传到该路径（再次上传即覆盖），
    /// 没指定（临时上传）则每个文件现场生成随机路径，互不覆盖。
    /// </summary>
    private string ResolveDropKey(string filePath) => TransferEngine.ResolveKey(_dropKey, filePath);

    /// <summary>
    /// 逐个上传并在结尾汇总（单个文件失败不中断整批）。
    /// <paramref name="resolveConflict"/> 为真时每个文件上传前先处理同名对象：
    /// 常驻气泡可能被反复拖入同名文件，每次都要给出替换/备份的选择。
    /// </summary>
    private async Task RunUploadsAsync(
        IReadOnlyList<UploadRequest> requests,
        CancellationTokenSource cts,
        bool resolveConflict,
        bool forceBackup)
    {
        var total = requests.Count;
        var succeeded = 0;
        string? lastLink = null;
        string? failure = null;
        string? warning = null;

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
                    if (resolveConflict)
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
                    lastLink = outcome.Link;
                    warning ??= outcome.Warning;
                    succeeded++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (TransferCancellation.IsUserCancellation(ex, cts.IsCancellationRequested))
                {
                    // SDK 把取消包成 OperationException（内层 TaskCanceledException），
                    // 不拦下就会落进下面的"单文件失败"分支：取消会被记成失败，还会继续传下一个文件
                    throw new OperationCanceledException("Transfer cancelled by user.", ex, cts.Token);
                }
                catch (Exception ex)
                {
                    // 单个文件失败不中断整批，记录后在结尾汇总
                    failure = $"{Path.GetFileName(request.FilePath)}: {ex.Message}";
                    StatusText = $"{T("transfer_status_failed")} {label}: {ex.Message}";
                }
            }

            // 收尾前确认仍是当前这次运行：取消后立刻再拖一个文件时，
            // 旧任务的收尾不能盖掉新任务的进行中状态
            if (!IsCurrentRun(cts)) return;

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

            if (warning is not null)
            {
                ErrorText = warning;
                StatusText += "（元数据未写入）";
            }

            if (failure is not null)
            {
                HasError = true;
                ErrorText = failure;
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentRun(cts)) FinishCancelled();
        }
        catch (Exception ex) when (TransferCancellation.IsUserCancellation(ex, cts.IsCancellationRequested))
        {
            if (IsCurrentRun(cts)) FinishCancelled();
        }
        catch (Exception ex)
        {
            if (IsCurrentRun(cts)) Fail(ex);
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
            if (IsCurrentRun(cts)) Complete(outcome);
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentRun(cts)) FinishCancelled();
        }
        catch (Exception ex) when (TransferCancellation.IsUserCancellation(ex, cts.IsCancellationRequested))
        {
            if (IsCurrentRun(cts)) FinishCancelled();
        }
        catch (Exception ex)
        {
            if (IsCurrentRun(cts)) Fail(ex);
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

        if (IsBusy) StatusText = T("transfer_status_uploading");
    }

    /// <summary>若处于确认态则退出（点按钮外的分支调用）；不在确认态时无操作。</summary>
    public void CancelIfPending()
    {
        if (IsCancelPending) ExitCancelPending();
    }

    /// <summary>
    /// 确认取消（取消按钮的对勾）：中止本轮传输并回到可继续操作的状态。
    /// 常驻气泡只回到等待拖入（关闭交给独立按钮），普通窗口照旧关闭。
    /// </summary>
    public void ConfirmCancel() => ConfirmCancel(closeAfter: !IsDropZone);

    /// <summary>
    /// 确认取消并指定是否关闭窗口。
    /// Alt+F4 是对「关闭」的明确表达，走这条路时要真的关掉。
    /// </summary>
    public void ConfirmCancel(bool closeAfter)
    {
        _cancelPendingTimer?.Stop();
        IsCancelPending = false;

        Abort();
        FinishCancelled();

        IsCloseConfirmed = closeAfter;
        if (closeAfter) CloseRequested?.Invoke();
    }

    /// <summary>点击抬头在「百分比」与「已传/总字节」之间切换。</summary>
    [RelayCommand]
    private void ToggleHeadline() => ShowSizeInsteadOfPercent = !ShowSizeInsteadOfPercent;

    /// <summary>
    /// 关闭按钮：无条件关闭气泡（传输中先中止本轮，不留后台任务）。
    /// 常驻气泡里它与「取消」是两颗独立按钮：取消只中止本轮、气泡留下继续接拖拽。
    /// </summary>
    [RelayCommand]
    private void CloseWindow()
    {
        Abort();
        IsCloseConfirmed = true;
        CloseRequested?.Invoke();
    }

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
        IsCloseConfirmed = false;
        StatusText = T("transfer_status_preparing");
        return _cts;
    }

    private void OnProgress(TransferProgress progress)
    {
        _rate.Report(progress, DateTimeOffset.Now);

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
        // 停掉不确定态的旋转，否则窗口在关闭前还会空转
        IsIndeterminate = false;
        PercentText = "";
        DetailText = "";
        SpeedText = "";

        // 常驻气泡取消后回到等待拖入，让用户可以直接再拖一个文件重来
        StatusText = IsDropZone ? T("transfer_drop_hint") : T("transfer_status_cancelled");
    }

    private void Fail(Exception ex)
    {
        IsBusy = false;
        IsConflictVisible = false;
        IsCancelPending = false;
        HasError = true;
        ErrorText = ex.Message;
        PercentText = "";
        DetailText = "";
        SpeedText = "";

        // 停止不确定态旋转；百分比沿用失败那一刻的实际进度（画满会让人误以为已传完）
        IsIndeterminate = false;
        StatusText = T("transfer_status_failed");
    }

    /// <summary>
    /// 本次运行是否仍是当前那一次。常驻气泡允许"取消后立刻再拖一个文件"，
    /// 此时旧任务的收尾（取消/失败）若照常写状态，会覆盖掉新任务的进行中状态。
    /// </summary>
    private bool IsCurrentRun(CancellationTokenSource cts) => ReferenceEquals(cts, _cts);

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
        // 同时清掉投递节流窗口：否则新文件的首个样本会被上一文件的时间戳压掉，
        // 起步几百毫秒内界面停在 0%
        _progress.Reset();
        Percentage = 0;
        IsIndeterminate = true;
        PercentText = "";
        DetailText = "";
        SpeedText = "";
    }
}
