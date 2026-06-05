---
title: Templating
collection: page
slug: docs/templating
---

## Templating with Scriban

Bukit uses the Scriban templating engine.

### Layout Inheritance

```html
{% layout "layouts/base.html" %}
<h1>{{ page.title }}</h1>
{{ page.content }}
```

### Partial Includes

```html
{{ include "partials/header.html" }}
```

See [Deployment](/docs/deployment/) for how to publish your site.
