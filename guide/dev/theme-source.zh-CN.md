# Git 主题源

Bukit 支持从 Git 仓库拉取主题，实现主题的分发和版本管理。无需中心化注册中心——直接在 `site.yaml` 中声明 Git URL 即可。

实现参考：
- `src/Bukit.Engine/ThemeSourceManager.cs`
- `src/Bukit.Config/AppConfig.cs` (ThemeConfig.Source)
- `src/Bukit.Engine/SiteEngine.cs` (BuildVariantAsync)

## site.yaml 配置

```yaml
theme:
  source: "https://github.com/user/bukit-theme.git@v1.2.0"
  name: my-custom    # 可选：仓库内子目录名称
```

| 字段 | 必需 | 说明 |
|------|------|------|
| `theme.source` | 是 | Git 仓库 URL + 可选版本标签（`@v1.0.0`） |
| `theme.name` | 否 | 仓库内的主题子目录名。若不指定，使用仓库根目录 |

## 版本锁定

版本通过 URL 中的 `@` 后缀指定：

```
https://github.com/user/theme.git@v1.0.0   # Git tag
https://github.com/user/theme.git@abc1234   # commit hash
https://github.com/user/theme.git           # 默认 main/master 分支
```

未指定版本时，每次构建执行 `git pull` 获取最新。

## 缓存机制

- 首次使用：`git clone` 到 `.cache/themes/{repo-name}/`
- 后续构建：若指定版本标签，`git checkout` 切换；否则 `git pull` 更新

## 与本地主题的优先级

若同时指定 `theme.source` 和本地 `themes/` 目录：

- `theme.source` 优先——先尝试从 Git 拉取
- 若 Git 拉取失败，回退到本地 `themes/` 目录
- `theme.name` 仅用于定位仓库内的子目录，不影响本地优先级

## 环境要求

- 构建环境需要安装 `git` 命令行工具
- 仓库需要公开可访问（或配置 SSH key）
- 克隆超时：120 秒
