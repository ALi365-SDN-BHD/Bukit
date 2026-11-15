# Improve the starter theme

## Why

BukitJalil 开发方案第 16 节第 6 条把“更好看的默认主题”留到 MVP 之后决定。
`bukit new` 合入后，Core 的 StarterTheme 是唯一来源，BukitJalil 升级内置
Bukit 后改用它，所以改进放在 Core。

现在的 StarterTheme 有几个和外观无关的缺陷：

- 导航模块链接和标签索引页的 `term.url` 没有加 `site.base_url`，子路径部署
  （GitHub Pages 项目站点）时 404。
- 内容派生的文本（`page.title`、`item.title`、摘要、标签、分类等）没有
  `html.escape`。
- `ListCardPartial` 带有业务字段 `country`、`industry`。
- 界面文字写死成英文（`Home`、`Latest content`、`Previous`…），首页还显示
  “Starter theme”。
- 手机上没有折叠菜单，没有深色模式。

## What Changes

用户批准（2026-11-16）：方案 A（Core 任务）；文字由 `bukit new --language`
写进 `site.yaml`（选项 c）；简洁内容型；做深色模式和手机折叠菜单；先单独修缺陷。

- T1 缺陷修复：链接加 `base_url`，内容文本转义，删除业务字段。
- T2 外观：
  - 模板只从 `site.params.labels.*` 读界面文字，缺省时用英文。
  - `bukit new` 按语言写入 `theme.params.labels`：简体中文（`zh`、`zh-CN`、
    `zh-Hans`、`zh-SG`）写中文，其他语言写英文。`theme.params.latest_heading`
    改为 `labels.latest`。
  - 简洁内容型样式；`prefers-color-scheme: dark` 深色模式；窄屏折叠菜单
    （无 JS 时链接照常显示）。
  - 不加载外部字体或脚本，不需要 SCSS，不改模板文件集合和 `theme.yaml`。
  - 保留 `--primary: #0b5fff;` 和 `--accent: #0f7b6c;` 两行原文，Labs 的
    `ApplyColorOverrides` 依赖它们。

## Non-goals

- 不新增主题、不加 `--theme` 选项、不做主题市场。
- 不在模板里维护翻译表；不支持简体中文以外的内置翻译。
- 不改渲染引擎、配置契约或 public API。
- 不修改 BukitJalil；它升级内置 Bukit 后的迁移另行处理。

## Impact

- 受影响：`Bukit.Cli/Resources/StarterTheme/*`、`StarterSite/SiteYaml.yaml`、
  `Commands/New/StarterSiteScaffold.cs` 及对应测试、CHANGELOG、quick start。
- 已生成的网站自带主题副本，不受影响。
- Labs 的 `theme`/clone 生成器读同一组资源，外观随之变化；颜色覆盖仍可用。
