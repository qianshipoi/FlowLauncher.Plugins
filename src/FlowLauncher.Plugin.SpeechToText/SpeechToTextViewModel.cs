using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace FlowLauncher.Plugin.SpeechToText;

/// <summary>
/// 语音转文字共享 ViewModel，供插件气泡窗口和 App 页面共同使用。
/// 管理录音状态、流式转写和结果。
/// </summary>
public partial class SpeechToTextViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly AudioRecorder _recorder;
    private readonly SpeechToTextService _service;
    private readonly TranscriptionHistory? _history;
    private readonly string? _recordingsDir;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly List<byte> _pcmAccumulator = [];
    private readonly Lock _bufferLock = new();
    private WaveFormat? _recordFormat;
    private DateTime _recordStartTime;

    private CancellationTokenSource? _realtimeCts;

    /// <summary>实时识别请求是否进行中（1=进行中）。用原子操作避免并发请求堆积。</summary>
    private int _realtimeBusy;

    /// <summary>上次实时识别时的 PCM 字节数，用于跳过无新增音频的轮次。</summary>
    private int _lastRealtimeLength;

    /// <summary>实时识别的音频长度上限（秒）。超过后停止实时识别，避免请求体无限增大。</summary>
    private const int MaxRealtimeAudioSeconds = 120;

    /// <summary>录音停止后触发。</summary>
    public event Action? RecordingStopped;

    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _statusText = "准备就绪";
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private bool _hasResult;

    /// <summary>历史记录列表（App 页面绑定用）。</summary>
    public ObservableCollection<TranscriptionRecord> History { get; } = [];

    /// <param name="settings">API 配置</param>
    /// <param name="dataDirectory">数据目录（历史记录 + 录音文件），传 null 则不保存</param>
    /// <param name="logger">日志记录器</param>
    public SpeechToTextViewModel(Settings settings, string? dataDirectory = null, ILogger? logger = null)
    {
        _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _httpClient = new HttpClient();
        _service = new SpeechToTextService(settings, _httpClient,
            logger is ILogger<SpeechToTextService> svcLogger ? svcLogger : null);
        _service.ApplyAuthHeaders();

        if (dataDirectory is not null)
        {
            _history = new TranscriptionHistory(dataDirectory);
            _recordingsDir = Path.Combine(dataDirectory, "recordings");
        }

        _recorder = new AudioRecorder(settings.SampleRate, 500);
        _recorder.RecordingStateChanged += OnRecordingStateChanged;
        _recorder.DurationUpdated += OnDurationUpdated;
        _recorder.AudioChunkReady += OnAudioChunkReady;
        _recorder.RecordingsDirectory = _recordingsDir;

        // 加载历史记录
        if (_history is not null)
        {
            foreach (var record in _history.GetRecent(100))
                History.Add(record);
        }
    }

    [RelayCommand]
    public void Start()
    {
        if (IsRecording) return;

        if (!_settings.IsValid())
        {
            _logger.LogWarning("无法开始录音: API 配置不完整");
            StatusText = "请先配置 API 设置";
            return;
        }

        lock (_bufferLock)
        {
            _pcmAccumulator.Clear();
            _recordFormat = null;
        }

        ResultText = "";
        HasResult = false;
        StatusText = "正在录音…";
        _recordStartTime = DateTime.Now;
        _logger.LogInformation("开始录音: Model={Model}, SampleRate={Rate}", _settings.ModelName, _settings.SampleRate);
        _recorder.StartRecording();

        // 启动实时识别循环：录音过程中定时把已录音频送去识别，边录边出结果
        _lastRealtimeLength = 0;
        _realtimeCts?.Dispose();
        _realtimeCts = new CancellationTokenSource();
        if (_settings.EnableRealtimeTranscription)
        {
            _ = RealtimeLoopAsync(_realtimeCts.Token);
        }
    }

    [RelayCommand]
    public void Stop()
    {
        if (!IsRecording) return;
        _recorder.StopRecording();
    }

    public void Toggle()
    {
        if (IsRecording) Stop();
        else Start();
    }

    private void OnRecordingStateChanged(bool recording)
    {
        IsRecording = recording;
        if (!recording)
        {
            // 停止实时识别循环，后续由最终转写（完整音频）接管结果
            _realtimeCts?.Cancel();
            StatusText = "正在转写…";
            _ = FinalizeTranscriptionAsync();
        }
    }

    private void OnDurationUpdated(TimeSpan duration)
    {
        DurationText = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    /// <summary>累积全部 PCM 数据，录音结束后一次性发送流式请求。</summary>
    private void OnAudioChunkReady(byte[] pcmData, int bytesRecorded, WaveFormat format)
    {
        lock (_bufferLock)
        {
            _pcmAccumulator.AddRange(pcmData.AsSpan(0, bytesRecorded));
            _recordFormat ??= format;
        }
    }

    /// <summary>录音期间的实时识别循环：每隔设定间隔对已录音频做一次识别并刷新界面。</summary>
    private async Task RealtimeLoopAsync(CancellationToken ct)
    {
        var interval = Math.Max(500, _settings.RealtimeIntervalMs);
        _logger.LogInformation("实时识别已启动，间隔 {Interval}ms", interval);

        try
        {
            // 先等一个间隔，避免刚开录就发送极短音频
            await Task.Delay(interval, ct);

            while (!ct.IsCancellationRequested && IsRecording)
            {
                await TryRealtimeTranscribeAsync(ct);
                await Task.Delay(interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 录音停止，正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "实时识别循环异常");
        }
    }

    /// <summary>
    /// 对当前已累积的音频做一次识别。每次提交的是从开头到现在的完整音频，
    /// 因此返回的是完整文本，直接覆盖显示即可（表现为文字持续增长）。
    /// </summary>
    private async Task TryRealtimeTranscribeAsync(CancellationToken ct)
    {
        // 上一次请求尚未结束则跳过本轮，避免慢请求堆积
        if (Interlocked.CompareExchange(ref _realtimeBusy, 1, 0) != 0)
        {
            _logger.LogDebug("上一次实时识别尚未完成，跳过本轮");
            return;
        }

        try
        {
            byte[] pcmData;
            WaveFormat format;

            lock (_bufferLock)
            {
                // 无新增音频时跳过，避免对同一段音频重复请求
                if (_pcmAccumulator.Count == 0 || _pcmAccumulator.Count == _lastRealtimeLength) return;

                pcmData = [.. _pcmAccumulator];
                format = _recordFormat ?? new WaveFormat(_settings.SampleRate, 16, 1);
                _lastRealtimeLength = _pcmAccumulator.Count;
            }

            var bytesPerSecond = format.SampleRate * format.Channels * (format.BitsPerSample / 8);

            // 音频过短（不足 0.3 秒）时识别无意义，跳过
            if (pcmData.Length < bytesPerSecond * 0.3)
                return;

            // 音频过长时暂停实时识别，避免请求体无限增大；最终结果仍会在停止后完整转写
            if (pcmData.Length > bytesPerSecond * MaxRealtimeAudioSeconds)
            {
                StatusText = "录音较长，实时识别已暂停";
                _logger.LogInformation("录音超过 {Max} 秒，实时识别暂停", MaxRealtimeAudioSeconds);
                return;
            }

            var wavData = AudioRecorder.EncodePcmToWav(pcmData, format);
            StatusText = "正在识别…";
            _logger.LogDebug("实时识别请求: {Size} bytes WAV", wavData.Length);

            var text = await _service.TranscribeBytesAsync(wavData, "realtime.wav", ct);

            // 录音已停止（最终转写已开始）时不再覆盖，避免与最终结果竞争
            if (ct.IsCancellationRequested || !IsRecording) return;

            if (!string.IsNullOrWhiteSpace(text))
            {
                ResultText = text;
                HasResult = true;
                _logger.LogInformation("实时识别结果: {Length} 字符", text.Length);
            }

            StatusText = "正在录音…";
        }
        catch (OperationCanceledException)
        {
            // 录音停止导致的取消，忽略
        }
        catch (Exception ex)
        {
            // 实时识别失败不影响录音继续，最终结果仍会在停止后完整转写
            _logger.LogWarning(ex, "实时识别失败，将在停止后完整转写");
            if (!ct.IsCancellationRequested && IsRecording)
                StatusText = "实时识别失败，继续录音…";
        }
        finally
        {
            Interlocked.Exchange(ref _realtimeBusy, 0);
        }
    }

    /// <summary>等待进行中的实时识别请求结束，避免与最终转写并发冲突。</summary>
    private async Task WaitForRealtimeIdleAsync()
    {
        for (var i = 0; i < 100 && Interlocked.CompareExchange(ref _realtimeBusy, 0, 0) == 1; i++)
        {
            await Task.Delay(100);
        }
    }

    /// <summary>录音停止后，用完整音频发送流式 ASR 请求，实时更新结果。</summary>
    private async Task FinalizeTranscriptionAsync()
    {
        try
        {
            // 等待进行中的实时识别结束，避免并发请求与结果覆盖冲突
            await WaitForRealtimeIdleAsync();

            byte[] pcmData;
            WaveFormat format;

            lock (_bufferLock)
            {
                pcmData = [.. _pcmAccumulator];
                format = _recordFormat ?? new WaveFormat(_settings.SampleRate, 16, 1);
                _pcmAccumulator.Clear();
            }

            if (pcmData.Length == 0)
            {
                _logger.LogWarning("录音数据为空");
                StatusText = "未录制到音频";
                RecordingStopped?.Invoke();
                return;
            }

            _logger.LogInformation("开始流式转写: {Size} bytes PCM", pcmData.Length);
            var wavData = AudioRecorder.EncodePcmToWav(pcmData, format);

            // 保存录音文件
            if (_recordingsDir is not null)
            {
                Directory.CreateDirectory(_recordingsDir);
                var filePath = Path.Combine(_recordingsDir, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                await File.WriteAllBytesAsync(filePath, wavData);
                _logger.LogInformation("录音已保存: {Path}", filePath);
            }

            // 流式转写：SSE 回调实时更新 UI
            ResultText = "";
            HasResult = false;

            var finalText = await _service.TranscribeStreamAsync(wavData, partial =>
            {
                ResultText += partial;
                HasResult = true;
            });

            if (string.IsNullOrWhiteSpace(ResultText) && !string.IsNullOrWhiteSpace(finalText))
                ResultText = finalText;

            HasResult = !string.IsNullOrWhiteSpace(ResultText);
            StatusText = HasResult ? "转写完成" : "未识别到语音";
            _logger.LogInformation("转写完成: {Length} 字符", ResultText.Length);

            // 保存历史
            if (_history is not null)
            {
                var duration = DateTime.Now - _recordStartTime;
                var record = new TranscriptionRecord
                {
                    Text = ResultText,
                    Duration = duration,
                    Model = _settings.ModelName,
                };
                _history.Add(record);
                History.Insert(0, record);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转写失败");
            StatusText = $"转写失败：{ex.Message}";
        }
        finally
        {
            RecordingStopped?.Invoke();
        }
    }

    public void Dispose()
    {
        _realtimeCts?.Cancel();
        _realtimeCts?.Dispose();
        _realtimeCts = null;
        _recorder.Dispose();
        _httpClient.Dispose();
    }
}
