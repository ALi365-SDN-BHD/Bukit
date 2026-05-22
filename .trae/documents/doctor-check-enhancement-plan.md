# Doctor 检查强化计划：Markdown 残留 / 模板硬编码 / 数据库字段缺失

## 背景

当前 `DoctorCommand` 是一个内联的单一静态类，17 个检查步骤全部写在 `RunAsync` 方法中，没有可扩展的检查基础设施。需要在不破坏现有结构的前提下，新增三类诊断检查。

## 基础设施：轻量检查重构（优先，可选）

当前 Doctor 使用 `✔`/`✖`/`⚠` 前缀约定 + 退出码 `0`/`1`。考虑引入最小化的检查抽象来容纳新检查：

```csharp
// 放在 Bukit.Cli/Commands/DoctorChecks/ 目录下
public sealed record DoctorCheckResult(string Name, DoctorCheckSeverity Severity, string Message, string? Detail = null);
public enum DoctorCheckSeverity { Info, Warning, Error }

public interface IDoctorCheck
{
    string Name { get; }
    Task<DoctorCheckResult?> RunAsync(DoctorContext ctx);
}
```

> **注意**：完整的提取重构（ICheck + 注册表）超出本次范围。本次采用**最低侵入**方式：新增检查作为私有方法（与现有 `CheckManifestCompleteness`、`WarnUnusedParams` 等风格一致），仅在需要时提取 `DoctorContext` 参数对象避免参数爆炸。

---

## 一、Markdown 残留检查

### 目标
扫描所有 Markdown 内容文件，检测常见的 Markdown 问题和残留。

### 1.1 Front Matter 问题检测

**检查内容**：
- `---` 未闭合（文件以 `---` 开头但没有对应的结束 `---`）
- YAML 解析失败（front matter 内容不是合法 YAML）
- 文件以 `---` 开头但无有效键值对（空 front matter）

**实现位置**：`DoctorCommand.cs` 新增 `CheckMarkdownFrontMatter()` 私有方法

**代码实现思路**：
1. 通过 `config.Site.Collections` 遍历已配置的集合
2. 对 Markdown provider 的集合，获取 `content_dir`（默认 `config.Content.Markdown?.Dir ?? "content"`）
3. 扫描 `*.md` 文件，逐文件检查 front matter
4. 复用 `MarkdownFolderProvider` 中的 `TryExtractFrontMatter` 逻辑（或直接调用公开方法）

**输出格式**：
```
⚠ 3 Markdown front matter issue(s) found:
  - content/posts/broken.md: unclosed front matter (missing closing ---)
  - content/pages/empty.md: empty front matter block
  - content/posts/bad-yaml.md: failed to parse YAML front matter
```

### 1.2 Markdown 语法残留检测

**检查内容**：
- 未闭合的代码块（`` ``` `` 出现奇数次）
- 空链接/空图片：`[]()`、`![]()`
- 引用链接未定义：`[text][ref]` 但 `[ref]: URL` 不存在
- 纯文本中混入 Markdown 语法残留（仅在严格模式下）

**实现位置**：`DoctorCommand.cs` 新增 `CheckMarkdownSyntax()` 私有方法（Warn 级别）

**代码实现思路**：
1. 读取每个 `.md` 文件的 body 部分（去除 front matter）
2. 用正则检测常见问题
3. 未闭合代码块：统计 `` ``` `` 出现次数，奇数次则警告
4. 空链接：`\[.*\]\(\)` 和 `!\[.*\]\(\)` 正则匹配

**输出格式**：
```
⚠ 2 Markdown syntax suggestion(s):
  - content/posts/example.md: line 42: unclosed code block (3 occurrences of ```)
  - content/pages/about.md: line 15: empty link detected `[click here]()`
```

### 1.3 空内容文件检测

**检查内容**：
- 去除 front matter 后的 body 为空或只有空白字符
- 文件内容仅为 `---\n---\n` 没有实际正文

**实现位置**：`DoctorCommand.cs` 新增 `CheckMarkdownEmptyBody()` 私有方法（Warn 级别）

---

## 二、模板硬编码检查

### 目标
扫描所有 `.html` Scriban 模板文件，检测不应该硬编码的值。

### 2.1 硬编码 URL 检测

**检查内容**：
- 绝对 URL 硬编码在模板中（`https://example.com/...`）
- `href="http"` 或 `src="http"` 开头的非变量引用
- 使用 `href="/"` 或 `src="/"` 而非 `site.base_url` 的场景

**实现位置**：`DoctorCommand.cs` 新增 `CheckHardcodedUrls()` 私有方法（Warn 级别）

**检测模式**：
- 正则 `(href|src)\s*=\s*"(https?://[^"]+)"` 匹配 URL 属性（排除 `{{` 变量）
- 正则 `(href|src)\s*=\s*"/"` 匹配根路径引用

**判断策略**：
- 排除 Scriban 变量内的 URL：`href="{{ site.url }}..."` ✅
- 排除 `href="https://` 在 `<!--` 注释中的内容
- 识别常见无害模式：`xmlns`、`xsi:schemaLocation` 等标准属性

**输出格式**：
```
⚠ 5 hardcoded URL(s) in templates:
  - layouts/layouts/base.html: line 8: href="https://example.com/about" (consider using site.url)
  - layouts/partials/footer.html: line 24: src="/assets/js/main.js" (consider using site.base_url)
```

### 2.2 硬编码文本检测

**检查内容**：
- 中文字符串、英文长句直接在 HTML 中硬编码
- 版权年份：`© 2024` 而非 `{{ site.now | date.to_string "%Y" }}`
- 固定的站点名称/标题直接写入模板而非使用 `{{ site.title }}`

**实现位置**：`DoctorCommand.cs` 新增 `CheckHardcodedText()` 私有方法（Info/Warn 级别）

**检测模式**：
- 使用正则提取 HTML 标签之间的纯文本（去除 `` {{ }} `` 块）
- 对超过 N 个字符（如 >20）的硬编码文本段发出 Info 提示
- 版权年份模式：`© 20\d{2}` 或 `Copyright 20\d{2}` 在 `` {{ }} `` 外

**排除策略**：
- 排除 `<script>`、`<style>`、`<!-- comment -->` 内的内容
- 排除 Scriban 表达式 `{{ ... }}` 和 `{% ... %}`
- 排除纯 HTML 结构标签属性（`class="..."`、`id="..."`）

**输出格式**：
```
⚠ 3 hardcoded text block(s) in templates:
  - layouts/partials/footer.html: line 46: "© 2024 My Company" (hardcoded year, consider {{ site.now }})
  - layouts/partials/announcement.html: line 3: long hardcoded text snippet (52 chars)
```

### 2.3 模板使用的 param 与 theme.yaml 声明不一致

**检查内容**（增强现有 `WarnUnusedParams`）：
- 模板中使用了 `site.theme.params.xxx` 但 `theme.yaml` 或 `site.theme.params` 未声明
- 报告"模板引用了未声明的 theme param"

**实现**：扩展现有 `WarnUnusedParams` 为 `CheckThemeParamsConsistency()`，双向检查

---

## 三、数据库/字段缺失检查

### 目标
跨 `CollectionConfig.Schema`、内容实际字段、模板声明字段三者之间做一致性校验。

### 3.1 Schema → Content：必需字段缺失

**检查内容**：
- 对每个定义了 `Schema` 的集合，扫描其所有内容项
- 检查 `Schema[].Required == true` 的字段在 `meta` 中是否存在且非空
- 如果定义了 `Default` 值则不算缺失

**实现位置**：`DoctorCommand.cs` 新增 `CheckSchemaFieldCompleteness()` 私有方法（Error/Warn 级别）

**代码实现思路**：
1. 遍历 `config.Site.Collections`，筛选有 `Schema` 的集合
2. 调用 `RouteInventoryValidator.BuildContentRoutesAsync` 复用路由构建（已有逻辑在 Doctor 第 202-209 行）
3. 遍历路由结果中的 content items，对每项的 `meta` 调用 `ContentSchemaValidator.Validate`
4. 按必需字段缺失（`Code == "required"`）为 Error，类型不匹配（`Code == "type_mismatch"`）为 Warn

**输出格式**：
```
✖ 2 schema validation error(s):
  - article/my-post (collection: articles): missing required field 'author'
  - article/another (collection: articles): field 'rating' expected type 'number' but got 'string'
```

### 3.2 Template.Fields → Schema 交叉检查

**检查内容**：
- 模板在 `bukit.templates.yaml` 中声明了 `capabilities.fields`，但对应的集合 `Schema` 中没有该字段
- 报告"模板预期字段但 Schema 未定义"

**实现位置**：`DoctorCommand.cs` 新增 `CheckTemplateFieldsVsSchema()` 私有方法（Warn 级别）

**代码实现思路**：
1. 遍历每个集合，获取其 `Template` 和 `ListTemplate` 路径
2. 调用 `TemplateCapabilitiesResolver.GetCapabilities` 获取模板声明的 Fields
3. 比较模板的 Fields 和集合的 Schema 定义
4. 模板声明了 `key: "author"` 但 Schema 中没有 `author` → Warn
5. 反之，Schema 定义了字段但模板未声明 → Info（建议同步到 manifest）

**输出格式**：
```
⚠ Template fields vs schema mismatch:
  - articles → pages/post.html: template declares field 'rating' but collection schema has no such field
  - articles → pages/list.html: template declares field 'excerpt' but collection schema has no such field
```

### 3.3 Content → Schema：内容中存在 Schema 未定义的字段

**检查内容**：
- 扫描内容文件的实际 `meta` 字段（排除保留字段）
- 对比集合 Schema 定义，报告未在 Schema 中声明的"野生字段"

**实现位置**：`DoctorCommand.cs` 新增 `CheckExtraContentFields()` 私有方法（Info 级别）

**输出格式**：
```
ℹ Extra fields in content not declared in schema:
  - articles/my-post: field 'legacy_id' not in collection schema
  - articles/another: field 'old_tag' not in collection schema
  (6 extra field(s) total across 2 files)
```

---

## 四、实现步骤

| 步骤 | 任务 | 文件 | 说明 |
|------|------|------|------|
| 1 | 引入 DoctorContext 参数对象 | `DoctorCommand.cs` | 将 `rootDir`、`config`、`layoutsDir` 等常用参数封装为 `DoctorContext` record，减少方法签名膨胀 |
| 2 | 实现 Markdown Front Matter 检查 | `DoctorCommand.cs` | 新增 `CheckMarkdownFrontMatter(DoctorContext ctx)` 方法，检测 YAML front matter 问题 |
| 3 | 实现 Markdown 语法残留检查 | `DoctorCommand.cs` | 新增 `CheckMarkdownSyntax(DoctorContext ctx)` 方法，检测未闭合代码块、空链接等 |
| 4 | 实现空内容检测 | `DoctorCommand.cs` | 新增 `CheckMarkdownEmptyBody(DoctorContext ctx)` 方法 |
| 5 | 实现硬编码 URL 检查 | `DoctorCommand.cs` | 新增 `CheckHardcodedUrls(DoctorContext ctx)` 方法 |
| 6 | 实现硬编码文本检查 | `DoctorCommand.cs` | 新增 `CheckHardcodedText(DoctorContext ctx)` 方法 |
| 7 | 增强 Theme Params 双向检查 | `DoctorCommand.cs` | 扩展现有 `WarnUnusedParams` 为 `CheckThemeParamsConsistency` |
| 8 | 实现 Schema 字段完整性检查 | `DoctorCommand.cs` | 新增 `CheckSchemaFieldCompleteness(DoctorContext ctx)` 方法，调用已有 `ContentSchemaValidator` |
| 9 | 实现 Template Fields vs Schema 交叉检查 | `DoctorCommand.cs` | 新增 `CheckTemplateFieldsVsSchema(DoctorContext ctx)` 方法 |
| 10 | 实现额外字段检测 | `DoctorCommand.cs` | 新增 `CheckExtraContentFields(DoctorContext ctx)` 方法 |
| 11 | 在 RunAsync 中集成所有新检查 | `DoctorCommand.cs` | 按合理顺序添加调用，确保不改变现有退出码行为 |
| 12 | 编写测试用例 | `DoctorCommandTests.cs` | 为每个新增检查编写最小可行测试 |
| 13 | 运行全量测试确保回归 | `dotnet test` | 确保 1842+ 测试全部通过 |

---

## 五、设计原则

1. **最低侵入**：新增检查作为私有方法，与现有代码风格一致（`Check*` / `Warn*` 前缀）
2. **非阻断原则**：Markdown 残留和模板硬编码检查均为 **Warn 级别**，不导致 `return 1`；仅 Schema required 字段缺失为 **Error 级别**
3. **复用现有基础设施**：复用 `ContentSchemaValidator.Validate`、`TemplateCapabilitiesResolver.GetCapabilities`、`RouteInventoryValidator.BuildContentRoutesAsync` 等现有服务
4. **性能可控**：所有新检查仅扫描 `.md` 和 `.html` 文件，不涉及网络 I/O
5. **向后兼容**：不改变现有 17 个检查的行为和退出码逻辑
