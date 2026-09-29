# scripts/play-to-virtual-audio.ps1
# 将音频文件播放到 VB-Audio Virtual Cable 虚拟声卡（用于语音识别插件联调）。
#
# 用法:
#   .\play-to-virtual-audio.ps1
#   .\play-to-virtual-audio.ps1 -FilePath D:\test.wav
#   .\play-to-virtual-audio.ps1 -FilePath D:\test.wav -NAudioDllPath C:\lib\NAudio.dll
#   .\play-to-virtual-audio.ps1 -FilePath D:\test.wav -DeviceNumber 2 -Loop -LogFile play.log
#
# 说明:
#   嵌入的 C# 播放器不使用 Console.WriteLine —— 在 .NET (Core) 下 Add-Type 默认不引用
#   System.Console.dll，会直接编译失败（CS0234 找不到 Console）。
#   这里改为通过 Action<string,string> 回调把日志交回 PowerShell 统一输出，
#   从而获得统一的时间戳、级别与日志文件落盘能力。
#
# 环境: PowerShell 7+（.NET 9 运行时）；项目目标框架 net9.0-windows7.0。
#       NAudio 2.x 为 netstandard2.0 程序集，可在 .NET 9 下直接加载。

#requires -Version 7.0

param(
    # 待播放的音频文件路径（默认使用仓库根目录的 core-capability-1.wav）
    [string]$FilePath = (Join-Path $PSScriptRoot '..\core-capability-1.wav'),

    # NAudio.dll 路径；缺省时自动在测试/插件目录的 bin 下搜索
    [string]$NAudioDllPath,

    # 输出设备编号；-1 表示自动挑选虚拟声卡
    [int]$DeviceNumber = -1,

    # 循环播放
    [switch]$Loop,

    # 可选：同时把日志写入该文件
    [string]$LogFile
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- 日志 ----------------------------------------------------------------

$script:LogPath = $LogFile

function Write-Log {
    param(
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('INFO', 'WARN', 'ERROR')][string]$Level = 'INFO'
    )

    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] [$Level] $Message"

    switch ($Level) {
        'ERROR' { Write-Host $line -ForegroundColor Red }
        'WARN' { Write-Host $line -ForegroundColor Yellow }
        default { Write-Host $line }
    }

    if ($script:LogPath) {
        try {
            Add-Content -LiteralPath $script:LogPath -Value $line -Encoding UTF8 -ErrorAction Stop
        }
        catch {
            # 日志落盘失败不应中断播放
            Write-Host "[WARN] 写入日志文件失败: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# -------------------------------------------------------------- 辅助函数 --------------------------------------------------------------

function Resolve-AudioFile {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw '音频文件路径为空，请通过 -FilePath 指定。'
    }

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "音频文件不存在: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-NAudioDll {
    param([string]$ExplicitPath)

    # 优先使用显式指定的路径
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "指定的 NAudio.dll 不存在: $ExplicitPath"
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $searchRoots = @(
        (Join-Path $PSScriptRoot '..\tests\SpeechToText.UnitTest\bin'),
        (Join-Path $PSScriptRoot '..\src\FlowLauncher.Plugin.SpeechToText\bin')
    )

    foreach ($root in $searchRoots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $hit = Get-ChildItem -LiteralPath $root -Recurse -Filter 'NAudio.dll' -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($hit) {
            return $hit.FullName
        }
    }

    throw '未找到 NAudio.dll。请先编译项目（dotnet build），或使用 -NAudioDllPath 显式指定路径。'
}

# -------------------------------------------------------------- C# 播放器 --------------------------------------------------------------

# 注意: 使用单引号 here-string，避免 PowerShell 对 C# 中的 $ 做变量展开。
# 该类型不引用 Console，所有输出经 log 回调传回 PowerShell。
$PlayerSource = @'
using System;
using NAudio.Wave;

// 注意: 不使用 List<T>/Console —— Add-Type 默认不引用 System.Collections.dll 与
// System.Console.dll，用到会直接编译失败（CS0246 / CS0234）。
public static class AudioPlayer
{
    public static string[] GetDeviceNames()
    {
        int count = WaveOut.DeviceCount;
        var names = new string[count];

        for (int i = 0; i < count; i++)
        {
            try
            {
                names[i] = WaveOut.GetCapabilities(i).ProductName;
            }
            catch (Exception)
            {
                names[i] = "(无法读取设备 " + i + ")";
            }
        }

        return names;
    }

    public static int FindVirtualDevice()
    {
        int count = WaveOut.DeviceCount;

        for (int i = 0; i < count; i++)
        {
            string name;
            try
            {
                name = WaveOut.GetCapabilities(i).ProductName;
            }
            catch (Exception)
            {
                continue;
            }

            if (name == null)
            {
                continue;
            }

            if (name.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Cable", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    public static void Play(string audioPath, int deviceNumber, bool loop, Action<string, string> log)
    {
        if (log == null)
        {
            log = delegate (string m, string l) { };
        }

        using (var reader = new AudioFileReader(audioPath))
        using (var waveOut = new WaveOut())
        {
            waveOut.DeviceNumber = deviceNumber;

            log("音频格式: " + reader.WaveFormat.SampleRate + " Hz, "
                + reader.WaveFormat.Channels + " 声道", "INFO");
            log("总时长: " + reader.TotalTime.ToString(@"mm\:ss"), "INFO");

            bool stopped = false;
            int lastReportedSecond = -1;

            waveOut.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                {
                    log("播放中断: " + e.Exception.Message, "ERROR");
                    stopped = true;
                    return;
                }

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

            log("已开始播放到设备 [" + deviceNumber + "]" + (loop ? "（循环模式）" : ""), "INFO");

            while (!stopped)
            {
                int sec = (int)reader.CurrentTime.TotalSeconds;
                if (sec != lastReportedSecond)
                {
                    lastReportedSecond = sec;
                    log("进度: " + reader.CurrentTime.ToString(@"mm\:ss")
                        + " / " + reader.TotalTime.ToString(@"mm\:ss"), "INFO");
                }

                // 不用 Thread.Sleep：Thread 类型已转发到 System.Threading.Thread 程序集，
                // 在 .NET (Core) 下需额外引用，改用 Task.Delay 避免该依赖。
                System.Threading.Tasks.Task.Delay(200).Wait();
            }
        }

        log("播放结束。", "INFO");
    }
}
'@

# ---------------------------------------------------------------- 主流程 ----------------------------------------------------------------

try {
    Write-Log '=== 开始播放音频到虚拟声卡 ==='

    # 步骤 1: 校验音频文件
    $audioPath = Resolve-AudioFile -Path $FilePath
    $sizeKb = [math]::Round((Get-Item -LiteralPath $audioPath).Length / 1KB, 1)
    Write-Log "步骤 1/5 音频文件校验通过: $audioPath ($sizeKb KB)"

    # 步骤 2: 定位 NAudio.dll
    $naudioDll = Resolve-NAudioDll -ExplicitPath $NAudioDllPath
    Write-Log "步骤 2/5 找到 NAudio 程序集: $naudioDll"

    # 步骤 3: 加载 NAudio 程序集及其子程序集。
    # 仅加载 NAudio.dll 不够：WaveOut 类型位于 NAudio.WinForms.dll，运行时解析不到会抛
    # FileNotFoundException，因此需把同目录下的 NAudio.*.dll 一并加载。
    # 用 .NET 取目录名：Split-Path 的 LiteralPath 参数集不含 -Parent，无法组合使用。
    $dllDir = [System.IO.Path]::GetDirectoryName($naudioDll)
    $naudioAssemblies = @($naudioDll) + @(Get-ChildItem -LiteralPath $dllDir -Filter 'NAudio.*.dll' -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName })

    $loadedCount = 0
    foreach ($asm in $naudioAssemblies) {
        try {
            Add-Type -LiteralPath $asm -ErrorAction Stop
            $loadedCount++
        }
        catch {
            Write-Log "程序集加载失败（若未使用其中类型可忽略）: $([System.IO.Path]::GetFileName($asm)) —— $($_.Exception.Message)" -Level WARN
        }
    }

    if ($loadedCount -eq 0) {
        throw 'NAudio 程序集全部加载失败，请确认已安装 Windows 版 .NET 桌面运行时。'
    }

    Write-Log "步骤 3/5 NAudio 程序集加载完成（$loadedCount/$($naudioAssemblies.Count) 个）"

    # 步骤 4: 编译 C# 播放器（已编译则复用，避免重复定义报错）
    if ('AudioPlayer' -as [type]) {
        Write-Log '步骤 4/5 C# 播放器已存在，复用当前会话中的类型'
    }
    else {
        # NAudio 2.x 把类型拆到多个子程序集：WaveOut 位于 NAudio.WinForms.dll、
        # WaveOutCapabilities 位于 NAudio.WinMM.dll、AudioFileReader 位于 NAudio.dll。
        # 因此必须全部引用（含 WinForms），不能按需裁剪。
        $references = @($naudioDll)
        $extra = @(Get-ChildItem -LiteralPath $dllDir -Filter 'NAudio.*.dll' -ErrorAction SilentlyContinue |
            ForEach-Object { $_.FullName })
        $references += $extra

        # NAudio 是 netstandard2.0 程序集，其中的 struct（如 WaveOutCapabilities）其基类
        # ValueType 定义在 netstandard.dll 中，缺引用会报 CS0012，故补上运行时目录下的该程序集。
        $netstandard = Join-Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'netstandard.dll'
        if (Test-Path -LiteralPath $netstandard) {
            $references += $netstandard
        }

        Write-Log "引用程序集 $($references.Count) 个（含 NAudio 子程序集与 netstandard）"

        try {
            # -IgnoreWarnings: 跨版本程序集引用会产生 CS1701 一类警告，Add-Type 默认将其视为错误。
            # -WarningAction SilentlyContinue: 这些警告无害，屏蔽以免淹没自身日志输出。
            Add-Type -TypeDefinition $PlayerSource -ReferencedAssemblies $references `
                -ErrorAction Stop -IgnoreWarnings -WarningAction SilentlyContinue
            Write-Log '步骤 4/5 C# 播放器编译成功'
        }
        catch {
            $detail = ($_.Exception.Message -split "`n" | Select-Object -First 5) -join ' '
            throw "编译 C# 播放器失败: $detail"
        }
    }

    # 步骤 5: 枚举设备并选择目标
    $devices = @([AudioPlayer]::GetDeviceNames())
    Write-Log "步骤 5/5 检测到 $($devices.Count) 个输出设备:"
    for ($i = 0; $i -lt $devices.Count; $i++) {
        Write-Log ("         [{0}] {1}" -f $i, $devices[$i])
    }

    if ($DeviceNumber -lt 0) {
        $autoId = [AudioPlayer]::FindVirtualDevice()
        if ($autoId -ge 0) {
            $DeviceNumber = $autoId
            Write-Log "自动选择虚拟声卡设备 [$DeviceNumber] $($devices[$DeviceNumber])"
        }
        else {
            Write-Log '未检测到虚拟声卡（名称含 VB-Audio / Cable / Virtual），回退到默认设备 [0]' -Level WARN
            $DeviceNumber = 0
        }
    }
    else {
        if ($DeviceNumber -ge $devices.Count) {
            throw "指定的设备编号 $DeviceNumber 超出范围，当前共 $($devices.Count) 个设备（0 - $($devices.Count - 1)）。"
        }
        Write-Log "使用指定设备 [$DeviceNumber] $($devices[$DeviceNumber])"
    }

    # 播放：日志经回调实时输出
    $logCallback = [Action[string, string]] {
        param($message, $level)
        Write-Log $message -Level $level
    }

    if ($Loop) {
        Write-Log '循环模式已开启，按 Ctrl+C 停止播放'
    }
    else {
        Write-Log '开始播放，按 Ctrl+C 可提前停止'
    }

    [AudioPlayer]::Play($audioPath, $DeviceNumber, [bool]$Loop, $logCallback)

    Write-Log '=== 播放完成 ==='
    exit 0
}
catch {
    Write-Log "播放失败: $($_.Exception.Message)" -Level ERROR
    Write-Log "调用堆栈: $($_.ScriptStackTrace)" -Level ERROR
    exit 1
}
