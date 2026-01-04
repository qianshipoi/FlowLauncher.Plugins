using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Services;

using Microsoft.Win32;

using System.IO;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

internal class DownloadQuery(IPublicAPI publicApi, OssService service)
{
    private readonly static HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".webp"
    };

    public async Task<List<Result>> ExecuteAsync(Query query, CancellationToken token)
    {
        var bucketName = query.SearchTerms[0];
        var objectName = query.SearchTerms[1];

        var objectExist = await service.ExistsAsync(bucketName, objectName, token);

        if (!objectExist)
        {
            return 
            [
                ResultBuilder.Create("对象不存在").Build()
            ];
        }

        var url = await service.PresignDownloadUrlAsync(bucketName, objectName, token);
        var ext = Path.GetExtension(new Uri(url).AbsolutePath);
        var isMedia = _imageExtensions.Contains(ext);

        var preview = new Result.PreviewInfo
        {
            IsMedia = isMedia,
            PreviewImagePath = isMedia ? url : string.Empty,
            FilePath = url,
            Description = Path.GetDirectoryName(new Uri(url).AbsolutePath)
        };

        return
        [
            ResultBuilder.Create("复制链接到剪贴板").Action(_ => CopyToClipboard(url)).Preview(preview).Build(),
            ResultBuilder.Create("下载").AsyncAction(_ => DownloadAsync(url, token)).Preview(preview).Build()
        ];
    }

    private async ValueTask<bool> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        await publicApi.ShowProgressBoxAsync("下载中...", async (reportProcess) => {
            var dialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(new Uri(url).AbsolutePath),
            };

            var result = dialog.ShowDialog();
            if (result != true) return;
            if (File.Exists(dialog.FileName))
            {
                File.Delete(dialog.FileName);
            }

            await publicApi.HttpDownloadAsync(url, dialog.FileName, reportProcess, cancellationToken);
            var dir = Path.GetDirectoryName(url)!;
            publicApi.OpenDirectory(url, dialog.FileName);
        });
        return true;
    }

    private bool CopyToClipboard(string url)
    {
        publicApi.CopyToClipboard(url);
        return true;
    }
}
