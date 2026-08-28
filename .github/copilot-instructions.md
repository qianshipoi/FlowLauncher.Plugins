# Flow Launcher Plugins 项目指南

## 项目概述

这是一个 Flow Launcher 插件集合 + 独立 WPF 应用（DevToolkit），使用 C# / .NET 9 / WPF 构建。

## 架构：Core 与宿主

**核心原则**：功能逻辑放在 `src/Core/DevToolkit.<功能>` 类库（无 Flow.Launcher 依赖，除密码管理等 Windows 专属能力外为 net9.0），Flow Launcher 插件是薄适配壳（查询分发 + FL API：剪贴板/通知/进度框），独立 App `src/DevToolkit/`（iNKORE.UI.WPF.Modern NavigationView 壳）直接复用同一批 Core 服务与 ViewModel。

- 修改功能逻辑时改 Core 工程，插件与 App 同时受益；**不要**把逻辑写回插件 Main.cs
- 跨宿主的 ViewModel 通过回调解耦持久化（`Action onChanged` 照 AiChat 范本）与事件（`ReQueryRequested`）；需要 UI 的视图放 `src/Core/DevToolkit.Core.Wpf`（宿主无关 WPF 窗口：AiChat 三窗口、JsonEditorWindow、密码管理 ManagerWindow/对话框），宿主需在 App.xaml 合并 iNKORE `ThemeResources` + `XamlControlsResources`
- App 数据目录 `%AppData%\DevToolkit\<功能>\`，与插件目录数据相互隔离；App 用 `RollForward=LatestMajor` 以运行在更高版本 .NET 桌面运行时上
- wwwroot（jsoneditor）单源在 DevToolkit.Core.Wpf，插件与 App 用 Content Link 复制

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
├── Core/                              # 与宿主无关的功能核心库
│   ├── DevToolkit.Toolbox/            # 工具合集服务 + 设置/VM
│   ├── DevToolkit.PasswordManager/    # 密码管理（SQLCipher+DPAPI）模型/服务/VM
│   ├── DevToolkit.Translator/         # 翻译服务/历史/设置/VM
│   ├── DevToolkit.VarName/            # 变量名服务/缓存/命名转换器/VM
│   ├── DevToolkit.AliyunOss/          # OSS 服务/下载服务/设置/VM
│   ├── DevToolkit.PortKiller/         # netstat 解析 + 端口历史
│   ├── DevToolkit.OpenDirectory/      # 路径规范化/打开
│   ├── DevToolkit.AiChat/             # AI 对话设置
│   └── DevToolkit.Core.Wpf/           # 宿主无关 WPF 窗口 + wwwroot
├── DevToolkit/                        # 独立 WPF App（NavigationView 壳，全部功能页面）
├── DemoApp/                            # AiChat 独立演示应用 (WinExe)
├── FlowLauncher.Plugin.AiChat/         # AI 聊天插件（薄壳）
├── FlowLauncher.Plugin.AliyunOss/      # 阿里云 OSS 插件（薄壳）
├── FlowLauncher.Plugin.OpenDirectory/  # 打开目录插件（薄壳）
├── FlowLauncher.Plugin.PasswordManager/# 密码管理器插件（薄壳）
├── FlowLauncher.Plugin.PortKiller/     # 端口占用查询/终止插件（薄壳）
├── FlowLauncher.Plugin.Translator/     # AI 翻译插件（薄壳）
├── FlowLauncher.Plugin.VarName/        # 变量命名插件（薄壳）
├── FlowLauncher.Plugin.Toolbox/        # 工具合集插件（薄壳）
└── tests/
    ├── AliyunOss.UnitTest/             # 测 DevToolkit.AliyunOss
    ├── PasswordManager.UnitTest/       # 测 DevToolkit.PasswordManager
    ├── VarName.UnitTest/               # 测 DevToolkit.VarName
    └── Toolbox.UnitTest/               # 测 DevToolkit.Toolbox
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

- 工具合集，子命令: `guid`（GUID v1/v3/v4/v5/v7 生成）、`json`（WebView2 + jsoneditor 编辑器窗口）、`fhash`（单遍流式计算 MD5/SHA1/SHA256/SHA384/SHA512/CRC32）、`dt`（时间与时间戳）、`enc`（URL/Base64/HTML/Hex 编解码）、`pw`（随机密码/密钥）、`base`（进制与字符码点互查）、`ts`（时间戳转日期）、`jwt`（解码 Header/Payload/exp，Payload 可右键进 JSON 编辑器）、`cron`（中文描述 + 未来 5 次执行，Cronos 包）
- JSON 编辑器：`wwwroot/` 本地打包 jsoneditor 10.4.3，`SetVirtualHostNameToFolderMapping` 虚拟域名加载，`WebMessageReceived`/`PostWebMessageAsJson` 宿主↔页面通信，UserDataFolder 为插件目录下 `WebView2Cache`，窗口单实例复用并在 `Closed` 时 Dispose
- 设置面板（ISettingProvider）：`Settings.cs` 各子命令独立启用开关（默认全开）+ `IsEnabled` 映射；`SettingsViewModel` 复选框变更即写回 `SaveSettingJsonStorage`；禁用命令查询返回提示行，帮助列表只显示启用项，`json` 开关联动 jwt 的"进 JSON 编辑器"菜单
- 不使用 iNKORE 组件（普通 WPF Window），无 AssemblyResolve 需求
- 依赖: Microsoft.Web.WebView2, Cronos

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
