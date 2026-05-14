# Bukit 引擎端部署功能 — 实施计划

## 概述

将 GitHub Pages 部署能力从外部 CI/CD 模板（`examples/github-pages-workflow.yml`）迁移到引擎内部，通过 **CLI 命令 + 可插拔 Provider** 架构实现"构建即部署"的一体化体验。首个 Provider 实现 GitHub Pages，架构预留 Netlify/Vercel/OSS 等扩展点。

## 当前状态分析

### 现状
- 部署完全依赖外部 GitHub Actions 模板（[github-pages-workflow.yml](file:///Users/ali/mydev/Git/Github/Bukit/examples/github-pages-workflow.yml)）
- 用户需手动复制 workflow 到仓库、配置 secrets
- `baseUrl` / `siteUrl` 的自动推导逻辑是 bash 脚本，不存于引擎代码
- 没有 `deploy` CLI 命令或配置节
- 没有 Provider 抽象

### 已有基础设施（可复用）
| 能力 | 位置 | 复用方式 |
|------|------|----------|
| CLI 命令注册系统 | [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs) | 注册 `deploy` 子命令 |
| 配置加载/验证 | [ConfigLoader.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs) + [ConfigValidator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs) | 新增 `DeployConfig` 节 |
| BuildContext 输出目录 | [BuildContext.OutputDir](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine.Abstractions/Plugins/BuildContext.cs) | 部署时读取产物路径 |
| 环境变量读取 | [EnvironmentHelper.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Shared/EnvironmentHelper.cs) | 读取 `GITHUB_TOKEN` 等凭证 |
| 日志系统 | [ConsoleLogger](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/BuildCommand.cs#L57) | 部署进度/错误日志 |
| site.url / site.baseUrl | [AppConfig](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs) | 部署目标 URL 计算 |

### 差距
1. 无 `DeployConfig` 配置模型
2. 无 `deploy` CLI 命令
3. 无 Provider 抽象接口
4. 无 `baseUrl` / `siteUrl` 自动推导的引擎代码
5. 无 `git` 操作封装（推送到 gh-pages 分支）

---

## 架构决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 触发方式 | `bukit deploy` 命令（手动触发）| 用户明确选择；部署不应耦合到每次 build |
| 配置位置 | `site.yaml` 新增 `deploy` 顶级配置节 | 部署配置语义独立，不适合塞入 externalPlugins.options |
| Provider 模型 | 引擎内 Provider 接口 + 实现 | 通用部署插件支持多平台，GitHub Pages 为首个 Provider |
| 凭证管理 | 环境变量 | 与现有 WebhookCommand 模式一致 |
| 分发方式 | 内置于 Bukit CLI 源码 | 随 CLI 一起发布，无需额外安装 |

> **注意**：虽然用户选择"外部协议插件"，但 `bukit deploy` 作为独立命令（非 after-build 钩子），其本质是 CLI 命令调用内置 Provider，不经过插件系统的 JSON 协议通信。Provider 接口保留了"可插拔"的设计意图，未来可迁移为真正的 protocol 插件。

---

## 设计方案

### 1. 新增 `bukit deploy` CLI 命令

注册于 [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs)：

```
bukit deploy [provider] [options]
  --provider      部署目标 (github-pages | netlify | ...)
  --config        配置文件路径
  --site          多站点名
  --output        输出目录（覆盖 build.output）
  --base-url      覆盖 site.baseUrl
  --site-url      覆盖 site.url
  --dry-run       仅预览，不实际部署
  --skip-build    跳过构建步骤（使用已有 dist）
  --branch        目标分支（GitHub Pages 默认 gh-pages）
  --message       提交信息（默认 "bukit deploy"）
```

### 2. 新增 `DeployConfig` 配置模型

在 [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs) 中新增：

```csharp
public sealed record DeployConfig
{
    public string? Provider { get; init; }              // "github-pages" | null
    public string? Branch { get; init; }                // 默认 "gh-pages"
    public string? Message { get; init; }               // 提交信息
    public string? Cname { get; init; }                 // 自定义域名
    public bool? KeepHistory { get; init; }              // 保留 git 历史（默认 false，即 --orphan）
    public IReadOnlyDictionary<string, object>? Options { get; init; }  // Provider 特有配置
}
```

site.yaml 示例：

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "deploy: site update"
  cname: example.com
  keepHistory: false
```

### 3. Provider 抽象接口

新建 `src/Bukit.Deploy/` 项目（或放在 `src/Bukit.Cli/Deploy/` 内）：

```csharp
public interface IDeployProvider
{
    string Name { get; }
    Task<DeployResult> DeployAsync(DeployContext context, CancellationToken ct);
}

public sealed record DeployContext
{
    public required string OutputDir { get; init; }
    public required string SiteUrl { get; init; }
    public required string BaseUrl { get; init; }
    public required DeployConfig Config { get; init; }
    public required ILogger Logger { get; init; }
}

public sealed record DeployResult
{
    public bool Success { get; init; }
    public string? DeployedUrl { get; init; }
    public string? Error { get; init; }
}
```

### 4. GitHub Pages Provider 实现

新建 `src/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs`，核心逻辑：

1. **前置条件检查**：
   - 检查 `git` 命令可用
   - 检查输出目录存在且非空
   - 检查 `GITHUB_TOKEN` 环境变量（用于 HTTPS 认证）
   - 检查是否在 git 仓库内

2. **部署流程**（模拟 `github-pages-workflow.yml` 的行为）：
   - `git init` 或 `git clone` 目标分支到临时目录
   - 将 `outputDir` 内容复制到临时目录
   - 生成/保留 `CNAME` 文件（如果配置了自定义域名）
   - 生成 `.nojekyll` 文件（禁用 Jekyll 处理）
   - `git add -A && git commit` 
   - `git push` 到目标分支
   - 输出部署后的 URL

3. **URL 自动推导**：
   - 从 `git remote origin` 解析 `owner/repo`
   - 区分 user/org pages（`<owner>.github.io`）和 project pages（`<owner>.github.io/<repo>`）
   - 自动设置 `baseUrl` 和 `siteUrl`（如果未显式指定）

### 5. DeployCommand 实现

新建 `src/Bukit.Cli/Commands/DeployCommand.cs`，流程：

```
1. 解析 CLI 参数 → DeployOptions
2. 加载 site.yaml → AppConfig
3. 合并 CLI 覆盖 → effective DeployConfig
4. 如果 --skip-build 为 false：
   a. 调用 BuildCommand 构建站点
5. 根据 provider 选择 IDeployProvider 实现
6. 构建 DeployContext（outputDir, siteUrl, baseUrl, config, logger）
7. 调用 provider.DeployAsync()
8. 输出结果（成功 URL 或 错误信息）
```

### 6. 文件变更清单

#### 新建文件

| 文件 | 职责 |
|------|------|
| `src/Bukit.Cli/Commands/DeployCommand.cs` | `bukit deploy` 命令入口 |
| `src/Bukit.Cli/Deploy/IDeployProvider.cs` | Provider 接口定义 |
| `src/Bukit.Cli/Deploy/DeployContext.cs` | 部署上下文模型 |
| `src/Bukit.Cli/Deploy/DeployResult.cs` | 部署结果模型 |
| `src/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs` | GitHub Pages Provider 实现 |
| `src/Bukit.Config/DeployConfig.cs` | 部署配置模型 |

#### 修改文件

| 文件 | 变更内容 |
|------|----------|
| [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs) | 新增 `DeployConfig? Deploy` 属性 |
| [ConfigLoader.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs) | 新增 `ReadDeployConfig()` 方法解析 `deploy:` 节点 |
| [ConfigValidator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs) | 新增 deploy 配置校验 |
| [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs) | 注册 `deploy` 命令及选项 |
| [Program.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Program.cs) | 添加 `deploy` 命令路由 |
| [Bukit.Cli.csproj](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Bukit.Cli.csproj) | 无需修改（无新依赖） |
| [guide/user/13-deploy-github-pages.md](file:///Users/ali/mydev/Git/Github/Bukit/guide/user/13-deploy-github-pages.md) | 更新文档，增加 `bukit deploy` 方式说明 |

---

## 验证步骤

1. **配置加载测试**：验证 `site.yaml` 中 `deploy:` 节点能正确解析为 `DeployConfig`
2. **URL 推导测试**：验证 user/org pages 和 project pages 的 `baseUrl`/`siteUrl` 自动推导逻辑
3. **dry-run 测试**：`bukit deploy --dry-run` 应输出将执行的 git 操作但不实际推送
4. **实际部署测试**：
   - 创建测试仓库
   - 配置 `GITHUB_TOKEN` 环境变量
   - 运行 `bukit deploy --provider github-pages`
   - 验证 GitHub Pages 站点可访问
5. **错误处理测试**：
   - 无 `GITHUB_TOKEN` 时应给出清晰错误
   - 非 git 仓库时应给出清晰错误
   - 输出目录为空时应给出清晰错误
6. **文档验证**：更新后文档完整覆盖 `bukit deploy` 使用方式

---

## 假设与约束

1. **git 命令可用**：假设部署环境中已安装 `git` CLI 工具
2. **仅 GitHub Pages**：首个版本仅实现 GitHub Pages Provider，其他平台留作扩展点
3. **单分支推送**：不支持同时部署到多个分支/环境
4. **HTTPS 认证**：通过 `GITHUB_TOKEN` 环境变量 + HTTPS 协议认证，不处理 SSH
5. **不修改 build 阶段**：`bukit deploy` 内部调用 `BuildCommand` 但不对构建逻辑做任何修改
6. **向后兼容**：`deploy` 配置节为可选，不影响现有 `site.yaml` 的解析
