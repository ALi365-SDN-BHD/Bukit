---
title: About Theme Inheritance
slug: about
---

## How Theme Inheritance Works

1. Child theme declares `extends: parent` in `theme.yaml`
2. Bukit loads the parent theme first, then the child theme
3. Child theme templates override parent templates with the same path
4. Parent templates serve as fallbacks for templates not defined in the child

This allows theme developers to:
- Create a base theme with shared structure
- Build child themes that customize specific parts
- Avoid duplicating common templates across themes
