## MODIFIED Requirements

### Requirement: Public output privacy detects CloudBase identifiers
The build-time public output privacy check SHALL fail when public output contains a CloudBase
identifier: the gateway host `{envId}.api.tcloudbasegateway.com` or
`{envId}.api.intl.tcloudbasegateway.com`, the storage CDN host `{envId}.storage.tcloudbase.com`,
a `cloud://` compat reference, a URL on the PG storage signed-download path carrying a
`token` parameter, or any other `*.storage.tcloudbase.com` URL. It SHALL NOT fail for a bare
envId in ordinary text or in an output file path. The deployment privacy validator reads no
configuration and applies to every site, so it SHALL detect only (a) signed links anywhere in
the text of any scanned text file: a gateway-host URL on the signed path with a `token`
parameter, or a storage CDN host URL with a `token` parameter; and (b) `cloud://` in URL
positions. URL positions are quoted or unquoted URL attribute values of any tag in HTML-like
files (`src`, `href`, each `srcset` candidate, `poster`, `data-src`, `action`, `formaction`,
`cite`, `data`, `xlink:href`, `url`, and `content` on `<meta>`) and CSS `url(...)` arguments in
`style` attributes, CSS `url(...)` arguments in `.css` files, and whole string values in JSON
files, each trimmed (CSS arguments also unquoted) and starting with `cloud://`
case-insensitively. Text nodes, code blocks and other text files SHALL NOT be checked for
`cloud://` at deploy time; the build-time check keeps full-text detection for sites with a
`cloudbase` source. A gateway or CDN host URL without `token` SHALL NOT fail deployment, so sites
without a CloudBase source keep their behaviour. When the deploy command passes CloudBase
envIds, it SHALL pass every configured source envId and match them only inside the gateway host,
storage CDN host or `cloud://` reference; a bare envId substring SHALL NOT fail. A JSON file
that contains `cloud://` but cannot be parsed SHALL be treated as a leak at deploy time.

#### Scenario: envId leaks into a page
- **WHEN** a public HTML page contains `https://env-abc.api.tcloudbasegateway.com/...` or `cloud://env-abc.media/a.png`
- **THEN** the build privacy check fails

#### Scenario: bare envId in prose or a path
- **WHEN** a site whose envId is `blog` publishes ordinary prose or a path containing `blog`
- **THEN** the build privacy check passes

#### Scenario: Signed link in deploy staging
- **WHEN** a staged deployment file contains a CloudBase signed link
- **THEN** deployment fails before any Git write without reading site configuration

#### Scenario: CDN signed link in deploy staging
- **WHEN** a staged deployment file contains `https://env-abc.storage.tcloudbase.com/media/a.png?token=…`
- **THEN** deployment fails before any Git write without reading site configuration

#### Scenario: CDN host in a CloudBase build
- **WHEN** a site with a `cloudbase` source writes any `*.storage.tcloudbase.com` URL into public output
- **THEN** the build privacy check fails

#### Scenario: Unsigned CDN link on a site without CloudBase
- **WHEN** a site without a `cloudbase` source links to `https://x.storage.tcloudbase.com/b/a.pdf` without a `token` parameter
- **THEN** deployment privacy validation passes, as before this change

#### Scenario: cloud:// image in deploy staging
- **WHEN** a staged HTML file contains `<img src="cloud://env-abc.media/a.png">` or `<img src=cloud://env-abc.media/a.png>`
- **THEN** deployment fails with `BKT-DEPLOY-PRIVACY-0001` before any Git write

#### Scenario: Signed link in a code block
- **WHEN** a staged page shows `https://env-abc.storage.tcloudbase.com/media/a.png?token=…` inside a `<code>` block
- **THEN** deployment fails with `BKT-DEPLOY-PRIVACY-0001`, because signed links are detected anywhere in text

#### Scenario: Tutorial mentions cloud:// in a code block
- **WHEN** a site without a `cloudbase` source publishes a page whose `<code>` block shows `cloud://env.bucket/a.png`
- **THEN** deployment privacy validation passes

#### Scenario: Every CloudBase envId is checked at deploy
- **WHEN** a site has two CloudBase sources and the second envId appears in a staged file's gateway host
- **THEN** deployment fails

#### Scenario: Malformed JSON with cloud://
- **WHEN** a staged `.json` file contains `cloud://` but is not valid JSON
- **THEN** deployment fails with `BKT-DEPLOY-PRIVACY-0001`
