# Checklist: 解耦 Starter 共享模板

- [x] 文档新增章节准确描述了三种独立模板实现方式（独立 Collection / front matter 覆盖 / FilteredList）
- [x] 文档明确说明共享模板属于 Starter 主题，非引擎核心
- [x] Starter site.yaml 新增 about collection，配置了独立 template（单页 collection 无需 listRoute）
- [x] `pages/about.html` 模板文件已创建，功能独立完整
- [x] `content/about.md` front matter 设置为 `collection: about`
- [x] `bukit.templates.yaml` 清单包含新增模板条目
- [x] `dotnet run --project ../../src/Bukit.Cli -c Release -- build` 构建成功 (exit code 0)
- [x] 输出的 about 页面使用 `pages/about.html`（eyebrow 显示 "About"）而非 `pages/page.html`
- [x] 现有 page collection 的其他页面不受影响（hello-world 仍用 page.html，eyebrow 为 "Page"）
