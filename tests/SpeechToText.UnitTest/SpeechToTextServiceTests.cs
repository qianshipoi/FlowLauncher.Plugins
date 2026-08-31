using System.Text;
using DevToolkit.SpeechToText;

namespace SpeechToText.UnitTest;

public class SpeechToTextServiceTests
{
    private static Settings CreateSettings()
    {
        var apiKey = Environment.GetEnvironmentVariable("MIMO_API_KEY", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("请设置环境变量 MIMO_API_KEY 后再运行集成测试");

        return new Settings
        {
            ModelName = "mimo-v2.5-asr",
            ApiUrl = "https://api.xiaomimimo.com/v1/",
            ApiKey = apiKey,
            Language = "auto",
        };
    }

    private static string GetTestAudioPath()
    {
        // 从输出目录的 TestData 子目录查找
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "core-capability-1.wav");
        if (File.Exists(path)) return path;

        // 回退到仓库根目录
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        path = Path.Combine(root, "core-capability-1.wav");
        if (File.Exists(path)) return path;

        throw new FileNotFoundException("找不到测试音频文件 core-capability-1.wav");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TranscribeFileAsync_真实API_返回非空文本()
    {
        var settings = CreateSettings();
        using var http = new HttpClient();
        var service = new SpeechToTextService(settings, http);
        service.ApplyAuthHeaders();

        var audioPath = GetTestAudioPath();
        var result = await service.TranscribeFileAsync(audioPath);

        Assert.False(string.IsNullOrWhiteSpace(result), $"转写结果为空，原始返回: {result}");
        // 输出到测试日志以便人工确认
        Assert.True(result.Length > 0, $"识别结果: {result}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TranscribeStreamAsync_真实API_逐块回调并返回完整文本()
    {
        var settings = CreateSettings();
        using var http = new HttpClient();
        var service = new SpeechToTextService(settings, http);
        service.ApplyAuthHeaders();

        var audioPath = GetTestAudioPath();
        var wavBytes = await File.ReadAllBytesAsync(audioPath);

        var partials = new List<string>();
        var result = await service.TranscribeStreamAsync(wavBytes, partial =>
        {
            lock (partials) partials.Add(partial);
        });

        Assert.False(string.IsNullOrWhiteSpace(result), "流式转写结果为空");
        Assert.True(partials.Count > 0, "未收到任何流式回调");

        // 拼接的 partial 应与最终结果一致
        string joined;
        lock (partials) joined = string.Concat(partials);
        Assert.Equal(result, joined);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TranscribeBytesAsync_真实API_返回非空文本()
    {
        var settings = CreateSettings();
        using var http = new HttpClient();
        var service = new SpeechToTextService(settings, http);
        service.ApplyAuthHeaders();

        var audioPath = GetTestAudioPath();
        var wavBytes = await File.ReadAllBytesAsync(audioPath);

        var result = await service.TranscribeBytesAsync(wavBytes, "test.wav");

        Assert.False(string.IsNullOrWhiteSpace(result), "字节流转写结果为空");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestConnectionAsync_真实API_返回True()
    {
        var settings = CreateSettings();
        using var http = new HttpClient();
        var service = new SpeechToTextService(settings, http);
        service.ApplyAuthHeaders();

        var ok = await service.TestConnectionAsync();

        Assert.True(ok, "API 连通性测试失败");
    }

    [Fact]
    public async Task SendAsrRequestAsync_配置无效_抛出异常()
    {
        var settings = new Settings { ApiKey = "", ApiUrl = "", ModelName = "" };
        var service = new SpeechToTextService(settings);

        // TranscribeFileAsync 检查 IsValid 后抛出
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeBytesAsync([0x00], "test.wav"));
    }

    [Fact]
    public void GetMimeType_各种扩展名_返回正确MIME()
    {
        // 通过反射测试私有静态方法
        var method = typeof(SpeechToTextService).GetMethod("GetMimeType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.Equal("audio/wav", method!.Invoke(null, ["test.wav"]));
        Assert.Equal("audio/mpeg", method.Invoke(null, ["test.mp3"]));
        Assert.Equal("audio/ogg", method.Invoke(null, ["test.ogg"]));
        Assert.Equal("audio/flac", method.Invoke(null, ["test.flac"]));
        Assert.Equal("audio/wav", method.Invoke(null, ["test.xyz"]));
    }

    [Fact]
    public void NormalizeUrl_尾部斜杠_正确拼接()
    {
        var method = typeof(SpeechToTextService).GetMethod("NormalizeUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.Equal("https://api.example.com/v1/chat/completions",
            method!.Invoke(null, ["https://api.example.com/v1/", "chat/completions"]));
        Assert.Equal("https://api.example.com/v1/chat/completions",
            method.Invoke(null, ["https://api.example.com/v1", "chat/completions"]));
    }

    [Fact]
    public async Task ReadSseStreamAsync_模拟SSE_正确解析增量文本()
    {
        // 构造模拟 SSE 响应
        var sseData = """
            data: {"choices":[{"delta":{"content":"你"}}]}

            data: {"choices":[{"delta":{"content":"好"}}]}

            data: {"choices":[{"delta":{"content":"世界"}}]}

            data: [DONE]

            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sseData));
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StreamContent(ms),
        };

        var service = new SpeechToTextService(new Settings());
        var partials = new List<string>();

        // 通过反射调用私有方法
        var method = typeof(SpeechToTextService).GetMethod("ReadSseStreamAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<string>)method!.Invoke(service,
            [response, (Action<string>)(p => { lock (partials) partials.Add(p); }), CancellationToken.None])!;
        var result = await task;

        Assert.Equal("你好世界", result);
        Assert.Equal(3, partials.Count);
        Assert.Equal("你", partials[0]);
        Assert.Equal("好", partials[1]);
        Assert.Equal("世界", partials[2]);
    }

    [Fact]
    public async Task ReadJsonResponseAsync_标准响应_提取content字段()
    {
        var json = """
            {"choices":[{"message":{"content":"测试结果"}}]}
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StreamContent(ms),
        };

        var service = new SpeechToTextService(new Settings());

        var method = typeof(SpeechToTextService).GetMethod("ReadJsonResponseAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<string>)method!.Invoke(service, [response, CancellationToken.None])!;
        var result = await task;

        Assert.Equal("测试结果", result);
    }
}
