using System.Security.Cryptography;

namespace FlowLauncher.Plugin.PasswordManager.Services;

/// <summary>
/// 生成密码时使用的字符集配置。
/// <paramref name="Exclude"/> 中的字符会从所有类别中剔除（如 0O1lI 等易混淆字符）。
/// </summary>
public sealed record PasswordCharacterSet(
    bool Lowercase,
    bool Uppercase,
    bool Digits,
    bool Symbols,
    string Exclude = "")
{
    public const string LowerPool = "abcdefghijklmnopqrstuvwxyz";
    public const string UpperPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string DigitPool = "0123456789";
    public const string SymbolPool = "!@#$%^&*()-_=+[]{};:,.?/|~";

    /// <summary>
    /// 各类别的有效字符池（已应用忽略字符并去重），空池表示该类已被完全忽略。
    /// </summary>
    public IReadOnlyList<string> BuildPools()
    {
        var pools = new List<string>();
        if (Lowercase) Add(LowerPool);
        if (Uppercase) Add(UpperPool);
        if (Digits) Add(DigitPool);
        if (Symbols) Add(SymbolPool);
        return pools;

        void Add(string pool)
        {
            var filtered = new string(pool.Where(c => !Exclude.Contains(c)).Distinct().ToArray());
            if (filtered.Length > 0) pools.Add(filtered);
        }
    }

    /// <summary>全部可用字符，例如 "abc...ABC...123...!@#"。</summary>
    public string BuildAlphabet() => string.Concat(BuildPools());

    /// <summary>类别的中文描述，用于结果副标题与设置面板提示。</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Lowercase) parts.Add("小写");
        if (Uppercase) parts.Add("大写");
        if (Digits) parts.Add("数字");
        if (Symbols) parts.Add("符号");
        return parts.Count == 0 ? "无可用字符" : string.Join("+", parts);
    }
}

/// <summary>随机密码生成（加密随机数），每位字符独立均匀取自可用字符池。</summary>
public static class PasswordGeneratorService
{
    public const int MinLength = 1;
    public const int MaxLength = 256;

    /// <summary>设置面板中允许配置的默认长度条数上限（避免结果列表过长）。</summary>
    public const int MaxConfiguredLengths = 8;

    /// <summary>常见易混淆字符，设置面板提供一键切换。</summary>
    public const string AmbiguousChars = "0O1lI";

    /// <summary>
    /// 生成指定长度的随机密码。每个已选类别至少出现一次（长度不足时优先覆盖靠前的类别），
    /// 其余位置从全部可用字符中随机取，最后整体洗牌。
    /// </summary>
    public static string Generate(int length, PasswordCharacterSet set)
    {
        length = Math.Clamp(length, MinLength, MaxLength);

        var pools = set.BuildPools();
        if (pools.Count == 0)
            throw new InvalidOperationException("没有可用字符：请至少勾选一类字符，或减少忽略字符。");

        var alphabet = string.Concat(pools);
        var chars = new char[length];

        // 类别多于长度时随机取舍，避免永远只用前几个类别
        if (pools.Count > length)
            pools = pools.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();

        var i = 0;
        for (; i < pools.Count && i < length; i++)
            chars[i] = Pick(pools[i]);
        for (; i < length; i++)
            chars[i] = Pick(alphabet);

        Shuffle(chars);
        return new string(chars);
    }

    /// <summary>生成长度对应的随机密码；配置无效时返回 null。</summary>
    public static string? TryGenerate(int length, PasswordCharacterSet set)
    {
        try
        {
            return Generate(length, set);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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

/// <summary>把用户输入的长度列表文本解析为去重、升序、限幅的长度集合。</summary>
public static class PasswordLengthParser
{
    /// <summary>
    /// 解析文本中的长度，分隔符支持逗号、空格、分号、顿号、竖线。
    /// 非数字项被忽略；结果去重、升序、限制在 [MinLength, MaxLength] 且不超过 <paramref name="maxCount"/> 项。
    /// </summary>
    public static bool TryParse(string? text, out List<int> lengths, int maxCount = PasswordGeneratorService.MaxConfiguredLengths)
    {
        lengths = [];

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var tokens = SplitTokens(text);
        var parsed = new List<int>();

        foreach (var token in tokens)
        {
            if (!int.TryParse(token.Trim(), out var value))
                continue;

            var clamped = Math.Clamp(value, PasswordGeneratorService.MinLength, PasswordGeneratorService.MaxLength);
            if (!parsed.Contains(clamped))
                parsed.Add(clamped);
        }

        if (parsed.Count == 0)
            return false;

        lengths = parsed.Order().Take(maxCount).ToList();
        return true;
    }

    /// <summary>文本中数字项的个数（用于提示超出上限被截断）。</summary>
    public static int CountNumericTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return SplitTokens(text).Count(token => int.TryParse(token.Trim(), out _));
    }

    private static string[] SplitTokens(string text) =>
        text.Split([',', '，', ' ', ';', '；', '、', '|', '/'], StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>密码强度粗估（按可用字符集的信息熵），用于结果副标题提示。</summary>
public static class PasswordStrengthEstimator
{
    public static double EntropyBits(int length, int alphabetSize)
    {
        if (length <= 0 || alphabetSize <= 1)
            return 0;
        return length * Math.Log2(alphabetSize);
    }

    public static string Describe(int length, int alphabetSize)
    {
        var bits = EntropyBits(length, alphabetSize);
        var level = bits switch
        {
            < 40 => "弱",
            < 60 => "中",
            < 80 => "强",
            _ => "极强",
        };
        return $"{level}（约 {bits:F0} bit 熵）";
    }
}
