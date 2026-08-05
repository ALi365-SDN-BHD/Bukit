# Bukit Article Image Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 增加两项确定性、非阻断的文章图片诊断：文章主体空 alt 人工复核，以及文章 SEO 图片来自站点默认图。

**Architecture:** 新的内部 `SeoImageResolver` 返回 URL 和来源，`SeoModelBuilder` 继续向公开模板模型暴露相同 URL，但通过 internal 属性把来源传给 publish audit。语义 HTML 审计只检查文章主体中的空 alt；两个诊断均为 warning，不修改生成 HTML 或图片。

**Tech Stack:** .NET 10, Bukit SEO model builder, semantic HTML audit, xUnit.

## Global Constraints

- WP1-C 必须已完成并集成。
- 不新增配置、不下载图片、不执行视觉或 AI 分析。
- 图片来源固定为 `ExplicitField|ContentMedia|SiteDefault|None`。
- `seo_image -> og_image -> cover -> image -> record.Media -> site.seo.defaultImage` 的现有优先级不得改变。
- `publish.article_image_alt_empty_review` 仅 warning；`alt=""` 不改写、不计入 `publish.image_alt_missing`。
- 空 alt 检查只遍历 `<main>`/`<article>` 主体，不检查 header/footer/nav、图标或模板装饰图。
- `seo.article_image_uses_site_default` 仅在 content-backed Article/BlogPosting/NewsArticle 且来源明确为 `SiteDefault` 时触发。
- 不做一般性的跨页面重复图报错；本包只识别配置默认图来源。
- 不改变构建退出码和现有报告 schema；新增问题进入现有 issues 数组。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Rendering/Models.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoImageResolver.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoModelBuilder.cs \
  --changed src/Bukit-Core/Bukit.Engine/PublishAuditRules/SemanticHtmlAuditRules.cs \
  --changed src/Bukit-Core/Bukit.Engine/PublishAuditRules/ArticleImageAuditRules.cs \
  --changed tests/Bukit.Engine.Tests/SeoModelBuilderTests.cs \
  --changed tests/Bukit.Engine.Tests/SeoAuditReportWriterTests.cs
```

Expected: `unmappedFiles: []`; exact Engine specialty command is classified
`dotnet-serial`.

### Task 1: Preserve image-source provenance internally

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/SeoImageResolver.cs`
- Modify: `src/Bukit-Core/Bukit.Rendering/Models.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoModelBuilder.cs:23-50`
- Modify: `tests/Bukit.Engine.Tests/SeoModelBuilderTests.cs`

**Interfaces:**
- Produces: Rendering-owned internal `SeoImageSource`, Engine-owned `ResolvedSeoImage`, and `SeoImageResolver.ResolveForContent`.
- Consumes: existing SEO fields, content media and site default.

- [ ] **Step 1: Generate closure**

Include Rendering public model consumers even though the new property is internal. Expected: no unmapped files.

- [ ] **Step 2: Write source RED tests**

Cover every precedence level and assert both URL and source. Add
`SeoImageSource` beside `SeoModel` in `Bukit.Rendering/Models.cs`, where the
model can reference it without a reverse Engine dependency. Add
`ResolvedSeoImage` in the new Engine resolver file. Use these exact types:

```csharp
internal enum SeoImageSource
{
    None,
    ExplicitField,
    ContentMedia,
    SiteDefault
}

internal sealed record ResolvedSeoImage(string? Url, SeoImageSource Source);
```

- [ ] **Step 3: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 4: Implement resolver without changing output URL**

Move only the existing cascade into the resolver. Resolve absolute URL once through the existing `BuildMaybeAbsoluteUrl`. Add this non-public property to `SeoModel`:

```csharp
internal SeoImageSource ImageSource { get; init; }
```

Set `Og.Image`, `Twitter.Image` and JSON-LD image from `ResolvedSeoImage.Url` exactly as before.

- [ ] **Step 5: Run Engine tests to GREEN**

Existing SEO image byte expectations must remain unchanged.

### Task 2: Detect empty alt inside primary article content

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/PublishAuditRules/SemanticHtmlAuditRules.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoAuditReportWriterTests.cs`

**Interfaces:**
- Produces: `publish.article_image_alt_empty_review`.
- Consumes: primary `<main>`/`<article>` HTML selected by existing audit regexes.

- [ ] **Step 1: Write RED tests**

Cover double/single quoted empty and whitespace-only alt, nonempty alt, missing alt, header icon empty alt, and multiple body images. Missing alt must still produce only `publish.image_alt_missing`; empty alt must produce only the new review warning.

- [ ] **Step 2: Run Engine tests and confirm RED**

- [ ] **Step 3: Add a dedicated empty-alt regex and primary-content counter**

```csharp
private static readonly Regex EmptyAltAttributeRegex = new(
    "\\balt\\s*=\\s*(?:\"\\s*\"|'\\s*')",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

Select the first nonblank primary region exactly as the existing
`ContainsScriptShellWithoutReadableContent` method does, then inspect image
tags only inside that region. This avoids double counting when `<main>`
contains `<article>`. Report one issue per route with the count; do not emit
one issue per image.

- [ ] **Step 4: Run Engine tests to GREEN**

### Task 3: Warn when Article SEO image uses the configured default

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/PublishAuditRules/ArticleImageAuditRules.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/PublishAuditRules/SemanticHtmlAuditRules.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoAuditReportWriterTests.cs`

**Interfaces:**
- Produces: `seo.article_image_uses_site_default`.
- Consumes: `PublishDocument.SeoModel.ImageSource` and Article-family schema type.

- [ ] **Step 1: Write RED tests**

Assert warning for content-backed Article, BlogPosting and NewsArticle using SiteDefault. Assert no warning for ExplicitField, ContentMedia, FAQPage, list page or no image.

- [ ] **Step 2: Implement one rule entry point**

```csharp
internal static void Analyze(PublishDocument document, List<PublishAuditIssue> issues)
```

The rule checks content-backed scope, Article-family schema type and `ImageSource == SiteDefault`. Message states that the image is generic and requires editorial review; it must not state that indexing or AI citation failed.

- [ ] **Step 3: Run the exact Engine specialty test**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 4: Review and commit WP1-D**

Review output compatibility, regex scope and issue severity. Commit:

```bash
git commit -m "feat(audit): diagnose weak article images"
```
