## MODIFIED Requirements

### Requirement: Notion body fingerprints reflect the effective content mode

内置 Notion 的稳定 `bodyFingerprint` MUST 包含有明确分隔的有效 `RenderContent` 布尔标记和 Notion 渲染器契约版本，并使用已合并来源覆盖与默认值的 options。返回该页 `last_edited_time` 的数据库查询响应的 `Date` 头（缺失时用本机 UTC）早于 `last_edited_time` 所在分钟结束后 5 秒时，指纹 MUST 额外带有「未落定」标记；这一判断与正文缓存的落定判断 MUST 使用同一时钟来源。`last_edited_time` 为空时 MUST NOT 输出稳定指纹。切换正文模式或渲染器契约版本 MUST 使增量输入失效，并产生与全量渲染一致的公开 HTML；页面从未落定变为落定时 MUST 再失效一次；稳定且已落定时，下一次热构建 MUST 立即复用。本要求本身不改变 manifest 结构。

#### Scenario: Bidirectional content-mode switch (INC-09)
- **WHEN** Notion 冷构建从 `RenderContent=true` 切到 `false`，稳定后切回 `true`
- **THEN** 每次模式变化均失效并重渲染，稳定模式立即复用，公开 HTML 分别有/无正文且与全量渲染一致

#### Scenario: Renderer contract bump without localization
- **WHEN** 未启用媒体本地化的站点用 `--no-clean` 构建，渲染器契约版本递增且页面未编辑
- **THEN** 页面重新渲染，公开 HTML 与全量渲染一致

#### Scenario: Edited within the fetch minute without localization
- **WHEN** 未启用媒体本地化，页面在服务器时间 10:00:10 编辑、构建在 10:00:30 读取数据库，之后在 10:00:50 再次编辑，下一次构建在服务器时间 10:01:05 之后运行
- **THEN** 下一次构建重新拉取正文并重新渲染该页面，公开 HTML 包含 10:00:50 的修改

#### Scenario: Local clock ahead of Notion without localization
- **WHEN** 未启用媒体本地化，本机时钟比 Notion 快 30 秒，页面在服务器时间 10:00:50 编辑，构建在服务器时间 10:00:55 读取数据库
- **THEN** 该次构建的指纹带有未落定标记，服务器时间 10:01:05 之后的下一次构建重新渲染该页面并包含该修改

#### Scenario: Missing last edited time
- **WHEN** 数据库返回的页面没有 `last_edited_time`
- **THEN** 不输出稳定指纹，页面内容哈希根据正文计算
