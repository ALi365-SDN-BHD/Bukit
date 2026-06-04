# Bukit Seed Data 数据规范

## 1. 目标

本规范定义 AI 在生成 Bukit Demo-to-CMS 内容数据时必须遵守的字段合同。

适用文件：

```text
pages.json
posts.json
companies.json
services.json
sections.json
faqs.json
media.json
components.json
```

默认 Notion push 范围：

```text
pages
posts
companies
services
```

默认 review-only 范围：

```text
sections
faqs
media
components
```

---

## 2. 通用字段

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `title` | string | 是 | 标题 |
| `slug` | string | 是 | 唯一标识，用于 URL 和 upsert |
| `summary` | string | 否 | 摘要 |
| `content` | string | 否 | HTML 正文 |
| `published` | boolean | 是 | 是否发布 |
| `language` | string | 否 | 语言，如 `zh` |
| `seoTitle` | string | 否 | SEO 标题 |
| `seoDescription` | string | 否 | SEO 描述 |
| `cover` | string | 否 | 封面图路径 |
| `tags` | array | 否 | 标签 |

## 2.1 slug 规则

`slug` 必须：

- 小写
- 使用英文、数字、连字符
- 不包含空格
- 不包含中文标点
- 同一集合内唯一

合法：

```text
malaysia-digital-economy
ali365
website-development
```

不推荐：

```text
马来西亚数字经济
Malaysia Digital Economy
company/detail
```

---

## 3. `pages.json`

用于页面内容，例如首页、关于、联系、加入我们。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `title` | string | 是 | 页面标题 |
| `slug` | string | 是 | 首页可为空字符串或 `index` |
| `type` | string | 是 | `Home` 或 `Page` |
| `template` | string | 否 | 对应模板名 |
| `summary` | string | 否 | 页面摘要 |
| `content` | string | 否 | 页面正文 HTML |
| `seoTitle` | string | 否 | SEO 标题 |
| `seoDescription` | string | 否 | SEO 描述 |
| `published` | boolean | 是 | 是否发布 |

示例：

```json
[
  {
    "title": "首页",
    "slug": "",
    "type": "Home",
    "template": "index",
    "summary": "连接中国与马来西亚的商务资讯平台。",
    "content": "<p>丝路商讯聚合企业、资讯、服务与合作机会。</p>",
    "seoTitle": "丝路商讯 - 中马商务资讯平台",
    "seoDescription": "聚合中国与马来西亚企业、资讯、服务与合作机会。",
    "published": true
  }
]
```

---

## 4. `posts.json`

用于资讯、文章、博客内容。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `title` | string | 是 | 文章标题 |
| `slug` | string | 是 | 文章 slug |
| `summary` | string | 是 | 摘要 |
| `content` | string | 是 | 正文 HTML |
| `tags` | array | 否 | 标签 |
| `category` | string | 否 | 分类 |
| `cover` | string | 否 | 封面图 |
| `publishDate` | string | 否 | 发布日期 |
| `seoTitle` | string | 否 | SEO 标题 |
| `seoDescription` | string | 否 | SEO 描述 |
| `published` | boolean | 是 | 是否发布 |

示例：

```json
[
  {
    "title": "马来西亚数字经济发展趋势",
    "slug": "malaysia-digital-economy",
    "summary": "解析马来西亚数字经济政策与企业机会。",
    "content": "<p>马来西亚数字经济正在快速发展，为跨境企业带来新机遇。</p>",
    "tags": ["数字经济", "马来西亚", "商务资讯"],
    "category": "商务资讯",
    "cover": "assets/images/news-1.jpg",
    "seoTitle": "马来西亚数字经济发展趋势",
    "seoDescription": "了解马来西亚数字经济发展机会。",
    "published": true
  }
]
```

---

## 5. `companies.json`

用于企业目录。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `title` | string | 是 | 企业名称 |
| `slug` | string | 是 | 企业 slug |
| `summary` | string | 是 | 企业简介 |
| `content` | string | 否 | 企业详情 HTML |
| `country` | string | 否 | 国家 |
| `industry` | string | 否 | 行业 |
| `logo` | string | 否 | Logo 路径 |
| `website` | string | 否 | 官网 |
| `contact` | string | 否 | 联系方式 |
| `seoTitle` | string | 否 | SEO 标题 |
| `seoDescription` | string | 否 | SEO 描述 |
| `published` | boolean | 是 | 是否发布 |

示例：

```json
[
  {
    "title": "ALi365 SDN BHD",
    "slug": "ali365",
    "summary": "专注企业数字化、AI、网站建设与跨境商务服务。",
    "content": "<p>ALi365 提供企业数字化、网站建设、AI 工具与跨境商务服务。</p>",
    "country": "Malaysia",
    "industry": "Technology",
    "logo": "assets/images/ali365.png",
    "website": "https://ali365.com.my",
    "published": true
  }
]
```

---

## 6. `services.json`

用于服务目录。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `title` | string | 是 | 服务名称 |
| `slug` | string | 是 | 服务 slug |
| `summary` | string | 是 | 服务简介 |
| `content` | string | 否 | 服务详情 |
| `category` | string | 否 | 服务分类 |
| `icon` | string | 否 | 图标 |
| `seoTitle` | string | 否 | SEO 标题 |
| `seoDescription` | string | 否 | SEO 描述 |
| `published` | boolean | 是 | 是否发布 |

示例：

```json
[
  {
    "title": "企业网站建设",
    "slug": "website-development",
    "summary": "为企业提供官网、内容站、电商站建设服务。",
    "content": "<p>我们提供从设计、开发到部署的一站式网站建设服务。</p>",
    "category": "数字化服务",
    "published": true
  }
]
```

---

## 7. `sections.json`

用于页面区块，默认 review-only。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `id` | string | 是 | 区块 ID |
| `page` | string | 是 | 所属页面 slug |
| `type` | string | 是 | 区块类型 |
| `title` | string | 否 | 区块标题 |
| `summary` | string | 否 | 区块摘要 |
| `content` | string | 否 | HTML 内容 |
| `sortOrder` | number | 否 | 排序 |

示例：

```json
[
  {
    "id": "home-hero",
    "page": "index",
    "type": "hero",
    "title": "连接中国与马来西亚的商务资讯平台",
    "summary": "聚合企业、资讯、服务与合作机会。",
    "sortOrder": 1
  }
]
```

---

## 8. `faqs.json`

用于 FAQ，默认 review-only，除非项目定义专门 schema。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `question` | string | 是 | 问题 |
| `answer` | string | 是 | 答案 |
| `page` | string | 否 | 所属页面 slug |
| `category` | string | 否 | 分类 |
| `sortOrder` | number | 否 | 排序 |
| `published` | boolean | 否 | 是否发布 |

示例：

```json
[
  {
    "question": "如何加入企业目录？",
    "answer": "提交企业资料后，由平台审核发布。",
    "page": "companies",
    "category": "企业目录",
    "sortOrder": 1,
    "published": true
  }
]
```

---

## 9. `media.json`

用于媒体资源，默认 review-only。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `path` | string | 是 | 本地资源路径 |
| `alt` | string | 否 | 图片替代文本 |
| `type` | string | 否 | image/video/file |
| `usage` | string | 否 | 用途 |
| `relatedSlug` | string | 否 | 关联内容 |

示例：

```json
[
  {
    "path": "assets/images/company-1.png",
    "alt": "ALi365 Logo",
    "type": "image",
    "usage": "company-logo",
    "relatedSlug": "ali365"
  }
]
```

---

## 10. `components.json`

用于记录从 Demo 中识别出的组件，默认 review-only。

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `name` | string | 是 | 组件名称 |
| `type` | string | 是 | 组件类型 |
| `source` | string | 否 | 来源页面 |
| `fields` | array | 否 | 组件字段 |
| `description` | string | 否 | 说明 |

示例：

```json
[
  {
    "name": "company-card",
    "type": "card",
    "source": "companies.html",
    "fields": ["title", "summary", "country", "industry", "logo"],
    "description": "企业列表卡片组件"
  }
]
```

---

## 11. 禁止事项

AI 不得：

- 将 `posts` 写成 `articles`
- 将 `companies` 写成 `businesses`
- 省略 `slug`
- 在同一集合中生成重复 slug
- 将布尔值写成字符串，如 `"published": "true"`
- 将正文写成 Markdown 后伪装为 HTML，除非项目明确允许
- 使用外部不可控图片 URL 作为核心资源
- 随意生成 Notion 字段名

---

## 12. 数据生成后验证

AI 生成 seed 后应检查：

```text
每个 JSON 文件语法有效
每个集合 slug 唯一
必需字段齐全
published 是 boolean
图片路径存在或可替换
content 是 HTML 字符串
Notion push 范围明确
review-only 范围明确
```

推荐执行：

```bash
bukit notion push \
  --input sites/<site-name>/notion-seed \
  --database-map sites/<site-name>/notion-seed/notion-database-map.yaml \
  --dry-run
```
