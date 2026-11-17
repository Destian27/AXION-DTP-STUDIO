// Add this inside the app module's android { } block to generate ABI APKs
// plus one combined Universal APK (requires matching native libraries).
splits {
    abi {
        isEnable = true
        reset()
        include("arm64-v8a", "armeabi-v7a", "x86", "x86_64")
        isUniversalApk = true
    }
}
