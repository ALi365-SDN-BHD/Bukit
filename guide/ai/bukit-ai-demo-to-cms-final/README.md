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


推荐的实际使用流程
阶段 1：输入用户需求

用户不需要一开始就懂 Bukit，只需要描述网站需求。

例如：

我要做一个名为“丝路商讯”的网站。

网站定位：
连接中国与马来西亚的商务资讯和企业资源。

核心入口：
1. 商务资讯
2. 企业目录

需要页面：
- 首页
- 资讯列表
- 资讯详情
- 企业总列表
- 中国企业列表
- 马来西亚企业列表
- 企业详情
- 关于我们
- 联系我们
- 加入我们

风格：
现代、商务、国际化，使用深蓝和金色。

请先按照 bukit-demo-to-cms 规范生成可预览 HTML Demo，
不要直接生成最终 Bukit 工程。

AI 应先输出：

网站信息架构
页面列表
内容集合
视觉方向
Demo 文件结构

然后生成：

demo/
  index.html
  insights.html
  article-detail.html
  companies.html
  china-companies.html
  malaysia-companies.html
  company-detail.html
  about.html
  contact.html
  join.html
  assets/
    css/style.css
    js/main.js
    images/

demo.routes.yaml
阶段 2：预览 Demo

在 Demo 目录启动一个简单 Web Server：

cd demo
python3 -m http.server 8080

浏览器打开：

http://localhost:8080

这个阶段只检查：

页面好不好看
导航是否合理
首页结构是否正确
企业卡片是否合适
资讯列表是否合适
移动端是否正常
CTA 是否清晰
文案方向是否正确
阶段 3：让用户修改和确认

用户可以继续提出修改：

首页 Hero 太普通，请增加更强的商务感。

企业目录需要突出中国企业和马来西亚企业两个入口。

企业卡片需要显示国家、行业和企业 Logo。

移动端导航需要使用折叠菜单。

AI 修改 Demo 后，再次预览。

直到用户明确确认：

Demo 样式、页面结构、功能和文案方向已经确认，可以转换为 Bukit 工程。

这个确认是流程中的重要门禁。

阶段 4：将最终 Demo 转换为 Bukit 工程

推荐优先使用 Bukit importer，而不是让 AI 手工编写全部模板。

bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source markdown \
  --route-map demo.routes.yaml \
  --strict warn \
  --force \
  --verify

这一步会生成：

themes/silkroadbiz/
sites/silkroadbiz/
sites/silkroadbiz/content/
sites/silkroadbiz/notion-seed/
sites/silkroadbiz/import-report.md

其中：

输出	用途
themes/silkroadbiz/	正式 Bukit 主题
sites/silkroadbiz/content/	本地 Markdown 预览内容
sites/silkroadbiz/notion-seed/	准备推送到 Notion 的数据
site.yaml	Bukit 构建配置
import-report.md	导入审计报告
阶段 5：检查导入结果

重点检查：

sites/silkroadbiz/import-report.md

必须查看这些章节：

Pages
Content Seeds
Seed Push Scope
Build/Data Source Relationship
Hardcoded Content Residue
Diagnostics
Link Validation
Visual Verification
Manual Review Required

如果发现业务文案仍残留在模板中，应让 AI 修改模板或调整 Demo 后重新导入。

阶段 6：本地构建验证
bukit doctor --config sites/silkroadbiz/site.yaml
bukit build --config sites/silkroadbiz/site.yaml

构建成功后，检查：

dist/

并预览生成结果：

bukit serve --config sites/silkroadbiz/site.yaml

这个阶段要确认：

首页可访问
列表页可访问
详情页可访问
图片路径正确
内部链接正确
样式与 Demo 基本一致
移动端正常
阶段 7：推送内容到 Notion

准备好 Notion token：

export NOTION_TOKEN="<your-notion-token>"

执行推送：

bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-map sites/silkroadbiz/notion-seed/notion-database-map.yaml \
  --create-missing-databases \
  --parent-page-id <notion-parent-page-id> \
  --mode upsert \
  --update-content replace

这一步会处理默认 Notion 集合：

pages
posts
companies
services

默认 review-only 集合：

sections
faqs
media
components
阶段 8：切换为 Notion-only 构建

当 Notion 数据库和内容确认无误后，使用 Notion-only 模式重新生成配置：

bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source notion \
  --route-map demo.routes.yaml \
  --force

然后再次验证：

bukit doctor --config sites/silkroadbiz/site.yaml
bukit build --config sites/silkroadbiz/site.yaml

此时构建链路变成：

Notion 多数据库
→ Bukit content.sources
→ Bukit build
→ dist
阶段 9：正式发布前检查

建议使用更严格的导入门禁：

bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source notion \
  --route-map demo.routes.yaml \
  --strict fail \
  --force \
  --verify

然后执行：

dotnet test
bash scripts/test-all.sh
bash scripts/quality-gate.sh
