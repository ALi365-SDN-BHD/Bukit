# Bukit Taxonomy 修复计划

## 问题摘要

5 个核心问题均源于 **taxonomy term 身份模型太弱**——内容分类值（中文）与分类库 slug（英文）被当作两套独立系统。

## 修复方案

### Fix 1+2: MergeEnsureTerms 增强（合并已有 term pages）

**当前行为**: `MergeEnsureTerms` 仅补空 term，不合并已有 term pages。
**目标行为**: 当 ensured term 有 slug `S` 和 title `T` 时，若存在 title=`T` 但 slug≠`S` 的已有 term，将其 Pages 合并到 slug=`S` 的 term，删除旧 term。

```
Before:  { slug="本地企业", title="本地企业", pages=[12 articles] }
         { slug="local-business", title="本地企业", pages=[] }

After:   { slug="local-business", title="本地企业", pages=[12 articles] }
```

### Fix 3: itemFields 默认值

**当前**: `itemFields` 默认 null，taxonomy item 仅有 title/url/publishAt/summary。
**目标**: 默认包含 cover/tags/categories 等常用字段。

### Fix 4: SSR 优先策略

**当前**: 前端 JS 从 taxonomy.json 二次重绘，绕开原分页/字段。
**目标**: term page 优先 SSR 渲染；taxonomy.json 的 `itemsByTerm` 确保包含完整字段。

### Fix 5: 模板匹配与文档

**当前**: `supports_taxonomy` 声明不够显式。
**目标**: 文档 + SKILL 中明确 taxonomy 模板约定和降级规则。
