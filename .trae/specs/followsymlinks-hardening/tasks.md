# Tasks

- [x] Task 1: DirectoryCopy realpath 边界校验
  - [x] 添加 `IsRealpathWithinSource(string symlinkPath, string sourceDir)` 私有方法
  - [x] 在 `Sync(DirectoryCopyOptions)` 的 symlink 跟随路径（lines 102-106, 119-123）中，当 `FollowSymlinks=true` 时调用 realpath 校验
  - [x] realpath 超出 sourceDir 时输出 `Console.Error.WriteLine` 警告并跳过
  - [x] 处理 symlink 链：使用 `Path.GetFullPath` + `FileInfo.LinkTarget` 递归解析到最终目标再判断
  - [x] 新建 `tests/Bukit.Engine.Tests/DirectoryCopyFollowSymlinksTests.cs`，覆盖：内部 symlink 正常复制、外部 symlink 跳过并警告、相对路径遍历 symlink 跳过、symlink 链解析（仅在 macOS/Linux 测试环境运行）

- [x] Task 2: ConfigLoader FollowSymlinks 警告
  - [x] 在 `ConfigLoader.Load()` 中，构建完 `BuildConfig` 后检查 `build.FollowSymlinks`
  - [x] 若为 `true`，通过 `Console.Error.WriteLine` 输出警告
  - [x] 注意：ConfigLoader 本身不持有 ILogger，使用 stderr 输出即可

- [x] Task 3: ConfigOverrides CI 强制 followSymlinks=false
  - [x] 在 `ConfigApplier.Apply()` 中，`IsCI` 块内追加 `build = build with { FollowSymlinks = false }`
  - [x] 确保 CI 强制优先于用户的 site.yaml 配置

- [x] Task 4: DoctorCommand FollowSymlinks 检查
  - [x] 找到 DoctorCommand 中现有的流程，添加检查项
  - [x] 检查 `config.Build.FollowSymlinks`
  - [x] 若为 `true`：输出安全建议 warning
  - [x] DoctorCommand 现有 24 个测试全部通过

# Task Dependencies

- Task 1, 2, 3, 4 均独立，可并行实施
- Task 1 依赖测试环境支持 Symlink（macOS/Linux），测试需用 `[Fact]` + 平台检测跳过 Windows
