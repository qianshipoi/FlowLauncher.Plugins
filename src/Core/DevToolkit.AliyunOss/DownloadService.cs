using System.IO;

using System.Net.Http;

namespace DevToolkit.AliyunOss;

/// <summary>带进度的 HTTP 文件下载（替代插件宿主的 HttpDownloadAsync，供独立 App 使用）。</summary>
public static class DownloadService
{
    public static async Task DownloadFileAsync(
        string url, string filePath, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(filePath, FileMode.Create, FileAccess.Write);

        var buffer = new byte[81_920];
        long transferred = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            transferred += read;
            if (total > 0)
                progress.Report((double)transferred / total);
        }
    }
}
