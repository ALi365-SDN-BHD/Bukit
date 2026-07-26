# TA-01 Bukit Core 定向修复报告

## 范围与基线

- BASE_HEAD：`8053748526aad9c8c8a2b98b1530e8800953a272`
- 分支：`codex/seo-geo-ta01-core`
- 范围：canonical author ownership 投影、空 filtered-list 索引策略、SEO/publish 审计日期模型。
- 未修改 SRBiz、公开抽象合同、配置合同、插件 lock 或构建 manifest。
- 未运行审计脚本、`post-change-*`、全方案测试或未定义门禁。

## TDD RED 证据

### Author profile

命令：

```text
env -u NOTION_TOKEN dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~CanonicalContentGraphBuilder_DoesNotProjectAuthorProfileTypeAsOwnershipWithoutAuthor'
```

结果：`Failed 1 / Passed 0`。非文章 `author-profile` 且没有 canonical author 时，raw `authorType=Organization` 被错误投影到 `Ownership.AuthorType`。

### 空 filtered-list

命令：

```text
env -u NOTION_TOKEN dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Build_EmptyFilteredCollectionInheritsNoindexPolicyAndAggregateExclusions|FullyQualifiedName~Build_EmptyListEpochSentinelIsNullInSeoAndPublishAuditModels'
```

结果：`Failed 2 / Passed 0`。空 `FilteredListPage` 没有继承 collection 的 `noindexWhenEmpty`；SEO 审计模型仍暴露 `1970-01-01`。

新 worktree 初始没有 NuGet `obj/project.assets.json`，首次 `--no-restore` 命令被 MSBuild 跳过且零测试输出，未计作 RED 或 GREEN。执行一次定向 `dotnet restore` 后取得上述真实 RED。

## 最小实现

1. `CanonicalContentGraphBuilder`
   - raw `authorType` 仅在存在 canonical author，或内容具有 `article`/`post`、Article schema、`seo_article` 语义时投影到 canonical ownership。
   - 非文章 author-profile/page 的实体类型字段保持在 raw custom fields，不再误作 ownership。
   - authored content 仍保留 orphaned `AuthorType`，因此既有 `canonical_author_type_without_author` 诊断继续生效。

2. `SeoIndexBuilder`
   - `FilteredListPage` 在集合启用 `noindexWhenEmpty` 且结果为空时，与主集合列表采用相同 `noindex,follow` 策略。
   - 路由继续生成，不使用 `emptyBehavior: skip`。
   - `SeoIndexEntry.LastModified` 继续保留内部 `UnixEpoch` 哨兵，避免改变公开索引条目合同。

3. SEO/publish 审计模型
   - `PublishDocumentBuilder.NormalizeLastModified` 只在审计投影边界把精确 `UnixEpoch` 转换为 `null`。
   - SEO audit route 与 publish audit document 均不再暴露 Epoch；正常日期不变。

## GREEN 证据

命令：

```text
env -u NOTION_TOKEN dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~CompanyEntityAndEmptyCollectionTests|FullyQualifiedName~ContentSchemaValidatorExtendedTests'
```

最终结果：`Failed 0 / Passed 22 / Skipped 0`，耗时约 `5.6s`。

首次完整运行结果为 `Passed 21 / Failed 1`：测试错误地要求在 `llmsFullTxt` 默认关闭时仍存在 `llms-full.txt`。断言被定向修正为“文件不存在或不包含该路由”，生产实现未因该失败扩大。

## 自审

- `git diff --check`：通过。
- 没有硬编码 SRBiz slug；测试路由仅为受控行为 fixture。
- 非文章无 author、文章无 author、存在 author 三个 ownership 分支均有明确行为保护。
- 空 filtered-list 的 sitemap/search/llms/llms-full 聚合输出不包含路由。
- 内部 Epoch 哨兵仍存在，只有 SEO/publish 审计模型归一为 `null`。
- Critical：0；Important：0；Minor：0。
