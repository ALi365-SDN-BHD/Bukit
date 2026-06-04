# Bukit AI Demo-to-CMS Checklist

## 使用说明

每个阶段完成后，都应执行对应检查清单。未通过的项目必须修复后再进入下一阶段。

---

# 1. 需求阶段

- [ ] 已明确网站名称
- [ ] 已明确网站定位
- [ ] 已明确目标用户
- [ ] 已明确核心栏目
- [ ] 已明确页面列表
- [ ] 已明确视觉风格
- [ ] 已明确语言
- [ ] 已明确内容集合
- [ ] 已确认是否使用 Notion CMS
- [ ] 已确认是否需要多数据库 Notion
- [ ] 已确认是否需要本地预览模式

---

# 2. Demo 生成阶段

## 文件结构

- [ ] 存在 `demo/index.html`
- [ ] 所有页面均为独立 HTML 文件
- [ ] 存在 `demo/assets/css/`
- [ ] 存在 `demo/assets/js/`
- [ ] 存在 `demo/assets/images/`
- [ ] 存在 `demo.routes.yaml`

## HTML 结构

- [ ] 每个页面包含 `header`
- [ ] 每个页面包含 `nav`
- [ ] 每个页面包含 `main`
- [ ] 每个页面包含 `footer`
- [ ] 页面包含 `<title>`
- [ ] 页面包含 SEO description
- [ ] 页面使用语义化 `section`
- [ ] 页面类型清晰

## 可迁移性

- [ ] 文章卡片使用 `article-card`
- [ ] 企业卡片使用 `company-card`
- [ ] 服务卡片使用 `service-card`
- [ ] FAQ 使用 `faq-item`
- [ ] 重要内容使用 `data-field`
- [ ] 列表页与详情页分离
- [ ] 图片路径使用本地 assets
- [ ] CSS 路径使用本地 assets
- [ ] JS 路径使用本地 assets
- [ ] 不依赖复杂运行时 JavaScript
- [ ] 不存在无法识别的大段业务文案结构

---

# 3. route-map 阶段

- [ ] 每个 HTML 文件都出现在 `demo.routes.yaml`
- [ ] 每个 source 都对应真实文件
- [ ] 每个 route 都以 `/` 开头
- [ ] 动态详情页使用 `{slug}`
- [ ] 列表页和详情页使用不同 route
- [ ] type 设置正确
- [ ] template 名稳定
- [ ] 首页 route 为 `/`
- [ ] 资讯详情 route 正确
- [ ] 企业详情 route 正确
- [ ] 自定义列表页 route 正确

---

# 4. 用户确认阶段

- [ ] 用户已确认整体视觉风格
- [ ] 用户已确认首页布局
- [ ] 用户已确认导航结构
- [ ] 用户已确认列表页
- [ ] 用户已确认详情页
- [ ] 用户已确认移动端体验
- [ ] 用户已确认 CTA
- [ ] 用户已确认文案方向
- [ ] 用户已确认图片风格
- [ ] 用户已确认 URL 结构
- [ ] 用户已确认内容集合

---

# 5. Bukit 工程化阶段

## 主题结构

- [ ] 已生成 `themes/<theme>/layouts/layouts/base.html`
- [ ] 已生成 pages templates
- [ ] 已生成 partials
- [ ] 已生成 components
- [ ] 已生成 `bukit.templates.yaml`
- [ ] 已复制主题 assets

## 模板拆分

- [ ] header 已拆分为 partial
- [ ] nav 已拆分为 partial
- [ ] footer 已拆分为 partial
- [ ] 重复卡片已拆分为 component
- [ ] 页面主体已拆分为 page template
- [ ] 列表页使用 collection 循环
- [ ] 详情页使用 `page.*` 字段
- [ ] 模板字段与数据字段一致

## 内容数据

- [ ] 已生成 `pages.json`
- [ ] 已生成 `posts.json`
- [ ] 已生成 `companies.json`
- [ ] 已生成 `services.json`
- [ ] 已生成 `sections.json`
- [ ] 已生成 `faqs.json`
- [ ] 已生成 `media.json`
- [ ] 已生成 `components.json`
- [ ] 已生成 `notion-database-map.yaml`

---

# 6. 本地预览阶段

- [ ] 使用 `--build-source markdown`
- [ ] 已生成 `content/`
- [ ] `site.yaml` 使用 markdown provider
- [ ] 已执行 `bukit doctor`
- [ ] 已执行 `bukit build`
- [ ] `dist/` 已生成
- [ ] 首页可访问
- [ ] 列表页可访问
- [ ] 详情页可访问
- [ ] 图片无缺失
- [ ] 内部链接正确
- [ ] 移动端布局正常

---

# 7. import-report 审查阶段

- [ ] 已检查 Pages
- [ ] 已检查 Content Seeds
- [ ] 已检查 Seed Push Scope
- [ ] 已检查 Build/Data Source Relationship
- [ ] 已检查 Hardcoded Content Residue
- [ ] 已检查 Diagnostics
- [ ] 已检查 Link Validation
- [ ] 已检查 Visual Verification
- [ ] 已检查 Manual Review Required
- [ ] 不存在高风险 warning
- [ ] 业务文案残留在可接受范围内

---

# 8. Notion CMS 阶段

- [ ] 已生成 `notion-database-map.yaml`
- [ ] 已填写 databaseId 或启用自动创建
- [ ] 已设置 Notion token
- [ ] schema validate 已通过
- [ ] pages 推送成功
- [ ] posts 推送成功
- [ ] companies 推送成功
- [ ] services 推送成功
- [ ] push report 无 failed
- [ ] upsert 行为正确
- [ ] replace 正文行为正确
- [ ] Notion 页面内容可编辑

---

# 9. Notion-only 构建阶段

- [ ] 使用 `--build-source notion`
- [ ] 不生成 `content/`
- [ ] `site.yaml` 使用 Notion provider 或 `content.sources`
- [ ] Notion database 环境变量已设置
- [ ] 已执行 `bukit doctor`
- [ ] 已执行 `bukit build`
- [ ] 多源 Notion 内容已加载
- [ ] 页面路由正确
- [ ] 内容集合正确
- [ ] `dist/` 已生成

---

# 10. 发布前质量门禁

- [ ] 已执行 `dotnet test`
- [ ] 已执行 `bash scripts/test-all.sh`
- [ ] 已执行 `bash scripts/quality-gate.sh`
- [ ] 已确认所有测试通过
- [ ] 已确认无敏感文件泄露
- [ ] 已确认无危险协议
- [ ] 已确认无无效内部链接
- [ ] 已确认 SEO title / description
- [ ] 已确认视觉还原
- [ ] 已确认发布目标环境

---

# 11. 发布完成

- [ ] 站点已部署
- [ ] 首页可访问
- [ ] 核心页面可访问
- [ ] Notion 内容更新可触发重新构建
- [ ] 发布日志已记录
- [ ] 回滚方案已准备
