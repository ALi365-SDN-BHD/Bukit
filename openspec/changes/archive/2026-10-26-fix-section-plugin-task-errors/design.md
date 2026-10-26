# Design

Both section entry points converge on RenderOneSectionBase. BeforeRender and
AfterRender currently skip GetResult for every completed Task, including failures
and cancellation. AfterRender also overwrites its failure diagnostic with the
prepopulated RenderedHtml value.

Always observe each hook Task with GetAwaiter().GetResult at the existing synchronous
bridge. Apply an AfterRender HTML replacement only inside the successful try block.
Strict mode keeps throwing RenderException; off/warn modes keep the plugin-failure
comment, with the original section HTML retained after an AfterRender failure.
Canceled hook Tasks use that same existing diagnostic policy; this change adds no
new build-cancellation API or synchronization-context guarantee.

Regressions cover both hooks, direct throws/completed faulted/completed canceled
Tasks, all three validation modes, and JSON/ScriptObject entry points. Existing
success tests remain the positive controls. The three directly affected Rendering
test classes are the complete specialty; Engine and whole-solution tests are excluded.
