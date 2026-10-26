# Resize cleanup boundary

## Root cause and repair

`ImageVariantGenerator.GenerateResizedVariantAsync` creates two temporary names inside its outer error-handling region, but executes the external or native backend before entering the inner cleanup region. Backend exceptions bypass the two existing `TryDelete` calls.

Keep both names outside the inner `try`, move that `try` above backend execution, and leave validation, freshness writing, atomic moves and the original `finally` together. Cleanup deletes only this invocation's temporary image and temporary freshness path. The outer caller-cancellation rethrow and ordinary warning catch remain unchanged. No helper, seam, configuration or janitor is needed.

## Compatibility boundaries

Successful bytes, freshness serialization/property order, variant identity, move order, logger payloads/counting, user-owned lookalikes, stale-managed policy and srcset/tracking remain unchanged. The real external runner retains its ten-second resize timeout and process-tree handling. The independently correct WebP cleanup remains untouched. Public signatures and the 426-type/19-assembly baseline are unchanged.

## Regression evidence

Strengthen the existing cancellation test to check the caller token and temporary-file absence before fixture teardown, retaining its observed partial file, child-kill marker and 1200ms late-write wait. Add one timeout test whose fake version probe succeeds immediately, resize writes the supplied GUID destination and a started marker outside assets, then sleeps for thirty seconds. Use the real ten-second production timeout, one size and no WebP formats. Require a timeout warning, unchanged source bytes and no published/tracked/projected or temporary output.

Run complete selected classes for ASSERT RED before production repair, and complete workflow-owner ASSERT RED before the one exact contract mapping rule. Freeze the eight-path delta before scheduled builds/GREEN. Preserve exact commands, full TRX cases, stable per-command consumed inputs, toolchain/environment states and official cache evidence externally.

## Limits

Existing `TryDelete` is best-effort for filesystem and permission errors. This repair does not guarantee cleanup after crashes/power loss, remove historical leftovers, validate real installed ImageMagick or claim unexecuted Windows/Linux runtime coverage. Fixture teardown deletes only fixture-owned directories. Parent specialty/final review and acceptance are separate from writer evidence.
