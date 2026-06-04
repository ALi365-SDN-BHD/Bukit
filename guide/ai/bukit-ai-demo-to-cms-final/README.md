# Bukit AI Demo-to-CMS 最终规范包

本规范包用于让 ChatGPT、Codex、Claude Code、Cursor、Trae 与其他 AI Agent 理解并执行 Bukit 的两阶段网站生产流程：

```text
用户需求
→ AI 生成可迁移 HTML Demo
→ 用户确认样式、页面与功能
→ AI / Bukit 转换为主题模板、内容数据、Notion seed 与配置文件
→ Bukit 验证、构建与发布
```

## 目录结构

```text
.
├── README.md
├── MANIFEST.md
├── AGENTS.md
├── CLAUDE.md
├── docs/
│   └── ai-demo-to-bukit/
│       ├── README.md
│       ├── engineering-spec.md
│       ├── prompt-template.md
│       └── checklist.md
├── skills/
│   └── bukit-demo-to-cms/
│       └── SKILL.md
├── .agents/
│   └── skills/
│       └── bukit-demo-to-cms/
│           └── SKILL.md
├── .claude/
│   ├── rules/
│   │   └── bukit-demo-to-cms.md
│   └── skills/
│       └── bukit-demo-to-cms/
│           └── SKILL.md
├── .cursor/
│   └── rules/
│       └── bukit-demo-to-cms.mdc
└── .trae/
    └── rules/
        └── bukit-demo-to-cms.md
```

## 文件用途

| 文件 | 用途 |
|---|---|
| `AGENTS.md` | Codex 与支持 AGENTS.md 的 AI Agent 项目级入口规则 |
| `CLAUDE.md` | Claude Code 项目级入口规则 |
| `.agents/skills/.../SKILL.md` | Codex 可自动发现的项目级 Skill |
| `.claude/skills/.../SKILL.md` | Claude Code 可自动发现的项目级 Skill |
| `.claude/rules/...` | Claude Code 详细规则 |
| `.cursor/rules/...` | Cursor 项目规则 |
| `.trae/rules/...` | Trae 项目规则 |
| `skills/.../SKILL.md` | 通用 Skill 源文件，便于其他工具或仓库复用 |
| `docs/ai-demo-to-bukit/*` | 完整工程规范、提示词模板与检查清单 |

## 安装方式

将本目录中的文件复制到 Bukit 仓库根目录，并保留隐藏目录结构：

```bash
cp -R bukit-ai-demo-to-cms-final/* /path/to/Bukit/
cp -R bukit-ai-demo-to-cms-final/.[!.]* /path/to/Bukit/
```

复制前请检查目标仓库中是否已存在 `AGENTS.md`、`CLAUDE.md` 或同名规则文件，避免覆盖已有内容。

## 推荐使用方式

在 AI 工具中提出类似需求：

```text
使用 bukit-demo-to-cms skill，先根据用户需求生成可迁移 HTML Demo。
用户确认样式和功能后，再将最终 Demo 转换为 Bukit 主题模板、内容数据、Notion seed 和配置文件。
```

## 推荐 Bukit 命令

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

## 版本

- 规范版本：v1.0
- 生成日期：2026-06-04
