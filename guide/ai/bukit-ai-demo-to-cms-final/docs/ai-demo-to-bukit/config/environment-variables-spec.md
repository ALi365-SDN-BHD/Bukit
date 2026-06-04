# Bukit 环境变量规范

## 1. 目标

本规范统一 AI 在生成 Bukit Notion 配置、构建命令和部署说明时使用的环境变量名称。

AI 不得自行发明 Notion 环境变量名。

## 2. 通用环境变量

| 环境变量 | 必需 | 说明 |
|---|---:|---|
| `NOTION_TOKEN` | 是 | Notion integration token |
| `NOTION_DATABASE_ID` | 单库模式必需 | 单数据库 Notion provider |
| `NOTION_PAGES_DATABASE_ID` | 多库模式必需 | Pages database |
| `NOTION_POSTS_DATABASE_ID` | 多库模式必需 | Posts database |
| `NOTION_COMPANIES_DATABASE_ID` | 多库模式必需 | Companies database |
| `NOTION_SERVICES_DATABASE_ID` | 多库模式必需 | Services database |

## 3. 单数据库模式

```bash
export NOTION_TOKEN="<notion-token>"
export NOTION_DATABASE_ID="<database-id>"
```

## 4. 多数据库模式

```bash
export NOTION_TOKEN="<notion-token>"
export NOTION_PAGES_DATABASE_ID="<pages-db-id>"
export NOTION_POSTS_DATABASE_ID="<posts-db-id>"
export NOTION_COMPANIES_DATABASE_ID="<companies-db-id>"
export NOTION_SERVICES_DATABASE_ID="<services-db-id>"
```

## 5. 禁止命名

AI 不得使用：

```text
PAGES_DB
POSTS_DB
NOTION_PAGE_DB
NOTION_API_KEY
NOTION_SECRET
NOTION_TOKEN_ENV
```

除非用户明确说明项目已有该变量并要求兼容。

## 6. 安全规则

- 不要把真实 token 写入文件。
- 不要把 token 写入 `site.yaml`。
- 只写 `tokenEnv: NOTION_TOKEN`。
- `.env` 不应进入 Demo、theme、site 或 import output。
- 不要把密钥推送到 Git。
