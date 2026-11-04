# Bukit IndexNow / WechatSync 问题梳理与修复排序

- 日期：2026-11-04
- 基线：`712bdc0351c419063add8e9779bd8ef659e20e02`
- 范围：IndexNow 业务实现与插件入口；WechatSync 业务实现与插件入口；两组共同验证映射缺口
- 本轮性质：**只整理，不修复**。未修改源码、测试、插件配置、CI/gate；未运行 `dotnet test`；未做远程调用；未重新审计 Import。

> 开始前工作树已存在一处与本任务无关的未提交修改：
> `src/Bukit-Plugins/Bukit.Plugin.WechatSync/examples/minimal/.bukit/plugins.yaml`。
> 本报告没有触碰该文件。

---

## 0. 执行结果（2026-11-04）

本报告的分析已按顺序执行，8 项问题全部落地，mapping 缺口已补齐：

| 项目 | 结果 |
|---|---|
| IN-01 | 已修复：同名不同内容拒绝覆盖；相同内容复用；`FileMode.CreateNew` 消除 Unix rename 竞态 |
| IN-02 | 已修复：默认 transport 禁用自动重定向；final URI 校验 |
| IN-03 | 已修复：采用保守策略 A，旧 pending 不再阻断新变化；结构非法仍 fail closed |
| WX-01 | 已修复：只解码标签外文本，属性与代码块保留 |
| WX-02 | 已修复：去重键区分大小写，规范化后去重，media key 保留 query |
| WX-03 | 已修复：无封面时走默认图/默认 media ID 回退 |
| WX-04 | 已修复：缓存按 AppId 隔离并持久化 AppId；旧 v3 缓存支持人工确认 AppId 后迁移，保留 Records/ThumbMediaIds/Operations |
| WX-05 | 已修复：manifest 字段/版本校验；声明 JSON 缺失或空对象明确失败 |
| 验证缺口 | 已修复：IndexNow + WechatSync 相关源码/测试/csproj 均映射到专项测试 |

验证证据：

```text
dotnet test tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj
Passed: 47 / 47

dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj
Passed: 306 / 306

bash scripts/checks/codex-workflow-self-test.sh
codex workflow self-test OK

python3 scripts/checks/codex-workflow.py closure ... 24 changed implementation files
unmapped 0
```

Native AOT（.NET SDK 10.0.403）：

```text
IndexNow   osx-arm64 / Release / PublishAot -> success, handshake exit 0
WechatSync osx-arm64 / Release / PublishAot -> success, handshake exit 0
```

实现后专项审查发现 3 个 Important（WX-03 HTTP 默认图失败未回退默认 media ID；WX-04 旧缓存迁移提示不可执行；WX-05 `{}` / 缺少 reviewStatus 的 content JSON 仍可能空任务成功），均已修复并补充回归；最终专项复审为 0 Critical / 0 Important / 0 Minor。IN-01 的 Unix rename 竞态已在后续加固中改为 `FileMode.CreateNew` 直接写并补并发创建回归；WX-02 旧 media key 无回退属于安全优先的缓存命中率退化，已补注释和回归防止未来误加 fallback，不阻塞本批交付。

---

## 1. 结论摘要

本次确认 8 项业务问题，另确认 1 项共同验证缺口：

| 类别 | 数量 | 说明 |
|---|---:|---|
| P1 | 6 | IN-01、IN-02、WX-01、WX-02、WX-04、WX-05 |
| P2 | 2 | IN-03、WX-03 |
| 共同验证缺口 | 1 | 19 个审计范围文件未映射到专项测试 |

P1 按“数据损坏、错发、漏发”优先，P2 按“功能与恢复”随后处理。

本轮建议执行顺序与用户给定顺序一致：

```text
[先决] 专项映射补齐
  -> IN-01
  -> WX-04
  -> WX-01 / WX-02 / WX-05
  -> IN-02
  -> WX-03
  -> IN-03
```

其中：

- **IN-03 不是实现就绪状态**。它的恢复规则必须先与部署基线规则对齐，尤其是“旧 pending 在跨版本后应丢弃、重基，还是保留到某个 generation/TTL”。未对齐前不要进入实现。
- **IN-02 的回归不能只用 `FakeTransport`**。现有 `FakeTransport` 位于 `IIndexNowTransport` 层，天然绕过了 `HttpClient` 的自动重定向行为；必须直接测试 `IndexNowHttpClient` 或真实 HTTP server。
- **IN-01 现有测试反而锁定了错误行为**。`IndexNowPluginContractTests.KeyFileWriter_RejectsTargetSymlinkAndAtomicallyReplacesRegularFile` 目前断言“已有普通文件会被覆盖”，修复时必须同步改写该测试。

---

## 2. 基线与责任边界

### 2.1 代码责任

```text
IndexNow
  Bukit.Plugin.IndexNow   协议、命令入口、参数映射、响应适配
  Bukit.IndexNow          业务实现：key 文件、HTTP transport、状态、pending、提交流程
  -> 8 项中的 IN-01/02/03 根因均在 Bukit.IndexNow

WechatSync
  Bukit.Plugin.WechatSync 协议、参数、响应适配
  Bukit.WechatSyncing     输入装载、规划、内容转换、图片/封面、缓存、状态机
  -> 8 项中的 WX-01/02/03/04/05 根因均在 Bukit.WechatSyncing
```

### 2.2 不在本轮范围

- `src/Bukit-Core/`、Core 公共契约、Core 测试/fixture。
- `Bukit.Importing` / `Bukit.Plugin.Import` 的重新审计。
- 全量/发布 gate、`scripts/test-all.sh`、`scripts/smoke-all.sh`、`post-change-*` 聚合矩阵。
- 任何生产代码或测试修改。

### 2.3 静态证据说明

本报告的行号来自上述基线。所有行为结论来自当前源码阅读和只读命令；没有把历史审计报告当作当前行为证据。修复后行号会变化，实施时应以当时的文件内容重新定位。

---

## 3. 共同验证缺口（正式修复前必须先补齐）

### 3.1 当前机器可读 closure 结果

使用当前 policy 对 19 个审计范围文件执行：

```text
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed <19 files>
```

实际输出：

```text
changed 19
unmapped 19
specialtyTests []
```

19 个未映射文件按项目分组如下。

**Bukit.IndexNow（8）**

```text
src/Bukit-Plugins/Bukit.IndexNow/Bukit.IndexNow.csproj
src/Bukit-Plugins/Bukit.IndexNow/IIndexNowTransport.cs
src/Bukit-Plugins/Bukit.IndexNow/IndexNowHttpClient.cs
src/Bukit-Plugins/Bukit.IndexNow/IndexNowKeyFileWriter.cs
src/Bukit-Plugins/Bukit.IndexNow/IndexNowModels.cs
src/Bukit-Plugins/Bukit.IndexNow/IndexNowStateStore.cs
src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs
src/Bukit-Plugins/Bukit.IndexNow/IndexNowUrlPolicy.cs
```

**Bukit.Plugin.IndexNow（10）**

```text
src/Bukit-Plugins/Bukit.Plugin.IndexNow/Bukit.Plugin.IndexNow.csproj
src/Bukit-Plugins/Bukit.Plugin.IndexNow/IndexNowPluginApp.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/IndexNowPluginCommandSpecs.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/IndexNowPluginInvoker.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/IndexNowPluginManifestProvider.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/IndexNowPluginOptionsMapper.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/IndexNowPluginResponseMapper.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/Program.cs
src/Bukit-Plugins/Bukit.Plugin.IndexNow/examples/minimal/README.md
src/Bukit-Plugins/Bukit.Plugin.IndexNow/examples/minimal/plugins/indexnow/plugin.yaml
```

**WechatSync 插件（1）**

```text
src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj
```

> 计数说明：IndexNow 两个项目共有 19 个 tracked 文件；上述审计口径排除了生成的示例配置
> `src/Bukit-Plugins/Bukit.Plugin.IndexNow/examples/minimal/.bukit/plugins.yaml`，因此为 18 个。
> 如果把这个生成配置也算进来，当前 closure 会报告 20 个未映射文件。建议映射规则覆盖全部
> 非 `bin`/`obj` 文件，避免后续再出现 19/20 的口径漂移。

### 3.2 建议的 mapping 归属

所有 IndexNow 文件归入现有专项测试项目：

```text
tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj
```

WechatSync 插件 `.csproj` 归入现有专项测试项目：

```text
tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj
```

建议对 `scripts/checks/codex-workflow-policy.v1.json` 做**独立的 mapping-only 变更**，不夹带任何业务修复：

```json
{
  "id": "bukit-indexnow",
  "matches": [
    "src/Bukit-Plugins/Bukit.IndexNow/*.cs",
    "src/Bukit-Plugins/Bukit.IndexNow/**/*.cs",
    "src/Bukit-Plugins/Bukit.IndexNow/Bukit.IndexNow.csproj",
    "src/Bukit-Plugins/Bukit.Plugin.IndexNow/*.cs",
    "src/Bukit-Plugins/Bukit.Plugin.IndexNow/**/*.cs",
    "src/Bukit-Plugins/Bukit.Plugin.IndexNow/Bukit.Plugin.IndexNow.csproj",
    "src/Bukit-Plugins/Bukit.Plugin.IndexNow/**/*.yaml",
    "src/Bukit-Plugins/Bukit.Plugin.IndexNow/**/*.md"
  ],
  "specialtyTests": [
    "dotnet test tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj"
  ],
  "contractConsumerGlobs": [],
  "publicContract": false,
  "resource": "dotnet-serial"
}
```

并在现有 `bukit-wechat-sync` 规则中加入：

```text
src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj
```

### 3.3 映射补齐的验证证据

使用上述规则的临时 policy 副本（未修改仓库文件）重新跑 closure：

```text
changed 19
unmapped 0
specialtyTests
 - dotnet test tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj
 - dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj
```

把生成的 `.bukit/plugins.yaml` 一并纳入后：

```text
changed 20
unmapped 0
```

因此 mapping-only 变更的验收标准是：

1. closure 对 19 个审计文件无 `unmappedFiles`。
2. 两个专项测试命令被归类为 `dotnet-serial`。
3. 该变更只改验证映射，不改变任何业务行为。
4. 因触及验证文件，需运行其直接 owner self-test：
   `bash scripts/checks/codex-workflow-self-test.sh`。

---

## 4. 问题总表

| 编号 | 优先级 | 领域 | 影响 | 最小修复方向 | 回归重点 |
|---|---|---|---|---|---|
| IN-01 | P1 | IndexNow key 文件 | 覆盖业务输出，通知失败也留下损坏 | 只新建或复用同内容；其他同名文件拒绝 | 同名业务文件不变 |
| WX-04 | P1 | WechatSync 缓存 | 跨公众号复用记录/素材/状态 | 缓存绑定非秘密 AppId，账号不符拒绝复用并隔离缓存 | 切 A/B 不命中旧账号 |
| WX-01 | P1 | WechatSync HTML | 属性被实体解码破坏 | 只解码标签外文本，保留属性与代码块 | 编码引号属性保持完整 |
| WX-02 | P1 | WechatSync 图片 | 大小写不同图片被合并 | 去重键区分大小写，保留路径/查询大小写 | 两张图分别上传替换 |
| WX-05 | P1 | WechatSync 输入 | 损坏输入被当作成功空任务 | 校验 manifest 字段/版本；声明 JSON 缺失明确失败 | `{}` 与缺失 JSON 必须失败 |
| IN-02 | P1 | IndexNow HTTP | 重定向导致部署验证失真 | transport 禁用自动重定向，按原始响应判断 | 302/307 不被误判成功 |
| WX-03 | P2 | WechatSync 封面 | 无封面文章不走默认图 | 无原始封面也走默认图/默认 media ID | 纯文字文章可同步 |
| IN-03 | P2 | IndexNow pending | 旧 pending 阻断新通知 | 明确跨版本 pending 更新/恢复规则 | 旧 A 不阻断新 C |

---

## 5. 按执行顺序逐项梳理

## 5.1 IN-01：key 文件覆盖业务输出

- **优先级**：P1
- **问题类型**：数据损坏 / 输出覆盖
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.IndexNow/IndexNowKeyFileWriter.cs:19-57`
  - 调用点：`src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs:91`
- **静态确认**：是。

### 触发条件

`INDEXNOW_KEY` 是合法 key（`^[A-Za-z0-9_-]+$`），且输出根目录已存在同名 `<key>.txt` 业务文件。例如：

```text
INDEXNOW_KEY=llms-full
outputRoot/llms-full.txt  已由 GEO 输出生成
```

`IndexNowKeyFileWriter.Write` 会先写临时文件，然后：

```csharp
File.Move(temporary, path, overwrite: true);
```

无条件替换目标文件。调用发生在 preflight 之前，因此即使后续页面验证、key 验证或 IndexNow 通知失败，业务文件已经被覆盖。

Core 确实会在输出根生成 `llms-full.txt`：

- `src/Bukit-Core/Bukit.Engine/PublicOutputLifecycle.cs:38`
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs:456`

因此这不是理论冲突，而是当前输出契约内的真实同名风险。

### 影响

- 业务输出被静默改写，且无法从插件状态恢复。
- 失败的通知流程也会留下损坏文件，重跑不能自动修复。
- 现有测试 `IndexNowPluginContractTests.KeyFileWriter_RejectsTargetSymlinkAndAtomicallyReplacesRegularFile`
  （`tests/Bukit.Plugin.IndexNow.Tests/IndexNowPluginContractTests.cs:631-661`）
  明确写入了 `"old"` 并断言 `Write` 后变成 key，等于把错误行为固定成回归预期。

### 根因

writer 把 `<key>.txt` 当成自己独占的可覆盖文件，没有把输出根视为共享命名空间；没有“目标已存在且内容不同”的冲突分支。

### 最小修复方向

1. 目标不存在：写临时文件后使用**不覆盖**的原子创建。
2. 目标存在且内容与 key 完全一致：直接复用，不重写。
3. 目标存在但内容不同：拒绝，返回结构化错误/诊断；不修改目标文件。
4. 保留现有路径边界、符号链接检查、临时文件清理。
5. 处理检查与创建之间的竞态：不覆盖创建失败后重新读取目标，内容一致则成功，否则冲突。
6. 可考虑使用专用诊断码（如 `plugin.indexnow.keyFileConflict`）；若坚持最小改动，至少不能把冲突伪装成普通成功。

### 必须补的回归

- 已存在 `llms-full.txt` 且内容为业务输出；key 为 `llms-full`；preflight 失败；断言文件内容逐字节不变。
- 已存在同名文件且内容恰好等于 key；断言复用成功，不产生临时文件残留。
- 目标不存在；断言正常创建。
- 目标为符号链接；断言仍被拒绝，链接目标不被触碰。
- 改写现有“覆盖普通文件”测试，使其断言“不同内容拒绝、相同内容复用”。

### 验收标准

- 任何非相同的同名业务文件都不会被修改。
- 相同内容 key 文件可复用，不重复写。
- 冲突时工作流返回失败/诊断，且不继续通知。
- 不降低现有路径/符号链接安全性。

---

## 5.2 WX-04：缓存未绑定公众号身份

- **优先级**：P1
- **问题类型**：跨账号错发 / 错误跳过 / 素材复用
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:76-84`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:162-168`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/SyncCache.cs:13-19`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/SyncCache.cs:249-288`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/SyncCache.cs:418-451`
- **静态确认**：是。

### 触发条件

同一项目、同一输出、同一 `CacheFile`，先以公众号 A 的 `AppId/AppSecret` 运行，再切换到公众号 B 运行。

### 当前行为

`RunAsync` 先解析并加载缓存：

```csharp
var cachePath = SyncCacheManager.ResolvePath(context.RootDir, options.CacheFile);
...
var cache = SyncCacheManager.LoadCache(cachePath, context.Logger);
...
var appId = ReadRequiredEnv(options.AppIdEnv);
```

缓存路径只由 `CacheFile` 决定；缓存模型只有：

```csharp
SyncCache(int Version, Dictionary<string, SyncRecord> Records)
  ThumbMediaIds
  Operations
```

没有 AppId、账号 ID 或账号命名空间。`ComputeContentHash` 也不包含 AppId。于是：

- A 的 `Records` 在 B 下内容哈希相同即可命中，B 被错误 `skipped`。
- A 的 `ThumbMediaIds` 可能被 B 复用，导致 B 文章使用 A 的素材 media ID。
- A 的 `Operations`（`DraftCreated`、`PublishSubmitted` 等）可能被 B 继续恢复。
- `Force` 也不能解决账号切换问题，只是绕过记录命中。

### 影响

- B 的应发文章被错误跳过，形成漏发。
- B 复用 A 的素材/草稿/发布状态，可能错发或中断。
- 切换回 A 时，状态已经被 B 污染，恢复语义不明确。
- 属于 P1 数据完整性问题，不是单纯缓存效率问题。

### 根因

缓存身份只绑定“项目 + 内容”，没有绑定“公众号账号”；账号信息在缓存加载之后才读取，缓存层没有机会拒绝跨账号复用。

### 最小修复方向

1. 在加载缓存之前读取非秘密 `AppId`。
2. 在缓存中持久化 `AppId`，并提升缓存版本（例如 v4）；**绝不持久化 AppSecret**。
3. 账号不符时拒绝复用，返回结构化诊断（如 `plugin.wechat-sync.cacheAccountMismatch`），不得进入 gateway 调用。
4. 切换账号使用独立缓存。推荐两者同时具备：
   - 缓存文件按 AppId 派生账号后缀/子目录；
   - 缓存内容再存 AppId 做二次校验。
5. 对升级前没有 AppId 的旧缓存，不要静默当成本账号缓存复用。建议：
   - 明确拒绝并提示用户备份/改名；或
   - 仅在用户显式确认后迁移。
6. `--cache-file` 仍限制在 `.cache/wechat-sync/` 下；账号隔离不能破坏路径边界。

### 必须补的回归

- A 运行后切换 B，相同内容不得命中 A 的 `Records`；B 必须实际同步。
- B 不得复用 A 的 `ThumbMediaIds`；断言 B 发生自己的封面上传或使用 B 的默认 media ID。
- B 不得恢复 A 的 `Operations`；尤其是 `DraftCreated` / `PublishSubmitted`。
- 切回 A 后，A 的记录/素材仍可用，且不会误用 B 的缓存。
- 缓存文件中存在 AppId，但不存在 AppSecret 明文。
- 旧版无 AppId 缓存的行为被明确测试（拒绝或显式迁移）。

### 验收标准

- 任意 AppId 切换都不会命中旧账号记录、素材或操作状态。
- 每个账号有独立、可恢复的缓存。
- 失败时是明确诊断，不是静默跳过或继续使用旧状态。

---

## 5.3 WX-01：HTML 属性被实体解码破坏

- **优先级**：P1
- **问题类型**：内容损坏 / 标签结构改变
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.WechatSyncing/ContentProcessor.cs:102-126`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/ContentProcessor.cs:646`
- **静态确认**：是。

### 触发条件

HTML 属性值中包含编码实体，例如：

```html
<a title="say &amp;quot;hi&amp;quot;">text &amp;quot;hi&amp;quot;</a>
```

这里属性中的 `&amp;quot;` 表示字面量 `&quot;`，不应变成裸引号 `"`。

### 当前行为

`DecodeHtmlEntities` 使用：

```csharp
Regex.Replace(html,
  @"(<(?:pre|code|script|style)\b[^>]*>[\s\S]*?</(?:pre|code|script|style)>)|([^<]+)",
  ...)
```

当扫描到普通开始标签的 `<` 时，第一组不匹配（不是受保护块），第二组 `[^<]+` 也不能从 `<` 开始，于是正则引擎从 `<` 后面的字符继续匹配，把标签内部当成“文本节点”。只读复现：

```text
输入: <a title="say &amp;quot;hi&amp;quot;">text &amp;quot;hi&amp;quot;</a>
匹配: 'a title="say &amp;quot;hi&amp;quot;">text &amp;quot;hi&amp;quot;'  (被当作 text)
```

因此属性中的 `&amp;quot;` 会被 `DecodeQuoteEntities` 解码成裸 `"`，可能直接破坏属性结构。`<pre>/<code>/<script>/<style>` 块被保护，但普通标签属性没有。

### 影响

- 属性值含义改变，甚至产生非法 HTML。
- 下游 WeChat 渲染、图片 alt/title、链接属性可能被破坏。
- 属于用户可见内容损坏，P1。

### 根因

`DecodeHtmlEntities` 不是 tag-aware tokenizer；它只保护了四个代码类标签，没有把“普通标签本身”排除在文本节点之外。

### 最小修复方向

1. 保持 `pre/code/script/style` 整体保护。
2. 增加“普通标签”分支，标签原样返回；只对真正的标签外文本调用 `DecodeQuoteEntities`。
3. 推荐的最小正则结构：

```text
(<(?:pre|code|script|style)\b[^>]*>[\s\S]*?</(?:pre|code|script|style)>)
|(<[^>]*>)
|([^<]+)
```

分组 1、2 原样返回，只解码分组 3。

4. 不要对属性值做任何实体归一化；属性语义由 HTML parser/后续渲染负责。
5. 保留现有文本节点的命名实体、数字实体、双重编码解码能力。

### 必须补的回归

- 带 `&amp;quot;` 的属性转换后保持完整，不出现裸引号破坏标签。
- 标签外文本中的 `&amp;quot;` 仍能正确解码为 `"`。
- `<pre>/<code>/<script>/<style>` 内文本和标签保持不变。
- 同时覆盖命名实体（`&ldquo;`）、数字实体（`&#34;`）和双重编码（`&amp;ldquo;`），防止修复只处理一个分支。

### 验收标准

- 属性值逐字节保持（除其他正常 pipeline 明确会改的属性外）。
- 文本节点实体解码行为不回退。
- 代码块不被解码。

---

## 5.4 WX-02：大小写不同的图片被错误合并

- **优先级**：P1
- **问题类型**：图片错换 / 文章含义改变
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.WechatSyncing/ContentImageProcessor.cs:48-52`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/ContentImageProcessor.cs:124-128`
  - 相关键规范化：`src/Bukit-Plugins/Bukit.WechatSyncing/Helpers.cs:361-385`
- **静态确认**：是。

### 触发条件

同一篇文章包含两个不同 URL，仅路径大小写不同：

```html
<img src="https://cdn.example.com/A.png">
<img src="https://cdn.example.com/a.png">
```

在大小写敏感的源站/文件系统中，这是两张不同图片。

### 当前行为

```csharp
var uploadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
```

去重键使用 `OrdinalIgnoreCase`，且查找发生在 URL 规范化之前：

```csharp
if (uploadMap.TryGetValue(candidateUrl, out var cachedWechatUrl))
{
    return BuildReplacedImgTag(imgTag, cachedWechatUrl, candidateUrl);
}
```

因此第二次遇到仅大小写不同的 URL 时，会直接复用第一次上传的 WeChat CDN URL，不再上传，也不会保留第二张图的差异。

### 影响

- 两张不同图片变成同一张，文章视觉/语义被改变。
- 若两张图承载不同信息（例如图表、对比图），属于内容损坏。
- 属于 P1 数据完整性问题。

### 根因

URL 去重键错误地使用了大小写不敏感比较；URL 的 scheme/host 可以大小写不敏感，但 path/query 必须保留大小写。

### 最小修复方向

1. 去重键改为区分大小写（`StringComparer.Ordinal`）。
2. 先规范化为绝对 URL，再查/存去重表，避免相对/绝对形式重复上传。
3. 规范化时只对 scheme/host 做小写，path/query 原样保留。
4. 相同 URL 仍应只上传一次。
5. 相邻风险：`NormalizeMediaSourceUrlKey` 当前保留 path 大小写但**丢弃 query**。如果两张图仅 query 不同，media cache 仍可能碰撞。建议在本项修复时明确：
   - 去重键至少覆盖 path+query；
   - media cache 键是否同步包含 query，需评估旧 `.media-index.json` 兼容性；必要时使用版本化键或先查新键再回退旧键。
6. 不要把修复扩大成重写整个图片缓存体系；先保证上传去重正确。

### 必须补的回归

- `A.png` 与 `a.png` 同时出现：断言上传两次，返回两个不同 CDN URL，替换后两个 `src` 不同。
- 同一 URL 重复出现：断言仍只上传一次。
- 仅 query 大小写/内容不同的两个 URL：按最终规则断言不会错误合并。
- 顺序保持：替换后第一张仍对应第一张，第二张仍对应第二张。

### 验收标准

- 大小写不同的图片分别上传、分别替换。
- 完全相同的 URL 仍去重。
- 不引入新的错误替换或顺序错乱。

---

## 5.5 WX-05：损坏输入被当作成功空任务

- **优先级**：P1
- **问题类型**：漏发 / 静默成功
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncInputLoader.cs:30-59`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:62-67`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncModels.cs:105-109`
  - schema：`docs/schemas/agent-manifest.v1.schema.json:6-9`
- **静态确认**：是。

### 触发条件

- `agent-manifest.json` 是 `{}` 或缺少 `schema` / `schemaVersion` / `generatedAt` / `documents`。
- manifest 中声明了某个 JSON representation，但对应文件不存在、不可读或反序列化为空。
- 所有声明条目都被跳过，最终 `context.Routed` 为空。

### 当前行为

Loader 只检查文件存在和反序列化结果非 null：

```csharp
var manifest = await JsonSerializer.DeserializeAsync(...);
if (manifest is null) { throw ...; }

var documents = manifest.Documents ?? [];
```

没有校验：

- `schema` 是否等于当前 canonical schema；
- `schemaVersion` 是否受支持；
- `generatedAt` 是否有效；
- `documents` 是否缺失（`?? []` 把缺失当成空站点）；
- 每个 document 的必需字段；
- representation 的 `kind/url`。

声明 JSON 缺失时只记 warning 并 `continue`：

```csharp
if (!File.Exists(jsonPath))
{
    logger.Warn($"plugin wechat-sync content json missing: {jsonUrl}");
    continue;
}
```

内容 JSON 反序列化为 null 时同样只 warning 并继续。

Workflow 在 planner 没有 error 且候选数为 0 时：

```csharp
if (plan.HasErrors || plan.Candidates.Count == 0)
{
    ...
    return new WechatSyncResult(!plan.HasErrors, 0, 0, 0, ...);
}
```

于是：

- `{}` manifest -> `Documents = null -> []` -> 0 候选 -> `Success = true`。
- 所有声明 JSON 缺失 -> 全部跳过 -> 0 候选 -> `Success = true`。

### 影响

- 构建输出损坏、manifest 版本不兼容、逐篇 JSON 漏生成时，插件仍报告成功。
- 运维看到“0 candidates”无法区分“合法空站点”和“输入损坏”，造成漏发。
- 属于 P1，因为会掩盖应发文章未发。

### 根因

Loader 把 manifest 当成 best-effort 可选输入，用“缺失/跳过 = 空站点”推断成功；没有把声明文件缺失和合法空集合区分开。

### 最小修复方向

1. 校验 manifest 顶层必需字段：
   - `schema` 等于当前支持的 canonical ID；
   - `schemaVersion` 在支持列表内（当前 schema 为 `1.0`）；
   - `generatedAt` 可解析；
   - `documents` 存在且非 null。
2. 校验每个 document 的必需字段和 representation 的 `kind/url`。
3. 声明了 JSON representation 但文件缺失/不可读/反序列化失败：返回结构化错误，包含路径和错误码；不要只 warning。
4. 合法空站点仍正常处理：`documents: []` 且 manifest 合法时返回成功、0 候选。
5. 推荐在进入 gateway 之前 fail-closed：输入损坏时不做部分同步，避免“部分发出 + 整体失败”的混合状态；如果选择继续处理有效条目，则结果必须为失败并带完整诊断。
6. 现有测试 fixture 使用旧的 `bukit.agent-manifest` / `1.0.0`，而 Core 当前 schema 是
   `https://bukit.dev/schemas/agent-manifest.v1.json` / `1.0`。修复时必须明确兼容策略：
   - 推荐更新 fixture 到当前 canonical 格式；
   - 若必须兼容旧格式，使用显式 allowlist 和弃用 warning，不要静默接受任意版本。
7. 保留现有路径越界和符号链接检查；不要在本项引入无界读取/超大 manifest 的新问题（属于其他审计范围）。

### 必须补的回归

- `{}` manifest：明确失败，结构化诊断，0 候选，gateway 0 调用。
- manifest 合法但 `documents: []`：成功，0 候选，无 error。
- manifest 声明 JSON 但文件缺失：明确失败，诊断包含缺失路径；gateway 0 调用。
- manifest 声明 JSON 但文件内容无效：明确失败。
- 合法的单篇/多篇输入：仍正常同步。
- 旧格式 fixture 的行为符合最终兼容策略（要么明确拒绝并提示迁移，要么显式兼容并测试）。

### 验收标准

- 损坏输入不再返回“成功、0 候选”。
- 合法空站点仍成功。
- 缺失声明文件有可定位的路径和稳定错误码。
- 不会因为跳过损坏条目而静默漏发。

---

## 5.6 IN-02：重定向导致部署验证失真

- **优先级**：P1
- **问题类型**：验证失真 / 错发 / 误判删除
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.IndexNow/IndexNowHttpClient.cs:13-30`
  - `src/Bukit-Plugins/Bukit.IndexNow/IndexNowHttpClient.cs:33-45`
  - 默认 safe client：`src/Bukit-Core/Bukit.Shared/SsrfGuard.cs:183-193`
- **静态确认**：是。

### 触发条件

- 页面 URL 返回 `302/307`，Location 指向另一个域；目标域返回 200 且 canonical 恰好等于原 URL。
- 删除 URL 返回 `302/307`，目标域返回 404/410。
- key URL 返回 `302/307`，目标域返回与 `INDEXNOW_KEY` 相同的内容。
- IndexNow POST endpoint 返回 `302`，目标域返回 200/202。

### 当前行为

`IndexNowHttpClient` 默认使用：

```csharp
_client = client ?? SsrfGuard.CreateSafeHttpClient();
```

`SsrfGuard.CreateSafeHandler()` 只配置了 `ConnectCallback`：

```csharp
public static SocketsHttpHandler CreateSafeHandler()
    => new() { ConnectCallback = SsrfSafeConnectAsync };
```

没有设置 `AllowAutoRedirect = false`。`SocketsHttpHandler` 默认会自动跟随重定向。`GetPageAsync` 只看最终响应的 `StatusCode`、body 和 canonical；`SubmitAsync` 只看最终响应状态码。没有校验最终响应 URI 是否仍是原始请求 URI。

因此：

- 页面 302 到别的域，最终 200 + canonical 匹配 -> preflight 通过。
- 删除地址 302 到别的域，最终 404/410 -> 被当成删除成功。
- key 地址 302 到别的域，最终 body 等于 key -> key proof 被接受。
- POST 302 到别的域，最终 200/202 -> 被当成 `Received`。

### 影响

- 部署验证可能验证的是别的域，而不是目标站点。
- 删除/新增/更新状态可能被错误推进，导致错误通知或漏通知。
- key 验证失真会让 IndexNow 提交失败或使用错误 key proof。
- 属于 P1。

### 根因

重定向策略被委托给通用 safe client，而该 client 默认允许自动重定向；transport 没有把“原始响应”作为验证对象，也没有 final URI 断言。

### 最小修复方向

1. `IndexNowHttpClient` 自己创建 handler 时显式：

```csharp
var handler = SsrfGuard.CreateSafeHandler();
handler.AllowAutoRedirect = false;
_client = new HttpClient(handler, disposeHandler: true);
```

2. 对注入的 `HttpClient`：调用方负责不自动重定向；transport 仍应在响应后校验
   `response.RequestMessage?.RequestUri` 与请求 URI 一致（scheme/host/path/query）。
   如果不一致，返回失败/不可用，而不是接受最终状态。
3. 同一个 client 同时用于 GET 和 POST，必须确保 POST 也不自动跟随（尤其 301/302/303 可能改变方法）。
4. 不改变 SSRF 防护；仍通过 `SsrfGuard.SsrfSafeConnectAsync` 连接。
5. 不建议在 Core 中修改 `SsrfGuard.CreateSafeHttpClient` 的默认行为，以免影响其他消费者；修复应限制在插件 transport。

### 必须补的回归

> 关键：不能只用 `FakeTransport`。`FakeTransport` 在 `IIndexNowTransport` 层返回预设状态，完全绕过 `HttpClient` 重定向。

建议新增 `IndexNowHttpClientTests`：

- 用自定义 `HttpMessageHandler`（或本地 HTTP server）返回 `302` / `307` + Location 到另一个 host。
- 直接调用 `IndexNowHttpClient.GetPageAsync`，断言返回原始 `302/307`，而不是跟随后的 200。
- 断言 `PreflightAsync` 对 added/updated 页面 302 返回失败。
- 断言 deleted 页面 302 不被当作 404/410 成功。
- 断言 key URL 302 不被当作 200/key proof 成功。
- 断言 `SubmitAsync` 对 302/307 不返回 200/202 `Received`。
- 如果 transport 支持注入 client，测试注入一个会跟随重定向的 client，验证 final URI 断言能拒绝；同时测试默认构造路径不跟随。
- 保留现有 `FakeTransport` 工作流测试，用于 pending/retry/状态机逻辑，不替代 transport 重定向测试。

### 验收标准

- 任何 302/307 都不会被误判为部署成功、删除成功、key 成功或提交通知成功。
- 验证基于原始响应；最终 URI 不一致时失败。
- SSRF 防护不回退。

---

## 5.7 WX-03：无封面文章不使用默认图片

- **优先级**：P2
- **问题类型**：功能中断 / 批次恢复
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.WechatSyncing/ThumbResolver.cs:40-48`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/ThumbResolver.cs:135-145`
  - `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncModels.cs:58`
  - `src/Bukit-Plugins/Bukit.Plugin.WechatSync/WechatSyncPluginOptionsMapper.cs:125`
- **静态确认**：是。

### 触发条件

文章没有 `cover` 字段，正文也没有可用首图（纯文字文章），但配置了有效的 `DefaultImageUrl`，没有配置 `DefaultThumbMediaId`。

### 当前行为

`ResolveAndUploadThumbAsync`：

```csharp
var thumbSource = ResolveThumbSource(item, cfg);
if (string.IsNullOrWhiteSpace(thumbSource))
{
    if (!string.IsNullOrWhiteSpace(cfg.DefaultThumbMediaId))
    {
        return (cfg.DefaultThumbMediaId.Trim(), false);
    }

    throw new InvalidOperationException("wechat-sync requires cover, first image in content, or wechat.defaultThumbMediaId.");
}
```

无封面时只检查 `DefaultThumbMediaId`，不会调用后面的 `TryUploadDefaultImageAsync`。默认图片 fallback 只在“有 thumbSource，但本地/下载/媒体缓存都失败”时才会到达（`ThumbResolver.cs:135-145`）。

### 影响

- 纯文字文章即使配置了有效默认图，也会直接抛错。
- 该异常会中断当前候选，严重时使整批处理停在错误上，后续文章不能继续。
- 属于 P2 功能/恢复问题。

### 根因

“无原始封面”被当作不可恢复错误，绕过了已有的默认图片/默认 media ID 回退链。

### 最小修复方向

1. 当 `thumbSource` 为空时，先调用现有 `TryUploadDefaultImageAsync(...)`。
2. 该函数本身已经会回退到 `DefaultThumbMediaId`，因此不要重复实现默认图逻辑。
3. 返回 `(mediaId, cacheUpdated)`；默认图上传成功时 `cacheUpdated = true`。
4. 只有默认图上传失败且没有 `DefaultThumbMediaId` 时，才抛出明确错误。
5. 保持“有原始封面但加载失败”的现有回退顺序不变。

### 必须补的回归

- 无封面、无正文图片、仅配置 `DefaultImageUrl`：同步成功，封面来自默认图，内容正常提交。
- 无封面、仅配置 `DefaultThumbMediaId`：仍成功（现有行为）。
- 批次中第一篇纯文字、第二篇有封面：两篇都处理，第一篇失败不再中断第二篇。
- 既无封面也无任何默认回退：仍返回明确错误，不静默成功。

### 验收标准

- 纯文字文章在配置有效默认图时可以同步。
- 默认 media ID 回退不回退。
- 无任何回退时错误清晰，不产生半同步状态。

---

## 5.8 IN-03：旧 pending 阻断新通知

- **优先级**：P2
- **问题类型**：恢复/重试状态机
- **涉及位置**：
  - `src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs:60-75`
  - `src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs:338-344`
  - 外层错误处理：`src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs:217-220`
- **静态确认**：是。
- **实现状态**：**已按策略 A（保守丢弃）实施**；若部署基线要求重试旧 pending，可后续切换为策略 B。

### 触发条件

上一次运行留下 pending A（通常来自旧版本/旧基线）。本次：

- 当前 change set 不包含 A；
- 当前 snapshot 中 A 已不存在，或 A 的 semanticHash 已变化；
- 本次有一个合法的新变化 C。

### 当前行为

```csharp
var currentUrls = changes.Select(change => change.Url).ToHashSet(StringComparer.Ordinal);
...
var recoveredPending = ValidateChanges(
    new PublishUrlChangeSetDocument(
        state.Pending.Where(change => !currentUrls.Contains(change.Url)).ToArray()),
    snapshot)
  ...
```

`ValidateChanges` 对 `added/updated` 要求：

```csharp
if (!routes.TryGetValue(change.Url, out var route) ||
    !string.Equals(route.SemanticHash, change.SemanticHash, StringComparison.Ordinal))
{
    throw new InvalidOperationException("Added or updated change must match the candidate snapshot.");
}
```

因此只要旧 pending A 是 `added/updated`，且：

- A 已不在当前 snapshot；或
- A 的 hash 与当前 snapshot 不同；

就会在恢复阶段抛 `InvalidOperationException`，被外层捕获为 `plugin.indexnow.invalidInput`，整个 `RunAsync` 返回失败，合法的新变化 C 也不会提交。

同 URL 的“当前变化覆盖旧 pending”已有测试覆盖
（`IndexNowPluginContractTests.Workflow_CurrentChangeReplacesStalePendingForTheSameUrl`），
但“旧 pending 不在当前 change set / 已老化”的场景没有覆盖。

### 影响

- 跨版本后旧 pending 可以让所有新通知卡死。
- 运维只能手工删除/编辑 `state.json`，恢复过程不可审计。
- 属于 P2 恢复问题，但会直接造成漏发。

### 根因

恢复流程把“旧基线 pending”当成“必须与当前 snapshot 完全匹配的输入”来硬校验；没有区分：

- 结构非法/不可信状态（必须 fail closed）；
- 结构合法但已过时/被替代的 pending（应清理或重基，不应整体失败）。

### 最小修复方向（需先对齐部署基线）

必须先把规则写清楚，再实现。建议拆成两层：

#### A. 结构安全校验（保留，不可放宽）

在任何网络/写操作前校验：

- `type` 只能是 `added/updated/deleted`；
- URL 必须通过 `IndexNowUrlPolicy.ParseContentUrl`；
- `semanticHash` 必须是合法 `sha256:`；
- state 其他字段的现有校验保留。

结构非法 -> 继续 fail closed，返回 `plugin.indexnow.invalidInput`，不写状态、不联网。

#### B. 基线协调（新增规则）

当前 change set 已包含的 URL：当前变化优先（现有行为）。

当前 change set 不包含的 pending：

| pending 类型 | 当前 snapshot 状态 | 建议动作 |
|---|---|---|
| added/updated | URL 存在且 hash 相同 | 保留，继续重试 |
| added/updated | URL 不存在或 hash 不同 | 视为 superseded，丢弃或按基线规则重基 |
| deleted | URL 不存在 | 保留，继续重试删除 |
| deleted | URL 存在（复活） | 视为 superseded，丢弃 |

两种可选策略，需要和部署基线规则二选一：

- **策略 A（保守丢弃）**：只重试 fingerprint 仍与当前 snapshot 一致的 pending；其余标记 superseded 并丢弃，记录 warning，继续提交 C。优点是避免发送非本次变更的通知；缺点是如果 pending 对应“已部署但未通知”的页面，可能永远不再自动重试。
- **策略 B（重基）**：如果 pending URL 仍在当前 snapshot 且仍 indexable，把 pending 的 semanticHash 更新为当前 snapshot hash，保留并重试；URL 不存在则丢弃。优点是保留恢复能力；缺点是可能为“不在本次 change set”的 URL 发送通知。

推荐默认：**策略 A**，因为 IndexNow 通知是幂等的 URL 提交，但“非本次变更”的通知仍应尽量少发；如果部署基线能证明 pending 一定是“已部署且必须通知”的集合，再选策略 B。

无论选哪种：

- 旧 pending 不得让整个 run 失败。
- 状态清理结果必须持久化；即使本次没有 actionable，也要保存 pending 变化。
- 诊断应为 warning/信息，而不是把合法新变化一起判失败。
- 如果要保留审计轨迹，可增加 superseded 列表/代次字段；这会引入 state schema 版本变更，需单独评估，不要和最小修复混在一起。

### 必须补的回归

- pending A（added/updated，旧 hash），当前 snapshot 无 A，change set 只有合法 C：
  - C 被提交；
  - A 不再出现在 active pending（或按策略 B 被重基）；
  - 结果成功；
  - 清理后的 state 已持久化。
- pending A（added/updated，旧 hash），当前 snapshot 有 A 但 hash 已变，change set 只有 C：
  - C 被提交；
  - A 按选定策略丢弃/重基，不阻断。
- pending 结构非法（bad type / bad URL / bad hash）：仍然失败、无网络、无写状态（保留现有安全测试）。
- 当前 change set 与 pending 同 URL：当前变化优先（保留现有测试）。
- 多次跨版本运行：pending 不会无限累积；每轮状态收敛。

### 验收标准

- 旧 pending 老化后不阻断合法新变化 C。
- 结构非法状态仍 fail closed。
- 清理/重基规则有明确文档和测试，不依赖手工删 `state.json`。
- 与部署基线规则的语义一致，不产生“应通知但永远不通知”的新漏洞。

---

## 6. 回归测试矩阵

### 6.1 测试归属

| 组 | 专项测试项目 | 说明 |
|---|---|---|
| IndexNow | `tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj` | 同时引用 `Bukit.IndexNow` 与 `Bukit.Plugin.IndexNow` |
| WechatSync | `tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj` | 同时引用 `Bukit.WechatSyncing` 与 `Bukit.Plugin.WechatSync` |

本轮不运行测试；实施阶段使用 policy 中的精确命令：

```text
dotnet test tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj
dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj
```

### 6.2 建议的测试落点

以下是建议的新增/扩展位置，不是本轮已创建的测试：

| 问题 | 建议测试文件 | 类型 | 关键断言 |
|---|---|---|---|
| IN-01 | `IndexNowKeyFileWriterTests.cs`（或现有 contract tests 扩展） | 文件系统单元 + workflow | 同名业务文件不变；相同内容复用；冲突拒绝；符号链接仍拒绝 |
| WX-04 | `WechatSyncAccountCacheTests.cs`（或 state machine tests 扩展） | workflow + cache | A/B 不共享 Records/ThumbMediaIds/Operations；无 AppSecret |
| WX-01 | `ContentProcessorTests.cs`（当前缺失） | 纯函数/内容 pipeline | 属性 `&amp;quot;` 不被解码；文本实体仍解码；代码块不变 |
| WX-02 | `WechatP104InlineImageTests.cs` 扩展 | ContentImageProcessor + fake gateway | `A.png`/`a.png` 上传两次、替换两个不同 URL；同 URL 仍一次 |
| WX-05 | `WechatSyncInputLoaderTests.cs` + `WechatSyncPluginInvokeCompatibilityTests.cs` | loader + invoker | `{}` 失败；缺失 JSON 失败；合法空 manifest 成功 |
| IN-02 | `IndexNowHttpClientTests.cs`（新增） | 真实 transport（自定义 handler/本地 server） | 302/307 原样返回；不跟随；不误判成功；final URI 校验 |
| WX-03 | `WechatSyncWorkflowTests.cs` 扩展 | workflow + ThumbResolver | 无封面 + DefaultImageUrl 成功；批次继续 |
| IN-03 | `IndexNowPendingRecoveryTests.cs`（或 contract tests 扩展） | workflow + state | 旧 A 不阻断 C；结构非法仍失败；状态清理持久化 |

### 6.3 现有测试必须同步调整

1. **IN-01**：`IndexNowPluginContractTests.KeyFileWriter_RejectsTargetSymlinkAndAtomicallyReplacesRegularFile`
   当前断言“已有普通文件会被覆盖”。修复后必须改为“不同内容拒绝、相同内容复用”，否则测试会阻止正确修复。
2. **WX-05**：现有 loader/invoker fixture 使用 `bukit.agent-manifest` / `1.0.0`；
   需要按最终兼容策略更新 fixture 或显式测试 legacy allowlist。
3. **WX-04**：缓存版本从 3 提升后，多个测试直接构造 `new SyncCache(3, ...)` 或断言 `Version == 3`；
   需要统一迁移到新版本/新构造函数，避免测试锁定旧缓存格式。
4. **IN-02**：现有 `FakeTransport` 测试保留，但不能作为重定向回归的唯一证据。

### 6.4 验证顺序

每个任务只跑其直接专项测试；不要混入全量/发布 gate：

```text
mapping-only
  -> closure（0 unmapped）
  -> bash scripts/checks/codex-workflow-self-test.sh

IN-01
  -> IndexNow specialty tests

WX-04
  -> WeChat specialty tests

WX-01 / WX-02 / WX-05
  -> 可并行准备，但写操作串行；每个修复后跑 WeChat specialty tests

IN-02
  -> IndexNow specialty tests（必须包含真实 transport 重定向测试）

WX-03 / IN-03
  -> 各自 specialty tests；IN-03 先完成基线决策
```

---

## 7. 修复顺序、依赖与闸门

### 7.1 先决：mapping-only

- 只修改 `scripts/checks/codex-workflow-policy.v1.json`。
- 加入 IndexNow 规则和 WechatSync `.csproj` 匹配。
- 运行 `closure` 验证 0 unmapped。
- 运行 `bash scripts/checks/codex-workflow-self-test.sh`（验证文件的直接 owner self-test）。
- 不与任何业务修复混合提交。

### 7.2 Phase 1：P1

按用户给定顺序：

1. **IN-01**：先消除最直接的数据覆盖；改动集中在 `IndexNowKeyFileWriter` 和调用点诊断。
2. **WX-04**：缓存身份隔离；涉及 `WechatSyncWorkflow`、`SyncCache` 及缓存版本，需先定迁移策略。
3. **WX-01 / WX-02 / WX-05**：同为 WeChat 输入/内容正确性，但修改点不同，可以按文件边界拆分；写操作仍串行。
4. **IN-02**：IndexNow transport 重定向；补真实 transport 测试。

### 7.3 Phase 2：P2

5. **WX-03**：复用已有默认图 fallback，改动小。
6. **IN-03**：已按策略 A 实现；部署基线若要求重试旧 pending，再单独评估策略 B。

### 7.4 任务边界与证据

- 每个任务开始前用 `python3 scripts/checks/codex-workflow.py closure` 生成该任务的 changed files、直接消费者、公共/序列化合同消费者和精确专项命令。
- 只运行该任务明确要求的专项测试；不运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、release/full gate。
- 每个普通实现任务完成后做一次 specialty review；只有 Critical/Important 进入修复和定向复审。
- 全部实现任务结束后生成一次 `review-scope`，做一次 delta-only 统一复核；复用未失效的 specialty 证据。
- 若发现 Core 缺陷，只报告证据和建议修复，不在本组任务中修改 Core。

---

## 8. 待决策与风险

### 8.1 IN-03：部署基线规则（当前按策略 A 实施，待基线确认）

必须回答：

1. `publish-url-snapshot.json` 是否总是代表“刚刚部署的版本”，还是仅代表“本次构建候选”？
2. pending 的“老化”定义是什么：按 snapshot 代次、时间 TTL，还是被后续 change set 覆盖？
3. 旧 pending 对应“已部署但未通知”的页面时，是否必须自动重试？如果是，选择策略 B（重基）；如果不是，选择策略 A（丢弃/标记 superseded）。
4. 是否需要保留 superseded 审计记录？如果需要，state schema 是否升版？
5. 旧 pending 与当前 change set 同 URL 时，当前变化优先是否始终成立？

当前实现选择策略 A：旧 pending 与当前 snapshot 不一致时丢弃并记录 warning，不阻断新变化。若后续确认部署基线要求“已部署但未通知”的旧 pending 必须重试，再切换为策略 B（重基），并补充对应回归。

### 8.2 WX-05：manifest 兼容策略

当前 Core 输出 canonical schema `https://bukit.dev/schemas/agent-manifest.v1.json` / `1.0`，
但现有测试 fixture 使用 `bukit.agent-manifest` / `1.0.0`。需要决定：

- 只接受当前 canonical（推荐，更新 fixture）；或
- 显式兼容旧格式并标记 deprecated。

不要静默接受任意 `schema` / `schemaVersion`，否则修复会重新制造“损坏输入被当成功”的入口。

### 8.3 WX-04：缓存迁移

推荐账号隔离缓存，但需要决定：

- 旧的无 AppId 缓存是拒绝、备份后迁移，还是显式确认后迁移；
- `--cache-file` 的自定义路径如何附加账号后缀；
- artifact 路径变化是否影响外部脚本/报告。

保守原则：宁可拒绝旧缓存并要求人工确认，也不要跨账号自动复用。

### 8.4 WX-02：媒体缓存键兼容

去重表使用 case-sensitive key 是最小修复。若同时修正 media cache 的 query/大小写键，需要考虑旧 `.media-index.json` 和 hash 文件名的兼容：

- 可以先查新键，未命中再回退旧键；
- 或接受一次重新上传（安全，但增加网络/素材操作）；
- 不要把“去重正确”与“缓存完全迁移”捆绑成一个大改动。

### 8.5 IN-01：文件系统竞态

不覆盖创建仍存在“检查后、创建前被其他进程占用”的竞态。实现时应：

- 使用不覆盖的原子创建；
- 失败后重新读取并比较内容；
- 相同内容视为成功，不同内容视为冲突；
- Windows 大小写不敏感路径需保持现有 `PathComparison` 逻辑。

### 8.6 IN-02：注入 client 的责任

默认构造路径必须禁用自动重定向。对于调用方注入的 `HttpClient`：

- 不能假设其 `AllowAutoRedirect` 为 false；
- 应在响应后校验 final URI；
- 或在 API 上明确要求注入 client 必须禁用重定向，并加测试。

---

## 9. 证据附录

### 9.1 基线

```text
git log -1 --format='%H%n%ci%n%s'
712bdc0351c419063add8e9779bd8ef659e20e02
2026-11-04 15:17:53 +0800
docs(release): reconcile 2.0.7 changelogs and release evidence (#89) [skip ci]
```

### 9.2 当前 closure（19 个审计文件）

```text
changed 19
unmapped 19
specialtyTests []
```

完整未映射列表见第 3.1 节。

### 9.3 临时 mapping policy 验证

使用未写入仓库的临时 policy 副本，加入第 3.2 节规则后：

```text
changed 19
unmapped 0
specialtyTests
 - dotnet test tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj
 - dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj
resources [{'command': 'dotnet test tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj', 'resource': 'dotnet-serial'}, {'command': 'dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj', 'resource': 'dotnet-serial'}]
```

### 9.4 WX-01 正则行为复现

```text
输入: <a title="say &amp;quot;hi&amp;quot;">text &amp;quot;hi&amp;quot;</a>
匹配: 'a title="say &amp;quot;hi&amp;quot;">text &amp;quot;hi&amp;quot;'  protected=False text=True
匹配: '/a>'  protected=False text=True
```

说明普通标签内部被错误归入“文本节点”，因此属性实体被解码。

### 9.5 关键源码位置

```text
IN-01  src/Bukit-Plugins/Bukit.IndexNow/IndexNowKeyFileWriter.cs:19-57
IN-01  src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs:91
IN-02  src/Bukit-Plugins/Bukit.IndexNow/IndexNowHttpClient.cs:13-45
IN-02  src/Bukit-Core/Bukit.Shared/SsrfGuard.cs:183-193
IN-03  src/Bukit-Plugins/Bukit.IndexNow/IndexNowSubmissionWorkflow.cs:60-75,338-344
WX-01  src/Bukit-Plugins/Bukit.WechatSyncing/ContentProcessor.cs:102-126,646
WX-02  src/Bukit-Plugins/Bukit.WechatSyncing/ContentImageProcessor.cs:48-52,124-128
WX-03  src/Bukit-Plugins/Bukit.WechatSyncing/ThumbResolver.cs:40-48,135-145
WX-04  src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:76-84,162-168
WX-04  src/Bukit-Plugins/Bukit.WechatSyncing/SyncCache.cs:13-19,249-288,418-451
WX-05  src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncInputLoader.cs:30-59
WX-05  src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncWorkflow.cs:62-67
WX-05  docs/schemas/agent-manifest.v1.schema.json:6-9
```

### 9.6 现有测试冲突/缺口

```text
IN-01  tests/Bukit.Plugin.IndexNow.Tests/IndexNowPluginContractTests.cs:631-661
       （当前断言覆盖已有普通文件，需改写）

IN-02  tests/Bukit.Plugin.IndexNow.Tests/IndexNowPluginContractTests.cs:765-815
       （FakeTransport 只覆盖业务层，不覆盖 HttpClient 重定向）

WX-01  当前 WechatSync 测试项目没有 ContentProcessor 专项测试类

WX-02  当前没有 A.png / a.png 大小写去重回归

WX-05  tests/Bukit.Plugin.WechatSync.Tests/WechatSyncInputLoaderTests.cs
       tests/Bukit.Plugin.WechatSync.Tests/WechatSyncPluginInvokeCompatibilityTests.cs
       （fixture 使用旧式 manifest schema/version）
```

---

## 10. 本轮未做事项

- 未修改任何生产代码。
- 未修改任何测试或 fixture。
- 未修改 `scripts/checks/codex-workflow-policy.v1.json`；mapping 只是建议和临时 policy 验证。
- 未运行 `dotnet test` 或任何全量/专项测试。
- 未运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、release/full gate。
- 未做远程 IndexNow/WeChat 调用。
- 未重新审计 Import。
- 未对 IN-03 的部署基线规则做最终裁决。

本报告第 0 节记录本轮执行结果，第 11 节为残留风险分析；验证结论以具体测试输出和 closure 证据为准。

---

## 11. 残留风险深挖（IN-01 / WX-02）

### 11.1 IN-01：Unix `File.Move(overwrite:false)` 的并发 rename 窗口

#### 结论

该风险曾真实存在，但只在**并发写入同一目标文件**时触发。确定性的“同名业务文件被覆盖”先被修复；剩余的 check-then-create 竞态已在本轮采用方案 A 消除：直接以 `FileMode.CreateNew` 写最终文件，不再使用 `File.Move` 的 rename 路径。以下保留竞态分析、触发条件和方案比较，作为设计依据。

#### 运行时行为证据（修复前的根因）

.NET 的 Unix 实现在 `FileSystem.MoveFile(source, dest, overwrite:false)` 中：

1. 先 `LStat(source)`、`LStat(dest)`。
2. 如果 `dest` 不存在，直接调用 `Rename(source, dest)`。
3. `rename(2)` 在目标已存在时会**原子替换**，不会返回 `EEXIST`。
4. 只有在 `LStat(dest)` 已看到目标存在时，才走 `LinkOrCopyFile`；此时 `link(2)` 会以 `EEXIST` 失败，因此不会覆盖。

对应源码：
`https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs`
中的 `FileSystem.MoveFile`。

#### 修复后的代码路径

- `src/Bukit-Plugins/Bukit.IndexNow/IndexNowKeyFileWriter.cs:36-46`：先检查目标是否存在；相同内容直接复用，不同内容直接拒绝。
- 目标不存在时，直接打开最终路径：

```csharp
using var stream = new FileStream(
    path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
    4096, FileOptions.WriteThrough);
stream.Write(keyBytes);
stream.Flush(flushToDisk: true);
```

- `FileMode.CreateNew` 使用 `O_CREAT|O_EXCL`，目标在任意时刻出现都会让创建失败，不存在“先检查后 rename”的窗口。
- 创建失败且目标已存在时重新比较：相同内容视为并发同 key 成功，不同内容返回冲突。
- 创建成功后写入失败会删除本次创建的不完整文件，并保留原始异常。
- 测试覆盖：
  - `KeyFileWriter_RejectsTargetCreatedAfterPrecheckWithoutOverwriting`：在预检查之后、创建之前插入同名业务文件，断言不覆盖。
  - 原有相同内容复用、不同内容拒绝、符号链接拒绝、workflow 不覆盖业务文件测试继续保留。

#### 修复前的竞态窗口（仅作历史证据）

```text
IndexNow: LStat(dest) -> 不存在
其他进程: create(dest) 写入 llms-full.txt
IndexNow: rename(temp, dest) -> 原子替换，成功返回
IndexNow: 不进入 IOException 分支，也不比较内容
```

结果：业务文件被 key 内容替换，且没有任何异常或诊断。

#### 触发场景

1. 构建/部署进程与 IndexNow 插件同时写同一个输出根；例如构建正在生成 `llms-full.txt`，插件同时写 `llms-full` key。
2. 两个 IndexNow 进程使用不同 key，但在大小写不敏感文件系统（如 macOS 默认 APFS）上映射到同一个文件名；或同一 key 的环境值在并发运行间变化。
3. 当前 `.cache/indexnow` 运行锁只串行化 IndexNow 状态访问，**不保护输出根**，所以不能排除构建或其他进程。

#### 影响与等级

- 影响：业务输出被 key 内容覆盖，若随后部署会把损坏文件发布出去。
- 概率：低；需要并发写同名文件。
- 综合：**影响 P1，触发概率 P3**；正常单写者构建/部署路径不会触发。

#### 可选修复

**方案 A（已实施）：直接用 `FileMode.CreateNew` 写最终文件**

```csharp
using var stream = new FileStream(
    path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
    4096, FileOptions.WriteThrough);
stream.Write(keyBytes);
stream.Flush(flushToDisk: true);
```

- 优点：`O_CREAT|O_EXCL` 原子创建，目标已存在必然失败；跨平台。
- 缺点：进程在写入中途崩溃会留下空的/不完整的 key 文件；下次运行会因内容不同而拒绝覆盖，需要人工删除。不会损坏业务文件，只会让 key 文件进入 fail-closed 状态。
- key 内容很短，一次写入加 flush 后崩溃窗口很小。

**方案 B：平台级原子 no-replace**

- Unix：写临时文件后用 `link(2)` 创建最终名；`EEXIST` 即冲突；成功后删除临时文件。
- Windows：`MoveFileEx` 不带 `MOVEFILE_REPLACE_EXISTING`。
- 优点：同时保证“内容完整”和“绝不覆盖”。
- 缺点：需要平台分支/P/Invoke；部分文件系统不支持硬链接，需要 fallback。

**方案 C：保留现状并声明单写者约束**

- 如果 Bukit 的构建/部署契约保证输出根同一时刻只有一个写者，当前实现可以接受。
- 需要把“输出根不得并发写”写入运维/CI 约束，否则该竞态仍可能发生。

#### 建议

- 当前实现已采用方案 A，并发写输出根时不会再由插件覆盖已存在的业务文件。
- 方案 B 仍可作为未来增强：如果还需要保证“进程崩溃时 key 文件内容完整”，可在 Unix 上使用 `link(2)`、Windows 上使用 no-replace `MoveFileEx`；当前方案 A 在崩溃时只会留下不完整 key 文件并 fail closed，不会损坏业务输出。
- 不要采用“move 后再读回比较”，因为覆盖已经发生，读回无法恢复业务文件。
- 回归应继续覆盖：
  - 目标在检查后、创建前出现：断言 `CreateNew` 失败且目标内容不变。
  - 目标不存在：正常创建并写入完整 key。
  - 崩溃/半写场景：下次运行拒绝覆盖并给出明确冲突错误。

---

### 11.2 WX-02：media cache 新 key 包含 query，旧 key 没有回退

#### 结论

当前“不回退旧 key”的行为是**有意的安全选择**，不是必须修复的缺陷。它会把部分旧缓存命中退化为重新下载，但不会错误合并图片。

#### key 的变化

`src/Bukit-Plugins/Bukit.WechatSyncing/Helpers.cs:361-385`：

```text
旧 key = scheme://host[:port]/path
新 key = scheme://host[:port]/path + uri.Query
```

- 对于**没有 query** 的 URL：新旧 key 完全相同，旧缓存仍然命中。
- 对于**有 query** 的 URL：旧 key 丢弃 query，无法区分 `?v=A` 与 `?v=a`；新 key 保留 query。

#### 为什么不应回退旧 key

旧 key 是有歧义的：

```text
https://cdn.example.com/image.png?v=A
https://cdn.example.com/image.png?v=a
```

在旧 key 下都变成：

```text
https://cdn.example.com/image.png
```

如果新 key 未命中时回退旧 key，可能把 `?v=A` 的缓存文件当成 `?v=a` 的图片，重新引入 WX-02 要修复的“错误合并”。旧 `.media-index.json` 只存 key → 文件名，没有保存原始 URL，无法在回退时验证文件到底属于哪个 query 变体。

#### 影响

- query URL：旧缓存未命中 → 回退到 HTTP 下载/上传；若源不可达，则保留原 `<img src>`，不会替换成错误图片。
- 无 query URL：无影响。
- 当前 tree 中 Core 的 `.media-index.json` 使用 `v3:<sha256>` key（`ImageAssetLocalizer.cs:761-768`），与插件 `NormalizeMediaSourceUrlKey` 的格式本来就不一致；插件对当前 Core 索引的命中依赖本地路径/HTTP fallback。这个不一致是既有问题，不是本次 query 变更引入的。

#### 历史兼容性

历史提交 `a6741cc5` 前后的 Core 曾使用与旧插件相同的 query-less `scheme://host/path` key；因此旧 `.media-index.json` 中确实可能存在 query-less 条目。但：

- 对无 query URL，新 key 与旧 key 相同，仍然可用。
- 对有 query URL，旧条目本身可能对应另一个 query 变体，不能安全复用。

#### 可选处理

1. **保持现状（推荐）**：miss 后重新下载，保证正确性。
2. 旧 key fallback：不推荐，会重新引入 query 变体错误合并。
3. “仅当 URL 无 query 时 fallback”：无实际效果，因为此时新旧 key 相同。
4. 新索引格式：在 `.media-index.json` 中同时保存原始 URL 或 query identity，回退前验证；这是更大范围的缓存迁移，不适合当前最小修复。

#### 建议（已实施）

- 保留当前行为，并已在 `Helpers.NormalizeMediaSourceUrlKey` 加注释说明“不回退旧 query-less key，避免把不同 query 变体映射到同一张图”。
- 已补回归：`WechatP104InlineImageTests.ProcessImagesAsync_DoesNotReuseLegacyQuerylessMediaIndexEntryForQueryUrl`。当 `.media-index.json` 只有旧 query-less key 时，带 query 的 URL 不命中旧文件，而是走下载；下载失败则保留原图。
- 如果未来要优化旧缓存命中率，应先升级索引格式保存原始 URL，再做安全迁移；不要直接加旧 key fallback。

---

### 11.3 两个残留项的最终判断

| 项目 | 是否必须修复 | 理由 | 建议 |
|---|---|---|---|
| IN-01 Unix rename 竞态 | 已修复（方案 A） | 已改为 `FileMode.CreateNew` 直接写最终文件；并发写输出根时不再覆盖已存在业务文件 | 保留并发创建测试；崩溃半写场景 fail closed |
| WX-02 旧 media key 无回退 | 不需要 | 回退会重新引入 query 变体错误合并；无 query URL 不受影响 | 已保留现状并补注释/回归，防止未来误加 fallback |

---

## 12. 审计复核：旧 v3 缓存升级会导致重复同步

### 12.1 审计结论确认

> **实施状态（2026-11-04）：已修复。** 采用方案 A：`--migrate-legacy-cache` + `--confirm-legacy-cache-app-id`，迁移保留 `Records`、`ThumbMediaIds`、`Operations`，旧缓存保留作备份。WechatSync 专项 306/306 通过。

审计对象：`75545ec8` + `8cae4fbb`。

结论：**审计发现成立，属于 Important，且比“额外创建 1 份草稿”更广。**

当前实现：

- `WechatSyncWorkflow.cs:70-76`：发现旧 `.cache/wechat-sync/sync-cache.json`（v2/v3，无 AppId）且账号缓存不存在时，直接抛出错误，提示“备份、恢复 submitting 操作、删除或归档后重试”。
- `SyncCacheManager.LoadCache` 在 `expectedAppId` 非空时拒绝所有非 v4/无 AppId 的缓存。
- `guide/dev/plugins.md:86-88` 也建议备份后删除/归档。

因此用户按提示归档旧缓存后，新版会在账号路径创建**空 v4 缓存**。旧缓存中的 `Records`、`ThumbMediaIds`、`Operations` 全部丢失。

### 12.2 影响范围

| 旧缓存内容 | 归档后的后果 |
|---|---|
| `Records` 成功记录 | 相同内容不再命中，重新创建草稿；发布模式可能重新发布 |
| `ThumbMediaIds` | 封面/默认图重新上传，浪费素材操作；若素材接口受限会中断 |
| `Operations: DraftCreated` | 不再复用已有草稿，重新创建草稿 |
| `Operations: PublishSubmitted` | 丢失 publishId，可能重复发布 |
| `Operations: DraftSubmitting` | 丢失 recovery guard，可能重复提交草稿 |
| `Operations: PublishSubmitting` | 丢失 recovery guard，可能重复发布 |

审计报告只提到“旧成功记录迁移后不重复提交”，但 `Operations` 的丢失更危险：它会把原本必须人工恢复的 unknown outcome 变回“可以自动提交”。

### 12.3 根因

当前升级路径只有“拒绝旧缓存”和“归档后空缓存启动”两种状态，没有“把旧缓存绑定到当前 AppId 并迁移”的第三种状态。

旧 v3 缓存没有 AppId，无法自动判断它属于哪个公众号；因此不能自动迁移，但也不能只让用户归档后重试。

### 12.4 最小修复方向

建议增加一次性、人工确认 AppId 的迁移路径，而不是继续建议归档重试。

#### 方案 A（已实施）：显式迁移 flag

在现有 `wechat-sync sync` 之外增加一次性迁移能力（独立子命令或显式 flag 均可），要求用户同时提供/确认：

- 当前 `AppId`（非秘密，来自配置的 AppId 环境变量）；
- 明确的迁移意图，不能是默认行为。

建议参数形态（二选一）：

```text
bukit wechat-sync migrate-cache --output dist --confirm-app-id <current-app-id>
```

或：

```text
bukit wechat-sync sync ... --migrate-legacy-cache --confirm-legacy-app-id <current-app-id>
```

迁移步骤：

1. 解析 `CacheFile` 得到旧 base path：`.cache/wechat-sync/sync-cache.json`。
2. 用现有 `SyncCacheManager.LoadCache(basePath, logger)` 读取 v2/v3；保留其全部结构校验。
3. 如果旧缓存已经是 v4/有 AppId：拒绝迁移（应走正常账号路径）。
4. 如果目标账号路径已经存在：拒绝迁移，**不要自动合并**；让用户先备份/对比/决定。
5. 校验用户确认的 AppId 与当前运行 AppId 一致；不一致直接失败。
6. 生成 v4 缓存：
   - `Version = 4`
   - `AppId = 当前 AppId`
   - `Records`、`ThumbMediaIds`、`Operations` 原样保留
   - 不写入 AppSecret
7. 使用现有 `SaveCache` 原子写入账号路径：
   `.cache/wechat-sync/sync-cache.<appid-hash>.json`
8. 保存后重新用 `LoadCache(accountPath, logger, appId)` 做 round-trip 校验，确认：
   - 版本为 4；
   - AppId 正确；
   - Records/ThumbMediaIds/Operations 数量与迁移前一致。
9. 输出迁移摘要（只输出数量/状态，不输出 secret）：
   - records 数；
   - thumb media ID 数；
   - operations 按 state 计数；
   - 如存在 `DraftSubmitting`/`PublishSubmitting`，明确提示需要先按 recovery 文档处理。
10. **不自动删除旧缓存**：旧文件保留作为备份；迁移成功并完成一次验证后再由用户归档。

#### 方案 B：只提供手工 JSON 迁移文档

不推荐作为唯一方案。手工编辑容易漏字段、破坏 Operations 状态，也无法保证 round-trip 校验。可以作为迁移命令不可用时的兜底。

### 12.5 迁移后的同步行为

迁移完成后，正常 `sync` 应保持现有语义：

- 相同 `ContentHash` 的 `Records` 命中 -> `skipped`，不创建新草稿。
- `ThumbMediaIds` 命中 -> 不重复上传封面。
- `DraftCreated` + target=draft -> 直接完成记录，不再创建草稿。
- `DraftCreated` + target=publish -> 从已有 draftId 继续发布。
- `PublishSubmitted` -> 只轮询已有 publishId，不重新发布。
- `DraftSubmitting` / `PublishSubmitting` -> 继续 `recoveryRequired`，不自动重试。

### 12.6 必须补的回归

1. **成功记录迁移后不重复提交**
   - 旧 v3 缓存含 `Records[syncKey]`，同 AppId、同内容。
   - 迁移到账号 v4 后运行 sync。
   - 断言 `Skipped=1`、`AddDraftCount=0`、旧记录仍在。

2. **ThumbMediaIds 迁移后不重复上传**
   - 旧缓存含封面 thumb media ID。
   - 迁移后同内容运行。
   - 断言 `UploadThumbCount=0` 或复用旧 media ID。

3. **DraftCreated 迁移后复用草稿**
   - 旧缓存含 `DraftCreated` + draftId，target=publish。
   - 迁移后运行。
   - 断言 publish 使用旧 draftId，`AddDraftCount=0`。

4. **PublishSubmitted 迁移后不重复发布**
   - 旧缓存含 `PublishSubmitted` + publishId。
   - 迁移后运行。
   - 断言只轮询旧 publishId，`PublishCount=0`。

5. **Submitting 状态仍阻断**
   - 旧缓存含 `DraftSubmitting` / `PublishSubmitting`。
   - 迁移后运行。
   - 断言 `recoveryRequired`、gateway 0 调用、operation 保留。

6. **目标账号缓存已存在时拒绝迁移**
   - 旧 base 与账号 v4 同时存在。
   - 断言明确失败，不覆盖、不合并。

7. **AppId 确认不一致时拒绝迁移**
   - 用户确认值与当前 AppId 不同。
   - 断言失败，旧缓存和目标文件均未改变。

8. **迁移失败可回滚**
   - 模拟 `SaveCache` 失败。
   - 断言旧缓存仍在，账号文件未出现或保持原状。

### 12.7 文档修改

`guide/dev/plugins.md` 不应再建议“归档后重试”。应改为：

1. 停止自动化发布，备份旧 `.cache/wechat-sync/sync-cache.json`。
2. 确认旧缓存属于当前公众号 AppId；无法确认时不要迁移。
3. 使用迁移命令/flag，把 Records、ThumbMediaIds、Operations 升级到 v4 账号文件。
4. 检查迁移摘要；有 `DraftSubmitting`/`PublishSubmitting` 时先按 recovery 文档处理。
5. 先跑 dry-run，再跑一次小范围 sync，确认 `skipped`/`synced` 符合预期。
6. 验证成功后再归档旧缓存；不要直接删除后重跑。

### 12.8 验收标准

- 旧 v2/v3 缓存可以在人工确认 AppId 后迁移到 v4 账号缓存。
- Records、ThumbMediaIds、Operations 全部保留，数量 round-trip 一致。
- 同内容迁移后不重复创建草稿；发布状态不重复发布。
- Submitting/unknown 状态仍然阻断，不因迁移丢失恢复保护。
- 旧缓存不会被自动删除；目标缓存已存在时拒绝合并。
- 文档明确禁止“归档后直接重试”的升级方式。
- 不持久化 AppSecret。

### 12.9 风险与决策点

- **AppId 归属无法自动验证**：旧 v3 没有 AppId，只能人工确认。迁移命令必须显式要求确认，并在输出中展示缓存摘要；错误确认仍可能把 A 账号状态挂到 B 账号下。
- **不要自动合并**：旧 base 与账号文件同时存在时，自动合并可能产生冲突记录/错误素材；必须拒绝并要求人工处理。
- **旧缓存保留策略**：建议保留为备份，不自动删除；用户验证成功后再归档。
- **迁移与正常 sync 的并发**：迁移应使用账号缓存路径的 run lock（或独立迁移锁），避免迁移写入期间另一个 sync 读取半成品；`SaveCache` 本身是原子替换，但仍应串行化。
- **v2 缓存**：没有 Operations，迁移时按空 Operations 处理；其余字段保留。

### 12.10 执行结果

1. 已实现 `--migrate-legacy-cache` + `--confirm-legacy-cache-app-id` 与 round-trip 校验。
2. 已补回归：
   - 成功记录迁移后 skip，不重复创建草稿；
   - `ThumbMediaIds` 迁移保留；
   - `DraftSubmitting` 迁移后仍 `recoveryRequired`；
   - `PublishSubmitted` 迁移后只轮询、不重复发布；
   - 无候选内容时仍可迁移；
   - AppId 确认不一致拒绝迁移；
   - 无迁移 flag 拒绝并要求迁移而非归档；
   - 账号缓存已存在时不合并。
3. 已更新 `guide/dev/plugins.md`。
4. 已用旧 v3 fixture 做本地假网关验证；完整 WechatSync 专项 306/306 通过。
5. 已有 v3 缓存的站点应按迁移步骤升级，不应归档后重试。

---

## 13. Native AOT 验证（.NET SDK 10.0.403）

### 13.1 SDK 安装

原环境：

```text
.NET SDK 10.0.100
Microsoft.DotNet.ILCompiler 10.0.0
NU1903（安全审计高危）
```

系统目录 `/usr/local/share/dotnet` 不可写，因此使用官方 `dotnet-install.sh` 安装到用户目录：

```text
/Users/ali/.dotnet/sdk/10.0.403
```

使用时需要：

```text
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

`global.json` 仍为 `10.0.100` + `latestFeature`，在上述 PATH 下会选择 `10.0.403`。该 SDK 的 runtime/host 为 `10.0.14`，`NU1903` 不再出现。

### 13.2 首次 AOT 结果与 IndexNow 修复

安装新 SDK 后，WechatSync 直接 AOT 成功；IndexNow 进入编译后暴露真实 AOT 不兼容：

```text
IL2026 / IL3050
- IndexNowHttpClient: HttpClientJsonExtensions.PostAsJsonAsync<T>
- IndexNowSubmissionWorkflow: JsonSerializer.DeserializeAsync<T>
- IndexNowStateStore: JsonSerializer.DeserializeAsync<T> / SerializeAsync<T>
```

根因是 `Bukit.IndexNow` 业务层使用反射式 `System.Text.Json`，而插件入口已使用 source generation。

最小修复：

- 新增 `IndexNowJsonContext`（source-generated）：
  - `PublishUrlSnapshotDocument`
  - `PublishUrlChangeSetDocument`
  - `IndexNowState`
  - `IndexNowHttpSubmissionPayload`
- `IndexNowHttpClient` 改用带 `JsonTypeInfo` 的 `PostAsJsonAsync`，并使用显式 `urlList` 字段的 DTO。
- `IndexNowSubmissionWorkflow.ReadAsync` 改为接收 `JsonTypeInfo<T>`。
- `IndexNowStateStore` 改为 source-generated 序列化/反序列化。
- 未使用 `NuGetAudit=false`、`TreatWarningsAsErrors=false` 或 `NoWarn=IL2026/IL3050`。

### 13.3 AOT 编译与启动验证

使用 `dotnet publish ... -c Release -r osx-arm64 --self-contained true -p:PublishAot=true`：

| 插件 | 结果 | 产物 |
|---|---|---|
| IndexNow | 成功 | `bukit-plugin-indexnow`，Mach-O arm64，约 7.4 MB |
| WechatSync | 成功 | `bukit-plugin-wechat-sync`，Mach-O arm64，约 8.0 MB |

两个 AOT 可执行文件均运行 handshake：

```text
indexnow exit=0   -> handshakeResponse success=true
wechatsync exit=0 -> handshakeResponse success=true
```

### 13.4 验证结论

- `NU1903` 阻断已由 SDK 升级解决，未降低安全审计。
- IndexNow 的 AOT 不兼容已通过 source-generated JSON 修复。
- WechatSync 无需额外 AOT 代码修改。
- 两个插件均完成 osx-arm64 / Release / Native AOT 编译，并完成启动级 handshake 验证。
- 未验证真实远端发布、完整插件业务 AOT smoke 或全量/发布门禁。
