# FlowLauncher.Plugin.OpenDirectory

快速打开文件或文件夹所在目录的轻量级插件。

## 功能

- 输入绝对路径，打开文件所在目录或直接打开目录
- 支持 Windows 路径（`C:\...`）和 UNC 路径（`\\server\share`）
- 自动规范化路径（正反斜杠、末尾分隔符）
- 复制规范化路径到剪贴板

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `dir` | 显示使用提示 |
| `dir C:\Users\test\file.txt` | 打开文件所在目录并选中文件 |
| `dir C:\Users\test\folder` | 直接打开目录 |
| `dir \\server\share\folder` | 打开 UNC 网络路径 |

### 操作流程

输入路径后显示多个结果：
1. **打开目录** — 在资源管理器中打开（文件路径会同时选中文件）
2. **复制路径** — 复制规范化后的路径到剪贴板

## 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，路径解析和目录打开逻辑 |

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |

无外部依赖，最轻量的插件。

## 构建

```bash
dotnet build src/FlowLauncher.Plugin.OpenDirectory/FlowLauncher.Plugin.OpenDirectory.csproj
```
