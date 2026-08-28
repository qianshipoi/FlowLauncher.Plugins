# Flow Launcher Plugins 项目指南

## 项目概述

这是一个 Flow Launcher 插件集合，使用 C# / .NET 9 / WPF 构建，包含 8 个独立插件和 1 个演示应用。

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
├── FlowLauncher.Plugins.slnx          # 解决方案文件
├── DemoApp/                            # AiChat 独立演示应用 (WinExe)
├── FlowLauncher.Plugin.AiChat/         # AI 聊天插件
├── FlowLauncher.Plugin.AliyunOss/      # 阿里云 OSS 插件
├── FlowLauncher.Plugin.OpenDirectory/  # 打开目录插件
├── FlowLauncher.Plugin.PasswordManager/# 密码管理器插件
├── FlowLauncher.Plugin.PortKiller/     # 端口占用查询/终止插件
├── FlowLauncher.Plugin.Translator/     # AI 翻译插件
├── FlowLauncher.Plugin.VarName/        # 变量命名插件
├── FlowLauncher.Plugin.Toolbox/        # 工具合集插件（GUID/JSON/哈希/时间戳）
└── tests/
    ├── AliyunOss.UnitTest/             # 阿里云 OSS 单元测试
    ├── PasswordManager.UnitTest/       # 密码管理器单元测试
    ├── VarName.UnitTest/               # 变量命名单元测试
    └── Toolbox.UnitTest/               # 工具合集单元测试
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

### PasswordManager（触发关键字: `pwd`）

- 本地加密存储和管理账号密码
- SQLCipher（AES-256）整库加密 + DPAPI 字段级加密，双重保护
- 模糊搜索（标题、用户名、标签）
- 支持 `pwd add 标题 | 用户名 | 密码` 快速添加和交互式添加
- 右键菜单：复制用户名、删除条目
- 复制后自动清除剪贴板（可配置）
- 依赖: Microsoft.Data.Sqlite, SQLitePCLRaw.bundle_e_sqlcipher

### VarName（触发关键字: `vn`）

- 变量命名助手，将中文翻译为英文变量名
- 基于 OpenAI API 生成多种命名风格（camelCase、PascalCase、snake_case 等）
- SQLite 缓存历史翻译结果
- 依赖: OpenAI, Microsoft.Data.Sqlite

### OpenDirectory（触发关键字: `dir`）

- 快速打开常用目录（桌面、文档、下载等）
- 轻量级插件，无外部依赖

### Toolbox（触发关键字: `tool`）

- 工具合集，子命令: `guid`（GUID v1/v3/v4/v5/v7 生成）、`json`（WebView2 + jsoneditor 编辑器窗口）、`fhash`（单遍流式计算 MD5/SHA1/SHA256/SHA384/SHA512/CRC32）、`dt`（时间与时间戳）
- JSON 编辑器：`wwwroot/` 本地打包 jsoneditor 10.4.3，`SetVirtualHostNameToFolderMapping` 虚拟域名加载，`WebMessageReceived`/`PostWebMessageAsJson` 宿主↔页面通信，UserDataFolder 为插件目录下 `WebView2Cache`，窗口单实例复用并在 `Closed` 时 Dispose
- 不使用 iNKORE 组件（普通 WPF Window），无 AssemblyResolve 需求
- 依赖: Microsoft.Web.WebView2

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
dotnet test tests/PasswordManager.UnitTest/PasswordManager.UnitTest.csproj
dotnet test tests/VarName.UnitTest/VarName.UnitTest.csproj
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
- 发布配置：每个插件 `Properties/PublishProfiles/FolderProfile.pubxml` 为**本机配置，被 `.gitignore` 忽略、不入库**（其中硬编码了本机的 Flow Launcher 插件目录，不同电脑路径不同需自行创建）；`scripts/publish-*.ps1` 调用 `dotnet publish -p:PublishProfile=FolderProfile` 发布。根目录 `Directory.Build.targets` 负责把 plugin.json 的 `Version` 拼进目录名（最终目录 = `<插件名>-<Version>\`，平铺不嵌套）并在发布前清空目标目录；Flow Launcher 运行中会锁定已加载插件的 DLL，更新前需先退出
- **更新插件前必须先读取该插件目录下的 `Readme.md`**，了解插件的功能、架构、数据模型和安全设计，避免破坏现有逻辑
