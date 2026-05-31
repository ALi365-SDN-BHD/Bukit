# Section Schema 参考

> **语言说明**：本页目前仅有中文版本。English version pending. Versi Bahasa Melayu belum tersedia.

Section Schema 是 JSON 格式的文件，用于定义 section 的 props 类型、必填性及约束。由 `SectionSchema.Load()` 加载，`SectionSchemaValidator.Validate()` 执行校验。

实现参考：
- `src/Bukit.Theme/Models/SectionSchema.cs`
- `src/Bukit.Theme/SectionSchemaValidator.cs`

## schema.json 格式

```json
{
  "name": "hero",
  "label": "Hero Section",
  "description": "Main hero with headline, subheadline, and CTA button",
  "props": {
    "headline": {
      "type": "string",
      "required": true,
      "maxLength": 120
    },
    "subheadline": {
      "type": "string",
      "required": false,
      "maxLength": 300
    },
    "background_image": {
      "type": "image",
      "required": false
    },
    "cta_text": {
      "type": "string",
      "required": false,
      "maxLength": 30
    },
    "cta_url": {
      "type": "url",
      "required": false
    },
    "show_overlay": {
      "type": "boolean",
      "required": false
    },
    "overlay_opacity": {
      "type": "number",
      "required": false
    }
  }
}
```

### SectionSchema 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `name` | string | section 名称 |
| `label` | string? | 人类可读标签 |
| `description` | string? | 描述文本 |
| `props` | dict? | prop 定义映射，key 为 prop 名 |

### SchemaPropDefinition 字段

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `type` | string | `"string"` | prop 类型 |
| `required` | bool | `false` | 是否必填 |
| `maxLength` | int? | `null` | 最大长度（仅 string 类型） |

## 支持的 prop 类型

| 类型 | 校验规则 |
|---|---|
| `string` | 值必须为字符串，支持 `maxLength` 校验 |
| `number` | 值必须为 int/long/float/double/decimal |
| `boolean` | 值必须为 bool |
| `url` | 值必须为字符串，且以 `http://`、`https://` 或 `/` 开头 |
| `image` | 值必须为字符串，校验非空 |

## 校验模式

通过 `site.yaml` 的 `theme.component_validation` 字段控制：

```yaml
theme:
  component_validation: warn
```

| 模式 | 对应枚举 | 行为 |
|---|---|---|
| `off`（默认） | `ValidationMode.Off` | 不执行任何校验 |
| `warn` | `ValidationMode.Warn` | 校验错误以 Warning 日志输出，不中断构建 |
| `strict` | `ValidationMode.Strict` | 校验错误抛出 `SchemaValidationException`，中断构建 |

## 示例：hero section schema

```json
{
  "name": "hero",
  "label": "Hero Section",
  "description": "Main hero section with headline and CTA",
  "props": {
    "headline": {
      "type": "string",
      "required": true,
      "maxLength": 120
    },
    "subheadline": {
      "type": "string",
      "required": false,
      "maxLength": 300
    },
    "background_image": {
      "type": "image",
      "required": false
    },
    "cta_text": {
      "type": "string",
      "required": false,
      "maxLength": 30
    },
    "cta_url": {
      "type": "url",
      "required": false
    }
  }
}
```

## 示例：card grid section schema

```json
{
  "name": "cardGrid",
  "label": "Card Grid",
  "description": "Responsive card grid for displaying content items",
  "props": {
    "columns": {
      "type": "number",
      "required": false
    },
    "show_images": {
      "type": "boolean",
      "required": false
    },
    "empty_message": {
      "type": "string",
      "required": false,
      "maxLength": 200
    }
  }
}
```

## 错误报告

校验错误通过 `SchemaValidationError` 记录：

```
[WARN] hero: Missing required prop: headline
[WARN] hero: Prop 'background_image' image value is empty
[WARN] cardGrid: Unknown prop: colums
[WARN] hero: Prop 'headline' exceeds maxLength 120 (actual: 145)
```

在 **strict** 模式下，第一个错误即抛出异常并中断构建。在 **warn** 模式下，所有错误收集为列表并逐条输出警告日志，不影响构建结果。
