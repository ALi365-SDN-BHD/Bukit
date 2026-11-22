## ADDED Requirements

### Requirement: Privacy checks detect CloudBase identifiers alongside Notion
The build-side `PublicOutputPrivacyCheck` SHALL add CloudBase detection next to the existing
Notion detection, without changing it: when a site has a `cloudbase` content source, the check
SHALL treat the configured envId, any `cloud://` compat reference, the CloudBase gateway host
(`*.api.tcloudbasegateway.com` or `*.api.intl.tcloudbasegateway.com`), and any URL whose path starts
with the PG storage signed-download prefix `/v1/storages/object/sign/` and carries a `token`
parameter, and the storage CDN host (`*.storage.tcloudbase.com`) in public output as a leak, and SHALL map it to the existing
`publicOutputPrivacy=failed` result. The deployment-side `DeploymentPrivacyValidator` reads no configuration and
applies to every site, so it SHALL detect by fixed pattern only (a) `cloud://` in URL positions
(quoted or unquoted URL attribute values, `<meta>` `content` and `style` `url(...)` arguments of any
tag in HTML-like files, `url(...)` arguments in CSS files after removing quotes, and whole JSON
string values, each starting with `cloud://` after trimming; text nodes, code blocks and other text
files are not checked for `cloud://` at deploy time) and (b) signed links anywhere in the text of any
scanned text file: a gateway-host URL on the signed path with a `token` parameter, or a storage CDN host
URL with a `token` parameter; a gateway or CDN host URL without `token` SHALL NOT be treated as a
leak, so sites without a CloudBase source keep their privacy results. It SHALL work without reading configuration, and SHALL detect the envId only when the deploy command reads it
from `site.yaml` and passes it in. The report schemas, the `.bukit/publish-audit-report.json`
contract, the existing error codes and the Notion applicability rules MUST NOT change. Diagnostics
MUST stay redacted and MUST NOT print the envId, the `cloud://` reference or the signed link.

#### Scenario: envId in public JSON feed
- **WHEN** a strict build of a site with a `cloudbase` source writes the configured envId into a public JSON feed
- **THEN** the privacy check fails, the transaction is not committed, and the diagnostic does not print the envId

#### Scenario: Signed link in deploy staging without configuration
- **WHEN** a staged deployment file contains a CloudBase gateway signed link and no configuration is passed
- **THEN** deployment returns `BKT-DEPLOY-PRIVACY-0001` before any Git add, commit or push

#### Scenario: CDN signed link in deploy staging
- **WHEN** a staged deployment file contains `https://env-abc.storage.tcloudbase.com/media/a.png?token=…` and no configuration is passed
- **THEN** deployment returns `BKT-DEPLOY-PRIVACY-0001` before any Git add, commit or push

#### Scenario: cloud:// image in deploy staging
- **WHEN** a staged HTML file contains `<img src="cloud://env-abc.media/a.png">` and no configuration is passed
- **THEN** deployment returns `BKT-DEPLOY-PRIVACY-0001` before any Git add, commit or push

#### Scenario: Tutorial with cloud:// in a code block
- **WHEN** a site without a `cloudbase` source publishes a tutorial page that shows `cloud://env.bucket/a.png` inside a `<code>` block
- **THEN** deployment privacy validation passes and the privacy results are identical to those produced before this change

#### Scenario: Unsigned CDN link on a site without CloudBase
- **WHEN** a site without a `cloudbase` source links to a public object at `https://x.storage.tcloudbase.com/b/a.pdf` without a `token` parameter
- **THEN** deployment privacy validation passes and the privacy results are identical to those produced before this change

#### Scenario: Report contract unchanged
- **WHEN** a site without a `cloudbase` source, whose output contains no CloudBase signed link and no `cloud://` in a URL position, is built and deployed
- **THEN** the security report, publish audit report and privacy results are identical to those produced before this change
