# init/create (Permulaan Perancah)

`bukit init <dir>` (sinonim `create <dir>`) mencipta projek tapak minimum boleh laku.

Pelaksanaan: `src/Bukit.Cli/Commands/InitCommand.cs`

## Penggunaan Asas
```bash
bukit init my-site
bukit create my-site
```

Parameter: `--provider <markdown|notion>` (lalai markdown), `--template <name>` (lalai minimal)

## Struktur Dijana
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

## Pengesahan
```bash
cd my-site && bukit doctor && bukit build --clean && bukit preview --dir dist
```

## Had Diketahui
- `--template` kini hanya mempengaruhi konfigurasi
- `.gitignore` mengabaikan `.bukit/` tetapi direktori cache lalai enjin ialah `.cache/`
