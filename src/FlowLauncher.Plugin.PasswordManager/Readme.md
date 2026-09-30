# FlowLauncher.Plugin.PasswordManager

Flow Launcher 本地密码管理插件，使用 SQLCipher 整库加密 + DPAPI 字段级加密双重保护，支持模糊搜索。

## 功能

- SQLCipher（AES-256）整库加密 + DPAPI 字段级加密双重保护
- 按标题、用户名、标签进行模糊搜索
- 一键复制密码或用户名到剪贴板
- 复制后自动清除剪贴板（默认 15 秒）
- 随机密码生成器：可配置字符类别、忽略字符与默认长度列表
- 数据库密钥由 DPAPI 保护，仅当前 Windows 用户可访问
- 交互式和快捷式两种添加方式

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `pwd` | 显示最近使用的条目 |
| `pwd <关键词>` | 模糊搜索标题、用户名、标签 |
| `pwd add 标题 \| 用户名 \| 密码` | 快速添加条目 |
| `pwd add` | 交互式添加（分步输入标题→用户名→密码） |
| `pwdgen` | 按设置的默认长度列表生成多组随机密码 |
| `pwdgen <长度>` | 在默认长度列表之外，额外生成指定长度的密码 |

| 操作 | 说明 |
|------|------|
| **Enter** | 复制密码到剪贴板 |
| 右键 → 复制用户名 | 复制用户名到剪贴板 |
| 右键 → 复制密码 | 复制密码到剪贴板 |
| 右键 → 删除条目 | 删除该密码条目 |
| 右键 → 重新生成 | （`pwdgen` 结果）重新生成同长度密码 |

### 随机密码生成

`pwdgen` 生成结果按长度从短到长排列，每条副标题显示长度、熵值估算与当前字符集。
`pwdgen 32` 会把 32 位结果排在首位，其余默认长度依次跟随。

- 每个已勾选的字符类别在生成的密码中至少出现一次，之后整体洗牌
- 长度不足 4 位时随机取舍类别，不会退化
- 使用 `RandomNumberGenerator`（加密随机数），每位字符独立均匀抽取
- 无可用字符（未勾选任何类别或全部被忽略）时给出提示并直达设置面板

## 架构

```
┌──────────────────────────────────────────────────────────────┐
│                          Main.cs                             │
│      (IAsyncPlugin, IContextMenu, ISettingProvider,          │
│       IDisposable)  · 触发词 pwd / pwdgen                    │
├──────────┬────────────────┬──────────────────────────────────┤
│ QueryAsync   │ LoadContextMenus │ CreateSettingPanel           │
│  - 搜索       │  - 复制用户名     │  - SettingsView              │
│  - 添加流程   │  - 复制密码       │  - 生成器配置 + 实时预览     │
│  - 最近条目   │  - 删除条目       │                              │
│  - 生成密码   │  - 重新生成       │                              │
└──────┬───────┴────────┬─────────┴──────────────────────────────┘
       │                │
       ▼                ▼
┌─────────────┐  ┌──────────────┐  ┌───────────────┐  ┌──────────────────────┐
│PasswordStore│  │ DBKeyManager │  │PasswordCrypto │  │PasswordGenerator     │
│ (SQLCipher) │  │  (DPAPI)     │  │   (DPAPI)     │  │Service (加密随机数)   │
│ 整库加密     │  │ 数据库密钥    │  │  字段级加密    │  │ 字符池/长度解析/熵估  │
└─────────────┘  └──────────────┘  └───────────────┘  └──────────────────────┘
```

### 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，处理查询、右键菜单、设置面板 |
| `PasswordEntry.cs` | 数据模型（`record` 类型） |
| `GeneratedPassword.cs` | 生成结果模型，作为 `ContextData` 供右键菜单复用 |
| `PasswordStore.cs` | SQLCipher 加密数据库 CRUD 操作 |
| `CryptoService.cs` | DBKeyManager（数据库密钥）+ PasswordCrypto（字段级 DPAPI 加密） |
| `PasswordGeneratorService.cs` | 随机密码生成、字符集构建、长度解析、熵估算 |
| `Settings.cs` | 配置模型（剪贴板清除时间、显示条目数、生成器选项） |
| `SettingsView.xaml` | WPF 设置面板 UI |
| `plugin.json` | 插件元数据 |

### 数据模型 (`PasswordEntry`)

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | `string` | GUID 主键 |
| `Title` | `string` | 标题/描述（搜索字段） |
| `Username` | `string` | 用户名（搜索字段） |
| `EncryptedPassword` | `string` | DPAPI 加密后的密码（Base64），数据库中同时受 SQLCipher 保护 |
| `Url` | `string` | 可选 URL |
| `Tags` | `string` | 标签，逗号分隔（搜索字段） |
| `Notes` | `string` | 备注 |
| `CreatedAt` | `string` | 创建时间 |
| `UpdatedAt` | `string` | 更新时间（用于排序最近使用） |

### 数据库表 (`passwords`)

```sql
CREATE TABLE passwords (
    id                 TEXT PRIMARY KEY,
    title              TEXT NOT NULL,
    username           TEXT NOT NULL,
    encrypted_password TEXT NOT NULL,
    url                TEXT NOT NULL DEFAULT '',
    tags               TEXT NOT NULL DEFAULT '',
    notes              TEXT NOT NULL DEFAULT '',
    created_at         TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at         TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX idx_passwords_title ON passwords(title);
CREATE INDEX idx_passwords_username ON passwords(username);
```

## 安全设计

| 层级 | 措施 |
|------|------|
| **数据库加密** | SQLCipher（AES-256）整库加密，磁盘上无明文数据 |
| **字段加密** | 密码字段额外使用 Windows DPAPI 加密（`PasswordCrypto`），双重保护 |
| **密钥保护** | SQLCipher 密钥使用 Windows DPAPI 加密存储在 `passwords.dbkey` |
| **密钥生成** | 首次运行时自动随机生成 32 字节密钥 |
| **作用域** | 仅当前 Windows 用户可解密，其他用户/系统无法读取 |
| **剪贴板** | 复制后默认 15 秒自动清除（可在设置中调整或禁用） |
| **随机密码** | 使用 `RandomNumberGenerator`（CSPRNG），不落到 `Random`；忽略字符在每类字符池上统一生效 |

## 配置项

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `ClearClipboardAfterMs` | `15000` | 复制后多少毫秒自动清除剪贴板（0 = 不清除） |
| `RecentItemsCount` | `10` | 空查询时显示的最近条目数量 |
| `GeneratorLengths` | `"8, 12, 16, 20, 24"` | `pwdgen` 默认展示的长度列表，最多 8 项 |
| `GeneratorLowercase` | `true` | 生成结果包含小写字母 |
| `GeneratorUppercase` | `true` | 生成结果包含大写字母 |
| `GeneratorDigits` | `true` | 生成结果包含数字 |
| `GeneratorSymbols` | `true` | 生成结果包含符号 |
| `GeneratorExcludeChars` | `""` | 忽略字符，不会出现在密码中（如 `0O1lI`） |

设置面板中修改生成器选项会立即写回配置并刷新预览；长度列表非法时自动回退到默认值。

## 数据文件

插件运行时在 `PluginDirectory` 下生成以下文件：

| 文件 | 说明 |
|------|------|
| `passwords.db` | SQLCipher 加密的 SQLite 数据库（整个文件 AES-256 加密） |
| `passwords.dbkey` | 数据库密钥（DPAPI 加密，仅当前 Windows 用户可解密） |

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| `Flow.Launcher.Plugin` | 5.1.0 | 插件 SDK |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM 框架 |
| `Microsoft.Data.Sqlite` | 9.0.8 | SQLite ADO.NET |
| `SQLitePCLRaw.bundle_e_sqlcipher` | 2.1.11 | SQLCipher 加密引擎 |

## 构建

```bash
# 构建插件
dotnet build src/FlowLauncher.Plugin.PasswordManager/FlowLauncher.Plugin.PasswordManager.csproj

# 运行单元测试
dotnet test tests/PasswordManager.UnitTest/PasswordManager.UnitTest.csproj
```
