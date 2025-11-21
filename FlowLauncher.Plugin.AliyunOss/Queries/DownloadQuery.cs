using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Services;

using Microsoft.Win32;

using System.IO;
using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

internal class DownloadQuery(
    IPublicAPI publicApi,
    OssService service)
{
    private readonly static HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".webp"
    };

    public async Task<List<Result>> ExecuteAsync(Query query, CancellationToken token)
    {
        var bucketName = query.SearchTerms[0];
        var objectName = query.SearchTerms.Length > 1 ? query.SearchTerms[1] : string.Empty;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return [new Result
                {
                    Title = "输入对象地址"
                }];
        }

        var objectExist = await service.ExistsAsync(bucketName, objectName, token);

        if (!objectExist)
        {
            return [
                    new Result
                        {
                            Title = "对象不存在"
                        }
                ];
        }

        var url = await service.PresignDownloadUrlAsync(bucketName, objectName, token);
        var ext = Path.GetExtension(new Uri(url).AbsolutePath);
        var isMedia = _imageExtensions.Contains(ext);

        return
        [
            new Result
            {
                Title = "复制下载链接到剪贴板",
                Action = ctx => {
                        publicApi.CopyToClipboard(url);
                        return true;
                    },
                Preview = new Result.PreviewInfo
                    {
                        IsMedia = isMedia,
                        PreviewImagePath = isMedia ? url : string.Empty,
                        FilePath = url,
                        Description = Path.GetDirectoryName(new Uri(url).AbsolutePath)
                    }
            },
            new Result
            {
                Title = "下载",
                AsyncAction = async ctx => {
                    await publicApi.ShowProgressBoxAsync("下载中...",async (reportProcess) => {
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

                        await publicApi.HttpDownloadAsync(url, dialog.FileName, reportProcess, token);
                        var dir = Path.GetDirectoryName(url)!;
                        publicApi.OpenDirectory(url, dialog.FileName);
                    });
                    return true;
                },
                Preview = new Result.PreviewInfo
                {
                    IsMedia = isMedia,
                    PreviewImagePath = isMedia ? url : string.Empty,
                    FilePath = url,
                    Description = Path.GetDirectoryName(new Uri(url).AbsolutePath)
                }
            }
        ];
    }
}

/// <summary>
/// 支持上传，替换文件，替换时备份源文件
/// </summary>
/// <param name="publicApi"></param>
/// <param name="service"></param>
internal class UploadQuery(
    IPublicAPI publicApi,
    OssService service)
{
    public async Task<List<Result>> ExecuteAsync(Query query, CancellationToken token = default)
    {
        var bucketName = query.SearchTerms[0];
        var objectName = query.SearchTerms[1];

        return
        [
            new Result
            {
                Title = "上传文件",
                AsyncAction = async ctx => {
                    await publicApi.ShowProgressBoxAsync("上传中...",async (reportProcess) => {
                            var fileName = Path.GetFileName(objectName);
                            var ext = Path.GetExtension(fileName);
                            var dialog = new OpenFileDialog
                            {
                                Multiselect = false,
                                DefaultExt = ext,
                                Filter = string.IsNullOrWhiteSpace(ext) ? "所有文件|*.*" : $"{ext} 文件|*{ext}|所有文件|*.*",
                            };
                            var result = dialog.ShowDialog();
                            if (result != true) return;
                            var filePath = dialog.FileName;
                            await service.UploadFileAsync(bucketName, objectName, filePath, reportProcess, token);

                            var dailogResult = publicApi.ShowMsgBox("上传完毕，是否复制下载链接", "提示",MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (dailogResult == MessageBoxResult.Yes)
                            {
                                var url = await service.PresignDownloadUrlAsync(bucketName, objectName, token);
                                publicApi.CopyToClipboard(url);
                            }
                        });
                    return true;
                }
            }
        ];
    }
}
