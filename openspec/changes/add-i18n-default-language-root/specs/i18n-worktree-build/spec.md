## MODIFIED Requirements

### Requirement: Worker 结果必须完整且可验证

每个 Worker SHALL 返回有版本、AOT 可序列化的结果，足以重建现有 `BuildVariantResult`。父进程 MUST 验证 schema、HEAD、配置/内容 hash、语言全集、重复语言、输出路径和完成状态，拒绝任何不一致或越界结果。当 `site.defaultLanguageInSubdir` 为 `false` 时，默认语言 Worker 的输出目录 SHALL 是 staging 根目录，其他 Worker 仍为 `staging/<lang>/`；默认语言 Worker MUST 在写文件前执行保留语言前缀检查，父进程 MUST 拒绝任何引用另一语言目录下路径的默认语言 Worker 结果。

#### Scenario: 结果损坏或错配
- **WHEN** Worker 结果损坏、来自不同 HEAD/hash、遗漏或重复语言，或引用 staging 外路径
- **THEN** 父进程拒绝聚合、取消其余 Worker，并保持正式输出不变

#### Scenario: 结果携带语言诊断
- **WHEN** Worker 完成一个语言变体
- **THEN** 结果包含该语言的审计问题和签名链接检测结果（与该语言报告目录中 `seo-report.json` 的问题重复，问题多时文件会相应变大），由父进程统一输出，每条不同的诊断只输出一次；即使根级输出失败，这些诊断也会在报错前输出；缺少这两个可选字段的旧结果按空列表还原

#### Scenario: 根目录默认语言越界写入
- **WHEN** 默认语言在根目录，且默认语言 Worker 结果引用了 `zh-CN/` 下的路径
- **THEN** 父进程拒绝聚合、取消其余 Worker，并保持正式输出不变

### Requirement: 根级输出必须保持现有语义

父进程 SHALL 从验证后的 Worker 结果重建现有变体模型，并 MUST 复用现有 sitemap、feed、search、robots、agent manifest、llms、根目录跳转页、报告和安全门禁实现。split、index 和 merged 模式的公开输出除非本来包含非确定时间指标，否则 MUST 与同一输入的进程内构建等价。该等价要求同样适用于默认语言在根目录的布局。

#### Scenario: 完整多语言投影
- **WHEN** 所有 Worker 成功且站点启用任意受支持的根级输出模式
- **THEN** 聚合结果与现有进程内构建具有相同公开文件、路由、内容和索引语义

#### Scenario: 根目录默认语言布局等价
- **WHEN** 同一干净提交分别用 `bukit build`（Worktree）和 `bukit deploy` 的进程内构建生成，二者都设置 `defaultLanguageInSubdir: false`
- **THEN** 两份输出包含相同的相对文件路径集合，并具有相同的公开文件、路由、内容和索引语义
