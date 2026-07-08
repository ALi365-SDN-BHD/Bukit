# Agent Task Workflow

This document defines the required execution order for Codex and other agents
working on Bukit Core 1.0 development tasks.

## Purpose

The repository allows sub agents, but does not allow uncontrolled multi-task
progress. Agents must finish one task at a time and must not start the next
task until the current task has passed testing, required gate validation, and
code audit.

## Mandatory Rules

1. Only one implementation task may be active at a time.
2. Sub agents may support the active task, but they must not be used to push
   multiple implementation tasks forward in parallel.
3. The active task is not complete until tests and final code audit both pass.
4. Development tasks require gate validation by default.
5. Rule-definition and rule-modification tasks do not require gate validation.
6. If any required verification step fails, the agent must stay on the same task,
   repair it, and rerun verification before continuing.

## Allowed Sub Agent Usage

- Read-only repository exploration for the current task.
- Focused evidence gathering for commands, contracts, tests, or docs.
- Isolated implementation help for the current task when the main agent keeps
  final integration and verification ownership.

## Disallowed Sub Agent Usage

- Advancing a later task before the current task is verified and audited.
- Running multiple unrelated implementation tracks at the same time.
- Treating partial implementation as complete before verification.

## Required Per-Task Flow

1. Scope the task.
   Write down the exact task being executed and keep edits inside that scope.
2. Gather context.
   Read the relevant source, tests, scripts, and active docs before editing.
3. Use sub agents only if they help the same task.
   Keep them bounded and do not turn them into parallel task execution.
4. Implement the task.
   Finish the intended code and documentation changes for that task.
5. Run focused verification first.
   Execute the most relevant targeted tests or checks for the changed area.
6. Run the task gate.
   Use a repository gate for normal development work. Default to
   `bash scripts/gates/ci-fast.sh Release` unless the changed surface clearly
   requires a narrower owned gate or a broader gate such as
   `bash scripts/gates/ci-full.sh Release`.
   Skip this step only when the task is defining or modifying repository rules.
7. Audit the final result.
   Review the final diff, impacted contracts, tests, and active docs for
   regressions, drift, missing coverage, and boundary violations.
8. Close the task.
   Only after the audit is clean may the agent start the next queued task.

## Audit Checklist

- The diff matches the task scope and does not contain unrelated edits.
- Tests cover the changed behavior or the task explicitly documents why they
  cannot.
- Required gate commands passed on the final version of the task, unless the
  task itself was rule definition or rule maintenance.
- Active documentation and skills match the new behavior when the task changes
  user-facing or agent-facing project contracts.
- No new Core/Labs boundary drift, stale command references, or compatibility
  claims were introduced.

## Stop Conditions

Stop and remain on the current task when any of the following is true:

- A focused test fails.
- A required gate script fails.
- The code audit finds an unresolved bug, regression risk, or documentation
  drift.
- The sub agent output conflicts with the repository source of truth.
