#!/usr/bin/env python3
"""Produce a concise failure hint from a Gradle build log."""
from __future__ import annotations
import argparse
import re
from pathlib import Path

RULES = [
    ('JDK tidak cocok', r'Android Gradle plugin requires Java|Unsupported class file major version|Could not determine java version'),
    ('Android SDK/platform belum tersedia', r'failed to find target|platforms;android-|SDK location not found|Failed to find Build Tools revision'),
    ('Gradle/AGP tidak kompatibel', r'requires Gradle version|Minimum supported Gradle version|incompatible version of Gradle'),
    ('Dependency atau koneksi repository', r'Could not resolve|Could not GET|Could not download|Read timed out|Connection timed out'),
    ('Resource atau Manifest Android', r'AAPT2?|Manifest merger failed|resource .* not found|Android resource linking failed'),
    ('Kompilasi Kotlin/Java', r'\.kt:\d+:\d+|\.java:\d+: error:|Compilation error'),
    ('NDK/CMake/native build', r'CMake Error|NDK not configured|No version of NDK|externalNativeBuild'),
    ('Memori runner tidak cukup', r'OutOfMemoryError|GC overhead limit exceeded|Java heap space'),
]

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--log', required=True)
    args = p.parse_args()
    log = Path(args.log)
    text = log.read_text(errors='replace') if log.exists() else ''
    lines = []
    lines += ['## Android build diagnostics', '']
    if not text:
        lines.append('Log build tidak tersedia atau kosong. Periksa output step Gradle di Actions.')
    else:
        hits = []
        for label, pattern in RULES:
            matches = [line.strip() for line in text.splitlines() if re.search(pattern, line, re.I)]
            if matches:
                hits.append((label, matches[-3:]))
        if hits:
            for label, matches in hits:
                lines.append(f'### {label}')
                for line in matches:
                    lines.append(f'- `{line[:350]}`')
                lines.append('')
        else:
            lines.append('Tidak ada pola error umum yang cocok. Lihat `dist/logs/build.log` pada artifact.')
        tail = [x.strip() for x in text.splitlines() if x.strip()][-25:]
        lines += ['### Baris log terakhir', '```text', *tail, '```']
    summary = '\n'.join(lines)
    print(summary)
    import os
    summary_path = os.environ.get('GITHUB_STEP_SUMMARY')
    if summary_path:
        with open(summary_path, 'a', encoding='utf-8') as f:
            f.write(summary + '\n')

if __name__ == '__main__':
    main()
