# FlowLauncher.Plugin.Translator

AI 翻译插件，基于 OpenAI API 实现中英互译。

## 功能

- 输入中文翻译为英文，输入英文翻译为中文（自动检测语言）
- 翻译结果自动复制到剪贴板
- 翻译历史记录管理（SQLite 持久化）
- 相同文本走缓存，不重复调用 API
- 右键菜单删除历史记录

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `trans` | 显示最近 5 条翻译历史 |
| `trans 你好世界` | 翻译文本，结果自动复制到剪贴板 |

### 操作流程

1. 输入待翻译文本，按 Enter
2. 如果历史中有缓存，直接显示结果
3. 否则调用 OpenAI API 翻译，结果缓存并显示
4. Enter 复制翻译结果到剪贴板

## 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，查询、右键菜单、设置面板 |
| `ITranslator.cs` | 翻译接口 |
| `HistoryManager.cs` | 翻译历史 JSON 文件管理 |
| `Settings.cs` | 配置模型（API Key、API URL、模型名称） |
| `SettingsViewModel.cs` | 设置面板 ViewModel |
| `SettingsView.xaml` | WPF 设置面板 |

## 配置项

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `ModelName` | `gpt-3.5-turbo` | OpenAI 模型名称 |
| `ApiUrl` | `https://api.openai.com/v1/chat/completions` | API 地址 |
| `ApiKey` | `""` | API 密钥 |

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 框架 |
| `OpenAI` | 2.8.0 | AI 翻译 |
| `iNKORE.UI.WPF.Modern` | 0.10.2.1 | UI 组件 |

## 构建

```bash
dotnet build src/FlowLauncher.Plugin.Translator/FlowLauncher.Plugin.Translator.csproj
```
