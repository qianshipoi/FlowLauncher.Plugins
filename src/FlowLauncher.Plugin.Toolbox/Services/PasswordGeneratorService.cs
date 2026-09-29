using System.Security.Cryptography;

namespace FlowLauncher.Plugin.Toolbox.Services;

/// <summary>随机密码/密钥生成（加密随机数）。</summary>
public static class PasswordGeneratorService
{
    public const int DefaultLength = 24;
    public const int MaxLength = 256;

    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?";
    private const string Ambiguous = "0O1lI";

    public static IReadOnlyList<ToolRow> Generate(int length)
    {
        length = Math.Clamp(length, 1, MaxLength);
        return
        [
            new ToolRow("强密码（大小写 + 数字 + 符号）", GenerateStrong(length)),
            new ToolRow("密码（无易混淆字符 0O1lI）", GenerateNoAmbiguous(length)),
            new ToolRow("Hex 密钥（API Key 风格）", RandomHex(length)),
        ];
    }

    private static string GenerateStrong(int length)
    {
        // 保证四类字符至少各一个，其余随机填充后整体洗牌
        var all = Lower + Upper + Digits + Symbols;
        var chars = new char[length];
        var pools = new[] { Lower, Upper, Digits, Symbols };

        var i = 0;
        for (; i < pools.Length && i < length; i++)
            chars[i] = Pick(pools[i]);
        for (; i < length; i++)
            chars[i] = Pick(all);
        Shuffle(chars.AsSpan(0, length));
        return new string(chars);
    }

    private static string GenerateNoAmbiguous(int length)
    {
        var pool = new string((Lower + Upper + Digits).Except(Ambiguous).ToArray());
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Pick(pool);
        return new string(chars);
    }

    private static string RandomHex(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes((length + 1) / 2);
        return Convert.ToHexString(bytes).ToLowerInvariant()[..length];
    }

    private static char Pick(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];

    private static void Shuffle(Span<char> chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
