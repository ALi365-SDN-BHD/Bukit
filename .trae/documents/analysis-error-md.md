# Analysis: error.md 问题与错误分析

## 概述

文件 `error.md` (9711 行，552KB) 是 `bash scripts/release-gate.sh Release` 的完整输出。总体结果是 **Quality gate OK**（质量门禁通过），但输出中包含了大量 error 级别日志。这些错误分为三大类。

---

## 当前状态分析

### 通过的部分（无问题）

| 阶段 | 结果 |
|------|------|
| `dotnet build` (两次) | 0 Error(s), 0 Warning(s) |
| 单元测试 (~6000+ 测试) | 全部通过，0 Failed |
| Skills 严格校验 (16+ 项) | 全部通过 |
| 文档资源一致性检查 | errors=0, warnings=0 |
| Smoke Golden/Fixture 测试 | 17/17 passed |
| 可重现构建 | OK |
| 覆盖率 (Core 83%, CLI 51.3%) | 超过阈值 |

---

## 错误类别 1: `publish.audit severity=error` — 内容表示不匹配 (16 条)

### 位置
- 第 1793-1807 行（第一轮 smoke 构建）
- 第 5339-5353 行（第二轮 smoke 构建）

### 错误详情

```
publish.representation_json_mismatch  (4 路由 × 2 轮 = 8 条)
publish.representation_markdown_mismatch (4 路由 × 2 轮 = 8 条)
```

影响路由：
- `/zh-CN/blog/about/`
- `/zh-CN/blog/hello-world/`
- `/zh-CN/pages/about/`
- `/zh-CN/pages/hello-world/`

### 错误含义

JSON/Markdown 内容表示与发布文档的 identity、language、trust、provenance 或 entities 不匹配。这出现在 **intent** 模式的 smoke 构建中（多语言环境，`zh-CN` 变体）。

### 影响评估

- **质量门禁仍然通过**（`Quality gate OK`），说明 release-gate.sh 不将这些 audit error 视为阻塞性错误
- 仅发生在 `intent` 构建变体中（其他变体如 blog、i18n-merged、modules、taxonomy 等没有此错误）
- 属于 starter 示例内容的问题，非 Bukit 引擎本身的 bug

---

## 错误类别 2: `ConfigFields` 文档检查 — 配置字段引用错误 (~200+ 条)

### 位置
第 5851 行起，`--- ConfigFields ---` 段

### 典型错误模式

```
Error:  README.md: Referenced config field 'content.md' does not exist in site.yaml schema.
Error:  README.md: Referenced config field 'site.yaml' does not exist in site.yaml schema.
Error:  guide/dev/theme.md: Referenced config field 'site.params' does not exist in site.yaml schema.
Error:  guide/user/04-site-yaml-config.md: Referenced config field 'site.seo' does not exist in site.yaml schema.
```

涉及的 "不存在" 字段：
- `site.yaml`, `site.modules`, `site.params`, `site.seo`, `site.feed`, `site.search`
- `content.md`, `content.zh`, `content.ms.md`, `content.media`, `content.notion`
- `theme.yaml`, `theme.git`, `theme.lock.json`, `theme.tar.gz`
- `taxonomy.json`, `taxonomy.templates`, `taxonomy.categories` 等
- `build.md`, `deploy.md` 等

### 根因分析

这些是 **docs check 工具（ConfigFieldChecker）的两类误报**：

1. **文件名误识别为配置字段**：`ExtractYamlReferences()` 的正则 `\b[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+\b` 匹配诸如 `site.yaml`（配置文件名）、`content.md`（文档名）、`theme.git`、`theme.tar.gz` 等文件格式引用，这些被误当作配置字段引用与 schema 对比。

2. **缺少中间节点路径**：`ExtractAllConfigPaths()` 通过反射遍历 `AppConfig` 的属性树。`WalkType()` 仅在 `IsTerminalType()` 为 true 时添加叶子路径（如 `site.seo.enabled`），在 `IsRecordType()` 分支递归子属性但不添加中间节点（如 `site.seo`、`site.seo.geo`）。文档中合法引用中间节点（如 "the `site.seo` section"）时被误报。

**影响文件范围**：`guide/user/`, `guide/dev/`, `guide/ai/chatgpt/`, `README.md` 等，涵盖全部三语版本，约 200+ 条误报。

---

## 错误类别 3: SKILL.md 文件引用错误 (~30 条)

### 位置
第 9600 行起

### 典型错误

```
Error:  src/skills/bukit-theme/SKILL.md:51: File reference not found: partials/seo.html
Error:  src/skills/bukit-theme/SKILL.md:57: File reference not found: layouts/base.html
Error:  src/skills/bukit-routing/SKILL.md:55: File reference not found: blog/hello-world/index.html
Error:  src/skills/bukit-i18n/SKILL.md:144: File reference not found: dist/sitemap.xml
```

### 根因分析

`FileRefChecker` 通过 `` `backtick` `` 引用和独立路径模式匹配文件路径，然后检查文件在项目根目录下是否存在。SKILL.md 中引用的文件分为两类：

1. **主题模板路径**（`layouts/`, `partials/`, `pages/`）：这些是 Scriban 模板的主题相对路径（如 `layouts/base.html`、`partials/seo.html`），它们只存在于各主题的 `templates/` 目录内，不在项目根目录。

2. **构建输出路径**（`dist/`, `feed/`）和**模板占位符**（`{slug}`, `<lang>`）：这些是运行时生成的路径或占位符，在任何时候都不作为真实文件存在。

---

## 实施修复

### 修复 1: ConfigFieldExtractor.cs — 两个子修复

**文件**: [src/Bukit.Cli/Commands/DocsCheck/ConfigFieldExtractor.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/DocsCheck/ConfigFieldExtractor.cs)

**问题 A**: `ExtractAllConfigPaths()` 只提取叶子节点路径（如 `site.seo.enabled`），不提取中间节点（如 `site.seo`）。文档中引用中间节点时被误报为"不存在"。

**修复**: 在 `WalkType()` 中，`IsRecordType` 为 true 时先添加 `fullPath` 作为中间节点，再递归子属性。

**问题 B**: `ExtractYamlReferences()` 的正则 `\b[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+\b` 过于贪婪，匹配到文件名而非配置字段：
- `site.yaml` → 是配置文件名，不是字段
- `content.md` → 是文档文件名，不是字段
- `theme.git`、`theme.tar.gz`、`theme.lock.json` → 文件格式名
- `taxonomy.json` → 输出文件名

**修复**: 添加 `FileExtensionSuffixes` 集合（`.yaml`, `.yml`, `.json`, `.md`, `.git`, `.tar.gz`, `.lock.json`, `.html`, `.css`, `.xml`, `.txt`, `.csv`, `.js`, `.ts`），在 `ExtractYamlReferences()` 中过滤尾部匹配这些后缀的引用。

### 修复 2: FileRefChecker.cs — 跳过模板和构建输出路径

**文件**: [src/Bukit.Cli/Commands/DocsCheck/FileRefChecker.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/DocsCheck/FileRefChecker.cs)

SKILL.md 中引用的是主题相对模板路径（如 `partials/seo.html`、`layouts/base.html`）和构建输出路径（如 `dist/sitemap.xml`、`feed/atom.xml`），这些不是项目根目录下的真实文件。

**修复**: 添加 `ShouldSkipReferencedPath()` 方法，跳过：
- 包含 `<` 或 `{` 的模板占位符路径
- 以 `/` 开头的绝对路径
- 前缀为 `layouts/`、`partials/`、`pages/` 的主题模板路径
- 前缀为 `dist/`、`feed/` 的构建输出路径

### 调查结果 3: publish.representation_*_mismatch

**根因**: [RepresentationAuditRules.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PublishAuditRules/RepresentationAuditRules.cs) 的审计逻辑本身没有问题。问题在于 [ContentProjectionWriter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProjectionWriter.cs) 与 `I18nOutputMerger` 的交互：多语言构建时，各语言变体的内容投影文件使用相同的 `content/<slug>.json` 路径，合并阶段后写入者覆盖前者（如 `en-US` 覆盖 `zh-CN`），导致审计检查时语言和路由不匹配。

**状态**: 非阻塞性（质量门禁通过），需要单独的设计方案处理投影文件的多语言命名冲突。

---

## 验证结果

- `dotnet build bukit.slnx -c Release -warnaserror` → **0 Warning(s), 0 Error(s)**
- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release` → **869 passed, 0 failed**
