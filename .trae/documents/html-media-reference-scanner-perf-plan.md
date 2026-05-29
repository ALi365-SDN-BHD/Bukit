# HtmlMediaReferenceScanner.Find 性能优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 HtmlMediaReferenceScanner.Find 从逐字符扫描 + 嵌套分配优化为向量化跳转 + 零冗余分配，预计在 1000 页 Notion 构建中实现 30–60% 的扫描器 CPU 下降和显著的 Gen0 GC 减少。

**Architecture:** 保留当前字符级扫描器的整体结构（标签名读取 → FindTagEnd → ScanAttributes → TryClassify），但将热点路径替换为 Span + IndexOf 向量化操作 + 延迟字符串物化 + 去重 HtmlDecode。同时优化下游 ContentImageRewritePipeline 的双遍历和重复 Regex。

**Tech Stack:** C# / .NET 10 / ReadOnlySpan<char> / MemoryExtensions.IndexOf / xUnit

---

## 修改文件总览

| 文件 | 变更类型 | 职责 |
|------|----------|------|
| `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs` | **重写** | 核心扫描器优化 |
| `src/Bukit.Content/Media/ContentImageRewritePipeline.cs` | **修改** | 消费端去重解码/双遍历 |
| `tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs` | **扩展** | 新增边界/性能相关测试 |

---

### Task 1: 向量化跳转 + 等号预检查（P1 + P5）

**Files:**
- Modify: `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs:23-75`

- [ ] **Step 1: 写失败测试** — 添加一个无媒体标签的长 HTML 测试（验证快速返回）

```csharp
[Fact]
public void HtmlMediaReferenceScanner_FastReturnForNonMediaHtml()
{
    var html = string.Join("", Enumerable.Range(0, 100).Select(_ => "<p>Some text</p>"));
    Assert.Empty(HtmlMediaReferenceScanner.Find(html));
}
```

- [ ] **Step 2: 运行测试验证通过**（当前实现已返回空，此测试验证回归保护）

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "HtmlMediaReferenceScanner_FastReturnForNonMediaHtml"`
Expected: PASS

- [ ] **Step 3: 替换外层 `i++` 逐字符推进为 `IndexOf('<')` 向量化跳转**

将 `Find` 方法的外层循环从：
```csharp
while (i < length)
{
    if (html[i] != '<') { i++; continue; }
```
改为：
```csharp
var span = html.AsSpan();
while (i < length)
{
    var remaining = span.Slice(i);
    var nextLt = remaining.IndexOf('<');
    if (nextLt < 0) break;
    i += nextLt;
```

- [ ] **Step 4: 添加等号预检查**（P5）

在 `FindTagEnd` 之后、`ScanAttributes` 之前插入：
```csharp
var tagBody = html.AsSpan(nameEnd, tagEnd - nameEnd);
if (tagBody.IndexOf('=') < 0)
{
    i = tagEnd + 1;
    continue;
}
```
并删除 `MayHaveMediaAttributes` 方法。

- [ ] **Step 5: 运行全部 ContentImageRewritePipeline 测试**

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "ContentImageRewritePipeline"`
Expected: 17 passed, 0 failed

- [ ] **Step 6: Commit**

```
feat(scanner): use IndexOf('<') vectorized skip + equals-precheck
```

---

### Task 2: 替换 char.IsWhiteSpace 为 ASCII 版本（P4）

**Files:**
- Modify: `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs:114-205`

- [ ] **Step 1: 添加 ASCII whitespace 辅助方法**

```csharp
private static bool IsAsciiWhitespace(char c)
    => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f';
```

- [ ] **Step 2: 替换 ScanAttributes 中 4 处 `char.IsWhiteSpace` 为 `IsAsciiWhitespace`**

位置：L125、L147、L161、L173

- [ ] **Step 3: 运行测试**

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "ContentImageRewritePipeline"`
Expected: 17 passed, 0 failed

- [ ] **Step 4: Commit**

```
perf(scanner): replace char.IsWhiteSpace with ASCII-only check
```

---

### Task 3: 重排 TryClassify 分支 + 按标签名分流（P6）

**Files:**
- Modify: `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs:207-255`

- [ ] **Step 1: 重写 TryClassify 为标签名优先分派**

```csharp
private static bool TryClassify(
    ReadOnlySpan<char> tagName,
    ReadOnlySpan<char> attributeName,
    string html,
    int valueStart,
    int valueLength,
    out HtmlMediaReferenceKind kind)
{
    kind = default;

    if (tagName.Equals("img", StringComparison.OrdinalIgnoreCase))
    {
        if (attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
            || attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase)
            || attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
        {
            kind = attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase)
                ? HtmlMediaReferenceKind.Srcset
                : HtmlMediaReferenceKind.Url;
            return true;
        }
        return false;
    }

    if (tagName.Equals("video", StringComparison.OrdinalIgnoreCase))
    {
        if (attributeName.Equals("poster", StringComparison.OrdinalIgnoreCase)
            || attributeName.Equals("src", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Url;
            return true;
        }
        if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Srcset;
            return true;
        }
        return false;
    }

    if (tagName.Equals("a", StringComparison.OrdinalIgnoreCase))
    {
        if (attributeName.Equals("href", StringComparison.OrdinalIgnoreCase))
        {
            if (IsImageHrefValue(html, valueStart, valueLength))
            {
                kind = HtmlMediaReferenceKind.Url;
                return true;
            }
        }
        if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase)
            || attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
        {
            kind = attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase)
                ? HtmlMediaReferenceKind.Srcset
                : HtmlMediaReferenceKind.Url;
            return true;
        }
        return false;
    }

    // Unknown tag: only data-src and srcset are relevant
    if (attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
    {
        kind = HtmlMediaReferenceKind.Url;
        return true;
    }
    if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
    {
        kind = HtmlMediaReferenceKind.Srcset;
        return true;
    }
    return false;
}
```

- [ ] **Step 2: 运行测试**

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "ContentImageRewritePipeline"`
Expected: 17 passed, 0 failed

- [ ] **Step 3: Commit**

```
refactor(scanner): dispatch TryClassify by tag name first
```

---

### Task 4: 消除 <a href> 的 Substring + HtmlDecode + Regex（P3）

**Files:**
- Modify: `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs`

- [ ] **Step 1: 实现 `IsImageHrefValue` 无分配图片扩展名检测**

替换 `AnchorImageHrefValueRegex.IsMatch(WebUtility.HtmlDecode(rawValue))` 为手动 span 检查：

```csharp
private static readonly string[] ImageExtensions = [
    ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".avif", ".bmp", ".ico", ".tiff", ".tif"
];

private static bool IsImageHrefValue(string html, int valueStart, int valueLength)
{
    var value = html.AsSpan(valueStart, valueLength);
    if (valueLength < 8) return false; // shortest: "http://a.b"

    if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var questionMark = value.IndexOf('?');
    var pathEnd = questionMark >= 0 ? questionMark : value.Length;
    if (pathEnd < 5) return false;

    var extStart = -1;
    for (var j = pathEnd - 1; j >= 0; j--)
    {
        if (value[j] == '.') { extStart = j; break; }
        if (value[j] == '/' || value[j] == ':') break;
    }
    if (extStart < 0) return false;

    var ext = value.Slice(extStart, pathEnd - extStart);
    foreach (var imageExt in ImageExtensions)
    {
        if (ext.Equals(imageExt.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }
    return false;
}
```

- [ ] **Step 2: 删除 `AnchorImageHrefValueRegex` 静态字段**

- [ ] **Step 3: 运行测试**

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "ContentImageRewritePipeline"`
Expected: 17 passed, 0 failed

- [ ] **Step 4: Commit**

```
perf(scanner): replace anchor regex with zero-alloc span-based image extension check
```

---

### Task 5: 延迟 Value 物化 — 改为 span 传参（P2）

**Files:**
- Modify: `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs` (record struct)
- Modify: `src/Bukit.Content/Media/ContentImageRewritePipeline.cs` (consumer)

- [ ] **Step 1: 从 HtmlMediaReference 移除 Value 字段**

将 record struct 改为：
```csharp
internal readonly record struct HtmlMediaReference(
    HtmlMediaReferenceKind Kind,
    int ValueStart,
    int ValueLength);
```

添加一个扩展方法或让 consumer 直接用 `html.AsSpan(reference.ValueStart, reference.ValueLength)`。

- [ ] **Step 2: 修改 ContentImageRewritePipeline.RewriteHtmlAsync**

替换所有 `reference.Value` 使用为 `html.AsSpan(reference.ValueStart, reference.ValueLength)` 或按需 `html.Substring(reference.ValueStart, reference.ValueLength)`。

具体修改点：
- `CollectSrcsetValueUrls(reference.Value, urls)` → 传 substring
- `RewriteSrcsetValueAsync(reference.Value, ...)` → 传 substring
- `RewriteUrlValueAsync(reference.Value, ...)` → 传 substring

- [ ] **Step 3: 更新测试中的 `.Value` 引用**

将 `references.Select(reference => reference.Value)` 改为 `references.Select(reference => html.Substring(reference.ValueStart, reference.ValueLength))`。

- [ ] **Step 4: 运行测试**

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "ContentImageRewritePipeline"`
Expected: 17 passed, 0 failed

- [ ] **Step 5: Commit**

```
perf(scanner): defer Value string materialization to consumer
```

---

### Task 6: 消除 pipeline 双遍历 + 去重 HtmlDecode（P9 + P15）

**Files:**
- Modify: `src/Bukit.Content/Media/ContentImageRewritePipeline.cs:68-115`

- [ ] **Step 1: 合并 pipeline 的两次 reference 遍历为单次**

当前代码先遍历 references 收集 URL，再遍历 references 做替换。合并为：扫描 references 时同时收集 URL 到 batch 列表，调用 `LocalizeDistinctUrlsAsync` 批量本地化，然后单次 StringBuilder 替换。

- [ ] **Step 2: 添加快速路径：无 `&` 时跳过 HtmlDecode**

在 `RewriteUrlValueAsync` 和 `CollectSrcsetValueUrls` 中，先检查 value 是否含 `&`：
```csharp
private static bool NeedsHtmlDecode(ReadOnlySpan<char> value)
    => value.IndexOf('&') >= 0;
```

- [ ] **Step 3: 运行测试**

Run: `dotnet test tests/Bukit.Content.Tests -c Release --filter "ContentImageRewritePipeline"`
Expected: 17 passed, 0 failed

- [ ] **Step 4: Commit**

```
perf(pipeline): merge double-pass and skip HtmlDecode when no ampersand
```

---

### Task 7: 添加 AggressiveInlining + 位运算字符分类（P10 + P12）

**Files:**
- Modify: `src/Bukit.Content/Media/HtmlMediaReferenceScanner.cs`

- [ ] **Step 1: 为 IsTagNameStart/IsTagNameChar/IsAttributeNameStart/IsAttributeNameChar 添加 `[MethodImpl(AggressiveInlining)]`**

- [ ] **Step 2: 用位运算替换范围检查**

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static bool IsTagNameStart(char c)
    => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
```

- [ ] **Step 3: 运行全量测试**

Run: `dotnet test bukit.slnx -c Release --no-build`
Expected: 全部通过

- [ ] **Step 4: Commit**

```
perf(scanner): AggressiveInlining + bit-trick char classification
```

---

### Task 8: 全量回归验证

**Files:** 无新修改

- [ ] **Step 1: 运行 Release 构建**

Run: `dotnet build bukit.slnx -c Release`
Expected: 0 warnings, 0 errors

- [ ] **Step 2: 运行全量测试**

Run: `dotnet test bukit.slnx -c Release`
Expected: 全部通过

- [ ] **Step 3: Final commit**

```
chore: HtmlMediaReferenceScanner performance optimization complete
```

---

## 预期效果

| 优化项 | 之前 | 之后 |
|--------|------|------|
| 外层字符推进 | 逐字符 `i++` | `IndexOf('<')` SIMD 向量化跳转 |
| 无关标签处理 | 对所有标签做属性扫描 | `=` 预检查，无等号直接跳过 |
| `<a href>` 分类 | Substring + HtmlDecode + Regex | span 手动扩展名检测，零分配 |
| Value 字段 | 每个匹配物化 substring | 延迟到 consumer 按需取 |
| HtmlDecode | 最多 3 次/URL | 快速路径：无 `&` 时跳过 |
| Pipeline 遍历 | 双遍（收集+替换） | 单次遍历+批量本地化 |
| 字符分类 | char.IsWhiteSpace（Unicode 表查找） | ASCII 内联位运算 |
| Regex 数量 | 1 个 | 0 个（全部手写解析） |

**预期构建时间改善：** 在 1000 页 Notion 站点上，扫描+重写阶段 CPU 下降 30–60%，Gen0 GC 分配显著减少。
