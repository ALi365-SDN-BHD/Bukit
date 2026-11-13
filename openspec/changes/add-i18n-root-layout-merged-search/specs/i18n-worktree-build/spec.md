## ADDED Requirements

### Requirement: 根目录布局合并搜索在 Worktree 与进程内构建结果一致

当 `site.defaultLanguageInSubdir` 为 `false` 且 `site.search.mode` 为 `merged` 时，默认语言 Worker SHALL 把自己的索引写到 `staging/<defaultLang>/search.json`，父进程 SHALL 接受该路径并在根目录写合并索引 `search.json`。Worktree 构建与同一输入的进程内构建 MUST 产生相同的搜索索引文件集合与内容。

#### Scenario: 合并搜索等价
- **WHEN** 同一干净提交分别用 Worktree 构建和进程内构建生成，二者都设置 `defaultLanguageInSubdir: false` 和 `search.mode: merged`
- **THEN** 两份输出的 `search.json`、`<defaultLang>/search.json` 与其他语言的 `<lang>/search.json` 相同，且父进程接受默认语言 Worker 结果
