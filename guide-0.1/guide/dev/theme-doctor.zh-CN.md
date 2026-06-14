# Theme Doctor CLI

> **语言说明**：本页目前仅有中文版本。English version pending. Versi Bahasa Melayu belum tersedia.

Theme Doctor 提供组件化主题的诊断、组件列表和目录导出功能。三个子命令均通过 `bukit theme` 分发。

实现参考：
- `src/Bukit.Cli/Commands/ThemeCommand.cs`
- `src/Bukit.Theme/ThemeDoctorCommand.cs`
- `src/Bukit.Theme/ThemeCatalogWriter.cs`

## bukit theme doctor

对组件化主题执行全面诊断，输出带颜色标记的报告。

```bash
bukit theme doctor --config site.yaml
```

### 诊断检查项

`ThemeDoctorCommand.Diagnose()` 按以下顺序执行检查：

1. **theme.yaml 存在性** — 检查文件是否存在，`name` 是否为空，`version` 是否填写
2. **page_templates** — 检查每个 `pageTemplate` 的模板文件是否存在
3. **sections** — 检查每个 section 的模板文件和 schema 文件是否存在
4. **schema 必填字段** — 加载每个 section 的 schema.json，列出 `required: true` 的字段，提醒模板作者必须提供这些 props
5. **components** — 检查组件名是否重复
6. **variants** — 检查每个 variant 的模板文件是否存在
7. **assets** — 检查 `assets.css` 和 `assets.js` 中声明的文件是否存在
8. **extends** — 检查父主题目录是否存在
9. **tokens** — 检查 `tokens.yaml` 是否存在并可解析
10. **硬编码文案检测** — 扫描 section 模板中的中文字符、电话号码、邮箱地址，建议参数化
11. **未使用组件检测** — 检测未使用的组件（当前标记为"尚未实现"）

### 输出标记

| 标记 | 颜色 | 含义 |
|---|---|---|
| `✓` | 绿色 | 检查通过 |
| `✗` / `✘` | 红色 | 错误（不可恢复） |
| `⚠` | 黄色 | 警告 |
| `◌` | 深灰色 | 建议（信息性） |

### 示例输出

```
═══ Theme Doctor Report ═══

  ✓ theme.yaml exists
  ◌ theme.yaml: version field is empty (recommended)
  ✓ pageTemplate 'home' OK
  ✓ pageTemplate 'page' OK
  ◌ section 'hero': no schema defined (recommended)
  ✓ section 'cardGrid': template file found
  ⚠ section 'cardGrid': schema file not found 'sections/card-grid/schema.json'
  ◌ Unused component detection: not yet implemented
  ✓ extends: parent theme 'starter' found
  ✓ tokens.yaml exists

Summary: WARNINGS FOUND
```

## bukit theme list-components

列出主题中所有注册的 sections 和 components。

```bash
bukit theme list-components --config site.yaml
```

### 示例输出

```
Sections:
  cardGrid                  Card grid for displaying content items
  hero                      Main hero section
  testimonials              Client testimonials carousel

Components:
  insightCard               props: [title, summary, url, date]
  authorBadge               props: [name, avatar, bio]
  navLink                   props: [text, url, active]
```

组件列表会显示每个组件的 props 名称。Section 列表会显示 `description` 字段（截断到 50 字符）。

## bukit theme export-catalog

将主题的 section 和 component 元数据导出为 JSON 文件到 `.cache/theme-catalog.json`。

```bash
bukit theme export-catalog --config site.yaml
```

### 输出格式

```json
{
  "theme": "component-demo",
  "version": "1.0.0",
  "description": "Minimal componentized theme demonstrating sections and components",
  "extends": null,
  "sections": [
    {
      "name": "hero",
      "description": "Main hero section",
      "variants": ["centered", "split"],
      "requiredProps": ["headline"],
      "optionalProps": ["subheadline", "ctaText", "ctaUrl"],
      "dataSources": null,
      "bestFor": ["landing", "homepage"]
    }
  ],
  "components": [
    {
      "name": "insightCard",
      "props": {
        "title": "string",
        "summary": "string",
        "url": "string",
        "date": "string"
      }
    }
  ]
}
```

catalog 可用于：
- 外部工具/UI 预览主题能力
- CI 中的差异检测
- 文档自动生成
