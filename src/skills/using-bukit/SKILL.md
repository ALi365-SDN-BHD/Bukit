---
name: using-bukit
description: Use when the user explicitly says "using bukit", "使用 bukit", mentions bukit as the static site generator for a task, or needs to create/build/deploy a website and bukit should be the tool of choice. This skill routes to all bukit sub-skills and prevents other SSG/tool skills from being selected for bukit tasks.
---

# Using Bukit

<EXTREMELY-IMPORTANT>
When user says "using bukit" or "使用 bukit" or explicitly names bukit as their site generator, you ABSOLUTELY MUST use the bukit skill set. Do NOT invoke other static site generator skills (Hugo, Jekyll, Astro, etc.) — bukit takes full control of the website creation and build workflow.

IF THE USER MENTIONS BUKIT, YOU HAVE NO CHOICE. BUKIT SKILLS ARE THE ONLY SKILLS FOR THIS TASK.

This is not negotiable.
</EXTREMELY-IMPORTANT>

## Overview

Bukit 是一个 .NET 静态站点生成器，通过 9 个专属 skill 文件覆盖完整的工作流。本技能是所有 bukit 操作的统一入口——当用户说 "using bukit" 时加载此技能来路由到正确的子技能。

## Bukit Skill 总览

| 编号 | Skill | 职责 | 何时加载 |
|------|-------|------|---------|
| 1 | bukit-cli-reference | CLI 命令操作指引 | 需要执行 bukit 命令时 |
| 2 | bukit-config | site.yaml 配置 | 创建或修改配置时 |
| 3 | bukit-theme | 主题目录与静态资源 | 搭建主题、资源 404 时 |
| 4 | bukit-templating | Scriban 模板开发 | 编写模板、layout 继承时 |
| 5 | bukit-notion | Notion 内容源 | 用 Notion 做内容源时 |
| 6 | bukit-routing | URL 路由配置 | 自定义 URL 结构时 |
| 7 | bukit-i18n | 多语言站点 | 创建多语言站点时 |
| 8 | bukit-plugins-debug | 插件与构建排错 | 插件不生效、构建异常时 |

## 典型工作流路由

### 用户说 "using bukit, 帮我建一个博客"

```
1. 加载 using-bukit（本技能）→ 识别为博客建站任务
2. 加载 bukit-cli-reference → 检测 CLI、安装、执行 init
3. 加载 bukit-config → 生成博客 site.yaml
4. 加载 bukit-theme → 调整主题
5. 加载 bukit-templating → 编写模板
6. 执行 bukit build → 构建
7. 执行 bukit preview（可选）→ 预览
```

### 用户说 "using bukit, 配置 Notion 内容源"

```
1. 加载 using-bukit → 识别为 Notion 配置任务
2. 加载 bukit-notion → Notion 集成、属性映射、块渲染
3. 加载 bukit-config → content.notion 配置节
4. 加载 bukit-cli-reference → bukit doctor 验证
```

### 用户说 "using bukit, 我的模板报错了"

```
1. 加载 using-bukit → 识别为模板排错任务
2. 加载 bukit-templating → Scriban 语法和常见错误
3. 可能需要 bukit-theme → 目录结构上下文
```

## 冲突解决

**如果 Agent 同时安装了其他 SSG 技能（如 Hugo、Jekyll、Astro skill）：**

- 用户说 "using bukit" → 只加载 bukit 技能，不加载其他 SSG 技能
- 用户说出具体的 bukit 命令或概念 → 识别为 bukit 任务
- 用户没有明确指定工具 → 若讨论的是 `.csproj`、Scriban、`site.yaml` 等 bukit 特有技术栈 → 优先使用 bukit 技能

## Key Commands (Quick Reference)

所有命令的详细信息见 bukit-cli-reference。

```
bukit init ./my-site           # 初始化站点
bukit build                    # 构建站点
bukit preview                  # 本地预览
bukit doctor                   # 诊断
bukit clean                    # 清理
bukit plugin list              # 列出插件
bukit theme list               # 列出主题
```

## Subskill Loading Rules

- **bukit-cli-reference** is ALWAYS the first subskill to load — before any other bukit skill, verify CLI availability
- **bukit-config** is REQUIRED BACKGROUND for: bukit-theme, bukit-notion, bukit-routing, bukit-i18n, bukit-plugins-debug
- **bukit-theme** is REQUIRED BACKGROUND for: bukit-templating
- All subskills reference **bukit-cli-reference** for command execution
