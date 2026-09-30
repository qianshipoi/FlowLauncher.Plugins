using FlowLauncher.Plugin.PasswordManager.Services;

namespace FlowLauncher.Plugin.PasswordManager.Models;

public class Settings
{
    /// <summary>
    /// 复制后多少毫秒自动清除剪贴板（0表示不清除）
    /// </summary>
    public int ClearClipboardAfterMs { get; set; } = 15000;

    /// <summary>
    /// 默认显示的最近条目数量
    /// </summary>
    public int RecentItemsCount { get; set; } = 10;

    /// <summary>
    /// 随机密码生成器中默认展示的长度列表（逗号分隔）
    /// </summary>
    public string GeneratorLengths { get; set; } = "8, 12, 16, 20, 24";

    /// <summary>
    /// 生成小写字母
    /// </summary>
    public bool GeneratorLowercase { get; set; } = true;

    /// <summary>
    /// 生成大写字母
    /// </summary>
    public bool GeneratorUppercase { get; set; } = true;

    /// <summary>
    /// 生成数字
    /// </summary>
    public bool GeneratorDigits { get; set; } = true;

    /// <summary>
    /// 生成符号
    /// </summary>
    public bool GeneratorSymbols { get; set; } = true;

    /// <summary>
    /// 要求忽略的字符，这些字符不会出现在生成的密码中（如 0O1lI）
    /// </summary>
    public string GeneratorExcludeChars { get; set; } = "";

    /// <summary>
    /// 默认显示的长度列表，解析失败时回退到 8/12/16/20/24
    /// </summary>
    public IReadOnlyList<int> GetGeneratorLengths() =>
        PasswordLengthParser.TryParse(GeneratorLengths, out var lengths)
            ? lengths
            : [8, 12, 16, 20, 24];

    /// <summary>
    /// 当前配置对应的字符集
    /// </summary>
    public PasswordCharacterSet GetCharacterSet() =>
        new(GeneratorLowercase, GeneratorUppercase, GeneratorDigits, GeneratorSymbols, GeneratorExcludeChars ?? "");
}
