# 18 网站克隆：将任意在线站点转换为 Bukit 主题

Clone 抓取网站的视觉设计——颜色、排版、间距、布局——并生成外观一致的 Bukit 主题。三阶段流程：提取 → 生成 → 验证。

相关文档：[docs/clone.md](../../docs/clone.md)

## 你将获得

- 一个视觉上匹配目标网站的 Bukit 主题目录
- 提取的设计令牌（颜色、字体、阴影、间距比例）
- 段落/组件布局分析
- 下载的资源（Logo、图标、主图）
- 使用新主题验证构建

## 何时使用

| 场景 | 工具 |
|------|------|
| 克隆现有在线站点的设计 | `bukit clone`（本页） |
| 从预设创建新主题 | `bukit theme wizard --preset blog` |
| 安装社区主题 | `bukit theme install --registry <name>` |
| 复制内置 starter 主题 | `bukit theme create <name>` |

## 工作原理

### 第一阶段：提取（浏览器 MCP）

使用浏览器自动化工具（Chrome MCP / Playwright MCP）从目标网站提取设计令牌：

1. **截图** — 桌面端（1440px）、平板（768px）、移动端（390px）全页截图
2. **设计令牌**（`tokens.json`）— 颜色、字体、圆角、阴影、间距比例、响应式断点
3. **页面布局**（`page.json`）— 标题、描述、SEO 元数据
4. **段落分析**（`sections.json`）— 有序可视段落（类型、文字、图片、按钮、样式）
5. **资源**（`assets.json`）— Logo、主图、favicon

详细浏览器脚本见 [bukit-clone skill](../../src/skills/bukit-clone/SKILL.md)。

### 第二阶段：生成（CLI）

```bash
bukit clone \
  --tokens tokens.json \
  --page page.json \
  --sections sections.json \
  --assets assets.json \
  --theme my-theme
```

生成 `themes/<name>/` 目录，包含模板、CSS 和资源。

更新 `site.yaml`：

```yaml
theme:
  name: my-theme
```

### 第三阶段：验证

```bash
bukit doctor
bukit build
bukit clone --verify   # 自动像素对比
```

## 命令选项

| 选项 | 说明 |
|------|------|
| `--tokens <file>` | 设计令牌 JSON 路径（必填） |
| `--page <file>` | 页面元数据 JSON 路径 |
| `--sections <file>` | 段落 JSON 路径 |
| `--assets <file>` | 资源 JSON 路径 |
| `--theme <name>` | 主题名称（必填） |
| `--verify` | 克隆后自动验证 |
| `--fail-on-visual-diff` | 发现视觉差异时报错退出 |

## 生成内容

```
themes/<name>/
  assets/
    style.css             # 提取的设计令牌 CSS
    images/               # 下载的资源
  layouts/
    layouts/base.html     # 基础布局
    pages/                # 页面模板
    partials/             # 局部模板
  theme.yaml              # 主题元数据
```

## 限制

- **JavaScript 交互** — 仅克隆静态 HTML/CSS，动画和客户端 JS 不复制
- **动态内容** — 通过 API 获取的内容无法抓取
- **复杂布局** — 深度嵌套 CSS Grid 可能需要手动调整
- **授权字体** — 商业字体可能不可重新分发

## 下一步

- [12 CLI 参考](./12-cli-reference.md)
- [08 主题与模板](./08-themes-templates.md)
- [bukit-clone skill](../../src/skills/bukit-clone/SKILL.md)
