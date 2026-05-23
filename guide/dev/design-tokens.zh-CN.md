# Design Tokens 参考

Design Tokens 是主题的视觉原子，通过 `tokens.yaml` 定义颜色、字体、圆角、间距和布局变量，构建时自动生成为 CSS 自定义属性（custom properties）。

实现参考：
- `src/Bukit.Theme/Models/ThemeTokens.cs`
- `src/Bukit.Theme/ThemeTokensLoader.cs`
- `src/Bukit.Theme/ThemeTokensProcessor.cs`

## tokens.yaml 格式

```yaml
colors:
  primary: "#0b5fff"
  accent: "#0f7b6c"
  bg: "#ffffff"
  surface: "#f8fafc"
  text: "#1a1a2e"
  text_muted: "#6b7280"
  border: "#e5e7eb"

font:
  family_base: "'Inter', system-ui, sans-serif"
  family_heading: "'Inter', system-ui, sans-serif"
  size_base: "1rem"
  size_sm: "0.875rem"
  size_lg: "1.125rem"
  size_xl: "1.25rem"
  size_2xl: "1.5rem"

radius:
  sm: "4px"
  md: "8px"
  lg: "12px"
  full: "9999px"

spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  section: "64px"

layout:
  content_max: "720px"
  wide_max: "1200px"
  header_height: "64px"
```

### 顶级字段

| 字段 | 说明 | CSS 前缀 |
|---|---|---|
| `colors` | 颜色变量 | `--color-*` |
| `font` | 字体相关变量 | `--font-*` |
| `radius` | 圆角变量 | `--radius-*` |
| `spacing` | 间距变量 | `--spacing-*` |
| `layout` | 布局变量 | `--layout-*` |

每个字段内使用 `snake_case` 的 key，生成的 CSS 变量使用 `kebab-case`（下划线替换为连字符）。

## CSS 生成规则

`ThemeTokensProcessor.GenerateCss()` 将 tokens 转换为：

```css
:root {
  --color-primary: #0b5fff;
  --color-accent: #0f7b6c;
  --color-bg: #ffffff;
  --color-surface: #f8fafc;
  --color-text: #1a1a2e;
  --color-text-muted: #6b7280;
  --color-border: #e5e7eb;
  --font-family-base: 'Inter', system-ui, sans-serif;
  --font-family-heading: 'Inter', system-ui, sans-serif;
  --font-size-base: 1rem;
  --font-size-sm: 0.875rem;
  --font-size-lg: 1.125rem;
  --font-size-xl: 1.25rem;
  --font-size-2xl: 1.5rem;
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-full: 9999px;
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  --spacing-section: 64px;
  --layout-content-max: 720px;
  --layout-wide-max: 1200px;
  --layout-header-height: 64px;
}
```

Key 转换规则：`snake_case` → `kebab-case`，前缀为字段名。例如 `colors.primary` → `--color-primary`。

## 输出路径

生成的 CSS 文件输出到：

```
dist/assets/css/theme-tokens.css
```

构建时引擎检测到组件化主题（`theme.yaml` 存在）后自动执行 tokens 生成，日志输出：

```
event=tokens.generated output=dist/assets/css/theme-tokens.css
```

## Token 继承

子主题通过 `extends` 继承父主题时，tokens 也会合并。合并规则由 `ThemeTokens.Merge()` 实现：

- **子优先**：子主题 tokens 覆盖父主题同名 key
- **父补充**：子主题未定义的 key 继承父主题值

加载流程：

1. 加载子主题 `tokens.yaml`
2. 加载父主题 `tokens.yaml`（若 `extends` 已设置）
3. 调用 `child.Merge(parent)`，子值覆盖父值

## 在 Scriban 模板中使用 tokens

tokens 不作为 Scriban 变量直接注入模板。推荐在 `base.html` 中通过 `<link>` 引入：

```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/css/theme-tokens.css" />
```

或在页面模板中内联引用变量：

```html
<style>
  .custom-banner {
    background: var(--color-primary);
    padding: var(--spacing-lg);
    border-radius: var(--radius-md);
  }
</style>
```

## 在 CSS 中使用 tokens

主题的 `style.css` 可以直接引用 CSS 自定义属性：

```css
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
}

.card-title {
  color: var(--color-primary);
  font-family: var(--font-family-heading);
  font-size: var(--font-size-lg);
}

.hero {
  max-width: var(--layout-wide-max);
  padding: var(--spacing-section) var(--spacing-lg);
}
```
