# FlowLauncher.Plugin.VarName

变量命名助手插件，将中文翻译为英文变量名，支持多种命名规范一键复制。

## 功能

- 输入中文，调用 AI 生成 3-5 个英文候选词
- 选择候选词后自动生成 5 种命名规范变体（camelCase、PascalCase、snake_case、kebab-case、UPPER_SNAKE_CASE）
- SQLite 缓存历史翻译结果，相同输入不重复调用 AI
- 记住上次选中的候选词和命名规范，下次优先显示
- 支持手动刷新缓存重新生成

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `vn` | 显示使用提示 |
| `vn 用户管理` | 输入中文，按 Enter 调用 AI 生成候选词 |
| `vn 用户管理` → 选候选词 | 显示 5 种命名规范变体，选中即复制到剪贴板 |

### 操作流程

```
vn 用户管理
  ↓ Enter（首次调用 AI，结果缓存）
  ├─ userManagement    → Enter 选择
  ├─ accountManagement
  └─ userAdmin
      ↓
      camelCase:        userManagement      → 复制
      PascalCase:       UserManagement      → 复制
      snake_case:       user_management     → 复制
      kebab-case:       user-management     → 复制
      UPPER_SNAKE_CASE: USER_MANAGEMENT     → 复制
```

| 操作 | 说明 |
|------|------|
| **输入中文 + Enter** | 调用 AI 生成候选词（仅首次，后续走缓存） |
| **选择候选词 + Enter** | 显示命名规范变体 |
| **选择变体 + Enter** | 复制到剪贴板并关闭 |
| 缓存结果第一项 | 🔄 刷新缓存，重新调用 AI |

## 架构

```
┌─────────────────────────────────────────┐
│               Main.cs                    │
│         (IAsyncPlugin, ISettingProvider) │
├──────────┬───────────────┬──────────────┤
│ QueryAsync │ NamingVariants │ SettingsView │
└──────┬─────┴───────────────┴──────────────┘
       │
       ├─ 缓存命中 → 直接显示候选词
       ├─ 缓存未命中 → 提示"按 Enter 生成"
       └─ 确认生成 → 调用 AI → 缓存 → 显示候选词
       │
       ▼
┌─────────────┐  ┌──────────────┐
│VarNameCache │  │VarNameService│
│  (SQLite)   │  │  (OpenAI)    │
└─────────────┘  └──────────────┘
```

### 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，查询流程、候选词展示、命名规范转换 |
| `VarNameService.cs` | OpenAI API 调用，生成候选词 |
| `VarNameCache.cs` | SQLite 缓存（候选词、上次选中值、命名规范偏好） |
| `Settings.cs` | 配置模型（API Key、API URL、模型名称） |
| `SettingsViewModel.cs` | 设置面板 ViewModel |
| `SettingsView.xaml` | WPF 设置面板 UI |
| `plugin.json` | 插件元数据 |

### 命名规范转换

插件内置纯本地转换，不依赖 AI：

| 规范 | 示例 |
|------|------|
| camelCase | `userManagement` |
| PascalCase | `UserManagement` |
| snake_case | `user_management` |
| kebab-case | `user-management` |
| UPPER_SNAKE_CASE | `USER_MANAGEMENT` |

### 缓存表 (`cache`)

```sql
CREATE TABLE cache (
    chinese_text         TEXT PRIMARY KEY,
    suggestions          TEXT NOT NULL,       -- JSON 数组
    last_selected_value  TEXT,                -- 上次选中的候选词
    created_at           TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE preferences (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

## 配置项

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `ModelName` | `gpt-3.5-turbo` | OpenAI 模型名称 |
| `ApiUrl` | `https://api.openai.com/v1/` | API 地址（支持兼容接口） |
| `ApiKey` | `""` | API 密钥 |

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 框架 |
| `Microsoft.Data.Sqlite` | 9.0.8 | SQLite 缓存 |
| `OpenAI` | 2.8.0 | AI 候选词生成 |

## 构建

```bash
# 构建插件
dotnet build src/FlowLauncher.Plugin.VarName/FlowLauncher.Plugin.VarName.csproj

# 运行单元测试
dotnet test tests/VarName.UnitTest/VarName.UnitTest.csproj
```
