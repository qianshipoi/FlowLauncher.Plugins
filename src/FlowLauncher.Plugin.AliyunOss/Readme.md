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
| `oss` | 显示子命令入口（ossd / ossu） |
| `ossd` | 下载模式 — 选择 Bucket → 选择文件 → 下载 |
| `ossu` | 上传模式 — 选择 Bucket → 选择本地文件 → 上传 |

### 操作流程

1. 输入 `oss` 选择下载或上传模式
2. 输入 Bucket 名称进行模糊搜索
3. 选择目标 Bucket 后浏览文件/目录
4. Enter 确认下载或上传

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
┌─────────────┐
│  OssService │ ← IMemoryCache 缓存 Bucket
└─────────────┘
```

### 文件结构

| 文件 | 职责 |
|------|------|
| `Main.cs` | 插件入口，路由 ossd/ossu |
| `Settings.cs` | 配置模型（AccessKeyId、AccessKeySecret、Region） |
| `Services/OssService.cs` | OSS SDK 封装，Bucket/文件操作 |
| `Queries/DownloadQuery.cs` | 下载流程处理 |
| `Queries/UploadQuery.cs` | 上传流程处理 |
| `ViewModel/` | MVVM ViewModel |
| `Views/` | WPF 设置面板 |
| `Utils/ResultBuilder.cs` | Result 构建辅助类 |

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
