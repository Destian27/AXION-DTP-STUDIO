using UnityEditor;

// Mengisi password keystore otomatis (Unity 2017.4 tidak menyimpan password di ProjectSettings),
// supaya build di Unity Cloud langsung bertanda tangan tetap dan APK bisa diinstal/ditimpa.
[InitializeOnLoad]
public static class DTPSigning
{
    static DTPSigning()
    {
#if UNITY_2019_1_OR_NEWER
        PlayerSettings.Android.useCustomKeystore = true;
#endif
        PlayerSettings.Android.keystoreName = "Keystore/dtp.keystore";
        PlayerSettings.Android.keystorePass = "dtp123456";
        PlayerSettings.Android.keyaliasName = "dtpkey";
        PlayerSettings.Android.keyaliasPass = "dtp123456";
    }
}
