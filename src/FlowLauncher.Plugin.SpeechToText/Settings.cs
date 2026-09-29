using FlowLauncher.Plugin.SpeechToText.Utils;

namespace FlowLauncher.Plugin.SpeechToText;

public class Settings
{
    public string ModelName { get; set; } = "mimo-v2.5-asr";
    public string ApiUrl { get; set; } = "https://api.xiaomimimo.com/v1/";

    /// <summary>磁盘形态：DPAPI 密文（<c>dpapi:</c> 前缀）或旧版明文。</summary>
    public string ApiKey { get; set; } = "";
    public string Language { get; set; } = "auto";
    public int SampleRate { get; set; } = 16000;

    /// <summary>
    /// 录音过程中实时识别：每隔 RealtimeIntervalMs 把已录音频送去识别并刷新界面。
    /// 注意 ASR 接口为一次性提交音频（chat/completions + base64），不支持流式上传，
    /// 因此"实时"通过定时分段识别实现；停止后仍会用完整音频做最终转写以保证结果准确。
    /// </summary>
    public bool EnableRealtimeTranscription { get; set; } = true;

    /// <summary>实时识别间隔（毫秒），仅在 EnableRealtimeTranscription 为 true 时生效。</summary>
    public int RealtimeIntervalMs { get; set; } = 3000;

    // 气泡窗口位置（WPF 单位，与屏幕坐标一致）；null 表示尚未记录，回退到默认定位
    public double? BubbleLeft { get; set; }
    public double? BubbleTop { get; set; }

    /// <summary>业务侧使用的明文 API Key。</summary>
    public string GetPlainApiKey() => SecretProtector.Unprotect(ApiKey);

    /// <summary>写入 API Key；传 null 表示不修改已存值。</summary>
    public void SetApiKey(string? apiKey)
    {
        if (apiKey is not null)
            ApiKey = SecretProtector.Protect(apiKey);
    }

    /// <summary>旧版明文升级为 DPAPI 密文；返回是否变更。</summary>
    public bool TryUpgradeSecrets()
    {
        if (string.IsNullOrEmpty(ApiKey) || SecretProtector.IsProtected(ApiKey))
            return false;
        ApiKey = SecretProtector.Protect(ApiKey);
        return true;
    }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ModelName) &&
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(GetPlainApiKey());
}
