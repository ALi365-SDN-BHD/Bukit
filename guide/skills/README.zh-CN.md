# Bukit Core 1.0 Agent Skills

这是面向 Core 1.0 的新 skills 包，旧包只作为输入来源。新包只默认加载稳定 Core 能力，并把 clone/import/webhook/theme registry/theme wizard/template tools 等历史或实验能力隔离到 `guide/labs-skills/`。

## Core 命令边界

默认 Core 只包含：

`build`、`doctor`、`config`、`preview`、`dev`、`clean`、`version`、`completion`、`seo`、`geo`、`publish`、`deploy`。

如果用户没有明确要求 Labs / experimental，不要把 `guide/labs-skills/` 技能作为默认路径。

## 使用顺序

1. Bukit 任务先加载 `using-bukit`。
2. 需要命令时先加载 `bukit-cli-reference`。
3. 配置、内容、主题、路由、i18n、SEO、GEO、部署、调试都先以 `bukit-config` 为背景。
4. 模板工作先读 `bukit-theme`，再读 `bukit-templating`。
5. 构建异常、doctor 输出、内置插件、路由冲突、输出安全等问题使用 `bukit-debug`。

## 校验

```bash
bash guide/skills/scripts/validate-skills-strict.sh
```

严格校验会检查索引同步、source anchors、guide chapters、Core 命令漂移、非 Core 命令误引用，以及 Core skills 中的开发服务器术语误称。
