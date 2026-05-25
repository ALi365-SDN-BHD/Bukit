---
title: Theme Inheritance Example
slug: home
---

This example demonstrates Bukit's theme inheritance system.

- **Parent theme** - Provides `base.html` layout with parent navigation
- **Child theme** - Extends parent, overrides `base.html` with child navigation and footer

The child theme's `page.html` template inherits from the parent theme since the child doesn't define its own.
