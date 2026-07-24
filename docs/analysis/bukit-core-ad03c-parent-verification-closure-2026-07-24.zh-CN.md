# Bukit Core AD-03C 父任务最终验证关闭台账

> 日期：2026-07-24
>
> 父基线：`e16142331111060a09385fb29fdf72c28da260c4`
>
> 已验证代码 HEAD：`52e38b4ca4cfe29c92a83a9b2db33efa682ff9af`
>
> 已验证 Git tree：`6ddf39d9753be762276d87a97675af3dc15d38ca`
>
> 最终组合范围：68 个 tracked 路径
>
> 范围：Bukit Core 与其契约门禁；Labs 和外部插件业务实现不在验证范围
>
> 状态：父任务 aggregate/full/release、安全、覆盖率与复审证据 closed

## 1. 关闭结论

AD-03C C6 台账明确把 direct-owner proof 与父任务最终 aggregate/full 验证分开。
后续 Config 文档引用分类器和 security selector ownership 两项既有门禁缺陷完成受控
修复后，用户明确授权执行一次最终 replacement aggregate，并补齐 exact-entrypoint
full/release、安全、覆盖率与最终复审证据。

最终验证绑定未再修改的代码树
`52e38b4ca4cfe29c92a83a9b2db33efa682ff9af`。本台账是 evidence-only 记录，不修改
Core runtime、公共 API、schema、插件协议、持久化格式、路径工具、Labs 或外部插件。

关闭结果：

- 最终 68 路径 replacement aggregate：exit 0，只执行一次；
- `ci-full.sh Release`：exit 0，Core 13 项目 4185/4185；
- `release.sh Release`：exit 0；
- security regression：302/302，六个项目 TRX 全部 fail-closed 验证通过；
- coverage：13/13 测试项目、4185/4185，overall 88.47%，全部 Core 程序集高于 70%；
- `git diff --check`、public API drift、格式/分析器、文档与静态上下文合同：通过；
- 分段独立只读复审链：C0-C6、CFG、SEC 与最终 post-closure diff 均为 CLEAN。

因此，[AD-03C 最终汇总关闭台账](bukit-core-ad03c-final-aggregate-closure-2026-07-24.zh-CN.md)
第 6 节保留的 C6 历史边界仍然真实，但“父任务证据另行追加”的后续动作已经完成。

## 2. 冻结范围与 replacement 授权

最终组合差异由两段构成：

| range | tracked paths | 内容 |
|---|---:|---|
| `e1614233..bb160b41` | 63 | AD-03C0-C6 实现、合同、迁移与关闭台账 |
| `bb160b41..52e38b4c` | 5 | Config 文档引用分类器、security selector ownership 与证据 |
| `e1614233..52e38b4c` | 68 | 最终组合验证范围 |

后 5 个 tracked 修复使较早冻结 HEAD 的父级证据不再足以独立代表最终组合。用户因此
明确授权一次 replacement aggregate。执行前验证：

- `git rev-parse HEAD` 精确为 `52e38b4c...`；
- `git diff --name-only e1614233..HEAD | wc -l` 精确为 68；
- 68 路径有序清单 SHA-256：
  `1dbb39029a78f04aaae76f254489a386b7a4fa9bb41d8cf59e6685cc6b03ba57`；
- `git diff --check e1614233..HEAD`：exit 0；
- worktree tracked 状态干净；
- `NOTION_TOKEN` 仅从测试子进程环境移除，未读取或输出其值。

## 3. 唯一 replacement aggregate

执行命令等价于：

```bash
base=e16142331111060a09385fb29fdf72c28da260c4
paths=(<git diff --name-only "$base"..HEAD 返回的全部 68 个路径>)
env -u NOTION_TOKEN bash scripts/checks/post-change-targeted.sh \
  --configuration Release \
  --base "$base" \
  -- "${paths[@]}"
```

结果：exit 0。该 replacement aggregate 没有重复执行。

Focused owner selection 实际通过：

| project | passed | failed | skipped |
|---|---:|---:|---:|
| `Bukit.Content.Tests` | 456 | 0 | 0 |
| `Bukit.Shared.Tests` | 299 | 0 | 0 |
| `Bukit.Architecture.Tests` | 278 | 0 | 0 |
| `Bukit.Config.Tests` | 292 | 0 | 0 |
| `Bukit.Engine.Tests` | 1628 | 0 | 0 |
| `Bukit.Notion.Tests` | 376 | 0 | 0 |
| **total** | **3329** | **0** | **0** |

同一次命令随后完整通过：

- security regression self-test，六个项目伪 TRX 所有权与 selector 校验均通过；
- `ci-fast` 全部门禁；
- docs consistency 与 public documentation contracts；
- active workflow、focused/targeted、portability 与 brainstorm server self-tests；
- `dotnet format`；
- code-analysis style/analyzer ratchets；
- public API drift self-test/check，0 drift；
- YAML static context deterministic check；
- `git diff --check`。

## 4. Full 与 release exact-entrypoint

### 4.1 `ci-full`

命令：

```bash
env -u NOTION_TOKEN bash scripts/gates/ci-full.sh Release
```

结果：exit 0。`ci-fast` 和 13 个 Core test projects 均完整通过：

| project | passed |
|---|---:|
| `Bukit.Cli.Tests` | 618 |
| `Bukit.Config.Tests` | 292 |
| `Bukit.Content.Tests` | 456 |
| `Bukit.Content.Notion.Tests` | 6 |
| `Bukit.Engine.Abstractions.Tests` | 61 |
| `Bukit.Engine.Tests` | 1628 |
| `Bukit.Notion.Tests` | 376 |
| `Bukit.Plugin.Abstractions.Tests` | 8 |
| `Bukit.PluginHost.Tests` | 171 |
| `Bukit.Rendering.Tests` | 169 |
| `Bukit.Routing.Tests` | 27 |
| `Bukit.Shared.Tests` | 299 |
| `Bukit.Theme.Tests` | 74 |
| **total** | **4185** |

所有项目均为 0 failed、0 skipped。

### 4.2 `release`

命令：

```bash
env -u NOTION_TOKEN bash scripts/gates/release.sh Release
```

结果：exit 0。当前 `release.sh` 是显式 thin gate，只执行 `ci-fast`，并输出：

```text
Release gate here is intentionally thin; run release artifact validation explicitly when publishing binaries.
```

本台账只证明 release contract entrypoint 已执行，不宣称完成 RID 发布、压缩包、签名、
校验和、安装或真实平台制品验证。

## 5. Security

命令：

```bash
env -u NOTION_TOKEN bash scripts/security/security-regression.sh Release
```

结果：exit 0。每个 selector 都产生并通过 TRX 验证：

| project | passed |
|---|---:|
| `Bukit.Cli.Tests` | 4 |
| `Bukit.Content.Tests` | 36 |
| `Bukit.Notion.Tests` | 86 |
| `Bukit.Engine.Tests` | 65 |
| `Bukit.PluginHost.Tests` | 103 |
| `Bukit.Routing.Tests` | 8 |
| **total** | **302** |

未弱化 filter、selector 或 `verify-trx.py` 的 fail-closed 语义。

## 6. Coverage

在执行真实 coverage 前，以下 owner self-tests 全部通过：

- output-path self-test；
- project-list self-test；
- matrix self-test；
- summarizer self-test；
- coverage policy schema check。

命令：

```bash
env -u NOTION_TOKEN bash scripts/checks/coverage.sh Release
```

结果：

- exit 0；
- 13/13 测试项目通过，共 4185/4185；
- 生成 13 个 Cobertura 文件；
- overall 88.47%（31417/35510），超过 84% overall 门槛；
- 14 个 Core 程序集全部高于 70% floor。

| assembly | line coverage |
|---|---:|
| `Bukit.Cli` | 85.44% |
| `Bukit.Cli.Shared` | 94.75% |
| `Bukit.Config` | 90.12% |
| `Bukit.Content` | 94.31% |
| `Bukit.Content.Notion` | 92.84% |
| `Bukit.Engine` | 87.18% |
| `Bukit.Engine.Abstractions` | 93.66% |
| `Bukit.Notion` | 95.63% |
| `Bukit.Plugin.Abstractions` | 92.46% |
| `Bukit.PluginHost` | 91.20% |
| `Bukit.Rendering` | 83.81% |
| `Bukit.Routing` | 93.81% |
| `Bukit.Shared` | 96.00% |
| `Bukit.Theme` | 84.43% |

`Bukit.Cli.Shared` 由 CLI coverage 文件共同统计，因此 13 个测试项目对应 14 个
Core assembly 汇总是预期行为。

Coverage 产物完整性摘要：

| artifact | SHA-256 |
|---|---|
| `coverage-summary.txt` | `c92db3e1639a9032ec0c28c5de61b3a7735e9bfa5b4e00f1e311ff58c578779d` |
| `coverage-summary.json` | `79a221bea57c17e0e9fbe5c3e6386e14cabe8f1235ae5f2aefe4fd878b02a679` |
| `coverage-files.txt` | `c436c78cf6b229c5d94d2d20dd283806e0971897127a320c54761441a3e6e6be` |

## 7. 独立复审证据边界

最终组合 diff 的独立审查采用可追溯的分段链，而不是声称一次 reviewer 重新读取全部
历史：

1. `e1614233..bb160b41` 的 AD-03C0-C6 每项均完成独立只读复审；C6 最终
   architecture review 与 evidence review 均为 APPROVED / CLEAN。
2. `bb160b41..52e38b4c` 的 CFG 与 SEC 分别完成独立复审；SEC 第二轮为
   Spec APPROVED / Code Quality CLEAN。
3. 同一后段最终 aggregate reviewer 对 5 文件 diff、Core/Architecture/security/
   coverage evidence 与范围边界给出 CLEAN，并判定 parent verification debt 可关闭。
4. 本次 68 路径 replacement aggregate 在未改变已复审代码树的前提下重新证明组合
   owner routing 与所有 fast contracts。

上述链不把 segmented review 伪装为新的单体 reviewer，也不覆盖 private、unindexed、
binary-only、reflection、serializer、external subclass 或 undisclosed consumer 风险。
这些不可知风险继续由 2.0 migration contract 管理。

## 8. 最终边界

本次关闭的是 AD-03C 父任务的验证和正式证据债务。它不：

- 修改或重新解释 AD-03C 的 public-surface 决策；
- 扩大 Core、Labs 或插件产品范围；
- 修改 runtime、API、schema、协议、资产 URL 或持久化格式；
- 把 release thin gate 表述为真实发布制品验证；
- 宣称不存在未公开的外部消费者。

后续若修改 `52e38b4c` 所代表的 Core/runtime/contract tree，必须按新任务范围重新选择
owner checks；本台账不能作为未来代码变更的永久通行证。
