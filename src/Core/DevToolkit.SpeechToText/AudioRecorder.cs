using System.IO;

using NAudio.Wave;

namespace DevToolkit.SpeechToText;

/// <summary>
/// 麦克风录音器，支持录制到文件和实时音频块事件。
/// </summary>
public sealed class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentFilePath;
    private readonly object _lock = new();
    private readonly int _sampleRate;
    private readonly int _chunkIntervalMs;
    private DateTime _startTime;

    /// <summary>录音状态变化。</summary>
    public event Action<bool>? RecordingStateChanged;

    /// <summary>录音时长更新（每 200ms）。</summary>
    public event Action<TimeSpan>? DurationUpdated;

    /// <summary>音频块就绪，可用于流式识别。参数：(pcmData, bytesRecorded, format)。</summary>
    public event Action<byte[], int, WaveFormat>? AudioChunkReady;

    public bool IsRecording { get; private set; }

    /// <summary>录音文件存放目录。录音文件名按时间戳生成。</summary>
    public string? RecordingsDirectory { get; set; }

    /// <summary>最近一次录音的文件路径。</summary>
    public string? LastRecordedFilePath { get; private set; }

    /// <param name="sampleRate">采样率，默认 16000</param>
    /// <param name="chunkIntervalMs">音频块间隔（毫秒），默认 500</param>
    public AudioRecorder(int sampleRate = 16000, int chunkIntervalMs = 500)
    {
        _sampleRate = sampleRate;
        _chunkIntervalMs = chunkIntervalMs;
    }

    /// <summary>开始录音。</summary>
    public void StartRecording()
    {
        lock (_lock)
        {
            if (IsRecording) return;

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(_sampleRate, 16, 1),
                BufferMilliseconds = _chunkIntervalMs,
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            if (RecordingsDirectory is not null)
            {
                Directory.CreateDirectory(RecordingsDirectory);
                _currentFilePath = Path.Combine(RecordingsDirectory, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);
            }

            _startTime = DateTime.Now;
            _waveIn.StartRecording();
            IsRecording = true;
            RecordingStateChanged?.Invoke(true);

            // 启动时长更新定时器
            _ = Task.Run(async () =>
            {
                while (IsRecording)
                {
                    DurationUpdated?.Invoke(DateTime.Now - _startTime);
                    await Task.Delay(200);
                }
            });
        }
    }

    /// <summary>停止录音。</summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            if (!IsRecording) return;
            IsRecording = false;
            _waveIn?.StopRecording();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0) return;

        lock (_lock)
        {
            if (_writer is not null)
            {
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
                _writer.Flush();
            }
        }

        // 复制数据以避免缓冲区重用
        var chunk = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, chunk, 0, e.BytesRecorded);
        AudioChunkReady?.Invoke(chunk, e.BytesRecorded, _waveIn!.WaveFormat);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;

            LastRecordedFilePath = _currentFilePath;

            if (_waveIn is not null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }
        }

        RecordingStateChanged?.Invoke(false);

        if (e.Exception is not null)
            System.Diagnostics.Debug.WriteLine($"录音异常: {e.Exception.Message}");
    }

    /// <summary>将 PCM 数据编码为 WAV 格式字节数组。</summary>
    public static byte[] EncodePcmToWav(byte[] pcmData, WaveFormat format)
    {
        using var ms = new MemoryStream();
        using var writer = new WaveFileWriter(ms, format);
        writer.Write(pcmData, 0, pcmData.Length);
        writer.Flush();
        return ms.ToArray();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (IsRecording)
            {
                IsRecording = false;
                _waveIn?.StopRecording();
            }
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;
        }
    }
}
