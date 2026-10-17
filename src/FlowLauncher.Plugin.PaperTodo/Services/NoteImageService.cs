using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class NoteImageService
{
    private const int MaxStoredDimension = 4096;
    private const int AutoCompressedDimension = 2048;
    private const int MaxStoredBytes = 8 * 1024 * 1024;
    private const int MaxInputBytes = 32 * 1024 * 1024;

    public PreparedImage Prepare(byte[] input, bool autoCompress)
    {
        if (input.Length == 0 || input.Length > MaxInputBytes)
        {
            throw new InvalidDataException("图片为空或超过 32 MB。");
        }

        var decoded = Decode(input);
        if (decoded.PixelWidth > MaxStoredDimension || decoded.PixelHeight > MaxStoredDimension)
        {
            throw new InvalidDataException("图片宽或高不能超过 4096 像素。");
        }

        var original = EncodeToPng(decoded);
        if (!autoCompress && original.Length > MaxStoredBytes)
        {
            throw new InvalidDataException("图片超过 8 MB，且已关闭自动压缩。");
        }

        if (!NeedsCompression(original.Length, decoded.PixelWidth, decoded.PixelHeight))
        {
            return new PreparedImage(original, "image/png", decoded.PixelWidth, decoded.PixelHeight);
        }

        var resized = ResizeForStorage(decoded);
        var compressed = TryEncodeJpeg(resized) ?? EncodeToPng(resized);
        var final = compressed.Length < original.Length ? compressed : original;
        if (final.Length > MaxStoredBytes)
        {
            throw new InvalidDataException("图片压缩后仍超过 8 MB。");
        }

        var bitmap = Decode(final);
        return new PreparedImage(
            final,
            GetMime(final),
            bitmap.PixelWidth,
            bitmap.PixelHeight);
    }

    public byte[] DecodeDataUrl(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || !dataUrl.AsSpan(0, comma).Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("图片数据格式无效。");
        }
        return Convert.FromBase64String(dataUrl[(comma + 1)..]);
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if (frame.CanFreeze)
        {
            frame.Freeze();
        }
        return frame;
    }

    private static bool NeedsCompression(int byteLength, int width, int height) =>
        byteLength > MaxStoredBytes ||
        Math.Max(width, height) > AutoCompressedDimension;

    private static BitmapSource ResizeForStorage(BitmapSource source)
    {
        var longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longest <= AutoCompressedDimension)
        {
            return source;
        }

        var scale = AutoCompressedDimension / (double)longest;
        var transformed = new TransformedBitmap(
            source,
            new ScaleTransform(scale, scale));
        if (transformed.CanFreeze)
        {
            transformed.Freeze();
        }
        return transformed;
    }

    private static byte[] EncodeToPng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static byte[]? TryEncodeJpeg(BitmapSource source)
    {
        try
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
            var encoder = new JpegBitmapEncoder { QualityLevel = 86 };
            encoder.Frames.Add(BitmapFrame.Create(converted));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string GetMime(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47)
        {
            return "image/png";
        }
        return "image/jpeg";
    }
}

internal sealed record PreparedImage(byte[] Bytes, string Mime, int Width, int Height);
