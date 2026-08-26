# Flow Launcher Plugins 项目指南

## 项目概述

这是一个 Flow Launcher 插件集合，使用 C# / .NET 9 / WPF 构建，包含 4 个独立插件和 1 个演示应用。

## 技术栈

- **运行时**: .NET 9.0 (net9.0-windows7.0)
- **UI 框架**: WPF
- **UI 组件库**: iNKORE.UI.WPF / iNKORE.UI.WPF.Modern
- **MVVM**: CommunityToolkit.Mvvm
- **插件 SDK**: Flow.Launcher.Plugin (v5.1.0)
- **解决方案格式**: .slnx (XML 格式)
- **全局启用**: Nullable, ImplicitUsings

## 项目结构

```
FlowLauncher.Plugins/
├── FlowLauncher.Plugins.slnx        # 解决方案文件
├── DemoApp/                          # AiChat 独立演示应用 (WinExe)
├── FlowLauncher.Plugin.AiChat/       # AI 聊天插件
├── FlowLauncher.Plugin.AliyunOss/    # 阿里云 OSS 插件
├── FlowLauncher.Plugin.PortKiller/   # 端口占用查询/终止插件
├── FlowLauncher.Plugin.Translator/   # AI 翻译插件
└── tests/
    └── AliyunOss.UnitTest/           # 阿里云 OSS 单元测试
```

## 插件架构模式

每个插件遵循相同的目录结构和约定：

### 核心文件

| 文件 | 用途 |
|------|------|
| `Main.cs` | 插件入口，实现 `IAsyncPlugin` 接口 |
| `plugin.json` | 插件元数据（ID、关键字、名称、描述） |
| `Settings.cs` | 配置模型类 |
| `*.xaml` + `*.xaml.cs` | WPF 设置面板 / UI 窗口 |

### 插件接口实现

所有插件的 `Main` 类必须：
- 实现 `IAsyncPlugin`（必需，提供 `InitAsync` 和 `QueryAsync`）
- 实现 `ISettingProvider`（可选，提供设置面板 `CreateSettingPanel`）
- 实现 `IContextMenu`（可选，提供右键菜单 `LoadContextMenus`）
- 实现 `IReloadable`（可选，支持热重载）

```csharp
// 标准插件骨架
public class Main : IAsyncPlugin, ISettingProvider
{
    private PluginInitContext _context = default!;
    private Settings _settings = default!;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        return Task.CompletedTask;
    }

    public Control CreateSettingPanel() => new SettingsView(_settings);

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        // 返回搜索结果列表
    }
}
```

### plugin.json 格式

```json
{
    "ID": "唯一 GUID",
    "ActionKeyword": "触发关键字",
    "Name": "插件名称",
    "Description": "描述",
    "Author": "作者名",
    "Version": "1.0.0",
    "Language": "csharp",
    "IcoPath": "favicon.ico",
    "ExecuteFileName": "FlowLauncher.Plugin.{Name}.dll"
}
```

### Settings 模式

- 使用 `context.API.LoadSettingJsonStorage<Settings>()` 加载配置
- 使用 `context.API.SaveSettingJsonStorage<Settings>()` 保存配置
- 设置面板通过 WPF UserControl 实现，注入 Settings 实例

## 各插件详情

### AiChat（触发关键字: `ai`）

- 打开 AI 聊天网页（如豆包），支持多标签页
- 使用 WebView2 渲染网页
- 支持浮动窗口模式
- 依赖: Microsoft.Web.WebView2

### AliyunOss（触发关键字: `oss`）

- 阿里云 OSS 文件上传/下载
- 子关键字: `ossd`（下载）、`ossu`（上传）
- 使用 `AlibabaCloud.OSS.V2` SDK
- 使用 `IMemoryCache` 缓存 bucket 列表
- MVVM 架构: ViewModel + Views + Services + Queries
- 依赖: AlibabaCloud.OSS.V2, Microsoft.Extensions.Caching.Memory

### PortKiller（触发关键字: `kp`）

- 查询端口占用进程并终止
- 通过 `netstat -ano` 解析端口信息
- 使用 CsWin32 P/Invoke 获取进程文件路径
- JSON 文件持久化查询历史
- 依赖: Microsoft.Windows.CsWin32

### Translator（触发关键字: `trans`）

- AI 翻译器，基于 OpenAI API
- 可配置模型、API 地址和密钥
- 翻译历史记录管理（JSON 持久化）
- 支持右键菜单删除历史
- 依赖: OpenAI

## 编码规范

- **命名空间**: `FlowLauncher.Plugin.{Name}`
- **异步优先**: 所有查询使用 `QueryAsync`，支持 `CancellationToken`
- **主构造函数**: 服务类使用 C# 12 主构造函数语法
- **记录类型**: 数据模型使用 `record`（如 `TranslationRecord`）
- **不可变集合**: 使用 `IReadOnlyList<T>` 作为返回类型
- **线程安全**: 使用 `Lock` 对象（.NET 9）保护共享状态
- **WPF UI 更新**: 通过 `Application.Current.Dispatcher.Invoke` 在主线程执行
- **结果构建**: 使用 `ResultBuilder` 模式（AliyunOss）或直接构造 `Result` 对象

## 构建命令

```bash
# 构建整个解决方案
dotnet build FlowLauncher.Plugins.slnx

# 构建单个插件
dotnet build FlowLauncher.Plugin.AiChat/FlowLauncher.Plugin.AiChat.csproj

# 运行测试
dotnet test tests/AliyunOss.UnitTest/AliyunOss.UnitTest.csproj
```

## 添加新插件的步骤

1. 创建目录 `FlowLauncher.Plugin.{Name}/`
2. 添加 `.csproj` 文件，引用 `Flow.Launcher.Plugin` NuGet 包
3. 创建 `plugin.json` 定义元数据
4. 创建 `Main.cs` 实现 `IAsyncPlugin`
5. 创建 `Settings.cs` 定义配置（如需要）
6. 在 `.slnx` 中添加项目引用
7. 如有 UI 需求，使用 iNKORE.UI.WPF.Modern 组件

## 注意事项

- 所有插件输出为 Library（DLL），不是独立可执行文件
- `plugin.json` 需设置 `CopyToOutputDirectory: PreserveNewest`
- `AppendTargetFrameworkToOutputPath` 设为 `false`，输出路径不含 TFM 子目录
- 使用 `Result` 对象时，`IcoPath` 通常设为 `"favicon.ico"`
- `Result.Action` 回调返回 `true` 关闭 Flow Launcher，`false` 保持打开
