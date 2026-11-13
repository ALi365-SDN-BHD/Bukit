## ADDED Requirements

### Requirement: 迁移跳转在 Worktree 与进程内构建结果一致

当 `site.defaultLanguageLegacyRedirects` 生效时，默认语言 Worker SHALL 在自己的变体管线中派生迁移跳转，父进程 SHALL 把它们作为默认语言 Worker 结果中的派生文档验证；迁移跳转只写在 `staging/<defaultLang>/` 下，MUST NOT 被当作越界输出拒绝。Worktree 构建与同一输入的进程内构建 MUST 产生相同的迁移跳转文件集合。

#### Scenario: 迁移跳转等价
- **WHEN** 同一干净提交分别用 Worktree 构建和进程内构建生成，二者都设置 `defaultLanguageInSubdir: false` 和 `defaultLanguageLegacyRedirects: true`
- **THEN** 两份输出包含相同的 `<defaultLang>/` 下跳转文件集合，且父进程接受默认语言 Worker 结果
