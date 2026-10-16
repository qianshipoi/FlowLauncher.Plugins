# FlowLauncher.Plugin.S3

支持多套配置的 S3 兼容对象存储插件，用于文件浏览、上传、下载和对象元数据编辑。

## 关键字

| 关键字 | 说明 |
|--------|------|
| `s3` | 显示子命令入口和当前配置 |
| `s3d` | 下载对象 |
| `s3u` | 打开拖拽上传气泡 |
| `s3b` | 打开文件浏览器 |
| `s3p` | 切换当前配置 |

## 配置

设置页支持保存多套命名配置。每套配置包含：

- AWS S3、阿里云 OSS S3 兼容、MinIO、通用自定义四种预设
- Endpoint、Region、Path/VirtualHosted 寻址方式
- AccessKey ID、DPAPI 加密的 AccessKey Secret 和可选 SessionToken
- 可选测试 Bucket
- SigV4 payload 签名和默认 checksum 的兼容开关

阿里云预设默认使用 `https://s3.oss-{region}.aliyuncs.com` 进行 S3 传输，并使用
`https://oss-{region}.aliyuncs.com` 的原生 OSS SDK 读写 `x-oss-meta-*` 元数据。

## 元数据

在文件浏览器中选中对象后，可以打开元数据窗口：

- 查看 Content-Type、大小、最后修改和 ETag
- 新增、编辑、删除用户元数据
- 标准 S3 服务通过同对象 CopyObject + `MetadataDirective=REPLACE` 保存
- 阿里云配置通过原生 OSS CopyObject 保存
- 保存前使用 ETag 检查并发修改

元数据改写可能创建对象新版本，并可能改变 ACL 或其他复制语义；插件会明确显示错误，
但不会在服务不支持时静默覆盖对象。

## 兼容性

- 通用 S3 后端使用 SigV4
- 端点至少需要支持 ListObjectsV2、PutObject、GetObject、HeadObject 和 CopyObject
- 如果凭据没有 ListBuckets 权限，可以直接输入 Bucket 名称
- 需要 SigV2 的服务不在支持范围内

## 测试

```powershell
dotnet test tests/S3.UnitTest/S3.UnitTest.csproj
$env:S3_RUN_DOCKER_TESTS = '1'
dotnet test tests/S3.IntegrationTest/S3.IntegrationTest.csproj
```

集成测试由 Testcontainers 启动 `rustfs/rustfs:1.0.2`，需要本机 Docker Desktop
使用 Linux containers。
