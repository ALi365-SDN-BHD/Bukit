# 组件化主题系统（Componentized Theme System）

组件化主题系统是 Bukit 的主题新范式，通过 `theme.yaml` 声明主题的 sections（区块）、components（组件）与 pageTemplates（页面模板），实现模板行为的声明式编排。

相关文档：
- [主题清单（theme.yaml）参考](./theme-manifest.zh-CN.md)
- [PageComposer 使用](./page-composer.zh-CN.md)
- [Section Schema 参考](./section-schema.zh-CN.md)
- [Theme Doctor CLI](./theme-doctor.zh-CN.md)
- [Design Tokens 参考](./design-tokens.zh-CN.md)
- [Section 插件系统](./section-plugin.zh-CN.md) — ISectionPlugin 接口
- [Git 主题源](./theme-source.zh-CN.md) — 从 Git 拉取主题
- [组件工具函数](./component-utilities.zh-CN.md) — util.format_date 等
- [性能基准测试](./performance-benchmarks.zh-CN.md)

示例主题：
- `examples/component-theme/themes/component-demo/`

## 与传统 layouts/assets/static 的关系

传统主题依赖 `layouts/`、`assets/`、`static/` 三个目录，引擎通过约定的文件名路由模板。组件化主题在此基础上增加 `theme.yaml` 清单文件，不替代原有目录结构，而是与之共存：

- `layouts/` — 所有模板文件（section 模板、组件模板、页面模板、layout 模板）仍然放在这里
- `assets/` — 仍用于 CSS/JS 等资源文件
- `static/` — 仍用于原样拷贝的静态文件
- `theme.yaml` — **新增**：声明 sections、components、pageTemplates、capabilities、layouts 等元数据

可以理解为：传统主题 = 目录约定驱动，组件化主题 = 声明驱动 + 目录约定共存。

## 目录结构

```text
themes/<name>/
  theme.yaml              # 主题清单（必需）
  tokens.yaml             # 设计令牌（可选）
  layouts/
    layouts/              # layout 模板（base.html 等）
      base.html
      landing.html
    pages/                # 页面模板
      home.html
      page.html
    sections/             # section 模板
      hero/
        hero.html
        schema.json
      card-grid/
        card-grid.html
        schema.json
    components/           # 可复用组件模板
      cards/
        insight-card.html
  assets/
    css/
      main.css
  static/
```

## theme.yaml 最小结构

```yaml
name: component-demo
version: 1.0.0
engine: bukit
min_engine_version: 0.3.0

capabilities:
  i18n: false
  seo: true

layouts:
  default: layouts/base.html

page_templates:
  home:
    template: pages/home.html
    label: Home Page
    accepts:
      type: page

sections:
  hero:
    template: layouts/sections/hero/hero.html
    schema: sections/hero/schema.json

components:
  insightCard:
    template: layouts/components/cards/insight-card.html
    props:
      title: string
      summary: string
```

## 向后兼容性

旧主题（仅通过目录约定工作，没有 `theme.yaml`）仍然可以正常构建。引擎在渲染时检测 `theme.yaml` 是否存在：

- **无 theme.yaml**：回退到传统渲染路径，`theme.zh-CN.md` 中描述的行为完全保留
- **有 theme.yaml**：启用组件化渲染路径，额外获得 `render_section`、`comp.render`（组件化版）等能力

旧主题可以渐进式迁移：添加一个 `theme.yaml` 并逐步声明 sections/components，不需要一次性完成。

## 命名约定（下划线 → PascalCase）

`theme.yaml` 使用 `snake_case`（下划线命名），对应 C# 模型使用 `PascalCase`：

| YAML 字段            | C# 属性            |
|----------------------|--------------------|
| `name`               | `Name`             |
| `display_name`       | `DisplayName`      |
| `min_engine_version` | `MinEngineVersion` |
| `page_templates`     | `PageTemplates`    |
| `required_fields`    | `RequiredFields`   |
| `dark_mode`          | `DarkMode`         |

序列化由 `YamlDotNet` + `UnderscoredNamingConvention` 自动处理。
