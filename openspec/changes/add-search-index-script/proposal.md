# Add a script form of the search index

## Why

内置的两个搜索界面都用 `fetch` 读取 `search.json`：

- Starter 主题的 `layouts/pages/search.html`（`StarterTheme/SearchTemplate.html`）
  请求 `{{ base_url }}/search.json`。
- `bukit-search.html` 片段（`SearchIndexPlugin.GetSearchJs`）请求
  `data-search-url`。

页面的 CSP 不允许 `connect-src` 时，这两个界面都搜不出结果。BukitJalil 的预览就是这种
情况：为了不让 AI 写的页面脚本访问管理端口，预览的响应头是
`sandbox allow-scripts allow-popups; connect-src 'none'; form-action 'none'`。
发布后的网站不受影响。

BukitJalil 一侧放宽 CSP 解决不了（2026-11-16 在 Chromium、Firefox、WebKit 里实测，
Playwright 1.65.0）：

| 预览 CSP | Chromium / Firefox | WebKit | `<script src>` |
|---|---|---|---|
| `connect-src 'none'`（现状） | 违反 CSP | 违反 CSP | 三个浏览器都能加载 |
| `connect-src 'self'` | CORS 拦截（来源为 `null`） | 违反 CSP | 能加载 |
| `'self'` + `Access-Control-Allow-Origin: *` | 能读 | 违反 CSP | 能加载 |

加 CORS 头还会让任何网页都能读本机预览里的未发布内容。所以要由 Bukit 提供一个不依赖
`connect-src` 的加载方式：用 `<script>` 加载索引。预览的 CSP 没有 `script-src`，
外部同源脚本不受限制。

## What Changes

- 每写一个 `search.json`，同目录写一个 `search.json.js`，内容是
  `self.bukitSearchIndex=<与 search.json 完全相同的 JSON>;`。根目录合并索引和各语言索引
  都写；`search.index.json`（`index` 模式的清单）不写。
- 两个内置搜索界面先 `fetch` JSON；失败（网络错误、CSP 拦截、非 2xx、不是数组）时插入
  `<script src="<索引地址>.js">` 再试一次，读 `self.bukitSearchIndex`。两种都失败时行为
  和现在一样（片段显示 "Search is unavailable"）。发布后的网站仍走 `fetch`，行为不变。
- 投影计划、构建清单归属、增量清理、静态文件冲突检查把 `search.json.js` 和对应的
  `search.json` 同样对待。
- 文档：内置输出、站点配置里的搜索说明；CHANGELOG 三语；说明已有主题的
  `search.html` 不会被改写，要用新行为需按文档复制加载代码。

### 已确认的决定（2026-11-16）

1. 文件名 `search.json.js` 和全局变量名 `bukitSearchIndex`（不用 `search.js`，避免和
   主题自带的 `static/search.js` 冲突）。
2. 总是生成，不加配置开关。代价是输出里多一份与索引同样大小的文件。
3. 加载顺序为先 `fetch` 后 `<script>`，保证发布后的网站行为不变；代价是在禁止
   `connect-src` 的页面上，控制台会多一条 CSP 报错。

## Capabilities

### New Capabilities

- `search-index-script`：脚本形式的搜索索引，以及内置搜索界面在 `fetch` 失败时改用它。

### Modified Capabilities

无。`i18n-output-layout` 规定的 `search.json` 路径不变；新文件跟随这些路径，写在新
capability 里。

## Impact

- 代码：`SearchIndexBuilder`（单语言和合并索引的写出）、`SearchIndexPlugin`（片段 JS）、
  `StarterTheme/SearchTemplate.html`、`PublicOutputLifecycle`（投影计划）、
  `I18nRootProjectionCoordinator` / 根目录冲突检查、相关测试。
- 发布审计：`search.json.js` 不是新的聚合表示，不加入 `PublishRepresentations`；审计仍只
  读 `search.json`。
- 跨仓影响：BukitJalil 的预览因此能搜索；BukitJalil 不改 CSP，升级内置 Bukit 后在三个
  浏览器的预览里验证。其他使用 Bukit 输出的仓库（如 SRBiz-bukit）只会在输出里多一个文件。

## Non-goals

- 不改写已经用脚手架生成到用户主题里的 `search.html`。
- 不改 `search.json` 的格式、路径和记录内容。
- 不给 `search.index.json` 生成脚本形式，也不加跨语言搜索界面。
- 不在页面里内联索引（大站点的搜索页会变得很大）。
- 不处理 BukitJalil 的 CSP；那边保持 `connect-src 'none'`。

## 批准范围

2026-11-16 用户要求“向 Bukit 提需求”，同日确认上面三个决定并要求实施（单独的 Core
任务）。不包含 commit、push 或发布。
