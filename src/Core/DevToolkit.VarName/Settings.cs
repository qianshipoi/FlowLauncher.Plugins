using DevToolkit.VarName.Utils;

namespace DevToolkit.VarName;

public class Settings
{
    public string ModelName { get; set; } = "gpt-3.5-turbo";
    public string ApiUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>磁盘形态：DPAPI 密文（<c>dpapi:</c> 前缀）或旧版明文。</summary>
    public string ApiKey { get; set; } = "";

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

    public bool IsValid()
    {
        return
            !string.IsNullOrWhiteSpace(ModelName) &&
            !string.IsNullOrWhiteSpace(ApiUrl) &&
            !string.IsNullOrWhiteSpace(GetPlainApiKey());
    }
}
