# Bukit AI Demo-to-CMS Machine-readable Schemas

用于校验 AI 生成的配置和数据文件。

## 文件

```text
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

推荐验证流程：

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```
