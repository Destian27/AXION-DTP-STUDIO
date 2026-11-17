#!/usr/bin/env python3
"""Detect basic Android Gradle toolchain requirements and emit GitHub outputs."""
from __future__ import annotations
import argparse
import os
import re
import sys
from pathlib import Path

SKIP = {'build', '.gradle', '.git', 'node_modules', '.idea'}
AGP_PATTERNS = [
    r'com\.android\.tools\.build:gradle:([0-9][0-9A-Za-z.+-]*)',
    r'id\s*\(?\s*["\']com\.android\.application["\']\s*\)?\s*version\s*["\']([^"\']+)',
    r'id\s*\(?\s*["\']com\.android\.library["\']\s*\)?\s*version\s*["\']([^"\']+)',
    r'com-android-application\s*=\s*\{[^}]*version\s*=\s*["\']([^"\']+)',
    r'androidGradlePlugin\s*=\s*["\']([^"\']+)',
    r'agp\s*=\s*["\']([^"\']+)',
]
def files(root: Path):
    for p in root.rglob('*'):
        if not p.is_file() or any(part in SKIP for part in p.parts):
            continue
        if p.name in {'build.gradle','build.gradle.kts','settings.gradle','settings.gradle.kts','libs.versions.toml','gradle-wrapper.properties','CMakeLists.txt','Android.mk','Application.mk'}:
            yield p

def first_version(text: str, patterns: list[str]) -> str:
    for pattern in patterns:
        m = re.search(pattern, text, re.I | re.M)
        if m:
            return m.group(1).strip()
    return ''

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', required=True)
    ap.add_argument('--java-override', default='auto')
    ap.add_argument('--compile-sdk-override', default='')
    ap.add_argument('--ndk-override', default='')
    args = ap.parse_args()
    root = Path(args.root).resolve()
    if not root.is_dir():
        raise RuntimeError(f'Project root tidak ada: {root}')
    all_files = list(files(root))
    gradle_files = [p for p in all_files if p.name in {'build.gradle','build.gradle.kts','libs.versions.toml'}]
    combined = '\n'.join(p.read_text(errors='replace') for p in gradle_files)
    agp = first_version(combined, AGP_PATTERNS)
    compile_sdk = first_version(combined, [r'compileSdk(?:Version)?\s*(?:=|\s|\()?\s*(\d{2,3})'])
    ndk_ver = first_version(combined, [r'ndkVersion\s*(?:=|\s)\s*["\']([^"\']+)["\']'])
    cmake_ver = first_version(combined, [r'cmake\s*\{[^}]*version\s*=\s*["\']([^"\']+)["\']'])

    wrapper_props = root / 'gradle' / 'wrapper' / 'gradle-wrapper.properties'
    wrapper = (root / 'gradlew').is_file() and wrapper_props.is_file() and (root / 'gradle' / 'wrapper' / 'gradle-wrapper.jar').is_file()
    gradle_version = ''
    if wrapper_props.is_file():
        props = wrapper_props.read_text(errors='replace')
        m = re.search(r'distributionUrl=.*?gradle-([0-9]+(?:\.[0-9]+){1,2})-(?:all|bin)\.zip', props)
        if m:
            gradle_version = m.group(1)

    # AGP compatibility: AGP 8+ needs JDK 17; AGP 7.x needs JDK 11;
    # AGP 3.x/older typically needs JDK 8. Use explicit override when provided.
    if args.java_override and args.java_override.lower() != 'auto':
        java_version = args.java_override
    elif agp:
        major = int(agp.split('.')[0]) if agp.split('.')[0].isdigit() else 0
        java_version = '17' if major >= 8 else ('11' if major >= 4 else '8')
    elif gradle_version:
        gm = int(gradle_version.split('.')[0])
        java_version = '17' if gm >= 8 else ('11' if gm >= 7 else '8')
    else:
        java_version = '17'

    if not java_version.isdigit() or java_version not in {'8','11','17','21','25'}:
        raise RuntimeError(f'JDK override tidak didukung oleh paket ini: {java_version}')

    compile_sdk = args.compile_sdk_override.strip() or compile_sdk or '35'
    if not compile_sdk.isdigit():
        raise RuntimeError(f'compileSdk harus angka, didapat: {compile_sdk}')
    ndk_ver = args.ndk_override.strip() or ndk_ver
    has_native = any(p.name in {'CMakeLists.txt','Android.mk','Application.mk'} for p in all_files)
    # JNI source trees can be used with externalNativeBuild too.
    if (root / 'app' / 'src' / 'main' / 'cpp').exists():
        has_native = True
    if has_native and not ndk_ver:
        ndk_ver = '27.2.12479018'
    if has_native and not cmake_ver:
        cmake_ver = '3.22.1'

    values = {
        'agp_version': agp or 'unknown',
        'gradle_wrapper_version': gradle_version or 'unknown',
        'has_wrapper': 'true' if wrapper else 'false',
        'java_version': java_version,
        'compile_sdk': compile_sdk,
        'has_native': 'true' if has_native else 'false',
        'ndk_version': ndk_ver,
        'cmake_version': cmake_ver,
    }
    out = os.environ.get('GITHUB_OUTPUT')
    if out:
        with open(out, 'a', encoding='utf-8') as handle:
            for key, value in values.items():
                handle.write(f'{key}={value}\n')
    for key, value in values.items():
        print(f'{key}={value}')
    if not wrapper:
        print('NOTE: Gradle Wrapper tidak ditemukan. Workflow akan memakai versi Gradle dari input gradle_version.', file=sys.stderr)
    return 0

if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f'ERROR: {exc}', file=sys.stderr)
        raise SystemExit(1)
