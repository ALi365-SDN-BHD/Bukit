# Bukit NewsArticle Publisher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 `Article`、`BlogPosting`、`NewsArticle` 在配置站点组织时生成一致的 publisher，并由现有 SEO schema audit 以 warning 保护缺失或错误类型。

**Architecture:** 保留现有 Organization 归一化与 URL 安全逻辑，只扩展 Article-family 类型判定。验证器对三种文章类型使用同一 publisher 结构规则；没有 publisher 不阻断构建，错误类型不升级为排名或富结果失败。

**Tech Stack:** .NET 10, Bukit Engine JSON-LD builder, `System.Text.Json`, xUnit.

## Global Constraints

- 只处理 publisher；不得同时添加 `mainEntityOfPage`、`citation`、`isBasedOn`、新配置或新 schema。
- 没有配置 `site.seo.organization` 时继续省略 publisher。
- publisher 复用现有规范化 `organizationNode`，不得构造第二套组织字段。
- 审计严重度固定为 warning；本包不改变 CLI/build 退出码。
- 允许的 publisher `@type` 为 `Organization` 或 `NewsMediaOrganization`；必须有非空 `name`。
- 精确专项命令：`dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`，最终以 closure 输出为准。

---

### Task 1: Lock the missing NewsArticle behavior with tests

**Files:**
- Modify: `tests/Bukit.Engine.Tests/SeoPublisherJsonLdTests.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoSchemaValidatorCoverageTests.cs`

**Interfaces:**
- Consumes: `SeoModelBuilder.BuildForContent`, `SeoSchemaValidator.Validate`.
- Produces: failing coverage for `NewsArticle.publisher` generation and audit.

- [ ] **Step 1: Generate the package closure**

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Engine/SeoJsonLdBuilder.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoSchemaValidator.cs \
  --changed tests/Bukit.Engine.Tests/SeoPublisherJsonLdTests.cs \
  --changed tests/Bukit.Engine.Tests/SeoSchemaValidatorCoverageTests.cs
```

Expected: `unmappedFiles: []`; Engine tests are `dotnet-serial`.

- [ ] **Step 2: Extend the existing publisher theory**

Add the third row without creating a parallel test helper:

```csharp
[Theory]
[InlineData("BlogPosting")]
[InlineData("Article")]
[InlineData("NewsArticle")]
public void BuildForContent_ArticlePublisherMatchesNormalizedSiteOrganization(string schemaType)
```

Keep all existing assertions for type, name, absolute URL, logo and `sameAs`.

- [ ] **Step 3: Add audit RED tests**

Add three focused tests using the existing validator fixture:

```csharp
[Fact]
public void ExtractSchemaTypes_NewsArticleWithoutPublisher_Warns()
{
    var issues = new List<SeoAuditIssue>();
    SeoSchemaValidator.ExtractSchemaTypes(
        ["""{"@type":"NewsArticle","headline":"News","datePublished":"2026-08-05T00:00:00Z","author":{"@type":"Person","name":"Desk"},"image":"https://example.com/news.jpg"}"""],
        "/news/",
        issues);

    Assert.Contains(issues, issue =>
        issue.Code == "seo.schema_newsarticle_publisher_missing" &&
        issue.Severity == "warning");
}

[Theory]
[InlineData("Organization")]
[InlineData("NewsMediaOrganization")]
public void ExtractSchemaTypes_NewsArticleWithSupportedPublisher_DoesNotWarn(string publisherType)
{
    var json = $$"""{"@type":"NewsArticle","headline":"News","datePublished":"2026-08-05T00:00:00Z","author":{"@type":"Person","name":"Desk"},"image":"https://example.com/news.jpg","publisher":{"@type":"{{publisherType}}","name":"Example News"}}""";
    var issues = new List<SeoAuditIssue>();
    SeoSchemaValidator.ExtractSchemaTypes([json], "/news/", issues);

    Assert.DoesNotContain(issues,
        issue => issue.Code.Contains("publisher", StringComparison.Ordinal));
}
```

Add one invalid-type case using `Person` and expect `seo.schema_newsarticle_publisher_type_invalid` warning.

- [ ] **Step 4: Run the Engine project and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

Expected: publisher theory fails for `NewsArticle`; new validator assertions fail because publisher rules do not exist.

### Task 2: Implement the shared Article-family publisher rule

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/SeoJsonLdBuilder.cs:228-233`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoSchemaValidator.cs:235-267`

**Interfaces:**
- Consumes: normalized `organizationNode`, Article-family `schemaType`.
- Produces: `IsArticleFamilyType(string)` and conditional publisher audit codes using the existing type prefix.

- [ ] **Step 1: Centralize the family predicate**

Add one private helper and use it in the publisher branch:

```csharp
private static bool IsArticleFamilyType(string schemaType)
    => schemaType is "Article" or "BlogPosting" or "NewsArticle";
```

If current code requires case-insensitive support, implement the same three comparisons with `StringComparison.OrdinalIgnoreCase`; do not mix comparison rules between builder and tests.

- [ ] **Step 2: Validate publisher shape after existing author/image checks**

```csharp
if (!node.TryGetProperty("publisher", out var publisher) || IsEmptySchemaValue(publisher))
{
    issues.Add(Warning($"{prefix}_publisher_missing", routeUrl,
        $"{type} JSON-LD should include publisher when publisher identity is available."));
}
else if (publisher.ValueKind != JsonValueKind.Object ||
         !HasSupportedPublisherType(publisher) ||
         !HasNonEmptyString(publisher, "name"))
{
    issues.Add(Warning($"{prefix}_publisher_type_invalid", routeUrl,
        $"{type} publisher should be an Organization or NewsMediaOrganization with a non-empty name."));
}
```

`HasSupportedPublisherType` reads `@type` through the existing `ReadTypes` helper. Do not accept arbitrary `Thing` or string publisher values.

- [ ] **Step 3: Run the exact specialty test**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

Expected: exit `0`; existing FAQPage non-publisher test remains green.

- [ ] **Step 4: Perform the one specialty review**

Review only the four closure files. Confirm no JSON-LD change for FAQPage/HowTo/WebPage, no new error severity, and no organization normalization duplication. Critical/Important must be zero.

- [ ] **Step 5: Commit WP0**

```bash
git add \
  src/Bukit-Core/Bukit.Engine/SeoJsonLdBuilder.cs \
  src/Bukit-Core/Bukit.Engine/SeoSchemaValidator.cs \
  tests/Bukit.Engine.Tests/SeoPublisherJsonLdTests.cs \
  tests/Bukit.Engine.Tests/SeoSchemaValidatorCoverageTests.cs
git diff --cached --name-only
git commit -m "fix(seo): emit NewsArticle publisher"
```

Expected staged paths: exactly the four files above.
