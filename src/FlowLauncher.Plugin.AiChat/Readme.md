# FlowLauncher.Plugin.AiChat

AI 聊天插件，使用 WebView2 打开 AI 聊天网页，支持多标签页和浮动窗口。

## 功能

- 打开 AI 聊天网页（如豆包等）
- 支持多标签页同时管理
- 支持浮动窗口模式，可置顶悬浮

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `ai` | 打开 AI 聊天窗口 |

按 Enter 打开独立的聊天窗口，可在设置中配置默认网页地址。

## 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，打开聊天窗口 |
| `AiChatWindow.xaml` | 多标签页聊天窗口 UI |
| `FloatingWindow.xaml` | 浮动窗口 UI |
| `Settings.cs` | 配置模型（网页地址等） |
| `SettingsControl.xaml` | 设置面板 |

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 框架 |
| `iNKORE.UI.WPF` | 1.2.8 | WPF 组件 |
| `iNKORE.UI.WPF.Modern` | 0.10.2.1 | 现代 UI 组件 |
| `Microsoft.Web.WebView2` | 1.0.2903.40 | 网页渲染引擎 |

## 构建

```bash
dotnet build src/FlowLauncher.Plugin.AiChat/FlowLauncher.Plugin.AiChat.csproj
```
