# Bukit Core 内置 `type: cloudbase` 内容源：实施任务计划书

> **执行方式：** 任务一个接一个做，每个任务单独验证、单独提交。提交的写法见第 0 节第 4 条。没有授权时，不 push、不开 PR、不发布，也不跑 full/release gate。
>
> **状态：** 草案，未批准。门禁规则：**G0-1～G0-5 没通过，不能开始 T00；G0-6 没通过，不能开始 T06。**
>
> **修订：** v3（2026-11-23）。v2 按第一轮独立审计（Critical 1 / Important 12 / Minor 15）修订，v3 按第二轮严格审计（Critical 0 / Important 11 / Minor 10）修订，处置记录见方案附录 A、B。
>
> **依据：**
> - [实施方案](2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)，下文简称"方案"。D1～D18 指方案第 4 节的决策。
> - [可行性分析](../analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md)，下文简称"分析文档"。
>
> **代码基线：** 提交 `5fdc9dd2` 时的仓库内容。源码路径从 `src/Bukit-Core/` 算起，测试路径从 `tests/` 算起。
>
> **工作量：** 下文的行数和工期都是估算。

---

## 0. 执行规则（来自 `AGENTS.md`，先读）

### 0.1 槽位、队列与回报

1. **四个固定槽位。**
   - 槽 1 是控制者：负责协调和验证，不写仓库。
   - 槽 2 是当前实施者，也是唯一能写仓库的人。
   - 槽 3 做只读检索：调用方检索、测试清单。
   - 槽 4 做只读的文档准备。

   槽 3 和槽 4 调查的内容不能重叠。槽 2 写仓库期间，它们不能动任何提示词、测试、锁文件、清单或实现文件。不设常驻的审计代理；专项审查或最终审查到期时，临时占用槽 3 或槽 4。

2. **队列。** 状态文件是 `/tmp/bukit-cloudbase-queue.json`，T00 第 1 步创建。每个任务的状态只能这样流转：

   ```text
   queue acquire（进入 writing） → testing → review_wait → done
   ```

   其中 `review_wait` 可以退回 `writing`，任何状态都可以转为 `blocked`。命令写法如下：

   ```bash
   Q=/tmp/bukit-cloudbase-queue.json
   python3 scripts/checks/codex-workflow.py queue acquire --state "$Q" --task T02
   python3 scripts/checks/codex-workflow.py queue transition --state "$Q" --task T02 --to testing
   ```

3. **代理报告。** 派发代理之前，控制者先给它分配报告路径，格式是 `/tmp/codex-reports/cloudbase-<task>-<role>.md`，例如 `cloudbase-T04-impl.md`、`cloudbase-T04-review.md`、`cloudbase-T04-consumers.md`。详细证据写进报告，代理最终的回复只包含这六行：

   ```text
   STATUS: DONE | BLOCKED
   COMMIT: <sha or none>
   TESTS: <commands and pass/fail summary>
   FINDINGS: Critical <n> / Important <n> / Minor <n>
   CONCERNS: <at most three items>
   REPORT: <path>
   ```

4. **长时间运行的命令。** 每个工具调用最多 60 秒就要返回一次，并报告进度。代理超过 90 秒没有进展时，控制者要检查它是卡住了、在等输入，还是占着某个资源。同一个错误出现两次后，停止盲目重试，改为定向诊断。

### 0.2 提交（每个任务都这样提交）

`git commit -- <路径>` 只会提交已经被跟踪的文件。新文件不会被提交；如果路径是一个新建的目录，命令会直接失败。所以每次提交都按下面四步做：

```bash
git status --short                                   # 1. 只能出现本任务的文件；有别人的改动，先停下
git add -- <本任务的显式文件列表>                     # 2. 包括新文件
git diff --cached --name-only                        # 3. 核对：暂存区正好是本任务的文件
git commit -m "<message>" -- <同一份文件列表>          # 4. 带 pathspec 提交
git status --short -- <本任务路径>                    #    确认没有残留的 ?? 或 M
```

下文各任务的"提交"一栏只列出 message 和文件列表，执行时一律套用这五条命令。

### 0.3 验证

1. **闭包。** 每个任务动手之前，先对本任务的候选文件运行 closure。`--changed` 每个文件写一次：

   ```bash
   args=(); for f in "${files[@]}"; do args+=(--changed "$f"); done
   python3 scripts/checks/codex-workflow.py closure \
     --policy scripts/checks/codex-workflow-policy.v1.json "${args[@]}"
   ```

   要求 `unmappedFiles` 为空。不要写成 `git diff | while …` 这样的管道：`while` 在子 shell 里执行，循环结束后 `args` 是空的。

2. **分类。** 对 closure 给出的每个路径和每条测试命令运行 `classify`：

   ```bash
   python3 scripts/checks/codex-workflow.py classify \
     --policy scripts/checks/codex-workflow-policy.v1.json --path <p> --test-command "<cmd>"
   ```

   执行方式按分类结果来：`static-parallel` 的互不相干的检查可以并行跑；`dotnet-serial` 的串行跑；`fixture-exclusive` 的单独跑。

   注意：分类只看 policy 的 `resourceRules`，pathRule 里的 `resource` 字段不起作用。T00 第 3 步会把 `dotnet-format.sh`、`code-analysis-ratchet.sh`、`public-api-drift.sh` 这些会构建的命令补进 `resourceRules` 的 `dotnet-serial`；在那之前，凡是会调用 `dotnet` 的命令一律串行跑。

3. **C# 工具门禁。** 改动 C# 的任务（T01～T08），除了 specialtyTests，还要跑下面三项：

   ```bash
   bash scripts/checks/dotnet-format.sh
   bash scripts/checks/code-analysis-ratchet.sh check          # CA1502/CA1506 不能高于基线
   bash scripts/checks/public-api-drift.sh check Debug           # 先构建 bukit-core.slnx，再比对基线
   ```

   需要更新基线时，先 `bash scripts/checks/public-api-drift.sh snapshot <out.json> Debug` 生成快照，再把变化合并进基线，并保留基线里已有的 owner 和分类等元数据。

4. **缓存。** 一个记录文件只存一条记录，后写的会覆盖先写的，所以**每条命令用一个单独的记录文件**，路径是 `/tmp/codex-reports/cache/<task>-<label>.json`。每条测试命令跑之前先 `cache check`：退出码 0 是命中，直接复用；退出码 1 是未命中；退出码 2 表示记录文件不存在，也当作未命中。没命中就跑，GREEN 之后 `cache record`：

   ```bash
   R=/tmp/codex-reports/cache/T02-shared-tests.json
   python3 scripts/checks/codex-workflow.py cache check --record "$R" --base "$(git rev-parse HEAD)" \
     --command "<cmd>" --sdk-version "$(dotnet --version)" --path <闭包文件，可重复>
   python3 scripts/checks/codex-workflow.py cache record --record "$R" --base "$(git rev-parse HEAD)" \
     --command "<cmd>" --sdk-version "$(dotnet --version)" --path <同上> \
     --result passed --exit-code 0 --duration-ms <n>
   ```

   `--env` 只填变量名，不能填值。缓存指纹包含 HEAD，任务一提交 HEAD 就变了，所以证据只在同一个 HEAD 上可以复用，不能跨任务复用。

5. **指标。** 指标写进单独的文件 `/tmp/bukit-cloudbase-metrics.json`，不要写进队列的状态文件。这个文件第一次使用前要初始化成 `{"events":[],"schemaVersion":1}`，由 T00 第 1 步完成。每个阶段（implementation、test、review）结束时记一条：

   ```bash
   M=/tmp/bukit-cloudbase-metrics.json
   python3 scripts/checks/codex-workflow.py metrics add --state "$M" --task T02 \
     --phase test --duration-ms <n> --cache-status <hit|miss|none> \
     --command-label shared-tests [--rerun] [--conflict] --status <completed|blocked>
   ```

   `--command-label` 只写一个简短的标签，不能写原始命令、环境变量值或密钥。

6. **边界。** 只跑 closure 给出的 specialtyTests 和第 3 条的工具门禁。不跑 full/release gate、`scripts/test-all.sh`、`scripts/smoke-all.sh`，也不跑整个解决方案的测试。

7. **没有真实网络。** 自动化测试一律用伪造的 HTTP 处理器。真实环境的冒烟测试只在 T10 获得授权之后做。

### 0.4 审查

1. 每个实现任务结束后，做一次专项审查。只有 Critical 或 Important 问题才退回实现，修完后只复审改动的部分。Minor 问题记录下来，不阻塞任务。
2. 同一份 diff 不重复派发审查。
3. 全部任务结束后，用 `review-scope` 做一次只看增量的统一审查，见 T10。

## 1. 里程碑与依赖

```text
Phase 0（S1～S4） ──> G0-1～G0-5 ──> T00
单写者串行链：T00 → T02 → T01 → T03 → T05 → T04 → T06 → T07 → T08 → T09 → T10
                                               ▲
                                  G0-6（S3）卡住 T06
```

代码依赖关系是这样的：

- T01 和 T03 都依赖 T02：T01 要报 `CloudBaseConfigInvalid`；T03 本身不产生诊断码，但 T04 要把它的错误映射到 T02 定义的码。T01 和 T03 之间没有依赖。
- T05 只给 `BasicMarkdownToHtml` 新增一个默认关闭的选项，不依赖 provider。
- T04 依赖 T01、T02、T03、T05，并在 body store 里打开 T05 加的选项。

因为只能有一个人写仓库，所有任务都**串行**执行，顺序就是上图那条链。只读的准备工作，比如整理测试夹具、检索调用方、起草文档，可以由槽 3 和槽 4 提前做。

| 里程碑 | 任务 | 产出 | 估算 |
|---|---|---|---|
| M0 决策 | Phase 0、G0 | 调研结论、G0 记录 | 2～4 天（取决于消费方配合） |
| M1 基础 | T00、T02、T01、T03 | policy 规则、openspec、诊断码、配置契约、transport、新项目登记 | 4.5～7 天 |
| M2 主体 | T05、T04、T06 | 端到端构建能跑通 | 3.5～5 天 |
| M3 收口 | T07、T08、T09、T10 | 隐私、doctor、文档、最终验证 | 4.5～7 天 |
| 合计 | | | **约 15～23 个工作日**（估算，按一名实施者计；T10 按全部测试重跑计算） |

## 2. Phase 0 与 G0（不改仓库代码）

**负责人：** 方案负责人和消费方。

**产出：** 调研结论，写进 T00 创建的 openspec proposal 的"调研结果"一节。

| 调研 | 内容 | 完成标准 |
|---|---|---|
| S1 | 数据形态；查询路径、分页参数、过滤和排序语法；分页上限；系统字段清单；一份脱敏的响应样本 | 给出结论和样本 JSON。样本作为 T03、T04 的测试夹具 |
| S2 | 每种鉴权方式能否只读访问；用临时工程（不入库）对 `Tencent.CloudBase` 试编译 PublishAot | 给出鉴权矩阵、AOT 和裁剪警告的数量，以及复用 SDK 还是自己写的结论 |
| S3 | 私有文件临时链接的域名和签名参数名；`maxAge` 的单位和上限 | 给出结论和一个脱敏的链接样例 |
| S4 | 正文样本里有没有 HTML、内嵌的 `cloud://`、`#片段`、相对链接、`<scheme:...>` 自动链接；文件字段是不是全是图片 | 给出结论 |

**G0 决策记录**（G0-1～G0-6 的定义见方案第 2 节）：每一项写明"通过"或"不通过"，附上证据和负责人签字。

- G0-1～G0-5 全部通过，才能开始 T00。
- G0-6 通过，才能开始 T06。
- 如果 G0-4 不通过，又没有人书面接受管理员级凭据的风险，本计划终止，改走方案 C。
- G0-2 的记录里要照抄方案里的说明：把 Website/Core 隔离规则用在新增能力上是类推，不是规则原文。

## 3. 任务

### T00：批准、队列、policy 与 openspec

**前置：** G0-1～G0-5 全部通过；用户明确授权这是一个独立的 Core 任务。

**文件：**

- 修改：`scripts/checks/codex-workflow-policy.v1.json`
- 新建：`openspec/changes/add-cloudbase-content-source/` 下的这些文件：
  - `proposal.md`、`design.md`、`tasks.md`
  - `specs/cloudbase-content-source/spec.md`
  - `specs/core-report-contracts/spec.md`
  - `specs/media-localization/spec.md`
  - `specs/miniprogram-data/spec.md`
  - `specs/incremental-invalidation/spec.md`
- 修改：本计划书和方案的"状态"行

**步骤：**

1. **先建队列，再动手写。**

   ```bash
   git status --short
   git diff --cached --quiet || { echo "staged changes exist" >&2; exit 1; }
   mkdir -p /tmp/codex-reports
   python3 scripts/checks/codex-workflow.py queue init --state /tmp/bukit-cloudbase-queue.json
   python3 scripts/checks/codex-workflow.py queue acquire --state /tmp/bukit-cloudbase-queue.json --task T00
   printf '{"events":[],"schemaVersion":1}\n' > /tmp/bukit-cloudbase-metrics.json
   mkdir -p /tmp/codex-reports/cache /tmp/codex-reports/evidence
   printf '{"findings":[],"schemaVersion":1}\n' > /tmp/codex-reports/cloudbase-findings.json
   git rev-parse HEAD > /tmp/bukit-cloudbase-base
   ```

2. **列出全部 P1 候选文件。** 来源是方案第 8 节，外加 openspec 的全部路径、新用户文档 `guide/user/27-cloudbase-content.md`、`guide/user/README.md`、`bukit-test.slnx`，以及两份计划文档。对这份完整清单运行 closure（写法见 0.3 第 1 条），记下 `unmappedFiles`。

3. **修改 policy。**

   **3a. 在 `pathRules` 里新增 4 条规则。** 每条规则都要写全六个字段，glob 一律写完整前缀：匹配用的是 `fnmatch.fnmatchcase`，裸的 `**/*.cs` 会匹配仓库里所有 C# 文件，裸文件名只匹配根目录。下面的 JSON 可以直接复制，只把计划文档的文件名按实际情况核对一遍：

   ```json
   {
     "id": "bukit-cloudbase-source",
     "matches": [
       "src/Bukit-Core/Bukit.CloudBase/*.cs",
       "src/Bukit-Core/Bukit.CloudBase/**/*.cs",
       "src/Bukit-Core/Bukit.CloudBase/Bukit.CloudBase.csproj"
     ],
     "specialtyTests": [
       "dotnet test tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj",
       "bash scripts/checks/dotnet-format.sh",
       "bash scripts/checks/code-analysis-ratchet.sh check"
     ],
     "resource": "dotnet-serial",
     "publicContract": true,
     "contractConsumerGlobs": [
       "tests/Bukit.CloudBase.Tests/*.cs", "tests/Bukit.CloudBase.Tests/**/*.cs",
       "tests/Bukit.Content.Tests/*.cs", "tests/Bukit.Content.Tests/**/*.cs",
       "tests/Bukit.Cli.Tests/*.cs", "tests/Bukit.Cli.Tests/**/*.cs",
       "tests/Bukit.Engine.Tests/*.cs", "tests/Bukit.Engine.Tests/**/*.cs",
       "tests/Bukit.Architecture.Tests/*.cs", "tests/Bukit.Architecture.Tests/**/*.cs"
     ]
   },
   {
     "id": "bukit-cloudbase-tests",
     "matches": ["tests/Bukit.CloudBase.Tests/*", "tests/Bukit.CloudBase.Tests/**"],
     "specialtyTests": ["dotnet test tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj"],
     "resource": "dotnet-serial",
     "publicContract": false,
     "contractConsumerGlobs": []
   },
   {
     "id": "cloudbase-content-source-change",
     "matches": [
       "openspec/changes/add-cloudbase-content-source/*",
       "openspec/changes/add-cloudbase-content-source/**",
       "openspec/changes/archive/*-add-cloudbase-content-source/**",
       "openspec/specs/cloudbase-content-source/spec.md",
       "docs/plans/2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md",
       "docs/plans/2026-11-23-cloudbase-content-source-task-plan.zh-CN.md"
     ],
     "specialtyTests": [
       "openspec validate add-cloudbase-content-source --type change --strict --no-interactive"
     ],
     "resource": "static-parallel",
     "publicContract": false,
     "contractConsumerGlobs": []
   },
   {
     "id": "cloudbase-user-docs",
     "matches": ["guide/user/07-multi-source-data.md", "guide/user/27-cloudbase-content.md"],
     "specialtyTests": [
       "bash scripts/checks/docs/active-links.sh",
       "bash scripts/checks/docs/size-policy.sh",
       "bash scripts/checks/config-docs-contract.sh"
     ],
     "resource": "static-parallel",
     "publicContract": false,
     "contractConsumerGlobs": []
   }
   ```

   `cloudbase-content-source-change` 的 specialtyTests 分两个阶段：归档前用上面这条；T10 归档时换成 `openspec validate cloudbase-content-source --type spec --strict --no-interactive`，并且和归档放在同一个提交里。

   `bukit-test.slnx`：如果 closure 显示它已经被现有规则覆盖，就不用管；否则把它加进现有 `bukit-core-solution` 规则的写法，specialtyTests 用 `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj`（`CoverageGateTests` 和 `CoreBoundaryTests` 会读这个文件）加上 `dotnet build tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj`。**不要用 `dotnet build bukit-test.slnx`**，那等于构建整个测试解决方案。

   **3b. 在 `resourceRules` 里补 `dotnet-serial` 的命令。** 执行分类只看 `resourceRules`，pathRule 的 `resource` 不起作用。在 `dotnet-serial` 那条规则的 `commandContains` 里加上 `dotnet-format.sh`、`code-analysis-ratchet.sh`、`public-api-drift.sh`、`dotnet build tests/`。然后在 codex-workflow 的 self-test 里加断言：这几条命令 `classify` 的结果是 `dotnet-serial`。

4. **重新运行 closure。** 对第 2 步的完整清单再跑一次，要求 `unmappedFiles` 为空。然后运行：

   ```bash
   bash scripts/checks/codex-workflow-self-test.sh
   python3 scripts/checks/codex-workflow.py classify --policy scripts/checks/codex-workflow-policy.v1.json \
     --test-command "bash scripts/checks/dotnet-format.sh" \
     --test-command "bash scripts/checks/code-analysis-ratchet.sh check"
   ```

   要求 self-test 通过，两条命令都分到 `dotnet-serial`。

5. **编写 openspec 提案。**
   - proposal：写 G0 记录和调研结果。
   - design：引用方案，并登记 Notion 硬编码这笔技术债。
   - tasks：列出 T01～T10。这是唯一的勾选来源。
   - 新规格 `cloudbase-content-source`：用 Requirement 和 Scenario 写出 AC1～AC10。
   - 4 份现有规格的变更（ADDED 或 MODIFIED）：
     - `core-report-contracts`：隐私检查要覆盖 CloudBase 标识；
     - `media-localization`：识别 CloudBase 签名链接，以及 D8 的约束；
     - `miniprogram-data`：拦截 CloudBase 临时链接；
     - `incremental-invalidation`：说明 P1 不做增量指纹。

6. **验证。**

   ```bash
   openspec validate add-cloudbase-content-source --type change --strict --no-interactive
   ```

   通过后，按 0.3 第 4 条记录缓存。

7. **转换状态、审查。** 依次转到 `testing`、`review_wait`，做一次专项审查（审 policy 和 openspec）。

8. **提交**（按 0.2 的写法）。
   - message：`spec: propose cloudbase content source`
   - 文件：`scripts/checks/codex-workflow-policy.v1.json`、codex-workflow 的 self-test 文件、`openspec/changes/add-cloudbase-content-source` 下逐个列出的文件、两份计划文档

   提交后转为 `done`，并记录 metrics。

**验收：**

- openspec change 的 strict 校验通过；
- 完整候选清单的 closure 没有 unmapped 文件；
- codex-workflow 的 self-test 通过，新加的分类断言通过；
- 队列和 metrics 文件都已初始化。

**估算：** 1～1.5 天。

---

### T02：诊断码

**文件：**

- 修改：`Bukit.Shared/DiagnosticCode.cs`、`Bukit.Shared/DiagnosticCodeFormatter.cs`
- 修改：`docs/governance/bukit-core-public-api-baseline.v1.json`
- 修改：`guide/dev/cli.md`。在 "BukitJalil consumer contract" 表里新增 `BKT-0Cxx` 一行，负责测试填 `DiagnosticCodeFormatterTests`。这一节在文件里出现了两次（第 59 行和第 89 行），两处都改；重复本身记为范围外发现，不在本任务处理。
- 测试：`tests/Bukit.Shared.Tests/BuildDiagnosticCodeTests.cs`（区间表和类别测试）、`tests/Bukit.Shared.Tests/DiagnosticCodeFormatterTests.cs`

**步骤：**

1. `queue acquire --task T02`，然后对上面的文件运行 closure 和 classify。
2. 先写测试，这时测试应该失败：
   - 在 `BuildDiagnosticCodeTests` 的区间表里加 `["CloudBase"] = (0x0C01, 0x0CFF)`；
   - 类别测试做同样的更新；
   - 为方案第 6 节的每个码断言名称和值；
   - `DiagnosticCodeFormatterTests` 为每个码加一条 InlineData，断言 `Format` 输出 `BKT-0C0n`，`Describe` 返回以 "CloudBase:" 开头的说明。
3. 实现：在 `DiagnosticCode` 里新增成员；在 `DiagnosticCodeFormatter.Describe` 里补上说明文字（formatter 没有分类功能，只有说明）。
4. 跑 `public-api-drift.sh check Debug`，确认只多出新成员；按 0.3 第 3 条用 `snapshot` 更新基线。再跑 `public-api-drift-self-test.sh`。
5. 依次转到 `testing`。跑 closure 给出的测试（预计包括 `Bukit.Shared.Tests` 全量）和 0.3 第 3 条的工具门禁，然后记录缓存。
6. 转到 `review_wait`，做专项审查。
7. **提交。**
   - message：`feat(shared): add CloudBase diagnostic codes`
   - 文件：上面"文件"一栏逐个列出的路径

   提交后转为 `done`，记录 metrics。

**验收：**

- 区间测试、类别测试、formatter 测试都通过；
- drift 只报告新增成员，基线已更新；
- 消费方契约表已登记新区间。

**估算：** 约 50 行生产代码，0.5 天。

---

### T01：配置模型与校验

**文件：**

- 修改：`Bukit.Config/AppConfig.cs`。新增 internal 的 `CloudBaseConfig`，以及 `ContentSourceConfig.CloudBase`。
- 修改：`Bukit.Config/ConfigCollectionReader.cs`、`Bukit.Config/SiteDefaultsApplier.Content.cs`。新增 `ReadCloudBaseConfigFrom`。
- 修改：`Bukit.Config/ConfigStrictFieldValidator.cs`。白名单加入 `cloudbase`，新增 `ValidateCloudBase`。
- 修改：`Bukit.Config/ConfigValidator.cs`、`Bukit.Config/ProviderValidators.cs`。实现方案第 5 节的校验、D8 第 ①②④ 条和第 ③ 条的 `onFailure` 部分、D13、D18。
- 修改：`Bukit.Config/ConfigJsonSchemaGenerator.cs`。枚举加入 `cloudbase`，新增 `CloudBaseSchema()`。
- 测试：`tests/Bukit.Config.Tests/` 下的 `ConfigContractDriftTests`、`ConfigSchemaNestedContractTests`，以及新建的 `CloudBaseConfigTests`。

**步骤：**

1. `queue acquire --task T01`，然后运行 closure 和 classify。
2. 先写测试，覆盖下面这些情况：
   - 合法配置能正确读取，`collectionName` 和 `model` 二选一；
   - 缺少必填项、取值越界、出现未知字段，都失败；
   - YAML 里出现凭据字段，失败；
   - `kind` 不是选定的值，失败；
   - `cacheMode` 不是 `off`，失败；
   - `bodyFormat` 不是 `markdown`，失败；
   - `filter` 出现 `field` 和 `value` 以外的键（例如 `op`），失败；
   - `envId` 不符合 `^[a-z0-9][a-z0-9-]{0,63}$`（含 `/`、`@`、`#`、`?`、`.`、大写），失败（D18）；
   - D8 中能在配置阶段检查的部分，每条单独写一个失败用例：
     - `downloadToLocal: false`；
     - `fileFields` 里有字段不在 `media.fieldKeys` 里；
     - `onFailure: keep`。

   "占位图缺失"不在这里测：`PlaceholderMissing` 由构建设置，配置校验时永远是 false，放到 T04 测。
3. 更新两个契约测试：
   - `content.sources[]` 的键集合加入 `cloudbase`；
   - 新增 4 份快照：`content/sources[]/cloudbase`、`content/sources[]/cloudbase/filter`、`content/sources[]/cloudbase/sort`、`content/sources[]/cloudbase/fieldPolicy`；
   - 加入 `ValidateCloudBase` 的路径映射。
4. 实现读取、校验和 schema。注意三点：
   - `CloudBaseConfig` 必须是 internal（D1）；
   - schema 里 `fieldPolicy` 沿用 `whitelist|all`；
   - 和临时链接域名有关的校验（D8 第 ⑤ 条）不在这里做，放到 T06；占位图存在性放到 T04。
5. 转到 `testing`。跑 closure 给出的测试（预计包括 `Bukit.Config.Tests` 全量和 drift）和工具门禁，记录缓存。
6. 转到 `review_wait`，做专项审查。
7. **提交。**
   - message：`feat(config): add cloudbase content source config`
   - 文件：上面"文件"一栏逐个列出的路径，以及新建的测试文件

   提交后转为 `done`，记录 metrics。

**验收：**

- AC1 中配置阶段的部分（不含 D8 第 ③ 条的占位图检查和第 ⑤ 条）；
- public-api-drift 没有出现新的配置类型；
- `config schema` 的输出里有 `cloudbase`。

**估算：** 300～400 行，1～1.5 天。

---

### T03：`Bukit.CloudBase` transport 与新项目登记

**前置：** G0-4 已经选定鉴权方式，G0-5 已经选定 transport 方案。

**文件：**

- 新建：`src/Bukit-Core/Bukit.CloudBase/Bukit.CloudBase.csproj`。不引用任何项目；`InternalsVisibleTo` 按 D16 的六项填写。
- 新建：`Bukit.CloudBase/Transport/*`、`Bukit.CloudBase/Models/*`（包括源生成的 JSON 上下文）。
- 新建：`tests/Bukit.CloudBase.Tests/`，包括 csproj 和测试文件。
- 修改：`bukit-core.slnx`、`bukit-test.slnx`。
- 修改：`tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs`。在 `GovernedAssemblies` 里加入 `Bukit.CloudBase`（D15）。
- 修改：`docs/governance/bukit-core-public-api-baseline.v1.json`。新增 `Bukit.CloudBase` 的条目，公开面为空。
- 修改：`tests/Bukit.Architecture.Tests/PublicApiBaselineFacts.cs`。`GovernedAssemblyCount` 从 18 改成 19；不改的话，有 10 个测试会失败。
- 修改：`tests/Bukit.Architecture.Tests/CoreProjectReferenceBoundaryTests.cs`。Core 项目数从 18 改成 19，并新增一条断言：`Bukit.CloudBase` 不引用任何项目。
- 修改：`scripts/checks/coverage/list-core-projects.sh`。新增一行 `emit tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj`（不带过滤参数）；CI 只通过这份清单运行 Core 测试。
- 修改：`tests/Bukit.Architecture.Tests/CoverageGateTests.cs`、`scripts/checks/coverage/project-list-self-test.sh`。测试项目数从 13 改成 14。
- 只在复用 SDK 时修改：`Directory.Packages.props`。它会触发 `central-package-management` 规则，这条规则覆盖 13 个项目，要提前预留测试时间。

**步骤：**

1. `queue acquire --task T03`，然后运行 closure 和 classify。
2. 先写测试，全部用伪造的 `HttpMessageHandler`：
   - 鉴权头的生成；
   - 网关域名按 `region` 选择；用 `UriBuilder` 拼地址，发送凭据前断言协议是 https、主机名正好是 `{envId}.api[.intl].tcloudbasegateway.com`；`envId` 含 `/`、`@`、`#`、`?`、`.`、大写或 IDN 时拒绝发送（D18）；
   - 分页直到读完；
   - 查询请求里 filter 和 sort 的构造；
   - 429 和 5xx 按指数退避重试，最多 `maxRetries` 次；
   - 限速符合 `maxRps`；
   - 401、403、404 映射到对应的 `CloudBaseErrorKind`（transport 不引用 `Bukit.Shared`，不产生 `DiagnosticCode`，见 D2）；
   - 响应体超过上限或 JSON 畸形时，报 `CloudBaseResponseInvalid`；
   - 所有异常和日志文本里都不含凭据、签名和查询串（AC7、D14）。
3. 实现 transport。JSON 一律用源生成，以兼容 AOT，不用反射序列化。
4. 如果 G0-5 选择复用 SDK：改为引入 `Tencent.CloudBase`，通过中央包管理（`Directory.Packages.props`）声明版本，测试改用 SDK 的扩展点。
5. 完成上面"文件"一栏里的全部登记，然后运行：

   ```bash
   bash scripts/checks/coverage/project-list-self-test.sh
   bash scripts/checks/public-api-drift-self-test.sh
   ```

6. 转到 `testing`。跑 closure 给出的测试，预计包括：
   - `Bukit.CloudBase.Tests`、`Bukit.Architecture.Tests`；
   - drift 的 check；
   - 覆盖率清单相关的规则；
   - 如果改了 `Directory.Packages.props`，再加上它触发的那些项目。

   另外跑工具门禁，并在本地确认 `Bukit.CloudBase` 的行覆盖率不低于 70%。最后记录缓存。
7. 转到 `review_wait`，做专项审查，重点看安全。
8. **提交。**
   - message：`feat(cloudbase): add CloudBase transport project`
   - 文件：上面"文件"一栏逐个列出的路径，以及新建的全部文件

   提交后转为 `done`，记录 metrics。

**验收：**

- transport 测试、架构测试、两个 self-test 都通过；
- drift 只报告 `Bukit.CloudBase` 这个新的受治理程序集，公开面为空；`PublicApiBaselineFacts` 相关的 10 个测试通过；
- 行覆盖率不低于 70%；
- `dotnet build` 没有裁剪或 AOT 警告。

**估算：** 600～800 行（复用 SDK 可以少一些），2～3 天。

---

### T05：正文链接安全（Markdown 渲染选项）

**文件：**

- 修改：`Bukit.Content/Markdown/BasicMarkdownToHtml.cs`。新增"不可信链接"选项，默认关闭。
- 新建：`Bukit.Content/CloudBase/CloudBaseUrlPolicy.cs`（D9）。它把有协议的 URL 交给 `RenderingSafeUrl.ForLink` / `ForMedia`；这两个类的协议集合是 private，不能直接复用，也不改 `Bukit.Notion`。
- 测试：`tests/Bukit.Content.Tests/` 下新建 `UntrustedLinkMarkdownTests`。

**步骤：**

1. `queue acquire --task T05`，然后运行 closure 和 classify。
2. **先在本地确认现状，不写进测试。** 用 Markdig 分别渲染 `[x](javascript:alert(1))` 和 `<javascript:alert(1)>`，确认现在的输出里有 `javascript:`。这样就核实了分析文档第 10 节第 6 项。结果记进报告。**不要提交任何断言"不安全行为就是默认行为"的测试。**
3. 写目标测试。选项开启时，各种输入的预期如下（与方案第 9 节一致）：

   | 输入 | 预期 |
   |---|---|
   | `javascript:`、`vbscript:`、`data:` 链接和图片 | 丢弃，或降级为纯文本 |
   | `<javascript:alert(1)>` 这样的自动链接 | 丢弃，或降级为纯文本 |
   | `JAVASCRIPT:alert(1)` | 丢弃，或降级为纯文本 |
   | `jav&#x61;script:`、`&#x6A;avascript:` | 丢弃，或降级为纯文本 |
   | 引用定义 `[r]: javascript:alert(1)` | 丢弃，或降级为纯文本 |
   | `java&#9;script:` | 输出里没有可执行的 `javascript:`（Markdig 会编码成 `java%09script:`） |
   | 带标题的图片 | 保留，标题照常输出 |
   | 裸 `www.` 链接（管线开启 `UseAutoLinks` 时） | 按 https 处理，保留 |
   | `//host` | 丢弃，或降级为纯文本 |
   | 引用式链接 | 按同样的规则处理 |
   | `#section` | 保留 |
   | `other-post/` | 保留 |
   | `../img.png` | 保留 |
   | `/abs` | 保留 |
   | `http`、`https`、`mailto`、`tel` | 保留 |

   另外写一组用例：选项关闭时，输出和现在完全一致。用现有的 Markdown 测试样本比较。
4. 实现。开启选项时，渲染流程改为 `Markdown.Parse` → 遍历语法树 → `HtmlRenderer`：
   - 遍历时处理 `LinkInline`（包括图片）和 `AutolinkInline`；
   - 原来独立的图片正则后处理保持不变；
   - URL 判断交给 `CloudBaseUrlPolicy`，只作用于解析后的 `LinkInline.Url` / `AutolinkInline.Url`，用 RFC 3986 语法判断有没有协议（D9）。

   选项关闭时，保持原来的 `Markdown.ToHtml` 路径不变。
5. 转到 `testing`。跑 closure 给出的测试（预计包括 `Bukit.Content.Tests` 全量）和工具门禁，记录缓存。
6. 转到 `review_wait`，做专项审查，重点看安全。
7. **提交。**
   - message：`feat(content): add untrusted-link mode to markdown rendering`
   - 文件：上面"文件"一栏逐个列出的路径

   提交后转为 `done`，记录 metrics。

**验收：**

- AC4 的渲染部分（provider 在 T04 才接入这个选项）；
- Markdown provider 的默认行为不变，现有测试全部通过。

**估算：** 80～150 行，0.5～1 天。

---

### T04：Provider、字段投影与工厂接入

**文件：**

- 新建：`Bukit.Content/CloudBase/` 下的 `CloudBaseContentSource.cs`、`CloudBaseContentSourceOptions.cs`、`CloudBaseFieldProjector.cs`、`CloudBaseBodyStore.cs`。
- 修改：`Bukit.Content/Bukit.Content.csproj`。加 ProjectReference，引用 `Bukit.CloudBase`。
- 修改：`Bukit.Engine/ContentProviderFactory.cs`。新增 `cloudbase` 分支和 `CreateCloudBaseProvider`。
- 修改：`Bukit.Engine/ContentFieldReservations.cs`、`Bukit.Engine/ContentSchemaValidator.cs`。加入 CloudBase 的系统字段。
- 修改：`tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`。在 `Bukit.Content` 的项目引用表里加入 `Bukit.CloudBase`。
- 新建：`Bukit.Content/CloudBase/CloudBaseErrorMapper.cs`。把 `CloudBaseErrorKind` 映射到 `DiagnosticCode.CloudBase*`（D2）。
- 测试：`tests/Bukit.Content.Tests/CloudBase/*`，以及 `tests/Bukit.Engine.Tests` 里的端到端用例和占位图检查。

**步骤：**

1. `queue acquire --task T04`，然后运行 closure 和 classify。
2. 先写测试：
   - 字段映射：title、slug、publishAt、updatedAt；
   - 白名单模式和 `all` 模式；
   - 硬性剔除：`_openid` 等字段在 `all` 模式下也不出现（AC3）；
   - `maxItems` 截断；
   - `mode: data` 的行为；
   - body store 开启了不可信链接模式，用一条 `javascript:` 正文断言（AC4）；
   - `CloudBaseErrorKind` 到诊断码的映射；
   - D8 第 ③ 条：`fileFields` 非空、占位图缺失时，`CreateCloudBaseProvider` 以 `CloudBaseConfigInvalid` 失败（`Bukit.Engine.Tests`）；
   - 端到端：用伪造的 HTTP 构建一个站点，包括分页和 filter/sort（AC2）；
   - 没有 cloudbase 源的站点，构建产物逐字节不变。
3. 实现 provider：
   - 凭据通过 `SecretStore.Get` 读取；
   - body store 打开 T05 加的选项；
   - 日志只输出记录 ID 的哈希（D14）；`_id` 在 `all` 模式下不投影（D7）；
   - `CreateCloudBaseProvider` 在 `fileFields` 非空时调用 `MediaPlaceholderProbe.FindMissing`（D8 第 ③ 条）。
4. 转到 `testing`。跑 closure 给出的测试（预计包括 `Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` 全量）和工具门禁，记录缓存。
5. 转到 `review_wait`，做专项审查。
6. **提交。**
   - message：`feat(content): add CloudBase content provider`
   - 文件：上面"文件"一栏逐个列出的路径，以及新建的全部文件

   提交后转为 `done`，记录 metrics。

**验收：** AC2、AC3，AC4 的 provider 部分，以及 AC1 中 D8 第 ③ 条的构建阶段检查。

**估算：** 500～800 行，2～3 天。

---

### T06：`cloud://` 换链、签名链接与 D8 第 ⑤ 条

**前置：** G0-6 通过，也就是 S3 已经给出临时链接的域名和签名参数名。

**文件：**

- 新建：`Bukit.Content/CloudBase/CloudBaseFileResolver.cs`
- 修改：`Bukit.Content/Media/SignedUrlIdentity.cs`。新增 CloudBase 规则（D10）。
- 修改：`Bukit.Config/ProviderValidators.cs`。实现 D8 第 ⑤ 条：`downloadHosts` 和 `skipHosts` 不能把临时链接的域名排除在外，违反时报 `CloudBaseMediaLocalizationRequired`。
- 修改：`guide/user/04-site-yaml-config.md`。在签名方案表里加一行。
- 测试：`tests/Bukit.Content.Tests/`、`tests/Bukit.Config.Tests/`、`tests/Bukit.Engine.Tests/`（小程序签名链接拦截）。

**步骤：**

1. `queue acquire --task T06`，然后运行 closure 和 classify。
2. 先写测试：
   - `fileFields` 和正文图片位置（`![]()`、图片引用定义）里的 `cloud://` 被收集起来，每 50 个一批换链，再替换回原文；
   - D17：正文里指向非图片的 `[pdf](cloud://…/a.pdf)` 降级为纯文本；不在 `fileFields` 里的公开字段含 `cloud://` 时整个值被丢弃；两种情况都报 `CloudBaseUnsupportedFileReference`；
   - 部分换链失败时报 `CloudBaseFileResolveFailed`，并且能定位到失败的条目；
   - `SignedUrlIdentity.IdentityTarget` 会去掉 CloudBase 的签名参数；
   - `MiniProgramJson.IsSignedUrl` 能识别 CloudBase 临时链接；
   - D8 第 ⑤ 条的失败用例；
   - 下载失败时落到占位图；
   - 构建产物里没有 `cloud://`、envId、临时链接的域名和签名（AC5）。
3. 实现。临时链接不能写进任何会持久化的地方；`maxAge` 按 S3 的结论设置。
4. 转到 `testing`。跑 closure 给出的测试（会命中 media-localization 相关规则，测试范围可能变大）和工具门禁，记录缓存。
5. 转到 `review_wait`，做专项审查。
6. **提交。**
   - message：`feat(content): resolve CloudBase file ids and recognize signed links`
   - 文件：上面"文件"一栏逐个列出的路径

   提交后转为 `done`，记录 metrics。

**验收：** AC5，以及 AC1 中 D8 第 ⑤ 条的部分。

**估算：** 约 150 行，外加签名规则，1 天。

---

### T07：公开产物隐私

**文件：**

- 修改：`Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs`
- 修改：`Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs`
- 修改：`Bukit.Engine.Primitives/PublicContentProjectionPolicy.cs`
- 修改：`Bukit.Engine.Feeds/JsonFeedGenerator.cs`
- 按需修改：`Bukit.Engine.Audit/PublishUrlSnapshot.cs`，也就是 `PublishUrlSemanticHasher` 的归一化规则
- 修改：`Bukit.Config/InternalsVisibleTo.cs`。加上 `Bukit.Engine.Audit` 和 `bukit`（D11）。不加的话，隐私检查读不到 internal 的 `CloudBase` 配置，T08 的 doctor 也读不到。
- 修改：deploy 命令的入口，让它读取 site.yaml 里的 envId 并传给 `DeploymentPrivacyValidator`（D11）。具体文件在本任务开始时用 closure 和调用方检索确定，写进报告和提交清单。
- 测试：`tests/Bukit.Engine.Tests/`、`tests/Bukit.Cli.Tests/`

**步骤：**

1. `queue acquire --task T07`，然后运行 closure 和 classify。
2. 先写测试：
   - 公开 HTML 或 JSON Feed 里出现 envId、`cloud://`、临时链接的域名或签名参数时，构建阶段检测会失败（AC6）；
   - 部署暂存目录里出现 `cloud://` 或临时链接的域名或签名参数时，部署校验失败，不需要配置；deploy 命令传入 envId 时，envId 也能检测到；
   - 发布审计报告 `.bukit/publish-audit-report.json` 的结构不变；
   - 投影策略和 JSON Feed 会过滤掉 CloudBase 来源的敏感实体；
   - 正文里如果有每次构建都会变的临时链接，语义哈希不应因此改变。只有确认确实需要时才改 hasher。
3. 实现：在现有的 Notion 逻辑旁边并列加上 CloudBase 的检测（D11），不做通用化重构。
4. 转到 `testing`。跑 closure 给出的测试，预计包括：
   - `Bukit.Engine.Tests`、`Bukit.Cli.Tests` 全量；
   - `core-report-inventory.py --self-test`；
   - `core-report-contracts` 规则对应的测试；
   - 工具门禁。

   然后记录缓存。
5. 转到 `review_wait`，做专项审查，重点看隐私。
6. **提交。**
   - message：`feat(audit): detect CloudBase identifiers in public output`
   - 文件：上面"文件"一栏逐个列出的路径

   提交后转为 `done`，记录 metrics。

**验收：** AC6；不涉及 CloudBase 的站点，审计报告内容不变。

**估算：** 200～300 行，1～1.5 天。

---

### T08：Doctor

**文件：**

- 新建：`Bukit.Cli/Commands/DoctorCloudBaseChecker.cs`
- 修改：`Bukit.Cli/Commands/DoctorCommand.cs`
- 修改：`Bukit.Cli/Bukit.Cli.csproj`。加 ProjectReference，引用 `Bukit.CloudBase`（D16）。
- 测试：`tests/Bukit.Cli.Tests/`

**步骤：**

1. `queue acquire --task T08`，然后运行 closure 和 classify。
2. 先写测试，全部用伪造的 HTTP：
   - 连通性正常和失败；
   - 凭据缺失；
   - 401 和 403；
   - 集合或模型不存在；
   - 映射的字段在样本记录里不存在；
   - 使用管理员级凭据时报 `CloudBaseAdminCredentialInUse` 警告（AC8）；
   - `fileFields` 非空而占位图缺失时给出警告（复用 `MediaPlaceholderProbe.CheckWarning`，D8 第 ③ 条）；
   - `_id` 被显式列进 `allowed` 时给出警告（D7）；
   - doctor 的输出里不含凭据。
3. 实现：结构参照 `DoctorNotionChecker` 和 `DoctorNotionSchemaChecker`。这两个文件只作参照，不改。
4. 转到 `testing`。跑 closure 给出的测试和工具门禁，记录缓存。
5. 转到 `review_wait`，做专项审查。
6. **提交。**
   - message：`feat(cli): add CloudBase doctor checks`
   - 文件：上面"文件"一栏逐个列出的路径

   提交后转为 `done`，记录 metrics。

**验收：** AC8。

**估算：** 150～250 行，1 天。

---

### T09：文档、CHANGELOG 与小改动

**文件：**

- 新建：`guide/user/27-cloudbase-content.md`。路径在 T00 已经写进 policy，不能改；写法参照 `06-notion-content.md`。
- 修改：`guide/user/README.md`（索引表加一行，参照 `06` 那一行）、`guide/user/07-multi-source-data.md`、`12-cli-reference.md`、`14-troubleshooting.md`、`16-parameter-cheatsheet.md`、`25-miniprogram-data.md`。
- 修改：`guide/dev/config-site-yaml.md`。
- 修改：`guide/skills/bukit-content-model/SKILL.md`、`guide/skills/bukit-troubleshooting/SKILL.md`。
- 修改：`Bukit.Cli/SecretsStdinOption.cs`。帮助文本和示例里加入 CloudBase 的密钥名。
- 可选修改：`Bukit.Engine/OutputDirectoryCleaner.cs`，只改文案。
- 修改：`CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md`，记在 Unreleased 下。

**步骤：**

1. `queue acquire --task T09`，然后运行 closure 和 classify。槽 4 可以提前起草文档，起草期间不能写仓库。
2. 编写文档，必须包含：
   - 鉴权方式和权限风险；
   - 管理员级凭据的警告；
   - D8 的五个前提条件；
   - 排错：`BKT-0C01`～`BKT-0C0B` 各自的含义和处理方法；
   - `cloud://` 只在 `fileFields` 和正文图片里换链，其他位置会被降级（D17）；
   - `envId` 的字符集要求（D18）；
   - P1 的限制：只支持 Markdown、只支持图片、不支持缓存、只支持一种数据形态、`filter` 不支持 `op`；
   - 配置字段 `collectionName` 和通用字段 `collection` 的区别。
3. 转到 `testing`。跑 closure 给出的检查，预计包括：
   - `docs/active-links.sh`、`docs/size-policy.sh`；
   - `config-docs-contract.sh`；
   - 命中时还有 `cli-docs-sync.sh`；
   - `ConfigContractDriftTests`（它会检查文档里引用的字段）；
   - 因为改了 C#，还要跑工具门禁。

   然后记录缓存。
4. 转到 `review_wait`，做专项审查（文档完整性）。
5. **提交。**
   - message：`docs: document CloudBase content source`
   - 文件：上面"文件"一栏逐个列出的路径

   提交后转为 `done`，记录 metrics。

**验收：** AC10 的文档部分。漏写的文档不会被自动检查发现，所以审查时要对照第 2 步的清单逐项核对。

**估算：** 1～1.5 天。

---

### T10：OpenSpec 归档、最终验证与审查

**步骤：**

1. `queue acquire --task T10`，然后执行：

   ```bash
   openspec validate add-cloudbase-content-source --type change --strict --no-interactive
   ```

   通过后，把 `tasks.md` 的勾选同步到实际进度。
2. **归档（需要授权）。** 授权后，归档 openspec 变更，把规格同步到 `openspec/specs/`。这会改动新规格和 4 个现有规格（`core-report-contracts`、`media-localization`、`miniprogram-data`、`incremental-invalidation`）。同时把 policy 规则 `cloudbase-content-source-change` 的 specialtyTests 换成归档后的命令。没有授权就跳过这一步，在最终报告里写明"未归档"。
3. **最终验证。** 用 T00 记下的 BASE（`/tmp/bukit-cloudbase-base`）算出全部变更，**包括第 2 步的归档改动**，运行 closure 和 classify，按输出**逐条**执行全部 specialtyTests，再加上工具门禁；如果第 2 步改了 policy，还要跑 `codex-workflow-self-test.sh`。每条先 `cache check`；HEAD 自上次记录后变过，就会未命中，按 0.3 第 4 条重跑，这是预期行为。

   跑之前转到 `testing`。
4. **Native AOT（需要授权）。**

   ```bash
   bash scripts/build/native-aot.sh <version> osx-arm64 <output-root> Release
   ```

   然后用伪造的配置跑一次 `config check`。没有授权时，改用 `dotnet run` 做一次端到端，并在最终报告里写明"AOT 未验证"。
5. **真实环境冒烟（需要授权，用消费方的测试环境）。** 运行 `doctor` 和 `build`，确认 AC5、AC6 在真实数据上成立。结果脱敏后记录。
6. **统一审查。** 转到 `review_wait`。用第 4 节生成的每个任务的证据文件和共用的问题文件，每个任务一个 `--evidence`，每个变更文件一个 `--changed`：

   ```bash
   ev=(); for f in /tmp/codex-reports/evidence/*.json; do ev+=(--evidence "$f"); done
   ch=(); for f in "${changed[@]}"; do ch+=(--changed "$f"); done
   python3 scripts/checks/codex-workflow.py review-scope "${ev[@]}" \
     --findings /tmp/codex-reports/cloudbase-findings.json "${ch[@]}"
   ```

   只审查跨任务的交叉点、已经失效的证据、没有被覆盖的变更文件、public 或序列化契约、归档改动，以及还没关闭的 Critical/Important 问题。没有 Critical/Important 就结束。
7. **提交。** 提交 `tasks.md` 的勾选；如果做了第 2 步，归档改动单独一个提交。
   - message：`spec: update cloudbase content source tasks`；归档提交用 `spec: archive cloudbase content source change`
   - 文件：`openspec/changes/add-cloudbase-content-source/tasks.md`；归档提交逐个列出移动和同步的规格文件，以及 policy 文件

   提交后转为 `done`。
8. **指标。**

   ```bash
   python3 scripts/checks/codex-workflow.py metrics add --state /tmp/bukit-cloudbase-metrics.json \
     --task T10 --phase review --duration-ms <n> --cache-status <hit|miss|none> --status completed
   python3 scripts/checks/codex-workflow.py metrics report --state /tmp/bukit-cloudbase-metrics.json
   ```

**验收：**

- AC1～AC10 全部满足；
- 队列里没有活动任务；
- 最终报告列出每条验收的证据、没有验证的项目（例如 AOT、真实环境、未归档），以及记录下来的 Minor 问题。

**估算：** 1.5～3 天，按全部测试重跑计算，不含等待授权的时间。

---

### Part B：BukitJalil 交接（不在本仓库，另行授权）

BukitJalil 按固定表给 BKT 码分类。本计划合入之后，要把下面这些交给 BukitJalil 的负责人：

- 新码区间 `BKT-0C01`～`BKT-0CFF`，以及每个码的含义；
- 需要的最低 Bukit 版本（`MIN_BUKIT_VERSION`）。

修改 BukitJalil 需要单独取得授权，不算在本计划的工作量里。

## 4. 每个任务的固定收尾

每个任务（T00～T09；T10 按它自己的步骤）结束时，依次做这几件事：

1. `queue transition --to testing`。跑 closure 给出的测试和工具门禁（T00 和 T09 中不涉及 C# 的部分除外），每条先 `cache check`，GREEN 后 `cache record`。
2. `queue transition --to review_wait`，做专项审查。只有 Critical 或 Important 问题才退回 `writing`。
3. 按 0.2 的写法提交，然后 `queue transition --to done`。
4. 执行 `metrics add`，记录各阶段的耗时、缓存命中情况、是否重跑、是否发生写者冲突。
5. 为 T10 的 `review-scope` 留证据：
   - 写 `/tmp/codex-reports/evidence/<task>.json`，内容是 `{"schemaVersion": 1, "taskId": "<task>", "cacheStatus": "hit|miss", "closureFiles": [...], "publicContractFiles": [...]}`，后两项取自本任务的 closure 输出；
   - 把专项审查发现的每个问题追加到 `/tmp/codex-reports/cloudbase-findings.json`，格式是 `{"schemaVersion": 1, "findings": [{"id": ..., "severity": ..., "status": ..., "files": [...]}]}`。这个文件在 T00 第 1 步初始化为空列表。

## 5. 风险与应对

| 风险 | 触发信号 | 应对 |
|---|---|---|
| G0-4 只能用管理员级凭据 | S2 的鉴权矩阵里没有只读路径 | 终止 A，改走 C；或者书面接受风险后继续 |
| SDK 不兼容 AOT | S2 统计到裁剪或 AOT 警告 | 按 D3 自己写 transport |
| 临时链接的参数无法识别 | S3 发现参数名和现有规则冲突 | T06 暂停，补充调研；不能放行签名链接 |
| 临时链接在构建过程中过期 | 正文图片延迟下载时报错 | 按 S3 设置 `maxAge`；失败时落到占位图 |
| 临时链接每次重签，导致正文指纹变化 | 增量构建总是不命中 | P1 可以接受；P3 用 `updatedAt` 和文件 ID 做稳定标识 |
| 新项目覆盖率达不到 70% | T03 本地的覆盖率报告 | 先补测试，覆盖率达标前不提交 |
| closure 测试范围太大、耗时太长 | 单个任务的测试超过 30 分钟 | 同一个 HEAD 上用 `cache check` 复用证据（跨任务不能复用）；`dotnet-serial` 的测试排队执行；不能因为耗时而截断测试 |
| 工作量超出估算 50% | metrics 显示偏差 | 停下来重新评估范围，必要时把功能移到 P2 或 P3 |
| 工作区混入别人的改动 | `git status` 出现无关文件 | 停止，交给所有者处理；按 0.2 只暂存本任务的文件 |

## 6. 验收清单

- [ ] G0-1～G0-5 已记录并经负责人确认；G0-6 在 T06 之前确认
- [ ] T00：队列、metrics、证据和问题文件已初始化；4 条新 pathRule 字段齐全，`resourceRules` 已补；openspec 提案（1 个新规格、4 份现有规格的变更）；closure 没有 unmapped 文件
- [ ] T02：诊断码（含 `0x0C0B`）；formatter 测试；public API 基线；`guide/dev/cli.md` 消费方契约表（两处）
- [ ] T01：配置契约；4 份嵌套快照；D8 中配置阶段能查的部分；`envId` 白名单
- [ ] T03：transport（只产生 `CloudBaseErrorKind`；主机名断言）；新项目登记全部完成（项目数 19、受治理程序集和 `GovernedAssemblyCount` 19、覆盖率清单、测试项目数 14）；覆盖率不低于 70%；没有 AOT 警告
- [ ] T05：不可信链接模式（包括自动链接、引用定义、大小写和实体编码）；`#片段` 和相对链接保留；默认行为不变
- [ ] T04：provider；硬性剔除；错误映射；占位图检查；端到端；body store 打开安全模式
- [ ] T06：`cloud://` 换链范围和 D17 降级；签名链接规则；D8 第 ⑤ 条
- [ ] T07：隐私检测与过滤；`Bukit.Config` 内部可见性；部署阶段检测
- [ ] T08：doctor
- [ ] T09：用户、开发、技能、排错文档；三语 CHANGELOG
- [ ] T10：openspec 已归档（已授权时）；包括归档改动在内的全部 specialtyTests 和工具门禁通过；AOT 和真实环境冒烟（已授权的部分）；统一审查；tasks.md 已提交；metrics 报告；队列已清空
- [ ] Part B：BukitJalil 交接已发出（另行授权）
