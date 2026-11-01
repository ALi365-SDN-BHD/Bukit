## ADDED Requirements

### Requirement: Privacy JSON scanning distinguishes clean, leak and incomplete

构建侧 `PublicOutputPrivacyCheck` 与部署侧 `DeploymentPrivacyValidator` MUST 使用同一内部 JSON 扫描实现，检查解码后的字符串值、解码后的属性名、对象、数组和顶层字符串，保留 `source`/`sourceKey == notion` 检测，并使用各自收集的敏感 token 集合。扫描结果 MUST 区分“完成且无泄露”“发现泄露”“扫描未完成”。解析位置的语法错误、深度超限等 `JsonException`，以及字符串输入解码失败 MUST 返回未完成；原文未命中 MUST NOT 把未完成覆盖为通过。字符串解码失败 MUST 在最窄读取边界归类：确认 `ValueKind` 为 String 且文档仍有效后，把 `JsonElement.GetString()` 的 `InvalidOperationException`、以及遍历中 `JsonProperty.Name` 的同类异常作为未完成，覆盖属性名、普通字段、数组元素和顶层字符串。实现 MUST NOT 依赖异常消息文本，MUST NOT 吞掉解码边界之外的 `InvalidOperationException`；I/O、取消及程序错误继续遵循原有失败路径。诊断 MUST 只记录固定的未完成原因和脱敏路径，不输出原始 JSON、属性内容或异常消息中的输入片段。

#### Scenario: Escaped identifiers in every JSON position (PRI-01, PRI-02, PRI-03, PRI-04, PRI-05)
- **WHEN** 已知 ID 部分或全部转义，带连字符或紧凑形式，位于嵌套对象、数组、属性名、顶层字符串，`.json` 或 `.jsonld`，或 provider marker 被转义
- **THEN** 两侧扫描器均识别泄露，既有 provider marker 覆盖继续通过

#### Scenario: Ordinary content stays clean (PRI-06)
- **WHEN** JSON 含未知业务 UUID、普通文本或字面反斜杠
- **THEN** 不误判为泄露，JSON 按语义解码一次，不把普通字面反斜杠当作再次编码的 ID

#### Scenario: Invalid or over-deep JSON (PRI-07, PRI-12, PRI-13)
- **WHEN** 无效 JSON 的原文分别含或不含已知 ID；或 64/65 层数组包裹部分/全部转义 ID
- **THEN** 原文命中时判泄露；无命中时判未完成且不能报告通过；边界内解码命中，边界外返回未完成，`.json`/`.jsonld` 一致

#### Scenario: Unpaired Unicode surrogates (PRI-15)
- **WHEN** 未配对高/低代理项出现在属性名、普通字段、数组元素或顶层字符串，或合法代理项配对作对照
- **THEN** 两侧解析/遍历均把输入解码失败归为未完成；合法配对正常扫描，不一律拒绝 Unicode 转义

#### Scenario: Redacted diagnostics (PRI-08)
- **WHEN** 内部报告和诊断输出包含被排除规则覆盖的产物或敏感 ID
- **THEN** 排除规则有效，诊断不泄露敏感 ID

### Requirement: Incomplete scans fail every consuming mode

构建侧 MUST 把泄露和未完成都映射为现有 `publicOutputPrivacy=failed` 并写入脱敏错误：strict MUST 阻断事务且旧输出/缓存保持；warn MUST 报告失败并按既有策略继续；`off` 及不适用判定 MUST 保持各自契约。部署侧 MUST 把两类失败转换为验证错误，在 Git add/commit/push 前停止，沿用 `BKT-BUILD-SECURITY-PRIVACY-0001` / `BKT-DEPLOY-PRIVACY-0001`，消息 MUST 明确区分“发现泄露”与“无法完成检查”，不能把未完成误报为已证实泄露。

#### Scenario: Escaped identifier in strict build (PRI-09, PRI-10)
- **WHEN** 严格构建或 warn 模式的公开产物包含转义已知 ID
- **THEN** 严格构建隐私检查失败且事务不提交、旧输出和缓存保持；warn 得到准确失败报告并按既有策略完成

#### Scenario: Deployment stops before Git writes (PRI-11)
- **WHEN** 实际部署暂存产物包含转义已知 ID
- **THEN** 返回 `BKT-DEPLOY-PRIVACY-0001` 并在 Git add/commit/push 前停止

#### Scenario: Incomplete scan in all modes (PRI-14, PRI-16)
- **WHEN** 已满足 Notion 适用性条件的公开 JSON 超深、畸形或含未配对代理项，分别进入 strict、warn 和部署路径
- **THEN** 异常不越过扫描器；strict 不提交且旧输出/缓存不变；warn 生成 failed 报告并按策略完成；部署返回隐私验证错误而非未归类异常；诊断脱敏

### Requirement: Report schema and boundaries stay unchanged

报告 schema、字段和状态集合 MUST NOT 扩展；完成性只作为内部判定和错误原因。原始文本检查、非 JSON 行为、现有错误码和路径遮蔽 MUST 保留。构建侧 Notion 适用性判断和部署侧内部身份报告校验 MUST 保持现有契约。JSON MUST 按语义解码一次，未知业务 UUID MUST 继续允许。

#### Scenario: Schema and applicability regression
- **WHEN** 安全报告与部署身份报告在泄露、未完成和不适用场景下生成
- **THEN** 既有 schema 校验通过，Notion 适用性与内部身份报告前提不变，退出码与路径遮蔽语义保持
