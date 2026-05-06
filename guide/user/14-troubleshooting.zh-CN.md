# 14 故障排查：doctor 优先、按现象定位

遇到问题时，不要先猜。建议按这个顺序排查：

1. `doctor`（配置/环境自检）
2. `build --clean`（排除增量缓存影响）
3. 对照 `examples/starter/`（找“能跑的基准”）

开发者版排障文档见：[guide/dev/doctor](../dev/doctor.zh-CN.md)、[guide/dev/cache-clean](../dev/cache-clean.zh-CN.md)。

## 快速命令清单

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## 现象 1：doctor 直接失败（配置校验）

### A）Notion token 缺失

现象：提示缺少 `NOTION_TOKEN` 或 Notion 相关配置不可用。

修复：

- 本地：设置环境变量 `NOTION_TOKEN`
- CI：用 GitHub Actions Secrets 注入（见：[13-部署-GitHub-Pages](./13-deploy-github-pages.zh-CN.md)）

### B）路径不存在（content/theme/build output）

现象：提示某个目录不存在（例如 `content`、`layouts`、`assets`）。

修复清单：

- 确认目录真实存在
- 确认你理解“相对路径基准”（相对 `site.yaml` 所在目录），见：[03-项目目录与约定](./03-project-structure.zh-CN.md)
- 如果你用 `--config path/to/site.yaml`，确保对应目录也在那个配置目录下

### C）字段类型写错（YAML 结构不符合）

典型错误：

- 把列表写成了字符串（例如 `languages: zh-CN` 而不是 `languages: [zh-CN]`）
- 缩进错误导致结构错位

修复：

- 先对照 `examples/starter/site.yaml`、`examples/starter/site.i18n.yaml`
- 再按 [04-配置-site-yaml](./04-site-yaml-config.zh-CN.md) 修正

## 现象 2：build 成功，但页面不见了 / URL 不对

### A）slug/type 改动导致路径变化

现象：你以为页面在 `/pages/about/`，但实际输出到别处。

修复：

- 确认内容的 `type` 与 `slug`
- 不要随意使用 `route/url/outputPath/template` 覆盖字段（除非你明确知道输出路径）

### B）多语言过滤导致内容被排除

现象：站点启用 `languages` 后，某些内容在某语言下“消失”。

修复：

- 给每条内容补 `language`
- 检查语言值是否完全一致（`en-US` 不要写成 `en`）

详见：[11-多语言与SEO](./11-i18n-seo.zh-CN.md)。

## 现象 3：部署后 404（本地 preview 正常）

### A）baseUrl 配错（项目仓库最常见）

症状：

- 首页能开，但 CSS/图片 404
- 或站点内链接点击后 404

修复：

- 项目仓库必须设置 `baseUrl: /<repo>`
- 构建时建议用 CLI 覆盖：`--base-url /<repo> --site-url https://<owner>.github.io/<repo>`

详见：[13-部署-GitHub-Pages](./13-deploy-github-pages.zh-CN.md)。

### B）上传目录错了

症状：GitHub Pages 部署成功，但内容为空。

修复：

- 确认工作流 `upload-pages-artifact` 的 `path` 指向实际输出目录（例如 `_site`）

## 现象 4：preview 端口占用或打不开

修复：

- 用 `--port auto` 自动选择端口
- 或换一个端口：`--port 4174`
- 如果你需要固定端口但被占用，先停掉占用该端口的进程

## 现象 5：改了内容/模板，但输出没变

优先用“排除法”：

1. `build --clean`（保证输出目录被清理）
2. 暂时关闭增量：`--no-incremental`
3. 清理缓存目录：`--cache-dir` 指向的目录（默认 `.cache`）或执行 `clean`

如果你确实依赖增量构建提速，建议先把站点跑通，再逐步打开增量。

## 现象 6：Modules（data）不生效

症状：

- `site.modules.*` 为空
- 首页没有渲染出 banner/faq 等模块

排查清单：

- sources 中 modules 是否为 `mode: data`
- 模块数据是否包含 `type`（决定分组键）
- 主题模板是否读取了 `site.modules`（对照示例主题）

详见：[09-Modules-结构化数据](./09-modules-data.zh-CN.md)。
