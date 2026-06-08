# 公开测试范围

本文档明确定义 Bukit 哪些能力已可公开测试，哪些仍处于实验阶段。

## 推荐公开测试

| 能力 | 说明 |
|---|---|
| Markdown 静态站点 | 从本地 Markdown 文件构建和部署站点 |
| Notion 内容站点 | 通过 `NOTION_TOKEN` 使用 Notion 数据库作为 CMS |
| GitHub Pages 部署 | 通过 Actions 或 CLI 部署到 GitHub Pages |
| 主题开发 | 使用 Scriban 模板创建和自定义主题 |
| SEO/GEO 校验 | 内置 SEO 输出 + `bukit geo audit` + llms.txt |
| AI 辅助配置 | `intent.yaml` 工作流（validate/apply/doctor/build 闭环） |
| 多语言站点 | 通过 `site.languages` 实现 i18n、合并 sitemap、hreflang |
| Modules（`mode=data`） | 企业官网结构化数据（banner、导航、FAQ） |
| 外部插件（AOT 安全） | 内置风格扩展的插件协议 |
| 增量构建 | `--incremental` 标志 + 基于 manifest 的跳过 |

## 预览/实验

| 能力 | 状态 |
|---|---|
| 主题注册表 | Experimental — 主题发现、搜索和注册表安装不属于 Bukit 1.0 GA 兼容承诺 |
| 网站克隆→主题 | 预览 — 浏览器提取到主题生成 |
| 外部插件生态（非 AOT） | 实验 — 动态插件加载 |
| 高级 AI 自动化 | 实验 — 多步骤 AI 构建流水线 |
| BukitJalil 本地控制面板 | 实验 — 本地 Web UI 站点管理 |

## 不包含（不在路线图中）

| 能力 |
|---|
| SaaS 托管平台 |
| 可视化拖拽编辑器 |
| 内置 CMS 后台（Notion 集成之外） |
| 服务端运行时渲染 |
| 实时预览服务器 |
