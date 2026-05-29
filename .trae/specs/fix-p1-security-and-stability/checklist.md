# Verification Checklist

## P1-1: ShortcodeProcessor XSS
- [x] `ShortcodeProcessor.ApplyShortcodeTemplate` 对参数值调用 `WebUtility.HtmlEncode` 后再替换
- [x] 含 `<script>` 的参数输出为 `&lt;script&gt;` 字面量
- [x] 普通文本参数行为不变
- [x] 新增/调整后的 Shortcode 测试全部通过

## P1-2: Notion BlockRenderer 颜色 class 注入
- [x] `CalloutBlockRenderer` 颜色 class 输出使用编码后的颜色值
- [x] `ToDoBlockRenderer` 颜色 class 输出使用编码后的颜色值
- [x] `ToggleBlockRenderer` 颜色 class 输出使用编码后的颜色值
- [x] `BookmarkBlockRenderer` 颜色 class 输出使用编码后的颜色值
- [x] `EquationBlockRenderer` 颜色 class 输出使用编码后的颜色值
- [x] 5 个 BlockRenderer 各新增的颜色注入回归测试通过

## P1-3: MediaConfig.BlockPrivateNetworks 默认值
- [x] `new MediaConfig().BlockPrivateNetworks == true` 测试通过
- [x] 审计文档已记录此 issue 已修复

## P1-4: IncrementalBuildEngine 异步阻塞
- [x] `IncrementalBuildEngine.ComputeListContentHash` 及其内部 `ComputeListItemHash` 已改为异步签名
- [x] 同步路径中所有 `ContentBodyResolver.GetHtml`（阻塞版）调用已被替换
- [x] `PageRenderDispatcher` 等调用方已迁移到异步链路
- [x] 现有增量构建相关测试仍然通过

## P1-6: CloneCommand & SeoExternalAuditor SSRF
- [x] 存在一处共享方法用于创建带 SSRF 保护的 `HttpClient`
- [x] `CloneCommand` 使用该共享方法替换 `new HttpClient()`
- [x] `SeoExternalAuditor` 使用该共享方法替换 `new HttpClient { Timeout = ... }`
- [x] 针对 `127.0.0.1` 的连接被拒绝的回归测试通过

## 全局验证
- [x] `dotnet build bukit.slnx -c Release` 输出 0 警告 0 错误
- [x] `dotnet test bukit.slnx -c Release` 全部测试通过
- [x] 未引入新的第三方依赖
