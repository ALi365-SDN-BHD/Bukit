# srbiz-content-presentation Specification

## Purpose
统一丝路商讯资讯与企业页面的关联导航和核验日期展示语义，使编辑关系只指向当前可公开、集合和语言正确的唯一页面。展示层保护无效目标信息，保留源数据的关系方向，并使用当前标题和清晰业务术语避免暗示信用背书。

## Requirements

### Requirement: 唯一有效关系身份
关系解析器 MUST 仅在当前语言、期望集合的有效页面中以 content_model.slug 确定唯一目标；禁止使用 page.slug、Notion URL 或标题作为身份。

#### Scenario: 唯一有效关系身份验收
- **WHEN** slug 对应多个、零个或错误集合/语言目标
- **THEN** 该关系隐藏，不猜测目标

### Requirement: 受限 URL 回退
存在 slug 时 SHALL 以集合+slug 为主；内部路径或本站 canonical URL 与其矛盾则隐藏。仅 slug 缺失时可用内部/本站 URL 找唯一目标；有效 slug 上的外部 Notion URL 被忽略。

#### Scenario: 受限 URL 回退验收
- **WHEN** 关系只有本站有效 URL 或带冲突内部 URL
- **THEN** 前者解析唯一有效目标，后者隐藏

### Requirement: 方向与去重上限
资讯报道对象 SHALL 去重后不限六条；企业相关报道 SHALL 显式关系优先，再按 URL 排序追加反向结果，统一去重且最多六个。JSON 源关系方向 MUST 保持。

#### Scenario: 方向与去重上限验收
- **WHEN** 显式和反向重复且有效结果超过六条
- **THEN** 企业显示确定的六个唯一目标，资讯保留所有有效企业

### Requirement: 标签与空区块
企业页 SHALL 按集合识别，标签取当前目标标题；无有效目标不得输出空 section 或占位文案；使用报道对象/相关报道，不暗示合作客户或信用背书。

#### Scenario: 标签与空区块验收
- **WHEN** 关系标签过期或全部失效
- **THEN** 有效链接用当前标题，全部失效则省略区块

### Requirement: 私密诊断
无效关系 MUST 仅在 HTML 注释输出固定原因码及计数；注释和诊断不得暴露目标标题、slug、URL、ID。验收报告只定位源页，不二次剥离 HTML。

#### Scenario: 私密诊断验收
- **WHEN** 关系因为缺失或冲突被隐藏
- **THEN** 输出聚合原因计数，公开 HTML 导航隐藏范围不改变源 JSON

### Requirement: 核验日期
VerifiedAt SHALL 仅展示有效业务核验日期，无值不得退回源编辑时间或 UpdatedAt，也不得映射 reviewedAt。

#### Scenario: 核验日期验收
- **WHEN** 有效、空值或含时区 VerifiedAt 输入
- **THEN** 展示既定日期语义，空值不生成假日期，不更改 reviewedAt
