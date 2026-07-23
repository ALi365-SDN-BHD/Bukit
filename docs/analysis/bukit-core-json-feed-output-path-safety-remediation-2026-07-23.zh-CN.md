# Bukit Core JSON Feed output path 安全修复台账

> 日期：2026-07-23
>
> 分支：`codex/json-feed-output-path-safety`
>
> 基线：`2.0@6f10269c515f328628955f706075d70cc3a21977`
>
> 状态：implementation-complete / aggregate-passed / review-pending

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

## 8. 待完成关闭证明

提交 `547b1728` 已包含 production 与两条测试，唯一 aggregate 已通过。正式关闭前还
必须：

1. `git diff --check`；
2. 确认 public API baseline 与 136-entry historical manifest 未变化；
3. 完成一次独立只读复审，记录 Critical/Important/Minor；
4. 将本台账更新为 closure-complete；
5. 本地合并回 `2.0` 后，更新暂停中的 G4 branch 基线。

在独立复审完成前，本任务不能标记关闭。
