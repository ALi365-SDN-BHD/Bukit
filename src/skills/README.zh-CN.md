# Bukit Agent Skills

`src/skills/` 存放的是面向 AI Agent 的 Bukit 专项知识与操作指引，而不是运行时代码。它把 Bukit 的常见任务拆成一组可组合的 `SKILL.md` 文件，帮助 Agent 在建站、配置、主题开发、内容接入和排障时快速选对知识边界。

如果你在 Trae、Claude Code、Copilot CLI、Codex CLI、Gemini CLI 等支持 skill 的环境中使用 Bukit，建议把这里当作 Agent 侧的“导航层”：

- 明确提到“using bukit / 使用 bukit”时，先进入 `using-bukit`
- 需要执行命令时，统一参考 `bukit-cli-reference`
- 需要改 `site.yaml`、主题、模板、Notion、路由、多语言或插件时，再进入对应子 skill

## 目录结构

```text
src/skills/
  using-bukit/            # 统一入口与路由
  bukit-cli-reference/    # CLI 操作单一知识源
  bukit-config/           # site.yaml 配置模型
  bukit-theme/            # 主题目录与静态资源
  bukit-templating/       # Scriban 模板开发
  bukit-notion/           # Notion 内容源
  bukit-routing/          # URL 路由与 permalink
  bukit-i18n/             # 多语言站点
  bukit-plugins-debug/    # 插件、增量构建与排障
```

## Skills 分工

| Skill | 主要职责 | 适用场景 |
|---|---|---|
| `using-bukit` | Bukit skill 总入口，识别任务并路由到子 skill | 用户明确说“using bukit / 使用 bukit”，或任务已确定采用 Bukit |
| `bukit-cli-reference` | CLI 检测、安装、命令速查、输出与退出码解读 | 需要执行 `bukit build`、`doctor`、`preview`、`theme`、`webhook` 等命令 |
| `bukit-config` | `site.yaml` 六大顶级节点、场景模板、字段解释 | 创建或修改站点配置、解释字段含义、修复配置校验错误 |
| `bukit-theme` | `layouts/`、`assets/`、`static/` 的分工与主题参数 | 从零搭主题、迁移主题、处理 CSS/静态资源 404、使用 `theme.params` |
| `bukit-templating` | Scriban 语法、layout 继承、数据访问与常见模板模式 | 编写页面模板、列表页、分页组件、排查模板渲染错误 |
| `bukit-notion` | Notion API 接入、字段映射、块渲染、图片本地化 | 用 Notion 做 CMS、排查拉取失败、检查属性映射与图片问题 |
| `bukit-routing` | permalink、集合路由、URL 编码与输出路径 | 自定义 URL 结构、解决路由冲突或 404、配置集合列表页 |
| `bukit-i18n` | 语言检测、独立变体构建、合并 sitemap/RSS/search | 搭建多语言站点、排查语言切换与输出合并问题 |
| `bukit-plugins-debug` | 插件生命周期、增量构建、性能诊断与常见故障排查 | 插件不生效、构建结果异常、构建性能退化 |

## 加载与依赖规则

这些 skill 的设计重点是“边界清晰、组合使用”，因此推荐遵循以下顺序：

1. 入口优先：当任务已经明确是 Bukit 任务时，先看 `using-bukit`
2. 命令单一来源：凡是需要执行命令，都以 `bukit-cli-reference` 为准，其他 skill 不重复维护命令说明
3. 配置作为背景知识：`bukit-theme`、`bukit-notion`、`bukit-routing`、`bukit-i18n`、`bukit-plugins-debug` 都建立在 `bukit-config` 的配置模型之上
4. 主题先于模板：`bukit-templating` 默认依赖 `bukit-theme` 提供目录结构与资源约定

可以把它理解成一条常见工作流：

```text
using-bukit
  -> bukit-cli-reference
  -> bukit-config
  -> bukit-theme / bukit-notion / bukit-routing / bukit-i18n / bukit-plugins-debug
  -> bukit-templating
```

## 推荐阅读路径

### 场景 1：从零创建站点

1. `using-bukit`
2. `bukit-cli-reference`
3. `bukit-config`
4. `bukit-theme`
5. `bukit-templating`

### 场景 2：接入 Notion 作为内容源

1. `using-bukit`
2. `bukit-notion`
3. `bukit-config`
4. `bukit-cli-reference`

### 场景 3：调整 URL、分类页或列表页

1. `using-bukit`
2. `bukit-routing`
3. `bukit-config`
4. `bukit-templating`

### 场景 4：排查构建异常或插件问题

1. `using-bukit`
2. `bukit-plugins-debug`
3. `bukit-config`
4. `bukit-cli-reference`

## 维护约定

为避免 skill 信息和真实实现脱节，维护时建议保持以下规则：

- 每个 skill 固定放在 `src/skills/<skill-name>/SKILL.md`
- `description` 只写“何时触发”，不写泛化介绍
- CLI 指令与执行注意事项只收敛到 `bukit-cli-reference`
- 主题目录、配置字段、CLI 参数要与仓库源码和用户文档保持一致
- 当新增 Bukit 能力时，优先判断应扩充现有 skill，还是新增独立 skill

## 相关文档

- 仓库入口：[`README.zh-CN.md`](../../README.zh-CN.md)
- 用户文档：[`guide/user`](../../guide/user/README.zh-CN.md)
- 开发者文档：[`guide/dev`](../../guide/dev/README.zh-CN.md)
- Skills 设计说明：[`docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md`](../../docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md)
