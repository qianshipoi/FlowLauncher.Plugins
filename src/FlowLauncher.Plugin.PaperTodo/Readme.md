# FlowLauncher.Plugin.PaperTodo

基于 SQLite 的本地 Todo 与 Markdown Note 插件，提供 Flow Launcher 快捷添加和独立多纸片窗口。

Note 编辑器的 WYSIWYG 和 Markdown 源码切换依赖 Microsoft Edge WebView2 Runtime
（Windows 11 通常已内置；Windows 10 如未安装可由 Flow Launcher 或系统安装程序补齐）。

## 功能

- Todo 与 Markdown Note 统一存储在本地 SQLite
- `todo`、`note` 两个触发词
- `todo add` 快捷追加到固定收件箱
- 每条 Todo/Note 使用独立可缩放窗口，并保存位置、尺寸和置顶状态
- Todo 支持完成置底、原地保留或完成后清除，以及 10 秒内撤销
- Todo 支持提醒、文件/文件夹关联和笔记关联
- Note 使用离线 Toast UI Editor，支持 WYSIWYG、Markdown 源码切换和本地图片
- FTS5 trigram 搜索，短关键词自动回退 `LIKE`
- 每日滚动备份、手动数据库导出与恢复
- 从 PaperTodo `data.json` + `note-assets.lmdb` 一次性导入

## 使用方式

| 查询 | 行为 |
|------|------|
| `todo` | 新建 Todo、打开收件箱、显示最近事项 |
| `todo add 买牛奶` | 添加到收件箱；支持多行一次添加多项 |
| `todo new 项目清单` | 新建独立 Todo 纸 |
| `todo 报告` | 搜索 Todo 内容，Enter 打开窗口 |
| `note` | 新建笔记、显示最近笔记 |
| `note add 周会记录 \| 讨论内容` | 创建笔记并打开编辑器 |
| `note new 灵感` | 新建空白笔记 |
| `note 预算` | 搜索笔记标题和正文 |

Todo 查询结果按 Enter 打开所属窗口；右键可完成、恢复或删除。Note 右键支持打开、重命名、复制 Markdown 和删除。

## 数据位置

数据库和 WebView2 用户数据位于：

```text
%AppData%\FlowLauncher\Settings\Plugins\PaperTodo\
├── papertodo.db
├── Backups\
└── WebView2\
```

数据库使用 WAL、外键、事务和 FTS5。自动备份默认每天创建一次并保留 7 份，可在设置中调整。

## 从 PaperTodo 导入

在“Flow Launcher 设置 → 插件 → PaperTodo”中选择 PaperTodo 目录内的 `data.json`。插件会自动读取同目录的 `note-assets.lmdb`。

- 导入前请关闭 PaperTodo
- 只导入 Todo、Note、事项、提醒、关联、窗口几何和 Note 图片
- 不导入主题、快捷键、插件设置或其他应用级配置
- 使用原始 Paper/Item ID；重复导入时跳过已存在纸片
- 单张图片校验失败时跳过该图片，并保留 Markdown 引用作为可见提示

导入后本插件与 PaperTodo 数据完全独立，不进行实时双向同步。

## 构建与测试

```powershell
dotnet build .\src\FlowLauncher.Plugin.PaperTodo\FlowLauncher.Plugin.PaperTodo.csproj
dotnet test .\tests\PaperTodo.UnitTest\PaperTodo.UnitTest.csproj
```

发布：

```powershell
.\scripts\restart-flowlauncher.ps1 -Action Stop
.\scripts\publish-papertodo.ps1
.\scripts\restart-flowlauncher.ps1 -Action Start
```

`LightningDB` 固定为 `0.22.0`。该版本仍使用与 PaperTodo 当前
`note-assets.lmdb` 兼容的 LMDB 0.9 文件格式；`0.23+` 已切换到不兼容的 LMDB 1.0 格式。

## 架构

| 目录/文件 | 职责 |
|-----------|------|
| `Main.cs` | Flow 查询、快捷添加和右键菜单 |
| `Services/PaperDatabase.cs` | SQLite schema、CRUD、FTS、提醒和备份 |
| `Services/PaperWindowManager.cs` | 多窗口复用与几何持久化 |
| `Views/TodoPaperWindow` | 原生 WPF Todo 纸片 |
| `Views/NotePaperWindow` | WebView2 Note 宿主与图片桥接 |
| `wwwroot/note-editor.*` | 离线 WYSIWYG 页面 |
| `Views/SettingsView` | 完成策略、提醒、图片、导入和备份 |

注意：提醒只在 Flow Launcher 运行期间检查。Flow Launcher 退出后不保证提醒。
