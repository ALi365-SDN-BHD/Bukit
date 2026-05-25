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

### D）路由冲突（Route Conflict）

现象：`doctor` 或 `build` 报 `Route conflict on url` 或 `Route conflict on outputPath`。

修复清单：
- 两篇内容 slug 相同 → 修改 slug 名称，或使用不同 collection 路由
- 两篇内容的 `route.outputPath` 覆盖值相同 → 确保唯一性
- 内容页 URL 与派生页（分页/归档/分类）冲突 → 改 `deriveConflictPolicy` 为 `warn` 或 `last-wins`，或调整冲突 URL

先跑 `bukit doctor` 可以在不完整 build 的情况下提前发现冲突。

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

### dev server 相关

**现象：`bukit dev` 启动后文件变更没有触发更新**

- 确认没有使用 `--no-watch` 参数
- 确认修改的文件在监控目录内（content/、themes/、layouts/、assets/、static/）
- `.cache/` 和 `dist/` 目录不会触发监控
- Touch 文件后再试（某些编辑器不会触发 LastWrite 事件）

**现象：HMR 实时刷新不生效（浏览器没有自动刷新）**

- 打开浏览器控制台查看 WebSocket 连接是否成功
- 确认访问的是 `bukit dev` 提供的 HTTP 地址（不是 `bukit preview`）
- 防火墙或代理可能阻断 WebSocket 连接

**现象：SCSS 文件没有被编译**

- 安装 `sass` CLI：`npm install -g sass`
- 确认 `theme.scss.enabled: true`
- `.scss` 文件必须放在 `assets/` 目录中

**现象：图片没有被转换为 WebP**

- 安装 `cwebp` 或 `magick`（ImageMagick）
- macOS：`brew install webp`
- Linux：`sudo apt install webp`
- 确认 `theme.images.enabled: true`

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

## 症状 7：clean 拒绝删除输出目录

症状：

- `build --clean` 失败，提示"output directory clean refused"
- 输出目录没有被删除

原因：Bukit 现在要求输出目录中存在 `.bukit-output-marker` 文件才允许清理。这防止了意外删除非 Bukit 目录（如项目根目录、home 目录、`.git` 目录）。

修复：

- 如果目录是 Bukit 创建的：先运行一次完整构建（会写入 marker），再 clean。
- 如果目录不是 Bukit 输出：手动删除，或选择其他输出目录。
- 如果 `build.output` 指向了一个已有的非 Bukit 目录：将 `build.output` 改为专有目录。

## 症状 8：插件 stdout/stderr 超限

症状：

- 构建失败，提示"stdout limit exceeded"或"stderr limit exceeded"
- 某个外部插件进程被杀死

原因：外部插件产生的输出超过了配置的 `maxStdoutBytes` / `maxStderrBytes` 上限。

修复：

- 在 `site.externalPlugins.<name>.maxStdoutBytes` / `maxStderrBytes` 中增大限制。
- 或删除该配置字段以允许无限制输出。
- 排查插件为何产生大量输出——可能是插件本身的 bug。

## 症状 9：主题锁文件 commit 不匹配

症状：

- 构建失败，提示"Theme lock mismatch for ... locked commit ..., current commit ..."
- 之前正常工作的远程主题现在失败

原因：远程主题（`theme.source`）之前构建时被锁定到了某个 Git commit。缓存的主题与 `bukit-theme.lock.json` 记录的 commit 不一致。

修复：

- 删除主题的本地缓存目录和锁文件，重新构建以重新克隆。
- 或只删除锁文件以强制重新校验。
- 如果是有意更新了主题，需要重新生成锁文件。
