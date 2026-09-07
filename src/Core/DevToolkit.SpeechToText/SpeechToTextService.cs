using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DevToolkit.SpeechToText;

/// <summary>
/// 语音转文字服务，调用 mimo-v2.5-asr（/v1/chat/completions）。
/// 音频以 base64 内嵌在 messages 中，支持流式 SSE 和非流式调用。
/// </summary>
public class SpeechToTextService
{
    private readonly Settings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<SpeechToTextService> _logger;

    public SpeechToTextService(Settings settings, HttpClient? httpClient = null, ILogger<SpeechToTextService>? logger = null)
    {
        _settings = settings;
        _http = httpClient ?? new HttpClient();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SpeechToTextService>.Instance;
    }

    /// <summary>非流式转写：对完整音频文件调用 API，返回识别文本。</summary>
    public async Task<string> TranscribeFileAsync(string audioFilePath, CancellationToken ct = default)
    {
        if (!_settings.IsValid())
            throw new InvalidOperationException("请先配置 API 设置");

        var fileSize = new FileInfo(audioFilePath).Length;
        _logger.LogInformation("转写文件: {Path}, 大小: {Size} bytes", audioFilePath, fileSize);

        var audioBytes = await File.ReadAllBytesAsync(audioFilePath, ct);
        var mimeType = GetMimeType(audioFilePath);
        var base64 = Convert.ToBase64String(audioBytes);

        return await SendAsrRequestAsync(base64, mimeType, stream: false, onPartial: null, ct);
    }

    /// <summary>非流式转写：对音频字节流调用 API。</summary>
    public async Task<string> TranscribeBytesAsync(byte[] audioData, string fileName, CancellationToken ct = default)
    {
        if (!_settings.IsValid())
            throw new InvalidOperationException("请先配置 API 设置");

        _logger.LogInformation("转写字节流: {FileName}, 大小: {Size} bytes", fileName, audioData.Length);
        var mimeType = GetMimeType(fileName);
        var base64 = Convert.ToBase64String(audioData);

        return await SendAsrRequestAsync(base64, mimeType, stream: false, onPartial: null, ct);
    }

    /// <summary>流式转写：发送音频数据，通过回调实时返回部分转写结果。</summary>
    public async Task<string> TranscribeStreamAsync(byte[] wavData, Action<string>? onPartial = null, CancellationToken ct = default)
    {
        if (!_settings.IsValid())
            return "";

        _logger.LogInformation("流式转写: 大小 {Size} bytes", wavData.Length);
        var base64 = Convert.ToBase64String(wavData);

        return await SendAsrRequestAsync(base64, "audio/wav", stream: true, onPartial, ct);
    }

    /// <summary>发送 ASR 请求的核心方法。</summary>
    private async Task<string> SendAsrRequestAsync(string base64Audio, string mimeType, bool stream, Action<string>? onPartial, CancellationToken ct)
    {
        var url = NormalizeUrl(_settings.ApiUrl, "chat/completions");

        var body = new
        {
            model = _settings.ModelName,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_audio",
                            input_audio = new
                            {
                                data = $"data:{mimeType};base64,{base64Audio}"
                            }
                        }
                    }
                }
            },
            asr_options = new { language = _settings.Language },
            stream
        };

        var json = JsonSerializer.Serialize(body);
        _logger.LogDebug("请求 URL: {Url}, Body 大小: {Size}", url, json.Length);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _settings.ApiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request,
                stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("ASR API 返回错误: {StatusCode}, Body: {Body}", response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

            if (stream)
                return await ReadSseStreamAsync(response, onPartial, ct);
            else
                return await ReadJsonResponseAsync(response, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "ASR 请求失败: {Url}", url);
            throw;
        }
    }

    /// <summary>读取 SSE 流，逐块解析 delta.content 并回调。</summary>
    private async Task<string> ReadSseStreamAsync(HttpResponseMessage response, Action<string>? onPartial, CancellationToken ct)
    {
        var fullText = new StringBuilder();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            // SSE 格式: "data: {...}" 或 "data: [DONE]"
            if (!line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var contentEl))
                    {
                        var text = contentEl.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            fullText.Append(text);
                            onPartial?.Invoke(text);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                _logger.LogDebug("跳过无法解析的 SSE 行: {Line}", data);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogDebug("SSE 行缺少预期字段: {Line}", data);
            }
        }

        var result = fullText.ToString();
        _logger.LogInformation("流式转写完成: {Length} 字符", result.Length);
        return result;
    }

    /// <summary>读取非流式 JSON 响应。</summary>
    private async Task<string> ReadJsonResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("content", out var contentEl))
                {
                    var content = contentEl.GetString() ?? "";
                    _logger.LogInformation("转写完成: {Length} 字符", content.Length);
                    return content;
                }
            }

            _logger.LogWarning("响应中未找到 choices[0].message.content，返回原始文本");
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析响应 JSON 失败，返回原始文本");
            return json;
        }
    }

    /// <summary>测试 API 连通性（端点可达即视为成功）。</summary>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_settings.IsValid())
            {
                _logger.LogWarning("测试连接: API 配置不完整");
                return false;
            }
            var url = NormalizeUrl(_settings.ApiUrl, "chat/completions");
            _logger.LogInformation("测试连接: {Url}", url);

            // 发一个最小请求，只要服务器有响应（包括 401/400）就说明连通
            var body = JsonSerializer.Serialize(new
            {
                model = _settings.ModelName,
                messages = Array.Empty<object>(),
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _settings.ApiKey);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, ct);

            // 任何 HTTP 响应（包括 4xx/5xx）都说明服务器可达
            var reachable = response.IsSuccessStatusCode
                || (int)response.StatusCode >= 400; // 400/401/403 等都说明端点存在
            _logger.LogInformation("测试连接结果: {StatusCode}, 可达: {Reachable}", response.StatusCode, reachable);
            return reachable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试连接失败");
            return false;
        }
    }

    private static string NormalizeUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        return $"{b}/{path}";
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".webm" => "audio/webm",
            _ => "audio/wav",
        };
    }

    /// <summary>设置 API 请求头中的 api-key。</summary>
    public void ApplyAuthHeaders()
    {
        if (_http.DefaultRequestHeaders.Contains("api-key"))
            _http.DefaultRequestHeaders.Remove("api-key");
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            _http.DefaultRequestHeaders.Add("api-key", _settings.ApiKey);
    }
}
