# FlowLauncher.Plugin.PortKiller

端口占用查询和进程终止插件。

## 功能

- 查询指定端口的占用进程（PID、协议、状态、端点）
- 显示进程文件图标
- 确认后终止占用进程
- 查询历史记录（JSON 持久化）

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `kp` | 显示历史查询记录 |
| `kp 80` | 查询占用 80 端口的进程 |

### 操作流程

1. 输入端口号（1-65535），按 Enter 查询
2. 显示占用该端口的所有进程（进程名、PID、协议、端点、状态）
3. 选择进程，确认后终止
4. 查询记录自动保存，下次输入 `kp` 可快速重新查询

## 架构

```
┌────────────────────────┐
│       Main.cs           │
│   (IAsyncPlugin)        │
├─────────┬──────────────┤
│ProcessHelpers│PortHistory│
│ (netstat解析) │ (JSON持久化)│
└─────────┴──────────────┘
```

### 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，查询和终止流程 |
| `ProcessHelpers.cs` | 通过 `netstat -ano` 解析端口占用，CsWin32 获取进程路径 |
| `PortHistory.cs` | 查询历史 JSON 文件管理 |

### 技术细节

- 通过 `netstat -ano` 命令解析端口信息
- 使用 `Microsoft.Windows.CsWin32` P/Invoke 获取进程文件路径和图标
- 历史记录以 JSON 文件持久化在插件目录

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 框架 |
| `Microsoft.Windows.CsWin32` | 0.3.264 | Windows API P/Invoke |
| `iNKORE.UI.WPF.Modern` | 0.10.2.1 | UI 组件 |

## 构建

```bash
dotnet build src/FlowLauncher.Plugin.PortKiller/FlowLauncher.Plugin.PortKiller.csproj
```
