# 审计 build 引擎多源 Notion + 测试补齐 计划

## 1. 审计：build 引擎 content.sources 多源 Notion 支持

### 结论：✅ 完整支持，无需修改

**链路追踪：**

| 环节                                           | 代码                                                                                                                                                                        |  状态 |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :-: |
| SiteConfigGenerator 生成 `content.sources:` 数组 | [L93-L110](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs#L93-L110) — `sources: [{- type: notion, collection: page, notion: {...}}]` |  ✅  |
| ConfigCollectionReader.ReadSources 解析数组      | [L203-L223](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigCollectionReader.cs#L203-L223) — 读取 `collection`, `addToCollections`, `notion` 等字段            |  ✅  |
| ContentSourceConfig 模型                       | [L214-L223](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs#L214-L223) — `Collection`, `AddToCollections`, `Notion` 字段齐全                           |  ✅  |
| ConfigValidator 校验                           | [L40-L78](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs#L40-L78) — 校验 type/name唯一性/notion必填/mode                                           |  ✅  |
| ContentProviderFactory 创建多源                  | [L15-L78](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs#L15-L78) — 遍历 sources 创建 NotionContentProvider → CompositeContentProvider   |  ✅  |
| TaxonomyTermsInjector 多源                     | [L108-L110](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/TaxonomyTermsInjector.cs#L108-L110) — 同样处理 sources 数组                                               |  ✅  |
| ConfigDeprecationScanner                     | [L107-L108](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs#L107-L108) — 已标记旧 `provider: notion` 为 deprecated，建议迁移到 `sources`       |  ✅  |
| ConfigJsonSchemaGenerator                    | [L74-L82](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigJsonSchemaGenerator.cs#L74-L82) — schema 已包含 `sources` 定义                                       |  ✅  |
| NotionContentProvider 创建                     | [L128-L148](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs#L128-L148) — 正确读取 `NotionConfig` 并创建 provider                             |  ✅  |

**关键匹配点：**

* SiteConfigGenerator 输出 `collection: page` → ConfigCollectionReader 读 `collection` 字段 → ContentSourceConfig.Collection → ContentProviderFactory 用 `s.Collection` 创建 provider

* SiteConfigGenerator 输出 `notion: {databaseId: xxx, tokenEnv: xxx}` → ConfigCollectionReader 读 `notion` mapping → ContentSourceConfig.Notion → ContentProviderFactory 用 `s.Notion` 创建 NotionContentProvider

* 多源时 ContentProviderFactory 返回 `CompositeContentProvider(providers)` 而非单个 provider

**无 gap。build 引擎完整支持 SiteConfigGenerator 生成的多源 Notion site.yaml 格式。**

***

## 2. 全量测试

执行 `dotnet test` 全量测试。当前期望 3,323 passed, 0 failed。

***

## 3. --strict warn 回归测试

### 现状

已有 4 个 `StrictMode = "fail"` 测试（strict-test, strict-script, strict-residue, link-validation-strict）。无 `StrictMode = "warn"` 测试。

### 新增测试

* [ ] **Test 1**: `Import_Strict_Warn_Succeeds` — `StrictMode = "warn"` + 有硬编码残留 → import 成功（不抛异常），警告出现在 result.Warnings 中

* [ ] **Test 2**: `Import_Strict_Warn_StillReportsDiagnostics` — warn 模式下 diagnostic 报告仍生成，但 severity ≤ warning

* [ ] **Test 3**: `Import_Strict_Fail_BackCompat` — 确认现有 `StrictMode = "fail"` 测试仍抛异常

***

## 实施顺序

```
Phase 1: 并行
├─ Task 1: 全量测试 dotnet test
└─ Task 2: 新增 3 个 --strict warn 回归测试

Phase 2:
└─ Task 3: 再次全量测试确认
```

