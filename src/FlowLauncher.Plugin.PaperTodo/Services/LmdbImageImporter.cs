using FlowLauncher.Plugin.PaperTodo.Models;

using LightningDB;

using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class LmdbImageImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<NoteImage> ReadReferencedImages(
        string databasePath,
        IReadOnlyList<Paper> notes,
        ICollection<string> warnings)
    {
        if (!File.Exists(databasePath))
        {
            warnings.Add($"未找到图片库：{databasePath}");
            return [];
        }

        var referencesByNote = notes
            .Select(note => (
                Note: note,
                Ids: MarkdownImageReferences.CollectImageIds(note.Content)))
            .Where(entry => entry.Ids.Count > 0)
            .ToList();
        if (referencesByNote.Count == 0)
        {
            return [];
        }

        var fileLength = new FileInfo(databasePath).Length;
        var mapSize = Math.Max(256L * 1024 * 1024, fileLength + 16L * 1024 * 1024);
        var configuration = new EnvironmentConfiguration
        {
            MapSize = mapSize,
            MaxDatabases = 2,
        };

        var images = new List<NoteImage>();
        try
        {
            using var environment = new LightningEnvironment(databasePath, configuration);
            environment.Open(
                EnvironmentOpenFlags.ReadOnly |
                EnvironmentOpenFlags.NoSubDir |
                EnvironmentOpenFlags.NoLock);
            using var transaction = environment.BeginTransaction(TransactionBeginFlags.ReadOnly);
            using var metadataDb = transaction.OpenDatabase("meta");
            using var blobDb = transaction.OpenDatabase("blob");

            foreach (var (note, ids) in referencesByNote)
            {
                foreach (var imageId in ids)
                {
                    try
                    {
                        var key = Encoding.ASCII.GetBytes(imageId);
                        var (metadataResult, _, metadataValue) = transaction.Get(metadataDb, key);
                        if (metadataResult != MDBResultCode.Success)
                        {
                            warnings.Add($"笔记“{note.Title}”缺少图片 {imageId} 的元数据。");
                            continue;
                        }

                        var asset = JsonSerializer.Deserialize<PaperTodoImageAsset>(
                            metadataValue.AsSpan(),
                            JsonOptions);
                        if (asset is null ||
                            !string.Equals(asset.Id, imageId, StringComparison.Ordinal) ||
                            !string.Equals(asset.NoteId, note.Id, StringComparison.Ordinal))
                        {
                            warnings.Add($"图片 {imageId} 的归属或元数据无效，已跳过。");
                            continue;
                        }

                        var (blobResult, _, blobValue) = transaction.Get(blobDb, key);
                        if (blobResult != MDBResultCode.Success)
                        {
                            warnings.Add($"笔记“{note.Title}”缺少图片 {imageId} 的二进制数据。");
                            continue;
                        }

                        var bytes = blobValue.AsSpan().ToArray();
                        if (bytes.Length != asset.ByteLength ||
                            asset.Width <= 0 ||
                            asset.Height <= 0 ||
                            asset.Width > 4096 ||
                            asset.Height > 4096)
                        {
                            warnings.Add($"图片 {imageId} 的尺寸或长度无效，已跳过。");
                            continue;
                        }

                        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                        if (!string.Equals(actualHash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add($"图片 {imageId} 的 SHA-256 校验失败，已跳过。");
                            continue;
                        }

                        images.Add(new NoteImage
                        {
                            Id = imageId,
                            PaperId = note.Id,
                            Mime = string.IsNullOrWhiteSpace(asset.Mime) ? "image/png" : asset.Mime,
                            Width = asset.Width,
                            Height = asset.Height,
                            ByteLength = bytes.Length,
                            Sha256 = actualHash,
                            Bytes = bytes,
                            CreatedAt = asset.CreatedAt == default
                                ? DateTimeOffset.UtcNow
                                : asset.CreatedAt,
                        });
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"图片 {imageId} 读取失败：{ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"图片库读取失败，Todo 和 Note 文本仍会继续导入：{ex.Message}");
        }

        return images;
    }

    private sealed class PaperTodoImageAsset
    {
        public string Id { get; set; } = "";
        public string NoteId { get; set; } = "";
        public string Mime { get; set; } = "image/png";
        public int Width { get; set; }
        public int Height { get; set; }
        public string Sha256 { get; set; } = "";
        public int ByteLength { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
