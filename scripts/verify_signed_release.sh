#!/usr/bin/env bash
set -euo pipefail
RELEASE_DIR="${1:?Usage: verify_signed_release.sh RELEASE_DIR BUILD_TOOLS_VERSION}"
BUILD_TOOLS_VERSION="${2:?Usage: verify_signed_release.sh RELEASE_DIR BUILD_TOOLS_VERSION}"
ANDROID_HOME="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}}"
APKSIGNER="$ANDROID_HOME/build-tools/$BUILD_TOOLS_VERSION/apksigner"
if [[ ! -x "$APKSIGNER" ]]; then
  echo "apksigner tidak ditemukan: $APKSIGNER" >&2
  exit 1
fi
shopt -s nullglob
APK_FILES=("$RELEASE_DIR"/*-signed.apk)
AAB_FILES=("$RELEASE_DIR"/*.aab)
if (( ${#APK_FILES[@]} == 0 && ${#AAB_FILES[@]} == 0 )); then
  echo 'Tidak ada file APK/AAB signed untuk diverifikasi.' >&2
  exit 1
fi
for apk in "${APK_FILES[@]}"; do
  echo "Verifying APK: $apk"
  "$APKSIGNER" verify --verbose --print-certs "$apk"
done
for aab in "${AAB_FILES[@]}"; do
  echo "Verifying AAB: $aab"
  jarsigner -verify -verbose -certs "$aab" >/dev/null
  echo "AAB signature OK: $aab"
done
