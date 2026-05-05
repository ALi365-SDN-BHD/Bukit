# 增量构建（manifest / cache-dir / render-skip 原因）

增量构建用于在模板与内容未变化时跳过渲染，提高本地与 CI 构建速度。

实现参考：
- `src/Bukit.Engine/Incremental/IncrementalBuildEngine.cs`（hash 计算与增量判定）
- `src/Bukit.Engine/PageRenderDispatcher.cs`（增量渲染调度）
- `src/Bukit.Engine/Incremental/BuildManifest.cs`
- `src/Bukit.Engine/Incremental/HashUtil.cs`

## 开关与目录

- 默认：启用增量构建
- CLI：
  - `--incremental` / `--no-incremental`
  - `--cache-dir <dir>`（默认 `<rootDir>/.cache`）
  - `--jobs <n>`：控制渲染并行度（默认 CPU 核心数；与增量判定独立）

缓存目录与 clean 的关系见：[缓存与清理](./cache-clean.md)。

## manifest 文件

manifest 用于记录“上次渲染时的指纹”，默认路径：
- 单语言：`<cacheDir>/build-manifest.json`
- 多语言：`<cacheDir>/build-manifest.<lang>.json`（例如 `build-manifest.zh-CN.json`）

manifest JSON 结构：

```json
{
  "version": 1,
  "templateHash": "<sha256 hex>",
  "entries": {
    "<normalizedOutputPath>": {
      "outputPath": "blog/hello/index.html",
      "url": "/blog/hello/",
      "template": "pages/post.html",
      "contentHash": "<sha256 hex>",
      "routeHash": "<sha256 hex>",
      "templateHash": "<sha256 hex>"
    }
  }
}
```

entry 键为 `NormalizeRelPath(outputPath)`（反斜杠统一转正斜杠）。manifest 加载失败时输出 stderr 警告并使用空 manifest（等效全量渲染）。

构建完成后，manifest 会自动移除不再属于当前构建输出集的旧 entry（例如已删除的文章），保持 manifest 与实际产物一致。

## 跳过渲染的判定条件

某个页面可跳过渲染需同时满足：

1. 增量开关开启
2. manifest 存在且包含该页面的 entry
3. 输出文件存在
4. 三个 hash 都一致：
   - `TemplateHash`：模板目录内容 hash（layoutsDir）
   - `ContentHash`：内容指纹，SHA256 覆盖以下字段：`Id`, `Title`, `Slug`, `PublishAt`（ISO "O"格式）, `meta.type`, `meta.summary`, Fields 指纹（键按 OrdinalIgnoreCase 排序，每个 field 取 key/Type/Value）, `ContentHtml`
   - `RouteHash`：路由指纹（url/outputPath/template）

补充：

- 首页与列表页（例如 `index.html`、`blog/index.html`、`pages/index.html`）也会写入 manifest，并参与增量判定。列表页使用专门的 `ListContentHash`：基于 templateHash + template 路径 + 每个子项的 url/outputPath/contentHash/routeHash（优先从 manifest 读取已有指纹，不存在时重新计算）。
- 插件派生页（taxonomy/pagination/archive 等）与普通内容页使用相同的增量判定逻辑，无特殊处理。

## renderReasons（诊断意义）

当需要渲染时，引擎会记录原因统计。通过 `--metrics <path>` 输出的 JSON 文件中，`variants[].reasons` 包含每种 renderReason 的计数。原因类型：
- `new_page`：manifest 中不存在
- `output_missing`：输出文件不存在
- `template_changed`：模板 hash 变化
- `content_changed`：内容指纹变化
- `route_changed`：路由指纹变化
- `full_render`：关闭增量时的全量渲染

当页面被跳过/或是列表页的增量判定时，可能还会看到：

- `unchanged`：内容页命中增量缓存而跳过
- `list_render`：列表页需要重渲染
- `list_unchanged`：列表页命中增量缓存而跳过

## 常见问题与排查

1. “改了模板但没生效”
   - 确认模板目录是否指向了预期 layouts（见 `theme.layouts`）
   - 确认未在多个 layoutsDir 间切换导致误判
2. “本地渲染很慢”
   - 确认未使用 `--no-incremental`
   - 检查 cache-dir 是否可写
3. “多语言增量缓存互相污染”
   - 设计上按语言 suffix 分离 manifest；检查 language 值是否被非预期覆盖
