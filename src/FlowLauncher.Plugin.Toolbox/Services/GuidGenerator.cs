using System.Security.Cryptography;

using System.Text;

namespace FlowLauncher.Plugin.Toolbox.Services;

/// <summary>
/// GUID 生成器：支持 RFC 4122 的 v1/v3/v4/v5 与 v7。
/// v1 使用随机节点并置多播位，不采集真实 MAC 地址，避免泄露硬件信息。
/// </summary>
public static class GuidGenerator
{
    private static readonly long s_gregorianEpochTicks =
        new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc).Ticks;

    private static readonly Lock s_v1Lock = new();
    private static long s_lastV1Timestamp;
    private static ushort s_clockSequence = (ushort)RandomNumberGenerator.GetInt32(0, 0x4000);

    /// <summary>RFC 4122 定义的 DNS 命名空间 UUID。</summary>
    public static readonly Guid DnsNamespace = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>RFC 4122 定义的 URL 命名空间 UUID。</summary>
    public static readonly Guid UrlNamespace = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>生成 v1：基于时间戳与随机节点，同进程内严格递增不重复。</summary>
    public static Guid NewV1()
    {
        lock (s_v1Lock)
        {
            var timestamp = DateTime.UtcNow.Ticks - s_gregorianEpochTicks;
            if (timestamp <= s_lastV1Timestamp)
                timestamp = s_lastV1Timestamp + 1;
            s_lastV1Timestamp = timestamp;

            Span<byte> bytes = stackalloc byte[16];
            bytes[0] = (byte)(timestamp >> 24);
            bytes[1] = (byte)(timestamp >> 16);
            bytes[2] = (byte)(timestamp >> 8);
            bytes[3] = (byte)timestamp;
            bytes[4] = (byte)(timestamp >> 40);
            bytes[5] = (byte)(timestamp >> 32);
            bytes[6] = (byte)((timestamp >> 56) & 0x0F | 0x10); // 版本位 1
            bytes[7] = (byte)(timestamp >> 48);
            bytes[8] = (byte)((s_clockSequence >> 8) | 0x80);   // 变体位 RFC 4122
            bytes[9] = (byte)s_clockSequence;
            RandomNumberGenerator.Fill(bytes[10..]);
            bytes[10] |= 0x01; // 多播位，表示节点为随机生成

            return ToRfcGuid(bytes);
        }
    }

    /// <summary>生成 v3：基于 MD5 的命名 UUID（RFC 4122）。</summary>
    public static Guid NewV3(Guid namespaceId, string name) =>
        NewNameBased(namespaceId, name, 3, MD5.HashData);

    /// <summary>生成 v4：随机 GUID。</summary>
    public static Guid NewV4() => Guid.NewGuid();

    /// <summary>生成 v5：基于 SHA-1 的命名 UUID（RFC 4122）。</summary>
    public static Guid NewV5(Guid namespaceId, string name) =>
        NewNameBased(namespaceId, name, 5, SHA1.HashData);

    /// <summary>生成 v7：Unix 毫秒时间戳 + 随机数（.NET 9 内置）。</summary>
    public static Guid NewV7() => Guid.CreateVersion7();

    private static Guid NewNameBased(Guid namespaceId, string name, byte version, Func<byte[], byte[]> hashData)
    {
        // RFC 4122 要求命名空间 UUID 以网络字节序参与哈希，
        // 而 Guid.ToByteArray() 的前三个字段是小端序，需要翻转回来。
        var ns = namespaceId.ToByteArray();
        Array.Reverse(ns, 0, 4);
        Array.Reverse(ns, 4, 2);
        Array.Reverse(ns, 6, 2);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[ns.Length + nameBytes.Length];
        ns.CopyTo(input, 0);
        nameBytes.CopyTo(input, ns.Length);

        var hash = hashData(input);
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | (version << 4));
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return ToRfcGuid(bytes);
    }

    /// <summary>
    /// Guid(byte[]) 的前三组字段按小端序解释，字符串显示与 RFC 4122 字节序相反；
    /// 此处预先反转前三组字节，使 guid.ToString() 输出符合 RFC 4122。
    /// </summary>
    private static Guid ToRfcGuid(ReadOnlySpan<byte> rfcBytes)
    {
        Span<byte> swapped = stackalloc byte[16];
        rfcBytes.CopyTo(swapped);
        swapped[..4].Reverse();
        swapped[4..6].Reverse();
        swapped[6..8].Reverse();
        return new Guid(swapped);
    }
}
