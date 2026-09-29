using FlowLauncher.Plugin.AliyunOss.Transfer;

using Flow.Launcher.Plugin;

namespace FlowLauncher.Plugin.AliyunOss.Views;

/// <summary>
/// 经 Flow Launcher 的 GetTranslation 读取插件语言目录（<c>Languages/&lt;语言码&gt;.xaml</c>）文案。
/// Flow 启动时必定加载插件目录下的 en.xaml 作为默认语言，因此缺失 key 只可能是漏写，
/// 此时返回 key 自身作为可见标记，便于排查也避免把 Flow 的提示串显示到界面上。
/// </summary>
internal sealed class FlowLocalization(IPublicAPI publicApi) : ILocalization
{
    /// <summary>Flow 未命中 key 时返回文本的前缀，见 Internationalization.GetTranslation。</summary>
    private const string MissingKeyPrefix = "No Translation for key";

    public string Get(string key, params object[] args)
    {
        var text = publicApi.GetTranslation(key);

        // 未命中时返回的是 "No Translation for key <key>" 而不是 key 本身，
        // 只比对 key 会漏判，把这段提示原样显示到界面上。
        if (string.IsNullOrEmpty(text) || text.StartsWith(MissingKeyPrefix, StringComparison.Ordinal))
        {
            text = key;
        }

        return args.Length > 0 ? string.Format(text, args) : text;
    }
}
