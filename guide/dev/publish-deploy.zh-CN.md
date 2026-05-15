# 发布与部署（Publish / Deploy）

本项目的“发布与部署”分两层：

1. 发布 bukit CLI（可选 AOT）
2. 用 bukit 构建出静态站点产物并部署到静态托管平台（GitHub Pages 等）

本页把两者的最佳实践、关键参数（baseUrl/siteUrl）与 CI 配置收敛成一份稳定说明。

相关文档：
- [CLI](./cli.zh-CN.md)
- [配置（site.yaml）字段参考](./config-site-yaml.md)
- [AOT 与非 AOT 构建模式](./aot.zh-CN.md)
- [多语言与 SEO](./i18n-seo.zh-CN.md)
- [Webhook](./webhook.zh-CN.md)

## 产物是什么

### 1) 站点产物

`bukit build` 的输出目录由以下来源决定（优先级从高到低）：

1. CLI `--output <dir>`
2. `site.yaml` 的 `build.output`
3. 默认值 `dist`

该目录是纯静态文件目录，可直接上传到任意静态托管平台。

### 2) CLI 产物

发布（publish）bukit CLI 时会得到可执行文件：
- 非 AOT：依赖 .NET Runtime 的产物（适合开发/可调试）
- AOT：原生单文件可执行（适合发布部署）

两种模式的行为差异见：[aot.md](./aot.zh-CN.md)。

## 本地发布 bukit CLI（用于部署机/CI）

权威配置来源：`src/Bukit.Cli/Bukit.Cli.csproj`

### AOT 发布

示例（Linux x64）：

```bash
dotnet publish src/Bukit.Cli -c Release -r linux-x64 -o out/bukit /p:PublishAot=true
```

说明：
- `-r` 必须指定 RID（例如 linux-x64/win-x64/osx-x64）
- AOT 模式下 external 插件加载（`plugins/*.dll`）不会启用（见 [aot.md](./aot.zh-CN.md)）

### 非 AOT 发布

示例（framework-dependent）：

```bash
dotnet publish src/Bukit.Cli -c Release -o out/bukit
```

适用：
- 需要更快编译/更友好调试
- 需要 external 插件加载（`plugins/*.dll`）能力

## 部署到 GitHub Pages（推荐路径）

Bukit 提供两种部署方式：

### 方式 A：`bukit deploy`（引擎内置，推荐）

一条命令完成构建与部署：

```bash
bukit deploy                           # 构建 + 推送
bukit deploy --dry-run                 # 预览
bukit deploy --branch pages            # 自定义分支
bukit deploy --skip-build --ci         # CI 模式，跳过构建
```

前置条件：`git` 可用、`GITHUB_TOKEN` 环境变量已设置、remote origin 指向 GitHub。

`bukit deploy` 自动推导 user/org pages 与 project pages 的 `baseUrl` 并正确部署。详见 [13-deploy-github-pages](../../guide/user/13-deploy-github-pages.zh-CN.md)。

### 方式 B：GitHub Actions CI/CD（自动部署）

### 必需配置

1. GitHub 仓库：Settings → Pages → Build and deployment 选择 “GitHub Actions”
2. 如使用 Notion：Settings → Secrets and variables → Actions 添加 `NOTION_TOKEN`

### baseUrl 与 siteUrl（最常见的 404 来源）

GitHub Pages 常见两种 URL 形态：

1. `owner.github.io`（用户/组织站）
   - baseUrl：`/`
   - siteUrl：`https://owner.github.io`
2. `owner.github.io/repo`（仓库站）
   - baseUrl：`/repo`
   - siteUrl：`https://owner.github.io/repo`

工作流中已经自动根据仓库名计算这两个值，并用 CLI 覆盖：
- `--base-url "$BASE_URL"`
- `--site-url "$SITE_URL"`

你在自建 CI 时也应遵循同样的规则。

更多与 sitemap/rss/search 的模式关系见：[i18n-seo.md](./i18n-seo.zh-CN.md)。

## 部署到其他静态托管（Nginx / OSS / Netlify / Vercel）

通用原则：
- 只要能把 `build.output` 目录当作静态根目录发布即可
- 站点路径不在根目录时，需要正确设置 `site.baseUrl`（或用 `--base-url` 覆盖）
- 需要 sitemap/rss 绝对 URL 时，需要配置 `site.url`（或用 `--site-url` 覆盖）

最小部署步骤：
1. 在 CI 中执行 `bukit build --clean`
2. 将输出目录（dist 或你指定的目录）上传/同步到静态托管

## Webhook 触发部署（Notion 更新自动触发）

如果你使用 `bukit webhook` 把 Notion webhook 转为 GitHub `repository_dispatch`，可参考：
- [Webhook](./webhook.zh-CN.md)
- 你自建的 workflow（例如 `.github/workflows/release.yml`）

## 常见问题

1. 部署后页面 404
   - 首先检查 baseUrl 是否与部署路径一致（尤其是 GitHub Pages 的 repo pages）
2. sitemap/rss 链接不对
   - 确认 `site.url` 或 `--site-url` 指向最终公网 URL
3. 插件本地可用，发布后不可用
   - 检查是否使用了 AOT 发布；AOT 下不支持 external 插件扫描加载（见 [aot.md](./aot.zh-CN.md)）
