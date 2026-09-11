## 1. 规范和基线
- [x] 1.1 固定两仓 HEAD/dirty/runtime/lock 指纹，保留无关改动，定义写者及共享资源。
- [x] 1.2 创建 spec-driven config、proposal、design 和三项 capability；生成零未映射闭包并分类。
- [x] 1.3 运行 openspec validate align-srbiz-publishing-contracts --strict --no-interactive。

## 2. 关系模板与 fixture（batch1）
- [x] 2.1 同文件解析器按集合/content_model.slug/语言/有效唯一 URL 解析，安全回退与冲突隐藏。
- [x] 2.2 显式优先、反向排序、唯一上限六条、正向不限六条；当前标签与空区块、私密计数。
- [x] 2.3 扩展 Task4 关系、重复/缺失/冲突/跨集合语言、日期与 root/nonroot BaseUrl fixture。
- [x] 2.4 运行 bash scripts/check-wp2-task4-template.sh。
- [x] 2.5 用已锁 Native AOT 在隔离副本串行运行 Task4 runtime 与 Task6 runtime 完整专项，保留旧信任/实体/空列表断言。
- [x] 2.6 校正 Core guide/dev/architecture.md 当前文档；完成本批唯一专项复审及缓存证据。

## 3. 交付验证（batch2）
- [x] 3.1 实现单 Python check/prepare/verify-online 及复用既有 checker 的兼容行为。
- [x] 3.2 验证隔离固定基线、公开/私有分离、字节保真、保留文件和已有 IndexNow 文件来源、篡改失效。
- [x] 3.3 验证有界只读 HTTPS、全部文件哈希、删除404/410及跨主机重定向拒绝。
- [x] 3.4 运行 bash scripts/tests/check-publish-artifact-gate.test.sh。
- [x] 3.5 运行 python3 scripts/tests/verify-site-delivery.test.py。
- [x] 3.6 运行 bash scripts/check-ta01-core-provenance.sh 并完成本批唯一专项复审。

## 4. 最终交付证据
- [x] 4.1 更新站点用户指南和逐条 requirement→代码→证据追踪。
- [x] 4.2 后续独立 Core 验证已完成：网站页脚改用已有 site.build_year；固定 TimeProvider UTC 2040-12-31T16:00:00Z 与原内容日期/mtime，root 和 /docs 各 131 个公开文件冷/暖/增量全部路径及 SHA256 一致，真实页脚为 2041；仅回拨时钟一小时则为 2040，增量与同时间冷构建一致。Engine 完整专项 2410 通过。来源为 candidate4 加明确单行派生，原候选封存不变；源版本证据见 [归档核对中的后续整合证据](completion.md)。锁定 Native AOT CLI 仍无时钟参数，历史 Notion 不要求重建。
- [x] 4.3 只读获取真实 SRBiz（含 Prasarana）数据并在隔离环境封存候选，不写 Notion/不部署。
- [x] 4.4 明确两处非空企业列表 robots 变化及媒体等字节改名报告，不改写 raw build。
- [x] 4.5 最终一次 delta-only 复审，无 open Critical/Important，披露未验证线上边界；不 commit/push/deploy/通知。
- [x] 4.6 完成前再次严格运行 openspec validate align-srbiz-publishing-contracts --strict --no-interactive。

## 5. 已批准CNAME合同修正
- [x] 5.1 更新部署元数据与有界Git验收合同，保留历史候选。
- [x] 5.2 实现schema2显式CNAME分类、发布commit普通blob和HTTPS域名校验。
- [x] 5.3 完整运行交付owner专项与OpenSpec严格校验，验证错误元数据及普通文件仍失败。
- [x] 5.4 完成一次限定复审，再交主控安排源码提交、新候选与部署验收。
