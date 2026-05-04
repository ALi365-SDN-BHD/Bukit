# 外部插件协议（External Plugin Protocol）v1/v2

`external-protocol` 是 Bukit 为 AOT 场景提供的动态扩展方案。

它的目标不是替代内置插件或 generated 插件，而是在 **不依赖外部 DLL 反射加载** 的前提下，提供可动态安装的插件能力。

## 1. 适用场景

- AOT 模式下需要动态扩展
- 希望插件独立发布，不重新编译主程序
- 希望未来兼容 `process` 与 `wasm` 等不同宿主

## 2. 当前能力范围

当前支持：

- `runtime: process`
- `runtime: wasm`
- `hooks: after-build, derive-pages`
- `stdin/stdout + JSON`
- `handshake` 协商与 `v2 -> v1` 降级

当前限制：

- wasm 默认禁网（`wasmAllowNetwork` 仅允许 `false`）
- wasm 文件系统仅允许 `none|output-only`
- 插件输出写盘必须经过主机侧 `outputs[]` 安全校验

## 3. 配置示例

```yaml
site:
  externalProtocolIncludeRoutedPages: false
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/sample-plugin.exe
      hooks:
        - after-build
        - derive-pages
      enabled: true
      timeoutMs: 5000
      wasmProfile: wasi-preview1
      maxMemoryMb: 64
      capabilities:
        - emit-outputs
      options:
        mode: demo
        processArgs:
          positionals:
            - plugins/sample-plugin.dll
            - after-build
          named:
            profile: prod
            dry-run: false
```

`externalProtocolIncludeRoutedPages` 用于控制 `after-build` 请求是否携带全量 `routedPages`：

- 默认 `false`：发送空数组，减少大站点 JSON 载荷
- 显式 `true`：发送全量 `routedPages`
- `options.arguments` 已禁用；请改用 `options.processArgs.positionals/named`
- 若启用 `site.externalAssemblyTrustMode: strict`，必须提供 `site.externalAssemblyAllowlist`

按需开启示例：

```yaml
site:
  externalProtocolIncludeRoutedPages: true
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/sample-plugin.exe
      hooks: [after-build]
```

## 4. 请求结构

主程序会向插件的 stdin 写入 JSON：

```json
{
  "schemaVersion": "1",
  "hook": "after-build",
  "plugin": {
    "name": "sample",
    "version": "protocol-v1"
  },
  "site": {
    "baseUrl": "/",
    "language": "zh-CN",
    "title": "Test"
  },
  "config": {
    "pluginOptions": {
      "mode": "demo"
    }
  },
  "afterBuild": {
    "outputDir": "dist",
    "routedPages": []
  }
}
```

说明：
- `routedPages` 字段始终存在
- 默认配置下其值为空数组
- 仅当 `site.externalProtocolIncludeRoutedPages: true` 时传全量路由信息

`derive-pages` 请求会使用同样的 envelope，并把 payload 放在 `derivePages` 字段，响应通过 `derivedPages` 返回页面列表。

## 5. 响应结构

插件通过 stdout 返回 JSON：

```json
{
  "ok": true,
  "logs": [
    { "level": "info", "message": "ok" }
  ],
  "outputs": [
    {
      "path": "plugin-output.json",
      "contentType": "application/json",
      "text": "{\"ok\":true}"
    }
  ]
}
```

`derive-pages` 响应示例（简化）：

```json
{
  "ok": true,
  "derivedPages": [
    {
      "id": "derived-1",
      "title": "Derived 1",
      "slug": "derived-1",
      "publishAt": "2026-01-01T00:00:00+00:00",
      "contentHtml": "<p>Derived</p>",
      "meta": { "type": "page" },
      "url": "/derived/derived-1/",
      "outputPath": "derived/derived-1/index.html",
      "template": "pages/page.html",
      "lastModified": "2026-01-01T00:00:00+00:00"
    }
  ]
}
```

失败时可返回：

```json
{
  "ok": false,
  "error": {
    "code": "PLUGIN_ERROR",
    "message": "plugin failed"
  }
}
```

## 6. 安全边界

- `outputs.path` 必须是相对输出目录的相对路径
- 不允许绝对路径
- 不允许 `..` 越界
- 主程序统一负责真正写文件

这意味着插件不能直接决定写到任意磁盘位置。

## 7. 与 failMode 的关系

协议插件复用现有 `site.pluginFailMode`：

- `strict`：协议插件失败时中断构建
- `warn`：记录错误并继续

## 8. 协议协商（v2）

主程序执行 `after-build` 时会先发 `hook=handshake`：

- 协商成功并返回 `negotiatedSchemaVersion=2`：走 v2 请求
- 协商失败或返回非法 JSON：自动降级到 v1
- 降级后仍失败：按 `site.pluginFailMode` 处理

这保证了新主程序对旧插件的兼容。

补充：
- 同一个 `BuildContext` 内会缓存握手协商结果
- 同一 external plugin 的重复 `after-build` 执行不会重复握手

## 9. 与 AOT 的关系

`external-protocol` 的重点是：

- 主程序保持 AOT 友好
- 外部插件不走 `Assembly.LoadFrom`
- 动态扩展能力来自协议，而不是来自反射加载外部 DLL

## 10. wasm 支持

当前 external-protocol 已支持 `runtime: wasm`，并复用与 `process` 相同的协议输入输出模型。

当前约束：

- `wasmProfile` 仅支持 `wasi-preview1`
- `capabilities` 仅允许 `emit-outputs`
- `wasmFsMode` 仅允许 `none|output-only`
- `wasmAllowNetwork` 仅允许 `false`

说明：

- 主机仍通过协议响应中的 `outputs[]` 回收产物；
- 最终落盘继续复用 `ProtocolOutputWriter` 路径边界校验，不允许越界写入；
- `derive-pages` 与 `after-build` 均可在 wasm 运行时下执行。
- 当 `wasmFsMode=output-only` 时，WASI 仅预开 outputDir 到 guest `/out`；
- `wasmAllowNetwork=true` 会在运行时 fail-fast（与配置校验保持一致）。
- 统一错误关键字：`[plugin-timeout]`、`[plugin-exit]`、`[plugin-protocol]`、`[plugin-policy]`、`[plugin-init]`、`[plugin-runtime]`。

## 11. 运维建议（上线前）

能力边界：

- process：能力更强，适合需要网络/外部运行时依赖的插件；
- wasm：默认受限沙箱，适合纯协议输入输出与受控产物写入；
- 默认保持 process 以确保兼容性，再按插件粒度灰度启用 wasm；
- 对第三方插件优先使用 wasm，对内部高信任插件可保留 process。

默认禁网原因：

- 降低数据外传与供应链投毒风险；
- 提高构建可复现性（不依赖外部网络波动）；
- 与“最小权限”一致，后续按 capability 渐进开放。

上线检查清单：

- 配置合法：`runtime/hooks/timeoutMs/wasmProfile/maxMemoryMb/wasmFsMode/wasmAllowNetwork`；
- process 参数仅使用 `options.processArgs`，禁止 `options.arguments`；
- 外部 DLL 路径启用 `externalAssemblyTrustMode + externalAssemblyAllowlist`；
- CI 已通过：Build/Format/Unit/WASM protocol/Coverage/Vulnerability/AOT/Smoke；
- 关键故障演练：超时、非法 JSON、路径越界、冲突策略（strict/warn）。

## 12. 故障排查路径

按错误关键字定位：

- `[plugin-timeout]`：优先检查 `timeoutMs` 与插件死循环/阻塞；
- `[plugin-exit]`：检查插件进程退出码与 stderr；
- `[plugin-protocol]`：检查 stdout 是否为合法 JSON、字段是否匹配 hook；
- `[plugin-policy]`：检查是否触发禁网/权限策略；
- `[plugin-init]`：检查 wasm 导出函数（`_start`/`run`）与入口文件；
- `[plugin-runtime]`：检查运行时异常与依赖环境。

建议排查顺序：

1. 先看 `BuildContext` 中 `PluginExecutions` 的 hook/name/success；
2. 再看 stderr 关键字并对照上述分类；
3. 最后最小化复现（单插件、单 hook、固定输入）定位根因。
