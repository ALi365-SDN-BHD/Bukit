# External Plugin 生态强化 Checklist

## 用户指南
- [x] `guide/user/20-external-plugins.md` 英文版存在
- [x] `guide/user/20-external-plugins.zh-CN.md` 中文版存在
- [x] 覆盖内容：配置字段、协议流程、安全模型、Node.js 示例、故障排查
- [x] 示例代码可复制粘贴执行

## Node.js 示例插件
- [x] `examples/plugin-site/plugins/hello-derive.js` 存在
- [x] 正确的 stdin/stdout JSON 协议
- [x] 返回 `/hello/` 派生页面
- [x] `examples/plugin-site/site.external-plugin.yaml` 存在

## 集成测试
- [x] Derive-pages 协议集成测试存在 (34 ExternalProtocolPlugin tests)
- [x] 测试验证 derive 页面输出正确
- [x] 测试通过

## 回归验证
- [x] `dotnet build bukit.slnx -c Release` 0 警告 0 错误
- [x] `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 全部测试通过 (1029 Engine + 524 Content + 730 Cli)
- [x] 外部插件示例站点构建成功
