# 13 部署到 GitHub Pages：最小配置、baseUrl 与常见 404

本页面向“普通用户部署”，目标是让你把构建结果稳定发布到 GitHub Pages，并能解释清楚最常见的 404/资源路径问题。

仓库提供了 Pages workflow 模板样例 [`.github/workflows/release.yml`](../../.github/workflows/release.yml)，你可直接复制到自己的仓库使用，或参考本文步骤自行创建。

## 你将获得什么

- GitHub Pages 的最小启用步骤
- baseUrl 与 site.url 的正确配置方式（含自动推导逻辑）
- Notion token 的安全注入方式（Secrets）
- 常见故障：部署后首页可打开但资源 404、或全站 404

## 步骤 1：启用 GitHub Pages（仓库设置）

1. GitHub 仓库 Settings → Pages
2. Build and deployment 选择 “GitHub Actions”

## 步骤 2：准备工作流（在你的仓库创建 pages.yml）

建议 workflow 做三件事：

1. 发布 `bukit`（Native AOT）
2. 根据仓库名自动计算 `BASE_URL` 与 `SITE_URL`
3. 执行 `bukit build` 并把输出上传到 Pages

关键片段（解读版）：

- 自动计算 URL（用户/组织页与项目页不同）：

```bash
REPO_NAME="${GITHUB_REPOSITORY#*/}"
OWNER="${GITHUB_REPOSITORY%/*}"
if [[ "$REPO_NAME" == *.github.io ]]; then
  BASE_URL=/
  SITE_URL=https://${OWNER}.github.io
else
  BASE_URL=/${REPO_NAME}
  SITE_URL=https://${OWNER}.github.io/${REPO_NAME}
fi
```

- 构建命令（注意 `--base-url` 与 `--site-url`）：

```bash
./out/bukit/bukit build --config <你的site.yaml> --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

## 步骤 3：把工作流改成“构建你的站点”

你需要改两处：

### 1）改 `--config` 指向你的配置文件

例如你的配置在仓库根目录 `site.yaml`：

```bash
./out/bukit/bukit build --config site.yaml --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

如果你使用多站点（`sites/blog.yaml`），推荐直接用 `--site blog`（并确保 rootDir 是仓库根目录）：

```bash
./out/bukit/bukit build --site blog --output _site --base-url "$BASE_URL" --site-url "$SITE_URL" --ci --clean
```

### 2）改 upload-pages-artifact 的 path 指向实际输出目录

如果你构建输出到 `_site`，上传路径也应该是 `_site`：

```yaml
- uses: actions/upload-pages-artifact@v3
  with:
    path: _site
```

## Notion 站点：注入 NOTION_TOKEN（Secrets）

如果你使用 Notion provider，需要在工作流中注入 `NOTION_TOKEN`。推荐做法：

1. GitHub 仓库 Settings → Secrets and variables → Actions
2. 新建 Secret：`NOTION_TOKEN`
3. 在工作流 build step 中注入环境变量：

```yaml
env:
  NOTION_TOKEN: ${{ secrets.NOTION_TOKEN }}
```

安全原则：

- 不要把 token 写进 `site.yaml`
- 不要在日志中打印 token

## baseUrl 与 site.url：怎么配才不会 404

### 1）你是用户/组织主页仓库：<owner>.github.io

访问地址：`https://<owner>.github.io/`

- `baseUrl`：`/`
- `site.url`：`https://<owner>.github.io`

### 2）你是项目仓库：<repo>

访问地址：`https://<owner>.github.io/<repo>/`

- `baseUrl`：`/<repo>`
- `site.url`：`https://<owner>.github.io/<repo>`

工作流里已经按这个规则自动推导并用 CLI 覆盖，所以你通常不需要在 `site.yaml` 写死这些值（尤其是想复用同一站点到不同 repo 时）。

## 常见故障与修复

### 1）首页能开，但 CSS/图片 404

原因：baseUrl 配错或主题模板没拼 baseUrl。

修复：

- 确认工作流传入 `--base-url` 正确（项目仓库必须是 `/<repo>`）
- 在主题里确保资源链接考虑 baseUrl（例如 `/assets/style.css` 在子路径下会 404）

### 2）全站 404

优先检查：

- GitHub Pages 是否启用了 GitHub Actions 部署
- upload-pages-artifact 的 `path` 是否指向了真正的输出目录
- `bukit build` 是否成功生成 `index.html`

### 3）sitemap/rss 里的 URL 不对

原因：`site.url` 不正确。

修复：

- 在构建命令中传 `--site-url`（工作流已自动推导）
- 或在 `site.yaml` 写正确的 `site.url`
