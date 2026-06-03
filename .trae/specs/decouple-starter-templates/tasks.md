# Tasks: 解耦 Starter 共享模板

- [x] Task 1: 在用户指南中补充独立模板最佳实践
  - 在 `guide/user/08-themes-templates.md` 中新增一节 "Using Independent Templates for Different Content Types"
  - 说明三种实现方式：独立 Collection、front matter `route.template` 覆盖、FilteredList
  - 说明共享模板属于 Starter 主题而非引擎核心
  - 提供 site.yaml 和模板的完整示例

- [x] Task 2: 重构 Starter 示例 —— 为 about 页面创建独立 collection 和模板
  - 在 `examples/starter/site.yaml` 中新增 `about` collection，配置 `template: pages/about.html` 和 `listRoute`
  - 创建 `examples/starter/layouts/pages/about.html` 独立模板
  - 更新 `examples/starter/content/about.md` 的 front matter，设置 `collection: about`
  - 更新 `examples/starter/layouts/bukit.templates.yaml` 模板清单，添加 about 模板条目

- [x] Task 3: 验证 Starter 构建正常
  - 运行 `dotnet run --project ../../src/Bukit.Cli -c Release -- build` 构建成功 (exit code 0)
  - 输出 about 页面确认使用 `pages/about.html`（eyebrow 显示 "About" 而非 "Page"）

# Task Dependencies
- Task 2 和 Task 1 可并行执行
- Task 3 依赖 Task 2
