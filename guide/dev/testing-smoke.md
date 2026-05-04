# 验收与冒烟（smoke/acceptance）

本仓库测试策略偏“可运行验收”，覆盖核心链路（build/doctor/i18n/sitemap/rss/taxonomy/multi-site/webhook 等），适合静态站点引擎这类系统型项目。

## 现有入口

- 一键 smoke（本地）：
  - `scripts/smoke.ps1`
  - `scripts/smoke.sh`
- 分项验收入口（操作步骤与期望结果）：`README.zh-CN.md` 中“v2 验收与测试”章节

## 何时新增 smoke / 何时新增 acceptance

建议原则：

- 改动影响“核心端到端链路”：优先补充 smoke（确保最小可跑通）
- 改动新增/增强“对外稳定契约”（新增配置字段、改变 CLI 行为、改变输出结构）：补充 acceptance 文档（明确可验证的步骤与断言）
- 改动是内部重构但对外无感：只要 smoke 不回归即可

## 新增验收用例的最小结构（建议）

每个用例建议包含：

1. 前置条件：
   - 需要的环境变量（尤其是 NOTION_TOKEN）
   - 需要的示例站点配置
2. 操作步骤：
   - build/doctor/preview 命令（带必要参数）
3. 断言：
   - 输出目录结构
   - 关键文件存在（sitemap/rss/search 等）
   - 关键页面路由可访问（可用 preview + 浏览器打开）
4. 回滚/清理：
   - clean 与 cache 处理
