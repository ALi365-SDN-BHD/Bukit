# 审计修复计划

## 当前状态 vs 审计发现

| 审计项                                        | 审计结论       | 实际状态                                                                                      |
| ------------------------------------------ | ---------- | ----------------------------------------------------------------------------------------- |
| ProcessPluginInvoker runtime env allowlist | "仍未修复"     | **已修复** ✅ — `DefaultRuntimeEnvironmentAllowlist` + `CopyAllowedEnvironment` 已实现，审计看到的是旧版本 |
| derive-success stdin 依赖                    | "当前未完成"    | **确认未完成** ⚠️ — 仍检查 hook，与文档描述不一致                                                          |
| last-wins 测试未验证 plugin-over-plugin 冲突      | "测试语义不够强"  | **确认** ⚠️                                                                                 |
| Failure 2 生产级并发确定性                         | "没有看到明显落地" | **确认** ⚠️ — 修复仅在测试层（排除非确定性文件 + 显式时间戳）                                                     |

***

## 实施步骤

### 步骤 1：消除 `derive-success` 的 stdin 依赖

**文件**: `tests/ProtocolEchoPlugin/Program.cs`

与 `derive-conflict`/`derive-lastwins` 一致，移除对 `hook` 的条件判断，直接写入输出：

```csharp
if (mode == "derive-success")
{
    Console.Out.Write("""{"ok":true,"derivedPages":[...]}""");
    return;
}
```

### 步骤 2：重命名现有 last-wins 测试 + 新增 plugin-over-plugin 冲突测试

**文件**: `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs`

**2.1** 重命名现有测试：

* `ExternalProtocolPlugin_DerivePages_LastWinsPolicy_AllowsConflictingDerivedPages` → `ExternalProtocolPlugin_DerivePages_LastWinsPolicy_AllowsDerivedPages`

**2.2** 新增真正的 plugin-over-plugin 冲突测试：`DerivePages_LastWins_ReplacesEarlierPluginDerivedPage`

需要通过两个外部插件（Plugin A 和 Plugin B）输出相同 URL 的派生页，验证 last-wins 下 Plugin B 覆盖 Plugin A。

**实现方式**：在 `ProtocolEchoPlugin` 中新增两个模式：

* `derive-plugin-a`：输出 URL `/plugin-conflict/page/`

* `derive-plugin-b`：输出 URL `/plugin-conflict/page/`

测试注册两个外部插件（sample-a → derive-plugin-a, sample-b → derive-plugin-b），均启用 `derive-pages` hook，使用 `last-wins` 策略，断言 Plugin B（后注册）的页面存在。

### 步骤 3：确认 Failure 2 修复定位并补充文档

当前修复已确保测试通过（20 轮稳定性验证）。但需明确记录修复类型：

| 修复        | 说明                                                                                  |
| --------- | ----------------------------------------------------------------------------------- |
| 排除构建元数据文件 | `DirectoriesMatch` 排除 `.bukit-build-state.json`、`.bukit-output-marker`、`.bukit/` 目录 |
| 内容时间戳确定化  | 测试内容 frontmatter 添加显式 `publishAt: 2026-01-01T00:00:00Z`                             |
| 诊断增强      | `DirectoriesMatch` 报告具体差异文件名和内容                                                     |

如需进一步生产级并发确定性（每个 variant 独立 cache/pipeline），应作为后续需求跟踪。

***

## 文件变更汇总

| 文件                                                        | 操作                                                                                |
| --------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `tests/ProtocolEchoPlugin/Program.cs`                     | **修改** — `derive-success` 移除 stdin 依赖；新增 `derive-plugin-a` / `derive-plugin-b` 模式 |
| `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs` | **修改** — 重命名 last-wins 测试；新增 plugin-over-plugin 冲突测试                              |

## 实施顺序

1. 步骤 1 — 消除 derive-success stdin 依赖
2. 步骤 2 — 重命名 + 新增 plugin-over-plugin 测试
3. 步骤 3 — 验证 20 轮 + 全量测试

