# Samurai vs Zombie DTP 2

Package: `com.dtp.samuraivszombie` · Min SDK 24 (Android 7.0+) · Target SDK 35 · Unity 6000.0.47f1 · IL2CPP ARMv7+ARM64

Build APK: GitHub Actions → "Build Android DTP 2" → Run workflow (butuh secret UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD).

## Controls (Configurable)

## Controls (Configurable)
|Control|Bind(s)|Default Key(s)|
|-------|----|--------------|
|Move Left|Move Left|`A`, `Left Arrow`|
|Move Right|Move Right|`D`, `Right Arrow`|
|Spawn Ally|Spawn Ally `X`|`1`, `2`, `3`, `4`, `5`|
|Pause / Back|Pause|`Escape`, `Return/Enter`|
|Attack|Attack|Space/Mouse 0|
|Upgrade Leadership|Upgrade Leadership|`Q`, `Left Shift`|
|Use Ability|Ability `X`|`E`, `R`, `T`, `Y`|

## Maintenance Mode (Supabase)

- Game: `Assets/Scripts/Assembly-CSharp/MaintenanceGate.cs` + `SupabaseConfig.cs` (otomatis aktif, tidak perlu dipasang di scene).
- Server: `supabase/migrations/20261126000000_maintenance.sql` (jalankan sekali di SQL Editor).
- Edge Function admin: `supabase/functions/maintenance-admin/` (lihat README di dalamnya).
