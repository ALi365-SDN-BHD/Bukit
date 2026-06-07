# Checklist: Bukit 1.0 T0-T7 契约冻结

## T0：产品边界

- [ ] `docs/bukit-1.0-contract-matrix.zh-CN.md` 存在且内容完整
- [ ] `content.provider` vs `content.sources` 决策已写入
- [ ] Notion 等级决策已写入（GA-limited）
- [ ] clone/import 等级决策已写入
- [ ] external process plugin 等级决策已写入（GA-limited）
- [ ] theme registry/search/install 等级决策已写入（Experimental）
- [ ] contract matrix 与 trust-plan 中 support tiers 表一致
- [ ] BukitJalil 标记为 Out of scope

## T1：配置契约

- [ ] 旧字段 warning-only 路径已升级为 rejected-with-message 或移除
- [ ] 关键拒绝路径有 BKT-000x 诊断码
- [ ] config check / doctor / build-time validation 对同一错误给出一致输出
- [ ] `build.report.enabled` 默认策略已定义
- [ ] `dotnet test tests/Bukit.Config.Tests -c Release --no-restore` 通过
- [ ] `dotnet test tests/Bukit.Cli.Tests -c Release --no-restore` 通过

## T2：内容模型

- [ ] type/collection 唯一 1.0 写法已决定并更新 starter
- [ ] starter schema 字段（seo_title/cover/cover_alt/tableOfContents）决策已写入
- [ ] starter 默认 smoke 不产生误导性 warning
- [ ] `bash scripts/smoke.sh Release` 通过
- [ ] `dotnet test tests/Bukit.Content.Tests -c Release --no-restore` 通过
- [ ] `dotnet test tests/Bukit.Engine.Tests -c Release --no-restore` 通过

## T3：路由契约

- [ ] nested route.outputPath 语义已冻结（契约或拒绝）
- [ ] 顶层 outputPath 拒绝行为有 BKT-02xx 诊断码
- [ ] collection/type 匹配规则已冻结
- [ ] 派生路由默认策略已冻结
- [ ] route inventory golden tests 存在且通过
- [ ] routes.json schema 字段语义固定
- [ ] route conflict / unsafe path 有 BKT-02xx 诊断码

## T4：主题接口

- [ ] theme.yaml 必填字段（version/engine/min_engine_version）已定义
- [ ] 无 theme.yaml 主题被 doctor 拒绝
- [ ] extends/fallbackDir/template capabilities 语义已冻结
- [ ] starter/alt/seo-best-practice 全部 doctor/build/smoke 通过
- [ ] `dotnet test tests/Bukit.Theme.Tests -c Release --no-restore` 通过

## T5：插件接口

- [ ] v1 fallback 已移除或拒绝
- [ ] 缺失 capabilities 的外部插件被拒绝
- [ ] plugin failure 统一到 BKT-07xx
- [ ] ProtocolEchoPlugin 覆盖所有场景（success/bad JSON/empty stdout/timeout/ok=false/capability missing/output traversal/stale cleanup）
- [ ] request/response schema 固化

## T6：可复现构建

- [ ] release artifact bundle 结构已定义
- [ ] normalized artifact compare 已实现
- [ ] artifact inventory 覆盖完整 public output
- [ ] security-report.json 包含真实安全检查结果
- [ ] clean build 两次 normalized manifest 一致
- [ ] clean vs incremental public output inventory 一致
- [ ] .bukit/*.json schema validation 通过

## T7：错误与安全

- [ ] 所有 GA-locked failure path 有稳定 BKT-xxxx 诊断码
- [ ] CLI 人类可读和机器可读错误输出统一
- [ ] route/output/theme/plugin/media/download 安全边界已审查
- [ ] `bash scripts/security-regression.sh Release` 通过
- [ ] security-regression.sh 为 release blocker

## 最终回归

- [ ] `dotnet test bukit.slnx -c Release --no-restore` 0 失败
- [ ] `bash scripts/smoke.sh Release` 退出码 0
- [ ] `bash scripts/smoke-all.sh Release` 全部通过
- [ ] `bash scripts/security-regression.sh Release` 通过
- [ ] `bash scripts/check-doc-asset-consistency.sh` 通过
