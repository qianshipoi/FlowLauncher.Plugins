using FlowLauncher.Plugin.PaperTodo.Services;

using System.IO;

namespace FlowLauncher.Plugin.PaperTodo.Tests;

public sealed class WebEditorAssetTests
{
    [Fact]
    public void MarkdownImageReferences_RoundTripPaperTodoIds()
    {
        const string stored = "正文\n\n![image|100%](i:1001)\n";
        var editor = MarkdownImageReferences.ToEditorMarkdown(stored);
        var roundTrip = MarkdownImageReferences.ToStoredMarkdown(editor);

        Assert.Contains("https://papertodo.local/images/1001", editor, StringComparison.Ordinal);
        Assert.Equal(stored, roundTrip);
        Assert.Equal(["1001"], MarkdownImageReferences.CollectImageIds(roundTrip));
    }

    [Fact]
    public void EditorAssets_AreLocalAndContainImageBridge()
    {
        var root = FindRepositoryRoot();
        var webRoot = Path.Combine(
            root,
            "src",
            "FlowLauncher.Plugin.PaperTodo",
            "wwwroot");
        var html = File.ReadAllText(Path.Combine(webRoot, "note-editor.html"));
        var script = File.ReadAllText(Path.Combine(webRoot, "note-editor.js"));

        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vendor/toastui-editor-all.min.js", html, StringComparison.Ordinal);
        Assert.Contains("addImageBlobHook", script, StringComparison.Ordinal);
        Assert.Contains("hooks:", script, StringComparison.Ordinal);
        Assert.DoesNotContain("editor.addImageBlobHook", script, StringComparison.Ordinal);
        Assert.Contains("imageBegin", script, StringComparison.Ordinal);
        Assert.Contains("usageStatistics: false", script, StringComparison.Ordinal);
        Assert.True(new FileInfo(Path.Combine(webRoot, "vendor", "toastui-editor-all.min.js")).Length > 500_000);
        Assert.True(new FileInfo(Path.Combine(webRoot, "vendor", "toastui-editor.min.css")).Length > 100_000);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FlowLauncher.Plugins.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("无法定位仓库根目录。");
    }
}
