using DevToolkit.AliyunOss.Transfer;

using Flow.Launcher.Plugin;

namespace FlowLauncher.Plugin.AliyunOss.Views;

/// <summary>
/// 经 Flow Launcher 的 GetTranslation 读取插件语言目录（plugin.json 的 I18nPath）文案。
/// Flow 按宿主当前语言自动选择对应文件；缺失 key 时返回 key 本身以便排查。
/// </summary>
internal sealed class FlowLocalization(IPublicAPI publicApi) : ILocalization
{
    public string Get(string key, params object[] args)
    {
        var text = publicApi.GetTranslation(key);

        if (string.IsNullOrEmpty(text) || text == key)
        {
            // 宿主语言未覆盖该 key（如 Flow 语言无此插件翻译）时回退英文
            text = EnglishFallback.TryGetValue(key, out var fallback) ? fallback : key;
        }

        return args.Length > 0 ? string.Format(text, args) : text;
    }

    /// <summary>英文兜底文案，与 Languages/en.json 保持一致（仅取会用到的 key）。</summary>
    private static readonly Dictionary<string, string> EnglishFallback = new()
    {
        ["transfer_title_upload"] = "Upload",
        ["transfer_title_upload_temp"] = "Upload Temp File",
        ["transfer_title_download"] = "Download",
        ["transfer_title_upload_many"] = "Upload {0} files",
        ["transfer_select_file"] = "Select file to upload",
        ["transfer_filter_all"] = "All files|*.*",
        ["transfer_select_save_location"] = "Choose save location",
        ["transfer_status_preparing"] = "Preparing",
        ["transfer_status_uploading"] = "Uploading",
        ["transfer_status_uploading_item"] = "Uploading {0}",
        ["transfer_status_downloading"] = "Downloading",
        ["transfer_status_backup"] = "Backing up original file…",
        ["transfer_status_checking"] = "Checking existing object…",
        ["transfer_status_signing"] = "Generating link…",
        ["transfer_status_uploaded"] = "Uploaded {0}",
        ["transfer_status_partial"] = "Done {0}/{1}",
        ["transfer_status_uploaded_done"] = "Upload finished",
        ["transfer_status_downloaded_done"] = "Download finished",
        ["transfer_status_copied"] = "Link copied",
        ["transfer_status_cancelled"] = "Cancelled",
        ["transfer_status_failed"] = "Failed",
        ["transfer_cancel_confirm"] = "Click again to confirm cancel",
        ["transfer_conflict_exists"] = "{0} already exists. Choose an action:",
    };
}
