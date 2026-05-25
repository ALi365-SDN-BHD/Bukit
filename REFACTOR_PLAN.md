# SiteEngine Refactor Plan

## 目标

将 856 行的 monolithic `SiteEngine` 重构为清晰的分层架构，抽取可独立测试的 Pipeline 组件。

## 执行记录

### Stage 3.1-3.8：Pipeline 抽取

| Stage | Pipeline | 职责 | 状态 |
|-------|----------|------|------|
| 3.1 | `BuildPipeline` | 配置加载、主题解析、输出目录准备 | ✅ done |
| 3.2 | `ContentPipeline` | provider 创建、内容加载、draft 过滤、schema 校验 | ✅ done |
| 3.3 | `RoutePipeline` | 内容路由生成、list routes、路由冲突校验 | ✅ done |
| 3.4 | `RenderPipeline` | 页面渲染、特殊列表渲染、增量判断 | ✅ done |
| 3.5 | `AssetPipeline` | static/assets 同步、SCSS、图片优化、tokens、media | ✅ done |
| 3.6 | `SeoPipeline` | SEO index、diagnostics、render 回调 | ✅ done |
| 3.7 | `PluginPipeline` | after-build 插件、stale 删除、manifest 保存 | ✅ done |
| 3.8 | `BuildReportPipeline` | 日志、BuildVariantResult、audit report | ✅ done |

### Stage 4：消除中间层

| 动作 | 内容 | 状态 |
|------|------|------|
| 新建 `BuildManifestTracker` | 独立化 manifest 追踪 helper（`TrackXxx`、`SyncXxx`、`DeleteStaleXxx`） | ✅ done |
| 新建 `SiteBuildOrchestrator` | 承载 `BuildCoreAsync` + `BuildVariantAsync` + 所有编排逻辑 | ✅ done |
| 简化 `SiteEngine` | 856 行 → 136 行委托层 | ✅ done |

### Stage 5：统一双 BuildAsync 路径

| 动作 | 内容 | 状态 |
|------|------|------|
| `BuildOptionsToConfig` | 将 `BuildOptions` 映射为 `AppConfig` | ✅ done |
| `FixedContentProviderFactory` | 适配器：用给定 `IContentProvider` 注入 pipeline 链 | ✅ done |
| Legacy `BuildAsync` | 从 55 行独立渲染路径 → 16 行委托到 orchestrator | ✅ done |

### Stage 6：消除反射兼容层

| 动作 | 内容 | 状态 |
|------|------|------|
| `BuildStageMetricsCollector.Merge` | 将 `MergeStageMetrics` 逻辑移为实例方法 | ✅ done |
| 删除 `SiteEngine` 3 个 wrapper | `BuildCollectionRules`、`MergeStageMetrics`、`GetSeoAlternates` | ✅ done |
| 更新 `SiteEngineHelperTests` | 反射调用 → `SiteBuildOrchestrator.BuildCollectionRules` / `collector.Merge` | ✅ done |
| 更新 `SiteEngineHelperExtendedTests` | 反射调用 → `SeoPipeline.GetSeoAlternates` | ✅ done |

### Stage 7：合并 Orchestrator

| 动作 | 内容 | 状态 |
|------|------|------|
| `SiteBuildOrchestrator` 逻辑合入 `SiteEngine` | 取消中间委托层，692 行单类 | ✅ done |
| `SiteBuildOrchestrator.cs` 变死代码 | 零引用 | ✅ done |

### Stage 8：清理

| 动作 | 内容 | 状态 |
|------|------|------|
| 删除 `SiteBuildOrchestrator.cs` | 580 行死代码 | ✅ done |

### Stage 9：消除反射测试

| 动作 | 内容 | 状态 |
|------|------|------|
| 升级 `SeoAlternatesService` 7 方法为 `internal static` | `NormalizePageSize`、`GetSeoStringList`、`GetCollection`、`BuildTaxonomyRouteUrls`、`BuildPaginationRouteUrls`、`AddTaxonomyKindRoutes`、`BuildTaxonomyTermCounts` | ✅ done |
| `SiteEngineHelperTests` 改为直接 API 调用 | 删除全部反射 helper | ✅ done |
| `SiteEngineHelperExtendedTests` 改为直接 API 调用 | 删除全部反射 helper | ✅ done |

### Stage 10：消除 TaxonomyTermsInjector 反射

| 动作 | 内容 | 状态 |
|------|------|------|
| 升级 `NormalizeNotionFieldKey` 为 `internal static` | 纯工具函数，无副作用 | ✅ done |
| 升级 `GetOrCreateEnsureTermsMap` 为 `internal static` | 纯工具函数，无副作用 | ✅ done |
| `TaxonomyTermsInjectorTests` 改为直接 API 调用 | 删除全部反射 helper | ✅ done |

### Stage 11：性能回归测试

| 动作 | 内容 | 状态 |
|------|------|------|
| 新建 `BuildPipelinePerformanceTests` | `FullBuild_With10Pages_CompletesUnderThreshold_AndAllStageKeysPresent`（10 页 < 30s） | ✅ done |
| 新建 `BuildPipelinePerformanceTests` | `FullBuild_With1Page_ProducesAllExpectedOutputFiles`（1 页验证输出完整性） | ✅ done |

### Stage 12：代码质量审查 + Benchmark + 进一步精简

| 动作 | 内容 | 状态 |
|------|------|------|
| A | 代码审查 — 0 unused imports，`BuildCollectionRules` → `private`，`GetSeoAlternates` → `internal` 保留 | ✅ done |
| B | Benchmarks — 15/15 通过，无性能退化 | ✅ done |
| C1 | 新建 `ThemeBootstrapper` — 64 行 theme 初始化独立类 | ✅ done |
| C2 | 新建 `BuildOptionsMapper` — 22 行映射逻辑独立类 | ✅ done |
| C3 | 新建 `FixedContentProviderFactory` — 23 行适配器独立类 | ✅ done |
| C4 | 新建 `PrepareOutputDirectory` — 16 行 clean/recovery 私有方法 | ✅ done |
| C total | `SiteEngine` 692 → 592 行（-100/-14%） | ✅ done |

---

## 反射状态：全部消除 ✅

整个测试套件中零反射调用。

## 测试指标

| 指标 | 重构前 | 重构后 |
|------|--------|--------|
| Engine 测试数 | 891 | 893 |
| Benchmarks | 15 | 15（零退化） |
| 全量测试数 | 1954 | 1956 |
| 性能回归测试 | 0 | 2 |
| 反射测试 | 3 文件 | 0 |

---

## 最终架构

```
SiteEngine (692 lines)
│
├── public API
│   ├── BuildAsync(AppConfig, rootDir, overrides) → BuildPipeline → BuildCoreAsync
│   ├── BuildAsync(IContentProvider, BuildOptions) → BuildOptionsToConfig → BuildAsync
│   └── GetListRoutes (static)
│
├── BuildCoreAsync
│   ├── ConfigApplier + ConfigValidator + theme resolution + clean/recovery
│   ├── ContentPipeline
│   └── BuildSingleLanguageVariantAsync / BuildMultiLanguageAsync
│       └── BuildVariantAsync
│           ├── Theme resolution (manifest, registry, plugins)
│           ├── DataModuleBuilder + sourceData
│           ├── RoutePipeline
│           ├── TaxonomyTermsInjector + PluginRunner.RunDerivePagesAsync
│           ├── SiteModel + BuildManifest
│           ├── SeoPipeline
│           ├── RenderPipeline
│           ├── AssetPipeline
│           ├── PluginPipeline
│           └── BuildReportPipeline
│
└── Helpers
    ├── BuildManifestTracker (static)
    ├── BuildStageMetricsCollector.Merge (instance)
    ├── MergeSiteData (private)
    ├── ComputeCompositeTemplateHash (private)
    ├── EnsureOutputDirectoryCanBeCleaned (private)
    └── BuildOptionsToConfig + FixedContentProviderFactory (private)
```

## 指标对比

| 指标 | 重构前 | 重构后 |
|------|--------|--------|
| `SiteEngine` 行数 | 856 | 692 |
| 类数 | 1 | 1 orchestrator + 8 pipeline + 2 helper |
| 架构层数 | 1 扁平层 | 3 层（API / 编排 / pipeline） |
| 反射 wrapper | 内联 | 0 |
| 独立构建路径 | 2 | 1（统一 pipeline 链） |
| 测试数 | 1954 | 1954（零回归） |
