## ADDED Requirements

### Requirement: Privacy checks detect CloudBase identifiers alongside Notion
The build-side `PublicOutputPrivacyCheck` SHALL add CloudBase detection next to the existing
Notion detection, without changing it: when a site has a `cloudbase` content source, the check
SHALL treat the configured envId, any `cloud://` compat reference, the CloudBase gateway host
(`*.api.tcloudbasegateway.com` or `*.api.intl.tcloudbasegateway.com`), and any URL whose path starts
with the PG storage signed-download prefix `/v1/storages/object/sign/` and carries a `token`
parameter, and the storage CDN host (`*.storage.tcloudbase.com`) in public output as a leak, and SHALL map it to the existing
`publicOutputPrivacy=failed` result. The deployment-side `DeploymentPrivacyValidator` SHALL detect
`cloud://` compat references, the gateway host, the signed path with `token` and the storage CDN
host by fixed pattern
without reading configuration, and SHALL detect the envId only when the deploy command reads it
from `site.yaml` and passes it in. The report schemas, the `.bukit/publish-audit-report.json`
contract, the existing error codes and the Notion applicability rules MUST NOT change. Diagnostics
MUST stay redacted and MUST NOT print the envId, the `cloud://` reference or the signed link.

#### Scenario: envId in public JSON feed
- **WHEN** a strict build of a site with a `cloudbase` source writes the configured envId into a public JSON feed
- **THEN** the privacy check fails, the transaction is not committed, and the diagnostic does not print the envId

#### Scenario: Signed link in deploy staging without configuration
- **WHEN** a staged deployment file contains a `cloud://` compat reference or a CloudBase gateway signed link and no configuration is passed
- **THEN** deployment returns `BKT-DEPLOY-PRIVACY-0001` before any Git add, commit or push

#### Scenario: Report contract unchanged
- **WHEN** a site without a `cloudbase` source is built and deployed
- **THEN** the security report, publish audit report and privacy results are identical to those produced before this change
