using System.IO;

namespace DevToolkit.AliyunOss.Utils;

/// <summary>
/// 文件选择框过滤器的组装。
/// <para>
/// <see cref="Microsoft.Win32.FileDialog.Filter"/> 的格式要求很严（"描述|模式"，
/// 多项之间以竖线分隔），传入不合法的字符串时属性赋值就直接抛
/// <see cref="ArgumentException"/>，而不是退化成"没有过滤器"。
/// </para>
/// <para>
/// 描述文案来自本地化文件、可能被翻译改动，模式则必须始终是合法通配符，
/// 因此两者在这里分开拼装：翻译只影响竖线左侧的描述，右侧模式由代码保证。
/// 与 WPF 无关，便于单独验证边界。
/// </para>
/// </summary>
public static class FileDialogFilter
{
    /// <summary>"所有文件"一项。</summary>
    public static string AllFiles(string allFilesLabel) => $"{allFilesLabel}|*.*";

    /// <summary>
    /// 按对象名推断扩展名，返回"该扩展名 + 所有文件"两项过滤器。
    /// 对象名没有扩展名时只返回"所有文件"。
    /// </summary>
    /// <param name="objectKey">OSS 对象名，用于取扩展名。</param>
    /// <param name="allFilesLabel">"所有文件"的描述文案。</param>
    /// <param name="namedLabelFormat">"{0} 文件"这类含扩展名占位符的描述文案。</param>
    public static string FromObjectKey(string objectKey, string allFilesLabel, string namedLabelFormat)
    {
        var all = AllFiles(allFilesLabel);

        var ext = Path.GetExtension(objectKey);
        if (string.IsNullOrWhiteSpace(ext) || ext == ".") return all;

        return $"{string.Format(namedLabelFormat, ext)}|*{ext}|{all}";
    }
}
