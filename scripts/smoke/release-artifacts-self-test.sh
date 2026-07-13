#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-release-artifacts-self-test.XXXXXX")"
scratch="$(cd "$scratch" && pwd -P)"
trap 'rm -rf "$scratch"' EXIT
total=0; passed=0; output="$scratch/output.log"

record() {
  total=$((total + 1))
  if "$@"; then passed=$((passed + 1)); echo "ok $total - $label"
  else echo "not ok $total - $label" >&2; sed 's/^/  /' "$output" >&2; fi
}
check() { label="$1"; shift; : > "$output"; record "$@"; }
log_is_ordered() {
  local log="$1" a b c d
  a="$(sed -n '1p' "$log")"; b="$(sed -n '2p' "$log")"
  c="$(sed -n '3p' "$log")"; d="$(sed -n '4p' "$log")"
  case "$a" in "config check --config "*) ;; *) return 1;; esac
  case "$b" in "build --config "*" --clean") ;; *) return 1;; esac
  case "$c" in "publish audit --dir "*) ;; *) return 1;; esac
  [ -z "$d" ]
}
smoke_ok() {
  local input="$1" rid="$2" log="$3"; : > "$log"
  FAKE_BUKIT_LOG="$log" bash "$repo_root/scripts/smoke/release-artifacts.sh" \
    "$input" "$rid" > "$output" 2>&1 && log_is_ordered "$log"
}
smoke_bad() {
  ! FAKE_BUKIT_LOG="$scratch/fail.log" bash "$repo_root/scripts/smoke/release-artifacts.sh" \
    "$1" "$2" > "$output" 2>&1
}
smoke_bad_clean() {
  local temp_root="$scratch/cleanup-root"; mkdir -p "$temp_root"
  ! TMPDIR="$temp_root" FAKE_BUKIT_LOG="$scratch/fail.log" \
    bash "$repo_root/scripts/smoke/release-artifacts.sh" "$1" "$2" > "$output" 2>&1 || return 1
  [ -z "$(find "$temp_root" -mindepth 1 -print -quit)" ]
}
extract_bad() {
  rm -rf "$3" "$4"
  ! python3 "$repo_root/scripts/smoke/extract-release-artifact.py" "$1" "$2" "$3" \
    > "$output" 2>&1 && [ ! -e "$4" ]
}
extract_bad_sanitized() {
  rm -rf "$3"
  ! python3 "$repo_root/scripts/smoke/extract-release-artifact.py" "$1" "$2" "$3" \
    > "$output" 2>&1 || return 1
  python3 -c 'import pathlib,sys,unicodedata; s=pathlib.Path(sys.argv[1]).read_text(); raise SystemExit(any(unicodedata.category(c)=="Cc" for c in s.rstrip("\n")))' "$output"
}
safe_relative_bad() {
  ! python3 -c 'import runpy,sys; runpy.run_path(sys.argv[1])["safe_relative"](sys.argv[2])' \
    "$repo_root/scripts/smoke/extract-release-artifact.py" "$1" > "$output" 2>&1
}
usage_bad() {
  if bash "$repo_root/scripts/smoke/release-artifacts.sh" "$@" > "$output" 2>&1; then
    return 1
  else
    [ "$?" -eq 2 ]
  fi
}
preserves_existing() {
  ! python3 "$repo_root/scripts/smoke/extract-release-artifact.py" "$1" linux-x64 "$2" \
    > "$output" 2>&1 && [ "$(cat "$3")" = "$4" ]
}
preserves_symlink_target() {
  ! python3 "$repo_root/scripts/smoke/extract-release-artifact.py" "$1" linux-x64 "$2" \
    > "$output" 2>&1 && [ -L "$3" ] && [ "$(cat "$4")" = "$5" ]
}

cat > "$scratch/fake" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${FAKE_BUKIT_LOG:?}"
SH
chmod 755 "$scratch/fake"
python3 - "$scratch" <<'PY'
import io, pathlib, stat, sys, tarfile, warnings, zipfile
root=pathlib.Path(sys.argv[1]); fake=(root/'fake').read_bytes(); data=b'payload'
def ti(name, body=data, mode=0o644, kind=None, link=''):
    x=tarfile.TarInfo(name); x.mode=mode
    if kind is not None: x.type=kind; x.linkname=link
    else: x.size=len(body)
    return x, (None if kind is not None else io.BytesIO(body))
def tar(name, entries):
    with tarfile.open(root/name,'w:gz') as z:
        for entry in entries: z.addfile(*entry)
def zi(name, mode, body=b''):
    x=zipfile.ZipInfo(name); x.create_system=3; x.external_attr=mode<<16; return x,body
tar('bukit-1.2.3-linux-x64.tar.gz',[ti('bin',mode=0o755,kind=tarfile.DIRTYPE),ti('bin/bukit',fake,0o755)])
with zipfile.ZipFile(root/'bukit-1.2.3-win-x64.zip','w') as z:
    for x,b in [zi('nested/',stat.S_IFDIR|0o755),zi('nested/bukit.exe',stat.S_IFREG|0o755,fake)]: z.writestr(x,b)
tar('bukit-empty-linux-x64.tar.gz',[]); tar('bukit-no-linux-x64.tar.gz',[ti('readme')])
tar('bukit-two-linux-x64.tar.gz',[ti('a/bukit',fake,0o755),ti('b/bukit',fake,0o755)])
tar('bukit-nonexec-linux-x64.tar.gz',[ti('bukit',fake,0o644)])
tar('bukit-late-dir-linux-x64.tar.gz',[ti('bin/bukit',fake,0o755),ti('bin',mode=0o755,kind=tarfile.DIRTYPE)])
tar('bukit-restricted-linux-x64.tar.gz',[ti('locked',mode=0o000,kind=tarfile.DIRTYPE),ti('locked/bukit',fake,0o755)])
special={
 'absolute':[ti('/absolute')], 'empty-name':[ti('')], 'dot':[ti('.')],
 'parent':[ti('../escape')], 'backslash':[ti('..\\escape')], 'drive':[ti('C:/escape')],
 'symlink':[ti('link',kind=tarfile.SYMTYPE,link='../escape')],
 'hardlink':[ti('target'),ti('hardlink',kind=tarfile.LNKTYPE,link='target')],
 'device':[ti('device',kind=tarfile.CHRTYPE)], 'fifo':[ti('fifo',kind=tarfile.FIFOTYPE)],
 'newline':[ti('bad\nname')], 'escape':[ti('bad\x1bname')], 'delete':[ti('bad\x7fname')],
 'c1':[ti('bad\x85name')],
 'duplicate':[ti('same'),ti('same')], 'normalized-duplicate':[ti('dir/item'),ti('dir/./item')],
 'file-parent':[ti('parent'),ti('parent/child')], 'existing':[ti('existing')],
 'existing-parent':[ti('parent/child')]}
for name,entries in special.items(): tar('bukit-'+name+'-linux-x64.tar.gz',entries)
warnings.simplefilter('ignore',UserWarning)
for name,entries in {
 'symlink':[zi('link',stat.S_IFLNK|0o777,b'../escape')],
 'device':[zi('device',stat.S_IFCHR|0o600,data)], 'fifo':[zi('fifo',stat.S_IFIFO|0o600,data)],
 'newline':[zi('bad\nname',stat.S_IFREG|0o644,data)], 'escape':[zi('bad\x1bname',stat.S_IFREG|0o644,data)],
 'delete':[zi('bad\x7fname',stat.S_IFREG|0o644,data)], 'c1':[zi('bad\x85name',stat.S_IFREG|0o644,data)],
 'duplicate':[zi('same',stat.S_IFREG|0o644,data),zi('same',stat.S_IFREG|0o644,data)],
 'file-parent':[zi('parent',stat.S_IFREG|0o644,data),zi('parent/child',stat.S_IFREG|0o644,data)],
 'directory':[zi('nested/',stat.S_IFDIR|0o755),zi('nested/bukit',stat.S_IFREG|0o755,fake)]}.items():
    with zipfile.ZipFile(root/('bukit-'+name+'-win-x64.zip'),'w') as z:
        for x,b in entries: z.writestr(x,b)
PY

mkdir -p "$scratch/dir-linux/nested" "$scratch/dir-win/nested" "$scratch/empty" \
  "$scratch/no" "$scratch/two/a" "$scratch/two/b" "$scratch/nonexec"
cp "$scratch/fake" "$scratch/dir-linux/nested/bukit"
cp "$scratch/fake" "$scratch/dir-win/nested/bukit.exe"
printf 'readme\n' > "$scratch/no/readme"; cp "$scratch/fake" "$scratch/two/a/bukit"
cp "$scratch/fake" "$scratch/two/b/bukit"; cp "$scratch/fake" "$scratch/nonexec/bukit"
chmod 644 "$scratch/nonexec/bukit"
mkdir -p "$scratch/package-publish"
cp "$scratch/fake" "$scratch/package-publish/bukit"
tar -C "$scratch/package-publish" -czf "$scratch/bukit-package-shaped-linux-x64.tar.gz" .
cp "$scratch/bukit-1.2.3-linux-x64.tar.gz" "$scratch/bukit-1.2.3-osx-arm64.tar.gz"

check "tar.gz archive runs real smoke" smoke_ok "$scratch/bukit-1.2.3-linux-x64.tar.gz" linux-x64 "$scratch/tar.log"
check "osx archive name runs real smoke" smoke_ok "$scratch/bukit-1.2.3-osx-arm64.tar.gz" osx-arm64 "$scratch/osx.log"
check "package-native-aot tar shape runs real smoke" smoke_ok \
  "$scratch/bukit-package-shaped-linux-x64.tar.gz" linux-x64 "$scratch/package-tar.log"
check "tar permits directory entries after children" smoke_ok "$scratch/bukit-late-dir-linux-x64.tar.gz" linux-x64 "$scratch/late.log"
check "zip archive runs real smoke" smoke_ok "$scratch/bukit-1.2.3-win-x64.zip" win-x64 "$scratch/zip.log"
check "directory has linux smoke semantics" smoke_ok "$scratch/dir-linux" linux-x64 "$scratch/dir.log"
check "windows directory finds bukit.exe" smoke_ok "$scratch/dir-win" win-x64 "$scratch/win.log"
for spec in "empty:empty" "without CLI:no" "with duplicate CLI:two" "with non-executable CLI:nonexec"; do
  label_part=${spec%%:*}; path_part=${spec#*:}
  check "directory $label_part is rejected" smoke_bad "$scratch/$path_part" linux-x64
done
for spec in "empty:empty" "without CLI:no" "with duplicate CLI:two" "with non-executable CLI:nonexec"; do
  label_part=${spec%%:*}; path_part=${spec#*:}
  check "archive $label_part is rejected" smoke_bad "$scratch/bukit-$path_part-linux-x64.tar.gz" linux-x64
done
check "zip rejects POSIX RID" smoke_bad "$scratch/bukit-1.2.3-win-x64.zip" linux-x64
check "tar rejects Windows RID" smoke_bad "$scratch/bukit-1.2.3-linux-x64.tar.gz" win-x64
check "linux archive is rejected for osx RID" smoke_bad "$scratch/bukit-1.2.3-linux-x64.tar.gz" osx-arm64
check "osx archive is rejected for linux RID" smoke_bad "$scratch/bukit-1.2.3-osx-arm64.tar.gz" linux-x64
for bad_name in bukit--linux-x64.tar.gz other-1.2.3-linux-x64.tar.gz "bukit-bad version-linux-x64.tar.gz"; do cp "$scratch/bukit-1.2.3-linux-x64.tar.gz" "$scratch/$bad_name"; check "invalid archive basename $bad_name is rejected" smoke_bad "$scratch/$bad_name" linux-x64; done
check "missing args return usage" usage_bad
check "extra args return usage" usage_bad "$scratch/dir-linux" linux-x64 extra
check "unsupported RID is rejected" smoke_bad "$scratch/dir-linux" linux-arm64
check "failed smoke cleans restricted archive scratch" smoke_bad_clean "$scratch/bukit-restricted-linux-x64.tar.gz" linux-x64
check "safe_relative rejects dot directly" safe_relative_bad .

for scenario in absolute empty-name dot parent backslash drive symlink hardlink device fifo duplicate normalized-duplicate file-parent; do
  check "tar rejects $scenario" extract_bad "$scratch/bukit-$scenario-linux-x64.tar.gz" linux-x64 \
    "$scratch/x-$scenario/root" "$scratch/x-$scenario/escape"
done
for scenario in symlink device fifo duplicate file-parent; do
  check "zip rejects $scenario" extract_bad "$scratch/bukit-$scenario-win-x64.zip" win-x64 \
    "$scratch/z-$scenario/root" "$scratch/z-$scenario/escape"
done
for scenario in newline escape delete c1; do
  check "tar rejects $scenario control with safe diagnostic" extract_bad_sanitized \
    "$scratch/bukit-$scenario-linux-x64.tar.gz" linux-x64 "$scratch/tar-control-$scenario"
  check "zip rejects $scenario control with safe diagnostic" extract_bad_sanitized \
    "$scratch/bukit-$scenario-win-x64.zip" win-x64 "$scratch/zip-control-$scenario"
done
check "zip permits safe directory" python3 "$repo_root/scripts/smoke/extract-release-artifact.py" \
  "$scratch/bukit-directory-win-x64.zip" win-x64 "$scratch/zip-directory"
mkdir -p "$scratch/existing-dest" "$scratch/parent-dest"
printf 'sentinel-file\n' > "$scratch/existing-dest/existing"
printf 'sentinel-parent\n' > "$scratch/parent-dest/parent"
check "extractor preserves existing file" preserves_existing "$scratch/bukit-existing-linux-x64.tar.gz" \
  "$scratch/existing-dest" "$scratch/existing-dest/existing" sentinel-file
check "extractor preserves existing parent" preserves_existing "$scratch/bukit-existing-parent-linux-x64.tar.gz" \
  "$scratch/parent-dest" "$scratch/parent-dest/parent" sentinel-parent
mkdir -p "$scratch/symlink-target-dest"; printf 'outside-sentinel\n' > "$scratch/outside-sentinel"
ln -s "$scratch/outside-sentinel" "$scratch/symlink-target-dest/existing"
check "extractor preserves existing target symlink" preserves_symlink_target \
  "$scratch/bukit-existing-linux-x64.tar.gz" "$scratch/symlink-target-dest" \
  "$scratch/symlink-target-dest/existing" "$scratch/outside-sentinel" outside-sentinel
mkdir -p "$scratch/outside-parent"; ln -s "$scratch/outside-parent" "$scratch/destination-parent-link"
check "extractor rejects symlinked destination parent" extract_bad "$scratch/bukit-existing-linux-x64.tar.gz" \
  linux-x64 "$scratch/destination-parent-link/new-root" "$scratch/outside-parent/new-root/existing"
mkdir -p "$scratch/outside-parent/existing"
check "extractor rejects symlink above an existing destination parent" extract_bad \
  "$scratch/bukit-existing-linux-x64.tar.gz" linux-x64 "$scratch/destination-parent-link/existing/new-root" \
  "$scratch/outside-parent/existing/new-root/existing"

if [ "$passed" -ne "$total" ]; then echo "release artifact self-test failed: $passed/$total" >&2; exit 1; fi
echo "release artifact self-test passed: $passed/$total"
