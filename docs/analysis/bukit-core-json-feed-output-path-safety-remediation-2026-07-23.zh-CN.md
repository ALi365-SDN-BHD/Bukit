# Bukit Core JSON Feed output path 安全修复台账

> 日期：2026-07-23
>
> 分支：`codex/json-feed-output-path-safety`
>
> 基线：`2.0@6f10269c515f328628955f706075d70cc3a21977`
>
> 状态：closure-complete

## 1. 问题

G-04D9 只读资格审计发现，JSON Feed writer 没有使用 Core 既有 output path policy：

```text
site.feed.path / collection.output.feedPath
  -> aggregate JSON Feed projection
  -> JsonFeedGenerator.Generate(..., feedFileName, ...)
  -> Path.Combine(outputDir, feedFileName)
  -> Directory.CreateDirectory(...)
  -> File.Create(...)
```

配置读取、JSON schema、strict field validator 和 `ConfigValidator` 均没有拒绝 `..`、
rooted path 或既有逃逸 symlink。因此：

```yaml
site:
  url: https://example.com
  feed:
    formats: [json]
    path: ../escaped-feed
```

会把 `dist/../escaped-feed/feed.json` 创建在 output root 外。若目标已存在，
`File.Create` 会截断它。

该问题在 G4 基线已经存在，不是公共面治理引入。为避免在 G-04 visibility task 中
超限修复，本修复使用独立 Core 分支、独立测试和独立复审。

## 2. 根因

`JsonFeedGenerator` 与其他 feed writer 的 destination handling 不一致：

| Writer | 目标处理 |
|---|---|
| Atom | `FileWriter.WriteUtf8` → `SafePathResolver` |
| RSS collection | relative path normalization → `FileWriter.WriteUtf8` |
| JSON Feed | 直接 `Path.Combine` + `File.Create` |

JSON serializer 本身不是根因。真正缺口发生在创建目录和打开文件之前，writer 没有调用
既有 `IOutputPathPolicy`。

## 3. 受控修复

唯一 production 变化：

```diff
- var path = Path.Combine(outputDir, feedFileName);
+ var path = FileWriter.GetSafeFullPath(outputDir, feedFileName);
```

这使 JSON Feed 在任何目录创建或文件打开前复用 `SafePathResolver`，拒绝：

- 解析后逃出 output root 的 `..`；
- rooted/absolute destination；
- output root 内解析到外部的既有 symlink/reparse segment。

后续代码保持原样：

- `Directory.CreateDirectory`；
- `File.Create`；
- `Utf8JsonWriter`；
- `Indented = true`；
- 字段顺序和 null/optional 字段规则；
- `_bukit` projection；
- feed URL、base URL 和 filename 计算。

因此本任务没有把 writer 改为新的 buffering、temp+rename 或 schema validator，也没有
声称现有 direct-write 具备 filesystem-atomic replacement。

## 4. 明确未修改

- config schema、`site.feed.path` 或 collection `feedPath` 语义；
- Atom/RSS/Sitemap writer；
- feed URL、JSON Feed 1.1 字段或输出字节生成逻辑；
- plugin registry、built-in ownership 或 external plugin protocol；
- public API、serializer/AOT roots；
- `FileWriter`、`SafePathResolver`、全局路径工具；
- Labs 或外部插件。

配置允许任意 string 的现状仍存在，但 unsafe destination 在 sink 写入前被统一 policy
拒绝。若未来需要配置加载阶段提前报错，必须另立 config-contract 任务。

## 5. TDD 证据

新增 direct sink 测试：

```text
GenerateJsonFeed_RejectsTraversalBeforeWritingOutsideOutputRoot
```

修复前 RED：

```text
Expected: OutputPathSecurityException
Actual:   no exception
Failed: 1 / Total: 1
```

修复后 GREEN：

```text
Passed: 1 / Failed: 0
```

新增产品调用链测试：

```text
JsonFeedProjection_RejectsConfiguredTraversalBeforeWritingOutsideOutputRoot
```

它从 aggregate representation adapter 使用配置中的 `Feed.Path`，断言：

- 抛出 `OutputPathSecurityException`；
- output root 外 `escaped-feed/feed.json` 不存在。

两条安全测试共同通过：2/2。

## 6. Owner 回归

定向 feed/representation owner tests：

```text
RssGeneratorTests
PublishRepresentationRegistryTests
I18nMergedFeedProjectionTests
```

结果：35 passed / 0 failed / 0 skipped。

首次 `post-change-focused.sh` 运行中，宿主 `NOTION_TOKEN` 使既有缺凭据测试不再抛出
预期异常：

```text
ContentProviderFactoryTests.CreateNotionProvider_WithNotionConfig_ReturnsNotionProvider
```

该次为 1596 passed / 1 failed，属于环境污染，不是 JSON Feed 回归。未修改 Notion
代码或测试。命令级 `env -u NOTION_TOKEN` 后重新运行同一 focused gate：

```text
1597 passed / 0 failed / 0 skipped
```

## 7. Aggregate targeted gate

从基线对四个 changed paths 只执行一次：

```text
env -u NOTION_TOKEN
bash scripts/checks/post-change-targeted.sh
  --base 6f10269c515f328628955f706075d70cc3a21977
  -- <production> <two tests> <this ledger>
```

结果：exit `0`。其中包括：

- `Bukit.Engine.Tests`：1597/1597；
- docs consistency；
- active workflow boundary 与 focused/targeted self-tests；
- `dotnet format`；
- 相关 fast contract checks。

aggregate 没有重复执行。宿主 token 只通过命令级环境隔离，不修改代码、测试或持久化
环境。

## 8. 独立只读复审

独立复审范围为本任务完整 base diff，未编辑文件或重复运行 tests/gates/AOT。

结果：

```text
Critical:  0
Important: 0
Minor:     0
```

复审确认：

- safe path resolution 发生在任何目录创建或文件打开之前；
- traversal、rooted/absolute path 和 output root 内既有逃逸 symlink/reparse segment
  由既有 `SafePathResolver` 处理；
- production 只有一行变化；
- JSON writer、缩进、字段顺序、feed/home URL、排序和 `_bukit` shape 无变化；
- direct sink 与 aggregate config chain 测试均真实进入 writer，不是提前返回；
- 临时目录唯一并在 `finally` 清理；
- 没有 schema、协议、其他 writer/path tool、Labs 或插件漂移；
- public API baseline 仍为 `14/484/56`；
- historical manifest 仍为 `136/136`，blob 未变；
- `git diff --check` 通过。

残余风险仅为既有全局 path policy 边界：output root 本身是可信根，且路径校验与文件
打开之间不是无竞态 filesystem 原子操作。本次 diff 没有扩大该风险；扩展为 dirfd、
handle-relative 或全局 atomic writer 必须另立架构/安全任务。

## 9. 关闭判定

production 修复、RED/GREEN、两条安全回归、35 项 feed/representation owner tests、
1597 项 focused owner tests、唯一 aggregate targeted gate、静态 baseline/manifest
复核和独立 `0/0/0` 复审均已完成。

本任务正式判定为 `closure-complete`，可以本地合并回 `2.0`。合并后应更新暂停中的
G4 branch `GROUP_BASE`，再继续 G-04D9 Task 33；不得把本独立 P1 修复计入 G4
visibility aggregate diff。
