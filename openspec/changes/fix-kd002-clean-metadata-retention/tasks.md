# KD-002 implementation checklist

- [x] 1. Define the affected closure and metadata-retention contract; activate byte-preservation and real-entrypoint regressions and observe behavioral RED. Original RED: 9 expected failures / 56 passed; case-alias extension: 13 expected failures / 56 passed; both 0 Skip, no build or infrastructure errors.
- [x] 2. Repair CleanCommand only; verify the complete clean/entrypoint/CLI-contract specialty and preserve existing safety behavior, with no KD-002 Skip. GREEN: CleanCommandTests 29 + ProgramEntryPointTests 29 + CliContractTests 11 = 69 passed / 0 failed / 0 Skip; runtime cache HIT, no unchanged fixture rerun.
- [x] 3. Update official guidance and the KD-002 record; accept one specialty review and one delta-only final review, verify unchanged unrelated inputs and close the writer slot. Both reviews: Critical 0 / Important 0 / Minor 0; final checks and receipts are in the evidence root.

Evidence root: `/Users/ali/.local/state/bukit-verification/kd002-clean-preserve-s2CkH1`. No Engine specialty, broad/release gate, dependency change or commit is scheduled.
