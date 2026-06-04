# `demo.routes.yaml` 路由映射规范

## 1. 目标

`demo.routes.yaml` 用于明确 HTML Demo 页面与 Bukit 页面类型、URL、模板之间的映射关系。

AI 不得依赖文件名猜测页面类型。每个 HTML 文件必须出现在 route-map 中。

## 2. 基本结构

```yaml
pages:
  - source: index.html
    route: /
    type: Home
    template: index
```

## 3. 字段定义

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `source` | string | 是 | Demo 中的 HTML 文件 |
| `route` | string | 是 | 目标 URL |
| `type` | string | 是 | 页面类型 |
| `template` | string | 是 | Bukit 模板名 |
| `slug` | string | 否 | 显式 slug |
| `description` | string | 否 | 页面说明 |

## 4. 允许页面类型

```text
Home
Page
PostList
PostDetail
CompanyList
CompanyDetail
ServiceList
ServiceDetail
Contact
Join
```

## 5. 标准示例

```yaml
pages:
  - source: index.html
    route: /
    type: Home
    template: index

  - source: insights.html
    route: /insights/
    type: PostList
    template: insights

  - source: article-detail.html
    route: /insights/{slug}/
    type: PostDetail
    template: article

  - source: companies.html
    route: /companies/
    type: CompanyList
    template: companies

  - source: company-detail.html
    route: /companies/{slug}/
    type: CompanyDetail
    template: company
```

## 6. 规则

- `source` 必须对应真实 HTML 文件。
- `route` 必须以 `/` 开头。
- 动态详情页必须使用 `{slug}`。
- 列表页和详情页必须分开。
- `template` 不要包含 `.html` 后缀。
- `template` 名称必须与生成的页面模板一致。
- 动态路由不能用 `{slug}` 反推具体内容 slug。

## 7. 生成后验证

```text
每个 HTML 文件都在 pages 中
每个 source 文件存在
每个 route 唯一
每个 template 唯一或有明确复用理由
动态详情页 route 包含 {slug}
```
