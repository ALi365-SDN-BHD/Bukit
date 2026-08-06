# Priority 4: single-writer queue.
queue_state="$scratch/writer-queue.json"
expect_exit 0 "${tool[@]}" queue init --state "$queue_state"
assert_contains "$command_output" "QUEUE INITIALIZED"

expect_exit 0 "${tool[@]}" queue acquire --state "$queue_state" --task task-a
assert_contains "$command_output" "QUEUE ACQUIRED task-a"

expect_exit 1 "${tool[@]}" queue acquire --state "$queue_state" --task task-b
assert_contains "$command_output" "active task-a"

expect_exit 2 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to done
assert_contains "$command_output" "invalid transition"

expect_exit 0 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to testing
expect_exit 0 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to review_wait
expect_exit 0 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to done

expect_exit 0 "${tool[@]}" queue acquire --state "$queue_state" --task task-b
expect_exit 0 "${tool[@]}" queue status --state "$queue_state"
queue_output="$command_output"
python3 - "$queue_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if result["activeTask"] != "task-b":
    raise SystemExit(f"unexpected active task: {result['activeTask']}")
if result["tasks"] != {"task-a": "done", "task-b": "writing"}:
    raise SystemExit(f"unexpected task states: {result['tasks']}")
if result["schemaVersion"] != 1:
    raise SystemExit("queue state must declare schemaVersion 1")
PY

corrupt_queue="$scratch/corrupt-writer-queue.json"
printf '%s\n' \
  '{"activeTask":"task-a","schemaVersion":1,"tasks":{"task-a":"testing","task-b":"review_wait"}}' \
  >"$corrupt_queue"
expect_exit 2 "${tool[@]}" queue status --state "$corrupt_queue"
assert_contains "$command_output" "multiple non-terminal tasks"

interleaved_queue="$scratch/interleaved-writer-queue.json"
expect_exit 0 "${tool[@]}" queue init --state "$interleaved_queue"
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$interleaved_queue" --task blocked-task
expect_exit 0 "${tool[@]}" queue transition \
  --state "$interleaved_queue" --task blocked-task --to blocked
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$interleaved_queue" --task active-task
expect_exit 0 "${tool[@]}" queue transition \
  --state "$interleaved_queue" --task blocked-task --to done
expect_exit 0 "${tool[@]}" queue status --state "$interleaved_queue"
python3 - "$command_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if result["activeTask"] != "active-task":
    raise SystemExit("completing a blocked task released another active task")
PY

stale_lock_queue="$scratch/stale-lock-queue.json"
expect_exit 0 "${tool[@]}" queue init --state "$stale_lock_queue"
printf 'dead-owner\n' >"${stale_lock_queue}.lock"
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$stale_lock_queue" --task recovered-task
assert_contains "$command_output" "QUEUE ACQUIRED recovered-task"

live_lock_queue="$scratch/live-lock-queue.json"
live_lock_ready="$scratch/live-lock-ready"
expect_exit 0 "${tool[@]}" queue init --state "$live_lock_queue"
python3 - "${live_lock_queue}.lock" "$live_lock_ready" <<'PY' &
import fcntl
import pathlib
import signal
import sys

with open(sys.argv[1], "a+", encoding="utf-8") as handle:
    fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
    pathlib.Path(sys.argv[2]).write_text("ready\n", encoding="utf-8")
    signal.pause()
PY
lock_holder_pid=$!
for _ in $(seq 1 100); do
  [[ -f "$live_lock_ready" ]] && break
  sleep 0.01
done
[[ -f "$live_lock_ready" ]] || fail "live lock holder did not become ready"

expect_exit 1 "${tool[@]}" queue acquire \
  --state "$live_lock_queue" --task live-lock-task
assert_contains "$command_output" "QUEUE BUSY"

kill -KILL "$lock_holder_pid" 2>/dev/null || true
wait "$lock_holder_pid" 2>/dev/null || true
lock_holder_pid=""
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$live_lock_queue" --task live-lock-task
assert_contains "$command_output" "QUEUE ACQUIRED live-lock-task"

