# Bukit Seed Data Specification

## 1. Purpose

This specification defines the content data contracts that AI must follow when generating Bukit Demo-to-CMS seed files.

Applicable files:

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

Default Notion push scope:

```text
pages
posts
companies
services
```

Default review-only scope:

```text
sections
faqs
media
components
```

## 2. Common Fields

| Field | Type | Required | Description |
|---|---|---:|---|
| `title` | string | yes | Title |
| `slug` | string | yes | Unique identifier for URL and upsert |
| `summary` | string | no | Summary |
| `content` | string | no | HTML body |
| `published` | boolean | yes | Publish status |
| `language` | string | no | Language |
| `seoTitle` | string | no | SEO title |
| `seoDescription` | string | no | SEO description |
| `cover` | string | no | Cover image |
| `tags` | array | no | Tags |

## 3. Slug Rules

A slug must be lowercase, use letters, numbers, and hyphens, contain no spaces, and be unique within the same collection.

Valid examples:

```text
malaysia-digital-economy
ali365
website-development
```

Invalid or discouraged examples:

```text
Malaysia Digital Economy
company/detail
my company
```

## 4. pages.json

Required fields:

```text
title
slug
type
published
```

Recommended fields:

```text
template
summary
content
seoTitle
seoDescription
language
```

Example:

```json
[
  {
    "title": "Home",
    "slug": "",
    "type": "Home",
    "template": "index",
    "summary": "A business information platform connecting China and Malaysia.",
    "content": "<p>Silkroad Business connects companies, insights, services, and opportunities.</p>",
    "seoTitle": "Silkroad Business - China Malaysia Business Platform",
    "seoDescription": "Business insights and company directory for China-Malaysia opportunities.",
    "published": true
  }
]
```

## 5. posts.json

Required fields:

```text
title
slug
summary
content
published
```

Recommended fields:

```text
tags
category
cover
publishDate
seoTitle
seoDescription
language
```

## 6. companies.json

Required fields:

```text
title
slug
summary
published
```

Recommended fields:

```text
country
industry
logo
website
contact
content
seoTitle
seoDescription
language
```

## 7. services.json

Required fields:

```text
title
slug
summary
published
```

Recommended fields:

```text
category
icon
content
seoTitle
seoDescription
language
```

## 8. sections.json

Default status: review-only.

Recommended fields:

```text
id
page
type
title
summary
content
sortOrder
```

## 9. faqs.json

Default status: review-only unless a dedicated FAQ schema is introduced.

Recommended fields:

```text
question
answer
page
category
sortOrder
published
```

## 10. media.json

Default status: review-only.

Recommended fields:

```text
path
alt
type
usage
relatedSlug
```

## 11. components.json

Default status: review-only.

Recommended fields:

```text
name
type
source
fields
description
```

## 12. Forbidden Output

AI must not:

- Rename `posts` to `articles`.
- Rename `companies` to `businesses`.
- Omit `slug`.
- Generate duplicate slugs.
- Write booleans as strings.
- Use uncontrolled external image URLs as core assets.
- Invent Notion field names.
- Promote review-only files into default Notion push scope without schema design.

## 13. Validation

Check that:

```text
All JSON files are valid
Slugs are unique
Required fields exist
published is boolean
Image paths exist or are replaceable
content is HTML
Notion push scope is clear
review-only scope is clear
```
