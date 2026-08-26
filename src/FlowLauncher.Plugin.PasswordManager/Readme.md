# FlowLauncher.Plugin.PasswordManager

Flow Launcher 本地密码管理插件，使用 SQLCipher 整库加密 + DPAPI 字段级加密双重保护，支持模糊搜索。

## 功能

- SQLCipher（AES-256）整库加密 + DPAPI 字段级加密双重保护
- 按标题、用户名、标签进行模糊搜索
- 一键复制密码或用户名到剪贴板
- 复制后自动清除剪贴板（默认 15 秒）
- 数据库密钥由 DPAPI 保护，仅当前 Windows 用户可访问
- 交互式和快捷式两种添加方式

## 使用方式

| 关键字 | 说明 |
|--------|------|
| `pwd` | 显示最近使用的条目 |
| `pwd <关键词>` | 模糊搜索标题、用户名、标签 |
| `pwd add 标题 \| 用户名 \| 密码` | 快速添加条目 |
| `pwd add` | 交互式添加（分步输入标题→用户名→密码） |

| 操作 | 说明 |
|------|------|
| **Enter** | 复制密码到剪贴板 |
| 右键 → 复制用户名 | 复制用户名到剪贴板 |
| 右键 → 复制密码 | 复制密码到剪贴板 |
| 右键 → 删除条目 | 删除该密码条目 |

## 架构

```
┌──────────────────────────────────────────────────┐
│                    Main.cs                        │
│            (IAsyncPlugin, IContextMenu,            │
│             ISettingProvider, IDisposable)         │
├──────────┬────────────────┬───────────────────────┤
│  QueryAsync  │  LoadContextMenus  │  CreateSettingPanel  │
│  - 搜索      │  - 复制用户名      │  - SettingsView      │
│  - 添加流程  │  - 复制密码        │                      │
│  - 最近条目  │  - 删除条目        │                      │
└──────┬───────┴────────┬──────────┴───────────────────────┘
       │                │
       ▼                ▼
┌─────────────┐  ┌──────────────┐  ┌───────────────┐
│PasswordStore│  │ DBKeyManager │  │PasswordCrypto │
│ (SQLCipher) │  │  (DPAPI)     │  │   (DPAPI)     │
│ 整库加密     │  │ 数据库密钥    │  │  字段级加密    │
└─────────────┘  └──────────────┘  └───────────────┘
```

### 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，处理查询、右键菜单、设置面板 |
| `PasswordEntry.cs` | 数据模型（`record` 类型） |
| `PasswordStore.cs` | SQLCipher 加密数据库 CRUD 操作 |
| `CryptoService.cs` | DBKeyManager（数据库密钥）+ PasswordCrypto（字段级 DPAPI 加密） |
| `Settings.cs` | 配置模型（剪贴板清除时间、显示条目数） |
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

## 配置项

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `ClearClipboardAfterMs` | `15000` | 复制后多少毫秒自动清除剪贴板（0 = 不清除） |
| `RecentItemsCount` | `10` | 空查询时显示的最近条目数量 |

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
