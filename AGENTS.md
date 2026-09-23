# AGENTS.md

## 硬性约束

- **禁止读取项目密钥与隐私配置**，包括但不限于：
  - `%AppData%\FlowLauncher\Settings\Plugins\**\Settings.json`（含 OSS AccessKey 明文）
  - `dotnet user-secrets` 内容 / UserSecrets 存储
  - `FolderProfile.pubxml` 中的本机路径与任何凭据字段
  - PasswordManager 的数据库/密钥材料
- 只需知道「配置存在即可」；不要打印、转述、写入日志或提交任何密钥值。
- 改功能前先读对应插件的 `Readme.md`（各插件目录下）。

## 架构（必读）

- **逻辑放 `src/Core/DevToolkit.<功能>`**（尽量 net9.0 无关宿主），Flow Launcher 插件只是薄壳（查询分发 + FL API：剪贴板/通知/进度框）。**不要**把业务写回插件 `Main.cs`。
- `src/DevToolkit/` 是独立 WPF 宿主（NavigationView 插件化：`IDevToolkitPlugin` / `IHostServices`），与插件共享 Core。
- 跨宿主 ViewModel 用 `Action onChanged` 回调解耦持久化；共享 WPF 窗口放 `src/DevToolkit.Core.Wpf`。
- 解决方案是 **`.slnx`**，不是 `.sln`。
- 插件数据：`%AppData%\DevToolkit\<功能>\`；App 与插件目录数据相互隔离。
- wwwroot（jsoneditor）单源在 `DevToolkit.Core.Wpf`，插件与 App 用 Content Link 复制。

## 命令

```powershell
# 构建（本机若 NuGet restore 报 "Value cannot be null. (Parameter 'path1')"，用 --no-restore）
dotnet build FlowLauncher.Plugins.slnx --no-restore
dotnet build src/FlowLauncher.Plugin.AliyunOss/FlowLauncher.Plugin.AliyunOss.csproj --no-restore

# 单测
dotnet test tests/AliyunOss.UnitTest/AliyunOss.UnitTest.csproj --no-restore
dotnet test tests/Toolbox.UnitTest/Toolbox.UnitTest.csproj --no-restore

# 发布插件（必须先停 Flow，否则 DLL 被占用）
.\scripts\restart-flowlauncher.ps1 -Action Stop
dotnet publish src/FlowLauncher.Plugin.AliyunOss/FlowLauncher.Plugin.AliyunOss.csproj -p:PublishProfile=FolderProfile --no-restore
.\scripts\restart-flowlauncher.ps1 -Action Start
# 等价一键：.\scripts\publish-aliyunoss.ps1（同样建议先 Stop）
```

## 发布约定

- `plugin.json` 的 `Version` 是唯一版本源；根目录 `Directory.Build.targets` 把发布目录改成 `<PublishDir去尾\>-<Version>\`（平铺）。
- `Properties/PublishProfiles/FolderProfile.pubxml` **本机配置、gitignore 忽略**；`PublishDir` 指向 Flow Launcher `Plugins` 目录。
- Flow 运行中会锁插件 DLL；`favicon.ico` 偶发被 **COM Surrogate (dllhost)** 锁住，复制失败时结束对应 dllhost 再拷。
- 改版本只需改 `plugin.json`，再走「停 → 发 → 启」。

## 易踩坑（实测）

- **Flow.Launcher 进度回调是 0–100**（`ShowProgressBoxAsync` / `HttpDownloadAsync`），不是 0–1。Core 的 `UploadFileAsync` 上报 0–1，调用侧要 `p * 100`。
- **`Query.SearchTerms` 很脆**：`"ossd <bucket> "` 常只有 1 项，直接取 `[1]` 会 `IndexOutOfRangeException`。所有 Query 处理器先判 `SearchTerms.Length`；对象名应 `Skip(1)` 拼接，状态关键字（`detail`/`notfound`）认末项。
- **AliyunOss 关键字**：`oss` 入口、`ossd` 下载、`ossu` 上传、`ossb` 文件浏览器。访问/分享链接必须用 **GET 预签名**（`GeneratePresignedDownloadUrl`），不要用 PUT 的 `GeneratePresignedUploadUrl`。
- **Exists/列表结果有 5 分钟缓存**；上传/复制后要失效对应 cache key，否则会跳过「文件已存在」提示。
- **密钥类字段**（AliyunOss Secret / VarName & SpeechToText ApiKey）在 Settings.json 中为 DPAPI 密文（`dpapi:` 前缀，CurrentUser）；**AccessKeyId 明文、可显示**。设置 UI 用密码框且**不回显**已存值，留空=不修改。加载时 `TryUpgradeSecrets` 自动升级旧明文。仍**禁止读取**配置文件。
- **AddActionKeyword 必须先查 `ActionKeywordAssigned` 和 `CurrentPluginMetadata.ActionKeywords`**，否则每次 `InitAsync` 都会重复注册触发词（官方 API 文档明确要求）。
- 部分 `.cs` 带 UTF-8 BOM，编辑时不要无故去掉。
- `tests/AliyunOss.UnitTest` 的凭据来自 **UserSecrets**（禁止读取）；`PutObjectsAsync` 依赖本机硬编码路径 `D:\Desktop\4.jpg`，缺文件时失败是预期，不是回归。

## 其它约定

- 命名空间 `FlowLauncher.Plugin.<Name>`；主构造函数、`record` 模型、`Lock`（.NET 9）护共享状态。
- WPF UI 更新走 `Application.Current.Dispatcher`。
- 插件 `plugin.json` / `favicon.ico` 需 `CopyToOutputDirectory: PreserveNewest`；`AppendTargetFrameworkToOutputPath=false`。
- 更多背景：`.github/copilot-instructions.md`、`README.md`、`docs/DevToolkit_插件化宿主重构_*.md`。以可执行配置（csproj / scripts / Directory.Build.targets）为准。
