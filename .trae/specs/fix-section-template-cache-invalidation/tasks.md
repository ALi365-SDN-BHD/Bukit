# Tasks

- [x] Task 1: 创建统一的 `TemplateFileSignature` 结构体，包含 `LastWriteTimeUtc`、`Length` 和 `ContentHash`（文件内容 SHA256 前 8 字节）
  - [x] 在 `ScribanTemplateRenderer.cs` 中定义 `private readonly record struct TemplateFileSignature(DateTime LastWriteTimeUtc, long Length, long ContentHash)`
  - [x] 删除旧的 `FileSignature` 和 `SectionFileSignature` 定义
  - [x] 更新 `CachedTemplate` 的 `Signature` 字段类型为 `TemplateFileSignature`
  - [x] 更新 `CachedSectionTemplate` 的 `Signature` 字段类型为 `TemplateFileSignature`

- [x] Task 2: 更新 `GetCachedTemplate` 方法使用新的签名
  - [x] 读取文件内容后计算 SHA256 前 8 字节作为 `ContentHash`
  - [x] 创建 `TemplateFileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length, contentHash)`

- [x] Task 3: 更新 `TryGetCachedSectionTemplate` 方法使用新的签名
  - [x] 读取文件内容后计算 SHA256 前 8 字节作为 `ContentHash`
  - [x] 创建 `TemplateFileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length, contentHash)`

- [x] Task 4: 运行测试验证修复
  - [x] 运行 `dotnet test tests/Bukit.Rendering.Tests -c Release` 确认失败测试通过（136/136）
  - [x] 运行 `dotnet test bukit.slnx -c Release --no-build` 确认无新增失败（3132/3132）

# Task Dependencies
- Task 2 和 Task 3 均依赖 Task 1
- Task 2 和 Task 3 可并行进行
- Task 4 依赖 Task 1-3 全部完成
