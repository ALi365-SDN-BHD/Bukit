# Template / Theme relocation (2.0)

This independent change was approved on 2026-10-25 against base
`ad13645205e8c61220a92835eb6f7d4e813dc1dc`. It is separate from the paused
logger work and the Section task. Its contract is recorded in
[`relocate-template-theme`](../../openspec/changes/archive/2026-10-26-relocate-template-theme/proposal.md).
The direct compiled snapshot/check measures **428 → 426 types**, with
**19 assemblies unchanged**. Specialty verification and the scheduled reviews
remain separate acceptance evidence.

## CLR migration map

All affected exported types retain the governance vocabulary
`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`. This is an
explicitly approved 2.0 breaking migration, not a compatible 1.x narrowing or
an SDK/productization promise.

| Before | Current owner / replacement | Consumer action |
|---|---|---|
| `Bukit.Shared.ScribanLayoutDirectiveParser` | Internal `Bukit.Rendering.Scriban.ScribanLayoutDirectiveParser` | No public helper replacement; use the rendering entry point, not internal helpers. |
| `Bukit.Shared.ShortcodeProcessor` | Internal `Bukit.Rendering.Scriban.ShortcodeProcessor` | No public helper replacement. |
| `Bukit.Engine.ITemplateRenderer` | `Bukit.Rendering.ITemplateRenderer` | Update assembly/imports and recompile custom implementations/callers; `RenderPage` / `RenderList` shapes remain. |
| `Bukit.Engine.TemplateRendererBase` | Deleted, not moved | There is no drop-in inheritance replacement. Adapt custom renderers to the relocated interface or compose the existing renderer; protected hooks/state are removed too. |
| `Bukit.Engine.ScribanTemplateLinter` | `Bukit.Rendering.Scriban.ScribanTemplateLinter` | Update assembly/imports; all four public methods remain. |
| `Bukit.Engine.TemplateVariableWarning` | `Bukit.Rendering.Scriban.TemplateVariableWarning` | Update identity and recompile; positional sealed record shape remains. |
| `Bukit.Engine.ThemeTemplateResolver` | `Bukit.Theme.ThemeTemplateResolver` | Update assembly/imports and recompile. |
| `Bukit.Engine.ThemeBootstrapper` | `Bukit.Theme.ThemeBootstrapper` | Both public bootstrap methods return the relocated result. |
| `Bukit.Engine.ThemeBootstrapResult` | `Bukit.Theme.ThemeBootstrapResult` | Six-argument positional record/nullability remain; `SectionPlugins` stays internal. |
| `Bukit.Config.ConfigValidator.ValidateThemeYaml(string)` | `Bukit.Theme.ThemeManifestYamlValidator.Validate(string)` | Call the new Theme entry; `List<string>` and BKT-0100/BKT-0101 diagnostics/order remain. Config's application-config `Validate` overloads remain. |
| `Bukit.Theme.ThemeDoctorCommand` | `Bukit.Theme.ThemeDoctor` | Rename calls to `Diagnose`; no Theme report-printing method remains. |
| `ThemeDoctorCommand.DoctorResult` | `ThemeDoctor.DoctorResult` | Nested public sealed record, mutable `List<string>`, constructor, properties/deconstruction and record/list semantics remain, but CLR identity changes. Reflection key is `Bukit.Theme.ThemeDoctor+DoctorResult`. |
| `ThemeDoctorCommand.PrintReport(DoctorResult)` | Internal Labs CLI `ThemeDoctorReportPrinter.Print` | Not a public replacement SDK. External callers own presentation or use the CLI. |
| `RouteInventoryValidator.BuildContentRoutesAsync(..., ThemeTemplateResolver?, ...)` | Same Engine method, resolver type now `Bukit.Theme.ThemeTemplateResolver?` | Recompile; other arguments/defaults/nullability remain. |
| Both `SiteEngine.GetListRoutes` resolver-taking overloads | Same Engine methods, resolver type now `Bukit.Theme.ThemeTemplateResolver?` | Recompile both collections-only and routed-document consumers. |

The analyzer/context/known-fields helpers move internally to Rendering.Scriban.
Theme owns internal `ThemePathResolver`, `ResolvedThemePaths`, `ThemeNameSanitizer`
and all three strict manifest-validator partials. None becomes a public helper.
Target metadata owners are `Rendering and theme model` and `Theme runtime`.

## Compatibility limits and preserved behavior

Existing binaries, explicit full-name/reflection consumers and source consumers
must migrate and recompile. There are no compatibility facades or type forwarders.
Repository-local consumer migration does **not** establish that private,
unindexed, binary-only, reflection-based or undisclosed consumers do not exist.
The internalized helpers and deleted inheritance/printing surfaces cannot be
recovered merely by changing a namespace.

This task does not redesign CLI output/exits, application configuration,
theme.yaml shapes/diagnostics, template objects, reports or the process-plugin
protocol. Core `bukit doctor` remains independent of Labs diagnosis; Labs still
returns its existing exit code after printing. The printer retains the exact
heading, blank lines, two-space indentation, issue order, colors/resets, and
error-before-warning-before-clean summaries.

Theme adds no Engine/Primitives project reference. Its private path helper keeps
the original enforced-root lexical normalization, null check, platform comparison,
root-equality/separator-prefix check and exact traversal diagnostic. It does not
resolve symlinks. `BuildPathUtils` is unchanged. The name sanitizer locally keeps
the original reserved-device-name predicate rather than acquiring an upward
Primitives dependency. Theme is a friend of Abstractions; Theme's friends are
Engine and Theme.Tests. Rendering's friend set is unchanged. CLI permits Rendering
only for `DoctorTemplateAnalyzer`; Content/Routing bans remain.

## Historical decisions and evidence

The closed 136-entry candidate manifest, blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`, and historical decision reports
remain unchanged. Their old names/counts describe those decisions, not current
exports. In particular:

- G-04D8B's old facade/printing retention reason is superseded by this later
  approved rename and split; the record/list shape remains retained.
- G-04D9B's earlier interface/base retention is superseded only for the relocated
  interface and deleted base/protected hooks; the other contracts are unchanged.
- G-04D9H's warning retention remains, now in Rendering.Scriban with its linter;
  the other list/template companions do not move.

## Scoped verification

The authorized evidence is complete Architecture, CLI, Config, Engine, Rendering,
Shared, Theme, Labs CLI and Engine.Abstractions test projects; the complete
Importing `ThemeGeneratorTests` class; and API/workflow owner self-tests. Commands
run serially because they create fixtures, output, locks or caches. No full,
release, whole-solution or post-change gate is implied.

The direct API tool snapshots/checks the 19 individually built governed assemblies,
not the solution-building wrapper. Measured reconciliation: 11 old identities
removed, nine new identities, three retained types with propagated/member changes,
and 414 otherwise unchanged entries. Reflection-derived member signatures and
nine explicitly assigned metadata rows must reconcile before adopting the
baseline. Review/acceptance identifies the base and uncommitted diff, not a
fictional migration commit.
