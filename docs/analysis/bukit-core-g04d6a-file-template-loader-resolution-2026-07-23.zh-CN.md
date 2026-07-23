# Bukit Core G-04D6A FileTemplateLoader 受控收窄决议

> 日期：2026-07-23
>
> 范围：G3 Task 22；只处理 Bukit Core
>
> 资格依据：G-04D6 Task 21 eligibility audit
>
> 状态：implemented / group-verification-pending

## 1. 决议

G-04D6A 只把：

```text
Bukit.Rendering.Scriban.FileTemplateLoader
```

从 public sealed class 收窄为 internal sealed class。production diff 只有一个
accessibility token：

```diff
- public sealed class FileTemplateLoader : ITemplateLoader
+ internal sealed class FileTemplateLoader : ITemplateLoader
```

constructor、`GetPath`、`Load`、`LoadAsync`、Scriban `ITemplateLoader`、fallback、
cache、path safety 与异常实现均未修改。没有新增 `InternalsVisibleTo`，没有修改
Engine、Theme、Labs、插件、schema、协议、asset URL 或全局路径工具。

本任务是 2.0 source、binary 与 reflection breaking change。private、未索引或未声明的
consumer 继续为 `unknown-until-voluntary-declaration`；public `ScribanTemplateRenderer`
仍是 Rendering 的受支持入口。

## 2. 精确影响面

### Production

唯一 production 文件变化：

```text
src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs
```

同程序集调用图保持：

```text
ScribanTemplateRenderer
  -> new FileTemplateLoader(...)
  -> TemplateContext.TemplateLoader : Scriban.Runtime.ITemplateLoader
  -> GetPath / Load / LoadAsync
```

`TemplateContextBuilder`、component/section helper 和 renderer 的 private/internal
字段与参数继续直接使用该类型。Engine 不直接命名 loader；既有
`InternalsVisibleTo("Bukit.Engine")` 不因本任务新增或改变。Theme Core 也没有 direct
candidate consumer。

### Public contract

从 current baseline 删除一项：

```text
public sealed class Bukit.Rendering.Scriban.FileTemplateLoader
  : Scriban.Runtime.ITemplateLoader
```

没有新增替代 public type、obsolete facade、type forwarding 或自有 loader abstraction。
internal class 上的 public constructor/接口方法仍保持原 metadata shape，以满足 Scriban
interface implementation 和 owner tests，但该 concrete type 不再由
`Assembly.GetExportedTypes()` 导出。

## 3. CG-005 fallback 冻结

G-04D6A 不改变 compatibility-governance `CG-005`：

```text
override existing
  -> child/root existing
     -> parent/fallback existing
        -> child/root primary path when all missing
```

owner tests 原本对 child/parent 分支传入 `overrideDir: null`，没有证明“override 已配置但
目标缺失”仍继续完整链。本任务只收紧这些 fixtures：

- child test：配置空 override directory，child 与 parent 同时存在，断言 child；
- parent test：配置空 override directory，child 不存在、parent 存在，断言 parent；
- 新增 all-missing test：三层都配置但目标都不存在，断言返回 primary child path。

已有 override 命中、relative/absolute path、traversal、sync/async load、missing file、
unchanged cache 与 forward-slash tests 保持不变。

没有顺带处理：

- symlink/realpath；
- case-sensitive filesystem policy；
- 同 length/同 timestamp 内容变更；
- cache algorithm；
- cancellation；
- component/section 的相邻路径检查。

这些不是 visibility narrowing 的授权范围。

## 4. Architecture guard

新增：

```text
tests/Bukit.Architecture.Tests/G04D6AFileTemplateLoaderTests.cs
```

静态契约覆盖：

1. exact full name 仍存在；
2. type 是 internal、sealed、non-abstract，且不在 exported types；
3. exact Scriban `ITemplateLoader` interface 仍存在；
4. constructor 仍为 `(string, string? = null, string? = null)`；
5. `GetPath`、`Load`、`LoadAsync` 参数与返回 shape 不变；
6. 三个 interface target method 仍 public、virtual、final；
7. Rendering friend set 仍精确为 `Bukit.Engine` 与
   `Bukit.Rendering.Tests`；
8. current baseline 为 `14/487/61` 且不再包含 loader；
9. closed manifest 仍为 136 项，保留 loader 历史 candidate；
10. historical manifest Git blob 仍为
    `7b07d6890562387010b52301e9f8716e9bf10ed1`。

该 guard 不为 Architecture assembly 新增 friend，而是从 public
`ScribanTemplateRenderer` 定位 Rendering assembly，再以 exact full name 检查 internal
type。

## 5. Current baseline 与治理文字

修改前：

```text
14 assemblies / 488 public types / 62 candidates
```

修改后：

```text
14 assemblies / 487 public types / 61 candidates
```

`docs/governance/bukit-core-public-api-baseline.v1.json` 只删除 loader entry；其相邻
`ScribanModelBinder` 和 retained public `ScribanTemplateRenderer` entries 未修改。

所有现行 G-04 architecture current-count guards 同步到 `14/487/61`。这只是让它们继续
验证同一份 current baseline；各历史决议中的具体类型断言、closed manifest 136 项和
blob guard 均保持。

`docs/governance/bukit-core-2.0-consumer-declaration.md`：

- 将使用“current public API baseline”措辞的计数同步为 `487/61`；
- 新增 G-04D6A 单类型决议；
- 保留历史 cohort、各任务授权边界与 private-unknown 声明。

closed
`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` 没有修改。静态检查得到：

```text
candidateCount = 136
candidates.length = 136
FileTemplateLoader historical entries = 1
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

## 6. 未改变的行为与边界

以下都不是本任务的变化：

- override、child、parent 的优先级；
- all-missing 返回 primary path；
- relative slash normalization；
- root boundary comparison；
- path traversal 的 `InvalidOperationException`；
- missing file 返回 empty string；
- sync/async file reads；
- `LastWriteTimeUtc + Length` cache signature；
- case-insensitive cache key；
- unchanged content 返回同一 string instance；
- Scriban interface dispatch；
- public renderer constructors、render methods与模板输出；
- Engine/Theme dependency direction；
- public template model、template keys与 plugin contract。

## 7. 验证状态

Task 22 受 master plan 明确约束，不在本任务运行 tests、aggregate、Native AOT 或独立
review。因此当前只完成静态证据：

| 检查 | 结果 |
|---|---|
| production diff | 仅 type accessibility 单 token |
| current baseline JSON parse/count | `14 assemblies / 487 types / 61 candidates` |
| loader current baseline entry | 0 |
| historical manifest count | `136 / 136` |
| historical loader entry | 1 |
| historical blob | 精确不变 |
| G-04 current-count hardcodes | 已同步为 `487/61` |
| tests / aggregate / AOT / review | **未运行；Task 30 pending** |

本报告中的 `implemented` 只表示受控 diff 已形成，不表示 G3 已关闭。

## 8. Task 30 待验证集合

Task 30 必须至少验证：

- `Bukit.Rendering.Tests`；
- `Bukit.Theme.Tests`；
- `Bukit.Engine.Tests`；
- `Bukit.Cli.Tests`；
- `Bukit.Architecture.Tests`；
- public API drift；
- G3 唯一 aggregate targeted gate；
- real Native AOT package/smoke；
- 一次独立轻量只读复审。

与 D6A 直接相关的验收：

1. owner fallback matrix 全通过；
2. internal type 的 Scriban interface map 与 constructor/method shape 通过；
3. public renderer include/component/section path 仍能真实 dispatch；
4. current baseline 精确为本任务后的阶段值；
5. closed 136 manifest 与 blob 不变；
6. AOT 不出现 missing type/method、trimmer warning 或 interface dispatch 回归。

后续 Task 23、26、29 会继续改变 G3 current baseline；Task 30 应以 Task 29 后最终值验证，
不得把本报告的阶段 `487/61` 错当成 G3 最终计数。

## 9. 停止条件复核

本次静态实施没有触发 Task 21 的停止条件：

- 没有发现 Core public signature 传播；
- 没有新增 friend；
- 没有改变 Scriban interface、constructor 或 methods；
- 没有改变 fallback、path、cache 或 exceptions；
- 没有修改 Labs/插件；
- 没有改 historical manifest。

若 Task 30 首次发现候选相关编译、runtime、fallback 或 AOT 回归，G-04D6A 必须回到
独立问题复审；不能通过扩大 public surface、修改相邻路径逻辑或禁用 trimming 超限修复。
