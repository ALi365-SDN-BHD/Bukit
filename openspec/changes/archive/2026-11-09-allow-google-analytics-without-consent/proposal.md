# Allow Google Analytics without consent configuration

## Why

配置了 Google Analytics 或 Google Tag Manager 的站点，现在必须同时写 `site.analytics.consent.google`，否则配置校验失败。不面向需要 Consent Mode 的地区、或由主题／第三方 CMP 自行处理同意的站点，被迫写一段不需要的默认值。

渲染层已经支持没有 consent 的情况：不输出 `gtag('consent','default',…)`，GA 使用自带 `dataLayer`/`gtag` 初始化的完整引导脚本，GTM 保留 `<noscript>`。只是校验层挡住了这种配置。

这条强制要求来自 Analytics 审计 AN-08（`docs/analysis/bukit-analytics-built-in-plugin-deep-audit-2026-07-18.zh-CN.md`），当时的目的是让 consent 默认值始终显式、并排在 Google 加载器之前。本变更保留后一半（配置了 consent 时顺序不变），放弃"必须配置"这一半：是否启用 Consent Mode 属于站点的合规决定，Core 不应替站点做出。该审计报告是历史记录，不修改。

## What Changes

- 删除"配置了 Google provider 就必须有 `consent.google`"的校验（`I18nValidator`）和 JSON Schema 中对应的 `if/then`。
- 保留反方向约束：写了 `consent.google` 却没有 Google provider，仍然报错；写了 `consent` 但缺 `consent.google`，仍然报错。
- 规格：Consent 默认值放在 Google 加载器之前的要求，改为只在配置了 consent 时适用。
- 文档写明：不配置 consent 时，Google 按其默认行为（相当于全部 granted）加载，由站点自行负责合规。

## Impact

- 受影响代码：`Bukit.Config/I18nValidator.cs`、`Bukit.Config/ConfigJsonSchemaGenerator.cs`。
- 渲染、CSP、报告、增量哈希不改；`AnalyticsRendererContract.Version` 不升级，因为已有合法配置的输出不变。
- 公共 API 不变。
- 兼容性：只放宽校验，已有配置行为不变。

## Non-Goals

- 不在构建时对"无 consent"输出提示。
- 不改变 consent 的取值规则。
