# Priority 6: speed metrics without raw commands.
metrics_state="$scratch/speed-metrics.json"
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase implementation \
  --duration-ms 100 --cache-status none --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase test \
  --duration-ms 50 --cache-status miss --command-label config-tests \
  --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase test \
  --duration-ms 40 --cache-status hit --command-label config-tests \
  --rerun --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase review \
  --duration-ms 20 --cache-status none --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-b --phase idle \
  --duration-ms 10 --cache-status none --conflict --status blocked

expect_exit 0 "${tool[@]}" metrics report --state "$metrics_state"
metrics_output="$command_output"
python3 - "$metrics_output" "$metrics_state" <<'PY'
import json
import pathlib
import sys

report = json.loads(sys.argv[1])
state_text = pathlib.Path(sys.argv[2]).read_text(encoding="utf-8")
if "dotnet test" in state_text or "--config" in state_text:
    raise SystemExit("metrics state unexpectedly stored a raw command")
if report["phaseDurationsMs"] != {
    "idle": 10,
    "implementation": 100,
    "review": 20,
    "test": 90,
}:
    raise SystemExit(f"unexpected phase totals: {report['phaseDurationsMs']}")
if report["cache"] != {"hitRate": 0.5, "hits": 1, "misses": 1}:
    raise SystemExit(f"unexpected cache metrics: {report['cache']}")
if report["duplicateCommandLabels"] != [{"count": 2, "label": "config-tests"}]:
    raise SystemExit(
        f"unexpected duplicate labels: {report['duplicateCommandLabels']}"
    )
if report["rerunCount"] != 1 or report["conflictCount"] != 1:
    raise SystemExit("unexpected rerun or conflict count")
if report["taskTotalsMs"] != {"task-a": 210, "task-b": 10}:
    raise SystemExit(f"unexpected task totals: {report['taskTotalsMs']}")
if report["statusCounts"] != {"blocked": 1, "completed": 4}:
    raise SystemExit(f"unexpected status counts: {report['statusCounts']}")
if report["eventCount"] != 5 or report["schemaVersion"] != 1:
    raise SystemExit("unexpected metrics event count or schema version")
PY

invalid_metrics="$scratch/invalid-metrics.json"
printf '%s\n' \
  '{"events":[{"cacheStatus":"hit","commandLabel":null,"conflict":false,"durationMs":"secret","phase":"test","rerun":false,"status":"completed","taskId":"task-a"}],"schemaVersion":1}' \
  >"$invalid_metrics"
expect_exit 2 "${tool[@]}" metrics report --state "$invalid_metrics"
assert_contains "$command_output" "metrics event 0"

