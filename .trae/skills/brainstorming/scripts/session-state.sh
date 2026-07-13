#!/usr/bin/env bash

read_session_line() {
  local file=$1 value extra
  [[ -f "$file" && ! -L "$file" ]] || return 1

  exec 3< "$file" || return 1
  if ! IFS= read -r value <&3; then
    exec 3<&-
    return 1
  fi
  extra=""
  if IFS= read -r extra <&3 || [[ -n "$extra" ]]; then
    exec 3<&-
    return 1
  fi
  exec 3<&-
  [[ -n "$value" ]] || return 1
  [[ "$value" != *$'\r'* && "$value" != *$'\n'* ]] || return 1
  printf '%s\n' "$value"
}

resolve_session_dir() {
  local path=$1
  [[ -d "$path" ]] || return 1
  (cd -- "$path" 2>/dev/null && pwd -P)
}

classify_session_dir() {
  local path=$1 physical tmp_root name parent
  physical="$(resolve_session_dir "$path")" || return 1
  tmp_root="$(cd /tmp 2>/dev/null && pwd -P)" || return 1
  name="${physical##*/}"
  parent="${physical%/*}"

  if [[ "$parent" == "$tmp_root" && "$name" =~ ^brainstorm-[0-9]+-[0-9]+-[0-9]+$ ]]; then
    printf '%s\n' ephemeral
    return 0
  fi
  [[ "$name" =~ ^[0-9]+-[0-9]+-[0-9]+$ ]] || return 1
  case "$physical" in
    */.superpowers/brainstorm/"$name")
      printf '%s\n' persistent
      return 0
      ;;
  esac
  return 1
}

canonical_server_path() {
  local server=$1 parent name physical
  [[ "$server" == /* && -f "$server" && ! -L "$server" ]] || return 1
  name="${server##*/}"
  [[ "$name" == server.cjs ]] || return 1
  parent="$(cd -- "${server%/*}" 2>/dev/null && pwd -P)" || return 1
  physical="$parent/$name"
  [[ "$physical" == "$server" ]] || return 1
  printf '%s\n' "$physical"
}

physical_executable_path() {
  local candidate=$1 parent name target count=0
  [[ -n "$candidate" ]] || return 1
  case "$candidate" in /*) ;; *) candidate="$PWD/$candidate" ;; esac
  while [[ "$count" -lt 40 ]]; do
    parent="$(cd -- "${candidate%/*}" 2>/dev/null && pwd -P)" || return 1
    name="${candidate##*/}"
    candidate="$parent/$name"
    if [[ ! -L "$candidate" ]]; then
      [[ -f "$candidate" && -x "$candidate" ]] || return 1
      printf '%s\n' "$candidate"
      return 0
    fi
    target="$(readlink "$candidate")" || return 1
    case "$target" in /*) candidate=$target ;; *) candidate="$parent/$target" ;; esac
    count=$((count + 1))
  done
  return 1
}

write_session_state() {
  local state=$1 pid=$2 token=$3 server=$4 owner_uid prefix server_path
  [[ -d "$state" && ! -L "$state" ]] || return 1
  [[ "$pid" =~ ^[0-9]+$ && "$pid" -gt 1 ]] || return 1
  [[ "$token" =~ ^[A-Za-z0-9._-]+$ ]] || return 1
  server_path="$(canonical_server_path "$server")" || return 1
  owner_uid="$(id -u)"
  [[ "$owner_uid" =~ ^[0-9]+$ ]] || return 1

  [[ ! -e "$state/server.pid" && ! -e "$state/owner.uid" && \
     ! -e "$state/server.path" && ! -e "$state/server.token" ]] || return 1

  prefix="$state/.session-state.$$.$RANDOM"
  if ! { printf '%s\n' "$pid" > "$prefix.pid" &&
         printf '%s\n' "$owner_uid" > "$prefix.uid" &&
         printf '%s\n' "$server_path" > "$prefix.path" &&
         printf '%s\n' "$token" > "$prefix.token" &&
         chmod 600 "$prefix.pid" "$prefix.uid" "$prefix.path" "$prefix.token" &&
         mv "$prefix.pid" "$state/server.pid" &&
         mv "$prefix.uid" "$state/owner.uid" &&
         mv "$prefix.path" "$state/server.path" &&
         mv "$prefix.token" "$state/server.token"; }; then
    rm -f "$prefix.pid" "$prefix.uid" "$prefix.path" "$prefix.token"
    rm -f "$state/server.pid" "$state/owner.uid" "$state/server.path" "$state/server.token"
    return 1
  fi
}

validate_process_identity() {
  local pid=$1 owner_uid=$2 server_path=$3 token=$4 live_uid live_comm command suffix node_command node_path node_name
  [[ "$pid" =~ ^[0-9]+$ && "$pid" -gt 1 ]] || return 1
  [[ "$owner_uid" =~ ^[0-9]+$ ]] || return 1
  [[ "$token" =~ ^[A-Za-z0-9._-]+$ ]] || return 1
  server_path="$(canonical_server_path "$server_path")" || return 1

  live_uid="$(ps -o uid= -p "$pid" 2>/dev/null | tr -d ' ')"
  live_comm="$(ps -o ucomm= -p "$pid" 2>/dev/null | tr -d ' ' || true)"
  [[ -n "$live_comm" ]] || live_comm="$(ps -o comm= -p "$pid" 2>/dev/null | sed 's#.*/##' | tr -d ' ')"
  command="$(ps -ww -o command= -p "$pid" 2>/dev/null)"
  [[ -n "$live_uid" && -n "$live_comm" && -n "$command" ]] || return 1
  [[ "$command" != *$'\r'* && "$command" != *$'\n'* ]] || return 1
  [[ "$live_uid" == "$owner_uid" ]] || return 1

  suffix=" $server_path --session-token=$token"
  case "$command" in *"$suffix") node_command="${command%"$suffix"}" ;; *) return 1 ;; esac
  [[ -n "$node_command" && "$node_command" != *' '* && "$node_command" != *$'\t'* ]] || return 1
  node_path="$(physical_executable_path "$node_command")" || return 1
  [[ "$node_path" == "$node_command" ]] || return 1
  node_name="${node_path##*/}"
  [[ "$node_name" == node || "$node_name" == nodejs ]] || return 1
  [[ "$live_comm" == "$node_name" ]] || return 1
  printf '%s\n' "$pid"
}

validate_session_process() {
  local state=$1 pid owner_uid server_path token
  [[ -d "$state" && ! -L "$state" ]] || return 1
  pid="$(read_session_line "$state/server.pid")" || return 1
  owner_uid="$(read_session_line "$state/owner.uid")" || return 1
  server_path="$(read_session_line "$state/server.path")" || return 1
  token="$(read_session_line "$state/server.token")" || return 1
  validate_process_identity "$pid" "$owner_uid" "$server_path" "$token"
}

session_state_matches() {
  local state=$1 expected_pid=$2 expected_token=$3 expected_server=$4 pid owner_uid server_path token
  [[ -d "$state" && ! -L "$state" ]] || return 1
  pid="$(read_session_line "$state/server.pid")" || return 1
  owner_uid="$(read_session_line "$state/owner.uid")" || return 1
  server_path="$(read_session_line "$state/server.path")" || return 1
  token="$(read_session_line "$state/server.token")" || return 1
  expected_server="$(canonical_server_path "$expected_server")" || return 1
  [[ "$pid" == "$expected_pid" && "$owner_uid" == "$(id -u)" ]] || return 1
  [[ "$server_path" == "$expected_server" && "$token" == "$expected_token" ]]
}
