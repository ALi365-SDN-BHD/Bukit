# Modules 数据源（mode=data → site.modules）

当你需要 banner、导航、页脚、FAQ、价格表等“结构化模块”时，推荐通过 `content.sources[].mode: data` 注入模块数据到模板变量 `site.modules`。

实现参考：
- `src/Bukit.Config/AppConfig.cs`（ContentSourceConfig.mode）
- `src/Bukit.Engine/SiteEngine.cs`（IsDataItem / BuildModules）
- `src/Bukit.Rendering/Models.cs`（ModuleInfo）
- `src/Bukit.Rendering/Scriban/ScribanModelBinder.cs`（site.modules 注入）

## 触发条件：sourceMode=data

当 `content.provider: sources` 且某个 source 的 `mode: data` 时，引擎会在加载后将其标记为 data item（Meta 中带 `sourceMode=data`），并在渲染前：

1. 从全部 items 中分离 dataItems
2. 对 dataItems 做过滤/分组/排序
3. 注入到 `site.modules.<type>`（数组）

## 注入结构

模板侧使用方式：
- `site.modules.<type>`：按 type 分组后的模块数组

每个模块项（ModuleInfo）结构：
- `id/title/slug/content`
- `fields.<key>.type/value`

## 分组规则：meta.type

dataItems 会按 `meta.type` 分组：
- `type` 为空时，默认分组为 `module`

建议 Notion 的 Modules 数据库用 `Type` 字段表达模块类型（例如 banner/navigation/footer）。

## 过滤规则：enabled

如果字段 `enabled`（注意是 fields 中的 key）能解析为 false，则该模块不会进入输出。

启用建议：
- Notion：设置 `Enabled` checkbox，并在 schema 中保证默认勾选
- Markdown data：在 Front Matter 中写 `enabled: true/false`

## 多语言过滤：locale

dataItems 会按字段 `locale` 做过滤：
- `locale` 为空：对所有语言变体可见
- `locale` 等于当前构建语言（忽略大小写）：对该语言可见

## 排序规则：order

同一分组内按字段 `order` 升序排序，缺失视为 0；若相同再按 `title` 排序。

## 配置示例

```yaml
content:
  provider: sources
  sources:
    - type: notion
      name: pages
      mode: content
      notion:
        databaseId: "..."
        filterProperty: Published
        filterType: checkbox_true
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "..."
        filterProperty: Enabled
        filterType: checkbox_true
        sortProperty: Order
        sortDirection: ascending
        fieldPolicy:
          mode: all
```

## Scriban 使用示例

```scriban
{{ if site.modules && site.modules.banner }}
  {{ for b in site.modules.banner }}
    {{ if b.fields && b.fields.image }}
      <img src="{{ b.fields.image.value }}" />
    {{ end }}
  {{ end }}
{{ end }}
```

## 相关专题

- 企业官网内容建模入口：[content.md](./content.md)
