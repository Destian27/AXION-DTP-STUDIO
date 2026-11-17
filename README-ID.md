# ULTIMATE ANDROID CI/CD — Paket GitHub Actions

Paket ini menyatukan alur build Android berbasis Gradle, setup Android SDK, cache Gradle, signing APK/AAB, verifikasi signature, checksum SHA-256, GitHub Actions Artifacts, dan GitHub Releases.

## Isi paket

- `.github/workflows/android-ultimate.yml` — workflow utama.
- `scripts/prepare_project.py` — memakai project folder atau mengekstrak ZIP source secara aman.
- `scripts/detect_toolchain.py` — mendeteksi AGP, Gradle Wrapper, compileSdk, JDK, NDK, dan CMake.
- `scripts/collect_build_outputs.py` — mengumpulkan APK/AAB dari semua module.
- `scripts/diagnose_failure.py` — memberi petunjuk dari log Gradle.
- `scripts/verify_signed_release.sh` — helper verifikasi APK/AAB untuk penggunaan lokal; workflow menjalankan verifikasi langsung pada job Release.
- `templates/abi-splits.gradle` dan `.gradle.kts` — contoh konfigurasi ABI APK + Universal APK.

## Cara memasang ke repository Android

1. Ekstrak ZIP ini.
2. Salin isi ZIP (folder `.github`, `scripts`, `templates`, `input`, dan `README-ID.md`) ke root repository aplikasi Android. Jangan menaruh folder induk ZIP sebagai subfolder baru.
3. Pastikan root project berisi `settings.gradle` atau `settings.gradle.kts`, Gradle build file, dan sebaiknya `gradlew` serta `gradle/wrapper/`.
4. Commit dan push ke GitHub.
5. Buka tab **Actions** → **Ultimate Android CI-CD** → **Run workflow**.
6. Untuk source project di root, biarkan `project_dir` = `.` dan `project_zip` kosong. Untuk source ZIP yang sudah di-commit, taruh di `input/` lalu isi `project_zip`, misalnya `input/aplikasi-android.zip`.

Workflow ini ditujukan untuk aplikasi Android native berbasis Gradle (Java/Kotlin, XML Views, dan NDK/CMake opsional). Workflow tidak memodifikasi source project secara otomatis.

## Mode build

- `debug`: build APK Debug.
- `release`: jalankan test, build APK Release dan AAB Release.
- `all`: jalankan test, lint, Debug APK, Release APK, dan Release AAB.

Hasil build (termasuk log dan laporan) disimpan sebagai **Actions Artifact** bernama `android-build-output`.

## Signing dan GitHub Release

Signing/release dijalankan terpisah dari job build. Job build tidak menerima keystore atau password, sehingga source build scripts tidak memperoleh signing secrets.
Untuk alur ini, konfigurasi `release` Gradle sebaiknya menghasilkan APK/AAB **unsigned**. Bila project sudah memasang `signingConfig` dan melakukan signing sendiri, lepaskan signing tersebut dari task Release untuk menghindari proses signing ganda. Jangan memasukkan password ke build script atau source ZIP.

### Buat keystore sendiri

Gunakan JDK `keytool` di komputer/Termux yang tepercaya. Simpan file JKS dengan aman dan jangan commit ke repository.

```bash
keytool -genkeypair -v \
  -keystore release.jks \
  -storetype JKS \
  -keyalg RSA -keysize 4096 \
  -validity 10000 \
  -alias release
```

Encode ke Base64 (Linux/Termux):

```bash
base64 -w 0 release.jks > release.jks.base64.txt
```

Tambahkan empat **Repository Secrets** di GitHub: **Settings → Secrets and variables → Actions → New repository secret**.

| Nama Secret | Isi |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Seluruh isi `release.jks.base64.txt` |
| `ANDROID_KEYSTORE_PASSWORD` | Password keystore |
| `ANDROID_KEY_ALIAS` | Alias, contoh `release` |
| `ANDROID_KEY_PASSWORD` | Password private key |

Jangan pernah mengunggah `.jks`, file Base64, atau password ke repository. Jangan gunakan keystore contoh/test dari repository pihak lain. Kehilangan keystore dapat membuat pembaruan aplikasi tidak bisa ditandatangani dengan identitas aplikasi yang sama.

### Cara membuat Release

**Otomatis:** push Git tag seperti `v1.0.0`. Contoh:

```bash
git tag v1.0.0
git push origin v1.0.0
```

**Manual:** Actions → Ultimate Android CI-CD → Run workflow, pilih `build_mode` = `release` atau `all`, aktifkan `publish_release`, lalu isi `release_tag` seperti `v1.0.0`.

Job publish menandatangani APK/AAB dengan Secrets, memverifikasi signature, membuat SHA-256, menyimpan artifact `android-signed-release`, lalu membuat GitHub Release.

## Universal APK dan ABI

Workflow dapat mengunggah semua APK yang dibuat Gradle, tetapi **tidak dapat memaksa satu proyek menghasilkan Universal APK hanya dari workflow**. Output ditentukan konfigurasi Android Gradle Plugin dalam module aplikasi.

Untuk membuat APK ABI terpisah sekaligus satu Universal APK, tambahkan isi salah satu file template ke dalam blok `android { }` pada `app/build.gradle` atau `app/build.gradle.kts`:

- Groovy: `templates/abi-splits.gradle`
- Kotlin DSL: `templates/abi-splits.gradle.kts`

Daftar ABI contoh: `arm64-v8a`, `armeabi-v7a`, `x86`, `x86_64`. Universal APK hanya dapat memuat native library untuk ABI yang benar-benar tersedia pada project. Bila aplikasi tidak memakai native library dan ABI split tidak diperlukan, APK Release biasa umumnya sudah menjadi satu APK.

## Konfigurasi untuk proyek tertentu

- `java_version`: pilih `auto` (deteksi AGP/Gradle) atau override `8`, `11`, `17`, `21`, `25`.
- `gradle_version`: dipakai jika Gradle Wrapper tidak tersedia. Untuk kompatibilitas terbaik, tetap sertakan Gradle Wrapper dari proyek.
- `compile_sdk`: isi angka SDK jika deteksi otomatis tidak menemukan variabel compileSdk yang didefinisikan lewat property eksternal.
- `ndk_version`: override NDK version jika project membutuhkan versi tertentu.
- `build_tools_version`: default `35.0.0`; workflow menginstalnya untuk APK signing.

## Batasan dan hal yang harus diperhatikan

1. Ini adalah workflow universal untuk **project Gradle Android**, bukan jaminan semua source Android dapat dibangun. Error pada source code, plugin, versi AGP/Gradle, dependency privat, atau konfigurasi aplikasi tetap harus diperbaiki pada project tersebut.
2. Build APK Universal/Multi-ABI membutuhkan konfigurasi Gradle aplikasi, dan library native yang sesuai untuk masing-masing ABI.
3. Proyek Flutter, React Native, Cordova, atau proyek yang tidak memakai Gradle native Android tidak termasuk target utama paket ini.
4. Source ZIP harus berada di repository (contoh `input/project.zip`) sebelum workflow dijalankan; halaman Run workflow GitHub tidak menerima unggahan file ZIP langsung.
5. Pada workflow manual, `project_zip` harus path relatif dari root repository.
6. `publish_release` membutuhkan keempat Secrets signing. Push tag `v*` juga akan menjalankan signing/release.
7. Action pihak ketiga dipanggil dari repository resminya pada versi yang dicantumkan di workflow. Untuk keamanan tertinggi di organisasi, pin semua Action eksternal ke commit SHA setelah melakukan audit dan update berkala.
8. Build output dari source ZIP tidak dipercaya otomatis. Job build tidak diberi keystore/password atau permission tulis Release. Hanya job publish yang memegang signing secrets.

## Action yang digunakan

- `actions/checkout@v7`
- `actions/setup-python@v7`
- `actions/setup-java@v6`
- `android-actions/setup-android@v4`
- `gradle/actions/setup-gradle@v6`
- `actions/upload-artifact@v7`
- `actions/download-artifact@v8`
- `r0adkll/sign-android-release@v1`
- `softprops/action-gh-release@v3.0.4`

Paket ini mengintegrasikan fungsi dari repository yang kamu berikan melalui workflow dan script integrasi. Paket ini tidak menyalin seluruh `node_modules`, contoh aplikasi, file test binary, cache, atau source internal dari setiap action ke repository aplikasimu.
