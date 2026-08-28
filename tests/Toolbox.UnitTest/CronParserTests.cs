using DevToolkit.Toolbox.Services;

namespace Toolbox.UnitTest;

public class CronParserTests
{
    [Fact]
    public void GetInfo_工作日早九_描述与未来执行()
    {
        var from = new DateTime(2026, 1, 5, 0, 0, 0); // 周一
        var rows = CronParserService.GetInfo("0 9 * * 1-5", from);

        Assert.Equal(CronParserService.PreviewCount + 1, rows.Count);
        Assert.Contains(rows, r => r.Description == "含义" && r.Value.Contains("09:00") && r.Value.Contains("周一"));

        var first = DateTime.Parse(rows.First(r => r.Description == "第 1 次执行").Value[..19]);
        Assert.Equal(new DateTime(2026, 1, 5, 9, 0, 0), first);
        Assert.Equal(DayOfWeek.Monday, first.DayOfWeek);

        // 后续预览单调递增
        var times = rows.Where(r => r.Description.StartsWith("第 "))
            .Select(r => DateTime.Parse(r.Value[..19]))
            .ToList();
        Assert.Equal(times, times.OrderBy(t => t).ToList());
    }

    [Fact]
    public void GetInfo_每十分钟_首次执行在下一个整十分钟()
    {
        var from = new DateTime(2026, 1, 5, 0, 7, 0);
        var rows = CronParserService.GetInfo("*/10 * * * *", from);

        Assert.Contains(rows, r => r.Description == "含义" && r.Value.Contains("每 10 分钟"));
        var first = DateTime.Parse(rows.First(r => r.Description == "第 1 次执行").Value[..19]);
        Assert.Equal(0, first.Minute % 10);
        Assert.True(first > from);
    }

    [Fact]
    public void GetInfo_六段表达式_秒字段进入描述()
    {
        var rows = CronParserService.GetInfo("*/30 * * * * *", new DateTime(2026, 1, 5, 0, 0, 0));
        Assert.Contains(rows, r => r.Description == "含义" && r.Value.Contains("每 30 秒"));
    }

    [Fact]
    public void GetInfo_非法表达式_抛出格式异常()
    {
        Assert.Throws<FormatException>(() => CronParserService.GetInfo("99 * * * *", new DateTime(2026, 1, 5, 0, 0, 0)));
        Assert.Throws<FormatException>(() => CronParserService.GetInfo("* * * *", new DateTime(2026, 1, 5, 0, 0, 0)));
    }
}
