# 为各插件生成基于文字图标的 .ico 文件
Add-Type -AssemblyName System.Drawing

function New-TextIcon {
    param(
        [string]$Text,
        [string]$OutputPath,
        [string]$BgColor = "#1E1E2E",
        [string]$FgColor = "#FFFFFF",
        [int]$Size = 256
    )

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 背景
    $bg = [System.Drawing.ColorTranslator]::FromHtml($BgColor)
    $g.Clear($bg)

    # 绘制文字
    $fg = [System.Drawing.ColorTranslator]::FromHtml($FgColor)
    $fontSize = $Size * 0.55
    $font = New-Object System.Drawing.Font("Segoe UI Emoji", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush($fg)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
    $g.DrawString($Text, $font, $brush, $rect, $sf)

    # 保存为 .ico
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    $fs = [System.IO.File]::Create($OutputPath)
    $icon.Save($fs)
    $fs.Close()
    $icon.Dispose()
    $bmp.Dispose()
    $g.Dispose()

    Write-Host "  已生成: $OutputPath" -ForegroundColor Green
}

$root = Split-Path -Parent $PSScriptRoot

$icons = @(
    @{ Plugin = "FlowLauncher.Plugin.AiChat";       Text = [char]0xD83E + [char]0xDD16; Bg = "#6C5CE7" },  # 🤖
    @{ Plugin = "FlowLauncher.Plugin.AliyunOss";     Text = [char]0x2601;                Bg = "#FF6B35" },  # ☁
    @{ Plugin = "FlowLauncher.Plugin.PortKiller";    Text = [char]0xD83D + [char]0xDD25; Bg = "#E74C3C" },  # 🔥
    @{ Plugin = "FlowLauncher.Plugin.Translator";    Text = [char]0xD83C + [char]0xDF0D; Bg = "#2ECC71" },  # 🌍
    @{ Plugin = "FlowLauncher.Plugin.OpenDirectory"; Text = [char]0xD83D + [char]0xDCC2; Bg = "#3498DB" }   # 📂
)

# 用 ASCII 字符代替 emoji（避免字体问题）
$textMap = @{
    "FlowLauncher.Plugin.AiChat"       = "AI"
    "FlowLauncher.Plugin.AliyunOss"    = [char]0x2601   # ☁
    "FlowLauncher.Plugin.PortKiller"   = [char]0x2716   # ✖
    "FlowLauncher.Plugin.Translator"   = [char]0x00A4   # ¤
    "FlowLauncher.Plugin.OpenDirectory"= [char]0x25B6   # ▶
    "FlowLauncher.Plugin.VarName"      = "Vn"
}

$colorMap = @{
    "FlowLauncher.Plugin.AiChat"       = "#6C5CE7"
    "FlowLauncher.Plugin.AliyunOss"    = "#FF6B35"
    "FlowLauncher.Plugin.PortKiller"   = "#E74C3C"
    "FlowLauncher.Plugin.Translator"   = "#2ECC71"
    "FlowLauncher.Plugin.OpenDirectory"= "#3498DB"
    "FlowLauncher.Plugin.VarName"      = "#F39C12"
}

Write-Host "正在为各插件生成图标..." -ForegroundColor Cyan

foreach ($item in $textMap.GetEnumerator()) {
    $outDir = Join-Path $root "$($item.Key)"
    $outPath = Join-Path $outDir "favicon.ico"
    $bgColor = $colorMap[$item.Key]
    New-TextIcon -Text $item.Value -OutputPath $outPath -BgColor $bgColor
}

Write-Host "`n全部完成" -ForegroundColor Green
