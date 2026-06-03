# 丝路商讯导入测试与 Bukit 主题迁移计划

## 概述

将 `/Users/ali/Documents/trae_projects/silkroad_biz/demo` 的静态 HTML 商务目录站点转换为 Bukit 主题，测试 `bukit import html-demo` 模块的完整流程。

## 计划步骤

### 阶段 1：验证运行环境

- [ ] **1.1 确认 Bukit CLI 可用**
  - 运行 `bukit version` 或 `dotnet run --project src/Bukit.Cli -- version` 确认 CLI 可执行
  - 记录版本信息

- [ ] **1.2 确认 silkroad_biz demo 目录完整**
  - 验证所有 28 个文件存在（index.html, insights.html, companies.html 等）
  - 确认 `assets/css/style.css` 和 `assets/js/main.js` 存在

### 阶段 2：执行 `bukit import html-demo --dry-run`（首次试运行）

- [ ] **2.1 执行 dry-run 分析**
  - 命令：`bukit import html-demo /Users/ali/Documents/trae_projects/silkroad_biz/demo --theme silkroad-biz --dry-run`
  - 或从项目目录运行：`dotnet run --project src/Bukit.Cli -- import html-demo /Users/ali/Documents/trae_projects/silkroad_biz/demo --theme silkroad-biz --dry-run --config testsite/site.yaml`
  - 捕获所有控制台输出

- [ ] **2.2 分析 dry-run 结果**
  - 检查：页面发现数量、页面分类结果（Home/PostList/PostDetail/CompanyList 等）
  - 检查：布局提取结果（Header/Footer/Nav）
  - 检查：组件发现结果
  - 检查：诊断警告或错误
  - 记录所有输出供报告

### 阶段 3：执行实际导入

- [ ] **3.1 创建临时 Bukit 站点用于测试导入**
  - 目录：在 Bukit 项目根下创建 `import-test/` 目录
  - 初始化：`bukit init import-test --provider markdown --template minimal`
  - 或直接运行导入到 Bukit 项目根

- [ ] **3.2 执行导入命令**
  - 命令：`bukit import html-demo /Users/ali/Documents/trae_projects/silkroad_biz/demo --theme silkroad-biz --force --verify --language zh`
  - 参数说明：
    - `--force`：覆盖已存在的主题目录
    - `--verify`：导入后自动运行 doctor + build 验证
    - `--language zh`：指定内容语言为中文
    - `--content-source notion`（默认）：生成 Notion seed 文件
    - `--build-source markdown`（默认）：使用 Markdown 构建站点

- [ ] **3.3 执行种子数据导入**
  - 命令：`bukit import seed sites/silkroad-biz/data --output sites/silkroad-biz/content --force`
  - 将生成的 JSON/YAML seed 转换为 Markdown 内容

### 阶段 4：跟踪执行过程与错误检测

- [ ] **4.1 完整捕获导入输出**
  - 捕获 stdout 和 stderr 全部输出
  - 记录每个阶段的时间戳

- [ ] **4.2 检查导入报告**
  - 读取 `sites/silkroad-biz/import-report.md`
  - 分析：页面扫描结果、模板生成统计、组件发现、诊断问题、残差内容分析

- [ ] **4.3 检查生成的主题结构**
  - 列出 `themes/silkroad-biz/` 目录结构
  - 验证：base.html、page.html、post.html、index.html、list.html 是否存在
  - 验证 partials（header、footer）生成情况
  - 验证 CSS/JS 资产是否迁移

- [ ] **4.4 记录所有错误、警告和异常**
  - 分类：配置错误、模板错误、渲染错误、运行时异常
  - 每个错误记录：错误代码（BKT-XXXX）、描述、触发条件

### 阶段 5：修复发现的问题

- [ ] **5.1 修复导入过程中的错误**
  - 根据错误类型逐一修复
  - 可能的问题区域：
    - 主题名称合法性（中划线 vs 下划线）
    - 页面分类不准导致模板映射错误
    - 中文内容的编码处理
    - 静态资源路径引用

- [ ] **5.2 修复生成的主题模板**
  - 检查并修复 Scriban 模板中的语法错误
  - 修正 CSS/JS 资产引用路径（`{{ site.base_url }}/assets/...`）
  - 修复 header 导航链接
  - 修复 footer 署名

- [ ] **5.3 修复 site.yaml 配置**
  - 检查 collections 配置是否匹配实际内容
  - 检查 SEO 配置（site.url、site.seo）
  - 补充 theme.params（brand、primary_color 等）

### 阶段 6：验证修复成果

- [ ] **6.1 运行 `bukit doctor`**
  - `bukit doctor --config sites/silkroad-biz/site.yaml`
  - 确认无配置错误、无缺失模板、无变量拼写错误

- [ ] **6.2 运行 `bukit build`**
  - `bukit build --config sites/silkroad-biz/site.yaml`
  - 确认构建成功（exit code 0）
  - 检查输出目录 `sites/silkroad-biz/dist/`

- [ ] **6.3 检查构建输出**
  - 列出生成的所有 HTML 页面
  - 验证关键页面：首页、洞察列表、公司目录
  - 确认页面间导航链接正确

### 阶段 7：存储迁移成果

- [ ] **7.1 生成主题目录到 silkroad_biz**
  - 将 `themes/silkroad-biz/` 复制到 `/Users/ali/Documents/trae_projects/silkroad_biz/themes/silkroad-biz/`

- [ ] **7.2 生成站点配置文件**
  - 将 `sites/silkroad-biz/site.yaml` 复制到 `/Users/ali/Documents/trae_projects/silkroad_biz/site.yaml`

- [ ] **7.3 生成内容文件**
  - 将 Markdown 内容（如有）复制到 `/Users/ali/Documents/trae_projects/silkroad_biz/content/`

- [ ] **7.4 生成数据种子文件**
  - 将 seed/data 目录复制到 `/Users/ali/Documents/trae_projects/silkroad_biz/data/`

- [ ] **7.5 生成导入报告**
  - 将 `import-report.md` 复制到 `/Users/ali/Documents/trae_projects/silkroad_biz/import-report.md`

- [ ] **7.6 整理文件结构**
  - 确认目标目录结构完整且可构建

### 阶段 8：最终验证与总结

- [ ] **8.1 在目标目录运行 `bukit doctor` 验证**
  - 在 `/Users/ali/Documents/trae_projects/silkroad_biz/` 运行验证

- [ ] **8.2 编写测试总结报告**
  - 导入测试结果概述
  - 问题与修复记录
  - 主题结构文档
  - 迁移成果清单

## 目录结构（预期输出）

```
/Users/ali/Documents/trae_projects/silkroad_biz/
├── site.yaml                          # Bukit 站点配置（迁移生成）
├── themes/
│   └── silkroad-biz/                  # Bukit 主题（迁移生成）
│       ├── layouts/
│       │   ├── layouts/base.html
│       │   ├── pages/
│       │   │   ├── index.html
│       │   │   ├── page.html
│       │   │   ├── list.html
│       │   │   └── ...
│       │   ├── partials/
│       │   │   ├── header.html
│       │   │   └── footer.html
│       │   └── bukit.templates.yaml
│       ├── assets/
│       │   ├── css/style.css
│       │   └── js/main.js
│       └── static/
├── content/                           # Markdown 内容（迁移生成）
├── data/                              # 种子数据文件（迁移生成）
├── import-report.md                   # 导入报告
└── demo/                              # 原始 HTML demo（已有）
```

## 关键风险

1. **导入模块可能处于 beta 阶段**：`bukit-import` 技能标为 beta，可能存在未完全实现的特性
2. **中文内容处理**：中文 HTML 内容转换为 Scriban 模板时可能出现编码或模板语法问题
3. **页面分类精度**：PageClassifier 对中文章节页面的分类可能不准确
4. **静态资源路径**：CSS 中引用的字体、图片路径需要验证
5. **导航链接转换**：页面间的硬编码链接需要转换为 Bukit 路由

## 技能加载链

根据 `skills-index.yaml` 中的 `import_html_demo` 工作流：
1. `using-bukit` — 已加载
2. `bukit-cli-reference` — 已读取
3. `bukit-import` — 已读取（新增 `import html-demo` 详细命令模型）
4. `bukit-theme` — 已读取
5. `bukit-templating` — 需要时加载
