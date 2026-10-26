## ADDED Requirements

### Requirement: Observe section hook completion
The renderer MUST observe the result of every BeforeRender and AfterRender hook
Task, including Tasks that are already faulted or canceled. Hook cancellation SHALL
follow the existing section plugin-failure diagnostic policy.

#### Scenario: Completed hook failure
- **WHEN** either section entry point invokes a hook which throws or returns a faulted/canceled Task
- **THEN** strict mode throws RenderException with the plugin-failure diagnostic, while off/warn modes return the plugin-failure comment

### Requirement: Preserve AfterRender failure diagnostics
The renderer MUST apply a hook's replacement HTML only when that hook succeeds.

#### Scenario: AfterRender hook fails in a permissive mode
- **WHEN** the AfterRender hook fails in off/warn mode
- **THEN** the result retains the plugin-failure comment followed by the original section HTML, rather than overwriting the diagnostic with RenderedHtml
