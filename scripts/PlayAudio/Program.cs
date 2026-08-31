using NAudio.Wave;

var filePath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "core-capability-1.wav");
var loop = args.Contains("--loop");

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"文件不存在: {filePath}");
    return 1;
}

Console.WriteLine($"音频文件: {filePath}");

var count = WaveOut.DeviceCount;
Console.WriteLine($"\n可用输出设备 ({count} 个):");
var autoDevice = -1;
for (var i = 0; i < count; i++)
{
    var caps = WaveOut.GetCapabilities(i);
    var name = caps.ProductName;
    var marker = "";
    if (name.Contains("VB-Audio") || name.Contains("Cable") || name.Contains("Virtual"))
    {
        autoDevice = i;
        marker = " <-- 虚拟声卡";
    }
    Console.WriteLine($"  [{i}] {name}{marker}");
}

var deviceNumber = autoDevice >= 0 ? autoDevice : 0;
Console.WriteLine($"\n使用设备 [{deviceNumber}] 播放...");

using var reader = new AudioFileReader(filePath);
using var waveOut = new WaveOut { DeviceNumber = deviceNumber };

var stopped = false;
waveOut.PlaybackStopped += (_, _) =>
{
    if (loop && !stopped)
    {
        reader.CurrentTime = TimeSpan.Zero;
        waveOut.Play();
    }
    else
    {
        stopped = true;
    }
};

waveOut.Init(reader);
waveOut.Play();
Console.WriteLine($"正在播放 (时长: {reader.TotalTime:mm\\:ss})，按 Ctrl+C 停止...");

while (!stopped)
{
    Console.Write($"\r进度: {reader.CurrentTime:mm\\:ss} / {reader.TotalTime:mm\\:ss}   ");
    Thread.Sleep(200);
}

Console.WriteLine("\n播放完成");
return 0;
