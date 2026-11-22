## ADDED Requirements

### Requirement: Privacy checks detect CloudBase identifiers alongside Notion
The build-side `PublicOutputPrivacyCheck` SHALL add CloudBase detection next to the existing
Notion detection, without changing it: when a site has a `cloudbase` content source, the check
SHALL treat the configured envId, any `cloud://` id, and any CloudBase temporary-link host or
signature parameter in public output as a leak and SHALL map it to the existing
`publicOutputPrivacy=failed` result. The deployment-side `DeploymentPrivacyValidator` SHALL detect
`cloud://` ids and CloudBase temporary-link hosts or signature parameters by fixed pattern without
reading configuration, and SHALL detect the envId only when the deploy command reads it from
`site.yaml` and passes it in. The report schemas, the `.bukit/publish-audit-report.json` contract,
the existing error codes and the Notion applicability rules MUST NOT change. Diagnostics MUST
stay redacted and MUST NOT print the envId, the `cloud://` id or the temporary link.

#### Scenario: envId in public JSON feed
- **WHEN** a strict build of a site with a `cloudbase` source writes the configured envId into a public JSON feed
- **THEN** the privacy check fails, the transaction is not committed, and the diagnostic does not print the envId

#### Scenario: Temporary link in deploy staging without configuration
- **WHEN** a staged deployment file contains a `cloud://` id or a CloudBase temporary link and no configuration is passed
- **THEN** deployment returns `BKT-DEPLOY-PRIVACY-0001` before any Git add, commit or push

#### Scenario: Report contract unchanged
- **WHEN** a site without a `cloudbase` source is built and deployed
- **THEN** the security report, publish audit report and privacy results are identical to those produced before this change
