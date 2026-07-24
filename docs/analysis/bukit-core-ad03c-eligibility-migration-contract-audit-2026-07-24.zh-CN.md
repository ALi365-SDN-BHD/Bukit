# Bukit Core AD-03C 2.0 Notion 兼容清偿资格与迁移合同审计

> 日期：2026-07-24
>
> 基线：`2.0@e16142331111060a09385fb29fdf72c28da260c4`
>
> 范围：Bukit Core；Labs 与外部插件业务实现不在实施范围
>
> 状态：C0 read-only audit complete / C1-C6 complete

## 1. 执行结论

AD-03C 是 2.0 的 Notion 兼容层清偿机会，不是未修复的 P0/P1
正确性、安全性或数据损坏缺陷，也不是未完成的 G-04 工作。原始 AD-03
关注的是重型 Notion 转换实现位于 Shared 的所有权问题；现行实现已经把转换、传输和
内容适配职责迁入 canonical owner，当前剩余的是 legacy CLR identity、兼容项目引用和
测试过渡 helper。

本次 C0 在不修改代码、测试、公共 API 基线或历史台账的前提下，确认：

- 当前 legacy public CLR inventory 精确为 **19**：
  `Bukit.Shared.Notion` 15 项，以及由 `Bukit.Content.dll` 导出的
  `Bukit.Content.Notion` 4 项；
- 与这些 facade 直接相关的兼容项目引用精确为
  `Bukit.Shared -> Bukit.Notion`、`Bukit.Content -> Bukit.Notion` 和
  `Bukit.Content -> Bukit.Content.Notion`；
- 两个 production-assembly internal helper 当前只有测试消费者；
- Shared converter、13 个 legacy model identity 与 mapper 必须原子删除；
  Shared URL facade 可以独立删除；
- Content API client/provider/options 有真实 Engine 生产消费者，只能在保留当前
  bridge 和依赖方向的前提下原子 internalize；
- `NotionPropertyParser` 没有生产消费者，但也没有等价的 public canonical replacement；
  C5 默认 retain-by-design，除非后续证据推翻该结论或另行批准 public SDK 决策。

因此，C1-C6 的顺序和原子边界在本审计中冻结。C1-C5 完成后预期 legacy public
inventory 从 19 项降为 1 项：仅保留 `Bukit.Content.Notion.NotionPropertyParser`。
这项计算不授权任何实现；每项仍必须分别满足下文 entry/exit criteria。

## 2. 审计口径与不可越界范围

### 2.1 证据口径

本审计使用四类证据：

1. 当前基线的源码、项目引用和 Architecture tests；
2. 当前 active governance 与 G-04 已关闭台账；
3. Core、Labs、仓库内插件及三个已知本地外部仓库的文本/项目引用检查；
4. 2026-07-24 对两个 namespace、converter 和 provider options 的公开 GitHub
   exact web search 记录。

第 3、4 类只能证明“在已检查范围没有发现 direct current-Core CLR consumer”，不能
证明消费者不存在。公开 GitHub 检查是公共网页搜索，不是 authenticated GitHub Code
Search；private、未索引、binary-only、reflection、serializer type-name、未披露的
外部派生类等消费者仍不可知。

### 2.2 明确不在 AD-03C 内

- 不修改 canonical `Bukit.Notion` 的 62 个 public types；
- 不修改 canonical `Bukit.Content.Notion` 的
  `NotionContentSource`、`NotionContentSourceOptions` 或其 public surface；
- 不改变 Notion API、TLS、HTTP retry、caching、config schema；
- 不改变 plugin protocol、assets、SEO 或 global path tooling；
- 不实施 Labs 或外部插件业务迁移；
- 不借 AD-03C 重开 AD-01、AD-05 或已关闭的 G-04；
- 不改写 immutable 136-entry historical candidate manifest 或 G-04 historical
  ledgers。

现行治理明确区分 CLR `public` 与受支持的通用 SDK 承诺，并要求 2.0 access narrowing
经过独立审查；见
[Public API Governance](../../guide/dev/public-api-governance.md#legacy-notion-facade-freeze)。
G-04 已以 443 public types / 0 candidates 关闭；“0 candidates”表示候选均已获终态，
不是这些兼容 facade 已不存在，见
[G-04 最终关闭审计](bukit-core-g04-final-aggregate-closure-audit-2026-07-24.zh-CN.md)。

## 3. 当前 19 项 public inventory

Architecture contract 同时验证原 assembly identity 与 namespace exact set；见
[`NotionBoundaryTests`](../../tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs#L242-L267)
及其
[exact arrays](../../tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs#L315-L340)。

### 3.1 `Bukit.Shared.Notion`：15 项

| 类别 | CLR identity | 当前角色 | C0 结论 |
|---|---|---|---|
| model | `Bukit.Shared.Notion.NotionBlock` | legacy abstract base record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.Heading1Block` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.Heading2Block` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.Heading3Block` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.ParagraphBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.BulletedListItemBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.NumberedListItemBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.QuoteBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.ImageBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.ToggleBlock` | recursive legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.CodeBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.CalloutBlock` | legacy block record | 与 converter 原子删除 |
| model | `Bukit.Shared.Notion.RichTextSegment` | legacy rich-text record | 与 converter 原子删除 |
| converter | `Bukit.Shared.Notion.HtmlToNotionBlockConverter` | canonical converter facade + legacy mapping | 与 13 models、mapper 原子删除 |
| URL facade | `Bukit.Shared.Notion.NotionApiUrls` | canonical URL owner facade | 可独立删除 |

13 个 model identity 在 C0 基线的源码定义见
[`NotionBlockTypes.cs`](https://github.com/ALi365-SDN-BHD/Bukit/blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Shared/Notion/NotionBlockTypes.cs)。
converter 的 `ToBlocksJson` 已直接委托 canonical converter；`Convert` 则把 canonical
block 重新映射成 legacy records，见
[`HtmlToNotionBlockConverter.cs`](https://github.com/ALi365-SDN-BHD/Bukit/blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs)
和
[`NotionCompatibilityMapper.cs`](https://github.com/ALi365-SDN-BHD/Bukit/blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Shared/Notion/NotionCompatibilityMapper.cs)。

这里不能逐个 internalize model：

```text
HtmlToNotionBlockConverter.Convert
  -> List<Bukit.Shared.Notion.NotionBlock>
     -> concrete legacy block records
     -> RichTextSegment
     -> ToggleBlock.Children -> List<NotionBlock>
```

public return signature 与递归 graph 共同构成一个 source/binary contract。既有 G-04D4A
retain-by-design 决议也明确把 13 项视为 converter 的必要 companion graph；见
[G-04D4A graph resolution](bukit-core-g04d4a-shared-notion-graph-resolution-2026-07-23.zh-CN.md#2-十三项-model-graphretain-by-design)。
AD-03C3 是新的 2.0 原子清偿决议，不追溯改写该历史结论。

`NotionApiUrls` 只转发 canonical constants 和 URL builders，不参与上述 model graph，
见 C0 基线的
[`NotionApiUrls.cs`](https://github.com/ALi365-SDN-BHD/Bukit/blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Shared/Notion/NotionApiUrls.cs)；
因此它属于独立 C2。

### 3.2 `Bukit.Content.Notion` in `Bukit.Content.dll`：4 项

| CLR identity | 当前生产消费者 | replacement/处置 |
|---|---|---|
| `Bukit.Content.Notion.NotionApiClient` | taxonomy、pages-index、default fetcher | C4 原子 internalize；继续 bridge canonical transport |
| `Bukit.Content.Notion.NotionContentProvider` | Engine content provider factory | C4 原子 internalize；继续 bridge canonical content source |
| `Bukit.Content.Notion.NotionProviderOptions` | content source、taxonomy、pages-index | C4 原子 internalize；继续 bridge canonical options |
| `Bukit.Content.Notion.NotionPropertyParser` | 无 production consumer | C5 retain-by-design；无等价 public canonical replacement |

这些类型的 namespace 与 canonical `Bukit.Content.Notion` 项目相同，但 assembly identity
不同：legacy 四项位于 `Bukit.Content.dll`；canonical project 仍只导出其自身受保护的
public surface。Architecture test 对原 assembly resolution 有显式断言，见
[`LegacyNotionTypes_MustResolveFromOriginalAssemblies`](../../tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs#L241-L252)。

Engine 的现实消费者包括：

- `ContentProviderFactory` 构造 `NotionContentProvider` 与
  `NotionProviderOptions`；
- `TaxonomyTermsInjector` 构造 `NotionProviderOptions` 与 `NotionApiClient`；
- `PagesIndexPlugin`、`DefaultNotionPageFetcher` 使用
  `NotionApiClient`，pages-index 同时构造 options。

这三个 legacy 类型互相出现在构造、factory、interface method 和 call graph 中，不能
分次 internalize。`NotionPropertyParser` 未出现在 Core、Labs 或仓库内插件 production
调用中；当前直接调用来自 tests，但 canonical project 没有 public 等价 parser，
所以“没有 production consumer”不能自动转化为删除许可。

## 4. 兼容引用与测试专用 helper

### 4.1 三条 compatibility reference

| 引用 | 当前原因 | 计划终态 |
|---|---|---|
| `Bukit.Shared -> Bukit.Notion` | Shared converter、URL facade 和 internal writer 委托 canonical owner | C3 完成后删除 |
| `Bukit.Content -> Bukit.Notion` | legacy Content bridge 使用 canonical Notion transport/queries | C4 后保留 |
| `Bukit.Content -> Bukit.Content.Notion` | legacy provider bridge 到 canonical content adapter | C4 后保留 |

`Bukit.Shared` 的 exact reference 由 Architecture test 固定，见
[`Shared_MayReferenceNotion_OnlyForOneXCompatibility`](../../tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs#L59-L76)；
项目文件见
[`Bukit.Shared.csproj`](../../src/Bukit-Core/Bukit.Shared/Bukit.Shared.csproj)。
`Bukit.Content` 的两条 Notion reference 见
[`Bukit.Content.csproj`](../../src/Bukit-Core/Bukit.Content/Bukit.Content.csproj)。

C4 只收窄三个 legacy CLR types 的可见性，不把 Engine 改成直接依赖 canonical adapter。
现行 Architecture contract 明确要求：

- Engine 不引用 `Bukit.Content.Notion` 项目；
- canonical adapter 不把 internals friendship 给 Engine 或 Engine tests；
- Engine 继续经 `Bukit.Content` compatibility boundary 访问 adapter internals。

见
[`Engine_MustUseContentCompatibilityBoundaryForNotionAdapterInternals`](../../tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs#L145-L189)。
新增 `Bukit.Engine -> Bukit.Content.Notion` 会重开已关闭的依赖问题，属于 C4 禁止项。

### 4.2 两个 test-only internal production helper

| helper | production assembly | 当前 consumer | C1 处置 |
|---|---|---|---|
| `Bukit.Shared.Notion.NotionBlockJsonWriter` | `Bukit.Shared.dll` | `Bukit.Shared.Tests` | 测试迁至 canonical owner 后删除 |
| `Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers` | `Bukit.Content.dll` | `Bukit.Content.Tests` | 测试迁至 canonical owner 后删除 |

二者均只是 canonical helper 的 internal forwarding bridge，见 C0 基线的
[`NotionBlockJsonWriter.cs`](https://github.com/ALi365-SDN-BHD/Bukit/blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Shared/Notion/NotionBlockJsonWriter.cs)
和
[`NotionBlockHelpers.cs`](https://github.com/ALi365-SDN-BHD/Bukit/blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/NotionBlockHelpers.cs)。
后者是 G-04D1B 为测试过渡明确保留的 internal bridge；见
[G-04D1B 台账](bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md#测试所有权迁移与保留边界)。
C1 删除 helper，不改变 canonical writer/renderer 行为，也不新增 public test hook。

## 5. 消费者审计与未知风险

### 5.1 仓库内生产消费者

对 Core、Labs 与仓库内插件 production `.cs` 的检查没有发现 Shared legacy graph 的
direct consumer；命中仅位于 Shared facade 自身。Importing 已直接使用
`Bukit.Notion.Conversion.HtmlToNotionBlockConverter`，见
[`ImportNotionSeedPusher.cs`](../../src/Bukit-Plugins/Bukit.Importing/ImportNotionSeedPusher.cs#L1-L3)
及其 canonical converter 调用。

Content 三类型的 Engine 生产消费者则真实存在，上一节已列出，因此 C3 与 C4
不能使用同一种删除策略。

### 5.2 已知本地外部仓库

| 仓库 | 检查结论 | 对 AD-03C 的含义 |
|---|---|---|
| `SRBiz-bukit` | CLI/binary 调用；没有 Core `.csproj` 或 direct CLR identity 引用 | 未发现 current-Core CLR consumer |
| `Bukit2` | 包含自己的旧 `src/Bukit.*` source tree 和内部 project references | self-contained old source fork，不是当前 Core assembly consumer |
| `WeSiteGen` | 包含自己的旧 `src/Bukit.*` source tree 和内部 project references | self-contained old source fork，不是当前 Core assembly consumer |

旧 fork 中出现相同 namespace 不能算作“当前 Core DLL 的外部消费者”；同时，也不能用
该分类推断未知仓库安全。

### 5.3 公开搜索与无法证明的范围

2026-07-24 的公开 GitHub exact web searches 对以下目标返回 no results：

- `Bukit.Shared.Notion`
- `Bukit.Content.Notion`
- `HtmlToNotionBlockConverter`
- `NotionProviderOptions`

该结果不是 authenticated GitHub Code Search，也不能覆盖 private/unindexed repositories。
尤其必须保留以下风险声明：

- 已针对 1.x DLL 编译的 binary-only consumer 会发生 type/method resolution break；
- reflection 或 serializer 以 assembly-qualified/type name 绑定时，namespace 替换不够；
- `NotionBlock` 是 public abstract record，未知外部 subclass 不能自动映射为 canonical
  subtype；
- private、未披露或未索引的源码 consumer 不可见；
- assembly identity 不同，canonical 同名/近似类型不构成二进制替换。

2.0 breaking-change notice 必须明确这些限制，并要求 direct CLR consumer 更新引用、
迁移 namespace/type、重新编译和重新验证；不得写成“没有外部消费者”或“无风险删除”。

## 6. C0 owner-test baseline

C0 入场 owner-test baseline 已由控制器完整执行并通过：

| owner test project | passed | failed | skipped |
|---|---:|---:|---:|
| `Bukit.Shared.Tests` | 335 | 0 | 0 |
| `Bukit.Notion.Tests` | 339 | 0 | 0 |
| `Bukit.Content.Notion.Tests` | 6 | 0 | 0 |
| `Bukit.Content.Tests` | 464 | 0 | 0 |
| `Bukit.Engine.Tests` | 1628 | 0 | 0 |
| `Bukit.Architecture.Tests` | 264 | 0 | 0 |
| **合计** | **3036** | **0** | **0** |

这些数字是 C1-C6 的回归参照，不等于后续任务已经验证。本 C0 文档任务本身未重复运行
tests 或 gates。

## 7. C1-C6 实施合同

以下任务必须按顺序进入。每项只在自身 exit criteria 满足后，才允许下一项把其提交
作为 entry base；不得把多个 rollback boundary 合并成一个不可审查的大提交。

### 7.1 AD-03C1：迁移测试并删除两个 test-only helper

| 合同项 | 要求 |
|---|---|
| scope | 把 legacy helper 行为测试迁到 canonical owner；删除 `NotionBlockJsonWriter` 与 `NotionBlockHelpers` 两个 internal forwarding helper；更新只与这两个 helper 相关的测试/architecture 断言 |
| public delta | 0；两个目标均为 internal，不改 19 项 public inventory |
| migration/replacement | writer 测试改测 `Bukit.Notion.Conversion.NotionBlockJsonWriter`；renderer helper 分支测试改测 canonical `Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers` 的 owner 行为，不复制 helper、不新增 public test API |
| risks | 测试迁移时丢失 branch coverage；误改 canonical 行为；为保留旧测试而重新公开 helper |
| complete owner-test projects | `Bukit.Shared.Tests`、`Bukit.Notion.Tests`、`Bukit.Content.Tests`、`Bukit.Architecture.Tests` |
| rollback boundary | 单一 C1 提交完整回退测试迁移、helper 删除与相应断言；不得回退 canonical production implementation |
| entry criteria | C0 baseline 可追溯；两个 helper 的 consumer search 仍只有 tests；canonical owner 行为可被 owner tests 直接覆盖 |
| exit criteria | 两个 production helper identity 均不存在；等价测试在 canonical owner；public API 无 drift；四个 owner projects 全绿；focused/owner verification 无非目标差异 |

### 7.2 AD-03C2：只删除 Shared URL facade

| 合同项 | 要求 |
|---|---|
| scope | 只删除 `Bukit.Shared.Notion.NotionApiUrls` 及其 legacy tests/architecture exact-set 条目；不得触碰 converter/model graph |
| public delta | `Bukit.Shared.dll` -1 public type；19 项降为 18 项 |
| migration/replacement | source consumer 改用 `Bukit.Notion.NotionApiUrls`；binary consumer 更新 assembly reference 并重新编译 |
| risks | 固定常量或默认 page size 的来源变化；reflection/assembly-qualified name break；误把 C2 扩展到 Shared model graph |
| complete owner-test projects | `Bukit.Shared.Tests`、`Bukit.Notion.Tests`、`Bukit.Content.Tests`、`Bukit.Architecture.Tests` |
| rollback boundary | 单一 C2 提交恢复 facade、其 tests 与 governed baseline delta；不回退 C1 |
| entry criteria | C1 exit complete；repository-local consumer search 无 Shared URL facade production consumer；canonical constants/builders exact replacement 已核对 |
| exit criteria | 旧 URL CLR identity 不再 exported；canonical URL owner 不变；只出现预期 -1 public delta；四个 owner projects 全绿；无 Notion API/header/wire behavior drift |

### 7.3 AD-03C3：原子删除 Shared converter、13 models 与 mapper

| 合同项 | 要求 |
|---|---|
| scope | 同一提交删除 `HtmlToNotionBlockConverter`、13 个 legacy model identities、`NotionCompatibilityMapper` 及对应 Shared tests；清理 `tests/Bukit.Engine.Tests/NotionSchemaDrivenMappingTests.cs` 中 stale `using Bukit.Shared.Notion;`；删除 `Bukit.Shared -> Bukit.Notion` project reference |
| public delta | `Bukit.Shared.dll` -14 public types；18 项降为 4 项；同时减少一条 compatibility project reference |
| migration/replacement | `HtmlToNotionBlockConverter.ToBlocksJson/Convert` 迁到 `Bukit.Notion.Conversion.HtmlToNotionBlockConverter`；model types 迁到 `Bukit.Notion.Blocks`；direct CLR consumer 更新 namespace/assembly reference 并重新编译；自定义 `NotionBlock` subclass 需由消费者显式重写/映射 |
| risks | 半拆 graph 造成不可编译 public signature；遗漏 recursive `ToggleBlock.Children` 或 `RichTextSegment`；binary/reflection/serializer break；误改 canonical 62-type surface；误影响 Importing |
| complete owner-test projects | `Bukit.Shared.Tests`、`Bukit.Notion.Tests`、`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` |
| rollback boundary | 单一 C3 提交恢复 converter、13 models、mapper、Shared reference、tests 和 baseline；不回退 C1/C2 |
| entry criteria | C2 exit complete；Core/Labs/plugins Shared graph production consumer search 仍为空；external/unknown 风险已冻结在 C0 minimum breaking/migration notice contract，且 C6 不得删减；完整 14-identity atomic set 已固定 |
| exit criteria | 14 个 legacy public identities 与 internal mapper 均不存在；`Bukit.Shared` 不再引用 `Bukit.Notion`；canonical 62 public types及 Importing canonical 调用不变；只出现预期 -14 public delta；四个 owner projects 全绿 |

### 7.4 AD-03C4：原子 internalize Content API client/provider/options

| 合同项 | 要求 |
|---|---|
| scope | 同一提交把 `NotionApiClient`、`NotionContentProvider`、`NotionProviderOptions` 从 public 收窄为 internal；同步迁移 tests/architecture/public baseline；保留现有文件位置、bridge 与 friendship |
| public delta | `Bukit.Content.dll` -3 public types；4 项降为 1 项 |
| migration/replacement | 外部 source consumer 应直接采用 canonical `Bukit.Notion.Transport` 与 `Bukit.Content.Notion` source/options contract，并显式迁移 request/content semantics；Core Engine 继续经 `Bukit.Content` internal bridge，不新增 adapter reference |
| risks | 三类型分拆导致 constructor/interface accessibility 不一致；打开 `Bukit.Engine -> Bukit.Content.Notion`；改变 retry、cancellation、exception translation、HttpClient ownership 或 content projection；binary break |
| complete owner-test projects | `Bukit.Content.Notion.Tests`、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` |
| rollback boundary | 单一 C4 提交恢复三个类型的 public visibility、tests 与 baseline；不得通过回退恢复已删除的 Shared graph |
| entry criteria | C3 exit complete；三个类型的 Engine consumer graph 已重新枚举；friendship/accessibility 方案编译成立；依赖方向断言先于实现固定 |
| exit criteria | 三个 legacy types 不再 exported，但 Engine 所有现有 call sites 继续经 `Bukit.Content` 编译运行；`Bukit.Content -> Bukit.Notion` 与 `Bukit.Content -> Bukit.Content.Notion` 保留；不存在 `Bukit.Engine -> Bukit.Content.Notion`；四个 owner projects 全绿；HTTP/content semantics 无 drift |

### 7.5 AD-03C5：`NotionPropertyParser` retain-by-design

| 合同项 | 要求 |
|---|---|
| scope | 对唯一剩余 legacy public type 作显式 retain 决议；更新 active classification/reason，不删除、不 internalize、不新增 replacement API |
| public delta | 0；legacy public inventory 保持 1 项 |
| migration/replacement | 当前无等价 public canonical replacement；继续使用现有 `Bukit.Content.Notion.NotionPropertyParser` in `Bukit.Content.dll`，直到独立 public SDK/productization 决策或新证据批准变更 |
| risks | 把“无 production consumer”误写成“无 consumer”；在未评审时发明 public canonical parser；namespace 相同导致 assembly identity 误解 |
| complete owner-test projects | `Bukit.Content.Notion.Tests`、`Bukit.Content.Tests`、`Bukit.Architecture.Tests` |
| rollback boundary | C5 是治理决议提交；回退仅恢复其 active governance/baseline metadata，不改变 parser implementation |
| entry criteria | C4 exit complete；再次确认没有等价 public canonical replacement；没有新的安全、正确性或 consumer evidence 推翻 retain 结论 |
| exit criteria | parser 仍 public/exported 且行为/assembly identity 不变；active governance 标记 retain-by-design 并写明重审触发条件；三个 owner projects 全绿 |

若后续证据证明 parser 必须删除、internalize 或产品化，必须另开 public SDK
decision；不得静默并入 C5。

### 7.6 AD-03C6：aggregate governance 与正式关闭

| 合同项 | 要求 |
|---|---|
| scope | 汇总 C1-C5；更新 governed public API baseline、active public API governance，最终汇总并发布 2.0 breaking/migration notice，并建立 AD-03C formal closure ledger；C6 不是 C3 的前置条件 |
| public delta | 不再新增 runtime delta；汇总确认相对 C0 为 legacy public types -18、test-only helpers -2、compatibility references -1，最终 retained legacy public type 为 1 |
| migration/replacement | 发布完整 Shared URL、conversion/model、Content bridge 迁移表；明确 assembly identity、重新编译、external subclass 与 unknown-consumer 风险；记录 parser retained-by-design |
| risks | 把 0 public-search results 宣称为无 consumer；把 G-04 误写为未完成；改写 immutable manifest/历史 ledgers；baseline 包含非目标 drift；过度宣称 full/release readiness |
| complete owner-test projects | `Bukit.Shared.Tests`、`Bukit.Notion.Tests`、`Bukit.Content.Notion.Tests`、`Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` |
| rollback boundary | C6 治理/closure 提交独立回退；若 aggregate proof 失败，回退 C6 状态声明但保留 C1-C5 各自已验证提交，逐项定位而非整组隐式重写 |
| entry criteria | C1-C5 每项 exit complete；所有预期 public/project-reference delta 可从 aggregate diff 精确解释；C0 minimum notice contract 在各实施任务中未被删减，等待 C6 最终汇总/发布 |
| exit criteria | governed baseline 与 compiled surface exact match；active governance 和 formal closure ledger 一致；六个 owner projects 全绿；direct owner self-tests、public API drift 与父级授权的 aggregate targeted verification 通过；immutable 136-entry manifest blob 与 G-04 ledgers无变化；独立只读复审无阻断项 |

C6 不得修改
[`bukit-core-2.0-public-surface-candidates.v1.json`](../governance/bukit-core-2.0-public-surface-candidates.v1.json)
这一 136-entry historical snapshot，也不得重写 G-04 的历史任务数字、决议或 consumer
search 结论。current surface 的事实来源是
[`bukit-core-public-api-baseline.v1.json`](../governance/bukit-core-public-api-baseline.v1.json)
与编译产物，不是历史 candidate manifest。

## 8. 2.0 breaking-change notice 最低合同

以下最低合同已在 C0 冻结，是 C3 的 entry evidence；C6 负责把它与 C1-C5 的实际
delta 最终汇总并发布，可以补充、不得删减。因此 C6 本身不是 C3 的前置条件。最终
对外/维护者迁移说明至少必须包含：

1. 删除的 exact legacy CLR identities 与原 assembly；
2. canonical namespace、assembly 与 API 对照；
3. `NotionBlock` 外部 subclass 没有机械迁移保证；
4. binary consumer 必须更新引用并重新编译，不能只替换 DLL；
5. reflection 与 serializer type-name consumer 必须更新绑定；
6. Content 三类型 internalization 不等于 canonical adapter SDK 产品化；
7. `NotionPropertyParser` 继续位于 `Bukit.Content.dll`，没有承诺新的 canonical public
   replacement；
8. 已知本地仓库和公开网页搜索未发现 direct current-Core CLR consumer，但
   private、unindexed、binary-only、reflection、serializer type-name、external
   subclass 和 undisclosed consumers 仍未知。

## 9. 最终资格判定

| 项目 | C0 判定 |
|---|---|
| AD-03C 性质 | P2/P3 架构、兼容与治理清偿；非 P0/P1 bug |
| G-04 关系 | G-04 已关闭；AD-03C 是新的 2.0 cleanup contract |
| Shared URL facade | eligible for independent removal in C2 |
| Shared converter/model graph | eligible only as one atomic C3 removal |
| Content client/provider/options | eligible only as one atomic C4 internalization |
| Content property parser | retain-by-design in C5 unless separately re-authorized |
| unknown external consumers | unknowable；必须进入 breaking/migration notice |
| implementation authorization | 仅按 C1-C6 顺序、原子边界与 entry/exit criteria 分项进入 |

C0 初始发布时只建立资格、迁移和验证合同；它没有执行任何 public API change，也未在
当时宣称后续 owner tests、aggregate targeted gate、full gate 或 release gate 已通过。
后续实际实施结果在下一节和正式关闭台账中单独记录，不追溯改写本节的资格判断。

## 10. C1-C6 实际实施结果

AD-03C1 至 AD-03C6 已按本报告冻结的顺序与原子边界完成。最终 governed surface 为
**14 / 425 / 0**：14 assemblies、425 public types、0 `2.0-candidate`。相对 C0：

- **18 removed**：Shared legacy URL/converter/13-model graph 共 15 项，
  Content client/provider/options 共 3 项；
- **one retained**：
  `Bukit.Content.Notion.NotionPropertyParser` in `Bukit.Content.dll`，
  `retain-by-design`；
- 两个 test-only internal forwarding helpers 已删除；
- `Bukit.Shared -> Bukit.Notion` compatibility project reference 已删除；
- C6 没有新增 runtime diff。

| 任务 | 实施/审查提交 | 完整 owner-suite 证据 | 独立复审 |
|---|---|---:|---|
| C0 | `38dbc0fb`；contract correction `7f700d99` | 3036 | clean |
| C1 | `fafed2bd` | 1403 | clean |
| C2 | `ed226179` | 1404 | clean |
| C3 | `9ef16a6a` | 2570 | clean |
| C4 | `6d053a13` | 2734 | clean |
| C5 | `1caa0482`；review fix `954a1fcb` | 734 | clean after fix |
| C6 | governance/closure commit 见正式关闭台账 | 六套完整 owner suites 见正式关闭台账 | 独立复审后冻结 |

上述测试数是各任务边界的 unfiltered Release project totals，不是 unique tests 的可加总
统计。2.0 consumer migration contract 见
[Bukit Core 2.0 Notion compatibility migration](../governance/bukit-core-2.0-notion-compatibility-migration.md)；
完整 delta、baseline 语义比较、验证边界与残余风险见
[AD-03C 最终汇总关闭台账](bukit-core-ad03c-final-aggregate-closure-2026-07-24.zh-CN.md)。

C0 记录的历史事实仍成立：**G-04 已以 443 public types / 0 candidates 关闭**；
AD-03C 是后续独立 2.0 cleanup，不把 G-04 改写为未完成。
