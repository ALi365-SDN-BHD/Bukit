# Fix resize temporary-file cleanup

## Why

The resize backend currently runs before the existing cleanup `try/finally`. A timeout or caller cancellation can therefore leave its GUID-named partial image behind, although no final variant is published. The existing cancellation test observes that partial image but only checks the final filename before deleting the fixture directory.

## Approved change

This is the separately user-approved Core task I-IMAGE-TEMP, not an extension of the accepted plugin-split migration. Move backend execution into the existing cleanup region and add timeout/cancellation regression coverage using fixture-owned fake tools and the real process runner.

## Scope and non-goals

Only the generator, its existing image test class, four new contract files, and the existing workflow mapping/owner self-test change. Preserve public API, image bytes, quality, orientation, sizes, freshness schema, atomic publication, ownership, logger behavior and process timeout/termination semantics. Do not sweep historical temporary files, touch user assets, alter WebP/optimizer/runner code, combine the separate Notion worktree, or perform publication or full/release gates.

## Verification

Use the two complete image/process classes, the complete BuiltInPluginSplitTests class, the workflow owner self-test and the unchanged direct API tool against this worktree's 426-type/19-assembly baseline. Exact commands, input closures, RED/GREEN evidence and acceptance remain in the assigned external task evidence, separate from historical migration acceptance.
