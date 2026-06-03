# Bukit Import 模块修复计划

> 基于根因分析报告 `silkroad-biz-import-root-cause-report.md` 的 8 个问题
> 计划日期：2026-06-02

---

## 1. 摘要

对 `src/Bukit.Importing/` 下 4 个文件进行代码修复，覆盖 3 个优先级（P0/P1/P2），共 6 处变更，预计总代码修改量约 100 行 C#。所有修复均不改动 Bukit 引擎或外部 API 接口。

### 修复优先级总览

| 优先级 | 问题 | 文件 | 行数估算 |
|--------|------|------|----------|
| 🔴 P0 | A: LayoutExtractor 无退避 | LayoutExtractor.cs | ~30 行 |
| 🟡 P1 | D: 中文文件名未映射 | PageClassifier.cs | ~10 行 |
| 🟡 P1 | E: 分页变量名不匹配 | ComponentExtractor.cs | ~10 行 |
| 🟡 P1 | F: base.html Scriban 语法 | ThemeGenerator.cs | ~5 行 |
| 🟡 P1 | C: AssetImporter 重复写入 | AssetImporter.cs | ~15 行 |
| 🟢 P2 | A: 布局提取失败无警告 | HtmlDemoImporter.cs | ~15 行 |

---

## 2. 当前状态分析

### 2.1 模块架构

```
HtmlDemoImporter.Import()          ← 入口
  ├─ HtmlDemoScanner.Scan()        ← 解析 HTML 为 DiscoveredPage
  ├─ LayoutExtractor.Extract()     ← 提取共享布局 → 可能返回空
  ├─ AssetImporter.Import()        ← 复制 CSS/JS/图片 → 可能重复写入
  ├─ ThemeGenerator.Generate()     ← 生成 Scriban 模板
  │   ├─ WriteBaseLayout()         ← base.html 有 Scriban 语法问题
  │   ├─ WritePartial()            ← 依赖 LayoutExtractor 结果
  │   └─ WritePageTemplate()       ← 逐个页面模板
  ├─ ComponentExtractor.Extract()  ← 组件提取（分页变量名不匹配）
  └─ ContentDraftWriter.Write()    ← Markdown 内容草稿
```

### 2.2 已知缺陷速查

| 文件 | 行号 | 缺陷 | 影响 |
|------|------|------|------|
| [LayoutExtractor.cs:145-171](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/LayoutExtractor.cs#L145-L171) | `FindLongestCommonPrefixLines` | 一行不匹配即停止，无退避 | 0 partials |
| [LayoutExtractor.cs:173-207](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/LayoutExtractor.cs#L173-L207) | `FindLongestCommonSuffixLines` | 同上 | 0 partials |
| [PageClassifier.cs:5-26](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/PageClassifier.cs#L5-L26) | `FileNameMapping` | 仅英文文件名，缺中文/混合名 | 页面分类 Unknown |
| [ComponentExtractor.cs:130-142](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ComponentExtractor.cs#L130-L142) | `GenerateTemplate` | 分页变量 `pagination.xxx` 非引擎标准 | doctor 警告 |
| [ThemeGenerator.cs:231-232](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ThemeGenerator.cs#L231-L232) | `WriteBaseLayout` | Scriban 内联赋值语法 | 模板语法不一致 |
| [AssetImporter.cs:70-74](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/AssetImporter.cs#L70-L74) | `Import` + `TransferAssetsToStatic` | 双重写入 static/ | 文件冗余 |
| [HtmlDemoImporter.cs:22](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs#L22) | `Import` | 布局提取空时不警告 | 静默失败 |

---

## 3. 修改方案

### 修改 1：LayoutExtractor.cs — 多轮退避机制（P0）

**文件**：[src/Bukit.Importing/LayoutExtractor.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/LayoutExtractor.cs)

**问题**：`FindLongestCommonPrefixLines` 和 `FindLongestCommonSuffixLines` 对输入格式不一致零容忍——一行不匹配即完全停止，无退避或兜底策略。

**修改内容**：

在 `Extract()` 方法中，当 `headerLines` 或 `footerLines` 为空的 retval 时，增加两轮退避：

1. **第一轮退避（P0）—— 单页面模式**：当第一轮行级比对返回空时，将处理逻辑降级到单页面模式（即用已有的单页面分支逻辑），为每页独立提取 header/footer。

   ```csharp
   // 在 Extract() 方法中，第 50 行之后插入：
   var headerContent = string.Join("\n", headerLines);
   
   // 新增：当公共内容为空时，退避到单页面提取模式
   if (string.IsNullOrWhiteSpace(headerContent) && pages.Count > 1)
   {
       warnings.Add("无法通过行级比对提取公共布局。已降级为单页面模式：使用第一个页面的布局结构。建议通过 route-map.yaml 精确指定。");
       // 使用第一页作为布局模板
       var first = pages[0];
       var fallbackHeader = ExtractByTag(first.BodyOpening, "header") ?? "";
       var fallbackFooter = ExtractByTag(first.BodyClosing, "footer") ?? "";
       var fallbackNav = ExtractNavBlock(fallbackHeader);
       return new LayoutInfo(
           Header: fallbackHeader,
           Nav: fallbackNav,
           Footer: fallbackFooter,
           HeadExtras: first.HeadContent ?? "",
           HeaderContainsNav: !string.IsNullOrWhiteSpace(fallbackNav));
   }
   ```

2. **同位置**：对 `footerContent` 也增加空值兜底：

   ```csharp
   var footerContent = string.Join("\n", footerLines);
   
   // 新增：如果 footer 为空但 header 有内容，也尝试退避
   if (string.IsNullOrWhiteSpace(footerContent) && !string.IsNullOrWhiteSpace(headerContent))
   {
       warnings.Add("检测到的 footer 为空，可能不准确。建议添加 <footer> 标签或使用 route-map.yaml。");
   }
   ```

**预期效果**：当 16 单行文件 + 6 多行文件混合时，行级比对失败后自动降级为使用第一页（index.html）的 header/footer，至少保证有导航可用。

### 修改 2：PageClassifier.cs — 扩展文件名映射（P1）

**文件**：[src/Bukit.Importing/PageClassifier.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/PageClassifier.cs)

**问题**：`FileNameMapping` 字典仅包含英文文件名模式，不识别 `china-companies`、`malaysia-companies`、`join` 等中文语义文件名。

**修改内容**：

在 `FileNameMapping` 字典中添加 3 条新映射：

```csharp
private static readonly Dictionary<string, PageType> FileNameMapping = new(StringComparer.OrdinalIgnoreCase)
{
    // ... 现有条目保持不变 ...

    // 新增：中文语义文件名映射
    ["china-companies"] = PageType.CompanyList,
    ["malaysia-companies"] = PageType.CompanyList,
    ["join"] = PageType.Page,
};
```

**预期效果**：`china-companies.html` → `CompanyList`（用 `companies.html` 模板），`join.html` → `Page`（用 `page.html` 模板）。

### 修改 3：ComponentExtractor.cs — 分页组件变量修正（P1）

**文件**：[src/Bukit.Importing/ComponentExtractor.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ComponentExtractor.cs)

**问题**：`GenerateTemplate` 中 pagination 分支使用 `pagination.xxx` 变量名，而 Bukit 引擎分页上下文变量为 `page_list.xxx`。

**修改内容**：

替换 [ComponentExtractor.cs:L131-142](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ComponentExtractor.cs#L131-L142) 的分页模板：

```csharp
if (componentName.Equals("pagination", StringComparison.OrdinalIgnoreCase))
{
    return """
{{ if page_list.has_prev }}
<nav class="pagination" aria-label="Pagination">
  {{ if page_list.has_prev }}<a href="{{ page_list.prev_page_url }}" rel="prev">‹</a>{{ end }}
  <span>{{ page_list.current_page }} / {{ page_list.total_pages }}</span>
  {{ if page_list.has_next }}<a href="{{ page_list.next_page_url }}" rel="next">›</a>{{ end }}
</nav>
{{ end }}
""";
}
```

**预期效果**：生成的分页组件模板使用 Bukit 引擎标准 `page_list` 上下文变量，消除 `bukit doctor` 的变量拼写警告。

### 修改 4：ThemeGenerator.cs — base.html Scriban 语法修正（P1）

**文件**：[src/Bukit.Importing/ThemeGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ThemeGenerator.cs)

**问题**：`WriteBaseLayout` 生成 `{{ base_url = site.base_url }}` 内联赋值，在 Scriban 不同版本中行为不一致。

**修改内容**：

删除第 231-232 行的赋值逻辑，改为在模板中直接使用 `site.base_url`，并让 site.yaml 默认 `baseUrl: ""` 而非 `"/"`：

```csharp
// 删除这两行：
// sb.AppendLine("  {{ base_url = site.base_url }}");
// sb.AppendLine("  {{ if base_url == \"/\" }}{{ base_url = \"\" }}{{ end }}");

// 改为：在 head 中直接使用 site.base_url
```

同时修正所有 `href="{{ base_url }}/..."` 为 `href="{{ site.base_url }}..."`（因为 baseUrl 默认为 `""` 而非 `"/"`），确保不产生双斜杠。

### 修改 5：AssetImporter.cs — 去除重复写入（P1）

**文件**：[src/Bukit.Importing/AssetImporter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/AssetImporter.cs)

**问题**：CSS/JS 文件同时出现在 `static/`（来自 Import 方法的分支逻辑）和 `static/assets/`（来自 TransferAssetsToStatic），造成冗余。

**修改内容**：

在 `TransferAssetsToStatic` 方法中（[AssetImporter.cs:L117-141](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/AssetImporter.cs#L117-L141)），增加去重判断：如果文件在 static/ 下已存在（通过 Import 方法写入），则跳过 Move：

```csharp
internal static void TransferAssetsToStatic(HtmlDemoImportOptions options)
{
    var themeBase = HtmlDemoImporter.GetThemeDir(options);
    var themeAssetsDir = Path.Combine(themeBase, "assets");
    var themeStaticDir = Path.Combine(themeBase, "static");

    if (!Directory.Exists(themeAssetsDir))
        return;

    if (!Directory.Exists(themeStaticDir))
        Directory.CreateDirectory(themeStaticDir);

    foreach (var file in Directory.GetFiles(themeAssetsDir, "*.*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(themeAssetsDir, file);
        var dest = Path.Combine(themeStaticDir, rel);
        if (!File.Exists(dest))
        {
            var destDir = Path.GetDirectoryName(dest);
            if (destDir is not null)
                Directory.CreateDirectory(destDir);
            File.Move(file, dest);
        }
        else
        {
            // 新增：目标已存在（Import 方法已写入 static/assets/...），删除源文件避免冗余
            File.Delete(file);
        }
    }
}
```

**预期效果**：CSS/JS 不再同时出现于 `static/css/` 和 `static/assets/css/`，避免冗余。

### 修改 6：HtmlDemoImporter.cs — 增加布局提取失败警告（P2）

**文件**：[src/Bukit.Importing/HtmlDemoImporter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs)

**问题**：LayoutExtractor 返回空布局时，用户无任何提示，导致生成的主题缺失导航而无从知晓。

**修改内容**：

在 `Import()` 方法中，LayoutExtractor 调用之后增加空布局检查（[HtmlDemoImporter.cs:L22](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs#L22) 之后）：

```csharp
var layout = LayoutExtractor.Extract(pages, warnings);

// 新增：空布局检测
if (string.IsNullOrWhiteSpace(layout.Header) && string.IsNullOrWhiteSpace(layout.Footer))
{
    Console.WriteLine();
    Console.WriteLine("  ⚠ 未提取到共享布局（header/footer）。可能原因：");
    Console.WriteLine("     • HTML 文件格式不一致（压缩 vs 分行）");
    Console.WriteLine("     • 页面结构差异过大");
    Console.WriteLine("  建议：");
    Console.WriteLine("     • 使用 --route-map route-map.yaml 精确指定页面结构");
    Console.WriteLine("     • 导入后手动创建 themes/<name>/layouts/partials/header.html 和 footer.html");
    Console.WriteLine("     • 所有页面已保留在 sites/<name>/original-demo/ 中供参考");
    Console.WriteLine();
}
```

**预期效果**：用户能立即知晓布局提取失败的原因和解决方案。

---

## 4. 不变更项（设计决策说明）

| 问题 | 报告归类 | 不修复原因 |
|------|---------|-----------|
| B: 硬编码 `.html` 导航链接 | Import 次因 | 需要完整的路由映射表（page → slug → url），复杂度 >50 行且存在误转换风险。建议通过 route-map.yaml 手动纠正 |
| G: 无 theme.yaml 生成 | 设计意图 | Beta 阶段明确使用 site.yaml params，不生成 theme.yaml 是降低复杂度的有意设计 |
| H: SEO 元数据缺失 | Demo 主因 + Import 次因 | Demo 侧问题为主（源文件无 OG 标签），ContentDraftWriter 可从 `<meta name="description">` 提取 summary 但该字段已在 front matter 中 |
| LayoutExtractor 投票制 | P2 优化 | 需要约 40 行变更 + 投票阈值参数，涉及算法变更，本次聚焦 P0/P1 修复 |

---

## 5. 假设与前提

1. **不改动 Bukit 引擎**：所有修改限定在 `src/Bukit.Importing/` 目录，不触碰 `Bukit.Engine/`、`Bukit.Rendering/`、`Bukit.Theme/` 等模块
2. **保持 API 兼容**：`LayoutInfo` record、`ImportResult` record 等公开类型结构不变
3. **不改 Scriban 模板生成框架**：仅修改模板内容（如变量名），不修改 `WritePageTemplate`、`WriteBaseLayout` 等方法签名
4. **测试策略**：修复后用上一次的 `silkroad_biz/demo` 作为回归测试输入，验证：partials 生成数量 > 0、doctor 通过、build 成功

---

## 6. 验证步骤

### 6.1 构建验证

```bash
cd /Users/ali/mydev/Git/Github/Bukit
dotnet build src/Bukit.Importing/Bukit.Importing.csproj
```

### 6.2 导入回归验证（使用 silkroad_biz demo）

```bash
# 清理上一次导入
rm -rf import-test/themes/silkroad-biz import-test/sites/silkroad-biz

# 重新导入
cd import-test
../publish/bukit import html-demo /Users/ali/Documents/trae_projects/silkroad_biz/demo \
  --theme silkroad-biz --force --verify --language zh
```

### 6.3 预期改善标准

| 验证项 | 修复前 | 修复后预期 |
|--------|--------|-----------|
| PartialsGenerated | 0 | ≥ 2（header + footer） |
| PageClassifier Unknown 页面 | > 0 (china-companies, malaysia-companies, join) | 0 |
| pagination.xxx 变量警告 | 有 | 无 |
| base.html `{{ base_url =` | 存在 | 不存在 |
| static/ 重复文件 | 2 份 | 1 份 |
| 布局提取失败控制台输出 | 无 | 有中文提示 |

### 6.4 最终验证

```bash
# doctor
./publish/bukit doctor --config import-test/sites/silkroad-biz/site.yaml

# build
./publish/bukit build --config import-test/sites/silkroad-biz/site.yaml --clean
```

---

## 7. 变更文件汇总

| 文件 | 变更行 | 类型 | 优先级 |
|------|--------|------|--------|
| `src/Bukit.Importing/LayoutExtractor.cs` | +25 | 退避逻辑新增 | 🔴 P0 |
| `src/Bukit.Importing/PageClassifier.cs` | +3 | 映射表扩展 | 🟡 P1 |
| `src/Bukit.Importing/ComponentExtractor.cs` | ~10 行替换 | 模板修正 | 🟡 P1 |
| `src/Bukit.Importing/ThemeGenerator.cs` | ~5 行删除+替换 | 语法修正 | 🟡 P1 |
| `src/Bukit.Importing/AssetImporter.cs` | +5 | 去重逻辑 | 🟡 P1 |
| `src/Bukit.Importing/HtmlDemoImporter.cs` | +12 | 控制台提示 | 🟢 P2 |

**总计**：~60 行新增/修改，6 个文件。
