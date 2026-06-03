# 丝路商讯导入迁移：根因分析报告

> 分析对象：`bukit import html-demo` 对 `/Users/ali/Documents/trae_projects/silkroad_biz/demo` 的导入过程
> 生成时间：2026-06-02
> 分析版本：v1.0

---

## 目录

1. [问题总览与归因矩阵](#1-问题总览与归因矩阵)
2. [问题 A：0 个 Partial 生成（核心问题）](#2-问题-a0-个-partial-生成核心问题)
3. [问题 B：硬编码导航链接](#3-问题-b硬编码导航链接)
4. [问题 C：静态文件路径重复](#4-问题-c静态文件路径重复)
5. [问题 D：页面分类不精确](#5-问题-d页面分类不精确)
6. [问题 E：分页组件变量名不匹配](#6-问题-e分页组件变量名不匹配)
7. [问题 F：base.html Scriban 语法不一致](#7-问题-fbasehtml-scriban-语法不一致)
8. [问题 G：无 theme.yaml 生成](#8-问题-g无-themeyaml-生成)
9. [问题 H：SEO 元数据缺失](#9-问题-hseo-元数据缺失)
10. [总结：各组件健康度评估](#10-总结各组件健康度评估)

---

## 1. 问题总览与归因矩阵

| # | 问题 | 严重度 | Demo | Import 模块 | Bukit 引擎 | 主题 |
|---|------|--------|------|-------------|------------|------|
| A | 0 个 partial 生成（header/footer 缺失） | 🔴 阻塞 | ✅ 主因：16/22 文件为单行压缩，6 文件分行，格式不一致 | ✅ 主因：LayoutExtractor 行级比对脆弱，未能容错 | ⚠️ 次因：无多轮退避策略 | — |
| B | 导航链接硬编码为 `.html` | 🟡 中 | ✅ 主因：源码使用 `index.html`/`insights.html` | ⚠️ 次因：未自动转换硬编码链接为 Bukit 路由 | — | — |
| C | 静态文件路径重复 | 🟢 低 | — | ✅ 主因：AssetImporter 同时写入 `static/` 和 `static/assets/` | — | ⚠️ 次因：路径匹配逻辑不明确 |
| D | 多页面分类为 Unknown | 🟡 中 | ✅ 主因：中文页面名未在 FileNameMapping 中注册 | ✅ 主因：PageClassifier 仅支持英文命名映射 | — | — |
| E | 分页组件使用非标准变量 | 🟢 低 | — | ✅ 主因：生成通用占位符而非 Bukit `page_list` 上下文 | — | ⚠️ 次因：组件模板未匹配引擎 API |
| F | base.html Scriban 语法不一致 | 🟢 低 | — | ✅ 主因：`ThemeGenerator.WriteBaseLayout` 内嵌非标准 Scriban | — | ⚠️ 次因：需后处理修正 |
| G | 未生成 theme.yaml | 🟢 低 | — | ✅ 设计意图：Beta 阶段仅使用 site.yaml params | ✅ 设计意图：V2 主题清单非必须 | — |
| H | SEO 元数据缺失（OG 图片、author） | 🟢 低 | ✅ 主因：Demo 中无 OG meta 标签 | — | — | ⚠️ 次因：需手动配置 |

### 严重度定义

| 级别 | 定义 |
|------|------|
| 🔴 阻塞 | 网站无法正常使用（无导航/无页脚） |
| 🟡 中 | 功能受限但可用（链接无法跳转/页面归类错误） |
| 🟢 低 | 可优化项（代码风格/路径整洁/SEO） |

### 代码路径速查

| 组件 | 文件 | 行数 |
|------|------|------|
| HtmlDemoImporter | `src/Bukit.Importing/HtmlDemoImporter.cs` | ~420 |
| LayoutExtractor | `src/Bukit.Importing/LayoutExtractor.cs` | 232 |
| ThemeGenerator | `src/Bukit.Importing/ThemeGenerator.cs` | 399 |
| PageClassifier | `src/Bukit.Importing/PageClassifier.cs` | 116 |
| NavigationMarkupExtractor | `src/Bukit.Importing/NavigationMarkupExtractor.cs` | 225 |
| HtmlDocumentParser | `src/Bukit.Importing/HtmlDocumentParser.cs` | ~130 |
| AssetImporter | `src/Bukit.Importing/AssetImporter.cs` | ~200 |
| HtmlDemoScanner | `src/Bukit.Importing/HtmlDemoScanner.cs` | 16 |

---

## 2. 问题 A：0 个 Partial 生成（核心问题）

### 症状

导入生成的 `themes/silkroad-biz/layouts/partials/` 目录为空，`base.html` 不含 `{{ include 'partials/header.html' }}` 和 `{{ include 'partials/footer.html' }}`。所有 22 个页面均无导航栏和页脚。

### 调用链

```
HtmlDemoImporter.Import()
  → HtmlDemoScanner.Scan()          # 扫描 22 个 HTML 文件
  → LayoutExtractor.Extract()       # 尝试提取公共布局 → 返回空
  → ThemeGenerator.Generate()       # 发现 layout.Header == "" → 不生成 partials
  → ThemeGenerator.WriteBaseLayout() # 不包含 include partials 指令
```

### 根因分析

#### 根因 1（DEMO）：HTML 文件格式不一致

22 个 HTML 文件分为两种格式：

| 格式 | 文件数 | 文件列表 | 特征 |
|------|--------|---------|------|
| **单行压缩** | 16 | `about.html`, `article-*.html`, `company-*.html`, `contact.html`, `index.html`, `insights.html`, `join.html` | 全文压缩为单行（>500 chars），`<body>` 与 `<header>` 在同一行 |
| **换行格式化** | 6 | `companies.html`, `china-companies.html`, `malaysia-companies.html`, 及它们的 `-page-2` 变体 | `<!DOCTYPE html>` 在独立行，`<body>` 在第 10-11 行 |

这种差异源于 Demo 的生成过程：第 1 组（16 文件）来自工具自动生成 HTML，第 2 组（6 文件）是手动编写或重新格式化的。

#### 根因 2（IMPORT 模块）：LayoutExtractor 行级比对算法脆弱

[LayoutExtractor.cs:L40-L48](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/LayoutExtractor.cs#L40-L48)

```csharp
var headerLines = FindLongestCommonPrefixLines(
    pages.Select(p => p.BodyOpening).ToList(),
    normalizedOpenings);
```

核心问题在 [FindLongestCommonPrefixLines](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/LayoutExtractor.cs#L145-L171)：

```csharp
var originalLines = originalTexts.Select(t => t.Split('\n').ToList()).ToList();
var normalizedLines = normalizedTexts.Select(t => t.Split('\n').ToList()).ToList();

for (var i = 0; i < minLines; i++)
{
    var normLine = normalizedLines[0][i];
    if (normalizedLines.All(l => string.Equals(l[i], normLine)))
        result.Add(originalLines[0][i]);
    else
        break;   // ← 一行不匹配即停止
}
```

**问题在于：**

1. **`\n` 分割行**：AngleSharp 的 `InnerHtml/OuterHtml` 重建 HTML 时，行分割取决于 DOM 序列化器的行为。即使 `StripClassId` 移除了 class/id 属性，不同页面的序列化行数可能不同，导致 `normalizedLines[0].Count != normalizedLines[1].Count` 或某行内容不一致。

2. **无容错机制**：一行不匹配就完全停止，没有：
   - 多轮退避（先去掉 format-2 组文件再尝试）
   - 投票制（如果有 >80% 页面共享某个 prefix，接受它）
   - 非行级比对（如文本级而非行级比对）

3. **无兜底策略**：没有降级方案（例如：0 行相同则退化到单页面提取模式，提取第一页的 header/footer）

#### 根因 3（IMPORT 模块）：SplitBody 依赖单一 `<main>` 锚点

[HtmlDocumentParser.cs:L68-L80](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDocumentParser.cs#L68-L80)

```csharp
var mainElement = body.QuerySelector("main") ?? body.QuerySelector("article");
if (mainElement == null)
{
    // 退化逻辑
}
```

`SplitBody` 方法以 `<main>` 作为唯一锚点分割 body。当某些页面 `<main>` 位置不同或没有 `<main>` 时，`BodyOpening` 和 `BodyClosing` 的内容差异会直接导致 LayoutExtractor 找不到公共内容。

#### 根因 4（BUKIT 引擎）：无布局提取反馈机制

当 LayoutExtractor 返回空 header/footer 时，引擎不做后续补救：
- 不发警告（"无法提取公共布局，建议手动创建 header/footer partials"）
- 不降级（"切换到单页面布局模式"）
- 不提供路线图建议

[ThemeGenerator.cs:L28-L46](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeGenerator.cs#L28-L46) 的 partial 生成是条件性的，无空值兜底：

```csharp
if (!string.IsNullOrWhiteSpace(layout.Header))
{
    WritePartial(themeDir, "header.html", layout.Header, pathMappings);
    partialCount++;
}
```

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P0 | LayoutExtractor.cs | 增加**多轮退避**：当公共行数 = 0 时，尝试移除离群页面（如 page-2 文件）重新提取 |
| P0 | LayoutExtractor.cs | 增加**投票制**：当 >80% 页面共享某 prefix 时接受该 prefix |
| P1 | HtmlDemoImporter.cs | LayoutExtractor 返回空时，打印**建议提示**："未提取到公共布局，建议通过 route-map.yaml 指定页面结构或手动创建 header/footer partials" |
| P1 | HtmlDocumentParser.cs | 增加**多锚点分割**：`<header>` 和 `<footer>` 标签作为补充分割锚点 |

---

## 3. 问题 B：硬编码导航链接

### 症状

生成的 page 模板中，所有导航链接均为 `.html` 格式：
```html
<a href="insights.html">商务资讯</a>
<a href="companies.html">企业资源库</a>
```

而非 Bukit 路由格式：
```html
<a href="/insights/">商务资讯</a>
```

### 根因

#### 根因 1（DEMO）：源文件使用 .html 引用

原始 Demo 的所有页面使用相对路径 `.html` 引用。这是静态 HTML 站点的常见做法。

#### 根因 2（IMPORT 模块）：未转换硬编码链接

[AssetImporter.RewritePaths](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/AssetImporter.cs) 处理的是 CSS/JS/图片路径的映射，**不处理** `<a href="...">` 中的 `.html` 链接。

[TemplateBodyTransformer](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/TemplateBodyTransformer.cs) 负责将 HTML 转换为 Scriban 模板，但同样不转换 `<a href>` 中的 `.html` 引用。

[TemplateResidueAnalyzer](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/TemplateResidueAnalyzer.cs) 会报告硬编码文本残留，但不会报告/修复硬编码链接。

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P1 | TemplateBodyTransformer.cs | 增加 `.html` → Bukit 路由转换（识别 `href="xxx.html"` 并映射为 `/{slug}/`） |
| P1 | PageClassifier.cs | 增加从文件名到路由的完整映射表（可用于链接转换） |

---

## 4. 问题 C：静态文件路径重复

### 症状

生成的主题中，CSS/JS 同时存在于两个位置：

```
static/assets/css/style.css    ← 来自 AssetImporter 的图片路径（图片 → assets/，其他 → static/）
static/assets/js/main.js
static/css/style.css           ← AssetImporter 将非图片也写入了 static/
static/js/main.js
```

### 根因

#### 根因 1（IMPORT 模块）：AssetImporter 双重写入

[AssetImporter.cs:L70-L78](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/AssetImporter.cs#L70-L78)：

```csharp
var isImage = ImageExtensions.Contains(Path.GetExtension(assetPath));
var destSubDir = isImage ? "assets" : "static";
var destPath = Path.Combine(themeDir, destSubDir, assetPath.TrimStart('/'));
```

非图片文件（CSS/JS）的目标目录是 `static/`。当原始引用的路径是 `assets/css/style.css` 时，写入 `static/assets/css/style.css`。

与此同时，[AssetImporter.TransferAssetsToStatic](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/AssetImporter.cs#L120-L130) 方法将 `assets/` 目录下的文件再复制到 `static/` 下一份：

```csharp
// TransferAssetsToStatic: copies theme assets/ → theme static/
```

这一环节引入了重复。

#### 根因 2（主题）：路径引用链条不清晰

`base.html` 引用的 CSS 路径为 `{{ base_url }}/assets/css/style.css`，对应 `static/assets/css/style.css` 而非 `static/css/style.css`。两个都有效但造成混淆。

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P1 | AssetImporter.cs | 统一写入策略：所有资源文件按 manifest 写入一次，不 split 到两个目录 |
| P1 | AssetImporter.cs | `TransferAssetsToStatic` 应判断目标是否已存在，跳过重复 |

---

## 5. 问题 D：页面分类不精确

### 症状

导入报告显示多页面分类为 `Unknown`，使用通用 `page.html` 模板而非专用模板：
- `china-companies.html` → Unknown → `page.html`（应为 `CompanyList` → `companies.html`）
- `join.html` → Unknown → `page.html`（应为 `Page` → `page.html`，但未获得合适的 page-hero 布局）

### 根因

#### 根因 1（DEMO）：中文文件名不在映射表中

[PageClassifier.cs:L5-L26](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/PageClassifier.cs#L5-L26) 的 `FileNameMapping` 字典包含的是英文文件名模式：

```csharp
["companies"] = PageType.CompanyList,
["company"] = PageType.CompanyDetail,
["services"] = PageType.ServiceList,
["china-companies"] = ?  // ← 未定义！
["malaysia-companies"] = ?  // ← 未定义！
["join"] = ?  // ← 未定义！
```

`china-companies`、`malaysia-companies`、`join` 等中文语义文件名未被识别，必须依赖 `ClassifyByContent` 的内容分析。

#### 根因 2（IMPORT 模块）：内容分类逻辑单一

[ClassifyByContent](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/PageClassifier.cs#L87-L103) 只通过 CSS class 名匹配进行猜测：

```csharp
var hasCompanyCards = CountOccurrences(html, "company-card") >= 2;
```

但如果 Demo 使用了不同的 class 名（如 `card` 而非 `company-card`），分类会失败。

#### 根因 3（IMPORT 模块）：无 route-map.yaml 时无法纠正

导入时未传入 `--route-map` 参数，无法通过 [RouteMapConfig](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/RouteMapConfig.cs) 手动指定分类。

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P1 | PageClassifier.cs | 增加中英文混合文件名映射（`china-companies`, `malaysia-companies`, `join` 等） |
| P1 | PageClassifier.cs | 增加通过 `<title>` 内容分析的辅助分类器（如标题含"企业资源库"→ CompanyList） |
| P1 | ImportCommand.cs | 在失败提示中建议使用 `--route-map` 进行手动纠正 |

---

## 6. 问题 E：分页组件变量名不匹配

### 症状

生成的分页组件 [pagination.html](file:///Users/ali/mydev/Git/Github/Bukit/import-test/themes/silkroad-biz/layouts/components/pagination.html) 使用非标准变量：

```html
{{ if pagination.has_prev }}{{ pagination.prev_url }}{{ end }}
```

而 Bukit 引擎使用的分页上下文是 `page_list` 而非 `pagination`：
```html
{{ if page_list.has_prev }}{{ page_list.prev_page_url }}{{ end }}
```

这导致 `bukit doctor` 报出变量拼写警告。

### 根因

#### 根因 1（IMPORT 模块）：ComponentExtractor 生成占位变量

[ComponentExtractor.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ComponentExtractor.cs) 在生成分页组件时使用通用 `pagination.xxx` 命名而非 Bukit 引擎的 `page_list.xxx` API。

#### 根因 2（主题）：组件模板未使用引擎上下文

生成的组件是"猜测"的页面结构分离，没有与 Bukit 的 `PageListPlugin` 或 `PaginationPlugin` 的实际上下文绑定。

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P1 | ComponentExtractor.cs | 对分页组件使用 `page_list.xxx`（Bukit 标准 API）而非 `pagination.xxx` |
| P2 | bukit-plugins-debug SKILL.md | 在分页文档中标明标准变量名 |

---

## 7. 问题 F：base.html Scriban 语法不一致

### 症状

生成的 base.html 包含潜在地语法问题：

```html
{{ base_url = site.base_url }}
{{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
```

### 根因

#### 根因 1（IMPORT 模块）：ThemeGenerator.WriteBaseLayout 内嵌非标准赋值

[ThemeGenerator.cs:L231-L232](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeGenerator.cs#L231-L232)：

```csharp
sb.AppendLine("  {{ base_url = site.base_url }}");
sb.AppendLine("  {{ if base_url == \"/\" }}{{ base_url = \"\" }}{{ end }}");
```

这段代码使用 Scriban 5.x 的表达式内联语法 `{{ var = value }}`，虽然能通过解析，但在 Scriban 的不同版本中行为不一致（某些版本将赋值语句当作表达式处理并输出值）。

#### 根因 2（主题）：未使用 Bukit 的标准 base_url 模式

Bukit 的推荐做法是在 `site.yaml` 中设置 `baseUrl: ""` 而非 `/`，或在模板中直接使用 `{{ site.base_url }}` 无需二次处理。

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P1 | ThemeGenerator.cs | 移除赋值逻辑，直接在 site.yaml 建议使用 `baseUrl: ""`，模板中直接 `{{ site.base_url }}` |
| P1 | ThemeGenerator.cs | 如果一定要处理，使用 Scriban `raw` 标签过滤：`{% raw %}{{ base_url = site.base_url }}{% endraw %}` |

---

## 8. 问题 G：无 theme.yaml 生成

### 症状

导入生成的主题无 `theme.yaml` 文件，主题能力声明（capabilities）缺失。

### 根因分析

#### 根因 1（IMPORT 模块 - 设计意图）：Beta 阶段仅使用 site.yaml params

[Skills/bukit-import/SKILL.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-import/SKILL.md#L66-L78) 明确输出合约不包含 `theme.yaml`。参数化通过 `site.yaml` 的 `theme.params` 实现。

#### 根因 2（BUKIT 引擎 - 设计意图）：V2 主题清单非必须

`bukit doctor` 和 `bukit build` 均能在无 `theme.yaml` 时正常工作，只需 `site.yaml` 中指定 `theme.name`。

### 评估

此问题为"设计选择"而非"缺陷"。从 Beta 阶段角度看，不生成 theme.yaml 降低了首次导入的复杂度。未来版本应考虑生成最小 theme.yaml（含 engine version 和 params 映射）。

---

## 9. 问题 H：SEO 元数据缺失

### 症状

构建时出现以下 SEO 警告：
- `og_default.gif` 不存在
- `description_duplicate`（多页面相同 description）
- `schema_blogposting_author_missing`

### 根因

#### 根因 1（DEMO）：缺乏 OG 标记

Demo 的 `<head>` 中没有 `og:image`、`og:title` 等 Open Graph 标签，只有基本的 `<meta name="description">`。

#### 根因 2（IMPORT 模块）：未从页面内容推断 SEO 字段

`SiteConfigGenerator` 和 `ContentDraftWriter` 不尝试从页面内容推断 `seo_title`、`seo_desc`、`seo_image` 等字段。

#### 根因 3（主题）：无 OG 模板

base.html 的 `<head>` 中没有 Open Graph meta 标签模板（如 `{{ if page.seo.image }}<meta property="og:image"...{{ end }}`）。

### 修复方案

| 优先级 | 修改位置 | 方案 |
|--------|---------|------|
| P1 | ContentDraftWriter.cs | 从每个页面的 `<title>` 和 `<meta name="description">` 提取 SEO 字段写入 content front matter |
| P2 | ThemeGenerator.cs | 在 base.html 模板中添加 OG template block |
| P2 | SiteConfigGenerator.cs | 为 SEO 显示默认图像使用 base64 占位或明确提示 |

---

## 10. 总结：各组件健康度评估

### 10.1 导入模块健康度

| 组件 | 文件 | 健康度 | 关键短板 |
|------|------|--------|---------|
| `HtmlDemoImporter` | 主流程 | 🟡 可运行 | 无失败恢复策略 |
| `LayoutExtractor` | 布局提取 | 🔴 脆弱 | 行级比对 + 无退避 |
| `ThemeGenerator` | 主题生成 | 🟡 基本可用 | partial/fallback/template 生成连贯性差 |
| `PageClassifier` | 页面分类 | 🟡 英文覆盖好 | 中文文件名/内容分类弱 |
| `NavigationMarkupExtractor` | 导航提取 | 🟢 较稳健 | 但依赖 LayoutExtractor 先提取 header |
| `AssetImporter` | 资源导入 | 🟡 有冗余 | 双重写入问题 |
| `HtmlDocumentParser` | HTML 解析 | 🟢 标准 | AngleSharp 集成良好 |
| `ContentDraftWriter` | 内容生成 | 🟡 基础 | 无 SEO 字段提取 |
| `ComponentExtractor` | 组件提取 | 🟡 基础 | 分页变量名不匹配引擎 API |

**总体健康状况：🟡 Beta 级别，可运行但需后处理**

### 10.2 Demo 健康度

| 维度 | 健康度 | 说明 |
|------|--------|------|
| HTML 格式一致性 | 🔴 不一致 | 16 压缩 vs 6 格式化，破坏行级解析 |
| 语义标签使用 | 🟢 良好 | 统一使用 `<main>`、`<header>`、`<footer>`、`<nav>` |
| CSS 设计系统 | 🟢 优秀 | CSS 变量标准化，无语义冲突 |
| JS 使用 | 🟢 极简 | 仅 18 行汉堡菜单，无依赖 |
| 中文内容 | 🟢 完整 | 所有页面有完整中文内容 |
| 导航 | 🟢 统一 | 8 个导航项在全部页面中存在 |
| OG/SEO | 🔴 缺失 | 无 Open Graph、结构化数据 |

**总体健康度：🟡 中等，内容优秀但格式不一致**

### 10.3 Bukit 引擎健康度（与导入相关部分）

| 组件 | 健康度 | 说明 |
|------|--------|------|
| Scriban 渲染引擎 | 🟢 稳定 | 27 模板全部正确渲染 |
| `site.yaml` 配置 | 🟢 良好 | site.yaml 各节点正确定义 |
| Theme resolution | 🟢 良好 | 无 theme.yaml 时正常工作 |
| 分页系统 | 🟡 无法通过 import 自动对接 | 变量名不匹配 |
| 布局系统 | 🟢 良好 | base → page 继承正确 |
| 变量拼写检查 | 🟡 过于宽松 | 只报 warning 不过硬失败 |

**总体健康度：🟢 稳定，但 import 模块未充分利用引擎能力**

### 10.4 主题健康度（后处理修复后）

| 维度 | 健康度 | 说明 |
|------|--------|------|
| 模板完整性 | 🟢 完整 | 22 页面模板 + 4 组件 + 2 partials |
| 导航 | 🟢 修复后正常 | 8 参数化导航链接 |
| 响应式 | 🟢 继承 demo | 原始 CSS 响应式设计保留 |
| SEO | 🟡 基础 | 仅有 base title/description |
| 分页 | 🔴 未适配 | 需手动修复变量名 |
| 性能 | 🟢 良好 | 纯静态输出 |

**总体健康度：🟢 修复后可用**

---

## 附录：推荐修复优先级

### 立即修复（P0 - 阻塞功能）

| 问题 | 修改量 | 影响 |
|------|--------|------|
| LayoutExtractor 多轮退避 | ~30 行 C# | 直接影响所有 HTML demo 导入的布局提取质量 |

### 重要改进（P1 - 功能受限）

| 问题 | 修改量 | 影响 |
|------|--------|------|
| PageClassifier 添加中文文件名 | ~10 行 C# | 扩展页面分类覆盖度 |
| TemplateBodyTransformer 链接转换 | ~50 行 C# | 减少手动后处理工作 |
| AssetImporter 去重 | ~15 行 C# | 消除路径冗余 |
| ThemeGenerator base_url 修正 | ~5 行 C# | Scriban 语法规范化 |
| ComponentExtractor 分页变量名 | ~10 行 C# | 匹配引擎标准 API |

### 优化建议（P2 - 体验提升）

| 问题 | 修改量 | 影响 |
|------|--------|------|
| LayoutExtractor 投票制 | ~40 行 C# | 提高复杂场景布局提取成功率 |
| ContentDraftWriter SEO 字段 | ~30 行 C# | 自动提取 description 到 front matter |
| base.html OG 模板 | ~15 行 Scriban | 主题侧 SEO 提升 |
