# AGENTS.md

本文件适用于整个仓库。规则冲突时按以下优先级处理：

1. 本文件的安全边界
2. `csproj`、`Directory.Build.targets`、`scripts/*.ps1` 等可执行配置
3. 目标插件的 `Readme.md`
4. `README.md` 等背景说明

## 项目概览与技术栈

- 本仓库是自用的 Flow Launcher 插件集合，使用 C#、.NET 9 和 WPF 构建。
- 所有插件目标框架均为 `net9.0-windows7.0`，启用 `Nullable` 和 `ImplicitUsings`，
  输出类型为 `Library`。
- 常见技术栈包括 WPF、CommunityToolkit.Mvvm、Flow.Launcher.Plugin 5.1.0 和
  xUnit；PasswordManager 额外使用 iNKORE.UI.WPF.Modern。
- `src/` 存放 7 个自包含插件工程，`tests/` 存放 5 个测试工程，`scripts/` 存放构建、
  发布和联调脚本。

```text
FlowLauncher.Plugins/
├── src/FlowLauncher.Plugin.*/
│   ├── Main.cs
│   ├── Services/ ViewModels/ Views/
│   ├── plugin.json
│   └── favicon.ico
├── tests/
├── scripts/
├── Directory.Build.targets
└── FlowLauncher.Plugins.slnx
```

## 安全红线

- **禁止读取、搜索、打印或转述项目密钥与隐私配置**，包括但不限于：
  - `%AppData%\FlowLauncher\Settings\Plugins\**\Settings.json`
  - `dotnet user-secrets` 与 UserSecrets 存储
  - `FolderProfile.pubxml` 中的本机路径和凭据字段
  - PasswordManager 的数据库或密钥材料
- 可以确认配置路径是否存在，但不得读取其内容；只需要知道“配置已存在”即可。
- 不得将密钥写入回答、日志、测试输出、补丁或提交；示例统一使用占位符。
- 修改功能前先读目标插件的 `Readme.md`，再检查该插件的实际实现和配置。

## 架构与职责

- 每个插件必须自包含：业务逻辑、ViewModel、WPF 窗口和查询分发都位于同一个
  `FlowLauncher.Plugin.<功能>` 工程内，不存在跨插件共享层。
- 业务逻辑放在该工程的 `Services/`、`ViewModels/`、`Views/`；`Main.cs` 只负责查询分发和
  Flow Launcher API 适配，不要堆放大段业务代码。
- 解决方案文件是 `FlowLauncher.Plugins.slnx`，不是 `.sln`。
- ViewModel 通过 `Action onChanged` 回调解耦持久化，由调用方决定保存位置。
- Flow Launcher 插件数据位于
  `%AppData%\FlowLauncher\Settings\Plugins\<插件>\`，由 `SaveSettingJsonStorage` 管理。
- `Main.cs` 中的 `_pluginDirectory` 用于插件自带数据，例如 Toolbox 的 `WebView2Cache`。
- Toolbox 的 `wwwroot`（含 jsoneditor）和 `JsonEditorWindow` 都位于 Toolbox 工程内，
  `wwwroot` 内容作为 Content 随输出复制。
- 命名空间使用 `FlowLauncher.Plugin.<Name>`；优先使用主构造函数、`record` 模型；
  .NET 9 下用 `Lock` 保护共享状态。
- WPF UI 更新通过 `Application.Current.Dispatcher` 执行。

## 插件通用契约

- 每个 `Main` 类必须实现 `IAsyncPlugin`，提供 `InitAsync` 和 `QueryAsync`。
- 按功能实现可选接口：`ISettingProvider` 用于设置面板，`IContextMenu` 用于右键菜单，
  `IReloadable` 用于热重载。
- 所有查询使用 `QueryAsync`，并尊重传入的 `CancellationToken`；耗时操作不得阻塞 UI 线程。
- 插件常见文件通常包括 `Main.cs`、`plugin.json`、`Settings.cs` 和
  `*.xaml` / `*.xaml.cs`；业务实现按职责拆到 `Services/`、`ViewModels/`、`Views/`。

标准插件骨架：

```csharp
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

    public Control CreateSettingPanel()
    {
        return new SettingsView(_settings);
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        return Task.FromResult(new List<Result>());
    }
}
```

`plugin.json` 使用以下结构，`ID` 必须是唯一 GUID，`Version` 是唯一版本源：

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

- 使用 `context.API.LoadSettingJsonStorage<Settings>()` 加载配置，
  使用 `context.API.SaveSettingJsonStorage<Settings>()` 保存配置。
- 设置面板通过 WPF `UserControl` 实现并注入 Settings 实例；修改后由调用方决定何时持久化。
- 使用 `Result` 时，`IcoPath` 通常为 `"favicon.ico"`；`Result.Action` 返回 `true`
  关闭 Flow Launcher，返回 `false` 保持窗口打开。
- 适合时使用 `IReadOnlyList<T>` 作为返回类型。AliyunOss 使用 `ResultBuilder` 模式，
  其他插件可直接构造 `Result`。
- PasswordManager 使用 iNKORE `ContentDialog`，必须合并 iNKORE 主题资源。

## 当前插件速览

- **AliyunOss（`oss`）**：OSS 文件下载、上传和浏览，子关键字为 `ossd`、`ossu`、`ossb`。
  使用 `AlibabaCloud.OSS.V2` 和 `IMemoryCache`，业务分层为 Queries、Services、
  ViewModels 和 Views；传输通过插件自己的圆形气泡窗口完成。
- **OpenDirectory（`dir`）**：解析 Windows/UNC 路径并打开文件所在目录或直接打开目录，
  同时支持复制规范化路径；无外部依赖。
- **PasswordManager（`pwd`）**：SQLCipher（AES-256）整库加密和 DPAPI 字段级加密，
  支持模糊搜索、快速/交互式添加、右键复制与删除、随机密码生成和复制后自动清除剪贴板。
- **PortKiller（`kp`）**：通过 `netstat -ano` 查询端口占用，使用 CsWin32 获取进程信息，
  确认后终止进程，历史记录以 JSON 持久化。
- **SpeechToText（`asr`）**：基于 ASR API 实时录音转写，支持识别气泡、历史记录搜索和
  复制结果，使用 NAudio 录音。
- **Toolbox（`tool`）**：工具合集，包含 `guid`、`json`、`fhash`、`dt`、`enc`、`pw`、
  `base`、`ts`、`jwt`、`cron`。JSON 编辑器使用 WebView2 加载本地打包的 jsoneditor，
  用户数据目录为插件目录下的 `WebView2Cache`；设置面板可逐项启停工具。使用普通 WPF
  Window，不使用 iNKORE，也不需要 `AssemblyResolve`。
- **VarName（`vn`）**：通过 OpenAI 兼容接口生成英文候选词，本地转换成 camelCase、
  PascalCase、snake_case、kebab-case 和 UPPER_SNAKE_CASE，并用 SQLite 缓存结果。

## 新增插件

1. 在 `src/` 下创建 `FlowLauncher.Plugin.<Name>/`。
2. 创建 `.csproj`，目标框架设为 `net9.0-windows7.0`，引用 `Flow.Launcher.Plugin`
   和所需依赖。
3. 创建 `plugin.json`、`Main.cs`、`Settings.cs` 及必要的 `Services/`、`ViewModels/`、
   `Views/`。
4. 将工程加入 `FlowLauncher.Plugins.slnx`。
5. 如有设置界面，实现 `ISettingProvider`；优先沿用现有插件和 CommunityToolkit.Mvvm 模式。
6. 如需测试，在 `tests/` 下新增对应测试工程，并直接引用插件工程。

## 常用命令

在仓库根目录执行：

```powershell
# 构建
dotnet build .\FlowLauncher.Plugins.slnx --no-restore
dotnet build .\src\FlowLauncher.Plugin.AliyunOss\FlowLauncher.Plugin.AliyunOss.csproj --no-restore

# 单测
dotnet test .\tests\AliyunOss.UnitTest\AliyunOss.UnitTest.csproj --no-restore
dotnet test .\tests\Toolbox.UnitTest\Toolbox.UnitTest.csproj --no-restore

# 发布 AliyunOss（发布前必须先停止 Flow Launcher）
.\scripts\restart-flowlauncher.ps1 -Action Stop
dotnet publish .\src\FlowLauncher.Plugin.AliyunOss\FlowLauncher.Plugin.AliyunOss.csproj `
  -p:PublishProfile=FolderProfile --no-restore
.\scripts\restart-flowlauncher.ps1 -Action Start
```

- 优先构建和测试受影响的工程，再按需要扩大到解决方案。
- 首次还原或清理 `obj`/`bin` 后不要使用 `--no-restore`。
- `publish-aliyunoss.ps1` 只封装 AliyunOss 的 `dotnet publish`；不会自动停止或启动
  Flow Launcher，使用前仍需执行 `restart-flowlauncher.ps1 -Action Stop`。

## 发布与版本

- `plugin.json` 的 `Version` 是唯一版本源。发布时 `Directory.Build.targets` 将输出目录改为
  `<PublishDir 去尾分隔符>-<Version>\`，并在复制前清理目标目录以避免残留旧 DLL。
- `Properties/PublishProfiles/FolderProfile.pubxml` 是本机配置，已被 gitignore 忽略；
  不要提交，也不要读取其中的本机路径或凭据。
- 改版本只改 `plugin.json`，然后执行“停止 Flow Launcher → 发布 → 启动 Flow Launcher”。
- Flow Launcher 运行时会锁定插件 DLL。复制 `favicon.ico` 失败时，确认是哪个
  COM Surrogate (`dllhost`) 持有文件锁，只结束对应的 `dllhost` 后再重试。

## Git 提交约定

- 提交消息采用 Conventional Commits 结构：`type(scope): 中文简述`。
- `type` 使用 `feat`、`fix`、`refactor`、`perf`、`test`、`docs`、`style`、`build`、`ci` 或 `chore`。
- `scope` 使用受影响的模块或项目名；影响范围不明确时可以省略。
- 首行使用中文、简明描述实际变更，不以句号结尾，建议控制在 50 个字符以内。
- 首行后需要补充背景、实现方式、兼容性或验证信息时，空一行后书写正文。
- 关联问题或破坏性变更时，在正文末尾使用 `Refs: #编号`、`Closes: #编号` 或 `BREAKING CHANGE:`。
- 提交按相关变化的类别或模块组织，不需要每个小变化都生成一条记录，也不得机械地按文件拆分提交。同一类变化或同一模块内的相关改动可以合并为一条提交，在正文中描述具体细节及验证结果。
- 只有目的独立、需要单独审查或回滚的变化才拆分提交；不混入无关格式化、临时文件或不相关的功能。
- 新增公共 API 或数据契约时，应考虑向后兼容。
- 对破坏性变更必须明确说明影响范围和迁移方案。
- 不得因为测试方便而降低认证、授权、数据隔离或秘密保护要求。

可使用 `Auth`、`DynSec`、`Audit`、`UI`、`publish` 或项目名等实际影响范围，例如 `docs: 完善中文项目协作指南`、`fix(Auth): 修复会话过期后的登录跳转`。版本发布相关提交使用适合变更内容的 `build` 或 `chore` 等类型，不另造 `release` 类型。

## Flow Launcher 与 AliyunOss 专项规则

- **进度回调单位不同**：`ShowProgressBoxAsync` / `HttpDownloadAsync` 使用 0–100；
  `UploadFileAsync` 使用 0–1，调用侧必须执行 `p * 100`。
- **`Query.SearchTerms` 不能直接越界访问**：例如 `"ossd <bucket> "` 可能只有 1 项。
  所有 Query 处理器先检查 `SearchTerms.Length`；对象名用 `Skip(1)` 拼接；
  `detail`、`notfound` 等状态关键字认末项。
- **AliyunOss 触发词**：`oss` 为入口、`ossd` 为下载、`ossu` 为上传、`ossb` 为文件浏览器。
- 访问链接与分享链接必须使用 GET 预签名
  (`GeneratePresignedDownloadUrl`)，不要使用 PUT 的 `GeneratePresignedUploadUrl`。
- Exists/列表结果缓存 5 分钟。上传或复制后必须失效对应 cache key，否则可能跳过
  “文件已存在”提示。
- **注册触发词必须幂等**：调用 `AddActionKeyword` 前先检查
  `ActionKeywordAssigned` 和 `CurrentPluginMetadata.ActionKeywords`，否则每次
  `InitAsync` 都会重复注册。
- **密钥字段持久化规则**：AliyunOss Secret、VarName/SpeechToText ApiKey 使用 DPAPI 密文
  （`dpapi:` 前缀，CurrentUser）；AccessKeyId 为明文且可显示。设置 UI 使用密码框、
  不回显已存值，留空表示不修改；加载时由 `TryUpgradeSecrets` 升级旧明文。
  即使实现或测试涉及这些字段，仍不得读取实际 Settings.json。
- 插件项目的 `plugin.json` 和 `favicon.ico` 应设置
  `CopyToOutputDirectory: PreserveNewest`；同时设置
  `AppendTargetFrameworkToOutputPath=false`。

## 排障与已知问题

- 部分 `.cs` 文件带 UTF-8 BOM，编辑时不要无故移除。
- `dotnet restore` 报
  `Value cannot be null. (Parameter 'path1')` 时，通常是当前 shell 中
  `ProgramFiles` / `ProgramFiles(x86)` 为空。先设置变量，再 restore：

  ```powershell
  $env:ProgramFiles = 'C:\Program Files'
  ${env:ProgramFiles(x86)} = 'C:\Program Files (x86)'
  dotnet restore .\FlowLauncher.Plugins.slnx -nodeReuse:false
  ```

  若解决方案级 restore 仍偶发失败，改为单项目 restore。
- 构建或测试出现指向已删除工程的 `MSB9008` 警告时，通常是 `obj/` 缓存陈旧；
  清理受影响工程的 `obj`/`bin` 后重新 restore。
- `tests/SpeechToText.UnitTest` 缺少 `core-capability-1.wav`。该文件被 `.gitignore`
  忽略且从未入库，属于已知历史问题，不是回归。
- `tests/AliyunOss.UnitTest` 的凭据来自 UserSecrets；禁止读取。
  `PutObjectsAsync` 依赖本机固定路径 `D:\Desktop\4.jpg`，文件缺失导致的失败属于预期。
- `tests/VarName.UnitTest` 中带 `Category=Integration` 的用例会真实请求 OpenAI。

## 参考

- 插件行为与使用说明：各插件目录下的 `Readme.md`
- 仓库背景：`README.md`
- 最终以 `csproj`、`scripts/`、`Directory.Build.targets` 等可执行配置为准。
