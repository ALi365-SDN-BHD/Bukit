# 归档核对 — 2026-09-11

已完成本变更批准的根错误页身份和共享 transform 修复；归档不代表 GA4 的所有后续问题均已解决。

- 核对基线和实现提交为 Bukit `141f4af1856ad9ea92e95a8d5ec0fceffe41ced7`。修复同时覆盖 RenderEntry 与 StaticFileService，并包含三个直接测试文件。root 404 保留 `/404.html` 与 `404.html`；嵌套 `docs/404.html` 保留原目录路由。
- SRBiz `3a82dad610f9b442d81584850853a0b60df6c956` 的匹配运行时、来源锁、404 模板及说明已经独立提交，且是站点当前 `45566fe` 的祖先。
- 已核对持久报告 `/Users/ali/Documents/Codex/Deliveries/SRBiz/2026-09-10-ga4-form-release/report.md`：当前 Core 提交当时的完整 Engine 2414 PASS（Debug/net10.0），候选通过，随机不存在路径返回 HTTP404，字节等于候选错误页，GA loader/config 各一次、noindex、无 canonical 和聚合排除。该报告整体 PARTIAL 涉及当时接收端补证，不等同本变更路由修复未完成。
- `/Users/ali/Documents/Codex/Deliveries/SRBiz/2026-09-10-404-reception-1519/report.md` 补齐原发布路径 `__qa_404_537cb54_20260910` 的 GA4 接收：page_view 1、标题正确。另一次后缀 1519 的新测试路径仍未匹配，保留其未完成边界，不能混用路径。
- `/Users/ali/Documents/Codex/Deliveries/Bukit/2026-09-11-ga4-first-title-investigation/report.md` 已复现独立的首次标题竞态：GA config 在 title 解析前执行。此缺陷跨普通页/404，涉及加载时序，不改变本变更根路径和进入 transform 的合同；未获本归档修复，也未声称解决。

本次仅执行 OpenSpec 严格校验和归档文档检查，不复用上述历史测试作本轮运行声明、不执行 Core 全套、不更改运行时、部署、Notion 或 GA。静态渲染规范的 Requirement/Scenario 原义保持。
