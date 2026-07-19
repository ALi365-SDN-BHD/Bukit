# Bukit Core 八项关闭后的 RC 资格验证报告

> 验证日期：2026-07-19
>
> 基线：`main@9ae3283effcefa1843f0fc7a85ff51f9b170cd9d`
>
> 验证分支：`codex/post-closure-rc-qualification`
>
> 主机证明范围：macOS arm64、.NET SDK 10.0.100、Release
>
> 最终裁决：**PASS——RC-04 已关闭，当前分支具备 Release Candidate 技术资格**

## 1. 执行摘要

八项原审计 finding 的关闭状态没有回归，继续保持 **8/8 已关闭**。本轮在关闭点之后完成了 delta 审计、Core 广泛回归、安全、覆盖率、依赖审计、架构契约和真实 Native AOT/归档 smoke 验证。

验证发现四个独立于 F-01～F-08 的现行问题，现均已用受控变更关闭。

| 编号 | 问题 | 级别 | 状态 |
|---|---|---:|---|
| RC-01 | 安全文档中的点号代码 token 被配置契约扫描器识别为不存在的 `site.yaml` 字段 | P2 | 已关闭 |
| RC-02 | 归档 smoke 把绝对路径传给只接受相对 `build.output` 的 Core，并在自定义 config 时错误推导 audit 输出根 | P1 | 已关闭 |
| RC-03 | `Bukit intentionally` 被架构契约作为非 Core 命令 `bukit intent` 的子串命中 | P2 | 已关闭 |
| RC-04 | YamlDotNet 静态生成器在干净编译时生成随机 GUID，导致 Native AOT 主二进制不可字节复现 | P1 / Release blocker | **已关闭** |

当前结论是 RC 技术资格通过，不等同于已经创建 tag、上传资产或发布 GitHub Release。真实双次 clean Native AOT、最终归档 smoke 和 release asset prepare/verify 均已通过。

## 2. 范围与执行纪律

- 审计增量为原关闭点 `5808d9a6` 至当前基线 `9ae3283e`，并对 `9ae3283e` 执行 RC 资格验证。
- 未修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/` 或 `scripts-0.2/`。
- 未修改 Core 公共 API、配置 schema、插件协议、持久化格式或运行时业务逻辑。
- 使用真实 NuGet 审计；本轮没有设置 `NuGetAudit=false`，也没有发现 NU190x。
- 涉及 repository script 的测试移除了 `NOTION_TOKEN`，避免真实外部状态进入验证。
- 首次真实可复现构建失败后，按失败规则停止后续步骤；RC-04 独立修复通过定向门禁和只读复审后，才重新执行真实双构建、归档 smoke 与 release asset prepare/verify。

## 3. 八项关闭后的 delta 复核

`5808d9a6..9ae3283e` 的运行时增量只涉及媒体排队取消与 localizer 预取消：

- 相关 Content 测试 71/71 通过；
- 严格竞态测试重复 30/30 通过；
- 四个相关生产/测试路径的 `post-change-targeted.sh` 通过，`Bukit.Content.Tests` 715/715；
- 独立只读复审结论为 `Ready`，Critical 0、Important 0；
- 未发现 F-01～F-08 的根因、公共契约或关闭证据回归。

完整 delta 证据已附加到[八项最终 aggregate 关闭台账](bukit-core-eight-findings-final-aggregate-closure-audit-2026-07-19.zh-CN.md)。

## 4. RC 广泛验证结果

### 4.1 Core、架构与安全

| 验证 | 结果 |
|---|---:|
| `bash scripts/gates/ci-full.sh Release` | 3,872/3,872 通过 |
| `bash scripts/security/security-regression-self-test.sh` | 通过 |
| `bash scripts/security/security-regression.sh Release` | 297/297 通过；TRX 集合验证通过 |
| `Bukit.Architecture.Tests` | 修复 RC-03 后 77/77 通过 |
| `dotnet list bukit-core.slnx package --vulnerable --include-transitive` | 12 个 Core 项目均无已知易受攻击包 |

`ci-full` 的 3,872 项分布为：CLI 568、Config 234、Content 715、Engine.Abstractions 60、Engine 1,557、Plugin.Abstractions 8、PluginHost 168、Rendering 164、Routing 23、Shared 309、Theme 66。

### 4.2 覆盖率

`coverage-baseline-schema.sh` 的 baseline/schema 变异自测通过；`coverage.sh Release` 通过。Core 总行覆盖率为 **87.03%（29,325 / 33,696）**。

| 项目 | 行覆盖率 |
|---|---:|
| Bukit.Cli | 82.94% |
| Bukit.Cli.Shared | 95.03% |
| Bukit.Config | 89.40% |
| Bukit.Content | 94.31% |
| Bukit.Engine | 86.08% |
| Bukit.Engine.Abstractions | 93.50% |
| Bukit.Plugin.Abstractions | 89.95% |
| Bukit.PluginHost | 91.04% |
| Bukit.Rendering | 83.72% |
| Bukit.Routing | 93.83% |
| Bukit.Shared | 92.92% |
| Bukit.Theme | 73.99% |

### 4.3 RC-04 修复前的 Native AOT、归档与脚本契约

- `native-aot.sh 1.0.7-rc-audit osx-arm64 ... Release`：真实发布成功；
- 最终 tar.gz 的真实 `release-artifacts.sh` smoke：通过，构建 2 条 route，publish audit errors=0；
- `native-aot-self-test.sh`：通过；
- `build-repro-self-test.sh`：通过；
- `release-assets-self-test.sh`：通过；
- `release-artifacts-self-test.sh`：59/59 通过，并从现行入口执行新增的 `core-self-test.sh`；
- `build-repro.sh 1.0.7-rc-audit osx-arm64 Release`：**失败，形成 RC-04 的 RED 证据**。

真实归档 smoke 的 22 个 warning 来自 smoke fixture 的非 strict 内容质量提示；没有 error，命令 exit 0。它们不是 RC-04 的原因。

### 4.4 RC-04 修复后的验收

| 验证 | 结果 |
|---|---:|
| normalizer self-test | 通过；随机变体、幂等、尾随空白、未知标识符、歧义 base、中央版本缺失/重复/空值均覆盖 |
| `yaml-static-context.sh check` | 通过；上游重新生成与签入源码 byte-for-byte 一致，默认构建不加载 analyzer |
| `Bukit.Theme.Tests` | 69/69 通过，其中 static context 反序列化、序列化、public surface 3/3 |
| `Bukit.Cli.Tests` | 568/568 通过 |
| `Bukit.Config.Tests` | 234/234 通过 |
| `Bukit.Architecture.Tests` | 77/77 通过 |
| `native-aot-self-test.sh` / `build-repro-self-test.sh` | 均通过 |
| `build-repro.sh 1.0.7-rc-audit osx-arm64 Release` | 通过；两棵独立 clean publish tree 完整 SHA-256 一致 |
| 真实 osx-arm64 tar.gz smoke | 通过；2 routes，0 errors，22 fixture warnings |
| `release-artifacts-self-test.sh` | 59/59 通过，且 `core-self-test.sh` 通过 |
| release assets prepare/verify | osx-arm64 精确资产集通过 |

Task 2 独立高风险复审首次发现 generator provenance 与中央版本脱节，修复并重跑门禁后第二次裁决为 `Ready`，Critical、Important、Minor 均为 0。

## 5. 已关闭的新问题

### 5.1 RC-01：文档 token 与配置路径扫描器冲突

**触发。** 首次 `ci-full` 在 `ConfigContractDriftTests.ConfigDocs_DoNotReferenceUnknownSiteYamlFields` 失败，报告五个不存在的配置路径：`summary.warningCount`、`summary.errorCount`、`document.write`、`FileAttributes.ReparsePoint`、`ImageAssetLocalizer.LocalizeAsync`。

**根因。** 测试按现行契约把反引号中的点号 token 作为配置路径扫描；文档把 JSON 字段和 C# 成员也写成了同一语法。

**受控修复。** JSON 字段改成 JSON Pointer `/summary/warningCount` 和 `/summary/errorCount`；C# 表述改成 `document.write()`、`ReparsePoint` flag from `FileAttributes` 和自然语言方法调用。未放宽扫描器，未加入例外。

**验证。** 精确测试 1/1、`Bukit.Config.Tests` 234/234、文档定向门禁均通过；独立复审在一处语法建议修正后为 `Ready`。

### 5.2 RC-02：release archive smoke 的输出路径契约漂移

**触发。** 真实 AOT archive 的 smoke 在 config check 后失败：`build.output must be a relative path.`

**根因。** `release-artifacts.sh` 向 `core.sh` 传绝对 scratch output；现行 Core 按安全契约只接受相对 `build.output`。进一步复核发现，自定义 `BUKIT_SMOKE_CONFIG` 不在默认 fixture 根时，build 与 audit 还会落到不同根目录。原 fake smoke 只检查命令顺序，没有验证这两个语义。

**受控修复。** build 固定接收相对 `dist`；audit 路径按 config 所在根解析，并保留 `sites/<name>/site.yaml` 的现行特例。新增 35 行 `core-self-test.sh`，由 200 行以内的 `release-artifacts-self-test.sh` 入口执行。

**验证。** 回归测试经历 52/59、59/60 的 RED 阶段后达到 59/59；真实 tar.gz smoke、四路径 `post-change-targeted.sh` 通过；独立高风险复审为 `Ready`，Critical/Important/Minor 均为 0。

### 5.3 RC-03：Core 命令边界的子串命中

**触发。** 首次独立执行 Architecture Tests 为 76/77；失败行是 `Use a dedicated ... Bukit intentionally ...`。

**根因。** 大小写不敏感的禁止词 `bukit intent` 是 `Bukit intentionally` 的前缀。

**受控修复。** 只把文档改成 `The clean operation refuses ...`。未改测试、禁止词或 Core 命令边界。

**验证。** 精确测试 1/1、Architecture Tests 77/77、文档 `post-change-targeted.sh` 通过。

## 6. RC-04 根因与关闭实现

### 6.1 可复现证据

第一次真实 `build-repro.sh` 的两个发布树集合相同，但 `bukit` 的 SHA-256 不同。立即复跑在共享中间产物已预热后通过，证明原脚本没有兑现文档所称的“两次 clean publish”，可能以增量复用制造假阳性。

为排除该变量，`package-native-aot.sh` 现使用 output root 下由 `mktemp` 创建的独立 `--artifacts-path`，退出时清理，并通过 PathMap 把源码和构建根分别映射到 `/_/src` 与 `/_/build`。注入式自测和定向门禁均通过。

隔离后的两次真实 clean publish 稳定失败，差异固定为：

```text
changed=['Bukit.Engine.pdb', 'Bukit.Rendering.pdb', 'Bukit.Theme.pdb', 'bukit']
```

即使让两次构建复用完全相同的物理 artifacts 路径并在每次前清理，差异集合仍相同，因此不是随机临时路径泄漏。

### 6.2 根因

启用 `EmitCompilerGeneratedFiles=true` 后，两次生成的唯一业务源码差异位于：

```text
YamlDotNet.Analyzers.StaticGenerator.TypeFactoryGenerator/
YamlDotNetAutoGraph.g.cs
```

`Vecc.YamlDotNet.Analyzers.StaticGenerator 16.3.0` 为每个 accessor 名调用 `Guid.NewGuid()`，例如同一类型在两次构建中分别生成：

```text
Bukit_Theme_ThemeManifestV2_e7407d87935b40e18a913cb1c088b8ef
Bukit_Theme_ThemeManifestV2_d6df2124b1924db1bc66f45a763d5bf0
```

因此 `Bukit.Theme.dll`/PDB 的内容与 MVID 改变，随后经 `Theme → Rendering → Engine → Bukit.Cli Native AOT` 级联。最终两个 Mach-O 大小相同，但 UUID、code signature hash 和 SHA-256 不同。

截至检索日，官方 NuGet 上当前静态生成器为 [18.0.0](https://www.nuget.org/packages/Vecc.YamlDotNet.Analyzers.StaticGenerator/)，下载后的 analyzer binary 仍包含 `NewGuid`/`GuidSuffix`，所以不能把依赖升级当作已经验证的修复。

### 6.3 方案选择

独立设计复核比较了以下方案：

1. 上游或自维护 generator 用类型身份的稳定 hash 替代随机 GUID；
2. 受治理地签入规范化后的静态上下文，并建立重新生成/漂移门禁；
3. 重新设计 YAML AOT 静态上下文，但保持现有 public type 和 Native AOT 行为。

最终选择方案 2。它保留当前 16.3.0 generator 的完整输出与 public/AOT 能力，只把随机 accessor suffix 规范化；相比维护约 74 KB 的 analyzer fork，变更面和长期同步风险更小。没有升级 YamlDotNet，也没有删除 static context、忽略 PDB、复用缓存、放宽 publish tree comparer或修改编译后二进制。

### 6.4 受控关闭实现

- 默认 `Bukit.Theme` 编译签入的 `ThemeManifestYamlStaticContext.Generated.cs`，不加载随机 generator；`Bukit.Cli` 删除未使用的 analyzer 引用。
- 只有 `yaml-static-context.sh check|update` 显式开启 generator，并排除签入文件；生成目录和 build artifacts 使用独立临时根，要求恰好一个 `YamlDotNetAutoGraph.g.cs`。
- normalizer 只接受实际声明为 `IObjectAccessor` 的 GUID-suffix class 及其闭合引用；使用 accessor 完整 base 的 UTF-8 SHA-256 前 32 个十六进制字符。未知 GUID 标识符、同 base 多 GUID、hash collision、NUL 和无 accessor 均 fail closed。
- generator 版本从 `Directory.Packages.props` 唯一解析并写入 provenance；缺失、重复、空值或无效 XML 均在写入前失败。输出固定 UTF-8/LF、移除上游尾随空白并以同目录临时文件原子替换。
- `ci-fast` 必经执行 normalizer self-test、治理 self-test 和真实 drift check；`release.sh` 与 CI workflow 均经过 `ci-fast`。
- `ThemeManifestYamlStaticContext` 的 public 构造、overrides、known types 和既有 public `StaticTypeInspector` 保持，并用静态 serializer/deserializer 直接验证。

### 6.5 兼容性与边界复核

两次上游生成源码均有 19 个 accessor class、57 次引用。独立比较确认签入文件只增加固定 provenance、稳定 suffix、LF/尾随空白规范化，未改变其他生成 token。YAML 配置语义、theme manifest schema、插件协议、路由、渲染和持久化格式均未修改。

## 7. RC 判定与后续动作

### 7.1 判定

- F-01～F-08：继续 8/8 关闭；
- Core 功能回归、安全、覆盖、依赖、架构与真实归档 smoke：通过；
- Native AOT 字节级可复现构建：通过；
- 真实最终归档 smoke：通过；
- release asset prepare/verify：通过；
- 当前 RC 技术资格：**PASS**。

### 7.2 发布边界与后续动作

本报告批准的是当前分支的 RC 技术资格，不执行 tag、push、GitHub Release 或生产发布。后续若要正式签发 RC，应在合并后的目标提交上按仓库发布流程重新生成对应版本和 commit 的资产；不能复用本报告中的 `1.0.7-rc-audit` 验证资产。
