#!/usr/bin/env python3
"""Prepare an Android Gradle project from a folder, a ZIP in the repo, or a Google Drive download.

Archives (zip, tar, tar.gz/tgz, tar.bz2, tar.xz, 7z, rar) are extracted safely and automatically,
including archives nested inside other archives, until a Gradle Android project is found.
"""
from __future__ import annotations
import argparse
import os
import shutil
import stat
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path, PurePosixPath

SKIP_DIRS = {'.git', '.gradle', 'build', 'node_modules', '.idea', '.dart_tool', '__MACOSX'}
MARKERS = ('settings.gradle', 'settings.gradle.kts')
ARCHIVE_SUFFIXES = ('.zip', '.tar', '.tar.gz', '.tgz', '.tar.bz2', '.tbz2', '.tar.xz', '.txz', '.7z', '.rar')
MAX_TOTAL_BYTES = 3 * 1024 * 1024 * 1024
MAX_NESTED_ROUNDS = 6


class Budget:
    """Tracks the total extracted size across every archive."""

    def __init__(self, limit: int = MAX_TOTAL_BYTES) -> None:
        self.limit = limit
        self.total = 0

    def add(self, size: int) -> None:
        self.total += size
        if self.total > self.limit:
            raise RuntimeError('Ukuran hasil ekstraksi melebihi batas 3 GiB.')


def find_project_root(base: Path, required: bool = True) -> Path | None:
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
        if not required:
            return None
        raise RuntimeError(f"Tidak menemukan project Gradle Android di {base}. Cari settings.gradle(.kts) atau gradlew.")
    candidates.sort(key=lambda p: (len(p.relative_to(base).parts), 0 if any((p/m).exists() for m in MARKERS) else 1))
    return candidates[0]


def is_archive(path: Path) -> bool:
    return path.name.lower().endswith(ARCHIVE_SUFFIXES) and not path.name.startswith('._')


def _check_member_path(name: str, destination: Path) -> Path:
    name = name.replace('\\', '/')
    pure = PurePosixPath(name)
    if pure.is_absolute() or '..' in pure.parts:
        raise RuntimeError(f"Arsip berisi path tidak aman: {name}")
    target = (destination / Path(*pure.parts)).resolve()
    root = destination.resolve()
    if root not in target.parents and target != root:
        raise RuntimeError(f"Arsip mencoba keluar dari folder ekstraksi: {name}")
    return target


def safe_extract_zip(zip_path: Path, destination: Path, budget: Budget) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as archive:
        for info in archive.infolist():
            mode = (info.external_attr >> 16) & 0xFFFF
            if stat.S_ISLNK(mode):
                raise RuntimeError(f"ZIP berisi symbolic link yang tidak diizinkan: {info.filename}")
            budget.add(info.file_size)
            target = _check_member_path(info.filename, destination)
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(info, 'r') as src, target.open('wb') as dst:
                    shutil.copyfileobj(src, dst)


def safe_extract_tar(tar_path: Path, destination: Path, budget: Budget) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(tar_path) as archive:
        for member in archive:
            if not (member.isfile() or member.isdir()):
                raise RuntimeError(f"TAR berisi entri non-reguler (link/device) yang tidak diizinkan: {member.name}")
            budget.add(member.size)
            target = _check_member_path(member.name, destination)
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            src = archive.extractfile(member)
            if src is None:
                continue
            with src, target.open('wb') as dst:
                shutil.copyfileobj(src, dst)


def extract_with_7z(archive_path: Path, destination: Path, budget: Budget) -> None:
    exe = shutil.which('7z') or shutil.which('7zz') or shutil.which('7za')
    if not exe:
        raise RuntimeError(f"Format {archive_path.suffix} membutuhkan 7-Zip, tetapi tidak terpasang. "
                           "Gunakan ZIP atau TAR.GZ.")
    destination.mkdir(parents=True, exist_ok=True)
    subprocess.run([exe, 'x', '-y', f'-o{destination}', str(archive_path)],
                   check=True, stdout=subprocess.DEVNULL)
    root = destination.resolve()
    for current, dirs, files in os.walk(destination):
        for name in dirs + files:
            path = Path(current) / name
            if path.is_symlink():
                raise RuntimeError(f"Arsip berisi symbolic link yang tidak diizinkan: {path.relative_to(destination)}")
            resolved = path.resolve()
            if root not in resolved.parents and resolved != root:
                raise RuntimeError(f"Arsip mencoba keluar dari folder ekstraksi: {path.relative_to(destination)}")
        for name in files:
            budget.add((Path(current) / name).stat().st_size)


def extract_archive(archive_path: Path, destination: Path, budget: Budget) -> None:
    lower = archive_path.name.lower()
    try:
        if lower.endswith('.zip'):
            safe_extract_zip(archive_path, destination, budget)
        elif lower.endswith(('.tar', '.tar.gz', '.tgz', '.tar.bz2', '.tbz2', '.tar.xz', '.txz')):
            safe_extract_tar(archive_path, destination, budget)
        else:
            extract_with_7z(archive_path, destination, budget)
    except RuntimeError:
        raise
    except (zipfile.BadZipFile, tarfile.TarError, subprocess.CalledProcessError, OSError) as exc:
        raise RuntimeError(f"Gagal mengekstrak {archive_path.name}: {exc}. "
                           "Arsip rusak, terenkripsi (berpassword), atau tidak lengkap.") from exc


def _archive_stem(path: Path) -> str:
    name = path.name
    lower = name.lower()
    for suffix in sorted(ARCHIVE_SUFFIXES, key=len, reverse=True):
        if lower.endswith(suffix):
            return name[:-len(suffix)] or 'archive'
    return path.stem


def find_archives(base: Path) -> list[Path]:
    found: list[Path] = []
    for current, dirs, files in os.walk(base):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and not d.startswith('.')]
        for name in files:
            path = Path(current) / name
            if is_archive(path):
                found.append(path)
    return sorted(found, key=lambda p: (len(p.relative_to(base).parts), str(p).lower()))


def resolve_root_with_archives(base: Path, budget: Budget) -> Path:
    """Find the Gradle project root; extract archives one by one (also nested) until it appears."""
    base = base.resolve()
    for _ in range(MAX_NESTED_ROUNDS + 1):
        root = find_project_root(base, required=False)
        if root is not None:
            return root
        archives = find_archives(base)
        if not archives:
            break
        archive = archives[0]
        dest = archive.parent / _archive_stem(archive)
        counter = 2
        while dest.exists():
            dest = archive.parent / f'{_archive_stem(archive)}_{counter}'
            counter += 1
        print(f'Mengekstrak: {archive.relative_to(base)} -> {dest.relative_to(base)}')
        extract_archive(archive, dest, budget)
        archive.unlink()
    root = find_project_root(base, required=False)
    if root is None:
        raise RuntimeError(f"Tidak menemukan project Gradle Android di {base} (termasuk di dalam arsip). "
                           "Pastikan ada settings.gradle(.kts) atau gradlew.")
    return root


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--workspace', required=True)
    parser.add_argument('--project-dir', default='.')
    parser.add_argument('--project-zip', default='')
    parser.add_argument('--source-dir', default='', help='Folder hasil unduhan Google Drive (prioritas tertinggi)')
    args = parser.parse_args()
    workspace = Path(args.workspace).resolve()
    budget = Budget()

    if args.source_dir.strip():
        source = Path(args.source_dir).resolve()
        if not source.is_dir():
            raise RuntimeError(f'Folder unduhan Drive tidak ditemukan: {source}')
        root = resolve_root_with_archives(source, budget)
        print(f'Sumber: Google Drive ({source})')
    elif args.project_zip.strip():
        zip_path = (workspace / args.project_zip).resolve()
        if workspace not in zip_path.parents:
            raise RuntimeError('project_zip harus berada di dalam repository.')
        if not zip_path.is_file() or not is_archive(zip_path):
            raise RuntimeError(f'Arsip tidak ditemukan atau formatnya tidak didukung: {zip_path}')
        extract_dir = Path(os.environ.get('RUNNER_TEMP', '/tmp')) / 'ultimate-android-source'
        if extract_dir.exists():
            shutil.rmtree(extract_dir)
        extract_archive(zip_path, extract_dir, budget)
        root = resolve_root_with_archives(extract_dir, budget)
        print(f'Arsip diekstrak ke: {extract_dir}')
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
