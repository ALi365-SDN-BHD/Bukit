# `bukit.templates.yaml` 模板清单规范

## 1. 目标

`bukit.templates.yaml` 用于描述主题中可用的布局、页面模板、局部模板和组件模板，帮助 AI 与 Bukit 保持模板命名一致。

AI 不得生成与真实模板文件不一致的清单。

## 2. 推荐结构

```yaml
layouts:
  base: layouts/base.html

pages:
  index: pages/index.html
  insights: pages/insights.html
  article: pages/article.html
  companies: pages/companies.html
  company: pages/company.html
  about: pages/about.html
  contact: pages/contact.html

partials:
  header: partials/header.html
  nav: partials/nav.html
  footer: partials/footer.html

components:
  article-card: components/article-card.html
  company-card: components/company-card.html
  service-card: components/service-card.html
  faq: components/faq.html
```

## 3. 命名规则

- key 使用短横线或小写英文。
- value 必须是相对 `layouts/` 的路径。
- 页面模板路径推荐 `pages/*.html`。
- partial 路径推荐 `partials/*.html`。
- component 路径推荐 `components/*.html`。
- 不允许 `..` 路径。
- 不允许绝对路径。
- 不允许重复 key。

## 4. 与 route-map 的关系

`demo.routes.yaml` 中：

```yaml
template: company
```

应对应：

```yaml
pages:
  company: pages/company.html
```

## 5. 生成后验证

```text
每个模板文件真实存在
每个 route-map template 能在 pages 中找到
partials/components include 路径正确
不存在 .. 路径
不存在绝对路径
```
