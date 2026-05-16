Bukit 当前隔离机制全览
3.1 现有隔离层级

Plain Text

┌─────────────────────────────────────────────────────┐
│                Bukit 隔离层级总览                      │
├─────────────┬───────────────────────────────────────┤
│ 插件级       │  ProcessPluginInvoker  (独立进程)      │
│ (最安全)    │  WasmPluginInvoker        (WASM沙箱)   │
│             │  ExternalAssembly SHA256   (白名单)    │
├─────────────┼───────────────────────────────────────┤
│ 渲染级       │  PageRenderDispatcher.Parallel.ForEach │
│ (中等隔离)  │  SemaphoreSlim 写锁 (按输出路径)        │
│             │  ConcurrentDictionary 线程安全容器       │
├─────────────┼───────────────────────────────────────┤
│ 调度级       │  PluginRunner → 顺序执行 (foreach)      │
│ (无隔离)    │  共享 BuildContext (RootDir, OutputDir) │
│             │  内置插件直接访问文件系统                 │
└─────────────┴───────────────────────────────────────┘
3.2 关键代码路径
插件顺序执行 — PluginRunner.cs:L22


C#

// 顺序 foreach，不是并行
foreach (var (plugin, _) in GetOrderedPlugins(context))
{
    if (plugin is IDerivePagesAsyncPlugin deriveAsync)
        var pages = await deriveAsync.DerivePagesAsync(context, cancellationToken);
    // 插件失败后，下一个插件继续执行（warn 模式）
}
页面并行渲染 — PageRenderDispatcher.cs


C#

// 并行但共享输出目录，通过文件级写锁保证安全
await Parallel.ForEachAsync(workItems, parallelOptions, async (work, ct) =>
{
    await WriteUtf8LockedAsync(outputRoot, relativePath, html, writeLocks, ct);
});
WASM 沙箱 — WasmPluginInvoker.cs


C#

// 最深度的隔离
wasiConfig.WithPreopenedDirectory(outputDir, "/out",
    WasiDirectoryPermissions.Write, WasiFilePermissions.Write);
// 网络: 强制禁止
// 内存: 限制 maxMemoryMb
// 超时: timeoutMs
// 文件系统: 只能写 /out
3.3 现有机制的局限性
局限	影响
I18n 语言变体顺序构建	SiteEngine L92 中 for 循环逐个构建每个语言，无法并行
插件顺序执行	foreach 导致耗时插件阻塞后续插件
开发期无隔离	bukit preview 时修改模板直接影响预览输出，无法做 A/B 对比
内置插件无沙箱	built-in/generated 插件直接访问文件系统和 BuildContext，依赖开发者信任
四、Worktree 隔离在 Bukit 中的应用分析
4.1 适用场景与方案
场景 A：I18n 多语言变体并行构建 ⭐⭐⭐⭐⭐
当前状态：SiteEngine L92-L118 顺序构建每种语言变体。

Worktree 方案：


Plain Text

bukit build --i18n-parallel

主仓库 (zh-CN 内容源)
        │
        ├── worktree: langs/en  → bukit build (language=en, output=dist/en)
        ├── worktree: langs/ms  → bukit build (language=ms, output=dist/ms)
        └── worktree: langs/ja  → bukit build (language=ja, output=dist/ja)
                │
                └── 所有 worktree 完成后
                    I18nOutputMerger.GenerateRootOutputs()
                    → 合并 sitemap.xml / RSS / search.json
伪代码实现思路：


C#

// SiteEngine.cs — 用 worktree 替代串行 for 循环
var tasks = languages.Select(async lang =>
{
    var worktreePath = Path.Combine(rootDir, ".worktrees", lang);
    GitWorktree.Create(rootDir, worktreePath, $"i18n/{lang}");
    
    var result = await BuildInWorktreeAsync(worktreePath, lang, config);
    return (lang, result);
});

var results = await Task.WhenAll(tasks);
I18nOutputMerger.GenerateRootOutputs(config, outputDir, rootBaseUrl, results.ToDictionary(...));
场景 B：多主题并行生成 ⭐⭐⭐⭐
当前状态：只支持单一主题（site.yaml 中 theme.name: "alt"）。

Worktree 方案：


Plain Text

bukit build --theme-parallel alt seo-best-practice

主仓库 (content/)
        │
        ├── worktree: theme-alt     → bukit build --theme alt     → dist/alt/
        └── worktree: theme-seo     → bukit build --theme seo     → dist/seo/
                │
                └── 产出两个独立的站点，用于 A/B 对比或 CI 质量检查
场景 C：插件开发/测试沙箱 ⭐⭐⭐
当前状态：插件开发时，bukit dev 或 bukit build 直接在主项目上运行，插件的错误可能破坏正常构建。

Worktree 方案：


Plain Text

bukit plugin test ./MyNewPlugin.dll

主仓库
        │
        └── worktree: plugin-test  →  加载 MyNewPlugin.dll
                                      运行完整构建流程
                                      测试结果返回 → 删除 worktree
                                      主工作区完全不受影响
场景 D：增量构建预测 / Dry Run ⭐⭐⭐
当前状态：增量构建直接修改 dist/ 目录。

Worktree 方案：


Plain Text

bukit build --dry-run --diff

主仓库
        │
        └── worktree: dry-run  →  bukit build → dist/
                                  与主仓库 dist/ 对比差异
                                  输出变更预览 → 删除 worktree
4.2 不适合 Worktree 的场景
场景	原因
单页面并行渲染	PageRenderDispatcher 的文件级写锁已足够，worktree 开销大于收益
插件运行时沙箱	已有 Process/WASM 隔离，安全级别更高
内容源并行加载	CompositeContentProvider 的 Task.WhenAll 已解决
非 Git 仓库	Worktree 依赖 Git，而 Bukit 站点不一定初始化了 Git
CI 中频繁构建	Worktree 创建/删除 I/O 开销可能抵消并行收益
4.3 优势分析

Plain Text

┌───────────────────────────────────────────────────────────┐
│           Worktree 隔离 vs Bukit 现有机制                   │
├─────────────┬──────────────────┬──────────────────────────┤
│ 维度         │ 现有机制          │ Worktree 隔离             │
├─────────────┼──────────────────┼──────────────────────────┤
│ 隔离级别     │ 文件锁/进程边界   │ 完整文件系统隔离          │
│ 并行粒度     │ 页面级/插件顺序   │ 变体级/构建级             │
│ 上下文污染   │ 共享 BuildContext │ 每个 worktree 独立上下文  │
│ 失败隔离     │ 插件 warn 模式    │ 失败 worktree 直接丢弃    │
│ 增量回滚     │ 需手动恢复        │ git worktree remove 即回滚 │
│ 配置切换     │ 需修改 site.yaml  │ worktree 之间独立配置     │
│ 额外开销     │ 无                │ worktree checkout + merge │
│ 依赖 Git     │ 否                │ 是                        │
└─────────────┴──────────────────┴──────────────────────────┘
4.4 限制与权衡

Plain Text

限制 1: Git 依赖
  → Worktree 要求项目是 Git 仓库
  → 解决方案: bukit init 时自动 git init; 或提供非 Git 的目录复制回退方案

限制 2: I/O 开销
  → git worktree add 需要 checkout 文件
  → 对于大型站点（>10000 文件），创建 worktree 可能耗时
  → 缓解: 使用 --detach 减少分支创建开销; 只 checkout 必要文件

限制 3: 合并复杂度
  → 当 worktree 修改了相同文件时需要合并
  → 对于构建输出（生成文件），合并无意义——每个变体产出独立文件
  → 这是优势：生成的文件天然不冲突

限制 4: 开发心智模型
  → 引入新的概念层次，增加理解成本
  → 建议: 仅在 `--parallel` 模式下自动启用，默认保持现有行为
五、实践建议
5.1 优先级排序

Plain Text

🥇 P0: I18n 并行构建 (场景 A)
  理由: 现有 for 循环明确可并行，提升最直接

🥈 P1: 多主题并行生成 (场景 B)
  理由: 对 CI/CD 和主题测试场景价值大

🥉 P2: 插件测试沙箱 (场景 C)
  理由: 增强开发体验，但现有 WASM 沙箱已覆盖安全隔离

🏅 P3: Dry Run 预测 (场景 D)
  理由: 锦上添花，可在增量构建成熟后再实现
5.2 最小可行实现路径

C#

// BuildPathUtils.cs 新增
public static class GitWorktreeHelper
{
    public static bool IsGitRepository(string rootDir)
        => Directory.Exists(Path.Combine(rootDir, ".git"));

    public static async Task<string> CreateWorktreeAsync(
        string rootDir, string branchName, string targetPath)
    {
        await RunGitAsync(rootDir, "worktree", "add", targetPath, branchName);
        return targetPath;
    }

    public static async Task RemoveWorktreeAsync(string rootDir, string targetPath)
    {
        await RunGitAsync(rootDir, "worktree", "remove", targetPath, "--force");
    }

    public static string GetWorktreesDir(string rootDir)
        => Path.Combine(rootDir, ".worktrees");
}
5.3 架构集成

Plain Text

SiteEngine.BuildAsync()
    │
    ├─ if (--parallel && IsGitRepo && languages.Count > 1)
    │   └─ BuildI18nParallelAsync()  ← 新增: worktree 并行路径
    │       ├─ foreach lang → 创建 worktree
    │       ├─ Task.WhenAll(BuildVariantInWorktreeAsync)
    │       └─ I18nOutputMerger.GenerateRootOutputs()
    │
    └─ else
        └─ 现有串行路径 (保持不变)
六、总结
Git Worktree 的本质是文件系统级的多版本并行工作空间。在 ai-website-cloner-template 中，它解决了 AI 编码代理之间的上下文污染问题——每个代理在隔离的文件系统中独立工作，互不干扰。

对于 Bukit 项目，Worktree 隔离最直接的价值是将 I18n 多语言变体构建从串行改为并行——当前 SiteEngine:L92-L118 的 for 循环是明确的并行化机会。其次，多主题并行生成可以为 CI/CD 提供更快的质量反馈。

不过需要注意的是，Bukit 现有的插件隔离（Process/WASM）在安全隔离方面已优于 Worktree（WASM 沙箱可以限制内存、网络、文件系统，这是 worktree 做不到的）。Worktree 的优势在于上下文隔离和并行能力，而不是安全防护。

核心区别：Worktree 隔离 = 并行 + 上下文隔离；进程/WASM 隔离 = 安全沙箱。两者解决的是不同层次的问题，可以互补而非替代。

