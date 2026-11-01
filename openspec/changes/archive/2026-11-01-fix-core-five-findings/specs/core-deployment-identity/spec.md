## ADDED Requirements

### Requirement: Deployment identity comes from the configured project scope

GitHub Pages 部署 MUST 以 `--config` 解析出的项目根目录作为仓库身份和传输上下文。公共 `DeployAsync(DeployContext, CancellationToken)` MUST 保留签名，并以调用时工作目录作为项目根目录委托内部重载。仓库 origin MUST 先在项目目录、使用清理后的子进程环境读取 `git config --worktree --includes --null --get-all remote.origin.url`；仅当退出码为 1（键不存在）时 MUST 回退到 `--local`。选中作用域 MUST 采用 Git fetch URL 的首值语义，保留空首值；首值为空、无效或非直接 GitHub 地址时 MUST 失败，不跳到后续 URL、较低优先级作用域或 global/system 配置。其他退出码、超时和读取失败 MUST 作为错误处理，不能伪装为缺键。

#### Scenario: Cross-project `--config` (DEP-01, DEP-02, DEP-04, DEP-05)
- **WHEN** 当前目录属于 A 仓库，`--config` 指向 B 项目，或当前目录不在任何仓库；配置位于仓库子目录；且 `--output` 覆盖产物位置
- **THEN** origin 查询工作目录为 B 或其所属仓库，部署仓库身份属于 B，`--output` 只选择产物位置

#### Scenario: Missing or global-only identity (DEP-03, DEP-10)
- **WHEN** 独立项目 B 没有有效项目 origin，而 A 或 global/system 存在同名 `origin`，或子进程继承指向 A 的定位变量
- **THEN** 部署失败，不使用全局同名值或其他工作目录兜底，不进入推送路径

#### Scenario: Worktree first-value and read errors (DEP-12, DEP-14)
- **WHEN** worktree 覆盖存在、仅 local 存在、local include 生效、多 URL 的首值为空或无效，或配置读取失败；或配置位于仓库子目录、linked worktree
- **THEN** 按 worktree 优先、local 回退与首值规则选择；仅键缺失回退，缺少有效项目 origin 或读取失败时拒绝

### Requirement: Canonical target is fixed before any transport

确定 owner/repo 后，每次部署 MUST 生成内部私有 remote 名，在临时 checkout 先执行仓库初始化，再把私有 remote 和唯一规范化 `https://github.com/{owner}/{repo}.git` 写入该临时仓库 local 配置。这两步 MUST NOT 发起传输或修改配置项目的 remote。后续分支查询、获取和推送 MUST 始终使用该仓库和该私有 remote，不得使用先前注入的 `-c remote...` 或 `clone --origin` 重新建立配置上下文。由 URL 重写才能成为 GitHub 地址的别名 MUST NOT 被用作 origin。

#### Scenario: Private remote isolation (DEP-13, DEP-16)
- **WHEN** 全局或 system 配置存在同名 remote/pushurl，且目标分支已存在或不存在、`keepHistory` 开或关、force 开或关，或连续两次部署
- **THEN** 分支查询、fetch/checkout 和私有 remote push 均指向 B；初始化后保持原分支/历史语义，浅获取深度为 1，keep-history 保留历史，临时 remote 不写回源仓库

### Requirement: URL-rewrite preflight runs in the final effective config context

在每次 `ls-remote`、fetch、push 之前，MUST 以相同实际工作目录、清理后的环境和受控命令行配置集合执行 `git config --includes --null --get-regexp '^url\..*\.(insteadof|pushinsteadof)$'`。检查 MUST 在本次传输相关配置（仓库初始化、私有 remote 写入）之后，且不得在检查后再增加会激活 include 的 remote 或配置覆盖。读取 MUST 按 Git 的 NUL 记录及 key/value 边界规则保留原值；仅退出码 1 代表无规则，其他查询或解码错误 MUST 阻止传输。任一规则值为 canonical URL 的前缀时 MUST 按 Git 大小写敏感语义前置拒绝，包括空前缀；无关 URL 规则 MUST 允许。同一次前检 MUST 核对私有 remote 的全部有效 fetch/push URL 均等于 canonical URL，有额外地址或差异时 MUST 拒绝。

#### Scenario: Conditional include activated after identity lookup (DEP-17)
- **WHEN** `includeIf hasconfig:remote.*.url:<canonical-url>` 在私有 remote 写入后引入匹配重写
- **THEN** 前检在首次分支查询/获取前看到规则并拒绝，错误仓库的 transport 未启动

#### Scenario: Gitdir conditional include and unrelated rules (DEP-18)
- **WHEN** `includeIf gitdir:<checkout>/.git` 在仓库初始化后引入匹配重写，或条件 include 只引入无关规则
- **THEN** 所有相关传输前拒绝；无关条件不误拒绝，不以事后 URL 正确代替阻断证据

#### Scenario: Matching and non-matching rewrites (DEP-15)
- **WHEN** global/system/local/include 中存在匹配或不匹配的 `insteadOf`/`pushInsteadOf`，包括协议重写和全局别名
- **THEN** 匹配规则在任何相关 Git 传输前拒绝，原始别名不用于定址；无关规则和正常认证配置继续工作

### Requirement: Git child processes are isolated from inherited repository overrides

`CreateGitProcess` MUST 从子进程环境移除继承的仓库定位、索引和对象覆盖变量：`GIT_DIR`、`GIT_WORK_TREE`、`GIT_COMMON_DIR`、`GIT_INDEX_FILE`、`GIT_OBJECT_DIRECTORY`、`GIT_ALTERNATE_OBJECT_DIRECTORIES`、`GIT_IMPLICIT_WORK_TREE`、`GIT_GRAFT_FILE`、`GIT_NO_REPLACE_OBJECTS`、`GIT_REPLACE_REF_BASE`、`GIT_PREFIX`、`GIT_INTERNAL_SUPER_PREFIX`、`GIT_SHALLOW_FILE`，以及 `GIT_CONFIG`、`GIT_CONFIG_PARAMETERS`、`GIT_CONFIG_COUNT` 和 `GIT_CONFIG_KEY_*`/`GIT_CONFIG_VALUE_*`。清理 MUST 覆盖身份读取和全部临时 Git 操作；正常 global/system 凭据、代理和 SSH 配置及其配置文件选择 MUST 保留，`GIT_CONFIG_GLOBAL`/`GIT_CONFIG_SYSTEM` MUST NOT 提供项目身份。认证入口 MUST 继续显式设置 askpass 且不修改父进程环境。实现 MUST 集中维护清理清单，不为每条命令增加发现子进程。

#### Scenario: Inherited overrides point at another repository (DEP-09, DEP-11)
- **WHEN** 子进程继承指向 A 的 `GIT_DIR`，或暂存操作继承 `GIT_WORK_TREE`、`GIT_COMMON_DIR`、`GIT_INDEX_FILE`、对象目录或运行时 config 覆盖
- **THEN** 真实 Git 仍返回 B 的 origin；本地真实 Git 只操作暂存仓库，A 的 refs、工作树和索引哨兵不变

#### Scenario: Normal authentication and parent environment (DEP-12)
- **WHEN** 存在正常认证配置，或部署完成后检查父进程环境
- **THEN** 必要认证设置保留，父进程环境保持原值

### Requirement: Dry-run and public compatibility are preserved

`DeployContext`、`IDeployProvider` 和公共 `DeployAsync` 签名 MUST 保持。dry-run MUST 继续免部署令牌、可在非 Git 临时项目中运行，且不触发构建或实际部署。仓库身份解析 MUST 位于实际部署路径。调用前后进程全局工作目录 MUST 保持原值。

#### Scenario: Dry-run and public entry (DEP-06, DEP-07, DEP-08)
- **WHEN** 使用 dry-run、skip-build 组合、公共旧入口，或比较调用前后 CWD
- **THEN** 既有免令牌和输出校验行为保持，旧入口兼容测试通过，全局工作目录不变
