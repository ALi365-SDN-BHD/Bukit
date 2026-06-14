# 测试体系

Bukit 的测试策略融合了单元测试、集成测试、冒烟测试、安全回归测试和基于 fixture 的验收测试。所有入口均支持 CI 和本地环境，一条命令即可运行。

## 脚本入口

| 脚本 | 用途 | 用法 |
|--------|---------|-------|
| `scripts/test-all.sh` | 一键全流程：restore → build → unit tests → quality gate → smoke → smoke-all → AOT publish | `bash scripts/test-all.sh [Release]` |
| `scripts/quality-gate.sh` | 覆盖率阈值（65%）、文件大小限制、编码检查、dotnet format | `COVERAGE_THRESHOLD=65 bash scripts/quality-gate.sh [Release]` |
| `scripts/smoke.sh` | 构建并验证 starter 示例站点 | `bash scripts/smoke.sh [Release]` |
| `scripts/smoke-all.sh` | 构建全部 7 个示例站点 + 9 个 fixture 站点，验证输出 | `bash scripts/smoke-all.sh [Release]` |
| `scripts/security-regression.sh` | 5 个模块的隔离安全测试（Shared/Config/CLI/Engine/Content） | `bash scripts/security-regression.sh [Release]` |
| `scripts/stress-test.sh` | 重复完整测试套件 N 次，捕获间歇性失败 | `bash scripts/stress-test.sh 20 [Release]` |

## CI 结构

GitHub Actions（`ci.yml`）运行 5 个 job：

| Job | OS 矩阵 | 触发条件 |
|-----|-----------|---------|
| `quality-gate` | ubuntu-latest | push, PR |
| `cross-platform-tests` | ubuntu, windows, macos | push, PR |
| `smoke-examples` | ubuntu-latest | push, PR |
| `native-aot` | ubuntu, windows, macos | push, PR |
| `stress-cli` | ubuntu-latest | 仅 `workflow_dispatch`（手动触发） |

## Fixture 站点

`tests/fixtures/` 下的 10 个 fixture 站点提供确定性的端到端验证：

| Fixture | 验证内容 |
|---------|-----------|
| `basic-markdown-site` | 最小 markdown 站点，index.html 生成 |
| `route-security-site` | 路由安全配置 |
| `safe-url-content-site` | 输出中的 URL 净化 |
| `plugin-policy-site` | 外部插件策略行为 |
| `output-safety-site` | 输出目录安全性 |
| `incremental-site` | 增量构建（首次 + 二次构建） |
| `i18n-site` | 多语言构建（en, zh-CN） |
| `taxonomy-site` | 分类法列表/术语页面生成 |
| `component-validation-site` | 组件/主题验证 |
| `dotfile-leak-site` | 敏感文件（.env, .key, .pfx, .git）不泄露到 dist/ |

每个 fixture 包含最小化的 `site.yaml`、`content/index.md`、`layouts/` 目录以及可选的 `static/` 文件。

### 冒烟验证

`smoke-all.sh` 对每次成功构建执行以下检查：
- `index.html` 存在（处理 i18n 子目录）
- `sitemap.xml` 包含 `<url>` 条目
- `rss.xml` 包含 `<channel>` 条目
- `search.json` 为合法 JSON
- 无 dotfile 泄露（`.env`、`.npmrc`、`.key`、`.pfx`、`.p12`、`.git/`）
- 输出中无危险 URL（`javascript:`、`data:text/html`、`file://`、`vbscript:`、`//evil.com`）

## 安全回归测试

`security-regression.sh` 隔离运行安全相关测试：

- **Shared**：`SafeUrl.ForLink/ForMedia/ForEmbed` 单元测试及协议相对 URL 拒绝
- **Config**：`ExternalPluginPolicy` 验证、配置异常路径
- **CLI**：路径穿越拒绝、配置异常处理
- **Engine**：路由安全、外部插件安全、插件失败模式
- **Content**：Block 渲染器 URL 安全性（8 个渲染器共 86 个测试）、Notion 富文本净化

## 测试协议插件

`ProtocolEchoPlugin`（`tests/ProtocolEchoPlugin/Program.cs`）为外部插件集成测试提供确定性模式：

| 模式 | Hook | 输出 |
|------|------|--------|
| `success`（默认） | 任意 | ok=true，附带示例输出文件 |
| `derive-success` | derive-pages | 1 个派生页面，路径 `/derived/derived-1/` |
| `derive-conflict` | derive-pages | 1 个页面，路径 `/blog/post-1/`（与测试内容冲突） |
| `derive-lastwins` | derive-pages | 1 个页面，路径 `/derived/conflict/`（不冲突） |
| `derive-plugin-a` | derive-pages | 1 个页面，路径 `/plugin-conflict/page/`（ID: plugin-a） |
| `derive-plugin-b` | derive-pages | 1 个页面，路径 `/plugin-conflict/page/`（ID: plugin-b，与 plugin-a 冲突） |
| `env` | after-build | 将 OPENAI_API_KEY、GITHUB_TOKEN、BUKIT_* 变量报告到文件 |
| `env-allowlist` | after-build | 将 PATH、HOME、NOTION_TOKEN、OPENAI_API_KEY、BUKIT_* 变量报告到 env-report.json |
| `error` | after-build | ok=false，附带错误消息 |
| `empty` | after-build | 无输出（空 stdin） |
| `sleep` | after-build | 休眠 1s，退出码 0 |
| `traversal` | after-build | 输出文件路径 `../escape.json`（应被拒绝） |
| `handshake-v2` | handshake | 协商 schema version 2 |

## 何时添加测试

- **单元测试**：`Bukit.Shared`、`Bukit.Config`、`Bukit.Content`、`Bukit.Engine`、`Bukit.Rendering` 中的新逻辑
- **Fixture 站点**：新增构建时行为、输出结构变更、安全边界
- **安全回归**：SafeUrl、外部插件协议、路由/输出路径验证的任何变更
- **冒烟测试**：影响示例站点构建或核心端到端路径的变更

## 架构概览

```
scripts/
  test-all.sh           → 一键全流程
  quality-gate.sh        → 覆盖率 + 格式化 + 编码检查
  smoke.sh               → 单站点冒烟
  smoke-all.sh           → 示例站点 + fixture 站点
  security-regression.sh → 隔离安全测试
  stress-test.sh         → 重复 N 次运行

tests/
  fixtures/              → 10 个确定性 fixture 站点
  ProtocolEchoPlugin/    → 用于集成测试的确定性外部插件
  Bukit.*.Tests/         → 单元/集成测试项目
```
