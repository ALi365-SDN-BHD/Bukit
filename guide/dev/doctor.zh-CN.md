# doctor（环境与配置自检）

`bukit doctor` 用于在构建前快速发现“配置错误/主题缺文件/Notion 不可达/缓存损坏”等问题，适合本地排障与 CI 预检。

实现参考：`src/Bukit.Cli/Commands/DoctorCommand.cs`

## 基本用法

```bash
bukit doctor --config site.yaml
```

常用覆盖：
- `--site-url <url>`：临时覆盖 `site.url`（用于 sitemap/rss 的绝对 URL 检查）

## 检查清单（权威）

doctor 的检查顺序基本如下：

1. 读取配置并校验（ConfigValidator）
   - 失败：返回码 1，并输出具体的校验错误
2. 解析主题目录（layouts/assets/static）
   - `layoutsDir` 必须存在，否则失败返回 1
3. 检查“必需模板文件”存在（缺失则失败返回 1）
   - `layouts/base.html`
   - `pages/page.html`
   - `pages/post.html`
   - `pages/index.html`
   - `pages/list.html`
4. 解析必需模板（Scriban parse）
   - 任一模板语法错误：失败返回 1，并打印 Scriban 的消息列表
5. 检查 assets/static 目录是否存在
   - 不存在仅告警（⚠），不会失败
6. 检查缓存目录 `.cache/` 下的 manifest JSON
   - 发现不可解析的 JSON：仅告警（⚠），不会失败
7. 发现插件数量（built-in/generated/external）
   - 输出 “Plugins discovered: <n>”
8. Notion 模式下探活（需要 `NOTION_TOKEN`）
   - 若存在 Notion source：缺少 `NOTION_TOKEN` 会失败返回 1
   - 会调用 Notion API 检查 databaseId 可达；失败返回 1
9. **模板变量拼写检查**
   - 对所有 `layouts/` 下的 `.html` 模板执行 AST 解析
   - 提取所有变量引用（`page.title`、`site.params.theme` 等）
   - 与已知字段白名单交叉比对
   - 发现未知变量时输出警告（不会失败，仅 ⚠）
   - 白名单定义：`src/Bukit.Rendering/Scriban/ScribanModelKnownFields.cs`
10. **Extra fields 报告**
    - 检查内容 front matter 中的字段是否在 content model field scope 中声明
    - 发现未声明字段时输出警告
11. **诊断码格式化输出**
    - 所有配置错误使用 `BKT-XXXX` 稳定格式输出
    - 实现：`src/Bukit.Shared/DiagnosticExceptionFormatter.cs`
    - 示例：`[BKT-0601] Refusing to clean unsafe output directory`
12. **主题资产完整性检查（P3-6 新增）**
    - 检查 `theme.assets` 目录下 SCSS 文件的存在性与可编译性
    - 检查 `theme.static` 目录下是否存在可疑符号链接（默认拒绝）
    - 检查主题 `manifest.yaml`/`theme.yaml` 的 `extends` 字段安全性（ThemeNameSanitizer）
13. **Notion 连通性深度检查（P3-6 新增）**
    - 在 Notion 模式下，除基本探活外增加：
    - 检查 `fieldPolicy` 白名单字段在 Notion 数据库中是否实际存在
    - 检查 `includeSlugs` 指定的 slug 是否可查找到对应页面
    - 检查 `cacheDir` 是否可写（如果启用缓存）
14. **配置废弃警告扫描（P3-3 新增）**
    - `ConfigDeprecationScanner` 检测 7 种历史配置模式
    - 发现旧字段时输出迁移建议（如 `rss` → `feed`、`outputPath` → `permalink`）

通过后输出 “Doctor passed”，返回码 0。

## 常见失败与修复

1. “Layouts dir not found”
   - 检查 `theme.layouts` 指向的目录是否存在
   - 如使用 `theme.name`，确认 `themes/<name>/layouts` 目录存在（见 [主题开发](./theme.md)）
2. “Missing templates”
   - 补齐必需模板文件清单中的缺失项
3. “Template parse error”
   - 修复 Scriban 语法错误；优先定位到输出的消息行号
4. “NOTION_TOKEN not set / Notion database check failed”
   - 设置环境变量 `NOTION_TOKEN`
   - 确认 databaseId 正确且 token 有权限访问
