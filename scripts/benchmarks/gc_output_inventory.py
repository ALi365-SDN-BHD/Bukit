"""Hash Bukit build output and normalize run-dependent report fields."""

import hashlib
import json
import os
from pathlib import Path
import stat


VOLATILE_FIELDS = {
    'build-report.json': ('startedAt', 'endedAt', 'durationMs'),
    'seo-report.json': ('generatedAt',),
    'publish-audit-report.json': ('generatedAt',),
    'geo-report.json': ('generatedAt',),
    'seo-route-map.json': ('generatedAt',),
    'artifact-manifest.json': ('generatedAt',),
    'build-manifest-digest.json': ('generatedAt',),
}
MANIFESTS = {
    'artifact-manifest.json': ('artifacts', 'artifactSetHash', 'artifactCount'),
    'build-manifest-digest.json': ('reports', 'reportSetHash', 'reportCount'),
}



def validate_entries(directory, names):
    for name in names:
        path = Path(directory) / name
        mode = path.lstat().st_mode
        if not (stat.S_ISREG(mode) or stat.S_ISDIR(mode)):
            raise ValueError(f'Expected ordinary files and directories: {path}')



def sha256(data):
    return 'sha256:' + hashlib.sha256(data).hexdigest()


def file_record(path):
    digest = hashlib.sha256()
    size = 0
    with path.open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(data)
            size += len(data)
    return 'sha256:' + digest.hexdigest(), size


def bundle_hash(entries):
    return sha256(''.join(f'{entry["path"]}|{entry["hash"]}|{entry["size"]}\n'
                          for entry in entries).encode('utf-8'))


def normalize_manifest(root, path, report, raw, normalized):
    entries_key, hash_key, count_key = MANIFESTS[path.name]
    entries = report[entries_key]
    if report[count_key] != len(entries) or report[hash_key] != bundle_hash(entries):
        raise ValueError(f'Invalid report inventory at {path.relative_to(root)}')

    for entry in entries:
        relative = entry['path']
        candidates = [(parent / relative).resolve() for parent in (path.parent, root)]
        expected = (entry['hash'], entry['size'])
        target = next((item for item in candidates if raw.get(item) == expected), None)
        if target is None:
            raise ValueError(f'Invalid report file hash or length: {relative}')
        entry['hash'], entry['size'] = normalized.get(target, expected)
        if path.name == 'build-manifest-digest.json' and relative == 'artifact-manifest.json':
            if report['artifactManifestHash'] != expected[0]:
                raise ValueError('Invalid artifactManifestHash in build-manifest-digest.json')
            report['artifactManifestHash'] = entry['hash']
    report[hash_key] = bundle_hash(entries)


def output_inventory(root):
    if not root.is_dir() or root.is_symlink():
        raise ValueError('The build did not create the dedicated output directory.')
    raw = {}
    def fail_walk(error):
        raise error

    for directory, dirs, files in os.walk(root, onerror=fail_walk):
        validate_entries(directory, dirs + files)
        for name in files:
            path = Path(directory) / name
            raw[path] = file_record(path)
    normalized = {}
    # Report inventories depend on their constituent report files.
    order = {'artifact-manifest.json': 1, 'build-manifest-digest.json': 2}
    for path in sorted(raw, key=lambda item: (order.get(item.name, 0), item)):
        relative = path.relative_to(root)
        if str(relative) == '.bukit-build-state.json':
            fields = ('ts',)
        elif relative.parent.name == '.bukit' and len(relative.parts) in (2, 3):
            fields = VOLATILE_FIELDS.get(path.name, ())
        else:
            fields = ()
        if not fields:
            continue
        report = json.loads(path.read_text(encoding='utf-8-sig'))
        if not isinstance(report, dict):
            raise ValueError(f'Expected a JSON report object: {relative}')
        for field in fields:
            if field in report:
                placeholder = 0 if field == 'durationMs' else '<run-dependent>'
                if type(report[field]) is not type(placeholder):
                    raise ValueError(f'Unexpected type for {field} in {relative}')
                report[field] = placeholder
        if path.name in MANIFESTS:
            normalize_manifest(root, path, report, raw, normalized)
        data = json.dumps(report, ensure_ascii=False, sort_keys=True).encode('utf-8')
        normalized[path] = (sha256(data), len(data))
    return {path.relative_to(root).as_posix(): normalized.get(path, raw[path])
            for path in sorted(raw)}
