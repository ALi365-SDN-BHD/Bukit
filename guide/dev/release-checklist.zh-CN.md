# 发布检查清单

每次公开发布（preview/beta/stable）前运行此清单。

## 文档

- [ ] README 语言版本已同步（`.md`、`.zh-CN.md`、`.ms.md`）
- [ ] `guide/user` 交叉引用已验证
- [ ] `guide/dev` 交叉引用已验证
- [ ] `guide/ai` Prompt Pack 已验证
- [ ] 无 SiteGen（旧项目名）残留引用
- [ ] BukitJalil 边界清晰（未列为核心功能）
- [ ] Skills 文档（`src/skills/*`）已从 README 链接但未重复

## 构建

- [ ] `dotnet build bukit.slnx -c Release` 通过
- [ ] `dotnet test` 通过（所有项目）
- [ ] 示例站点（`examples/starter/`）Smoke 脚本通过
- [ ] AOT 构建零警告

## 安全

- [ ] 文档中无 token、密钥或 secret
- [ ] `NOTION_TOKEN` 是文档中唯一的 Notion 认证引用
- [ ] Webhook token 示例只使用占位符（如 `YOUR_WEBHOOK_SECRET`）
- [ ] 所有图片 URL 为相对路径或来自允许的域名

## 公开测试

- [ ] `public-preview-scope.zh-CN.md` 保持最新
- [ ] 实验性功能已明确标注
- [ ] Roadmap 未过度承诺未交付的能力
- [ ] 根 README 中的项目状态章节准确

## 版本

- [ ] 版本号已更新（如适用）
- [ ] Changelog 条目与实际变更匹配
- [ ] 破坏性变更已附带迁移指南
