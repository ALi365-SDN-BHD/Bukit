# 脚本 P1-P3 加固设计

语言：[English](2026-07-14-script-p1-p3-hardening-design.md) | 简体中文

## 状态

已批准的设计基线：路线 A，完整能力修复。

## 目标

关闭 2026-07-14 脚本全量审计发现的全部 P1-P3 问题，且不保留任何
“假绿”行为。脚本只有在获得其所声称校验契约的直接证据后，才可以返回成功。

实现必须限制在以下活跃仓库表面内：

- `scripts/`
- `guide/skills/scripts/`
- `.github/workflows/release.yaml`
- 聚焦的 Architecture 测试和活跃开发文档
- `.trae/skills/` 下三个已审计的辅助脚本

不得修改备份/参考目录 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/` 和
`scripts-0.2/`，也不得把它们作为可执行来源。本设计不涉及
`src/Bukit-Core/` 下的任何 Bukit Core 运行时代码。

## 问题关闭矩阵

| ID | 优先级 | 当前缺陷 | 必须达到的最终状态 |
|---|---|---|---|
| F1 | P1 | 安全回归接受零匹配测试 | 每个声明的选择器都必须对应至少一个已执行且通过的 TRX 结果 |
| F2 | P1 | 发布元数据接受重复、额外或陈旧资产 | 磁盘文件、校验和、JSON 元数据与预期 RID 必须构成同一个精确集合 |
| F3 | P1 | 发布冒烟接受空目录，且上传前未执行冒烟 | 每个最终归档都必须先解压，并由其中打包的 CLI 完成 Core 冒烟后才能上传 |
| F4 | P1 | 两个扫描器把搜索工具故障转换为成功 | 无匹配可以成功；扫描器故障必须是独立的非零错误，并由可移植性测试覆盖 |
| F5 | P1 | Brainstorm 停止辅助脚本信任任意 `/tmp` 路径和 PID | 发信号或删除前必须验证会话路径、所有者、PID、进程命令和会话令牌 |
| F6 | P2 | `build-repro.sh` 和 `native-aot.sh` 是成功的空操作 | 两者必须执行真实工作，并在无法证明其构建契约时失败 |
| F7 | P2 | Core CLI 契约扫描备份工作流 | 只搜索活跃脚本和 `.github/workflows/` |
| F8 | P2 | Native AOT 打包可能保留陈旧发布文件，并把路径插入 PowerShell 源码 | 发布输出必须干净且受保护；归档路径必须作为数据跨越 PowerShell 边界 |
| F9 | P3 | 活跃尺寸策略忽略 Python 自动化脚本 | 活跃 Shell、Python 自动化及已审计辅助脚本必须共享脚本尺寸策略 |
| F10 | P3 | Brainstorm 启动辅助脚本在选项缺值时可能循环 | 严格模式和完备参数解析器必须立即拒绝缺失或畸形值 |
| F11 | P3 | 污染源定位脚本拆分文件名并隐藏测试失败 | NUL 安全枚举必须保留路径；测试失败只能得到“不确定”或“失败”，绝不能得到“干净” |

## 架构

本次修复采用四个有界契约层，而不是新增一个大型门禁：

1. **证据生产器**执行真实操作：`dotnet test`、Native AOT 发布、归档构建、
   归档解压和 CLI 冒烟命令。
2. **聚焦验证器**解析 TRX、发布元数据等结构化证据。不适合或不安全地用
   Bash 解析的内容放入小型 Python 辅助脚本。
3. **入口自测**注入空值、重复、陈旧、畸形和工具失败场景。它们必须先在旧行为
   上失败，再在实现变更后通过。
4. **工作流契约**以结构化方式证明执行顺序和连线。发布任务必须在打包和上传
   artifact 之间对归档执行冒烟。

现有公开脚本路径保持稳定。实现可以新增聚焦辅助脚本，但不得引入第二套发布清单
schema，也不得建立宽泛的替代框架。

## F1：安全回归证据

`scripts/security/security-regression.sh` 保持每个测试项目一次
`dotnet test` 调用，但每次调用都要把唯一命名的 TRX 文件写入新的临时结果目录。
项目的选择器列表是 VSTest 过滤表达式和结果验证的唯一事实来源。

一个聚焦的 Python 验证器读取 TRX，并证明以下全部条件：

- 项目恰好存在一个预期 TRX 结果；
- `total` 大于零；
- 所有发现的测试均已执行且通过；
- 不存在失败、跳过或未执行测试；
- 每个声明的选择器都按完全限定类名或方法名匹配至少一个已执行结果。

`dotnet test` 的退出码继续作为测试失败的权威证据。TRX 验证负责关闭 VSTest
空过滤器可能以零退出的独立行为。退出时删除临时结果。
`BUKIT_SECURITY_SKIP_RESTORE=1` 继续只表示 `--no-restore`，绝不绕过证据验证。

自测使用假的 `dotnet` 可执行文件生成有效、零测试、缺少选择器、缺少 TRX 和
失败结果夹具，无需运行真实测试套件。

## F2：精确发布资产契约

保留以下 Shell 入口：

- `scripts/release/prepare-release-assets.sh`
- `scripts/release/verify-release-assets.sh`

一个聚焦的 Python 辅助脚本负责规范资产名校验、元数据解析、哈希和精确集合比较。
准备过程在新的同级暂存目录中进行。只有所有输入和生成的元数据文件都通过校验后，
辅助脚本才替换请求的输出目录。输出路径必须规范化；如果它是文件系统根目录、
仓库根目录、`.`、`..`，或者跨越带符号链接的父目录边界，则拒绝执行。

准备阶段拒绝：

- 缺失输入、非普通文件或符号链接；
- 重复源路径或重复 basename；
- 包含分隔符、`.`/`..`、控制字符或保留元数据名称的名字；
- 不符合 `bukit-<version>-<known-rid>` 且扩展名与平台不匹配的归档名。

验证把以下四种表示视为双射：

1. 根据预期 RID 推导出的归档名；
2. 磁盘顶层的普通归档文件；
3. `release-manifest.json` 和 `checksums.json` 中的资产对象；
4. 严格格式的 `checksums.txt` 记录。

所有名称必须唯一。JSON 对象只能包含精确的必需键和类型；SHA-256 必须是 64 位
小写十六进制；字节大小必须是非负整数；每条校验和记录必须恰好包含一个摘要和一个
安全 basename。嵌套路径、符号链接、未列出文件、额外校验和记录以及重复预期 RID
全部失败。

发布工作流始终把选定 RID 集合传给验证器，而不是只在正式发布时传入。`all`
展开为三个受支持的 RID。

## F3：对最终归档执行冒烟

`scripts/smoke/release-artifacts.sh` 从目录存在性探针改为 artifact 执行门禁。
其精确接口为 `release-artifacts.sh <archive-or-publish-dir> <rid>`。为保持本地兼容，
它既接受未打包的发布目录，也接受最终 `.tar.gz`/`.zip` 归档；发布工作流始终传入
最终归档。

对于归档，聚焦的解压器必须在解压前验证成员路径：

- 不是绝对路径；
- 不包含 `..` 路径穿越；
- 规范化后没有成员逃出临时根目录；
- 归档类型和文件名与 RID 一致。

门禁必须定位到恰好一个预期可执行文件（`bukit` 或 `bukit.exe`），把
`tests/fixtures/basic-markdown-site` 复制到隔离临时目录，并使用明确的
`BUKIT_BIN`、配置和输出路径调用 `scripts/smoke/core.sh`。因此，成功将证明归档中的
二进制完成了 `config check`、干净构建和 publish audit。空归档、缺少可执行文件、
重复可执行文件、解压错误或任何 CLI 失败均为阻塞错误。

`package-native-aot.sh` 同时向工作流输出 `archive` 和 `publish_dir`，后者用于诊断。
每个平台的打包任务都在打包后、`actions/upload-artifact` 前增加具名冒烟步骤。
Architecture 测试解析工作流 YAML，并断言该顺序以及归档输出的数据流。

`scripts/smoke/core.sh` 中失效的示例路径改为已跟踪的基础夹具。

## F6 和 F8：真实 Native AOT 与可重复性

### 打包卫生

`scripts/build/package-native-aot.sh` 规范化输出根目录，拒绝带符号链接的发布父目录，
并且只有在证明派生目录严格位于输出根目录之下后，才重建
`<output-root>/publish/<rid>`。写入前删除相同版本和 RID 的既有归档。发布或归档
操作失败时，不得把旧输出误认为新输出。

在 Windows 上，目标路径通过环境变量或 PowerShell 位置参数传入，绝不插入
PowerShell 源码文本。ZIP 必须包含完整发布目录；结果缺失或为空时失败。使用假的
PowerShell 可执行文件覆盖路径含单引号的回归场景。

### Native AOT 兼容入口

`scripts/build/native-aot.sh` 成为规范打包脚本的严格兼容入口。其精确接口是
`native-aot.sh <version> <rid> <output-root> [configuration]`；configuration
默认 `Release`，其余三个值强制必填。脚本打印委托命令上下文，并返回打包脚本的
状态和归档路径。参数缺失或 RID 不受支持时以用法状态 2 退出。

### 确定性的 clean-twice 证明

`scripts/build/build-repro.sh` 的精确接口为
`build-repro.sh <version> <rid> [configuration]`，configuration 默认 `Release`。
它针对同一版本、当前提交、RID、configuration 和确定性构建属性执行两次隔离的
Native AOT 打包。它比较展开后的发布树，而不比较带时间戳的归档容器字节。比较范围
覆盖精确的相对文件集合、文件类型、大小以及每个普通文件的 SHA-256。出现符号链接或
特殊文件时证明失败。

发生差异时，脚本打印缺失、额外和内容变化的相对路径并返回非零。工具链或 Native
AOT 的非确定性不能降级成警告。临时根目录始终清理。

自测使用假的 `dotnet`、归档和 PowerShell 命令，证明陈旧发布文件会被移除、危险
输出路径被拒绝、单引号路径始终作为数据传递，以及相同/不同的干净构建会被正确分类。
最终验证还必须针对当前主机 RID 执行真实可重复性命令。

## F4、F7 和 F9：活跃扫描器契约

`scripts/checks/active-workflow-boundary.sh` 和
`guide/skills/scripts/validate-skills-strict.sh` 使用仓库标准的 grep 状态处理模式：

- 状态 0：发现匹配，按契约违规处理；
- 状态 1：无匹配，正常继续；
- 状态大于 1：打印明确的 `text search failed` 错误，并返回工具的非零状态。

skills 检查不再依赖 ripgrep。两个脚本都加入 `ci-fast-portability-self-test.sh`，
覆盖无 ripgrep 环境和注入 grep 故障两种情况。

`scripts/checks/core-cli-contract.sh` 只搜索 `scripts/` 和 `.github/workflows/`。
备份工作流目录既不参与搜索，也不通过排除列表隐藏，从而避免未来范围漂移被掩盖。

`scripts/checks/docs/size-policy.sh` 枚举 `scripts/`、`guide/skills/scripts/` 和已审计
`.trae/skills` 辅助表面中的 `.sh`、`.py` 自动化脚本。全部沿用 200 行脚本上限。
文档保留现有 1000 行上限。门禁必须在一次运行中报告所有违规。

## F5 和 F10：Brainstorm 服务生命周期安全

`start-server.sh` 增加 `set -euo pipefail`、`require_value` 参数解析辅助函数，并立即
拒绝缺失值、未知参数、互相冲突的前台/后台参数、包含换行的路径和空 host。
会话状态以限制性 umask 创建。

每个已启动服务分别记录不可 `source` 的独立状态文件：

- 数字 PID；
- 当前数字 UID；
- 规范化的 `server.cjs` 路径；
- 同时出现在 Node 进程参数列表中的安全单会话令牌。

前台模式使用 `exec`，使记录的 PID 就是 Node 进程。后台模式调用相同的绝对服务
路径和令牌。启动失败只删除本次新建的会话。

`stop-server.sh` 在发送任何信号前规范化传入目录并验证全部状态字段。它把 `ps`
报告的 PID 所有者、预期服务路径和会话令牌与实时进程命令比较。状态缺失、畸形、
过期、PID 被复用、属于其他用户或身份不匹配时，脚本必须报错，且不得调用 `kill`
或删除文件。

只有规范化路径是 `/tmp` 的直接子目录，且 basename 严格符合生成格式
`brainstorm-<pid>-<time>-<random>` 时，才允许递归删除。有效停止后仍保留持久化的
`.superpowers/brainstorm/` 会话。先尝试 SIGTERM；只有经过验证的同一进程在宽限期后
仍存活时才允许 SIGKILL。

快速辅助自测不启动真实服务，但覆盖选项缺值、参数冲突、畸形状态、任意 `/tmp`
路径、PID 身份不匹配、有效停止以及临时/持久目录清理差异。

## F11：可靠的污染源搜索

`find-polluter.sh` 使用 NUL 分隔的 `find` 输出和 Bash 数组，保留测试文件名中的
空格、制表符、glob 字符和换行。污染目标在运行前已存在，或者模式匹配零个测试时，
均在测试执行前失败。

每个测试都以 `npm test -- <exact-path>` 运行，输出写入临时日志。分类顺序为：

1. 如果出现污染，报告精确测试及其命令状态，然后返回“找到污染源”状态；
2. 如果没有污染但测试命令失败，记录该失败并继续寻找污染源；
3. 如果最终没有污染源但存在任意命令失败，返回非零“不确定”结果并列出失败测试；
4. 只有全部测试成功且无污染时，才打印 `No polluter found` 并以零退出。

辅助自测包含文件名带空格的测试、产生污染的测试、失败但不污染的测试、零匹配以及
完全干净的运行。

## 错误处理与输出规则

- 用法错误和畸形调用者输入返回 2。
- 契约违规、操作失败、不安全路径和证据不一致返回 1；如果底层工具存在值得保留的
  更具体非零状态，则保留该状态。
- 工具错误必须明确标识为工具错误，不能伪装成普通契约不匹配。
- 所有临时目录使用 Bukit 专用前缀，并由 trap 删除。
- 诊断输出必须指出失败的项目、RID、归档、选择器或相对文件。不得打印秘密或任意
  文件内容。
- 证据生产或验证命令周围不得出现 `|| true`。只有在保存主要退出状态后，尽力清理
  才可以忽略错误。

## 测试与交付策略

实现是一个父任务，包含以下按顺序执行且独立验证的子任务：

1. 安全 TRX 证据及其自测。
2. 精确发布准备/验证及其自测。
3. 干净 AOT 打包、安全 PowerShell 传输、真实 Native AOT 与可重复性入口及其自测。
4. 最终归档冒烟、发布工作流和 Architecture 契约。
5. 扫描器故障分类、活跃范围和 Python 尺寸策略。
6. Brainstorm 生命周期安全及其辅助自测。
7. 污染源分类及其辅助自测。
8. 活跃文档同步和聚合审计。

每个子任务都必须先观察到红色测试或注入故障，再修改生产实现。转绿后运行：

```bash
bash scripts/checks/post-change-targeted.sh -- <that-subtask-paths>
```

适用时还必须运行以下归属检查：

- F1 的真实 `scripts/security/security-regression.sh Release`；
- 发布资产、打包、冒烟、可移植性和辅助自测；
- 发布工作流变更对应的定向 `Bukit.Architecture.Tests`；
- F6 的一次真实主机 RID `build-repro.sh`；
- 每个变更 Shell 脚本的 `bash -n`，以及每个变更 Python 辅助脚本的 Python 编译；
- `git diff --check` 和明确的备份目录范围检查。

CI/release/gate 变更属于高风险。由于用户没有明确要求子代理，主线程在每个此类子任务
后执行所需的即时有界只读审计，并在最后执行一次父任务聚合审计。审计把每条 F1-F11
要求映射到当前状态的直接证据，并检查无关变更。

除非用户另行要求更广证明，本任务不运行 `ci-full`、`scripts/gates/release.sh`、
`test-all`、`smoke-all` 或整个 solution 的 `.slnx` 测试。

## 成功标准

只有满足以下全部条件，父任务才算完成：

- F1-F11 的所有负向场景都因预期原因失败；
- 所有正向自测和归属定向门禁通过；
- 真实安全回归对每个选择器都包含非零已执行证据；
- 真实主机 Native AOT 构建在两个干净根目录间可重复；
- 工作流结构证明每个上传归档都已先执行冒烟；
- 活跃扫描器均不依赖 ripgrep，也不隐藏扫描器故障；
- 已审计辅助脚本均不能杀死未验证进程、删除任意临时目录、拆分测试路径，或在测试
  失败后报告干净；
- 没有修改任何备份/参考文件；
- 最终 diff 只包含关闭 F1-F11 所需文件。
