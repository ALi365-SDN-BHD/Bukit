# Bukit Core G-04D9D Feed / SEO / Sitemap Graph 受控收窄台账

> 日期：2026-07-24
>
> 任务：G-04 Group 4 / Task 36
>
> 状态：implementation-complete / g4-verification-pending

## 1. 终态

七项 internalized：

- `AtomFeedGenerator`；
- `JsonFeedGenerator`；
- `SitemapGenerator`、`SitemapGenerator.Alternate`、
  `SitemapGenerator.UrlEntry`；
- `SeoAlternatesService`；
- `SeoInjectionPolicy`。

`RssGenerator` retained public 并重分类为
`cross-assembly-implementation / 1.x-do-not-narrow`；其稳定 public nested
`RssGenerator.Post` 不属于本批候选，继续可达。production 只修改五个 containing
type modifiers，nested record/member shape 不变。

## 2. Baseline 与证据

current baseline 从 D9C `14/469/31` 变为：

```text
14 assemblies / 462 public types / 23 candidates
```

historical manifest 保持 `closed / 136 / 136`，八项历史记录与 blob
`7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

新增 `G04D9DFeedSeoSitemapGraphTests` 锁定七项 internal、RSS/Post retained、
sitemap nested graph、baseline、历史记录和活动治理文档。

## 3. 行为下界

Task 42 必须覆盖 `RssGeneratorTests`、`SitemapGeneratorTests`、
`PublishRepresentationRegistryTests`、`I18nMergedFeedProjectionTests`、
`SeoPipelineTests` 和外部图片审计测试，确认：

- RSS/Atom/JSON/sitemap 字段、escaping、排序、limit、URL 和 locale alternates；
- JSON Feed 继续在写入前使用既有 safe path resolution；
- `seo_inject=false/off` 行为；
- true external OG/Twitter images 不发 HTTP 请求，继续发出两个
  `external_unverified` codes；
- Native AOT 静态调用与 JSON writer 可达性。

## 4. 边界

不修改 config/schema、feed URL、JSON/XML/HTML bytes、HTTP/TLS、SEO 网络权限、
path tool、plugin protocol、Labs 或外部插件。已独立关闭的 JSON Feed P1 不重复
实施或扩展。

Task 36 按 G4 规则不单独运行 tests/gates/AOT/review；全部证明留到 Task 42。
