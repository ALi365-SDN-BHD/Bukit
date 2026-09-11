# srbiz-delivery-verification Specification

## Purpose
为丝路商讯提供从隔离构建、候选封存到线上只读核验的有限交付流程，明确源码、运行时、公开包、私有证据与线上响应的责任边界。准备候选不授权发布或修补原始输出，任何必需检查失败都不得形成可批准的成功候选。

## Requirements

### Requirement: 单工具三入口
交付工具 MUST 支持 check --build-root … --report …、prepare --site-root … --baseline-repo … --baseline-commit … --out …、verify-online --candidate … --base-url …，含CNAME时必须提供 --published-repo … --published-commit 完整不可变提交。

#### Scenario: 单工具三入口验收
- **WHEN** 调用任一模式
- **THEN** 仅执行该模式声明的读取、隔离准备或只读核验，不隐含发布

### Requirement: 固定隔离候选
prepare SHALL 固定隔离源码配置模板、锁定 runtime 和不可变 Git 基线；out 不得覆盖现有目录；只允许环境变量凭据，不复制秘密。

#### Scenario: 固定隔离候选验收
- **WHEN** 准备含 Prasarana 的真实 SRBiz 候选
- **THEN** 可只读 Notion 取数，不写 Notion；源工作区和原始业务构建不变

#### Scenario: 输出目录已存在
- **WHEN** out 已存在（包括非空目录）
- **THEN** prepare 拒绝覆盖，保留原目录全部内容

### Requirement: 公开私有分离
候选 MUST 分开私有证据和公开 package；公开包不得带 .bukit 报告，业务产物逐字节复制，仅显式保留现有 CNAME、README、验证文件并记录来源哈希。

#### Scenario: 公开私有分离验收
- **WHEN** 基线存在保留文件或业务输出
- **THEN** 只保留白名单来源，不混入私有证据或秘密

#### Scenario: 打包中途失败
- **WHEN** 复制公开文件或写入必需证据失败
- **THEN** prepare 返回失败，不输出成功，不将不完整目录标记为有效封存候选

### Requirement: IndexNow边界
prepare SHALL 复用现有 IndexNow prepare helper，但不得生成或轮换 key，不进行通知。

#### Scenario: IndexNow边界验收
- **WHEN** 候选需要已有验证文件
- **THEN** 复用已知文件来源和哈希，缺失则报告，不自行创建新密钥

### Requirement: 禁止交付修补
工具 MUST 不修改 raw build，不补写 robots、不重写媒体，不执行 commit/push/deploy/通知；站点候选 staging 不代表 Core 原子 staging。

#### Scenario: 禁止交付修补验收
- **WHEN** 发现两处既有 robots 偏差或媒体改名
- **THEN** 只报告并交付独立部署审阅说明

### Requirement: 封存与篡改拒绝
候选清单、基线、runtime 和文件哈希 SHALL 封存；任何篡改使候选失效。固定 fixture 可重放，封存候选可重验，不要求历史 Notion 快照可重建。

#### Scenario: 封存与篡改拒绝验收
- **WHEN** 候选文件被修改后核验
- **THEN** 核验失败且不得继续将其视为批准候选

### Requirement: 有界HTTPS核验
verify-online MUST 只读核对全部文件：精确根目录CNAME作为GitHub Pages部署元数据，核对封存字节/哈希与明确本地发布仓不可变提交中的普通blob一致，唯一合法域名与HTTPS目标host匹配，不请求/CNAME；其余新增/保留文件保持HTTP200及哈希校验、删除404/410，限制响应头、正文及总体deadline（含发布commit读取），拒绝跨主机重定向。候选schema必须显式封存CNAME分类与工具版本/哈希，旧候选不得静默迁移。

#### Scenario: 有界HTTPS核验验收
- **WHEN** 站点响应200但字节错、删除文件仍在或跳转其他主机
- **THEN** 核验失败，不能将200或Git推送等同上线验收

#### Scenario: 正文持续阻塞
- **WHEN** HTTPS 响应头立即返回但正文持续阻塞，或整体核验超过总期限
- **THEN** verify-online 在正文或总体 deadline 内取消并返回失败，不输出首次成功日志

#### Scenario: CNAME部署元数据
- **WHEN** 候选封存CNAME且平台对/CNAME返回404
- **THEN** 必须提供完整发布commit，核对普通blob字节与唯一域名匹配HTTPS目标；不发送/CNAME请求，其余页面仍严格HTTP验收

#### Scenario: 元数据不可信
- **WHEN** CNAME缺失、损坏、多行、多域名、域名不匹配、提交缺失/非普通blob或读取超时
- **THEN** 失败关闭，不输出VERIFIED；不得豁免其他路径或添加.nojekyll

### Requirement: 分批验收停止边界
实施 SHALL 使用唯一 tasks 清单、每批一次专项及最终一次新差异复审，结束时无 open Critical/Important；发现新增 Core 缺陷须停止受影响项并报告。

#### Scenario: 分批验收停止边界验收
- **WHEN** 专项发现 Important 或网站修复需要 Core 代码
- **THEN** 仅限定重入，禁止扩大成 Core 修改或额外审计
