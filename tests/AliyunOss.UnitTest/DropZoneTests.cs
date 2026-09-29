using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Services;
using DevToolkit.AliyunOss.Transfer;
using DevToolkit.AliyunOss.ViewModels;

namespace AliyunOss.UnitTest;

/// <summary>
/// 常驻拖拽气泡：ossu 打开后停在屏幕上等拖入，可反复使用。
/// 打开时确认用户指定了对象名就每次传同一路径，否则（临时上传）每个文件现场生成随机路径。
/// </summary>
public class DropZoneTests
{
    // ---- 目标对象名解析 ----

    [Fact]
    public void ResolveKey_WithFixedKey_AlwaysUsesSamePath()
    {
        var first = TransferEngine.ResolveKey("dir/report.pdf", @"C:\a\one.pdf");
        var second = TransferEngine.ResolveKey("dir/report.pdf", @"C:\b\two.png");

        // 指定了对象名 → 每次都落到该路径（再次上传即覆盖）
        Assert.Equal("dir/report.pdf", first);
        Assert.Equal("dir/report.pdf", second);
    }

    [Fact]
    public void ResolveKey_WithoutFixedKey_GeneratesDistinctTemporaryPaths()
    {
        var first = TransferEngine.ResolveKey("", @"C:\a\photo.png");
        var second = TransferEngine.ResolveKey(null, @"C:\a\photo.png");

        // 临时上传 → 同一份文件两次拖入也不能是同一个键，否则后一次会覆盖前一次
        Assert.NotEqual(first, second);
        Assert.StartsWith("tmp/", first, StringComparison.Ordinal);
        Assert.EndsWith(".png", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveKey_TreatsWhitespaceKeyAsTemporaryUpload()
    {
        var key = TransferEngine.ResolveKey("   ", @"C:\a\photo.png");

        Assert.StartsWith("tmp/", key, StringComparison.Ordinal);
    }

    // ---- 取消不关窗、关闭另有按钮 ----

    [Fact]
    public void ConfirmCancel_OnDropZone_KeepsBubbleOpen()
    {
        var (vm, closed) = CreateDropZone();

        vm.ConfirmCancel();

        // 取消只中止本轮传输，气泡留在原地等下一次拖入
        Assert.False(closed());
        Assert.True(vm.IsDropZone);
        Assert.False(vm.IsBusy);
        Assert.Equal("transfer_drop_hint", vm.StatusText);
    }

    [Fact]
    public void CloseWindow_AlwaysCloses()
    {
        var (vm, closed) = CreateDropZone();

        vm.CloseWindowCommand.Execute(null);

        Assert.True(closed());
    }

    [Fact]
    public void Cancel_OnNonDropWindow_StillClosesLikeBefore()
    {
        var vm = CreateViewModel();
        var closed = false;
        vm.CloseRequested += () => closed = true;

        // 非常驻窗口的取消语义未被改动：非传输中即关闭
        vm.CancelCommand.Execute(null);

        Assert.True(closed);
    }

    // ---- 拖拽反馈 ----

    [Fact]
    public void SetDragOver_OnIdleDropZone_SwapsHintText()
    {
        var (vm, _) = CreateDropZone();

        vm.SetDragOver(true);
        Assert.True(vm.IsDragOver);
        Assert.Equal("transfer_drop_release", vm.StatusText);

        vm.SetDragOver(false);
        Assert.False(vm.IsDragOver);
        Assert.Equal("transfer_drop_hint", vm.StatusText);
    }

    [Fact]
    public void ConfigureDropZone_StopsIndeterminateSpin()
    {
        var (vm, _) = CreateDropZone();

        // 等待拖入不是传输中，环应静止（否则像卡在 0% 空转）
        Assert.False(vm.IsIndeterminate);
        Assert.True(vm.IsDropIdle);
    }

    // ---- 测试替身 ----

    private static (TransferViewModel Vm, Func<bool> Closed) CreateDropZone()
    {
        var vm = CreateViewModel();
        var closed = false;
        vm.CloseRequested += () => closed = true;

        vm.ConfigureDropZone("my-bucket", "", checkExists: false, forceBackup: false);

        return (vm, () => closed);
    }

    private static TransferViewModel CreateViewModel()
    {
        var settings = new Settings();
        var loc = new FakeLocalization();
        return new TransferViewModel(
            new TransferEngine(new OssService(settings), loc), settings, new FakeHost(), loc);
    }

    private sealed class FakeLocalization : ILocalization
    {
        /// <summary>返回 key 本身，便于断言到底取了哪个文案。</summary>
        public string Get(string key, params object[] args) =>
            args.Length > 0 ? string.Format(key, args) : key;
    }

    private sealed class FakeHost : ITransferHost
    {
        public void CopyText(string text) { }

        public void RevealInExplorer(string filePath) { }

        public BubblePosition? LoadBubblePosition() => null;

        public void SaveBubblePosition(BubblePosition position) { }
    }
}
