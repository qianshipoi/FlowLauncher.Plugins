# FlowLauncher.Plugin.Toolbox

工具合集插件：GUID 生成、JSON 编辑器、文件哈希、时间戳、编码转换、密码生成、进制转换、JWT 解码、Cron 解析。

## 功能

### GUID 生成器（`tool guid`）

- 每次查询生成 v4（随机）、v7（时间有序）、v1（时间戳）三个 GUID
- 输入 `tool guid 名称` 额外生成 v3（MD5）与 v5（SHA1）命名 UUID（DNS 命名空间）
- v1 为手写 RFC 4122 实现：随机节点并置多播位，**不采集真实 MAC 地址**；同进程内严格递增不重复
- Enter 复制；右键菜单支持复制大写、无连字符（N）、花括号（B）格式

### JSON 编辑器（`tool json`）

- `tool json {"name": "value"}` 打开编辑器窗口；`tool json`（无参数）读取剪贴板内容
- 查询结果会预校验 JSON 并提示错误位置，无效 JSON 也可打开修复（默认/保持 code 模式）
- 编辑器窗口基于 WebView2 加载本地打包的 [jsoneditor](https://github.com/josdejong/jsoneditor)（离线可用，无需联网）
- 默认进入「代码」视图；菜单栏右侧注入复制 / 导入 / 导出（jsoneditor 无官方自定义按钮 API，挂到 `.jsoneditor-menu`）
- 导出走宿主「另存为」对话框选路径后写入文件（浏览器预览时回退为直接下载）
- 窗口置顶显示，标题栏带固定图标
- WebView2 用户数据目录：`插件目录/WebView2Cache`

### 文件哈希（`tool fhash`）

- `tool fhash C:\path\to\file` 一次读盘流式计算 MD5、SHA1、SHA256、SHA384、SHA512、CRC32
- Enter 复制单个哈希；结果首行可复制全部（每行 `算法: 哈希`）
- 无参数时提供"选择文件…"入口（文件对话框）
- 大文件安全：1MB 缓冲分块读取，不整体载入内存；结果按路径缓存避免逐键重算
- CRC32 为 zlib 标准实现（多项式 0xEDB88320），可与常用校验工具对照

### 时间戳（`tool dt`）

显示本地时间、Unix 秒时间戳、Unix 毫秒时间戳、UTC ISO 8601、本地 ISO 8601，Enter 复制对应值。

### 编码转换（`tool enc`）

- 输入文本平铺显示：URL 编码、HTML 实体转义、十六进制编码（UTF-8）、Base64 编码
- 解码类结果（URL/Base64/Hex 解码）仅在可成功解码且与输入不同时出现；含 `+` 时额外提供 `+` 转空格的 URL 解码

### 密码生成（`tool pw [长度]`）

- 默认 24 位，上限 256；三行结果：强密码（大小写+数字+符号，保证四类各一）、无易混淆字符密码（去 0O1lI）、Hex 密钥（API Key 风格）
- 使用加密随机数 `RandomNumberGenerator`

### 进制转换（`tool base <值>`）

- 支持 `0x`/`0b`/`0o` 前缀、十进制、裸十六进制回退（如 `FF`）；输出十进制/十六进制/二进制/八进制
- 单个字符或 `U+4E2D` 输入：字符 ↔ Unicode 码点互查，附 UTF-8 字节

### 时间戳转日期（`tool ts`）

- 输入秒/毫秒时间戳自动按量级识别（>10^12 视为毫秒），输出本地时间、UTC、相对时间（"3 天前"）

### JWT 解码（`tool jwt`）

- 解析 header.payload[.signature]，Base64url 手写解码，Payload 以紧凑 JSON 展示
- 有 `exp` 声明时显示过期时间与"剩余/已过期"，有 `iat` 时显示签发时间
- Payload 行右键菜单可直接**在 JSON 编辑器中打开**（复用 `tool json` 窗口）
- 仅解码展示，不校验签名

### Cron 解析（`tool cron`）

- 支持 5 段（分 时 日 月 周）与 6 段（含秒）表达式（[Cronos](https://github.com/HangfireIO/Cronos) 解析）
- 输出中文含义描述（如 `0 9 * * 1-5` → "每周一至周五 09:00"）+ 未来 5 次执行时间（本地时区）

## 设置

设置面板（Flow Launcher 设置 → 插件 → Toolbox）提供 10 个复选框，逐个启用/禁用各子命令：

- 变更即时保存（`SaveSettingJsonStorage`），无需重启 Flow Launcher
- 禁用的子命令查询时返回明确提示行；`tool` 帮助列表只显示启用的工具
- 关联行为联动：`json` 被禁用时，`jwt` Payload 的右键"在 JSON 编辑器中打开"一并隐藏
- 所有工具都禁用时，`tool` 显示指引提示

## 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，`tool` 子命令分发（guid / json / fhash / dt / enc / pw / base / ts / jwt / cron）+ 设置启停拦截 |
| `Settings.cs` | 设置模型：各工具启用开关（默认全开）与 `IsEnabled` 映射 |
| `ViewModels/SettingsViewModel.cs` | 设置面板 VM，复选框变更即写回持久化 |
| `Views/SettingsView.xaml(.cs)` | 设置面板（ISettingProvider） |
| `Services/*.cs` | 各工具纯函数服务（GuidGenerator、HashCalculator、TimestampProvider、TextEncoderService、PasswordGeneratorService、BaseConverterService、TimestampParser、JwtDecoderService、CronParserService，均 internal 供单元测试） |
| `Views/JsonEditorWindow.xaml(.cs)` | WebView2 宿主窗口，虚拟域名加载 wwwroot |
| `wwwroot/index.html` | 编辑器页面，与宿主通过 `postMessage` 双向通信 |
| `wwwroot/vendor/` | jsoneditor 10.4.3 发行文件（min.js / min.css / img） |

## 架构说明

- **宿主 ↔ 编辑器通信**：页面加载完成后 `postMessage({type:'ready'})`，宿主回发 `{type:'init', json}` 注入内容；页面 `{type:'copy', text}` 由宿主写剪贴板（比页面内 `navigator.clipboard` 可靠）
- **静态资源**：`SetVirtualHostNameToFolderMapping("appassets.local", wwwroot)` 将本地目录映射为虚拟域名，规避 `file://` 协议限制
- **单实例**：JSON 编辑器窗口复用，重复触发时激活已有窗口并注入新内容；窗口 `Closed` 时释放 WebView2

## 开发

```bash
dotnet build FlowLauncher.Plugin.Toolbox/FlowLauncher.Plugin.Toolbox.csproj
dotnet test tests/Toolbox.UnitTest/Toolbox.UnitTest.csproj
```

单元测试覆盖 GUID 生成（RFC 4122 标准向量）、哈希计算（标准测试向量 + 流式分块一致性）、时间戳格式化。
