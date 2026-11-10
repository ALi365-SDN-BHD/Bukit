# Bukit Core Agent Skills

这组技能教 AI 助手用稳定版 Bukit Core 搭建和维护网站。内容只描述当前 Core 行为，事实来源是
`src/Bukit-Core` 源码：命令以 `BukitCliSpecs.cs` 为准，`site.yaml` 以 `AppConfig.cs` 和配置校验器为准。

先加载 [`using-bukit`](using-bukit/SKILL.md)，再按任务加载对应的细分技能，完整列表见
[README.md](README.md)。

默认技能不得把 Labs、clone、import、intent、webhook、远程主题、插件市场或外部插件写成 Core 能力。需要这些能力时，必须显式进入
[`guide/labs-skills`](../labs-skills/README.md)。

维护规则见 [`MAINTENANCE.md`](MAINTENANCE.md)。
