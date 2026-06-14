# Bukit AI Demo-to-CMS Checklist

## 1. Requirements Stage

- [ ] Site name is defined
- [ ] Site purpose is defined
- [ ] Target users are defined
- [ ] Core sections are defined
- [ ] Page list is defined
- [ ] Visual style is defined
- [ ] Language is defined
- [ ] Content collections are defined
- [ ] Notion CMS requirement is confirmed
- [ ] Multi-database Notion requirement is confirmed
- [ ] Local preview requirement is confirmed

## 2. Demo Stage

- [ ] `demo/index.html` exists
- [ ] Every page is an independent HTML file
- [ ] `demo/assets/css/` exists
- [ ] `demo/assets/js/` exists
- [ ] `demo/assets/images/` exists
- [ ] `demo.routes.yaml` exists
- [ ] Each page contains `header`
- [ ] Each page contains `nav`
- [ ] Each page contains `main`
- [ ] Each page contains `footer`
- [ ] Each page has a `<title>`
- [ ] Each page has SEO description
- [ ] Pages use semantic `section`
- [ ] Page type is clear

## 3. Migratability

- [ ] Article cards use `article-card`
- [ ] Company cards use `company-card`
- [ ] Service cards use `service-card`
- [ ] FAQ items use `faq-item`
- [ ] Important content uses `data-field`
- [ ] List pages and detail pages are separate
- [ ] Image paths use local assets
- [ ] CSS paths use local assets
- [ ] JS paths use local assets
- [ ] No complex runtime JavaScript dependency
- [ ] No large unstructured business copy blocks

## 4. Route Map

- [ ] Every HTML file appears in `demo.routes.yaml`
- [ ] Every `source` exists
- [ ] Every `route` starts with `/`
- [ ] Detail routes use `{slug}`
- [ ] List and detail pages have separate routes
- [ ] `type` is valid
- [ ] `template` is stable
- [ ] Home route is `/`

## 5. User Confirmation

- [ ] Visual style confirmed
- [ ] Home layout confirmed
- [ ] Navigation confirmed
- [ ] List pages confirmed
- [ ] Detail pages confirmed
- [ ] Mobile experience confirmed
- [ ] CTA confirmed
- [ ] Copy direction confirmed
- [ ] Image style confirmed
- [ ] URL structure confirmed
- [ ] Content collections confirmed

## 6. Bukit Engineering

- [ ] `themes/<theme>/layouts/layouts/base.html` generated
- [ ] Page templates generated
- [ ] Partials generated
- [ ] Components generated
- [ ] `bukit.templates.yaml` generated
- [ ] Theme assets copied
- [ ] Header/nav/footer split into partials
- [ ] Repeated cards split into components
- [ ] List pages use collection loops
- [ ] Detail pages use `page.*` fields
- [ ] Template fields match seed fields

## 7. Content Data

- [ ] `pages.json` generated
- [ ] `posts.json` generated
- [ ] `companies.json` generated
- [ ] `services.json` generated
- [ ] `sections.json` generated
- [ ] `faqs.json` generated
- [ ] `media.json` generated
- [ ] `components.json` generated
- [ ] `notion-database-map.yaml` generated

## 8. Configuration Contract

- [ ] Standard `site.yaml` Profile selected
- [ ] No invented `site.yaml` fields
- [ ] `content.sources[]` is present and `legacy content provider field` is absent
- [ ] `--build-source notion` only used with `--content-source notion`
- [ ] Notion multi-database mode uses `content.sources`
- [ ] `demo.routes.yaml` follows the route spec
- [ ] `notion-database-map.yaml` follows the map spec
- [ ] `bukit.templates.yaml` paths exist
- [ ] Environment variable names follow the spec
- [ ] Schema validation run if supported
- [ ] `bukit doctor` run
- [ ] `bukit build` run
- [ ] Validation failures fixed

## 9. Notion CMS

- [ ] `notion-database-map.yaml` exists
- [ ] databaseId values are filled or auto-create is enabled
- [ ] Notion token environment variable is configured
- [ ] Schema validation passed
- [ ] Pages pushed successfully
- [ ] Posts pushed successfully
- [ ] Companies pushed successfully
- [ ] Services pushed successfully
- [ ] Push report has no failed items
- [ ] Upsert behavior is correct
- [ ] Replace content behavior is correct
- [ ] Notion content is editable

## 10. Release Gate

- [ ] `dotnet test` passed
- [ ] `bash scripts/test-all.sh` passed
- [ ] `bash scripts/quality-gate.sh` passed
- [ ] No sensitive files leaked
- [ ] No dangerous protocols
- [ ] No invalid internal links
- [ ] SEO title and description confirmed
- [ ] Visual match confirmed
- [ ] Deployment target confirmed
