using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss;
using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.Transfer;
using FlowLauncher.Plugin.AliyunOss.Utils;
using FlowLauncher.Plugin.AliyunOss.Views;

using FlowLauncher.Plugin.AliyunOss.Views;

using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

/// <summary>
/// 支持上传，替换文件，替换时备份源文件
/// </summary>
internal class UploadQuery(IPublicAPI publicApi, OssService service, Settings settings)
{
    private const string ForwardSlash = "/";

    private readonly ITransferHost _host = new FlowTransferHost(publicApi);
    private readonly ILocalization _loc = new FlowLocalization(publicApi);

    private string T(string key, params object[] args) => _loc.Get(key, args);

    public List<Result> GetTempUploadResult(string bucketName)
    {
        return
        [
            ResultBuilder
                .Create(T("ossu_temp_upload"))
                .Subtitle(T("ossu_temp_upload_subtitle", bucketName))
                .Action(_ => ShowUpload(bucketName, ""))
                .Build()
        ];
    }

    public Task<List<Result>> ExecuteAsync(Query query, CancellationToken token = default)
    {
        if (query.SearchTerms.Length < 1 || string.IsNullOrWhiteSpace(query.SearchTerms[0]))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create(T("ossu_input_bucket")).Subtitle(T("ossu_usage")).Build()
            });
        }

        var bucketName = query.SearchTerms[0];

        if (query.SearchTerms.Length < 2 || string.IsNullOrWhiteSpace(query.SearchTerms[1]))
        {
            return Task.FromResult(GetTempUploadResult(bucketName));
        }

        var objectName = OssKey.Normalize(query.SearchTerms[1]);

        // 用户可能只输入斜杠（如 "ossu bucket /"），规范化后没有有效对象名
        if (objectName.Length == 0)
        {
            return Task.FromResult(GetTempUploadResult(bucketName));
        }

        if (objectName.EndsWith(ForwardSlash))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create(T("ossu_object_slash")).Build()
            });
        }

        // 三个 term 且末尾为空 → 文件已确认存在，显示替换选项
        if (query.SearchTerms.Length == 3)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create(T("ossu_action_replace"))
                    .Subtitle(T("ossu_action_replace_subtitle", objectName))
                    .Action(_ => ShowUpload(bucketName, objectName))
                    .Build(),
                ResultBuilder
                    .Create(T("ossu_action_backup_replace"))
                    .Subtitle(T("ossu_action_backup_replace_subtitle"))
                    .Action(_ => ShowUpload(bucketName, objectName, forceBackup: true))
                    .Build()
            });
        }

        // 首次查询，不检查文件是否存在
        return Task.FromResult(new List<Result>
        {
            ResultBuilder
                .Create(T("ossu_action_upload"))
                .Subtitle(T("ossu_action_upload_subtitle"))
                .Action(_ => ShowUpload(bucketName, objectName, checkExists: true))
                .Build(),
            ResultBuilder
                .Create(T("ossu_action_backup_replace"))
                .Subtitle(T("ossu_action_backup_replace_subtitle"))
                .Action(_ => ShowUpload(bucketName, objectName, forceBackup: true))
                .Build()
        });
    }

    /// <summary>
    /// 打开常驻拖拽上传气泡：文件不再通过文件选择框挑选，而是直接拖进气泡。
    /// 气泡传完（或取消）都留在原地可继续拖，由气泡上的关闭按钮结束。
    /// </summary>
    /// <param name="objectName">
    /// 目标对象名；为空表示临时上传，每个文件现场生成随机路径以免互相覆盖。
    /// </param>
    private bool ShowUpload(string bucketName, string objectName, bool checkExists = false, bool forceBackup = false)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TransferWindow
                .ForDropUpload(service, settings, _host, _loc, bucketName, objectName, checkExists, forceBackup)
                .Show();
        });
        return false;
    }
}
