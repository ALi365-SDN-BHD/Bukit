#!/usr/bin/env python3
"""Compare GC settings using a trusted, offline Bukit site snapshot."""

import argparse
import csv
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import time

import yaml

from gc_output_inventory import file_record, output_inventory, validate_entries


OUTPUT = '.gc-phase0-output'
CACHE = '.gc-phase0-cache'
CONCURRENT_OFF = {'DOTNET_gcConcurrent': '0'}
CONSERVE_MEMORY = {'DOTNET_GCConserveMemory': '5'}
COMBINED = {**CONCURRENT_OFF, **CONSERVE_MEMORY}
EXPERIMENTS = {
    'E0': {},
    'E1': CONCURRENT_OFF,
    'E2': CONSERVE_MEMORY,
    'E3': COMBINED,
    # Native AOT takes hexadecimal digits without the 0x prefix.
    'E4a': {**COMBINED, 'DOTNET_GCgen0size': '1000000'},
    'E4b': {**COMBINED, 'DOTNET_GCgen0size': '4000000'},
}


def copy_filter(directory, names):
    validate_entries(directory, set(names) - {'.git'})
    return {'.git'}


def local_path(root, value):
    if Path(value).is_absolute():
        raise ValueError(f'Use a snapshot-relative path: {value}')
    path = (root / value).resolve()
    if not path.is_relative_to(root):
        raise ValueError(f'Path escapes the snapshot: {value}')
    return path


def validate_snapshot(root, notion_snapshot):
    config = yaml.safe_load((root / 'site.yaml').read_text(encoding='utf-8-sig'))
    content = config['content']
    if (root / '.bukit/plugins.yaml').exists():
        raise ValueError('Prepare an offline snapshot without external command plugins.')
    if (content.get('media') or {}).get('downloadToLocal') is not False:
        raise ValueError('Prestage media in the snapshot and disable media downloads.')

    for source in content.get('sources') or []:
        provider = source['type'].strip().lower()
        if provider == 'markdown':
            local_path(root, source['markdown']['dir'])
        elif provider != 'notion' or notion_snapshot is None:
            raise ValueError('Use local Markdown or provide an offline Notion snapshot.')

    resolve = ((config.get('theme') or {}).get('params') or {}).get('pages_index') or {}
    cache_path = (resolve.get('resolve_notion') or {}).get('cache_path')
    if cache_path:
        local_path(root, cache_path)
    if notion_snapshot is not None:
        snapshot = local_path(root, notion_snapshot)
        if not (snapshot / 'manifest.json').is_file():
            raise ValueError('Notion snapshot manifest.json is missing.')
    for name in (OUTPUT, CACHE):
        if (root / name).exists():
            raise ValueError(f'Remove the reserved path from the snapshot: {name}')



def measured_build(command, work, env, log, system):
    started = time.perf_counter()
    with subprocess.Popen(command, cwd=work, env=env, stdout=log,
                          stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL) as child:
        _, status, usage = os.wait4(child.pid, 0)
        child.returncode = os.waitstatus_to_exitcode(status)
    elapsed = time.perf_counter() - started
    # Darwin reports bytes and Linux reports KiB.
    peak_mib = usage.ru_maxrss / (1024 * 1024 if system == 'Darwin' else 1024)
    return child.returncode, elapsed, peak_mib


def run_matrix(args):
    site = args.site.resolve(strict=True)
    binary = args.binary.resolve(strict=True)
    system = platform.system()
    if system not in ('Darwin', 'Linux'):
        raise ValueError(f'Unsupported benchmark host: {system}')
    if Path(tempfile.gettempdir()).resolve().is_relative_to(site):
        raise ValueError('The benchmark temporary directory must be outside the input snapshot.')
    run_dir = Path(tempfile.mkdtemp(prefix='bukit-gc-phase0-')).resolve()
    print(f'Results: {run_dir}', flush=True)
    seed = shutil.copytree(site, run_dir / 'input', ignore=copy_filter)
    validate_snapshot(seed, args.notion_snapshot)
    work = run_dir / 'site'
    env = {'PATH': os.defpath, 'LC_ALL': 'C', 'TMPDIR': str(run_dir)}
    version = subprocess.check_output([binary, 'version'], cwd=seed, env=env, text=True).strip()
    (run_dir / 'header.json').write_text(json.dumps({
        'binary': str(binary), 'binary_record': file_record(binary), 'version': version,
        'source': str(site), 'host': platform.platform(), 'cpu_count': os.cpu_count(),
        'experiments': args.experiments, 'runs': args.runs,
    }, indent=2) + '\n')

    build = [binary, 'build', '--config', 'site.yaml', '--clean', '--output', OUTPUT, '--cache-dir', work / CACHE]
    if args.notion_snapshot is not None:
        build += ['--notion-offline', '--notion-snapshot', args.notion_snapshot]
    baseline = None
    with (run_dir / 'results.tsv').open('w', encoding='utf-8', newline='') as result:
        writer = csv.writer(result, delimiter='\t', lineterminator='\n')
        writer.writerow(['experiment', 'run', 'wall_s', 'peak_rss_mib', 'files', 'matches_e0'])
        for experiment in args.experiments:
            for repeat in range(1, args.runs + 1):
                label = f'{experiment}-{repeat}'
                shutil.copytree(seed, work)
                log_path = run_dir / f'{label}.log'
                with log_path.open('wb') as log:
                    rc, elapsed, peak_mib = measured_build(
                        build, work, {**env, **EXPERIMENTS[experiment]}, log, system)
                if rc:
                    raise ValueError(f'{label} failed (exit {rc}). See {log_path}')
                inventory = output_inventory(work / OUTPUT)
                if not any(Path(name).parts[-2:] == ('.bukit', 'publish-audit-report.json')
                           for name in inventory):
                    raise ValueError(f'{label} must produce a publish audit report for comparison.')
                (run_dir / f'{label}.json').write_text(json.dumps(inventory, indent=2) + '\n')
                work.rename(run_dir / label)
                if baseline is None:
                    baseline = inventory
                matches = baseline == inventory
                writer.writerow([experiment, repeat, f'{elapsed:.4f}', f'{peak_mib:.2f}',
                                 len(inventory), str(matches).lower()])
                result.flush()
                if not matches:
                    different = sorted(name for name in baseline.keys() | inventory.keys()
                                       if baseline.get(name) != inventory.get(name))
                    raise ValueError(f'{label} differs from E0: {", ".join(different[:20])}')
                print(f'{label}: {elapsed:.4f} s, {len(inventory)} files match E0', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--yes', required=True, action='store_true', help='confirm this is a trusted offline snapshot')
    parser.add_argument('site', type=Path, help='directory containing site.yaml and local content')
    parser.add_argument('--binary', type=Path, required=True, help='existing Bukit Native AOT binary')
    parser.add_argument('--runs', type=int, default=2, help='repeats per experiment (default: 2)')
    parser.add_argument('--experiments', nargs='+', choices=EXPERIMENTS, default=['E0'])
    parser.add_argument('--notion-snapshot', help='offline Notion snapshot path relative to the site')
    args = parser.parse_args()
    if args.runs < 2 or args.experiments[0] != 'E0' or len(set(args.experiments)) != len(args.experiments):
        parser.error('Use at least two runs and distinct experiments, starting with E0.')
    try:
        run_matrix(args)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError, yaml.YAMLError) as error:
        print(f'Benchmark failed: {error}', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
