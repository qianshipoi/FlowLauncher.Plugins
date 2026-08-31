# scripts/restart-flowlauncher.ps1
# 停止 / 启动 Flow Launcher，用于解除插件 DLL 被宿主锁定导致的发布失败。
#
# 用法:
#   .\restart-flowlauncher.ps1                          # 重启（默认）
#   .\restart-flowlauncher.ps1 -Action Stop             # 仅停止
#   .\restart-flowlauncher.ps1 -Action Start            # 仅启动
#   .\restart-flowlauncher.ps1 -Action Stop -TimeoutSeconds 30
#
# 注意: 该脚本设计为被发布脚本用 & 调用，请勿使用 exit（会终止调用方脚本），
#       错误统一通过 throw 抛出，交由调用方处理。

param(
    [ValidateSet('Stop', 'Start', 'Restart')]
    [string]$Action = 'Restart',

    # 等待进程退出的最长时间（秒）
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = 'Stop'

$processName = 'Flow.Launcher'

function Get-FlowLauncherProcess {
    Get-Process -Name $processName -ErrorAction SilentlyContinue
}

function Get-FlowLauncherExePath {
    # 1) 运行中进程的路径最准确，优先使用
    $running = Get-FlowLauncherProcess | Select-Object -First 1
    if ($running -and $running.Path) {
        return $running.Path
    }

    # 2) Squirrel 安装目录 %LOCALAPPDATA%\FlowLauncher\app-<版本>\Flow.Launcher.exe，取版本号最大的
    $installRoot = Join-Path $env:LOCALAPPDATA 'FlowLauncher'
    if (Test-Path $installRoot) {
        $latest = Get-ChildItem -Path $installRoot -Directory -Filter 'app-*' -ErrorAction SilentlyContinue |
            Sort-Object {
                $v = $_.Name -replace '^app-', ''
                try { [version]$v } catch { [version]'0.0.0' }
            } |
            Select-Object -Last 1

        if ($latest) {
            $exe = Join-Path $latest.FullName "$processName.exe"
            if (Test-Path $exe) {
                return $exe
            }
        }
    }

    # 3) 兜底：解析开始菜单快捷方式
    $lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Flow Launcher\Flow Launcher.lnk'
    if (Test-Path $lnk) {
        try {
            $shell = New-Object -ComObject WScript.Shell
            $target = $shell.CreateShortcut($lnk).TargetPath
            if ($target -and (Test-Path $target)) {
                return $target
            }
        }
        catch {
            # 快捷方式解析失败不影响后续报错，继续返回 $null
        }
    }

    return $null
}

function Stop-FlowLauncher {
    $procs = @(Get-FlowLauncherProcess)
    if ($procs.Count -eq 0) {
        Write-Host 'Flow Launcher 未运行，无需停止。'
        return
    }

    Write-Host "正在停止 Flow Launcher (PID: $($procs.Id -join ', '))..."
    $procs | Stop-Process -Force

    # 轮询等待进程真正退出，确保插件 DLL 的文件锁已释放
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-FlowLauncherProcess)) {
            Write-Host 'Flow Launcher 已停止。'
            return
        }
        Start-Sleep -Milliseconds 200
    }

    $stillRunning = @(Get-FlowLauncherProcess)
    throw "等待 Flow Launcher 退出超时（${TimeoutSeconds}s），PID: $($stillRunning.Id -join ', ')。插件 DLL 可能仍被占用。"
}

function Start-FlowLauncher {
    if (Get-FlowLauncherProcess) {
        Write-Host 'Flow Launcher 已在运行，无需启动。'
        return
    }

    $exe = Get-FlowLauncherExePath
    if (-not $exe) {
        throw "未找到 $processName.exe，请确认 Flow Launcher 已安装，或手动启动。"
    }

    Write-Host "正在启动 Flow Launcher: $exe"
    Start-Process -FilePath $exe
    Write-Host 'Flow Launcher 已启动。'
}

switch ($Action) {
    'Stop'    { Stop-FlowLauncher }
    'Start'   { Start-FlowLauncher }
    'Restart' { Stop-FlowLauncher; Start-FlowLauncher }
}
