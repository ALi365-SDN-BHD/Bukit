# Bukit Core 八项新问题最终 Aggregate 关闭审计与正式台账

> 审计日期：2026-07-19
> 修复前基线：`9ff5d452`
> 八项 residual 关闭点：`b87a332c`
> 最终验证点：`main@5808d9a6`，与 `origin/main` 一致
> 技术状态：**F-01～F-08 全部关闭（8/8）**；历史执行与证据留存偏差见 3.1 节
> 文档覆盖：见[八项修复文档覆盖矩阵](bukit-core-eight-findings-documentation-coverage-2026-07-19.zh-CN.md)

## 1. 执行结论

本轮按《Bukit Core 8 个新问题专项复核、根因分析与受控修复方案》规定的技术边界和关闭标准，对八项修复进行了最终 aggregate 审计。结论如下：

- F-01、F-02、F-03 为 P1，F-04～F-08 为 P2；八项原始根因均已关闭。
- 各项主修复、后续 residual 修复和当前源码行为一致，没有发现仍可重现原问题的入口。
- 八项精确定向复核共通过 193 个测试；F-03 与 F-07 的关键并发/重复执行用例另各连续执行 10 轮，共 100/100 通过。
- 安全回归通过 295/295；架构契约通过 77/77；最终 aggregate targeted gate 通过，覆盖 3,674 个测试，均为 0 failed、0 skipped。
- `9ff5d452..b87a332c` aggregate diff 为 64 个文件、4,764 行新增、447 行删除；`git diff --check` 通过。
- 未修改配置 schema、插件协议、持久化格式或备份目录；在当前实现中没有发现错误的运行时顺序依赖、变更范围内的重复安全机制或超出批准边界的功能扩张。
- 当前仍有少量增强型测试矩阵和长期维护债务，均不构成原 finding 的残余实现缺陷，已在第 9 节单独登记，禁止用“全部关闭”掩盖。

因此，自本报告起，原修复方案文档仍作为修复前设计与验收基线保留，但其“未关闭”状态说明由本正式台账取代。

## 2. 审计范围与裁决规则

### 2.1 审计范围

本报告审计以下三个时间点：

1. `9ff5d452`：八项修复开始前的共同基线；
2. `b87a332c`：最后一个 F-03 output identity residual 修复完成后的八项关闭点；
3. `5808d9a6`：最终验证时的当前 `main`。该点比关闭点多一个独立 SEO/TLS 修复，不属于八项修复，但已被 aggregate gate 一并验证。

本轮只新增本报告，不修改 Core 代码、公共 API、配置 schema、插件协议、asset URL 或持久化格式。

### 2.2 “已关闭”的必要条件

每项只有同时满足以下条件才标为已关闭：

- 原始根因和全部已识别 residual 均在当前源码中消失；
- 精确回归测试覆盖触发条件、成功路径、失败路径及关键跨模块影响；
- 相关 targeted gate、安全/并发/缓存/契约专项证据通过；
- 修复提交未引入超限功能、兼容性漂移或新的同等级问题；
- 独立只读源码复审未发现 Critical/Important 未解决项；
- aggregate diff 与当前主线均通过最终门禁。

原审计已在隔离环境确认八项问题；各独立修复任务采用回归测试固定行为。最终 aggregate 审计不在当前主线上重新执行会删除仓库、产生 XSS 或制造不安全输出的旧实现，而是复核修复前源码、提交历史、当前回归测试和通过结果。

## 3. 修复提交台账

| 提交 | 归属 | 作用 | Aggregate 判定 |
|---|---|---|---|
| `9b407d49` | F-01/F-02/F-03/F-04 | 统一安全清理、移除 search HTML sink、建立 asset output plan、统一安全枚举 | 主根因修复 |
| `49e9e619` | F-07 | 将并发上限落实到真实媒体下载 | 主根因修复 |
| `5806a934` | F-05 | 以内容快照失效模板能力缓存并移除跨调用分析缓存 | 主根因修复 |
| `c8b5c4c3` | F-06 | 传播 search content cap 到所有 representation | 主根因修复 |
| `11ffd721` | F-08 | 采集真实 build diagnostics 和 public output inventory | 主根因修复 |
| `ac6cb310` | F-01 residual | 补齐 root 外路径、`.git` descendant、symlink/reparse 防护 | residual 关闭 |
| `06d7af03` | 门禁依赖 | AngleSharp 1.2.0 升至 1.5.2，解除既有 NU1902 | 非 finding；受控门禁依赖修复 |
| `8ce0938a` | 架构门禁 | 将 `Shared -> Content` IVT 改为精确 source-target 许可 | 非 finding；架构边界守卫适配 |
| `699799e5` | F-03 residual | 将 render/static-template 纳入写入前 ownership preflight | residual 关闭 |
| `8d8eb4d9` | F-07 residual | 让并发 body-store 调用共享下载 gate | residual 关闭 |
| `beca8800` | F-05 residual | 隔离缓存返回对象及 `Fields` 集合 | residual 关闭 |
| `33824e27` | F-06 residual | runtime 拒绝非正数 cap | residual 关闭 |
| `ffda8ed9` | F-08 residual | 修正 observability 文档的嵌套字段表达 | 契约说明关闭 |
| `b87a332c` | F-03 residual | 统一 output destination identity comparer，并供 plan/manifest 共用 | residual 关闭 |

### 3.1 历史执行与证据留存偏差

最终审计必须明确记录两项过程事实，不能用当前 green state 反向美化历史：

1. 首个修复提交 `9b407d49` 同时包含 F-01、F-02、F-03、F-04，未遵守原方案后来规定的“一项一 diff、逐项完成后再进入下一项”。因此 Git 历史不能证明这四项按推荐顺序分别通过 gate 后才进入下一项。
2. 各独立任务曾执行 targeted/post-change 验证，但仓库没有保存八份逐项命令输出或机器可读日志。本轮 193 个测试是当前主线的精选根因复核，不能等同或冒充八次历史 repository gate；原始 RED 日志同样没有作为仓库 artifact 留存。

上述偏差不改变当前源码中八个根因已消失的技术事实，但降低了历史过程的可重放性。本轮采用以下补偿控制：

- 对 F-01～F-08 重新执行精选根因测试；
- 对 F-03/F-07 重新执行 10 轮重复/并发测试；
- 重新执行完整安全回归和架构契约；
- 从共同修复前基线执行一次 aggregate `post-change-targeted.sh`；
- 分两组独立复核八项源码，再对正式台账执行一次独立只读复审。

因此本报告的“8/8 已关闭”是**当前技术状态裁决**，不是“历史过程完全合规”的声明。

## 4. 正式关闭总台账

| Finding | 严重度 | 根因关闭 | Residual 关闭 | 精确复核 | 跨模块审计 | 最终状态 |
|---|---:|---|---|---:|---|---|
| F-01 `clean --dir` 可删除 `.git` | P1 | 是 | 是 | 16/16 | CLI、build/recovery 共用安全 cleaner | **已关闭** |
| F-02 默认 search UI DOM XSS | P1 | 是 | 无未关闭 residual | 4/4 | search UX 保留，不再解释 title/snippet HTML | **已关闭** |
| F-03 AssetPipeline 输出所有权竞争 | P1 | 是 | 是 | 40/40；重复 50/50 | asset/render/manifest 使用同一目标 identity | **已关闭** |
| F-04 递归枚举穿透目录 symlink | P2 | 是 | 无未关闭 residual | 26/26 | content/static/media/report 四条链统一 | **已关闭** |
| F-05 模板能力与静态分析缓存不失效 | P2 | 是 | 是 | 28/28 | manifest/root/include/layout 修改可见 | **已关闭** |
| F-06 search cap 被接受但未消费 | P2 | 是 | 是 | 10/10 | single/list/plugin/publish/i18n merged 一致 | **已关闭** |
| F-07 媒体并发上限未限制真实下载 | P2 | 是 | 是 | 39/39；重复 50/50 | HTML、fields、documents、body store 受限 | **已关闭** |
| F-08 build report 健康与清单失真 | P2 | 是 | 是 | 30/30 | diagnostics、inventory、schema、hash 一致 | **已关闭** |

## 5. 逐项关闭证据

### 5.1 F-01：`clean --dir` 可删除 `.git`

**根因。** 配置式 clean 和无配置 `--dir` 使用不同删除策略；后者可直接递归删除任意解析后的目录，绕过 marker 和危险路径检查。

**关闭实现。** `CleanCommand` 的两种入口现在都进入 `OutputDirectoryCleaner.CleanIfExists`。cleaner 拒绝项目根、root 外路径、home、文件系统根、任意 `.git` path segment 和 root 下 reparse-point 路径；非空目录必须具有 output marker。拒绝后返回码为 2，且不会继续清理状态目录或打印成功信息。

**复核证据。** 16/16 `CleanCommandTests` 通过，覆盖 `.git`、`.git` descendant、项目根、未标记非空目录、标记目录、空目录、逃逸 symlink ancestor/target 及固定缓存 symlink。

**影响审计。** CLI 与 build/recovery 现在共享相同策略；没有增加 CLI 选项或扩大允许删除范围。检查与删除之间仍不是基于目录句柄的原子操作，恶意本地 symlink-swap/TOCTOU 属于更强威胁模型，不是本 finding 的普通误删 residual。

### 5.2 F-02：默认 search UI 内容驱动 DOM XSS

**根因。** 不可信 title/snippet 进入 `innerHTML`，高亮逻辑把内容数据重新解释为 HTML。

**关闭实现。** 动态结果只使用 `createTextNode`、`textContent`、`createElement` 和 `replaceChildren`；生成脚本不再包含 `.innerHTML`、`insertAdjacentHTML`、`outerHTML` 或 `document.write` 动态 sink。placeholder 继续使用 HTML encoder。

**复核证据。** 四个精确测试 4/4 通过，分别验证动态结果无 `innerHTML`、title/snippet/mark 使用文本节点、恶意 placeholder 被编码、恶意 title/snippet 只能作为数据存在。

**影响审计。** anchor、键盘导航、结果层级与 `<mark>` 高亮 UX 保留。`href` 消费的是已验证路由图，不属于本 finding 的内容解释 sink。当前证明以生成代码和 sink 断言为主，尚无真实浏览器 DOM 测试，但实现中未发现残余解释型 sink。

### 5.3 F-03：AssetPipeline 重叠目标并行写入

**根因。** static、assets、media、generated tokens 及后续 render 没有统一目标所有权；并行任务可能写入同一路径，增量 manifest 还可能形成双 owner 或误删当前输出。

**关闭实现。** `AssetOutputPlan` 在业务输出写入前收集、规范化并验证 claim；跨类别相同目标和 file/descendant 结构冲突稳定报告 `BuildAssetOutputCollision`，类别内 parent/site 覆盖保持原语义。render/static-template 同样纳入 aggregate preflight。`OutputDestinationIdentityComparer` 在真实 output filesystem 上探测大小写语义，并由 `AssetOutputPlan` 与 `BuildManifestTracker` 共用，避免以操作系统名称猜测 volume 行为。

**复核证据。** 40/40 定向测试通过，覆盖跨类别冲突、结构冲突、继承覆盖、dotfile/symlink ghost claim、render/asset collision、stale owner 迁移、case-variant collision 和无冲突重复构建。五个关键用例连续执行 10 轮，共 50/50 通过。

**影响审计。** 冲突发生在页面 render 和 asset copy 前；identity 探测会短暂创建并删除随机隐藏 probe，但不会写入业务目标或 manifest owner。after-build 第三方插件输出尚未纳入统一 ownership，这属于计划明确排除的全局插件输出所有权扩展，不影响本 finding 的 AssetPipeline/render 范围关闭。

### 5.4 F-04：默认递归枚举穿透目录 symlink

**根因。** content、static、media、report 等发布链使用多套递归 walker，部分没有跳过 reparse point，默认 `followSymlinks=false` 不能形成统一安全边界。

**关闭实现。** `SafeFileEnumerator` 统一采用递归枚举并跳过 `FileAttributes.ReparsePoint`；content、static、media、build manifest、output inventory 及相关 hash/lint/image tooling 已复用该 helper。显式 `build.followSymlinks=true` 仍只保留在原有受控 copy path，没有借本修复扩大能力。

**复核证据。** Engine 25/25、Content 1/1，共 26/26 通过，验证 external directory symlink 不进入 content、static、media、manifest、report、hash、image tooling 或 template lint 结果。

**影响审计。** public output privacy/deploy walker 虽未调用 helper，但已显式跳过 reparse point。Doctor、Lint、DocsCheck 等不在发布闭环内的 CLI 辅助 walker 不属于 F-04 既定范围；不得把本结论表述成“仓库所有递归枚举已统一”。

### 5.5 F-05：模板能力与静态分析缓存不失效

**根因。** process-global 缓存生命周期长于 manifest/template 文件内容生命周期；同进程 rebuild 可能继续使用旧 capability、root、include 或 layout 决策，返回的可变 `Fields` 还可能污染缓存。

**关闭实现。** manifest 每次读取内容快照并计算 SHA-256，只有 fingerprint 相同才复用；invalid 结果不缓存，appearance/deletion/correction 均可恢复。静态分析每次创建新 analyzer，只在单次依赖图内缓存。resolver 返回新的 flags 和 `Fields` list，调用方不能反向修改缓存。

**复核证据。** 28/28 通过，覆盖同长度且时间戳不变的内容变化、invalid 后修正、appearance/deletion、并发读取、返回集合隔离，以及 root/include/layout directive target 变化和同一 `SiteEngine` rebuild。

**影响审计。** list、pagination、taxonomy 和 search snippet 消费同一 resolver，现均取得当前决策。按 layouts path 保存的 cache/lock dictionary 尚无 eviction，属于长进程内存治理债务，不再造成陈旧行为。

### 5.6 F-06：`site.search.maxContentLength` 被接受但未消费

**根因。** 配置值在调用链中丢失，部分 writer 继续使用固定默认值；single、list、plugin、publish projection 和 i18n merged 输出不一致。

**关闭实现。** 所有 search representation 显式传播 cap；只截断 `content`，不截断 title、summary 或 generated snippet；UTF-16 截断避免切开有效 surrogate pair。默认值仍为 8000，runtime 与 schema 均拒绝非正数。

**复核证据。** 10/10 通过，覆盖 document、list、plugin、merged root、split/index i18n、默认值、非默认值、snippet、surrogate pair 和 `<=0` 配置失败。

**影响审计。** 非默认小 cap 会按用户配置缩小所有 search content，这是预期契约兑现。契约按 UTF-16 code unit 计数，不承诺 grapheme cluster 完整；`I18nOutputMerger` 未使用的 builder 参数为原计划排除的既有债务。

### 5.7 F-07：`content.media.maxConcurrency` 未限制真实下载并发

**根因。** 旧 gate 限制的是 document transform 数量，不是 `_localizer.LocalizeAsync` 的真实网络调用；同一文档多个 URL 或并发 body-store 调用仍可突破上限。

**关闭实现。** 每次 public rewrite operation 创建共享 download-level gate，所有 HTML、fields、documents 的 localizer 调用都收敛到同一 helper。`LocalizedContentBodyStore` 使用 store-level lazy gate，使并发 `GetAsync` 共享配置上限；成功获得 permit 后由 `finally` 恰好释放一次。

**复核证据。** 39/39 通过，覆盖单文档、多文档、HTML/fields、standalone body、body store、异常释放、取消、顺序与 mapping。五个关键并发用例连续执行 10 轮，共 50/50 通过。

**影响审计。** memo 仍按原有 document/public operation 生命周期，不改变 URL 去重语义。没有专门证明同一 pipeline 同时运行两个不同 public operations 时的总和峰值，但正常 build 链没有并行混用证据；该扩展场景不构成原并发契约 residual。

### 5.8 F-08：build report 健康字段与 `generatedFiles` 失真

**根因。** build report 没有本次 build 的诊断收集器，warning/error 固定为零；public output inventory 未从最终磁盘状态生成，`generatedFiles` 不可信。

**关闭实现。** 每次 `BuildCoreAsync` 创建独立 `BuildDiagnosticLogger`，variant forwarder 只共享该次 build 的原子计数器。single/multi-language 在报告前快照真实计数。`BuildOutputInventory` 安全枚举、稳定排序和去重，并排除 `.bukit`、state、marker；artifact manifest 在 build report 完成后读取最终文件并计算 hash。

**复核证据。** 30/30 通过，覆盖所有日志级别和并发计数、连续 build 重置、无诊断零值、并发语言 variant、public inventory、symlink、取消、build-report 字段和 artifact hash。

**影响审计。** `BuildResult`/`BuildSummary` 的字段集合、`build-report.v1` schema 和持久化格式未改变，只修正值来源。当前 exact-property 测试与冻结 schema 一致，但尚未在该测试中调用独立 JSON Schema validator；这是增强型验证缺口，不是已发现的 writer/schema 偏移。

## 6. 原方案整体完成条件复核

| 原完成条件 | 本轮证据 | 结果 |
|---|---|---|
| 每项有失败前/通过后证据 | 原审计隔离复现、修复前源码、regression tests 与修复提交形成审计链；当前重新验证 green state；原始 RED 日志未入库 | 技术证据通过；留存偏差已登记 |
| 每项 targeted gate | 历史个项任务有执行记录但仓库未保存八份可重放输出；本轮精选复核 193/193，并以 aggregate gate 补偿 | 当前状态通过；历史留存偏差已登记 |
| F-01/F-02/F-04 安全回归 | `security-regression.sh Release` 295/295 | 通过 |
| F-03/F-07 并发与重复执行 | 各 5 个关键测试 × 10 轮，合计 100/100 | 通过 |
| F-05 同进程 mutate-and-rebuild | manifest/root/include/layout 与 same-engine rebuild 28/28 | 通过 |
| F-06 single/list/i18n merged 非默认 cap | 10/10 | 通过 |
| F-08 schema identity/exact shape、inventory、count、hash | 30/30，冻结 schema 文件无 diff | 通过 |
| 每项独立只读审计 | F-01～F-04、F-05～F-08 两组独立只读源码复核均无 Critical/Important | 通过 |
| 最终 aggregate diff 审计 | 64 文件；4,764+/447-；diff check、边界、依赖与公共面复核完成 | 通过 |
| 路径/代码/文档一致性 | aggregate targeted gate 的 docs、CLI、skills、architecture 和相关项目测试全部通过 | 通过 |

## 7. 最终验证记录

所有测试命令均在 `/Users/ali/mydev/Git/Github/Bukit`、Release 配置执行。NuGet 审计使用命令级 `NuGetAudit=false`，原因是本轮验证边界是代码/契约回归；AngleSharp 已在修复链中从 1.2.0 升至 1.5.2，原 NU1902 依赖版本已替换。涉及 repository script 的命令同时移除 `NOTION_TOKEN`，避免真实外部状态进入测试。

| 验证 | 结果 |
|---|---:|
| 八项精确定向复核 | 193 passed；0 failed；0 skipped |
| F-03 关键用例 10 轮 | 50 passed；0 failed；0 skipped |
| F-07 关键用例 10 轮 | 50 passed；0 failed；0 skipped |
| `bash scripts/security/security-regression.sh Release` | 295 passed；0 failed；0 skipped |
| `Bukit.Architecture.Tests` | 77 passed；0 failed；0 skipped |
| `bash scripts/checks/post-change-targeted.sh --base 9ff5d452` | exit 0；3,674 passed；0 failed；0 skipped |
| `git diff --check 9ff5d452..b87a332c` | 通过 |

aggregate targeted gate 实际选择的测试项目为：

| 项目 | 通过数 |
|---|---:|
| `Bukit.Cli.Tests` | 568 |
| `Bukit.Config.Tests` | 234 |
| `Bukit.Content.Tests` | 713 |
| `Bukit.Engine.Tests` | 1,557 |
| `Bukit.Shared.Tests` | 309 |
| `Bukit.Architecture.Tests` | 77 |
| `Bukit.Importing.Tests` | 216 |
| **合计** | **3,674** |

按任务边界未运行 full、release、`test-all`、`smoke-all` 或整仓库解决方案门禁；这些不是八项最终关闭的必要证据。

本轮可直接重放的主验证命令为：

```bash
env -u NOTION_TOKEN NuGetAudit=false \
  bash scripts/security/security-regression.sh Release

dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  --configuration Release -p:NuGetAudit=false --nologo

env -u NOTION_TOKEN NuGetAudit=false \
  bash scripts/checks/post-change-targeted.sh --base 9ff5d452
```

193 个精选测试的选择面如下。它们用于复核当前根因，不替代 3.1 节所述历史逐项 repository-gate 日志：

| Finding | 当前复核选择面 | 通过数 |
|---|---|---:|
| F-01 | `CleanCommandTests` | 16 |
| F-02 | 四个 `WriteSearchUi`/恶意内容 DOM sink 精确用例 | 4 |
| F-03 | `AssetPipelineTests`、`BuildManifestTests` 与 ownership/collision integration 用例 | 40 |
| F-04 | symlink 相关 asset tooling、directory copy、manifest、report、markdown provider 用例 | 26 |
| F-05 | capability resolver、static analysis 与 same-engine rebuild 用例 | 28 |
| F-06 | `MaxContentLength`、`ConfiguredMaxContentLength`、i18n merged 与非正数配置用例 | 10 |
| F-07 | `ContentImageRewritePipelineTests`、`LocalizedContentBodyStoreTests` | 39 |
| F-08 | `BuildReporterTests` 与 report/inventory integration 用例 | 30 |

## 8. Aggregate diff 严格审计

### 8.1 范围与防漂移

- 变更集中在八项根因对应的 CLI、Config、Content、Engine、Shared、相关 tests 和两处现行 guide 文档。
- `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 无变更。
- `docs/schemas/`、`Bukit.Plugin.Abstractions`、`Bukit.Engine.Abstractions`、`Bukit.PluginHost` 无变更。
- 未新增 CLI 选项、插件 hook、配置键或 schema 版本；未改变 asset URL、插件协议或持久化格式。
- `Directory.Packages.props` 的 AngleSharp 1.5.2 升级是解除真实依赖告警的独立受控修复，不改变八项业务语义。
- aggregate diff 在 `Bukit.Shared` 新增 `Shared -> Content` 与 `Shared -> Engine` 两条 `InternalsVisibleTo`：前者由 architecture test 以精确 source-target pair 约束，后者使用既有 Core global allowlist；两者都不是 public API，也没有形成通配式内部面暴露。

### 8.2 共享机制与顺序依赖

- F-04 先提供唯一的默认安全枚举 helper；F-03 claim 和 F-08 inventory 复用该 helper，没有再复制 raw recursive walker。
- F-03 以 `AssetOutputPlan` 形成唯一的 asset/render ownership preflight；`OutputDestinationIdentityComparer` 同时传给 plan 和 manifest tracker，没有保留两套大小写判断。
- F-07 download gate 与 document transform gate 职责分离；residual 修复没有把并发状态提升为 process-global。
- F-05 的缓存失效和返回对象隔离没有改变 public model 类型或调用方生命周期。
- F-08 复用 F-04 安全枚举，但不把 SEO/publish/security warning 混入 build health，维持 observability 分层。

### 8.3 公共契约与兼容性

- F-01 是有意的安全收紧：以前可删除的危险或无 marker 非空目录现在拒绝，属于 bug fix 而非兼容性回退。
- F-02 保持 search UI 的结构与交互，只改变不可信文本的构造方式。
- F-03 对此前非确定性的重叠输出改为确定性失败；无冲突构建与类别内覆盖保持兼容。
- F-04 只落实默认不跟随 symlink 的既有承诺；没有扩张显式 follow 能力。
- F-05 只让同进程后续调用看到当前文件内容。
- F-06 让已公开配置真正生效；默认 8000 保持兼容。
- F-07 让已公开并发上限真正限制下载，不改变输出 mapping。
- F-08 保持 v1 字段和 schema，只把固定/空值改为真实值。

没有发现需要整体回滚、schema migration 或 major-version 处理的公共契约变化。

## 9. 非阻断 residual 与后续建议

下列事项不重开八项 finding，也不得在本任务中顺带修改：

| 事项 | 分类 | 为何不阻断关闭 | 建议 |
|---|---|---|---|
| F-01 检查与递归删除间的恶意 symlink-swap/TOCTOU | 更强本地威胁模型 | 原问题是普通 CLI 任意危险路径误删；当前所有入口已统一拒绝 | 若威胁模型需要，另立 handle-based deletion 研究任务 |
| F-02 缺少真实浏览器 DOM 执行测试 | 测试深度 | 生成代码已无解释型 sink，四个恶意输入测试通过 | 可在 UI E2E 体系建立后补充 |
| F-03 case-insensitive CI matrix 不固定 | 平台证明矩阵 | 实现按真实 filesystem 探测，当前 volume 分支测试通过 | 在 macOS/Windows 测试矩阵稳定后加入持续验证 |
| F-03 after-build 第三方插件未进入全局 ownership | 架构扩展 | 原 finding 和批准 residual 只覆盖 asset/render/manifest；计划明确禁止扩张全局插件所有权 | 另立插件输出所有权 RFC |
| F-05 cache/lock dictionary 无 eviction | 长进程内存治理 | 不再产生陈旧决策 | 以实际 profiling 决定是否增加回收策略 |
| F-06 不保证 grapheme cluster 完整 | 契约边界 | 现有契约是 UTF-16 code units，已避免拆 surrogate pair | 如需用户可见字符语义，先版本化定义 |
| F-07 取消测试曾出现一次 `CallCount` 瞬时差异，随后隔离 20/20、完整定向与 aggregate 均通过 | 测试可靠性观察 | 未出现 permit 泄漏、超限或 hang；当前所有 gate 通过 | 若再次出现，独立收集调度时序证据，不修改生产 gate 语义 |
| F-08 缺少独立 JSON Schema validator 测试和同 engine 双 `BuildAsync` 特定用例 | 增强型验证缺口 | exact-property/schema identity、连续 build、concurrent variants 已通过，未发现 schema/source 漂移 | 可作为验证基础设施任务补齐 |

## 10. 正式关闭声明

截至 `main@5808d9a6`：

- F-01：已关闭；
- F-02：已关闭；
- F-03：已关闭；
- F-04：已关闭；
- F-05：已关闭；
- F-06：已关闭；
- F-07：已关闭；
- F-08：已关闭。

最终技术裁决为 **8/8 全部关闭**，没有环境阻塞，没有未解决的代码级 Critical/Important 审计项，也没有需要把任何一项降级为“部分关闭”的实现证据。独立台账复审发现的两项 Important 过程/证据表述问题已通过 3.1、6、7 节修正；它们保留为历史合规偏差，不再被误写为完全符合原逐项执行要求。

本结论严格限定于各 finding 及其已批准 residual 的范围，不表示 Bukit Core 不再存在其他缺陷，也不把第 9 节的架构扩展和验证增强误写成已实现能力。后续任何新问题都必须建立独立任务，重新定义范围、失败证据、修复边界和验收条件。
