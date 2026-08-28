# FlowLauncher.Plugin.Toolbox

工具合集插件：GUID 生成、JSON 编辑器、文件哈希、时间戳查询。

## 功能

### GUID 生成器（`tool guid`）

- 每次查询生成 v4（随机）、v7（时间有序）、v1（时间戳）三个 GUID
- 输入 `tool guid 名称` 额外生成 v3（MD5）与 v5（SHA1）命名 UUID（DNS 命名空间）
- v1 为手写 RFC 4122 实现：随机节点并置多播位，**不采集真实 MAC 地址**；同进程内严格递增不重复
- Enter 复制；右键菜单支持复制大写、无连字符（N）、花括号（B）格式

### JSON 编辑器（`tool json`）

- `tool json {"name": "value"}` 打开编辑器窗口；`tool json`（无参数）读取剪贴板内容
- 查询结果会预校验 JSON 并提示错误位置，无效 JSON 也可打开修复（自动切换到 code 模式）
- 编辑器窗口基于 WebView2 加载本地打包的 [jsoneditor](https://github.com/josdejong/jsoneditor)（离线可用，无需联网）
- 窗口工具栏：格式化 / 压缩 / 复制（格式化）/ 复制（压缩）
- WebView2 用户数据目录：`插件目录/WebView2Cache`

### 文件哈希（`tool fhash`）

- `tool fhash C:\path\to\file` 一次读盘流式计算 MD5、SHA1、SHA256、SHA384、SHA512、CRC32
- Enter 复制单个哈希；结果首行可复制全部（每行 `算法: 哈希`）
- 无参数时提供"选择文件…"入口（文件对话框）
- 大文件安全：1MB 缓冲分块读取，不整体载入内存；结果按路径缓存避免逐键重算
- CRC32 为 zlib 标准实现（多项式 0xEDB88320），可与常用校验工具对照

### 时间戳（`tool dt`）

显示本地时间、Unix 秒时间戳、Unix 毫秒时间戳、UTC ISO 8601、本地 ISO 8601，Enter 复制对应值。

## 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，`tool` 子命令分发（guid / json / fhash / dt） |
| `Services/GuidGenerator.cs` | v1/v3/v4/v5/v7 生成（internal，供单元测试） |
| `Services/HashCalculator.cs` | 单遍流式多算法哈希 + 内置 CRC32 |
| `Services/TimestampProvider.cs` | 时间/时间戳条目（纯函数） |
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
