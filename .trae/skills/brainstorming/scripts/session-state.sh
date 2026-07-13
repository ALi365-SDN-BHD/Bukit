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

write_session_state() {
  local state=$1 pid=$2 token=$3 server=$4 owner_uid prefix server_path
  [[ -d "$state" && ! -L "$state" ]] || return 1
  [[ "$pid" =~ ^[0-9]+$ && "$pid" -gt 1 ]] || return 1
  [[ "$token" =~ ^[A-Za-z0-9._-]+$ ]] || return 1
  server_path="$(canonical_server_path "$server")" || return 1
  owner_uid="$(id -u)"
  [[ "$owner_uid" =~ ^[0-9]+$ ]] || return 1

  prefix="$state/.session-state.$$.$RANDOM"
  printf '%s\n' "$pid" > "$prefix.pid" || return 1
  printf '%s\n' "$owner_uid" > "$prefix.uid" || return 1
  printf '%s\n' "$server_path" > "$prefix.path" || return 1
  printf '%s\n' "$token" > "$prefix.token" || return 1
  chmod 600 "$prefix.pid" "$prefix.uid" "$prefix.path" "$prefix.token" || return 1
  mv -f "$prefix.pid" "$state/server.pid" || return 1
  mv -f "$prefix.uid" "$state/owner.uid" || return 1
  mv -f "$prefix.path" "$state/server.path" || return 1
  mv -f "$prefix.token" "$state/server.token" || return 1
}

validate_session_process() {
  local state=$1 pid owner_uid server_path token live_uid command
  [[ -d "$state" && ! -L "$state" ]] || return 1
  pid="$(read_session_line "$state/server.pid")" || return 1
  owner_uid="$(read_session_line "$state/owner.uid")" || return 1
  server_path="$(read_session_line "$state/server.path")" || return 1
  token="$(read_session_line "$state/server.token")" || return 1

  [[ "$pid" =~ ^[0-9]+$ && "$pid" -gt 1 ]] || return 1
  [[ "$owner_uid" =~ ^[0-9]+$ ]] || return 1
  [[ "$token" =~ ^[A-Za-z0-9._-]+$ ]] || return 1
  server_path="$(canonical_server_path "$server_path")" || return 1

  live_uid="$(ps -o uid= -p "$pid" 2>/dev/null | tr -d ' ')"
  command="$(ps -ww -o command= -p "$pid" 2>/dev/null)"
  [[ -n "$live_uid" && -n "$command" ]] || return 1
  [[ "$live_uid" == "$owner_uid" ]] || return 1
  case " $command " in
    *" $server_path "*) ;;
    *) return 1 ;;
  esac
  case " $command " in
    *" --session-token=$token "*) ;;
    *) return 1 ;;
  esac
  printf '%s\n' "$pid"
}
