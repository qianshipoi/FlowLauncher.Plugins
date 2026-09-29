using DevToolkit.AliyunOss.Utils;

namespace AliyunOss.UnitTest;

/// <summary>
/// 回归保护：文件选择框过滤器格式错误会让宿主崩溃。
/// FileDialog.Filter 赋值不合法字符串时直接抛 ArgumentException，
/// 曾因把本地化文案当整条过滤器用（"transfer_filter_all" 未命中返回的提示串没有竖线）而崩在窗口 Loaded 里。
/// </summary>
public class FileDialogFilterTests
{
    /// <summary>Win32 文件对话框要求的格式：描述|模式，多项以竖线分隔，两项一组。</summary>
    private static void AssertValidFilterSyntax(string filter)
    {
        Assert.False(string.IsNullOrWhiteSpace(filter));

        var parts = filter.Split('|');
        Assert.True(parts.Length % 2 == 0, $"过滤器必须是「描述|模式」成对出现，实际: {filter}");

        for (var i = 0; i < parts.Length; i += 2)
        {
            Assert.False(string.IsNullOrWhiteSpace(parts[i]), $"描述不能为空: {filter}");
            Assert.False(string.IsNullOrWhiteSpace(parts[i + 1]), $"模式不能为空: {filter}");
            Assert.Contains("*", parts[i + 1]);
        }
    }

    [Theory]
    [InlineData("photo.png", "所有文件", "{0} 文件")]
    [InlineData("a/b/c/report.tar.gz", "所有文件", "{0} 文件")]
    [InlineData("noext", "所有文件", "{0} 文件")]
    [InlineData("dir/", "所有文件", "{0} 文件")]
    [InlineData("", "所有文件", "{0} 文件")]
    [InlineData("photo.png", "All files", "{0} files")]
    public void FromObjectKey_AlwaysProducesValidFilter(string key, string allLabel, string namedFormat)
    {
        AssertValidFilterSyntax(FileDialogFilter.FromObjectKey(key, allLabel, namedFormat));
    }

    [Fact]
    public void FromObjectKey_WithExtension_OffersExtensionAndAllFiles()
    {
        var filter = FileDialogFilter.FromObjectKey("photo.png", "所有文件", "{0} 文件");

        Assert.Equal(" .png 文件|*.png|所有文件|*.*".TrimStart(), filter);
    }

    [Fact]
    public void FromObjectKey_WithoutExtension_OnlyAllFiles()
    {
        var filter = FileDialogFilter.FromObjectKey("README", "所有文件", "{0} 文件");

        Assert.Equal("所有文件|*.*", filter);
    }

    /// <summary>对象名只有点结尾时不应产生 "*.|" 这种空扩展名模式。</summary>
    [Fact]
    public void FromObjectKey_TrailingDot_TreatedAsNoExtension()
    {
        var filter = FileDialogFilter.FromObjectKey("weird.", "所有文件", "{0} 文件");

        Assert.Equal("所有文件|*.*", filter);
    }

    [Fact]
    public void AllFiles_AppendsWildcardPattern()
    {
        Assert.Equal("所有文件|*.*", FileDialogFilter.AllFiles("所有文件"));
        AssertValidFilterSyntax(FileDialogFilter.AllFiles("All files"));
    }
}
