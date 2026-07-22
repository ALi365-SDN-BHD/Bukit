# Bukit Core G-04B3 认证 GitHub 搜索刷新审计

日期：2026-07-22

源码基线：`main@4d9f92205c5f98ccd11e4357e84a6a01f1af4a47`

状态：`authenticated-search-refresh-complete / eligibility-review-required`

## 1. 执行结论

G-04B3 要求的 136 项稳定发布后认证 GitHub Code Search 已完成。认证资料读取
成功，但本报告不记录账号、邮箱、token、cookie 或其他认证材料。全部查询均为
公开代码的只读搜索，没有创建或修改 Issue、评论、PR、分支、标签、Release 或
远端仓库文件。

- 136 个候选均完成完整 CLR 名称和简单名称查询，共 272 个主查询；
- 完整名称返回 0 个文件，简单名称返回 1871 个文件；
- 87 个简单名称查询达到 `topn = 20`，全部执行一个成功且未截断的 Bukit
  namespace/assembly 上下文补充查询；补充查询共返回 2 个文件；
- 272 个主查询和 87 个候选补充查询均无连接器错误，`search-incomplete = 0`；
- 两个补充命中经固定 commit 文件内容复核后均为误报；
- 最终 136 项均为 `no-public-match-found`，没有确认的外部 CLR 消费者、待复核
  外部候选命中或 fork/mirror 命中；
- 该状态只表示本次认证公开搜索未发现经复核的直接候选消费。私有、未索引、
  通过反射/序列化/AOT 间接依赖或未自愿披露的消费者仍不可见。

现行 [136 候选 manifest](../governance/bukit-core-2.0-public-surface-candidates.v1.json)
只刷新了每项 `externalEvidence`。候选 identity、owner、compatibility、声明状态、
私有消费者状态、proposed action、窗口字段及 `eligibleAfterRelease = null` 均未
改变。本任务没有关闭声明窗口、设置 eligibility 或授权 G-04C。

## 2. 查询方法与证据形状

本次沿用 G-04B1 的固定方法。每个候选执行：

1. `fullName` 精确查询，`topn = 20`；
2. `simpleName` 查询，`topn = 20`；
3. 若简单名称返回 20 项，则执行现有 manifest 所记录的 Bukit 上下文窄查询；
4. 保存查询文本、返回数量、截断状态、仓库、路径、固定 commit URL 和 UTC 时间；
5. 结合完整名称、上下文补查、仓库身份、路径和依赖语境处置误报。

搜索时间范围为 `2026-07-22T05:16:15.308Z` 至
`2026-07-22T05:24:16.126Z`。manifest 顶层 `preparedAtUtc` 继续表示最初治理
清单的准备时间，没有借本次搜索刷新改写；每项最新时间以
`externalEvidence.searchedAtUtc` 为准。

| 证据项 | 结果 |
|---|---:|
| 候选数 | 136 |
| 完整名称主查询 | 136 |
| 简单名称主查询 | 136 |
| 主查询合计 | 272 |
| 完整名称文件命中 | 0 |
| 简单名称文件命中 | 1871 |
| 截断主查询 | 87 |
| 未截断补充查询 | 87 |
| 补充查询文件命中 | 2 |
| 连接器错误 / `search-incomplete` | 0 / 0 |
| 复核并排除的候选—仓库组合 | 1640 |

## 3. 索引漂移与简单名称误报

与 2026-07-20 认证快照按候选、仓库和路径对账后，本次 top-20 结果包含 269 个
current-only 路径，同时有 263 个旧路径退出当前结果。简单名称总返回量从 1870
变为 1871；路径集合变化不能解释为新增或消失的 Bukit 消费者。

current-only 中有 31 个 `.cs` 路径。它们可能声明相同的普通类型名，但没有完整
`Bukit.*` 名称命中，也没有在未截断的 Bukit 上下文补查或仓库级依赖查询中出现
对应 package/project/dependency 证据。因此按固定 bounded 方法记录为 lexical
false positive，而不是仅因文件扩展名推断为 CLR 消费者。

这项处置的边界是公开索引证据，不是源码穷尽证明。若后续自愿声明给出程序集、
具体类型、反射字符串、序列化模型、派生类、公共签名传播、source generator、
trimming 或 Native AOT 依赖，必须重新打开对应候选的兼容性处置。

## 4. 两个补充命中

| 候选 | 补充命中 | 复核结论 |
|---|---|---|
| `Bukit.Theme.SchemaValidationError` | `ALi365-SDN-BHD/BukitJalil` 的 `docs/prompts-and-schemas.md` | 产品设计文档中独立建议的 schema validation model 名称；没有引用 `Bukit.Theme.SchemaValidationError`、Bukit 程序集或候选 CLR contract。排除为同组织产品文档的模型名碰撞。 |
| `Bukit.Theme.ThemeDoctorCommand+DoctorResult` | `wenzhan99/SmileBrightbase` 的牙科预约 PHP 文件 | `doctor`/result 牙科业务词义碰撞；没有 Bukit namespace、类型、程序集或依赖语境。排除为 lexical false positive。 |

两项完整名称查询均为 0，补充文件也不是候选 CLR 使用。故没有保留
`external-match-needs-review`。

## 5. 仓库级依赖信号

另执行 5 个认证主查询：

- `ALi365-SDN-BHD/Bukit`
- `github.com/ALi365-SDN-BHD/Bukit`
- `Bukit.Engine`
- `Bukit.Content`
- `Bukit.PluginHost`

前两个 URL/仓库词查询各返回 20 并截断；三个程序集查询返回 0。随后执行 7 个
未截断的 `PackageReference`、`.gitmodules`、`uses` 和程序集组合语境补查：

- package、project、submodule 和程序集组合查询均返回 0；
- 两个 `uses` 查询各返回 4，涉及 BukitJalil README 或 ali365 生成 HTML 中的
  产品链接/文字，而不是 GitHub Actions `uses:`、源码程序集引用或依赖 manifest。

因此，本次仓库级证据只支持“这些认证公开查询下未观察到明确的
repository/package/submodule/actions CLR 依赖”。它不支持“全网不存在 Bukit
消费者”或“私有仓库没有消费者”。

## 6. 治理不变量

刷新前后，删除所有候选 `externalEvidence` 后的 manifest canonical SHA-256 均为
`78c073aefa1ae13f88396ac537f13ee298e8b41b8e302ccc38dcd883be5d326e`；
`windowPolicy` canonical SHA-256 均为
`2b969d384ab5dd58732a5071b6d8bb00e68762cef18a29358c9ce77e94a61e58`。
这证明本次机械替换没有改变候选治理字段或窗口字段。

刷新后的完整 manifest SHA-256 为
`7ee649be39ab37c17202ae38e1aa2c7df0a3c171df7d660848fb1968de3ce014`。

以下值保持不变：

- `candidateCount = 136`；
- `declarationState = open`；
- 全部候选仍为 `consumer-declaration-pending`；
- 全部私有消费者状态仍为 `unknown-until-voluntary-declaration`；
- 全部 proposed action 仍为 `review-only`；
- `eligibleAfterRelease = null`；
- 1.x CLR 访问级别、schema、插件协议和持久化格式均未修改。

## 7. 独立只读证据复审

独立 reviewer 对临时证据、候选 identity、查询形状、时间、固定 commit URL、
索引漂移、31 个 current-only C# 路径、两个补充命中和仓库级查询边界进行了复核。

- Verdict：`Approved`；
- Critical / Important / Minor：`0 / 0 / 0`；
- Ready：`Yes`；
- 需要重新检索的候选：0。

reviewer 允许只刷新 136 项 `externalEvidence`，明确禁止改变候选身份、声明状态、
私有消费者状态、`eligibleAfterRelease` 或窗口字段。本次变更遵守该边界。

## 8. 验证与已知基线失败

- `post-change-targeted.sh` 在最终只读复审前对本任务 aggregate diff 执行完成，退出码
  为 0；复审后仅补充本节验证披露，并由文档 owner checks 覆盖；
- 单独执行 `Bukit.Architecture.Tests` 得到 87 项中 86 项通过、1 项失败；
- 唯一失败为
  `CoverageGateTests.CoverageDocs_SeparateCurrentMatrixContractFromHistoricalPlans`，
  原因是 `guide/dev/testing.md` 缺少既有 `coverage-plan` 文档断言；
- 在未修改的 `main@4d9f92205c5f98ccd11e4357e84a6a01f1af4a47` 上以相同测试命令
  可以原样复现该失败，因此它是既有基线失败，不归因于本次两份文档差异。

本报告不宣称 Architecture suite 已通过，也不在 G-04B3 认证搜索刷新任务中越界
修复该既有覆盖率文档契约。

## 9. 下一门槛

认证搜索刷新已经完成，但 **G-04C 仍未获授权**。下一步应建立独立的 G-04B3
eligibility/窗口关闭审计，重新核对：

1. Issue #60 在关闭审计时点的全部反馈；
2. `v1.0.10` 稳定发布条件；
3. 三个已声明 CLI/process 消费者的候选级限制；
4. 本次 136 项认证搜索证据及公开/私有可见性限制；
5. manifest drift、文档门禁和最终 aggregate diff。

只有该独立审计通过并获得明确批准后，才能讨论设置 eligibility 或关闭声明窗口。
即使 eligibility 获批，G-04C 的弃用、迁移或访问级别变更仍必须另立任务、逐类型
决策并再次获得明确授权。
