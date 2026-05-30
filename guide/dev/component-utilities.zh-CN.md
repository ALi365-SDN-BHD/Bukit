# 组件工具函数

Scriban 模板中可通过 `util` 对象访问内置工具函数，用于日期格式化、文本截断等常见操作。

实现参考：
- `src/Bukit.Rendering/Scriban/ComponentFunctions.cs` (ComponentUtilityFunctions)
- `src/Bukit.Rendering/Scriban/RenderComponentFunction.cs` (util 注册)

## 可用函数

### util.format_date

格式化日期字符串。

```scriban
{{ post.publish_date | util.format_date '%Y-%m-%d' }}
{{ "2025-01-15" | util.format_date '%B %d, %Y' }}
```

参数：
| 参数 | 类型 | 说明 |
|------|------|------|
| input | string | 日期字符串（ISO 8601 或常见格式） |
| format | string | .NET 日期格式字符串 |

### util.truncate

截断文本至指定长度。

```scriban
{{ summary | util.truncate 120 }}
{{ title | util.truncate 50 }}
```

参数：
| 参数 | 类型 | 默认 | 说明 |
|------|------|------|------|
| input | string | - | 要截断的文本 |
| maxLength | string | "100" | 最大字符数，超出部分追加 `…` |

### util.titleize

将 snake_case 或 kebab-case 转换为 Title Case。

```scriban
{{ "my_section_name" | util.titleize }}
{{ "hello-world" | util.titleize }}
```

### util.slugify

将文本转换为 URL 友好的 slug。

```scriban
{{ title | util.slugify }}
```

## 调用方式

所有 util 函数均支持两种调用方式：

```scriban
{{ util.format_date date '%Y-%m-%d' }}    {{ pipe 风格 -- }}
{{ date | util.format_date '%Y-%m-%d' }}    {{ pipe 风格 }}
```

## 限制

- `format_date` 仅接受字符串输入（无法接受 `DateTimeOffset` / `DateTime`），需在模板外预格式化或传入字符串。
- 如需更复杂的转换（如布尔判断、循环内计算），建议在 C# 层预计算后注入 Scriban 变量。

## 底层实现

```csharp
internal static class ComponentUtilityFunctions
{
    public static string FormatDate(object? input, string format = "yyyy-MM-dd") { ... }
    public static string Truncate(object? input, int maxLength = 100) { ... }
    public static string Titleize(object? input) { ... }
    public static string Slugify(object? input) { ... }
}
```

这些方法被包装为 `Func<string, string, string>` 委托注册到 Scriban 的 `util` 全局对象中。
