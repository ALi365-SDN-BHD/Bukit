# Section 插件系统

Section 插件允许在 section 渲染管线的关键节点注入自定义逻辑，实现动态数据注入、渲染后处理等能力。

实现参考：
- `src/Bukit.Engine.Abstractions/Plugins/ISectionPlugin.cs`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs` (SectionRenderHelper.RenderOneSectionBase)
- `src/plugins/WordCountSectionPlugin/WordCountPlugin.cs`

## 架构

```
Section JSON → PageComposer → SectionRenderHelper
                                  │
                    ┌─────────────┼─────────────┐
                    ▼             ▼             ▼
              BeforeRender   ResolveItems   AfterRender
                    │             │             │
                    ▼             ▼             ▼
              Scriban 模板渲染 →  HTML 输出
```

## ISectionPlugin 接口

```csharp
public interface ISectionPlugin
{
    SectionHook SupportedHook { get; }
    Task ExecuteAsync(SectionContext context, CancellationToken ct = default);
}
```

## SectionHook 枚举

| 值 | 时机 | 用途 |
|----|------|------|
| `BeforeRender` | 模板渲染前 | 修改 props、注入额外数据 |
| `AfterRender` | HTML 生成后 | 后处理 HTML、注入脚本/徽章 |
| `ResolveItems` | 数据解析后 | 自定义 items 转换、过滤 |

## SectionContext

```csharp
public sealed class SectionContext
{
    public required string SectionType { get; init; }   // section 类型名
    public string? Variant { get; init; }                // variant 名
    public Dictionary<string, object?>? Props { get; set; }  // 可修改的 props
    public string? RenderedHtml { get; set; }            // AfterRender 可修改
    public Dictionary<string, object?> Data { get; init; }   // 插件间共享数据
}
```

## 声明方式

在 `theme.yaml` 中声明 section 使用的插件：

```yaml
sections:
  hero:
    template: sections/hero/hero.html
    plugin: WordCount  # ← 插件名称
```

插件通过 `IReadOnlyDictionary<string, ISectionPlugin>` 注册到 `ScribanTemplateRenderer`。

## 示例：WordCountPlugin

```csharp
public sealed class WordCountPlugin : ISectionPlugin
{
    public SectionHook SupportedHook => SectionHook.AfterRender;

    public Task ExecuteAsync(SectionContext context, CancellationToken ct = default)
    {
        if (context.RenderedHtml is null) return Task.CompletedTask;

        var wordCount = CountWords(context.RenderedHtml);
        var badge = $"""
            <div class="word-count-badge">
              {wordCount:N0} words
            </div>
            """;

        context.RenderedHtml += badge;
        return Task.CompletedTask;
    }
}
```

## 生命周期

1. **BeforeRender** — 在 schema 校验之前执行。可修改 `context.Props` 来注入或覆盖属性值。
2. **AfterRender** — 在模板渲染后执行。可修改 `context.RenderedHtml` 来追加或替换 HTML。
3. **错误处理**：插件异常被静默捕获，不中断渲染流程。

## 限制

- 插件在渲染线程同步执行（`GetAwaiter().GetResult()`），不支持异步 I/O。
- 插件运行在静态站点生成的主线程上，耗时操作会影响构建性能。
- 一个 section 只能绑定一个插件实例。
