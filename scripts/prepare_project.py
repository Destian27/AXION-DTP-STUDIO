#!/usr/bin/env python3
"""Prepare an Android Gradle project directory or safely extract a ZIP source."""
from __future__ import annotations
import argparse
import os
import shutil
import stat
import sys
import zipfile
from pathlib import Path, PurePosixPath

SKIP_DIRS = {'.git', '.gradle', 'build', 'node_modules', '.idea', '.dart_tool', '__MACOSX'}
MARKERS = ('settings.gradle', 'settings.gradle.kts')

def find_project_root(base: Path) -> Path:
    base = base.resolve()
    candidates: list[Path] = []
    for current, dirs, files in os.walk(base):
        cur = Path(current)
        rel_depth = len(cur.relative_to(base).parts)
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and not d.startswith('.') and rel_depth < 4]
        names = set(files)
        if any(m in names for m in MARKERS):
            candidates.append(cur)
            continue
        if rel_depth > 0 and ('build.gradle' in names or 'build.gradle.kts' in names) and 'gradlew' in names:
            candidates.append(cur)
    if not candidates:
        # Root projects sometimes contain a build.gradle but no settings file.
        if any((base / n).is_file() for n in ('build.gradle', 'build.gradle.kts', 'gradlew')):
            return base
        raise RuntimeError(f"Tidak menemukan project Gradle Android di {base}. Cari settings.gradle(.kts) atau gradlew.")
    candidates.sort(key=lambda p: (len(p.relative_to(base).parts), 0 if any((p/m).exists() for m in MARKERS) else 1))
    return candidates[0]

def safe_extract(zip_path: Path, destination: Path) -> None:
    max_total = 3 * 1024 * 1024 * 1024
    total = 0
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as archive:
        for info in archive.infolist():
            name = info.filename.replace('\\', '/')
            pure = PurePosixPath(name)
            if pure.is_absolute() or '..' in pure.parts:
                raise RuntimeError(f"ZIP berisi path tidak aman: {info.filename}")
            # Reject symlinks; Android source ZIP should contain regular files/directories.
            mode = (info.external_attr >> 16) & 0xFFFF
            if stat.S_ISLNK(mode):
                raise RuntimeError(f"ZIP berisi symbolic link yang tidak diizinkan: {info.filename}")
            total += info.file_size
            if total > max_total:
                raise RuntimeError('Ukuran hasil ekstraksi ZIP melebihi batas 3 GiB.')
            target = (destination / Path(*pure.parts)).resolve()
            if destination.resolve() not in target.parents and target != destination.resolve():
                raise RuntimeError(f"ZIP mencoba keluar dari folder ekstraksi: {info.filename}")
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(info, 'r') as src, target.open('wb') as dst:
                    shutil.copyfileobj(src, dst)

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--workspace', required=True)
    parser.add_argument('--project-dir', default='.')
    parser.add_argument('--project-zip', default='')
    args = parser.parse_args()
    workspace = Path(args.workspace).resolve()
    if args.project_zip.strip():
        zip_path = (workspace / args.project_zip).resolve()
        if workspace not in zip_path.parents:
            raise RuntimeError('project_zip harus berada di dalam repository.')
        if not zip_path.is_file() or zip_path.suffix.lower() != '.zip':
            raise RuntimeError(f'File ZIP tidak ditemukan atau bukan .zip: {zip_path}')
        extract_dir = Path(os.environ.get('RUNNER_TEMP', '/tmp')) / 'ultimate-android-source'
        if extract_dir.exists():
            shutil.rmtree(extract_dir)
        safe_extract(zip_path, extract_dir)
        root = find_project_root(extract_dir)
        print(f'ZIP diekstrak ke: {extract_dir}')
    else:
        candidate = Path(args.project_dir)
        if not candidate.is_absolute():
            candidate = workspace / candidate
        candidate = candidate.resolve()
        if workspace != candidate and workspace not in candidate.parents:
            raise RuntimeError('project_dir harus berada di dalam repository.')
        if not candidate.is_dir():
            raise RuntimeError(f'Folder project tidak ditemukan: {candidate}')
        root = find_project_root(candidate)
    out = os.environ.get('GITHUB_OUTPUT')
    if out:
        with open(out, 'a', encoding='utf-8') as handle:
            handle.write(f'project_root={root}\n')
    print(f'ANDROID_PROJECT_ROOT={root}')
    return 0

if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f'ERROR: {exc}', file=sys.stderr)
        raise SystemExit(1)
