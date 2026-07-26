# Bukit Core 增量外部合同评审记录

> 日期：2026-07-26
>
> 基线提交：`23105d8d05bd4a1c1e0235960a54cd69ba7e18a7`
>
> 范围：Gap C；只评审三个既有类型的四个增量 public 成员
>
> 状态：reviewed / focused-verification-complete

## 1. 决议与边界

本次评审接受以下四个增量外部合同：

```text
Bukit.Config.CollectionConfig.NoindexWhenEmpty
Bukit.Config.SeoOrganizationConfig.SameAs
Bukit.Config.SeoOrganizationConfig.Type
Bukit.Rendering.PageModel.Pages
```

candidate snapshot 相对原 governed baseline 只有这四个 public member addition：
三个既有类型的 type signature、protected members、owner、classification、
compatibility 与 migration horizon 均未改变，没有新增 public type。

本决议不处理实现内部化、`SeoIndexEntry.LastModified` 或其他 public API 变更。相关
源码与测试由各自工作项负责，不纳入本次 baseline 接受范围。

## 2. 分类与兼容决议

| 类型与成员 | Owner | Classification | Compatibility | Migration horizon |
|---|---|---|---|---|
| `CollectionConfig.NoindexWhenEmpty` | Configuration | `serialized-contract` | `1.x-shape-stable` | `retain-1.x` |
| `SeoOrganizationConfig.SameAs` | Configuration | `serialized-contract` | `1.x-shape-stable` | `retain-1.x` |
| `SeoOrganizationConfig.Type` | Configuration | `serialized-contract` | `1.x-shape-stable` | `retain-1.x` |
| `PageModel.Pages` | Rendering and theme model | `serialized-contract` | `1.x-shape-stable` | `retain-1.x` |

这些值沿用三个既有类型的 governed metadata。评审没有通过改变分类或兼容标签来隐藏
contract-shape drift；而是把新成员作为新的受控稳定形状接受。写入 baseline 后，后续
删除、改型或收窄仍需独立 reviewed migration。

## 3. 默认值、向后兼容、迁移与理由

### 3.1 `CollectionConfig.NoindexWhenEmpty`

- 精确签名：
  `public System.Boolean NoindexWhenEmpty { get; init; }`
- 默认值：`false`。
- 向后兼容：旧配置省略该字段时，空 collection 保持原有可索引行为。
- 迁移：无强制迁移。只有显式配置为 `true` 时，空 collection list route 才使用
  `noindex,follow`，并从 sitemap、search、`llms.txt` 与 `llms-full.txt` 表示中排除。
- 理由：允许站点显式控制空 collection 的索引策略，同时不改变既有站点默认。

### 3.2 `SeoOrganizationConfig.Type`

- 精确签名：`public System.String! Type { get; init; }`
- 默认值：`Organization`。
- 向后兼容：旧配置只包含 `name`、`url`、`logo` 时，仍生成
  `Organization` publisher；只有显式配置才选择 `NewsMediaOrganization`。
- 迁移：无强制迁移。显式非法值继续由严格配置验证拒绝。
- 理由：让 publisher schema type 成为显式、可验证且不猜测的配置合同。

### 3.3 `SeoOrganizationConfig.SameAs`

- 精确签名：
  `public System.Collections.Generic.IReadOnlyList<System.String!>! SameAs { get; init; }`
- 默认值：空只读列表。
- 向后兼容：旧配置不输出 `sameAs`；空列表继续从 JSON-LD 中省略。
- 迁移：无强制迁移。需要组织身份链接的站点显式增加 URL 列表。
- 理由：提供显式组织身份关联，不从其他字段或网络推断。

### 3.4 `PageModel.Pages`

- 精确签名：
  `public System.Collections.Generic.IReadOnlyList<Bukit.Rendering.PageInfo!>! Pages { get; init; }`
- CLR 默认值：`Array.Empty<PageInfo>()`。
- CLR 向后兼容：旧调用方仍可只初始化 `Site` 与 `Page`；未设置时获得非 null 空数组。
- 模板兼容：不引用 `pages` 的旧 detail template 无需迁移。Engine 生成的 detail
  model 现在会提供当前 render batch 的 page index；以前以 `pages` 不存在作为
  page/list 判别条件的模板会观察到行为变化。
- 迁移：不要再以 `pages` 是否存在区分 detail 与 list；应使用明确的模板或 route
  上下文。detail page index 中每项 `Content` 为空，只提供索引元数据，顺序保持
  dispatcher 输入顺序；当前正文仍来自 `page.content`。
- 理由：允许 detail template 构建作者页、导航与跨页面索引投影，同时避免加载全部正文。

## 4. Candidate snapshot 证据

生成命令：

```sh
TMPDIR=/tmp bash scripts/checks/public-api-drift.sh snapshot \
  /tmp/bukit-public-api-reviewed-20260726-c.json Release
```

结果：exit 0；Release build 为 0 warning、0 error；candidate 文件大小
`589152` bytes，SHA-256：

```text
bda3fa935225ebf6de26f9a991b63a2dea2d05ba43c49975a6b99ce8263c68d9
```

相对原 governed baseline 的完整差异为：

```text
+ public System.Boolean NoindexWhenEmpty { get; init; }
+ public System.Collections.Generic.IReadOnlyList<System.String!>! SameAs { get; init; }
+ public System.String! Type { get; init; }
+ public System.Collections.Generic.IReadOnlyList<Bukit.Rendering.PageInfo!>! Pages { get; init; }
```

四个成员均由 public API formatter 按 `StringComparer.Ordinal` 插入，baseline 的其他
canonical bytes 与 candidate 一致。

## 5. 合同测试与验证

本变更复用已经为产品合同建立的测试，不新增只会在实现后通过的 change-detector：

- `EmptyCollectionSeoConfigTests`：binding、默认值、schema、严格字段与 unknown field；
- `SeoOrganizationConfigContractTests`：binding、schema、非法值、unknown field 与旧配置默认；
- `SeoPublisherJsonLdTests`：publisher type、URL、`sameAs` 与兼容输出；
- `CompanyEntityAndEmptyCollectionTests`：空 collection 的 downstream indexability；
- `ScribanModelBinderTests`：detail/list root 的 `pages` 显式投影；
- `SeoGeoDocumentationContractTests`：活动配置 schema 与 SEO/GEO 文档。

最终专项验证结果：

- candidate snapshot：exit 0；Release build 为 0 warning、0 error；
- `bash scripts/checks/public-api-drift-self-test.sh`：exit 0；
- `bash scripts/checks/public-api-drift.sh check Release`：exit 0；Release build 为
  0 warning、0 error；
- `SeoGeoDocumentationContractTests` 精确过滤：4 passed、0 failed、0 skipped；
- `bash scripts/checks/docs/active-links.sh`：exit 0；
- `bash scripts/checks/docs/no-absolute-paths.sh`：exit 0。

未执行全矩阵、单独 fixture、历史审计、post-change 或 `ci-fast`；它们不属于本次
获准的专项证据范围。公共 API self-test 内部行为属于该获准 owner self-test 本身，
未另行扩展验证范围。
