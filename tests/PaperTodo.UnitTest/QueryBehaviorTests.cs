using FlowLauncher.Plugin.PaperTodo.Services;

namespace FlowLauncher.Plugin.PaperTodo.Tests;

public sealed class QueryBehaviorTests
{
    [Theory]
    [InlineData("add 买牛奶", "买牛奶")]
    [InlineData("ADD", "")]
    public void TryTakeCommand_IsCaseInsensitiveAndRequiresBoundary(string input, string expected)
    {
        Assert.True(Main.TryTakeCommand(input, "add", out var rest));
        Assert.Equal(expected, rest);
    }

    [Theory]
    [InlineData("address")]
    [InlineData("additional text")]
    public void TryTakeCommand_DoesNotConsumeSearchAsCommand(string input)
    {
        Assert.False(Main.TryTakeCommand(input, "add", out _));
    }

    [Fact]
    public void TodoTextParser_SplitsMultilinePasteAndRemovesListMarkers()
    {
        var lines = TodoTextParser.Split(
            """
            - [ ] 第一项
            * 第二项
            3. 第三项

            第四项
            """);

        Assert.Equal(["第一项", "第二项", "第三项", "第四项"], lines);
    }
}
