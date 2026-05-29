# Tasks

- [x] Task 1: 修复 P1-1（ShortcodeProcessor XSS）
  - [x] SubTask 1.1: 在 `tests/Bukit.Shared.Tests` 中为 `ShortcodeProcessor` 添加失败测试：参数含 `<script>` 应输出 HTML 编码后的字面量
  - [x] SubTask 1.2: 修改 `src/Bukit.Shared/ShortcodeProcessor.cs:ApplyShortcodeTemplate`，使用 `System.Net.WebUtility.HtmlEncode(value)` 替换 `value`
  - [x] SubTask 1.3: 验证现有 shortcode 相关测试是否仍然通过；若期望未编码值需调整测试以匹配新的安全行为

- [x] Task 2: 修复 P1-2（Notion BlockRenderer 颜色 class 注入）
  - [x] SubTask 2.1: 在 `tests/Bukit.Content.Tests` 为 Callout/ToDo/Toggle/Bookmark/Equation BlockRenderer 各添加一条测试：颜色值含双引号时输出应被编码
  - [x] SubTask 2.2: 修改 5 个 BlockRenderer 中拼装 `class=\"notion-{color}\"` 的位置：
    - `CalloutBlockRenderer.cs` L55-58
    - `ToDoBlockRenderer.cs` L24-27
    - `ToggleBlockRenderer.cs` L31-32
    - `BookmarkBlockRenderer.cs` L28
    - `EquationBlockRenderer.cs` L25
    - 改为对 color 使用 `WebUtility.HtmlEncode(color)` 后再拼接（或复用 `GetBlockColorClass()` 模式）

- [x] Task 3: 验证 P1-3（MediaConfig.BlockPrivateNetworks 默认值）
  - [x] SubTask 3.1: 在 `tests/Bukit.Config.Tests` 添加测试断言 `new MediaConfig().BlockPrivateNetworks == true`
  - [x] SubTask 3.2: 在审计报告对应章节追加备注：此前发现已被修复，仅需保留回归测试

- [x] Task 4: 修复 P1-4（IncrementalBuildEngine 异步阻塞）
  - [x] SubTask 4.1: 在 `tests/Bukit.Engine.Tests` 添加一条针对 `ComputeListContentHash` 的测试，断言其异步路径不阻塞（行为正确即可，不强测线程池）
  - [x] SubTask 4.2: 将 `IncrementalBuildEngine.ComputeListContentHash` / `ComputeListItemHash` 改为 `Async` 版本，使用 `await bodyStore.GetAsync(...)`
  - [x] SubTask 4.3: 将调用方 `PageRenderDispatcher` 等同步入口改为异步调用
  - [x] SubTask 4.4: 在 `ContentBodyResolver.GetHtml`（同步入口）添加 `[Obsolete]` 标记或文档说明仅用于不可异步化的边界，必要时保留向后兼容

- [x] Task 5: 修复 P1-6（CloneCommand & SeoExternalAuditor SSRF）
  - [x] SubTask 5.1: 创建一个共享辅助方法（如 `Bukit.Shared.HttpClientFactory.CreateWithSsrfGuard`），返回安装了 `SsrfGuard.SsrfSafeConnectAsync` 的 `HttpClient`。注意 `SsrfGuard` 当前位于 `Bukit.Content.Media`，需评估是否需要将其上移到 `Bukit.Shared` 或在 CLI 中复用其逻辑
    - **补充说明**：`SsrfGuard` 已从 `Bukit.Content.Media` 上移至 `Bukit.Shared`（命名空间 `Bukit.Shared`），访问级别从 `internal` 改为 `public`，解决了 CLI 直接依赖 Content 的架构违规。同时还原了 `Bukit.Content/InternalsVisibleTo.cs` 中不必要的 `Bukit.Cli`/`bukit`/`Bukit.Cli.Tests` 条目。
  - [x] SubTask 5.2: 修改 `CloneCommand.cs` L334 的 `new HttpClient()` 改为使用上述辅助方法
  - [x] SubTask 5.3: 修改 `SeoExternalAuditor.cs` L11 的 `new HttpClient { Timeout = ... }` 同样替换
  - [x] SubTask 5.4: 添加测试：使用 `http://127.0.0.1` URL 调用时连接被拒绝

- [x] Task 6: 全量回归与构建验证
  - [x] SubTask 6.1: `dotnet build bukit.slnx -c Release` 必须 0 警告 0 错误
  - [x] SubTask 6.2: `dotnet test bukit.slnx -c Release` 全部通过

# Task Dependencies

- Task 6 依赖 Task 1-5 全部完成
- Task 5 可能依赖将 `SsrfGuard` 移到 Bukit.Shared 的小重构（仅在评估后确实需要时）
- 其他任务相互独立，可并行执行
