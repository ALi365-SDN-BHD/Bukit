# Bukit AI Demo-to-CMS 配置合同规范

本目录用于约束 AI 生成 Bukit 配置文件和内容数据文件，避免出现“看起来合理但无法构建”的配置。

## 文件清单

| 文件 | 说明 |
|---|---|
| `site-yaml-spec.md` | `site.yaml` 字段、层级、合法组合、路径规则与常见错误 |
| `site-yaml-profiles.md` | AI 可直接选择的标准 `site.yaml` Profile |
| `seed-data-spec.md` | pages/posts/companies/services 等内容 seed 字段规范 |
| `demo-routes-spec.md` | `demo.routes.yaml` 路由映射规范 |
| `notion-database-map-spec.md` | `notion-database-map.yaml` 规范 |
| `template-manifest-spec.md` | `bukit.templates.yaml` 模板清单规范 |
| `environment-variables-spec.md` | Notion 与构建环境变量命名规范 |

## 机器可读 Schema

Schema 位于仓库根目录：

```text
schemas/
  README.md
  site.schema.json
  demo-routes.schema.json
  notion-database-map.schema.json
  template-manifest.schema.json
  seed/
    pages.schema.json
    posts.schema.json
    companies.schema.json
    services.schema.json
```

## 核心配置生成规则

1. AI 不得自行发明 `site.yaml` 字段。
2. 生成 `site.yaml` 前必须选择标准 Profile。
3. 必须参考 `site-yaml-spec.md`。
4. 不得同时生成 `content.provider` 和 `content.sources`。
5. `build-source notion` 只能与 `content-source notion` 配合。
6. Notion 多数据库模式必须使用 `content.sources`。
7. 配置生成后必须执行 schema validate、`bukit doctor` 和 `bukit build`。
8. 如果验证失败，必须修复配置，不得忽略错误。

## 必须执行的验证流程

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

如果 Bukit 支持配置 Schema 校验：

```bash
bukit config validate --config sites/<site-name>/site.yaml
```

或者使用严格 doctor：

```bash
bukit doctor --config sites/<site-name>/site.yaml --strict
```
