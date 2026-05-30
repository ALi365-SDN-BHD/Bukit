# Bukit 全量自动化测试系统 — 实施计划

## 现状分析

### 已存在且匹配的资产

| 资产 | 状态 | 备注 |
|------|------|------|
| `scripts/test-all.sh` | ✅ 已存在 | 与 spec 基本一致，echo 消息略有差异 |
| `scripts/stress-test.sh` | ✅ 已存在 | 与 spec 一致 |
| `scripts/quality-gate.sh` | ✅ 已存在 | 含文件大小门禁、编码检查、覆盖率阈值、format、冒烟测试 |
| `scripts/smoke-all.sh` | ✅ 已存在 | 覆盖 7 个示例站点 |
| `scripts/smoke.sh` | ✅ 已存在 | starter 站点的详细冒烟测试 |
| `.github/workflows/ci.yml` | ⚠️ 存在但需重构 | 目前仅 2 个 job (build-and-test, aot-check)，需拆分为多 job 矩阵 |

### 已存在但不完整

| 资产 | 问题 |
|------|------|
| `scripts/security-regression.sh` | 仅有 SafeUrl/Security 过滤，缺少 Config/CLI/Engine/Content 专项安全测试 |
| renderer URL sanitization 测试 | 仅 AudioBlockRenderer 有危险 URL 测试，Image/Video/Embed/Bookmark/File/Pdf/LinkPreview 均缺失 |
| SafeUrl 测试 | 无独立 SafeUrl 单元测试文件 |

### 缺失

| 资产 | 
|------|
| `tests/fixtures/` 全部 10 个 fixture 站点 |
| 各 BlockRenderer 的危险 URL 测试（7 个 renderer + rich text + mention） |
| CI 跨平台矩阵 (ubuntu/windows/macos) |
| CI stress-cli 手动触发 job |

---

## 实施步骤

### 步骤 1：补齐 Renderer 级 URL 安全测试

**文件**: `tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs` (新建)

为 7 个使用 `SafeUrl` 的 block renderer 添加危险/安全 URL 测试：

| Renderer | SafeUrl 方法 | 需测试 |
|----------|-------------|--------|
| AudioBlockRenderer | ForMedia() | ✅ 已有（补齐 file:// 和 vbscript:） |
| ImageBlockRenderer | ForMedia() | ❌ 新建 |
| VideoBlockRenderer | ForMedia()/ForEmbed() | ❌ 新建 |
| EmbedBlockRenderer | ForEmbed() | ❌ 新建 |
| BookmarkBlockRenderer | ForLink() | ❌ 新建 |
| FileBlockRenderer | ForLink() | ❌ 新建 |
| PdfBlockRenderer | ForLink() | ❌ 新建 |
| LinkPreviewBlockRenderer | ForLink() | ❌ 新建 |

**测试矩阵**:

危险 URL（预期拒绝/null）:
- `javascript:alert(1)`
- `data:text/html,<script>alert(1)</script>`
- `file:///etc/passwd`
- `vbscript:msgbox(1)`
- `//evil.com`
- `//evil.com/x.js`
- `//cdn.evil.com/audio.mp3`

安全 URL（预期正常渲染）:
- `https://example.com/resource`
- `http://example.com/resource`
- `/assets/local-file.png`
- `/audio/local.mp3`
- `mailto:user@example.com` (仅 link renderer)
- `tel:+1234567890` (仅 link renderer)

**额外添加**: NotionRichTextRenderer 的扩展危险 URL 测试（现有仅测了 javascript:），以及 mention link 的危险 URL 测试。

**文件**: `tests/Bukit.Shared.Tests/SafeUrlTests.cs` (新建)

为 `SafeUrl` 三个方法添加独立单元测试：
- `ForLink()` — 验证 http/https/mailto/tel 白名单 + 拒绝 // 协议相对 URL
- `ForMedia()` — 验证 http/https 白名单 + 拒绝 // 协议相对 URL  
- `ForEmbed()` — 验证 https 白名单 + 拒绝 // 协议相对 URL
- 验证空/null 输入处理
- 验证 `//` 协议相对 URL 全部拒绝
- 验证 rel="noopener noreferrer" 输出

---

### 步骤 2：创建 10 个 Fixture 站点

在 `tests/fixtures/` 下创建：

每个 fixture 结构：
```
<fixture-name>/
  site.yaml
  content/
    index.md (或更多内容文件)
  layouts/
    _default.html (Scriban 模板)
  static/ (按需)
```

#### 2.1 `basic-markdown-site`
- 最小 Markdown 站点，验证 `dist/index.html` 生成
- 内容：简单的标题 + 段落

#### 2.2 `route-security-site`
- 验证不安全路由被拒绝
- site.yaml 中手动配置 unsafe routes：`../x`, `../../x`, `/absolute/path`, `C:\Windows`, `\\server\share`, `CON`, `PRN`, `AUX`, `NUL`, `COM1`, `LPT1`, `%2F`, `%5C`, `//evil.com`, `https://evil.com`
- 预期：unsafe routes 抛出 `ConfigException`

#### 2.3 `safe-url-content-site`
- 含危险 URL 的内容，验证生成输出中不含危险 URL
- 内容文件中嵌入 `javascript:`, `data:`, `file:`, `vbscript:`, `//evil.com`, 同时含安全 URL
- 验证 `dist/` 中不含任何危险 URL

#### 2.4 `plugin-policy-site`
- 验证 `externalPluginPolicy` 行为
- 4 个 site YAML 变体：deny/warn/allow/invalid(alow)
- deny → 不执行插件
- warn → 执行插件 + 日志警告
- allow → 执行插件
- invalid → ConfigException

#### 2.5 `output-safety-site`
- 验证输出目录安全性
- 安全：output: dist → 构建成功
- 不安全：output: `..`, `/`, `C:\Users` → 构建失败

#### 2.6 `incremental-site`
- 验证增量构建行为
- 首次构建成功 → 二次构建成功 → manifest/cache 创建
- 修改文件触发重建
- 未修改文件被跳过/缓存

#### 2.7 `i18n-site`
- 多语言构建验证
- 默认语言 + 第二语言输出
- alternate links 配置
- sitemap/rss/search 模式行为

#### 2.8 `taxonomy-site`
- 分类法（taxonomy）生成验证
- 分类列表页 + 分类项页
- 禁用 taxonomy → 不生成分类输出
- taxonomy JSON 有效性

#### 2.9 `component-validation-site`
- 组件/主题验证
- 有效组件渲染
- 无效组件 props → warn 或 fail（根据配置）
- strict mode → 无效组件直接失败

#### 2.10 `dotfile-leak-site`
- 敏感文件不泄露到输出
- static/ 下放置：`.env`, `.npmrc`, `.yarnrc`, `private.key`, `cert.pfx`, `cert.p12`, `.git/config`
- 验证 `dist/` 中无任何上述文件

---

### 步骤 3：扩展 `scripts/security-regression.sh`

在现有的基础上增加以下过滤：

```bash
# Config 安全测试
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~ExternalPluginPolicy|FullyQualifiedName~Path|FullyQualifiedName~Traversal"

# CLI 安全测试
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~CIEnv|FullyQualifiedName~PathTraversal|FullyQualifiedName~NoConfig"

# Engine 安全测试
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~Security|FullyQualifiedName~SafePath|FullyQualifiedName~Output|FullyQualifiedName~Plugin"

# Content 安全测试
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl|FullyQualifiedName~Renderer|FullyQualifiedName~Audio|FullyQualifiedName~Notion"
```

**注意**：过滤器需与实际测试方法名匹配，根据新增的测试方法名进行调整。

---

### 步骤 4：扩展 `scripts/smoke-all.sh`

在现有的 7 个示例站点检查基础上，添加：

**成功构建 fixture 检查** (9 个)：
- `basic-markdown-site` — index.html 存在
- `safe-url-content-site` — 无危险 URL，index.html 存在
- `plugin-policy-site` (allow/warn) — 构建成功 + 警告日志
- `output-safety-site` — dist/index.html 存在
- `incremental-site` — 首次构建 + 二次构建成功
- `i18n-site` — 默认语言 + 第二语言输出
- `taxonomy-site` — 分类列表页 + 分类项页存在
- `component-validation-site` — 有效组件渲染
- `dotfile-leak-site` — 无敏感文件泄露

**预期失败 fixture 检查** (3 个)：
- unsafe route config — 必须失败
- unsafe output config (output: `..`) — 必须失败
- invalid `externalPluginPolicy: alow` — 必须失败

若预期失败 fixture 反而构建成功，脚本必须 exit 1。

**输出验证**（每个成功的站点）：
- `sitemap.xml` 有效（若启用）
- `rss.xml` 有效（若启用）
- `search.json` 有效 JSON（若启用）
- 无 `.env`, `.npmrc`, `.key`, `.pfx`, `.p12`, `.git` 泄露
- 无危险 URL

---

### 步骤 5：重构 GitHub Actions CI

将现有的 2 个 job CI 重构为：

```yaml
jobs:
  quality-gate:
    runs-on: ubuntu-latest
    env:
      COVERAGE_THRESHOLD: "65"
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: bash scripts/quality-gate.sh Release

  cross-platform-tests:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore bukit.slnx
      - run: dotnet build bukit.slnx -c Release
      - run: dotnet test bukit.slnx -c Release --no-build

  smoke-examples:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: bash scripts/smoke.sh Release
      - run: bash scripts/smoke-all.sh Release

  native-aot:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -p:PublishAot=true

  stress-cli:
    if: github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: bash scripts/stress-test.sh 20 Release
```

**注意**：shell 脚本在 Windows 上可能不兼容。Windows job 中的 bash 调用需使用 `git-bash` 或转为 `dotnet` 命令直接调用。若 Windows 不支持 bash，跨平台测试和 AOT job 使用 `dotnet` 命令，smoke job 保持在 Ubuntu。

---

### 步骤 6：更新 `scripts/test-all.sh` 使其与 spec 完全一致

当前 `test-all.sh` 与 spec 仅 echo 消息略有差异，更新为：
```bash
echo "=== smoke ==="
echo "=== smoke all ==="
echo "=== native aot publish ==="
echo "=== test-all OK ==="
```

无需功能更改。

---

### 步骤 7：验证与最终检查

按 spec 验收标准逐项检查：

- [ ] `bash scripts/test-all.sh` 本地通过
- [ ] `bash scripts/stress-test.sh 20 Release` 通过
- [ ] GitHub Actions 在 Ubuntu/Windows/macOS 通过
- [ ] Native AOT publish 通过
- [ ] 所有 7 个示例站点构建通过
- [ ] 所有 10 个 fixture 站点构建通过或预期失败
- [ ] 覆盖率保持 >= 65%
- [ ] 无测试遗留 CWD 变更
- [ ] 无测试遗留环境变量变更
- [ ] 无测试遗留后台任务
- [ ] 无生成输出泄露敏感文件
- [ ] 无生成输出含危险 URL
- [ ] CI 清楚区分 quality-gate/cross-platform/smoke/AOT/stress job
- [ ] 预期失败 fixture 确实失败
- [ ] 安全回归测试可独立运行

---

## 文件变更汇总

| 文件 | 操作 | 
|------|------|
| `tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs` | **新建** — 7 个 block renderer 的危险/安全 URL 测试 |
| `tests/Bukit.Shared.Tests/SafeUrlTests.cs` | **新建** — SafeUrl 三方法的独立单元测试 |
| `tests/fixtures/basic-markdown-site/` | **新建** — 含 site.yaml, content/, layouts/ |
| `tests/fixtures/route-security-site/` | **新建** — 含不安全路由配置 |
| `tests/fixtures/safe-url-content-site/` | **新建** — 含危险 URL 内容 |
| `tests/fixtures/plugin-policy-site/` | **新建** — 含 deny/warn/allow/invalid 配置 |
| `tests/fixtures/output-safety-site/` | **新建** — 含不安全输出目录配置 |
| `tests/fixtures/incremental-site/` | **新建** |
| `tests/fixtures/i18n-site/` | **新建** |
| `tests/fixtures/taxonomy-site/` | **新建** |
| `tests/fixtures/component-validation-site/` | **新建** |
| `tests/fixtures/dotfile-leak-site/` | **新建** |
| `scripts/security-regression.sh` | **修改** — 扩展过滤范围 |
| `scripts/smoke-all.sh` | **修改** — 添加 fixture 构建检查 + 预期失败检查 |
| `scripts/test-all.sh` | **微调** — echo 消息对齐 spec |
| `.github/workflows/ci.yml` | **重构** — 拆分为 5 个 job (quality-gate, cross-platform-tests, smoke-examples, native-aot, stress-cli) |

---

## 实施顺序

1. **步骤 1** — 先新建 URL 安全测试（无依赖，纯新增）
2. **步骤 2** — 创建 fixture 站点（无依赖，纯新增）
3. **步骤 3** — 扩展 security-regression.sh（依赖步骤 1 的测试类名）
4. **步骤 4** — 扩展 smoke-all.sh（依赖步骤 2 的 fixture）
5. **步骤 5** — 重构 CI（依赖步骤 3, 4）
6. **步骤 6** — 微调 test-all.sh（独立，最后做）
7. **步骤 7** — 运行全部验证
