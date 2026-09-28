using DevToolkit.AliyunOss.Models;

using System.IO;

using System.Net.Http;

namespace DevToolkit.AliyunOss;

/// <summary>带进度的 HTTP 文件下载（不依赖任何插件的宿主 API，插件与独立 App 共用）。</summary>
public static class DownloadService
{
    public static async Task DownloadFileAsync(
        string url, string filePath, IProgress<TransferProgress> progress, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        using var response = await http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var target = new FileStream(
                filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81_920, useAsync: true);

            var buffer = new byte[81_920];
            long transferred = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                transferred += read;
                progress.Report(new TransferProgress(transferred, total > 0 ? total : 0));
            }

            progress.Report(TransferProgress.Completed(transferred));
        }
        catch (OperationCanceledException)
        {
            // 取消时清掉半成品文件，避免用户误以为下载成功
            TryDelete(filePath);
            throw;
        }
        catch (Exception)
        {
            TryDelete(filePath);
            throw;
        }
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
