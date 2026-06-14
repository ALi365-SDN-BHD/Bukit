# 模板能力清单

`layouts/bukit.templates.yaml` 用于声明模板的数据依赖与能力特征。

它的目标有两个：

- 让引擎在 `build.listPageContentMode: auto` 下优先依据显式声明决定是否装配列表页正文
- 为主题提供一份可校验、可演进的模板能力清单

## 1. 文件位置

文件固定放在当前生效 layouts 根目录下：

```text
layouts/bukit.templates.yaml
```

如果使用主题目录，则位置通常是：

```text
themes/<name>/layouts/bukit.templates.yaml
```

## 2. 基本结构

```yaml
templates:
  pages/index.html:
    capabilities:
      needs_page_content: false
  pages/list.html:
    capabilities:
      needs_page_content: true
      supports_pagination: true
      supports_taxonomy: false
      supports_search_snippets: false
```

规则：

- `templates` 必须存在且非空
- 键必须是相对于 `layouts/` 的模板路径
- 路径不能越出 `layouts/`
- 声明的模板文件必须真实存在
- 每个模板都必须声明至少一个 capability

## 3. 当前已识别的 capability

### `needs_page_content`

表示该模板是否依赖 `page.content` 或 `pages[*].content`。

这是当前已经被引擎实际消费的字段：

- 在 `build.listPageContentMode: auto` 下
- 若模板显式声明 `needs_page_content`
- 引擎会优先使用该值，而不是继续依赖模板文本启发式

### `supports_pagination`

表示该模板适合作为分页列表模板。

当前会被分页构建流程实际消费：

- 若 `pages/pagination.html` 存在且声明 `supports_pagination: true`
- 分页派生页会优先使用该模板
- 否则回退到默认的 `pages/page.html`

### `supports_taxonomy`

表示该模板适合作为 taxonomy / term 列表模板。

当前会被 taxonomy 构建流程实际消费：

- 若 `pages/taxonomy-index.html` 存在且声明 `supports_taxonomy: true`
- taxonomy kind 索引页优先使用该模板
- 若 `pages/taxonomy-term.html` 存在且声明 `supports_taxonomy: true`
- term 页与 term 分页页优先使用该模板

### `supports_search_snippets`

表示该模板适合渲染搜索摘要片段或搜索结果卡片。

当前会被搜索索引构建流程实际消费：

- 若 `pages/search.html` 存在且声明 `supports_search_snippets: true`
- `search.json` 会额外输出 `snippet`
- `snippet` 优先使用 `summary`，否则回退到正文纯文本截断

## 4. 与 `build.listPageContentMode` 的关系

- `always`：无论模板声明什么，始终装配列表页正文
- `never`：无论模板声明什么，始终不装配列表页正文
- `auto`：优先读取 `bukit.templates.yaml`；若未声明，再回退到兼容性启发式

`doctor` 现在会优先做 include/layout 递归静态分析；只有在静态分析无法准确确认时，才会提示模板仍然依赖 heuristic fallback。

## 5. 推荐实践

- 列表页优先使用 `summary`
- 只有确实需要正文片段时才把 `needs_page_content` 设为 `true`
- 把 `bukit.templates.yaml` 当作“主题能力声明文件”，不要只把它当作一个正文开关
- 若主题提供专用分页、taxonomy、搜索模板，建议同时声明对应 capability，避免引擎回退到通用模板
- starter 主题现在提供了 `pages/pagination.html`、`pages/taxonomy-index.html`、`pages/taxonomy-term.html`、`pages/search.html` 作为能力驱动模板示例
