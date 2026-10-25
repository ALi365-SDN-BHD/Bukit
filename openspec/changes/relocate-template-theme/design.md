# Design

Rendering remains above Theme and below Engine. Shared has no Scriban implementation; Config has no theme.yaml validation. `ITemplateRenderer` is public in Rendering; linting/warning types are public in Rendering.Scriban and their implementation helpers are internal. Theme owns its resolver/bootstrap types and internal name/path helpers.

Theme may not reference Engine or Engine.Primitives. Its private path helper copies only the original enforced-root branch of `BuildPathUtils.MakeAbsolute`: null check, lexical `GetFullPath`, root equality or separator-delimited prefix using `PlatformPathHelper.PathComparison`, and the exact `ConfigException`/`ConfigPathTraversal` message. Symlink-aware `PathUtils` is intentionally not substituted. `BuildPathUtils` is unchanged.

The name sanitizer also formerly called the Primitives device-name predicate; retain that exact trimmed/lowercase/four-character digit predicate locally without another dependency or public helper.

Theme exposes internals to Engine and Theme.Tests; Abstractions adds only Theme as a friend. Rendering retains exactly Engine, Engine.Audit and Rendering.Tests. CLI keeps its Content/Routing bans and permits Rendering only for `DoctorTemplateAnalyzer`.

`ThemeManifestYamlValidator.Validate` retains the former Config entry's body and diagnostics/order/returns, with all three strict partials. ThemeDoctor retains diagnosis, issue order and `DoctorResult` record/list semantics. Labs' internal `ThemeDoctorReportPrinter.Print` retains the printing body, text, colors, headings, spacing and error/warning/clean priority.

Tests move with ownership, except direct consumer tests, which change imports/calls. Compiler-safe reflection boundaries first demonstrate missing migration by assertion failures. Current architecture graphs/counts/signatures change; historical candidate full names and blob `7b07d6890562387010b52301e9f8716e9bf10ed1` do not. Direct API snapshot/check uses only the 19 built governed assemblies; intentional old/new and propagated signature drift is reconciled before adopting the baseline.
