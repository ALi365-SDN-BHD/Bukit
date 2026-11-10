# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 渲染器契约

- [x] `NotionBlocksRenderer.ContractVersion`（internal const）。
- [x] `NotionBlockRendererRegistry` 新增内部类型枚举入口。
- [x] 契约语料覆盖注册表全部块类型、列表项分组、富文本注解、未知块回退；契约测试按「版本号 → 哈希」只追加记录，输出变化未递增时失败，缺少块类型时失败。

## 2. 服务器时钟

- [x] 传输层通过 internal 成员或内部重载暴露响应 `Date` 头（`NotionClient.SendAsync` → `NotionContentClient`），不改变公共签名；缺失时回退本机 UTC。API 漂移检查确认基线不变。
- [x] `NotionContentSource` 注入时钟；数据库查询响应的 `Date` 用于落定判断。
- [x] 渲染器记录本页正文所有请求中最早的响应 `Date`，作为 `fetchedAt`。

## 3. 正文缓存格式 2

- [x] 写入 `renderer`、`fetchedAt`。
- [x] readwrite 复用条件：格式 2、契约一致、`lastEditedTime` 非空且相同、链接未过期、已落定（+65s，服务器时钟）。
- [x] readonly：可解析格式 + `lastEditedTime` 相同即复用；契约缺失或不一致的条目计入一条汇总警告。

## 4. 稳定正文指纹

- [x] 指纹改为 `{lastEditedTime}:renderer={ContractVersion}[:unsettled]:content={on|off}`。
- [x] `last_edited_time` 为空时不输出稳定指纹。
- [x] 确认指纹以外的 manifest 结构不变。

## 5. 测试

- [x] 缓存：契约递增重拉、分钟内抓取重拉、落定复用、本机时钟超前、空 `lastEditedTime` 不复用、格式 1 失效、readonly 升级后复用并警告；`fetchedAt` 取最早响应时间。
- [x] 指纹：契约版本进入指纹；落定前后指纹不同；落定后稳定；空 `last_edited_time` 无稳定指纹；现有 `EndsWith(":content=…")` 断言保持通过。
- [ ] 端到端（无媒体本地化，`--no-clean`）：契约递增重渲染；分钟内二次编辑在下一次构建生效；本机时钟超前时同样生效。INC-09 保持通过。

## 6. 文档

- [x] 开发文档：修改 Notion 渲染器时递增契约版本并追加哈希记录的流程。
- [x] 用户指南：升级后首次构建会重拉全部 Notion 正文；从 readonly 切回 readwrite 时需要 `--clean`。
- [x] 中英马 CHANGELOG。

## 7. 验证

- [x] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。
- [x] Notion、Content 专项测试与相关增量测试，格式检查，`openspec validate fix-notion-body-cache-contract --strict`，AOT 二进制编译。

证据（2026-11-10）：Notion 439、Content.Notion 79、Content 666、Engine 3280、端到端 Theory 与 INC-09 3/3；审查 0 Critical / 0 Important（`/tmp/claude/codex-reports/review-s58/report.md`）；AOT `aot-s58`。未勾选项：`ContractVersion` 为 const，无法在端到端测试中递增；契约递增后的重拉由缓存层测试覆盖。
