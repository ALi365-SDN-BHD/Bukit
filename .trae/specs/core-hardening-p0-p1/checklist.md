# Core Hardening P0-P1 Checklist

## P0: 增量构建依赖指纹
- [ ] `RenderDependencyHasher.Compute` 存在且返回 deterministic hash
- [ ] `BuildManifestEntry.RenderDependencyHash` 字段存在
- [ ] `PageRenderDispatcher.RenderPagesAsync` skip 逻辑包含 `RenderDependencyHash` 比较
- [ ] `PageRenderDispatcher.RenderSpecialListIfNeededAsync` skip 逻辑包含 `RenderDependencyHash` 比较
- [ ] 旧 manifest（`RenderDependencyHash` 为 null/空）触发重新渲染，不崩溃
- [ ] 修改 `site.title` 后页面重新渲染
- [ ] 修改 `theme.params` 后页面重新渲染
- [ ] 修改 `site.description` 后页面重新渲染
- [ ] 修改 `baseUrl` 后页面重新渲染
- [ ] 修改 `site.analytics` 后页面重新渲染
- [ ] 修改 SEO 默认图后页面重新渲染
- [ ] 修改 shortcode 映射后页面重新渲染
- [ ] 修改 component 映射后页面重新渲染
- [ ] 修改 collections 后页面重新渲染
- [ ] 修改 plugin toggles 后页面重新渲染
- [ ] 修改 `site.data` 后页面重新渲染
- [ ] 构建报告显示 `render_dependency_changed` 原因
- [ ] `dotnet test` 所有增量构建测试通过

## P0: Static HTML 冲突检查
- [ ] 无 `staticTemplate` 配置时 static HTML 与 content page 冲突 → 构建失败
- [ ] 有 `staticTemplate` 配置时 static HTML 与 list page 冲突 → 构建失败
- [ ] static 非 HTML 文件覆盖 generated page 输出路径 → 构建失败
- [ ] 错误信息包含两个冲突来源与输出路径
- [ ] 正常无冲突场景构建通过

## P0: 统一 Safe Output FileSystem
- [ ] `StaticFileService.RenderStaticFiles` 非 HTML 复制经过安全校验
- [ ] `DirectoryCopy.Sync` 所有变体经过安全校验
- [ ] `AssetPipeline` 所有复制经过安全校验
- [ ] `BuildManifestTracker` 文件操作经过安全校验
- [ ] 全仓库搜索 `File.Copy(` 输出目录相关无不安全调用
- [ ] 全仓库搜索 `Path.Combine(outputDir` 无不安全调用

## P0: Dotfile deny list
- [ ] `IgnoreDotPrefixedFiles` 默认值为 `true`
- [ ] `.env` 默认不发布
- [ ] `.git/` 默认不发布
- [ ] `.DS_Store` 默认不发布
- [ ] `*.pem` / `*.key` / `*.pfx` 默认不发布
- [ ] `.well-known/` 默认发布
- [ ] `build.publishDotFiles: true` 可显式启用 dotfile 发布
- [ ] 构建日志提示跳过的敏感文件

## P1: URL 段校验
- [ ] `/../admin/` → 校验失败
- [ ] `/%2e%2e/private/` → 校验失败
- [ ] `/%2E%2E/private/` → 校验失败
- [ ] `/a\b` → 校验失败
- [ ] `//evil.com/x` → 校验失败
- [ ] `https://evil.com/x` → 校验失败
- [ ] 正常中文 slug 通过
- [ ] 正常英文 slug 通过
- [ ] 多层路径通过

## P1: Top-level outputPath 废弃
- [ ] top-level `outputPath: custom/index.html` → 抛出 `ConfigException`，含 "deprecated" 和迁移指引
- [ ] `route.outputPath: custom/index.html` → 正常工作
- [ ] `route.url: /custom/` → 正常工作

## P1: collections.yaml 解析失败报错
- [ ] collections.yaml 非法 YAML → 抛出 `ConfigException`，含文件路径
- [ ] collections.yaml 不存在 → 正常回退
- [ ] collections.yaml 合法 → 正常加载

## P1: 配置严格解析
- [ ] `clean: fasle`（拼写错误）→ 抛出 `ConfigException`，含 "expected boolean"
- [ ] `pageSize: ten` → 抛出 `ConfigException`，含 "expected integer"
- [ ] `yes` / `no` / `true` / `false` → 正常解析
- [ ] `-1` / `0` 等合法数值 → 正常解析

## P1: Draft 统一 bool coercion
- [ ] `draft: true` → 过滤
- [ ] `draft: "TRUE"` → 过滤
- [ ] `draft: "true"` → 过滤
- [ ] `draft: "yes"` → 过滤
- [ ] `draft: 1` → 过滤
- [ ] `draft: "on"` → 过滤
- [ ] `draft: false` → 不过滤
- [ ] `draft: "no"` → 不过滤
- [ ] `draft: 0` → 不过滤
- [ ] `ValueCoercion` 类存在于 `Bukit.Shared` 命名空间

## P1: --jobs 贯穿渲染
- [ ] `--jobs 1` → 列表页单并发渲染
- [ ] `--jobs 4` → 列表页最多 4 并发
- [ ] `--jobs` 不指定 → 使用 `Environment.ProcessorCount`
- [ ] `--jobs` 0 或负数 → 回退到 `Environment.ProcessorCount`

## 回归验证
- [ ] `dotnet build` 通过，无错误无警告
- [ ] `dotnet test` 全部通过，含新增测试
- [ ] 现有 starter 示例站点构建成功，输出正确
- [ ] `dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release` 成功（AOT 兼容性）
