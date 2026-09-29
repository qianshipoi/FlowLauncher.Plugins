# FlowLauncher.Plugin.AliyunOss

阿里云 OSS 对象存储文件上传/下载插件。

## 功能

- 浏览和搜索 Bucket 列表
- 下载 OSS 文件到本地
- 上传本地文件到 OSS
- Bucket 列表缓存，减少 API 调用

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `oss` | 显示子命令入口（ossd / ossu / ossb） |
| `ossd` | 下载模式 — 选择 Bucket → 选择对象 → 下载 |
| `ossu` | 上传模式 — 选择 Bucket → 打开**常驻拖拽气泡** → 把文件拖进气泡上传 |
| `ossb` | 文件浏览器 — 浏览对象、拖拽上传 |

### 操作流程

1. 输入 `oss` 选择下载或上传模式
2. 输入 Bucket 名称进行模糊搜索
3. 选择目标 Bucket 后浏览文件/目录
4. Enter 确认，弹出传输窗口

### 上传：常驻拖拽气泡（`ossu`）

上传不走文件选择框，而是打开一个**常驻气泡**，直接把文件拖进去：

- 气泡打开后停在屏幕上等待拖入（不自动关闭），环为静止浅灰，提示「拖文件到这里上传」；文件悬停其上时环变蓝、提示改为「松手即上传」
- 松开即开始上传；可以一次拖多个，逐个上传并在结尾汇总（单个失败不中断其余）
- **目标路径取决于打开时是否指定了对象名**：
  - 指定了（`ossu <bucket> <object>`）→ 每次都传到该路径，再次上传即覆盖
  - 未指定（`ossu <bucket>`，即「上传临时文件」）→ 每个文件现场生成随机路径 `tmp/{guid}{ext}`，多次拖拽互不覆盖
- **路径已存在的处理**：指定了对象名时，气泡会在上传前检查同名对象并就地询问「备份并替换 / 直接替换 / 取消」
- **传输中**：底部同时有「取消」和「关闭」两颗图标按钮。**取消只中止本轮传输、气泡留在原地**（回到等待拖入，可以立刻换个文件重来）；关闭才结束气泡
- **上传完毕**：底部显示「复制链接」与「关闭」，气泡**不会**自动关闭，可以继续拖下一个文件
- 气泡只能同时处理一批：传输中再次拖入会被忽略

### 传输气泡

上传/下载都通过插件自己的**圆形气泡窗口**完成，**不使用 Flow Launcher 宿主的进度弹窗**：

- 气泡外圈即进度环（进行中蓝色、冲突琥珀、完成绿色、失败红色），带投影阴影，在浅色/深色背景上都能与背景区分
- **文案与按钮同层**：文案在上、按钮在下，按钮不会叠在文字之上；没有遮罩层
- 圆心显示百分比、状态与实时速率；**点击百分比可切换显示「已传/总字节」**，再点切回。字节按紧凑格式显示（数值过百省略小数），字号也会随文本长度自动缩小，大数据量下不会超出圆内；长度未知时环上显示旋转的短弧
- **传输中鼠标移入气泡** → 底部出现垃圾桶图标；点击后变红对勾（环同步转深红、提示"再点确认取消"），**再点对勾**才真正取消。点击气泡其它区域或 5 秒内未确认则恢复垃圾桶、传输继续；Alt+F4 同走此确认
- **传输完毕** → 底部显示主操作 + 「关闭」按钮。下载的产物是本地文件，主操作是**「打开文件所在位置」**（不再自动弹出资源管理器，改由用户点击）；上传的产物是访问链接，主操作是「复制链接」
- 各状态之间以淡入淡出过渡（170ms 缓动），避免瞬间切换显得生硬
- 需要显示按钮时**气泡缓动放大**（152 → 188px，同为 170ms 缓动），给按钮留出余白
- 同名对象冲突（备份并替换 / 直接替换 / 取消）在底部以图标就地询问
- 失败或取消 → 显示状态文字 + 关闭图标，详细原因在按钮 ToolTip 上。**「取消」与「失败」区分处理**：用户主动取消（按了对勾、或点关闭/Alt+F4）不算错误，气泡静默回到等待拖入，不会留下任何失败提示；只有上传/下载真的出错才进入失败态
- 对象名会自动规范化（去掉前导斜杠、统一为 `/`、合并重复斜杠）
- 气泡无边框、可按住任意位置拖动，置顶显示；**位置会被记住**，下次打开时若原位置仍在屏幕内则原地恢复，否则回到屏幕中央

这样即使主搜索窗失焦自动隐藏，气泡也能正常出现并持续汇报进度。

### 设置：文件 MD5 元数据（默认关闭）

设置面板里的「文件 MD5 元数据」开关控制上传时是否计算文件 MD5 并写入对象元数据 `x-oss-meta-file-md5`。

**默认关闭**，因为算哈希必须把整个文件多读一遍（实测 512MB ≈ 2.7 秒，4GB 级要十几秒），而 OSS SDK 本身已在上传时做 CRC64 完整性校验，一份文件读两遍纯属浪费。只有下游服务确实要靠这个指纹识别/比对文件时才需要开启——开启后大文件上传会明显变慢。

> 该元数据服务端只原样保存、不做校验。若目的是让服务端拒绝损坏的上传，应改用请求头的 `Content-MD5` 而不是自定义元数据。

## 架构

```
┌──────────────────────────────────────────┐
│               Main.cs                     │
│  (IAsyncPlugin, IReloadable, ISettingProvider) │
├──────────┬──────────────┬────────────────┤
│ DownloadQuery │ UploadQuery │ OssSettings   │
└──────┬─────┴──────────────┴────────────────┘
       │
       ▼
┌──────────────────────┐
│ TransferEngine /      │ ← 上传下载编排（宿主无关）
│ TransferWindow        │
└──────────┬───────────┘
           ▼
┌─────────────┐
│  OssService │ ← IMemoryCache 缓存 Bucket / 对象存在性
└─────────────┘
```

### 文件结构

全部位于 `src/FlowLauncher.Plugin.AliyunOss`：

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，路由 oss/ossd/ossu/ossb |
| `Services/OssService.cs` | OSS SDK 封装，Bucket/文件操作（进度以 `IProgress<TransferProgress>` 上报） |
| `DownloadService.cs` | 带进度的 HTTP 下载，失败/取消时清理半成品文件 |
| `TransferEngine.cs` | 上传/下载业务编排：冲突处理、备份、生成访问链接；`ResolveKey` 决定固定路径 / 临时随机路径 |
| `Models/TransferProgress.cs` | 进度模型（已传字节 / 总字节），`Total=0` 表示长度未知 |
| `Models/TransferSnapshot.cs` | 速率与剩余时间快照 |
| `Utils/TransferRateTracker.cs` | 滑动窗口速率估算，抑制数字抖动 |
| `Utils/FileSizeFormatter.cs` | 字节量、速率格式化（含紧凑格式） |
| `Utils/TextFit.cs` | 按可用宽度为文字挑选字号（圆形气泡内防溢出） |
| `Utils/RingArc.cs` | 圆环进度弧的角度与坐标换算（含整圆退化处理） |
| `Utils/OssKey.cs` | 对象名规范化（前导斜杠会导致 403） |
| `Transfer/ITransferHost.cs` | 宿主能力：复制文本、定位文件 |
| `Transfer/ITransferUi.cs` | 流程向界面反馈（状态文案、冲突询问） |
| `ViewModels/TransferViewModel.cs` | 气泡 VM：进度节流、取消、链接、冲突选择；常驻拖拽态（`IsDropZone` / `RunDropUploadAsync`） |
| `Views/TransferWindow.xaml(.cs)` | 圆形气泡窗口：环形进度、两级取消确认、完成图标按钮、淡入淡出与尺寸缓动、拖拽上传 |
| `Views/FadeTransition.cs` | 元素显隐的淡入淡出过渡（处理快速悬停竞态） |
| `Views/Controls/RingProgress.cs` | 自绘圆环进度控件（不确定态旋转） |
| `Queries/DownloadQuery.cs` | 下载流程处理 |
| `Queries/UploadQuery.cs` | 上传流程处理 |
| `ViewModel/FileBrowserViewModel.cs` | 文件浏览器（拖拽上传走传输窗口） |
| `Views/FlowTransferHost.cs` | `ITransferHost` 的 Flow Launcher 适配（剪贴板 / OpenDirectory） |
| `Views/FlowLocalization.cs` | `ILocalization` 的 Flow Launcher 适配（经 `GetTranslation` 读语言文件） |
| `Languages/en.xaml`、`Languages/zh-cn.xaml` | 中英文案（Flow 的插件语言文件格式，见下） |
| `Views/FileBrowserWindow.xaml` | 文件浏览器窗口 |
| `Views/OssSettings.xaml` | 设置面板 |

## 多语言

Flow Launcher 的插件语言文件是 **`Languages/<语言码>.xaml`**（WPF `ResourceDictionary`），
由 `Flow.Launcher.Core` 的 `Internationalization` 按固定文件名加载，扩展名硬编码为 `.xaml`：

- 文件名必须是语言码本身（`en.xaml`、`zh-cn.xaml`），Flow 会回退到 `en.xaml`
- 每条为 `<system:String x:Key="...">文案</system:String>`，`{0}` 作占位符
- 文件带有 UTF-8 BOM 与 XML 声明（中文必须，否则解码失败）
- **`UseWPF` 的默认通配会把 `**/*.xaml` 当 Page 编译进 baml**，而 Flow 是从磁盘按路径读取，
  因此 csproj 里必须把 `Languages\**\*.xaml` 从 `Page` 移除并按普通文件复制
- 代码侧经 `IPublicAPI.GetTranslation(key)` 取值；未命中时 Flow 返回的是
  `"No Translation for key <key>"` 而不是 key 本身，判断回退时要按此前缀匹配
- 界面上受圆形空间限制的短提示走绑定（见 `TransferViewModel` 的 Tooltip 属性）

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 框架 |
| `AlibabaCloud.OSS.V2` | 0.1.2 | 阿里云 OSS SDK |
| `iNKORE.UI.WPF.Modern` | 0.10.2.1 | UI 组件 |
| `Microsoft.Extensions.Caching.Memory` | 9.0.11 | Bucket 列表缓存 |

## 构建

```bash
dotnet build src/FlowLauncher.Plugin.AliyunOss/FlowLauncher.Plugin.AliyunOss.csproj
dotnet test tests/AliyunOss.UnitTest/AliyunOss.UnitTest.csproj
```
