#!/usr/bin/env python3
"""Collect APK/AAB outputs from all Android modules without overwriting names."""
from __future__ import annotations
import argparse
import json
import re
import shutil
from pathlib import Path

def safe_name(value: str) -> str:
    value = re.sub(r'[^A-Za-z0-9._-]+', '-', value).strip('-_.')
    return value or 'android'

def module_name(build_dir: Path, root: Path) -> str:
    rel = build_dir.parent.relative_to(root)
    return safe_name('-'.join(rel.parts) if rel.parts else 'root')

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    root = Path(args.root).resolve()
    output = Path(args.output).resolve()
    debug_dir = output / 'debug'
    release_dir = output / 'release-to-sign'
    other_dir = output / 'other'
    reports_dir = output / 'reports'
    for d in (debug_dir, release_dir, other_dir, reports_dir, output / 'logs'):
        d.mkdir(parents=True, exist_ok=True)

    collected = []
    for p in root.rglob('*'):
        if not p.is_file() or p.suffix.lower() not in {'.apk','.aab'}:
            continue
        parts = p.parts
        if 'build' not in parts:
            continue
        try:
            build_index = len(parts) - 1 - list(reversed(parts)).index('build')
        except ValueError:
            continue
        if 'outputs' not in parts[build_index + 1:]:
            continue
        out_index = parts.index('outputs', build_index + 1)
        category = parts[out_index + 1] if out_index + 1 < len(parts) else ''
        variant = parts[out_index + 2] if out_index + 2 < len(parts) else 'unknown'
        lower = (category + '/' + variant + '/' + p.name).lower()
        mod = module_name(Path(*parts[:build_index + 1]), root)
        prefix = safe_name(f'{mod}-{variant}')
        dest_dir = debug_dir if 'debug' in lower else release_dir if 'release' in lower else other_dir
        dest = dest_dir / f'{prefix}-{safe_name(p.stem)}{p.suffix.lower()}'
        counter = 2
        while dest.exists():
            dest = dest_dir / f'{prefix}-{safe_name(p.stem)}-{counter}{p.suffix.lower()}'
            counter += 1
        shutil.copy2(p, dest)
        collected.append({'source': str(p), 'destination': str(dest.relative_to(output)), 'size_bytes': dest.stat().st_size})

    # Copy Gradle reports for troubleshooting.
    for report in root.rglob('build/reports'):
        if report.is_dir():
            rel = report.parent.parent.relative_to(root)
            dest = reports_dir / safe_name('-'.join(rel.parts) if rel.parts else 'root')
            try:
                shutil.copytree(report, dest, dirs_exist_ok=True)
            except OSError:
                pass

    summary = {'project_root': str(root), 'count': len(collected), 'files': collected}
    (output / 'manifest.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary, indent=2))
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
