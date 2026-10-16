using FlowLauncher.Plugin.S3.Transfer;

using Flow.Launcher.Plugin;

using System.IO;

namespace FlowLauncher.Plugin.S3.Views;

/// <summary>把 Flow Launcher 的公共 API 适配为传输窗口所需的宿主能力。</summary>
internal sealed class FlowTransferHost(IPublicAPI publicApi) : ITransferHost
{
    public void CopyText(string text) => publicApi.CopyToClipboard(text, showDefaultNotification: false);

    public void RevealInExplorer(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        publicApi.OpenDirectory(
            string.IsNullOrEmpty(directory) ? filePath : directory,
            Path.GetFileName(filePath));
    }

    public BubblePosition? LoadBubblePosition()
    {
        try
        {
            var storage = publicApi.LoadSettingJsonStorage<BubblePositionStorage>();
            if (!storage.HasPosition) return null;

            return new BubblePosition(storage.X, storage.Y);
        }
        catch
        {
            // 位置记录损坏不应影响打开窗口，退回屏幕中央
            return null;
        }
    }

    public void SaveBubblePosition(BubblePosition position)
    {
        try
        {
            var storage = publicApi.LoadSettingJsonStorage<BubblePositionStorage>();
            storage.X = position.X;
            storage.Y = position.Y;
            storage.HasPosition = true;
            publicApi.SaveSettingJsonStorage<BubblePositionStorage>();
        }
        catch
        {
            // 保存失败只影响下次的默认位置，不值得打断传输
        }
    }
}

/// <summary>气泡窗口位置的持久化载体（独立文件，不与密钥设置混存）。</summary>
public sealed class BubblePositionStorage
{
    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>是否记录过有效位置。首次使用前为 false。</summary>
    public bool HasPosition { get; set; }
}
