#!/usr/bin/env python3
"""Download a public Google Drive file or folder with gdown.

Supported links:
  https://drive.google.com/drive/folders/<ID>        (folder)
  https://drive.google.com/file/d/<ID>/view          (single file, e.g. a ZIP)
  https://drive.google.com/open?id=<ID>
  https://drive.google.com/uc?id=<ID>&export=download
  <ID>                                               (bare file ID)

The link must be shared as "Anyone with the link" (Siapa saja yang memiliki link).
"""
from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path
from urllib.parse import parse_qs, urlparse

ALLOWED_HOSTS = {
    'drive.google.com',
    'docs.google.com',
    'drive.usercontent.google.com',
}
ID_CHARS = r'[A-Za-z0-9_-]{10,}'
FOLDER_RE = re.compile(rf'/folders/({ID_CHARS})')
FILE_RE = re.compile(rf'/file/d/({ID_CHARS})')
BARE_ID_RE = re.compile(rf'^{ID_CHARS}$')

HINT = (
    "Pastikan link Google Drive dibagikan sebagai 'Siapa saja yang memiliki link' (Anyone with the link). "
    "Jika muncul 'Too many users have viewed or downloaded this file recently', tunggu beberapa jam "
    "atau salin file ke Drive milikmu lalu bagikan link yang baru."
)


def parse_drive_url(raw: str) -> tuple[str, str]:
    """Return (kind, id) where kind is 'folder' or 'file'."""
    raw = raw.strip()
    if not raw:
        raise ValueError('Link Google Drive kosong.')
    if BARE_ID_RE.match(raw):
        return 'file', raw
    parsed = urlparse(raw)
    if parsed.scheme not in ('http', 'https') or parsed.hostname not in ALLOWED_HOSTS:
        raise ValueError(f'Hanya link Google Drive yang diizinkan (drive.google.com). Nilai: {raw}')
    match = FOLDER_RE.search(parsed.path)
    if match:
        return 'folder', match.group(1)
    match = FILE_RE.search(parsed.path)
    if match:
        return 'file', match.group(1)
    query_id = parse_qs(parsed.query).get('id', [''])[0]
    if query_id and re.fullmatch(ID_CHARS, query_id):
        return 'file', query_id
    raise ValueError(f'Tidak dapat mengenali ID file/folder dari link: {raw}')


def count_files(path: Path) -> int:
    return sum(1 for p in path.rglob('*') if p.is_file())


def download_with_api(kind: str, drive_id: str, out: Path) -> None:
    import gdown  # imported lazily so the CLI fallback still works if the API changes

    if kind == 'folder':
        url = f'https://drive.google.com/drive/folders/{drive_id}'
        result = gdown.download_folder(url=url, output=str(out), quiet=False, remaining_ok=True)
        if result is None:
            raise RuntimeError('gdown.download_folder tidak mengembalikan hasil.')
    else:
        url = f'https://drive.google.com/uc?id={drive_id}'
        # Trailing separator => gdown keeps the original file name from Drive.
        result = gdown.download(url=url, output=str(out) + os.sep, quiet=False, fuzzy=True)
        if result is None:
            raise RuntimeError('gdown.download tidak mengembalikan hasil.')


def download_with_cli(kind: str, drive_id: str, out: Path) -> None:
    exe = shutil.which('gdown')
    if not exe:
        raise RuntimeError('Perintah gdown tidak ditemukan di PATH.')
    if kind == 'folder':
        cmd = [exe, '--folder', f'https://drive.google.com/drive/folders/{drive_id}', '-O', str(out), '--remaining-ok']
    else:
        cmd = [exe, '--fuzzy', f'https://drive.google.com/uc?id={drive_id}', '-O', str(out) + os.sep]
    subprocess.run(cmd, check=True)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--url', required=True, help='Link Google Drive (file atau folder)')
    parser.add_argument('--out', required=True, help='Folder tujuan unduhan')
    args = parser.parse_args()

    kind, drive_id = parse_drive_url(args.url)
    out = Path(args.out).resolve()
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    print(f'Mengunduh {kind} Google Drive (ID: {drive_id}) ke {out}')

    try:
        download_with_api(kind, drive_id, out)
    except Exception as api_error:  # noqa: BLE001 - fall back to the CLI on any API failure
        print(f'Unduhan via API gagal ({api_error}); mencoba perintah gdown...', file=sys.stderr)
        shutil.rmtree(out, ignore_errors=True)
        out.mkdir(parents=True, exist_ok=True)
        download_with_cli(kind, drive_id, out)

    total = count_files(out)
    if total == 0:
        raise RuntimeError('Unduhan selesai tetapi tidak ada file yang tersimpan.')
    print(f'Selesai: {total} file diunduh.')
    if kind == 'folder' and total >= 50:
        print('Catatan: gdown membatasi folder Drive sekitar 50 file. Jika project tidak lengkap, '
              'kompres project menjadi satu ZIP lalu bagikan link file ZIP tersebut.')
    for path in sorted(out.rglob('*')):
        if path.is_file():
            print(f'  {path.relative_to(out)}  ({path.stat().st_size:,} bytes)')

    github_output = os.environ.get('GITHUB_OUTPUT')
    if github_output:
        with open(github_output, 'a', encoding='utf-8') as handle:
            handle.write(f'download_dir={out}\n')
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001
        print(f'ERROR: {exc}', file=sys.stderr)
        print(HINT, file=sys.stderr)
        raise SystemExit(1)
