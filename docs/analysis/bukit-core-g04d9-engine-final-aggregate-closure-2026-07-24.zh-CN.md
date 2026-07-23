# Bukit Core G-04D9 Engine 56 项决策汇总

> 日期：2026-07-24
>
> 任务：G-04 Group 4 / Task 41
>
> G4 基线：`2.0@729088dbc2faf1bf7a20fe670e96a09b7568e7ba`
>
> 状态：decision-consolidated / g4-verification-pending

## 1. 汇总结论

56 个 Engine historical/current candidates 已逐项获得唯一终态：

| Cluster | 总数 | internalized | retained public |
|---|---:|---:|---:|
| D9A build orchestration | 7 | 4 | 3 |
| D9B content validation/stage | 9 | 2 | 7 |
| D9C filesystem/output | 9 | 9 | 0 |
| D9D feed/SEO/sitemap | 8 | 7 | 1 |
| D9E built-in plugins | 13 | 13 | 0 |
| D9F Notion fetch | 2 | 2 | 0 |
| D9G plugin source/capability | 3 | 3 | 0 |
| D9H list/template capability | 5 | 1 | 4 |
| **合计** | **56** | **41** | **15** |

current baseline 的条件投影已实现：

```text
14 assemblies / 443 public types / 0 candidates
```

这是 source/baseline 终态，尚不是 Task 42 已验证结论。

## 2. 56/56 终态

### D9A（7）

- internalized：`BuildPipeline`、`BuildPipelineContext`、`RoutePipeline`、
  `RoutePipelineResult`；
- retained：`BuildOptions`、`BuildVariantSummary`、`ContentPipelineResult`。

### D9B（9）

- internalized：`ContentCollectionContractValidator`、
  `ContentSchemaValidator`；
- retained：`ContentValidationIssue`、`IContentProviderFactory`、
  `ITemplateRenderer`、`ContentStageInput`、`ContentStageOutput`、
  `IContentStage`、`TemplateRendererBase`。

### D9C（9）

全部 internalized：`DirectoryCopy`、`DirectoryCopyOptions`、`FileWriter`、
`Incremental.HashUtil`、`IOutputFileSystem`、`IOutputPathPolicy`、
`OutputPathSecurityException`、`SafeOutputFileSystem`、`SafePathResolver`。

### D9D（8）

- internalized：`AtomFeedGenerator`、`JsonFeedGenerator`、
  `SitemapGenerator`、`SitemapGenerator.Alternate`、
  `SitemapGenerator.UrlEntry`、`SeoAlternatesService`、
  `SeoInjectionPolicy`；
- retained：`RssGenerator`，以保持稳定 nested `RssGenerator.Post` 可达。

### D9E（13）

全部 internalized：Alias、Archive、DataFiles、Feed、ImageProcessing、
LlmsTxt、Menu、PagesIndex、Pagination、RelatedContent、SearchIndex、
Sitemap、Taxonomy built-in plugin classes。

静态归属仍为 9 registry-owned + 4 aggregate-only，非候选
`AnalyticsPlugin` 继续注册。

### D9F（2）

`INotionPageFetcher`、`NotionFetchedPage` 全部 internalized。

### D9G（3）

`BuiltInPluginSource`、`IPluginSource`、`PluginCapability` 全部
internalized。

### D9H（5）

- internalized：`SpecialListRouteBuilder`；
- retained：`TemplateCapabilitiesResolver.ListPageContentResolution`、
  `TemplateCapabilityFlags`、`TemplateFieldDeclaration`、
  `TemplateVariableWarning`。

## 3. Retained public 原则

15 项 retained 均改为
`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`。
保留原因不是“忘记收窄”，而是 stable public parent signature、真实 protected
extension seam 或稳定 nested companion：

- 不收窄 `SiteEngine`、`BuildResult`、`ContentPipeline`；
- 不删除 public stage/provider/renderer composition seam；
- 不隐藏 `RssGenerator.Post`；
- 不修改 template resolver/linter public return types。

任何父 facade redesign、renderer replacement、stage injection removal 或 new DTO
migration 必须另立 2.0 API 任务。

## 4. 治理不变量

historical manifest 完全不修改：

```text
declarationState = closed
candidateCount = 136
candidates.length = 136
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

历史记录继续承担迁移追踪；current baseline 只描述当前 exported CLR surface。
private、未索引、未自愿声明消费者仍为未知。

独立 JSON Feed output-path P1 已在 G4 基线前关闭，不计入 visibility diff，不在
D9D 重复实施。

## 5. Task 42 验证矩阵

Task 42 必须一次性执行并记录：

| 面 | 必须验证 |
|---|---|
| Engine | build/context/cancel、content stages、filesystem/security、feeds/SEO/sitemap、built-ins、Notion、registry、list/template |
| CLI | `BuildOptions`/content result/doctor 等跨程序集消费者编译与行为 |
| Architecture | D9A～D9H 新断言、历史 G-04 断言、baseline `14/443/0`、manifest blob |
| Public API | compiled exported types 与 baseline 无 drift；41 项不 exported；15 项 retained |
| Security | F-01/F-03/F-04、JSON Feed safe destination、external image no-fetch |
| AOT | static registration、serialization roots、published CLI/package smoke |
| Docs | active governance/current counts/links/size policy |

G4 aggregate targeted gate 只能执行一次。full/release/whole-solution tests 仍不在授权
范围。环境阻塞必须与真实回归分开记录。

## 6. Task 41 边界

Task 41 只汇总已提交决策，不运行 tests、focused gate、aggregate、AOT 或复审，
也不新增 production、baseline 或测试变更。Task 42 完成前状态保持
`g4-verification-pending`。
