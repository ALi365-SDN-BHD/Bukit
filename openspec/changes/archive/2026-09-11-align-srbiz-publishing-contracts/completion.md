# 归档核对 — 2026-09-11

本变更的已批准实施与必要验收完成。本次仅同步规范并归档，不重新构建、部署或改写历史候选。核对基线：Bukit `141f4af1856ad9ea92e95a8d5ec0fceffe41ced7`；SRBiz `45566fe0ab532a613e0fe71febb9211371ec8fdb`。

## 实施和证据链

- Bukit `28fbe381812b6fbd3547d9dc81a55d9a4ab00ace` 纳入 OpenSpec、架构文档、真实 Task4 fixture 和 SiteEngineSrbizClockTests；`417d0da2d4b02149107c231b5e2f5212384e3f12` 纳入 CNAME 合同修正。两者均为核对 HEAD 的祖先。
- SRBiz `da47633424d216d95dd53339e93f9543a31460de` 完成关系模板、检查/prepare/verify-online、直接测试和 build_year 页脚；`00b1fe1c5b916ddb8e238d06487ca66cbf2cd739` 完成 CNAME 普通 Git blob/域名及有界读取。两者均为站点核对 HEAD 的祖先。
- 原 Task4/Task6、artifact gate、delivery owner、provenance 的证据与后续整合记录已核对；requirement 对应关系仍见站点 `docs/operations/site-delivery.md`，历史批次 traceability 中 task4.2 的 PARTIAL 已由后续固定时钟证据补齐，不能复写早期报告为通过。
- 时钟整合日志记录 Release Engine 2410 PASS。root 与 /docs 各 131 个公开文件，固定 UTC 2040-12-31T16:00:00Z，冷/暖/增量路径与 SHA256 相同；页脚本地年 2041，回拨一小时后为 2040且增量等于同刻干净构建。当前仓库保留真实 fixture 与可重跑测试；原工作区 `8f41` 的 REPORT.md 已不存在，本页记录可核对的后续整合证据，未伪造原工作区或重标历史候选。
- CNAME owner 历史 23 PASS。candidate6 封存摘要 `d1b1145695e4a348743295d0b3611c587783dccb2a5cc0febf2171feb5a4b108`；独立发布 `abec08e5d37352135f1cbac36eccb744c6c5a66c` 的 276 个 HTTP 文件、1 个删除路径和 CNAME 元数据验收已完成。此前 CNAME Important 已关闭。归档只确认该版本证据，不宣称它是目前线上最新版本。
- 两处非空企业列表历史 noindex、同字节媒体改名均按正式合同交付；未进行构建后二次修补。

## 证据定位与指纹

以下均为本次实际读到的既有证据；报告中的 RUN/PASS 是其记录的历史执行，不是本次重新运行。临时路径存在清理风险，当前规范、源码及 fixture 随仓库保留；指纹本身不代替原始日志。

| 证据 | SHA256 | 当前来源 |
|---|---|---|
| 时钟回放与源码整合报告 | `a00b9ce6beeba210b54ce26ff86fd2426f9f56189f8b24c1802be592a4fd969b` | `/tmp/codex-reports/srbiz-contract-closeout-20260910/REPORT.md` |
| root/docs 时钟回放摘要 | `6487c64e6c20cc3f60b6104e88810e52a6c0a9c5b2432733ce9b0c399579a454` | `/tmp/codex-reports/srbiz-contract-closeout-20260910/clock-replay-summary.json` |
| 整合版本完整 Engine 专项日志 | `a32499b0ceff4499fc999e2f3b110a0cc66ec53729958dcac9fabec04357c919` | `/tmp/codex-reports/srbiz-contract-closeout-20260910/engine.log` |
| CNAME 修正及正式交付报告 | `ae0d720497538cfc247db5ea415078196e38aec54da16b0ac6420fdf5eb0f77e` | `/Users/ali/Documents/Codex/Deliveries/SRBiz/2026-09-10-cname-contract-release/report.md` |

## 归档边界

三个 capability 的 SHALL/MUST 和场景保持原批准语义，转入 `openspec/specs/`；历史 proposal/design/tasks 保留在本归档。归档前两变更严格校验通过，归档后的主规范另行严格校验。

旧静态 404 非根链接范围、真实阅读效果、正式凭据替换及后续 GA4 标题竞态均不是本变更尚未完成的实施项。后续有实际需求或新缺陷，应单独立项；不扩大本归档的通过范围。未将旧检查缓存当作当前 HEAD 的新测试通过，没有运行 Core 全套或发布门禁。
