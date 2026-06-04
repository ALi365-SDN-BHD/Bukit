# Bukit AI Demo-to-CMS 最终规范包 v1.2

本包是可替代所有历史版本的完整最终版，合并了：

```text
v1.0：完整 Demo-to-CMS 流程规范与多 AI 工具规则
v1.1：详细 site.yaml 与 seed 数据配置合同
v1.2：新增 route/map/template/env 规范与机器可读 JSON Schema
```

## 核心流程

```text
用户需求
→ AI 生成可迁移 HTML Demo
→ 用户确认样式、页面与功能
→ AI / Bukit 转换为主题模板、内容数据、Notion seed 与配置文件
→ Schema 校验
→ Bukit doctor
→ Bukit build
→ Notion CMS
→ 发布
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
│       ├── checklist.md
│       └── config/
│           ├── README.md
│           ├── site-yaml-spec.md
│           ├── site-yaml-profiles.md
│           ├── seed-data-spec.md
│           ├── demo-routes-spec.md
│           ├── notion-database-map-spec.md
│           ├── template-manifest-spec.md
│           └── environment-variables-spec.md
├── schemas/
│   ├── README.md
│   ├── site.schema.json
│   ├── demo-routes.schema.json
│   ├── notion-database-map.schema.json
│   ├── template-manifest.schema.json
│   └── seed/
│       ├── pages.schema.json
│       ├── posts.schema.json
│       ├── companies.schema.json
│       └── services.schema.json
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

## 配置生成核心规则

1. 不得自行发明 `site.yaml` 字段。
2. 生成 `site.yaml` 前必须选择标准 Profile。
3. 必须参考 `site-yaml-spec.md`。
4. 不得同时生成 `content.provider` 和 `content.sources`。
5. `build-source notion` 只能与 `content-source notion` 配合。
6. Notion 多数据库模式必须使用 `content.sources`。
7. 配置生成后必须执行 schema validate、`bukit doctor` 和 `bukit build`。
8. 如果验证失败，必须修复配置，不得忽略错误。

## 必须执行的验证

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

如果支持：

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml --strict
```

## 安装方式

将本目录中的文件复制到 Bukit 仓库根目录，并保留隐藏目录结构。复制前请检查目标仓库中是否已存在同名文件，避免覆盖已有自定义规则。


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