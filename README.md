# Samurai vs Zombie DTP

Proyek Unity **2017.4.40f1** (hasil ekspor Samurai vs Zombies).

- Nama aplikasi: `Samurai vs Zombie DTP`
- Package Android: `com.dtp.samuraivszombie2` (beda dari proyek lain, jadi bisa terpasang berdampingan)
- Backend: IL2CPP, tanda tangan tetap `Keystore/dtp.keystore`
  (alias `dtpkey`, password `dtp123456`). Pengaturan ini terisi otomatis saat Unity dibuka
  lewat `Assets/Editor/AndroidSigning.cs`; bisa juga lewat menu **DTP > Terapkan Keystore Android**.

## Build
1. Buka folder ini dengan Unity Hub memakai versi 2017.4.40f1 + modul Android Build Support (SDK, NDK, OpenJDK).
2. File > Build Settings > Android > Build.

CI: `.github/workflows/build-android.yml` (isi secrets UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD).
