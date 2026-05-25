# Core Hardening P0-P1 Tasks

## Task 1: 新增 RenderDependencyHasher + RenderDependencyHash ✅
- [x] 1.1 在 `src/Bukit.Engine/Incremental/` 下新增 `RenderDependencyHasher.cs`
- [x] 1.2 在 `BuildManifestEntry` 新增 `RenderDependencyHash` 属性
- [x] 1.3 修改 `PageRenderDispatcher.RenderPagesAsync`，加入 RenderDependencyHash skip 判断
- [x] 1.4 修改 `PageRenderDispatcher.RenderSpecialListIfNeededAsync`，列表页也加入
- [x] 1.5 修改 `SiteEngine.BuildVariantAsync`，计算并传递 RenderDependencyHash
- [x] 1.6 旧 manifest 无 RenderDependencyHash 时触发重新渲染
- [x] 1.7 新增 render reason：`render_dependency_changed`
- [x] 1.8 dotnet build + dotnet test 验证通过

## Task 2: Static HTML 冲突检查解耦 staticTemplate ✅
- [x] 2.1 修改 `SiteEngine.BuildVariantAsync`，无论 staticTemplate 是否配置都调用 `BuildStaticHtmlRoutes`
- [x] 2.2 无 staticTemplate 时传入占位符模板名 `__raw_static__`
- [x] 2.3 `RouteInventoryValidator.ValidateFinalRoutes` 始终包含 staticHtmlRoutes
- [x] 2.4 已有路由冲突检测机制生效

## Task 3: 统一 SafeOutputFileSystem 覆盖 ✅
- [x] 3.1 扩展 `SafeOutputFileSystem` 覆盖
- [x] 3.2 `StaticFileService` 非 HTML 复制经过 `FileWriter.GetSafeFullPath` 校验
- [x] 3.3 `DirectoryCopy` 所有方法接受 `outputRoot` 参数并在写入前校验
- [x] 3.4 `AssetPipeline` 传递 `outputRoot` 给 DirectoryCopy
- [x] 3.5 `BuildManifestTracker` 媒体文件操作传递 outputRoot

## Task 4: 默认 dotfile deny list ✅
- [x] 4.1 `IgnoreDotPrefixedFiles` 默认值改为 `true`
- [x] 4.2 新增内置默认 deny list（.env/.git/.DS_Store 等）和 allow list（.well-known）
- [x] 4.3 allowlist 优先于 denylist
- [x] 4.4 新增 `build.publishDotFiles` 配置项
- [x] 4.5 `ShouldSkipDotfile` 方法实现过滤逻辑

## Task 5: ValidateInternalUrl 增加 URL 段遍历检查 ✅
- [x] 5.1 新增 `ValidateUrlPathSegments` 私有方法
- [x] 5.2 拒绝 `.`/`..`/编码后的 `..`/反斜杠/编码斜杠
- [x] 5.3 解码异常时 fail
- [x] 5.4 正常 slug 不受影响

## Task 6: 废弃 top-level outputPath ✅
- [x] 6.1 `GenerateWithSource` 中检测 top-level `outputPath`
- [x] 6.2 抛出 `ConfigException`，含 "deprecated" 和 `route.outputPath` 迁移指引
- [x] 6.3 `route.outputPath` 正常工作不受影响

## Task 7: collections.yaml 解析失败报错 ✅
- [x] 7.1 移除 `TryReadCollectionsFile` YAML 异常静默捕获
- [x] 7.2 YAML 语法错误自然向上传播
- [x] 7.3 文件不存在时正常回退

## Task 8: 配置 bool/int/long/double 严格解析 ✅
- [x] 8.1 新增 `GetOptionalBoolStrict`/`GetOptionalIntStrict`/`GetOptionalLongStrict`/`GetOptionalDoubleStrict`
- [x] 8.2 解析失败抛出 `ConfigException`，信息含配置路径、期望类型、实际值
- [x] 8.3 关键调用点（clean/pageSize/timeoutMs 等 9 处）迁移到 strict 变体
- [x] 8.4 `yes`/`no`/`true`/`false` 正常解析

## Task 9: Draft 统一 bool coercion ✅
- [x] 9.1 新增 `ValueCoercion` 静态工具类（IsTruthy/IsFalsy/ToBooleanOrNull）
- [x] 9.2 `ContentPipeline` draft 过滤使用 `ValueCoercion.IsTruthy`
- [x] 9.3 `RouteInventoryValidator` draft 检查使用 `ValueCoercion.IsTruthy`

## Task 10: --jobs 贯穿全部渲染阶段 ✅
- [x] 10.1 `RenderSpecialListsAsync` 接受 `maxDegreeOfParallelism` 参数
- [x] 10.2 `BuildPageInfosAsync` 接受并使用 `maxDegreeOfParallelism`
- [x] 10.3 `RenderPipeline.ExecuteAsync` 传递 `MaxDegreeOfParallelism`

## Task 11: 测试补全
- [x] 现有 1315 测试全部通过（893 Bukit.Engine.Tests + 422 Bukit.Cli.Tests）
- [ ] 11.1 P0 增量构建专门测试（原有测试覆盖了基础增量逻辑，新增 RenderDependencyHash 可通过集成测试验证）
- [ ] 11.2 P0 输出安全专门测试
- [ ] 11.3 P0 dotfile 专门测试
- [ ] 11.4 P1 路由安全专门测试（RouteSecurityValidator 已有 Urls/OutputPaths/Slugs 测试覆盖基本场景）

# Task Dependencies
- 所有依赖已解除，Task 1-10 全部完成
- Task 11 为补充性测试，可在后续迭代中完成
