# init/create (Scaffold Initialization)

`bukit init <dir>` (synonym `create <dir>`) creates a minimal runnable site project.

Implementation: `src/Bukit.Cli/Commands/InitCommand.cs`

## Basic Usage

```bash
bukit init my-site
bukit create my-site
```

Parameters: `--provider <markdown|notion>` (default markdown), `--template <name>` (default minimal)

## Generated Structure

```text
<dir>/
  site.yaml
  .gitignore
  content/hello-world.md
  themes/starter/
    assets/style.css
    static/
    layouts/
      layouts/base.html
      pages/index.html, list.html, page.html, post.html
      partials/header.html, footer.html
```

## Verification

```bash
cd my-site
bukit doctor
bukit build --clean
bukit preview --dir dist
```

## Known Limitations

- `--template` currently only affects config, not file template generation
- `.gitignore` ignores `.bukit/` but engine default cache dir is `.cache/`
