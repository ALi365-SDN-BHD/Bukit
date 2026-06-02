# 21 导入 HTML Demo：把本地 Demo 转成 Bukit 站点草稿

当你手里已经有一个本地 HTML demo 目录，并希望 Bukit 生成可维护的主题/站点草稿时，使用 `bukit import html-demo`。

如果目标是按 URL 复制一个在线网站的视觉设计，请看 [18 网站克隆](./18-clone-website.zh-CN.md)。`clone` 从浏览器提取开始；`import html-demo` 从本地 `.html`、CSS、图片和资源文件开始。

## 会生成什么

```bash
bukit import html-demo ./demo --theme silkroadbiz --force --verify
```

默认输出：

- `themes/silkroadbiz/`：生成的 layouts、partials、assets、static 和 `bukit.templates.yaml`
- `sites/silkroadbiz/site.yaml`：生成的站点配置
- `sites/silkroadbiz/content/`：当构建源是 Markdown 时生成的 Markdown 草稿
- `sites/silkroadbiz/notion-seed/` 或 `sites/silkroadbiz/data/`：用于审核/导入的 seed 文件
- `sites/silkroadbiz/original-demo/`：保留的原始 HTML demo
- `sites/silkroadbiz/import-report.md`：转换报告与人工审核清单

`--verify` 会针对生成的 `site.yaml` 运行 `bukit doctor` 和 `bukit build`。

## 推荐第一轮流程

```bash
# 1. 只分析，不写文件
bukit import html-demo ./demo --theme silkroadbiz --dry-run

# 2. 生成可本地构建的草稿
bukit import html-demo ./demo --theme silkroadbiz --force --verify

# 3. 阅读报告
cat sites/silkroadbiz/import-report.md
```

第一轮完成后，用 preview/dev 打开生成站点，在桌面、平板、手机宽度下对比原 demo。

## Content Source 与 Build Source

`--content-source` 决定生成什么 seed 文件，支持 `notion`、`json`、`yaml`。

`--build-source` 决定生成站点从哪里构建，支持 `markdown`、`notion`。

默认是：

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source markdown
```

这会生成 Notion seed 文件供审核，但站点仍从本地 Markdown 构建。因此 `--verify` 不需要 `NOTION_TOKEN`，也不会访问外部 API。

只有在生成站点需要构建时直接读取 Notion，才使用：

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source notion
```

`--build-source notion` 只能和 `--content-source notion` 一起使用。

## Seed 导入

如果你已经有 JSON/YAML seed 文件，只想转成本地 Markdown 内容：

```bash
bukit import seed sites/silkroadbiz/data \
  --output sites/silkroadbiz/content \
  --force
```

`import seed` 不会写入 Notion，它只是本地构建适配器。

## Notion 推送

`import html-demo` 默认不会写入 Notion。先生成 dry-run 计划：

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --dry-run
```

推送到单个 database：

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-id <notion-database-id>
```

使用多 database map：

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-map sites/silkroadbiz/notion-seed/notion-database-map.yaml
```

显式创建缺失的 mapped databases：

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --create-missing-databases \
  --parent-page-id <notion-parent-page-id>
```

也可以在 import 后立即推送，但必须显式开启：

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --push-notion \
  --notion-database-id <notion-database-id>
```

`--push-notion` 不能和 `--dry-run` 同时使用。

## 重要参数

| 参数 | 含义 |
|---|---|
| `--theme <name>` | 必填目标主题名 |
| `--site-path <dir>` | 覆盖生成站点目录，默认 `sites/<theme>` |
| `--content-source <notion|json|yaml>` | seed 输出类型 |
| `--build-source <markdown|notion>` | 生成站点的构建 provider |
| `--route-map <file>` | 可选路由/模板覆盖表，相对 demo 目录解析 |
| `--strict` | strict 诊断直接失败 |
| `--strict warn` | strict 诊断只报告不失败 |
| `--no-extract-content` | 只生成主题/配置，不抽取内容 |
| `--no-seed` | 跳过 seed 文件 |
| `--no-preserve-html` | 不复制原始 HTML demo 到 `original-demo/` |
| `--no-report` | 跳过 `import-report.md` |
| `--push-notion` | import 后把生成 seed 写入 Notion |

## 审核清单

- `import-report.md` 中的页面路由、模板、组件、seed 文件符合预期。
- `bukit doctor --config sites/<theme>/site.yaml` 通过。
- `bukit build --config sites/<theme>/site.yaml` 通过。
- 发布前替换 `site.url`。
- 原始 HTML 保存在 `original-demo/`，不要进入主题 `static/`。
- 完成前做桌面/平板/手机的视觉对比。

相关 agent skill：[bukit-import](../../src/skills/bukit-import/SKILL.md)。
