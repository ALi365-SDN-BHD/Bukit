# 按改动类型定位源码入口

本文档面向维护者与功能开发者，解决一个非常实际的问题：

> **当你准备修改某一类能力时，第一眼应该去看哪些文件，改完后又该如何验证？**

与“架构总览”不同，这份手册不是按模块介绍，而是按**改动类型**反向定位源码入口。

## 1. 使用方式

当你准备动手前，先判断自己属于哪一种改动：

- 改命令、参数、配置读取
- 改 Markdown / Notion / 多源内容接入
- 改 URL、输出路径、模板选择
- 改主题、模板变量、页面 HTML
- 改 sitemap/rss/search/taxonomy 等输出
- 改增量构建、跳过渲染、缓存行为

找到对应章节后，按以下顺序看：

1. 主入口
2. 相关次入口
3. 常见改动点
4. 建议验证方式
5. 相关测试

## 2. 改 CLI / 参数 / 配置

### 2.1 什么时候看这一章

- 新增一个 CLI 命令
- 新增或修改 `build/doctor/preview/theme/intent` 参数
- 改 `--config` / `--site` 的寻址规则
- 改 `site.yaml` 字段、默认值或校验逻辑

### 2.2 第一入口

- `src/Bukit.Cli/Program.cs`
- `src/Bukit.Cli/Commands/BuildCommand.cs`
- `src/Bukit.Cli/ConfigPathResolver.cs`
- `src/Bukit.Config/ConfigLoader.cs`
- `src/Bukit.Config/ConfigValidator.cs`

### 2.3 常见改动落点

| 需求 | 先看哪里 |
|---|---|
| 新命令或命令分发 | `Program.cs` |
| `build` 参数行为变化 | `BuildCommand.cs` |
| `--config` / `--site` 规则 | `ConfigPathResolver.cs` |
| YAML 读取与字段装配 | `ConfigLoader.cs` |
| 字段合法性与错误提示 | `ConfigValidator.cs` |
| CLI 覆盖值优先级 | `ConfigOverrides.cs` |

### 2.4 建议验证

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
```

### 2.5 重点测试

- `tests/Bukit.Cli.Tests/ConfigPathResolverTests.cs`
- `tests/Bukit.Engine.Tests/ConfigValidatorTests.cs`

## 3. 改内容源接入

### 3.1 什么时候看这一章

- 改 Markdown front matter 解析
- 改 Notion 字段映射、页面拉取、块渲染
- 改多源 `content.sources` 聚合
- 改内容图片本地化

### 3.2 第一入口

- `src/Bukit.Engine/ContentProviderFactory.cs`
- `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit.Content/Notion/NotionContentProvider.cs`
- `src/Bukit.Content/CompositeContentProvider.cs`

### 3.3 常见改动落点

| 需求 | 先看哪里 |
|---|---|
| 决定用哪个 provider | `ContentProviderFactory.cs` |
| Markdown 文件扫描 / 摘要 / Fields | `MarkdownFolderProvider.cs` |
| Notion database / property / block 处理 | `NotionContentProvider.cs` |
| 多源并发聚合与 source 元信息 | `CompositeContentProvider.cs` |
| 图片下载与本地替换 | `ContentProviderFactory.cs` |

### 3.4 建议验证

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
```

若改动的是 Notion：

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config <your-site.yaml>
```

### 3.5 重点测试

- `tests/Bukit.Content.Tests/NotionApiClientTests.cs`

## 4. 改路由 / URL / 输出路径

### 4.1 什么时候看这一章

- 改 `post/page` 兼容 URL 规则
- 改 `site.collections` 的 permalink/template/list 策略
- 改 `site.permalinks`
- 改 `route/url/outputPath/template` override
- 改输出路径编码、slug 化、安全规则

### 4.2 第一入口

- `src/Bukit.Routing/RouteGenerator.cs`
- `src/Bukit.Engine/SiteEngine.cs`

### 4.3 常见改动落点

| 需求 | 先看哪里 |
|---|---|
| collections 路由与模板规则 | `SiteEngine.BuildCollectionRules` + `RouteGenerator.Generate` |
| 默认 `/blog/...`、`/pages/...` 规则 | `RouteGenerator.Generate` |
| permalink 模式 | `BuildFromPermalink` / `ExpandPermalinkPattern` |
| route override | `TryReadRouteOverride` |
| 输出路径编码 | `NormalizeOutputPath` / `ApplyOutputPathEncoding` |
| 引擎接入路由位置 | `SiteEngine.BuildVariantAsync` |

### 4.4 建议验证

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter RouteGenerator
```

### 4.5 重点测试

- `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`

## 5. 改渲染 / 主题 / 模板变量

### 5.1 什么时候看这一章

- 改模板目录约定
- 改 `site` / `page` / `pages` / `modules` 暴露给模板的变量
- 改 Scriban layout/include 行为
- 改页面与列表页的渲染逻辑
- 改 assets / static / uploads 的复制策略

### 5.2 第一入口

- `src/Bukit.Engine/BuildPathUtils.cs`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- `src/Bukit.Rendering/Scriban/ScribanModelBinder.cs`
- `src/Bukit.Rendering/Scriban/FileTemplateLoader.cs`
- `src/Bukit.Engine/PageRenderDispatcher.cs`

### 5.3 常见改动落点

| 需求 | 先看哪里 |
|---|---|
| 主题目录解析 | `BuildPathUtils.ResolveThemeDirectories` |
| 模板渲染入口 | `ScribanTemplateRenderer.cs` |
| layout 指令 / 模板缓存 | `ScribanTemplateRenderer.cs` |
| include 加载与路径边界 | `FileTemplateLoader.cs` |
| 模型如何暴露给模板 | `ScribanModelBinder.cs` |
| 页面/列表页面渲染 | `PageRenderDispatcher.cs` |
| assets/static/media 拷贝 | `SiteEngine.cs` |

### 5.4 建议验证

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
```

### 5.5 重点测试

- `tests/Bukit.Rendering.Tests/ScribanTemplateRendererTests.cs`
- `tests/Bukit.Rendering.Tests/FileTemplateLoaderTests.cs`

## 6. 改插件 / 输出产物

### 6.1 什么时候看这一章

- 新增一个内置或外部插件
- 改 derive-pages / after-build 生命周期
- 改 sitemap/rss/search/taxonomy/path-report 等输出
- 改插件失败策略或插件执行顺序

### 6.2 第一入口

- `src/Bukit.Engine/Plugins/PluginRegistry.cs`
- `src/Bukit.Engine/Plugins/PluginRunner.cs`
- `src/Bukit.Engine.Abstractions/Plugins/BuildContext.cs`
- `src/Bukit.Engine/Plugins/BuiltIn/*`

### 6.3 常见改动落点

| 需求 | 先看哪里 |
|---|---|
| 插件发现与注册 | `PluginRegistry.cs` |
| derive-pages 执行链 | `PluginRunner.RunDerivePages` |
| after-build 执行链 | `PluginRunner.RunAfterBuild` |
| 插件可访问的数据 | `BuildContext.cs` |
| 搜索索引输出 | `SearchIndexPlugin.cs` |
| sitemap/rss 输出 | `SitemapPlugin.cs` / `RssPlugin.cs` |
| taxonomy 输出 | `TaxonomyPlugin.cs` |
| 调试/报告插件 | `src/plugins/PathReportPlugin/*` |

### 6.4 建议验证

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
```

构建后重点检查：

- `dist/sitemap.xml`
- `dist/rss.xml`
- `dist/search.json`
- `dist/taxonomy.json`

### 6.5 重点测试

- `tests/Bukit.Engine.Tests/PluginRunnerTests.cs`
- `tests/Bukit.Engine.Tests/SitemapPluginTests.cs`
- `tests/Bukit.Engine.Tests/PathReportPluginTests.cs`
- `tests/Bukit.Engine.Tests/TaxonomyEnsureTermsTests.cs`
- `tests/Bukit.Engine.Tests/TaxonomyPinningTests.cs`

## 7. 改增量构建 / 跳过渲染 / 缓存

### 7.1 什么时候看这一章

- 页面没变却被重复渲染
- 明明改了模板或内容但页面没刷新
- 想改 manifest 结构、缓存路径、跳过原因统计
- 想调构建性能或并行渲染行为

### 7.2 第一入口

- `src/Bukit.Engine/SiteEngine.cs`
- `src/Bukit.Engine/PageRenderDispatcher.cs`
- `src/Bukit.Engine/Incremental/BuildManifest.cs`
- `src/Bukit.Engine/Incremental/HashUtil.cs`

### 7.3 常见改动落点

| 需求 | 先看哪里 |
|---|---|
| manifest 路径与启用逻辑 | `SiteEngine.cs` |
| 单页跳过判定 | `PageRenderDispatcher.cs` |
| 列表页跳过判定 | `PageRenderDispatcher.cs` |
| manifest 读写结构 | `BuildManifest.cs` |
| 模板目录哈希 | `HashUtil.cs` |
| 构建结束清理与保存 | `SiteEngine.cs` |

### 7.4 建议验证

先执行两轮构建：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --no-clean --incremental
```

再分别尝试：

- 修改 1 个 Markdown 内容文件
- 修改 1 个模板文件
- 修改 1 条 permalink 或 route 规则

观察：

- 日志中的 `rendered / skipped`
- `.cache/build-manifest*.json`
- 正文读取路径是否走 `BodyStore + BodyKey`（避免把正文当作默认常驻元数据处理）

## 8. 仓库边界说明

当前仓库聚焦 `Bukit` 主线，不包含 [BukitJalil](https://github.com/ALi365-SDN-BHD/BukitJalil) 相关源码与解决方案。

## 9. 快速决策表

| 你要改什么 | 第一站 |
|---|---|
| 命令或参数 | `Program.cs` / `BuildCommand.cs` |
| 配置字段或校验 | `ConfigLoader.cs` / `ConfigValidator.cs` |
| Markdown / Notion 接入 | `ContentProviderFactory.cs` / `MarkdownFolderProvider.cs` / `NotionContentProvider.cs` |
| 页面 URL 与输出路径 | `RouteGenerator.cs` |
| 模板变量与渲染 | `ScribanModelBinder.cs` / `ScribanTemplateRenderer.cs` |
| 页面写出与列表页 | `PageRenderDispatcher.cs` |
| 搜索、RSS、sitemap、taxonomy | `PluginRunner.cs` + `Plugins/BuiltIn/*` |
| 缓存与跳过渲染 | `SiteEngine.cs` / `PageRenderDispatcher.cs` / `BuildManifest.cs` |
| 当前仓库未包含的模块 | [BukitJalil](https://github.com/ALi365-SDN-BHD/BukitJalil) 相关入口请忽略，以 `Bukit.*` 工程为准 |

## 10. 推荐搭配阅读

- 仓库总览：[`code-wiki.md`](./code-wiki.md)
- 模块调用关系图：[`code-wiki-call-graph.md`](./code-wiki-call-graph.zh-CN.md)
- 新开发者路线：[`new-developer-30min.md`](./new-developer-30min.zh-CN.md)
- 架构边界：[`architecture.md`](./architecture.md)
- 插件体系：[`plugins.md`](./plugins.md)

## 11. 一句话总结

维护这个仓库时，最省时间的方法不是“先全局搜索”，而是**先按改动类型找到正确入口层，再沿主入口 → 次入口 → 测试与验证路径推进**。
