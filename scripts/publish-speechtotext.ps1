# scripts/publish-speechtotext.ps1
# 发布语音识别插件到 Flow Launcher 插件目录（增量覆盖，不清空目录）。
#
# 用法:
#   .\publish-speechtotext.ps1          # 直接发布（Flow Launcher 未占用插件 DLL 时使用）
#   .\publish-speechtotext.ps1 -Force   # 自动停止 Flow Launcher，发布完成后再启动
#
# 为什么不使用 -p:PublishProfile=FolderProfile:
#   该 profile 会命中 Directory.Build.targets 里的 FlowLauncherCleanPublishDir，
#   发布前 RemoveDir 整个插件目录，而插件会在自身目录下写持久化数据
#   （data\ 转写历史、recordings\ 录音等），清空会删除用户数据。
#   这里自行拼出发布目录并直接传 PublishDir，只覆盖同名文件，保留其它内容。
#
# Flow Launcher 运行时会锁定插件 DLL，覆盖发布会因文件占用而失败；
# 加 -Force 会在发布前调用 restart-flowlauncher.ps1 停用宿主，发布后（无论成败）重新拉起。

param(
    # 强制解除 Flow Launcher 对插件 DLL 的占用：发布前停止，发布后启动
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot '../src/FlowLauncher.Plugin.SpeechToText'
$project = Join-Path $projectDir 'FlowLauncher.Plugin.SpeechToText.csproj'
$restartScript = Join-Path $PSScriptRoot 'restart-flowlauncher.ps1'

# 版本号取自 plugin.json，与 Directory.Build.targets 的命名规则保持一致：<插件名>-<版本>
$pluginJsonPath = Join-Path $projectDir 'plugin.json'
if (-not (Test-Path -LiteralPath $pluginJsonPath)) {
    throw "未找到 plugin.json: $pluginJsonPath"
}

$version = (Get-Content -LiteralPath $pluginJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json).Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = '0.0.0'
}

# 目录名去掉 FlowLauncher.Plugin. 前缀，与其余插件（OSS / VarName / PortKiller …）保持一致
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
$pluginName = $projectName -replace '^FlowLauncher\.Plugin\.', ''

$pluginsRoot = Join-Path $env:APPDATA 'FlowLauncher\Plugins'
# MSBuild 的 PublishDir 需以目录分隔符结尾，否则会被当作文件路径
$publishDir = (Join-Path $pluginsRoot "$pluginName-$version").TrimEnd('\', '/') + '\'

Write-Host "发布目录: $publishDir"

if ($Force) {
    & $restartScript -Action Stop
}

try {
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained false `
        -p:PublishDir="$publishDir"

    if ($LASTEXITCODE -ne 0) {
        throw "发布失败，dotnet publish 退出码: $LASTEXITCODE"
    }
}
finally {
    if ($Force) {
        & $restartScript -Action Start
    }
}
